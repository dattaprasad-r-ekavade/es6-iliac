using UnityEngine;

public class PlayerInteract : MonoBehaviour
{
    [SerializeField] private float range = 3f;

    private void Update()
    {
        if (GameStateService.Instance != null && !GameStateService.Instance.GameplayInputAllowed) return;
        if (!GameInput.Interact.WasPressedThisFrame()) return;
        var cam = GetComponentInChildren<Camera>();
        var origin = cam != null ? cam.transform.position : transform.position + Vector3.up;
        var direction = cam != null ? cam.transform.forward : transform.forward;
        if (Physics.SphereCast(origin, 0.4f, direction, out var hit, range,
                GameLayers.InteractMask, QueryTriggerInteraction.Ignore))
        {
            var npc = hit.collider.GetComponentInParent<NpcInteractable>();
            if (npc != null) npc.Interact(); else GameHud.Instance?.ShowToast("Nothing to use");
        }
        else GameHud.Instance?.ShowToast("Nothing nearby (E)");
    }
}
