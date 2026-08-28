using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace RatnaBay.Client;

/// <summary>
/// The moodboard, stambha trailer shot and generated-asset case.
///
/// Game-specific lighting studies, not engine. They use <see cref="SceneRenderer"/> and
/// <see cref="BillboardRenderer"/> the same way a real room does, with different lights.
/// A second game does not take these.
/// </summary>
internal sealed class SpikeScenes
{
    /// <summary>
    /// The trailer's opening shot, in engine.
    ///
    /// A dark cave, one jiva stone glowing, and its light raking across a carved Stambha. Flat
    /// pigment with a single hard light source is a look; flat pigment with even lighting is a
    /// placeholder, which is the whole reason the scene is lit this way.
    /// </summary>
    public void DrawStambha(
        GraphicsDevice device,
        SceneRenderer scene,
        FirstPersonView camera,
        BasicEffect primitiveEffect,
        UiCanvas ui)
    {
        // Framed as the trailer's opening: close on the pillar, the stone low and left.
        camera.Position = new Vector3(0.35f, 0.45f, 3.15f);
        camera.Pitch = 0.06f;
        camera.Yaw = 0f;
        camera.RebuildView();

        device.Clear(new Color(14, 13, 16));
        device.DepthStencilState = DepthStencilState.Default;
        device.RasterizerState = RasterizerState.CullCounterClockwise;

        // The stone is the light. BasicEffect has no point lights, so it is faked with a warm
        // directional raking up from where the stone sits, the key light killed, and the
        // ambient dropped hard. Flat pigment with one hard source is a look; flat pigment
        // under even light is a placeholder.
        var ambient = primitiveEffect.AmbientLightColor;
        var keyDirection = primitiveEffect.DirectionalLight0.Direction;
        var keyColour = primitiveEffect.DirectionalLight0.DiffuseColor;
        var fillEnabled = primitiveEffect.DirectionalLight1.Enabled;
        var backEnabled = primitiveEffect.DirectionalLight2.Enabled;
        var perPixel = primitiveEffect.PreferPerPixelLighting;

        primitiveEffect.AmbientLightColor = new Vector3(0.055f, 0.05f, 0.062f);

        // Key: the stone. It sits on the floor to the left of the pillar, so its light travels
        // up, to the right, and away from the camera — which is what puts the catch on the
        // upper lip of every cut and throws the pillar's own shadow up the back wall.
        primitiveEffect.DirectionalLight0.Enabled = true;
        primitiveEffect.DirectionalLight0.Direction =
            Vector3.Normalize(new Vector3(0.40f, 0.30f, -0.86f));
        primitiveEffect.DirectionalLight0.DiffuseColor = new Vector3(1.30f, 0.82f, 0.40f);
        primitiveEffect.DirectionalLight0.SpecularColor = Vector3.Zero;

        // Fill: cold, from the opposite side, at a tenth of the key. Without it the unlit half
        // of the pillar is pure black and the silhouette dies against the cave.
        primitiveEffect.DirectionalLight1.Enabled = true;
        primitiveEffect.DirectionalLight1.Direction =
            Vector3.Normalize(new Vector3(-0.75f, -0.25f, -0.5f));
        primitiveEffect.DirectionalLight1.DiffuseColor = new Vector3(0.10f, 0.13f, 0.22f);
        primitiveEffect.DirectionalLight1.SpecularColor = Vector3.Zero;

        // EnableDefaultLighting leaves a third grey light on, and nothing in this scene ever
        // set it. It was washing a flat neutral over every surface the key was deliberately
        // keeping dark, which is most of why the shot read as evenly lit rather than as one
        // stone in a black cave.
        primitiveEffect.DirectionalLight2.Enabled = false;

        // One hard source across faceted stone is exactly the case vertex lighting handles
        // worst; the carved band is a single quad, so per-vertex it would have no gradient
        // across it at all.
        primitiveEffect.PreferPerPixelLighting = true;

        // Cave floor and back wall, as low-poly and as flat as everything else.
        scene.DrawCube(new Vector3(0f, -1.4f, 0f), new Vector3(16f, 0.4f, 16f), new Color(44, 39, 35), 0f);
        scene.DrawCube(new Vector3(0f, 2.2f, -5.6f), new Vector3(13f, 7f, 0.4f), new Color(24, 22, 22), 0f);
        scene.DrawCube(new Vector3(-5.4f, 2.2f, -2.2f), new Vector3(0.4f, 7f, 7f), new Color(20, 19, 19), 0f);
        scene.DrawCube(new Vector3(5.4f, 2.2f, -2.2f), new Vector3(0.4f, 7f, 7f), new Color(20, 19, 19), 0f);

        // The pillar.
        //
        // It was three stacked boxes of almost the same width, which is a post rather than a
        // stambha. The silhouette is the whole read at six seconds and muted, so it is built
        // the way the real ones are: a rough footing the rock has half swallowed, a monolithic
        // shaft that tapers as it rises, the inscription band at eye height, then the bell and
        // the abacus flaring back out above it. The taper is faked in three courses because a
        // cube is the only solid this renderer has, and at this distance three is enough.
        const float ShaftZ = -3.4f;
        const float ShaftDepth = 1.2f;
        const float ShaftFrontZ = ShaftZ + ShaftDepth * 0.5f;

        var stone = StambhaCarving.ShaftStone;
        var stoneDeep = new Color(78, 72, 64);

        // Footing: wider than the shaft, sunk into the floor, and rougher.
        scene.DrawCube(new Vector3(0f, -1.30f, ShaftZ), new Vector3(2.30f, 0.44f, 1.50f), stoneDeep, 0f);
        scene.DrawCube(new Vector3(0f, -1.00f, ShaftZ), new Vector3(1.94f, 0.26f, 1.28f), new Color(86, 80, 71), 0f);

        // Shaft, in four tapering courses. A monolith has no joints, but four courses of a cube
        // is the only taper this renderer can spell, and at this framing the silhouette is what
        // carries — narrow, and rising out of the top of the frame.
        scene.DrawCube(new Vector3(0f, -0.30f, ShaftZ), new Vector3(1.54f, 1.20f, ShaftDepth), stone, 0f);
        scene.DrawCube(new Vector3(0f, 0.80f, ShaftZ), new Vector3(1.44f, 1.05f, ShaftDepth * 0.94f), stone, 0f);
        scene.DrawCube(new Vector3(0f, 1.85f, ShaftZ), new Vector3(1.34f, 1.05f, ShaftDepth * 0.88f), stone, 0f);
        scene.DrawCube(new Vector3(0f, 2.95f, ShaftZ), new Vector3(1.24f, 1.15f, ShaftDepth * 0.82f), stone, 0f);

        // Bell capital and abacus, deliberately near the top of the frame — a hint of what the
        // shaft carries rather than the whole capital, which would pull the eye off the verse.
        scene.DrawCube(new Vector3(0f, 3.62f, ShaftZ), new Vector3(1.44f, 0.20f, 1.02f), stoneDeep, 0f);
        scene.DrawCube(new Vector3(0f, 3.86f, ShaftZ), new Vector3(1.74f, 0.28f, 1.22f), new Color(96, 89, 79), 0f);
        scene.DrawCube(new Vector3(0f, 4.14f, ShaftZ), new Vector3(2.02f, 0.30f, 1.44f), stoneDeep, 0f);

        // The verse, lying on the shaft's front face at eye height and lit with it.
        var carving = StambhaCarving.Get(device, StambhaCarving.SurfaceVerse);
        if (carving is not null)
        {
            // Exactly the width of the course it sits on, so its edges are the pillar's edges.
            const float BandWidth = 1.54f;
            var bandHeight = BandWidth * carving.Height / carving.Width;

            scene.DrawCarvedFace(
                new Vector3(0f, -0.22f, ShaftFrontZ + 0.006f),
                BandWidth,
                bandHeight,
                carving);
        }

        // The jiva stone, low and to the left, so the light rakes up across the cuts. It emits
        // rather than reflects, so it is drawn emissive — otherwise the light source is the
        // darkest object in its own shot.
        var stonePosition = new Vector3(-1.28f, -1.18f, -1.55f);

        // Turned off-axis so three facets face the camera at three angles. Square on, an
        // octahedron presents one edge and two faces and reads as a flat kite.
        const float StoneSpin = 0.42f;

        scene.DrawCrystal(stonePosition, 0.34f, new Color(255, 206, 132),
            new Vector3(0.95f, 0.62f, 0.30f), StoneSpin);
        scene.DrawCrystal(stonePosition + new Vector3(0f, 0.02f, 0f), 0.17f, new Color(255, 250, 236),
            new Vector3(1f, 0.94f, 0.82f), StoneSpin);

        primitiveEffect.EmissiveColor = Vector3.Zero;

        primitiveEffect.AmbientLightColor = ambient;
        primitiveEffect.DirectionalLight0.Direction = keyDirection;
        primitiveEffect.DirectionalLight0.DiffuseColor = keyColour;
        primitiveEffect.DirectionalLight1.Enabled = fillEnabled;
        primitiveEffect.DirectionalLight2.Enabled = backEnabled;
        primitiveEffect.PreferPerPixelLighting = perPixel;

        ui.Begin();
        if (carving is null)
            ui.TextCentred("No carving font loaded", ui.LogicalWidth / 2f, 300f, 20,
                new Color(228, 128, 118));

        // The shot is meant to be cut in Brahmi. Falling back to Devanagari is legible and
        // wrong by a thousand years, so it says so rather than passing silently — this frame
        // is the microtrailer, and it should not ship in the fallback script by accident.
        if (carving is not null && !StambhaCarving.IsPeriodScript)
            ui.TextCentred("Devanagari fallback — NotoSansBrahmi not installed",
                ui.LogicalWidth / 2f, 62f, 13, new Color(150, 126, 96));

        // Lower third, and off to the right: centred, it sat on top of the jiva stone, which is
        // the one thing in the frame that has to stay clean.
        ui.TextCentred("\"Covet not \u2014 for whose is wealth?\"",
            ui.LogicalWidth * 0.63f, 606f, 20, new Color(214, 206, 190));
        ui.TextCentred("Isha Upanishad 1", ui.LogicalWidth * 0.63f, 640f, 14, new Color(140, 132, 120));
    }

