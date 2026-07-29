using UnityEngine;

/// <summary>
/// Keeps the player on land: returns them to Caldemar if they drown in the bay
/// or walk off the edge of the world.
/// </summary>
public class PlayerSafetyGuard : MonoBehaviour
{
    public static PlayerSafetyGuard Instance { get; private set; }

    /// <summary>How long the player may be off solid ground before being rescued.</summary>
    private const float OffGroundGrace = 2.5f;

    private const float CheckInterval = 0.35f;

    private float _invulnUntil;
    private float _checkTimer;
    private float _offGroundFor;

    public bool IsInvulnerable => Time.time < _invulnUntil;

    public void GrantSpawnProtection() => _invulnUntil = Time.time + 8f;

    private void Awake() => Instance = this;

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        _checkTimer -= Time.deltaTime;
        if (_checkTimer > 0f) return;
        float elapsed = CheckInterval - _checkTimer;
        _checkTimer = CheckInterval;

        CheckBounds(elapsed);
    }

    private void CheckBounds(float elapsed)
    {
        var pos = transform.position;

        // Drowned / fell through the world. The old test was `pos.y < 8f`, but terrain
        // edges legitimately slope down to y=2, so every coastline in a game about a bay
        // teleported the player home the moment they reached the shore.
        if (pos.y < WorldLayout.WaterLevel - 1.5f)
        {
            TeleportToSpawn(transform, "The bay pulls you back to shore.");
            _offGroundFor = 0f;
            return;
        }

        // Off any walkable surface. Jumps and short falls are legitimate, so this only
        // fires once the player has been over nothing for a while.
        if (KessilWorldGenerator.HasGroundAt(pos))
        {
            _offGroundFor = 0f;
            return;
        }

        _offGroundFor += elapsed;
        if (_offGroundFor >= OffGroundGrace)
        {
            _offGroundFor = 0f;
            TeleportToSpawn(transform, "You cannot leave the map — returned to Caldemar.");
        }
    }

    public static void TeleportToSpawn(Transform player, string toast = null)
    {
        if (player == null) return;
        var cc = player.GetComponent<CharacterController>();
        var pos = KessilWorldGenerator.GetPlayerSpawn(cc);
        if (cc != null) cc.enabled = false;
        player.position = pos;
        if (cc != null) cc.enabled = true;

        if (Instance != null)
        {
            Instance._invulnUntil = Time.time + 4f;
            Instance._offGroundFor = 0f;
        }

        PlayerStats.Instance?.FullRestore();
        if (PlayerCombat.Instance != null)
            PlayerCombat.Instance.ClearCombat();

        if (!string.IsNullOrEmpty(toast))
            GameHud.Instance?.ShowToast(toast);
    }
}
