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

    /// <summary>
    /// The guard only understands the open world: it measures drowning against the bay's
    /// water level and solid ground against the world generator's terrain.
    ///
    /// An authored interior has neither. Its floor sits at y≈0, which is below
    /// <c>WaterLevel - 1.5</c>, so walking into the docks read as drowning and teleported the
    /// player out to the overworld spawn — and <see cref="KessilWorldGenerator.HasGroundAt"/>
    /// finds no generated terrain in there either, so the off-ground rescue fired as well.
    ///
    /// Interiors are enclosed, have their own floor and their own exit door. There is nothing
    /// for this to protect the player from, so it stands down.
    /// </summary>
    public static bool PolicesScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName)) return true;
        return GreyThreadSceneCatalog.Find(sceneName) == null;
    }

    private static bool PolicesCurrentScene()
    {
        var transition = SceneTransitionService.Instance;
        return PolicesScene(transition != null ? transition.ActiveContentSceneName : null);
    }

    private void CheckBounds(float elapsed)
    {
        if (!PolicesCurrentScene())
        {
            _offGroundFor = 0f;
            return;
        }

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
            TeleportToSpawn(transform, "You cannot leave the map — returned to safe ground.");
        }
    }

    public static void TeleportToSpawn(Transform player, string toast = null)
    {
        if (player == null) return;
        var cc = player.GetComponent<CharacterController>();
        var pos = RespawnPosition(cc);
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

    public static Vector3 RespawnPosition(CharacterController controller = null)
    {
        var transition = SceneTransitionService.Instance;
        string sceneName = transition != null
            ? transition.ActiveContentSceneName
            : UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (string.IsNullOrWhiteSpace(sceneName))
            sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        return RespawnPositionForScene(sceneName, controller);
    }

    public static Vector3 RespawnPositionForScene(string sceneName, CharacterController controller = null)
    {
        return sceneName == GreyThreadDirector.RegionScene
            ? CapitalRegion.PlayerSpawn
            : KessilWorldGenerator.GetPlayerSpawn(controller);
    }
}