    /// <summary>
    /// One room at the fidelity being argued for.
    ///
    /// Every surface and every prop in here is generated from a palette and some numbers —
    /// there is not one authored image in the scene. That is the point of it: the question is
    /// not whether hand-drawn pixel art would look good, it is how far the existing pipeline
    /// gets without hiring anyone.
    /// </summary>
    public void DrawMoodboard(
        GraphicsDevice device,
        SceneRenderer scene,
        BillboardRenderer billboards,
        FirstPersonView camera,
        BasicEffect primitiveEffect,
        List<PointLight> lights,
        StoneTextures.StonePalette stone,
        UiCanvas ui,
        float clock,
        bool assetCase)
    {
        device.Clear(new Color(10, 9, 11));
        device.DepthStencilState = DepthStencilState.Default;
        device.RasterizerState = RasterizerState.CullCounterClockwise;

        camera.Position = new Vector3(0f, 1.65f, 6.4f);
        camera.Pitch = -0.02f;
        camera.Yaw = 0f;
        camera.RebuildView();

        var ambient = primitiveEffect.AmbientLightColor;
        var keyDirection = primitiveEffect.DirectionalLight0.Direction;
        var keyColour = primitiveEffect.DirectionalLight0.DiffuseColor;
        var light2 = primitiveEffect.DirectionalLight2.Enabled;

        // Lit almost entirely by the torch. The directional pair only keeps the unlit half of
        // the room from being pure black, which a screenshot needs and a moving camera does not.
        primitiveEffect.AmbientLightColor = new Vector3(0.20f, 0.18f, 0.19f);
        primitiveEffect.DirectionalLight0.Enabled = true;
        primitiveEffect.DirectionalLight0.Direction = Vector3.Normalize(new Vector3(0.5f, 0.55f, -0.7f));
        primitiveEffect.DirectionalLight0.DiffuseColor = new Vector3(0.85f, 0.72f, 0.55f);
        primitiveEffect.DirectionalLight0.SpecularColor = Vector3.Zero;
        primitiveEffect.DirectionalLight1.Enabled = true;
        primitiveEffect.DirectionalLight1.Direction = Vector3.Normalize(new Vector3(-0.6f, -0.3f, -0.5f));
        primitiveEffect.DirectionalLight1.DiffuseColor = new Vector3(0.16f, 0.17f, 0.26f);
        primitiveEffect.DirectionalLight2.Enabled = false;

        // Real lights now, not smears on the wall. The torch is at the same place it was; the
        // difference is that the wall beside it, the floor under it and the ceiling above it
        // each work out their own share from their own normal and their own distance.
        var torch = new Vector3(-4.5f, 2.05f, -3.2f);

        lights.Clear();
        // The lamp flickers with the flame rather than independently of it. A steady light
        // beside a moving fire is worse than both being still.
        var flicker = 1f + MathF.Sin(clock * 9.3f) * 0.05f + MathF.Sin(clock * 21.7f) * 0.028f;

        lights.Add(new PointLight(torch + new Vector3(0.35f, 0f, 0f),
            new Vector3(2.35f, 1.42f, 0.62f) * flicker, 13.5f));
        lights.Add(new PointLight(new Vector3(1.6f, 1.1f, -5.6f),
            new Vector3(0.22f, 0.20f, 0.30f), 7.5f));

        scene.SetCaveAmbience(
            ambient: new Vector3(0.075f, 0.072f, 0.086f),
            keyDirection: new Vector3(0.4f, -0.75f, -0.5f),
            keyColour: new Vector3(0.16f, 0.17f, 0.23f));

        var wall = StoneTextures.Wall(device, stone);
        var floor = StoneTextures.Floor(device, stone);
        var tint = new Color(228, 224, 220);

        const float Half = 5f;
        const float Tall = 5.2f;

        scene.DrawTexturedCube(new Vector3(0f, -0.2f, 0f), new Vector3(Half * 2f, 0.4f, 14f), tint, floor, 2.0f);
        scene.DrawTexturedCube(new Vector3(0f, Tall, 0f), new Vector3(Half * 2f, 0.4f, 14f),
            new Color(150, 146, 148), floor, 2.4f);
        scene.DrawTexturedCube(new Vector3(0f, 2.4f, -6.6f), new Vector3(Half * 2f, Tall, 0.5f), tint, wall, 2.2f);
        scene.DrawTexturedCube(new Vector3(-Half, 2.4f, 0f), new Vector3(0.5f, Tall, 14f), tint, wall, 2.2f);
        scene.DrawTexturedCube(new Vector3(Half, 2.4f, 0f), new Vector3(0.5f, Tall, 14f), tint, wall, 2.2f);

        // The door, its own material, standing just proud of the wall it is set into.
        scene.DrawTexturedCube(new Vector3(1.35f, 1.45f, -6.32f), new Vector3(1.7f, 2.9f, 0.16f),
            new Color(245, 238, 230), PropTextures.Door(device), 2.9f);

        billboards.Begin(camera.View, camera.Projection);

        // Banner, torch bracket and flame are all cutout quads, which is what the whole art
        // direction already is.
        billboards.Draw(PropTextures.Banner(device),
            new Vector3(-2.6f, 1.55f, -6.28f), 2.6f, 0f, new Color(236, 226, 214));

        // Twelve frames a second. Fire read at sixty is a blur and at six is a strobe; this is
        // the rate hand-drawn fire is almost always animated at, for the same reason.
        var flameFrame = (int)(clock * 12f);
        billboards.Draw(PropTextures.Flame(device, flameFrame),
            torch, 1.35f, camera.Yaw, Color.White);

        device.DepthStencilState = DepthStencilState.Default;
        device.RasterizerState = RasterizerState.CullCounterClockwise;

        // One small glow left, tight around the flame itself. The shader lights the room;
        // this is only the bloom around the fire, which no amount of surface lighting can
        // produce because the flame is not a surface.
        scene.DrawGlow(torch, 1.15f * flicker, new Color(210, 148, 74, 255));

        primitiveEffect.AmbientLightColor = ambient;
        primitiveEffect.DirectionalLight0.Direction = keyDirection;
        primitiveEffect.DirectionalLight0.DiffuseColor = keyColour;
        primitiveEffect.DirectionalLight2.Enabled = light2;

        DrawMoodboardUi(device, ui, assetCase);
    }

