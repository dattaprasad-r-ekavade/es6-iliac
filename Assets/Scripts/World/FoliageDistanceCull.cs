using UnityEngine;

/// <summary>
/// Marks a prop as distance-culled. Registration only — the actual work is done in
/// one pass by <see cref="FoliageCullingSystem"/>.
///
/// This component used to run its own <c>Update</c> and its own
/// <c>GameObject.Find("Player")</c>, on every one of the ~640 scattered props.
/// The component is kept (rather than deleted) so the baked Main.unity, which has
/// it attached 639 times, doesn't come back with missing-script warnings.
/// </summary>
public class FoliageDistanceCull : MonoBehaviour
{
    public float maxDistance = 500f;

    [System.NonSerialized] public Renderer[] Renderers;
    [System.NonSerialized] public bool Visible = true;

    private void OnEnable()
    {
        Renderers ??= GetComponentsInChildren<Renderer>(true);
        FoliageCullingSystem.Register(this);
    }

    private void OnDisable()
    {
        FoliageCullingSystem.Unregister(this);
    }

    public void SetVisible(bool visible)
    {
        if (Visible == visible || Renderers == null) return;
        Visible = visible;
        for (int i = 0; i < Renderers.Length; i++)
        {
            var r = Renderers[i];
            if (r != null) r.enabled = visible;
        }
    }
}
