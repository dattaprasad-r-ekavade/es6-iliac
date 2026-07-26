using UnityEngine;

/// <summary>
/// Keeps the player on land: teleports back to Daggerfall if they fall in the bay or leave the world.
/// </summary>
public class PlayerSafetyGuard : MonoBehaviour
{
    public static PlayerSafetyGuard Instance { get; private set; }

    private float _invulnUntil;
    private float _checkTimer;

    public bool IsInvulnerable => Time.time < _invulnUntil;

    public void GrantSpawnProtection() => _invulnUntil = Time.time + 8f;

    private void Awake() => Instance = this;

    private void Update()
    {
        _checkTimer -= Time.deltaTime;
        if (_checkTimer > 0f) return;
        _checkTimer = 0.35f;
        if (!enabled) return;
        CheckBounds();
    }

    private void CheckBounds()
    {
        var pos = transform.position;

        if (pos.y < 8f || IsOnVoidSurface())
        {
            TeleportToSpawn(transform, "The bay pulls you back to shore.");
            return;
        }

        if (!IsOverLand(pos))
            TeleportToSpawn(transform, "You cannot leave the map — returned to Daggerfall.");
    }

    private static bool IsOnVoidSurface()
    {
        var origin = Instance.transform.position + Vector3.up * 2f;
        if (!Physics.Raycast(origin, Vector3.down, out var hit, 40f, ~0, QueryTriggerInteraction.Ignore))
            return true;
        var n = hit.collider.gameObject.name;
        return n.Contains("FallCatcher") || n.Contains("Ocean");
    }

  private static bool IsOverLand(Vector3 pos)
    {
        var origin = new Vector3(pos.x, 400f, pos.z);
        var hits = Physics.RaycastAll(origin, Vector3.down, 900f, ~0, QueryTriggerInteraction.Ignore);
        foreach (var h in hits)
        {
            if (h.collider == null) continue;
            var n = h.collider.gameObject.name;
            if (n.Contains("TerrainSurface") || n.Contains("SpawnPad") || n.Contains("Plaza") ||
                n.Contains("Road") || n.Contains("Land"))
                return true;
        }
        return false;
    }

    public static void TeleportToSpawn(Transform player, string toast = null)
    {
        if (player == null) return;
        var pos = IliacBayWorldGenerator.SnapToWalkable(player.position);
        var cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        player.position = pos;
        if (cc != null) cc.enabled = true;

        if (Instance != null)
            Instance._invulnUntil = Time.time + 4f;

        PlayerStats.Instance?.FullRestore();
        if (PlayerCombat.Instance != null)
            PlayerCombat.Instance.ClearCombat();

        if (!string.IsNullOrEmpty(toast))
            GameHud.Instance?.ShowToast(toast);
    }

    public static bool IsInDaggerfallSafeZone(Vector3 pos)
    {
        var d = new Vector2(pos.x + 2000f, pos.z - 1450f);
        return d.sqrMagnitude < 380f * 380f;
    }
}