    /// <summary>The interface, drawn in the same ornament as the world.</summary>
    private void DrawMoodboardUi(GraphicsDevice device, UiCanvas ui, bool assetCase)
    {
        ui.Begin();

        // Vignette first, under the interface: four bands is what DrawDamageFlash already does,
        // and at this strength it is enough to pull the eye to the middle.
        for (var i = 0; i < 5; i++)
        {
            var alpha = (byte)(20 + i * 12);
            var inset = i * 14;
            var shade = new Color((byte)8, (byte)7, (byte)9, alpha);

            ui.Fill(new Rectangle(0, inset, ui.LogicalWidth, 14), shade);
            ui.Fill(new Rectangle(0, ui.LogicalHeight - inset - 14, ui.LogicalWidth, 14), shade);
            ui.Fill(new Rectangle(inset, 0, 14, ui.LogicalHeight), shade);
            ui.Fill(new Rectangle(ui.LogicalWidth - inset - 14, 0, 14, ui.LogicalHeight), shade);
        }

        DrawFramedPanel(device, ui, new Rectangle(392, 12, 496, 54), Color.White);
        ui.TextCentred("MINE 4211  ·  DEPTH 2", 640f, 28f, 22, new Color(238, 214, 158));

        DrawFramedPanel(device, ui, new Rectangle(906, 12, 174, 54), Color.White);
        ui.Text("AWARENESS", new Vector2(924, 22), 12, new Color(196, 170, 120));
        ui.Text("UNAWARE", new Vector2(924, 40), 15, new Color(238, 232, 220));

        DrawFramedPanel(device, ui, new Rectangle(1092, 12, 176, 54), Color.White);
        ui.Text("AT RISK", new Vector2(1110, 22), 12, new Color(196, 170, 120));
        ui.Text("0", new Vector2(1110, 40), 15, new Color(238, 232, 220));

        if (assetCase) DrawAssetCase(device, ui);

        DrawFramedBar(device, ui, new Rectangle(24, 552, 330, 46), 1f, new Color(168, 44, 46), "HEALTH  100/100");
        DrawFramedBar(device, ui, new Rectangle(24, 606, 330, 46), 1f, new Color(48, 92, 172), "PRANA    80/80");
        DrawFramedBar(device, ui, new Rectangle(24, 660, 330, 46), 1f, new Color(64, 138, 66), "STAMINA 100/100");

        DrawFramedPanel(device, ui, new Rectangle(470, 590, 340, 116), Color.White);
        ui.TextCentred("READIED", 640f, 604f, 13, new Color(196, 170, 120));
        ui.TextCentred("Flame", 640f, 626f, 26, new Color(238, 178, 96));
        ui.TextCentred("16 prana", 640f, 660f, 16, new Color(150, 186, 232));
        ui.TextCentred("Q to cast", 640f, 682f, 14, new Color(214, 206, 192));

        DrawFramedPanel(device, ui, new Rectangle(926, 590, 330, 116), Color.White);
        ui.Text("LEVEL 1", new Vector2(950, 606), 20, new Color(238, 232, 220));
        ui.Text("Iron Sword", new Vector2(950, 642), 17, new Color(206, 198, 186));
        ui.Text("0 gold", new Vector2(950, 674), 17, new Color(226, 190, 108));
    }

