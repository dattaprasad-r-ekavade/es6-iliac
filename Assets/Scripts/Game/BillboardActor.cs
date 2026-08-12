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

    /// <summary>
    /// Seeds the figure. Using the actor's name means a character's build, dress and headwear
    /// are a property of who they are, so the same person looks the same in every session
    /// without any of it being authored or saved.
    /// </summary>
    [SerializeField] private string figureKey = "";

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
        // Match the sprite's aspect exactly, or point-filtered texels come out rectangular and
        // the whole look reads as a stretched image rather than as a drawing.
        quad.transform.localScale = new Vector3(
            height * (CharacterSprite.Width / (float)CharacterSprite.Height), height, 1f);
        Object.Destroy(quad.GetComponent<Collider>());

        var actor = quad.AddComponent<BillboardActor>();
        actor.height = height;
        actor.tint = tint;
        actor.figureKey = name;
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

    /// <summary>
    /// Rebuild the sprite on load.
    ///
    /// Without this the figure only existed if <see cref="Spawn"/> had just run: the material
    /// and texture are created in code, and Unity does not serialise runtime-created assets into
    /// a saved scene, so every billboard in a generated scene came back with a null material
    /// after the editor reopened it. Regenerating costs 2,048 pixels, which is cheaper than
    /// storing it would be.
    /// </summary>
    private void Awake() => Apply();

    private void Apply()
    {
        var renderer = GetComponent<MeshRenderer>();
        renderer.sharedMaterial = MaterialFor(figureKey, tint);
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
    /// The figure, drawn by <see cref="CharacterSprite"/>: flat colour fields separated by a
    /// hard contour rather than by shading.
    ///
    /// This used to draw three rectangles inline. The art direction lock says silhouette reads
    /// at any fidelity while surface detail does not — which is true, and was being used to
    /// excuse a silhouette that was not actually drawn. The outline is what turns flat fields
    /// into a drawing instead of a greybox.
    /// </summary>
    private static Material MaterialFor(string figureKey, Color tint)
    {
        var texture = CharacterSprite.Build(CharacterSprite.From(figureKey, tint));

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
