// Cave lighting: one directional fill, plus up to eight point lights with real falloff.
//
// This exists because BasicEffect has no point lights and a generated mine cannot bake any.
// Every torch in the game was previously an additive quad pasted onto the wall behind it,
// which is convincing in a screenshot and falls apart the moment the camera moves: the smear
// does not wrap a corner, does not dim with distance correctly, and lights the ceiling and the
// floor identically because it has no idea which way either of them faces.
//
// Kept deliberately small. Per-pixel lambert, distance falloff, no specular, no shadows. The
// art direction is flat pigment with hard light; anything more would be paying for a look the
// game does not want.

// Four, not eight.
//
// A level 9.3 pixel shader gets 32 constant registers and no more. Eight lights as three
// separate arrays wanted 24 of them before anything else was declared, and the compiler
// rejected it outright. Four is not a compromise for a room lit by torches — it is more
// torches than a cave room should have — and packing the range into the colour's w costs
// nothing and leaves headroom for whatever the shader needs next.
#define MAX_POINT_LIGHTS 4

matrix World;
matrix View;
matrix Projection;

// World-space inverse transpose would be correct for non-uniform scale. Every box in this game
// is axis-aligned and scaled per axis, which does skew normals — but the faces are all axis
// aligned too, so the skew lands along the normal itself and comes out in the wash. Passing
// World and normalising is enough here and costs one matrix.
matrix WorldInverseTranspose;

float3 DiffuseColour = float3(1, 1, 1);
float3 AmbientColour = float3(0.12, 0.12, 0.14);

float3 KeyDirection = float3(0.3, -0.8, 0.4);
float3 KeyColour = float3(0.25, 0.25, 0.30);

// Where the eye is, used only to decide which way a surface is really facing.
float3 CameraPosition = float3(0, 0, 0);

int PointCount = 0;
float3 PointPosition[MAX_POINT_LIGHTS];

// rgb is the light's colour; w is the radius at which it reaches zero. Falloff is smooth to
// that point and hard zero past it, so a light that cannot reach a surface costs nothing on it.
float4 PointColour[MAX_POINT_LIGHTS];

texture Surface;
sampler2D SurfaceSampler = sampler_state
{
    Texture = <Surface>;
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = Linear;
    AddressU = Wrap;
    AddressV = Wrap;
};

struct VertexInput
{
    float4 Position : POSITION0;
    float3 Normal   : NORMAL0;
    float2 Texture  : TEXCOORD0;
};

struct VertexOutput
{
    float4 Position      : SV_POSITION;
    float2 Texture       : TEXCOORD0;
    float3 WorldPosition : TEXCOORD1;
    float3 Normal        : TEXCOORD2;
};

VertexOutput MainVS(VertexInput input)
{
    VertexOutput output;

    float4 world = mul(input.Position, World);
    output.WorldPosition = world.xyz;
    output.Position = mul(mul(world, View), Projection);
    output.Texture = input.Texture;
    output.Normal = normalize(mul(input.Normal, (float3x3)WorldInverseTranspose));

    return output;
}

float4 MainPS(VertexOutput input) : COLOR0
{
    float3 normal = normalize(input.Normal);

    // Two-sided. The cube geometry carries normals that agree with its winding rather than
    // with its faces — a convention that is fine for BasicEffect, where the light direction was
    // tuned against it, and useless here, where the light is a position in the room and the
    // maths has to be right. Flipping any normal that points away from the eye makes the
    // lighting correct no matter which convention the geometry was built with, and costs one
    // dot product. It is also simply what double-sided geometry needs.
    float3 toEye = CameraPosition - input.WorldPosition;
    if (dot(normal, toEye) < 0.0) normal = -normal;

    // Directional fill only, deliberately weak. In a mine the torches are the light; the
    // directional term exists so that a surface no torch reaches is dim rather than black.
    float key = saturate(dot(normal, -normalize(KeyDirection)));
    float3 light = AmbientColour + KeyColour * key;

    [unroll]
    for (int i = 0; i < MAX_POINT_LIGHTS; i++)
    {
        if (i >= PointCount) break;

        float3 toLight = PointPosition[i] - input.WorldPosition;
        float distance = length(toLight);
        float range = max(0.001, PointColour[i].w);

        // Smooth to zero at the range rather than inverse-square to infinity. Inverse square is
        // physically right and practically wrong here: it never quite reaches zero, so every
        // torch faintly lights the whole mine and the darkness the game runs on disappears.
        float attenuation = saturate(1.0 - distance / range);
        attenuation = attenuation * attenuation;

        float lambert = saturate(dot(normal, toLight / max(distance, 0.001)));
        light += PointColour[i].rgb * lambert * attenuation;
    }

    float4 surface = tex2D(SurfaceSampler, input.Texture);
    return float4(surface.rgb * DiffuseColour * light, surface.a);
}

// Shader model 4.0 level 9.3, not 3.0.
//
// WindowsDX refuses anything below SM 4.0 level 9.1, and the content pipeline is on the Reach
// profile, which refuses anything above level 9.3. That leaves exactly one target, and it is
// why the point loop is unrolled against a compile-time maximum rather than run to PointCount:
// level 9.3 has no dynamic looping, so the count has to be a branch inside an unrolled body.
technique CaveLit
{
    pass P0
    {
        VertexShader = compile vs_4_0_level_9_3 MainVS();
        PixelShader  = compile ps_4_0_level_9_3 MainPS();
    }
}