    /// <summary>
    /// The generated-asset case, laid out as the shop it would actually be.
    ///
    /// Shown at two sizes on purpose. Icons are judged at the size they are used, and a sprite
    /// that survives being doubled is one whose form is right rather than one whose noise
    /// happens to be pleasing.
    /// </summary>
    private static void DrawAssetCase(GraphicsDevice device, UiCanvas ui)
    {
        var items = new (string Name, string Price, Texture2D Icon)[]
        {
            ("Pickaxe", "120", ItemSprites.Pickaxe(device)),
            ("Iron Sword", "150", ItemSprites.Sword(device)),
            ("Jiva Stone", "80", ItemSprites.JivaCrystal(device)),
            ("Gold Bars", "200", ItemSprites.GoldBars(device))
        };

        var panel = new Rectangle(300, 74, 680, 468);
        DrawFramedPanel(device, ui, panel, Color.White);
        ui.TextCentred("MERCHANT", panel.Center.X, panel.Y + 18f, 20, new Color(238, 214, 158));

        for (var i = 0; i < items.Length; i++)
        {
            var slot = new Rectangle(panel.X + 26 + i * 160, panel.Y + 56, 148, 168);
            DrawFramedPanel(device, ui, slot, new Color(210, 208, 206));

            ui.Batch.Draw(items[i].Icon,
                new Rectangle(slot.X + 18, slot.Y + 14, 112, 112), Color.White);

            ui.TextCentred(items[i].Name, slot.Center.X, slot.Y + 128f, 15, new Color(240, 234, 222));
            ui.TextCentred(items[i].Price + " gold", slot.Center.X, slot.Y + 148f, 14,
                new Color(232, 196, 112));
        }

        // The same four at inventory size, unscaled, beside the creature.
        var strip = new Rectangle(panel.X + 26, panel.Y + 248, 420, 120);
        DrawFramedPanel(device, ui, strip, new Color(200, 198, 196));
        ui.Text("AT 48 PIXELS", new Vector2(strip.X + 16, strip.Y + 14), 12, new Color(196, 170, 120));

        for (var i = 0; i < items.Length; i++)
            ui.Batch.Draw(items[i].Icon,
                new Rectangle(strip.X + 20 + i * 100, strip.Y + 42, 48, 48), Color.White);

        var creature = new Rectangle(panel.X + 466, panel.Y + 248, 188, 120);
        DrawFramedPanel(device, ui, creature, new Color(200, 198, 196));
        ui.Text("THE RISEN", new Vector2(creature.X + 16, creature.Y + 14), 12, new Color(196, 170, 120));

        // The three tiers side by side, which is the only way to judge whether they read as
        // one creature at three ages rather than as three unrelated things.
        var tiers = new[]
        {
            ItemSprites.ChhayaSprite(device),
            ItemSprites.VetalaSprite(device),
            ItemSprites.PishachaSprite(device)
        };

        for (var i = 0; i < tiers.Length; i++)
            ui.Batch.Draw(tiers[i],
                new Rectangle(creature.X + 14 + i * 56, creature.Y + 34, 52, 52), Color.White);

        // And the flame, every frame of it, so the cycle can be read as a strip. Fire is the
        // one thing a still sprite cannot be, and a strip is the only honest way to show that
        // the frames actually differ rather than being one image nudged sideways.
        var cycle = new Rectangle(panel.X + 26, panel.Y + 380, 628, 68);
        DrawFramedPanel(device, ui, cycle, new Color(200, 198, 196));
        ui.Text("FLAME CYCLE", new Vector2(cycle.X + 16, cycle.Y + 12), 12, new Color(196, 170, 120));

        for (var i = 0; i < PropTextures.FlameFrames; i++)
            ui.Batch.Draw(PropTextures.Flame(device, i),
                new Rectangle(cycle.X + 150 + i * 74, cycle.Y + 12, 30, 44), Color.White);
    }

