using UnityEngine;

/// <summary>
/// An Arena-style character: a camera-facing quad with a generated, palette-locked texture.
///
/// This exists because the humanoid mesh is the project's hardest blocker — it needs a
/// browser, an Adobe account and taste, and W-13 proved the meshes already in the repo cannot
/// carry a Unity Humanoid rig at all. Sprites need no rig, no retargeting, no knee joints and
/// no animation system, and they are period-correct for the reference.
///
/// The texture is drawn in code, so a character is data rather than an asset.
/// </summary>
[RequireComponent(typeof(MeshRenderer))]
public sealed class BillboardActor : MonoBehaviour
{
    [SerializeField] private float height = 1.9f;
    [SerializeField] private Color tint = Color.grey;

    private Transform _billboard;
    private static Material _sharedTemplate;

    /// <summary>Build a standing actor at a world position.</summary>
    public static BillboardActor Spawn(string name, Vector3 position, Color tint, float height = 1.9f)
    {
        var root = new GameObject(name);
        root.transform.position = position;

        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "Billboard";
        quad.transform.SetParent(root.transform, false);
        quad.transform.localPosition = Vector3.up * (height * 0.5f);
        quad.transform.localScale = new Vector3(height * 0.55f, height, 1f);
        Object.Destroy(quad.GetComponent<Collider>());

        var actor = quad.AddComponent<BillboardActor>();
        actor.height = height;
        actor.tint = tint;
        actor.Apply();

        // The hit volume lives on the root, not the quad, so a rotating billboard does not
        // drag its collider around with it.
        var capsule = root.AddComponent<CapsuleCollider>();
        capsule.height = height;
        capsule.radius = 0.4f;
        capsule.center = Vector3.up * (height * 0.5f);

        WorldTagger.SetLayerRecursive(root, GameLayers.Npc);
        return actor;
    }

    private void Apply()
    {
        var renderer = GetComponent<MeshRenderer>();
        renderer.sharedMaterial = MaterialFor(tint);
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _billboard = transform;
    }

    private void LateUpdate()
    {
        // Yaw only. A quad that pitches toward the camera reads as a sheet of paper the
        // moment the player looks up or down at it.
        var camera = Camera.main;
        if (camera == null || _billboard == null) return;

        var to = _billboard.position - camera.transform.position;
        to.y = 0f;
        if (to.sqrMagnitude < 0.0001f) return;
        _billboard.rotation = Quaternion.LookRotation(to.normalized, Vector3.up);
    }

    /// <summary>
    /// A crude standing figure drawn in code: head, torso, legs, in the actor's tint against
    /// transparency. Deliberately simple — it is a silhouette, and the art direction lock says
    /// silhouette reads at any fidelity while surface detail does not.
    /// </summary>
    private static Material MaterialFor(Color tint)
    {
        const int size = 32;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        var clear = new Color(0f, 0f, 0f, 0f);
        var dark = tint * 0.55f; dark.a = 1f;
        var body = tint; body.a = 1f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size;
                float v = (y + 0.5f) / size;
                float dx = Mathf.Abs(u - 0.5f);

                Color c = clear;
                if (v > 0.80f && dx < 0.11f) c = dark;                      // head
                else if (v > 0.42f && v <= 0.80f && dx < 0.20f) c = body;    // torso
                else if (v <= 0.42f && dx < 0.16f && dx > 0.03f) c = dark;   // legs

                texture.SetPixel(x, y, c);
            }
        }
        texture.Apply();

        if (_sharedTemplate == null)
            _sharedTemplate = new Material(
                Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Transparent"));

        var material = new Material(_sharedTemplate) { name = "M_Billboard" };
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
        else material.mainTexture = texture;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);

        // Alpha-clip rather than blend: sprites that sort against each other are the classic
        // way a crowd starts flickering.
        material.EnableKeyword("_ALPHATEST_ON");
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 0f);
        if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 1f);
        if (material.HasProperty("_Cutoff")) material.SetFloat("_Cutoff", 0.5f);
        material.renderQueue = 2450;
        return material;
    }
}