    /// <summary>
    /// Draw an ornate panel by nine-slicing <see cref="PropTextures.Frame"/>.
    ///
    /// Corners are blitted at their own size and never scaled; edges stretch along one axis;
    /// the middle stretches both ways. Scaling the whole texture instead is the thing that
    /// makes a framed panel look like a stretched JPEG, and it is the single most common way
    /// an otherwise good interface gives itself away.
    /// </summary>
    private static void DrawFramedPanel(GraphicsDevice device, UiCanvas ui, Rectangle bounds, Color tint)
    {
        var frame = PropTextures.Frame(device);
        const int c = PropTextures.FrameCorner;
        var far = frame.Width - c;

        void Piece(Rectangle source, Rectangle destination) =>
            ui.Batch.Draw(frame, destination, source, tint);

        var innerWidth = Math.Max(0, bounds.Width - c * 2);
        var innerHeight = Math.Max(0, bounds.Height - c * 2);

        Piece(new Rectangle(0, 0, c, c), new Rectangle(bounds.Left, bounds.Top, c, c));
        Piece(new Rectangle(far, 0, c, c), new Rectangle(bounds.Right - c, bounds.Top, c, c));
        Piece(new Rectangle(0, far, c, c), new Rectangle(bounds.Left, bounds.Bottom - c, c, c));
        Piece(new Rectangle(far, far, c, c), new Rectangle(bounds.Right - c, bounds.Bottom - c, c, c));

        Piece(new Rectangle(c, 0, far - c, c),
            new Rectangle(bounds.Left + c, bounds.Top, innerWidth, c));
        Piece(new Rectangle(c, far, far - c, c),
            new Rectangle(bounds.Left + c, bounds.Bottom - c, innerWidth, c));
        Piece(new Rectangle(0, c, c, far - c),
            new Rectangle(bounds.Left, bounds.Top + c, c, innerHeight));
        Piece(new Rectangle(far, c, c, far - c),
            new Rectangle(bounds.Right - c, bounds.Top + c, c, innerHeight));

        Piece(new Rectangle(c, c, far - c, far - c),
            new Rectangle(bounds.Left + c, bounds.Top + c, innerWidth, innerHeight));
    }

    /// <summary>A bar inside a framed slot, drawn the way the mock-ups do it.</summary>
    private static void DrawFramedBar(GraphicsDevice device, UiCanvas ui, Rectangle bounds,
        float fraction, Color fill, string label)
    {
        DrawFramedPanel(device, ui, bounds, Color.White);

        // Emblem first, and the bar starts where the emblem ends. Laying the bar across the
        // whole panel and then putting the emblem on top of it is what made the first pass
        // look like three things fighting for the same forty pixels.
        var emblem = bounds.Height - 16;
        ui.Batch.Draw(PropTextures.Lotus(device),
            new Rectangle(bounds.X + 9, bounds.Y + 8, emblem, emblem),
            new Color(255, 236, 196));

        var track = new Rectangle(
            bounds.X + emblem + 16,
            bounds.Y + 10,
            bounds.Width - emblem - 26,
            bounds.Height - 20);

        ui.Fill(track, new Color(14, 11, 10));

        var filled = track;
        filled.Width = (int)(track.Width * MathHelper.Clamp(fraction, 0f, 1f));
        ui.Fill(filled, fill);

        // A lit band across the top third, so the bar reads as a filled vessel rather than a
        // rectangle of flat colour.
        var sheen = filled;
        sheen.Height = Math.Max(1, filled.Height / 3);
        ui.Fill(sheen, new Color(255, 255, 255, 44));
        ui.Border(track, new Color(12, 10, 9));

        const float LabelSize = 15f;
        var textY = track.Y + (track.Height - LabelSize) * 0.5f - 1f;
        ui.Text(label, new Vector2(track.X + 10, textY), LabelSize, new Color(248, 242, 230));
    }
}
