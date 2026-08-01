using UnityEngine;

public class PlayerCombat : MonoBehaviour
{
    public static PlayerCombat Instance { get; private set; }
    public bool InCombat { get; private set; }
    [SerializeField] private float meleeDamage = 18f;
    [SerializeField] private float meleeRange = 2.4f;
    [SerializeField] private float meleeCooldown = 0.45f;
    [SerializeField] private float combatForgetTime = 6f;
    private float _cd;
    private float _combatTimer;
    private Transform _cam;

    private void Awake() => Instance = this;
    private void OnDestroy() { if (Instance == this) Instance = null; }

    private void Start()
    {
        var cam = GetComponentInChildren<Camera>(true);
        _cam = cam != null ? cam.transform : transform;
    }

    private void Update()
    {
        if (!enabled) return;
        if (GameStateService.Instance != null && !GameStateService.Instance.GameplayInputAllowed) return;
        _cd -= Time.deltaTime;
        if (_combatTimer > 0f)
        {
            _combatTimer -= Time.deltaTime;
            if (_combatTimer <= 0f) InCombat = false;
        }
        if (GameInput.PrimaryAttack.WasPressedThisFrame()) TryMelee();
        if (GameInput.SecondaryAttack.WasPressedThisFrame()) CastFlare();
        if (GameInput.UsePotion.WasPressedThisFrame()) PlayerInventory.Instance?.UseHotPotion();
    }

    private void TryMelee()
    {
        if (_cd > 0f) return;
        if (PlayerStats.Instance != null && !PlayerStats.Instance.SpendStamina(8f))
        {
            GameHud.Instance?.ShowToast("Too exhausted");
            return;
        }
        _cd = meleeCooldown;
        GameSfx.Instance?.PlayMeleeSwing();
        var origin = _cam != null ? _cam.position : transform.position + Vector3.up;
        var dir = _cam != null ? _cam.forward : transform.forward;
        if (Physics.SphereCast(origin, 0.35f, dir, out var hit, meleeRange,
                GameLayers.CombatMask, QueryTriggerInteraction.Ignore))
        {
            var enemy = hit.collider.GetComponentInParent<EnemyBrain>();
            if (enemy != null)
            {
                enemy.TakeDamage(meleeDamage);
                GameSfx.Instance?.PlayMeleeHit();
                EnterCombat();
                PlayerStats.Instance?.AddXp(4);
            }
        }
    }

    private void CastFlare()
    {
        if (PlayerStats.Instance == null || !PlayerStats.Instance.SpendMana(16f))
        {
            GameHud.Instance?.ShowToast("Not enough mana");
            return;
        }
        var origin = _cam != null ? _cam.position : transform.position + Vector3.up;
        var dir = _cam != null ? _cam.forward : transform.forward;
        if (Physics.SphereCast(origin, 0.5f, dir, out var hit, 18f,
                GameLayers.CombatMask, QueryTriggerInteraction.Ignore))
        {
            var enemy = hit.collider.GetComponentInParent<EnemyBrain>();
            if (enemy != null)
            {
                enemy.TakeDamage(26f);
                EnterCombat();
                PlayerStats.Instance.AddXp(6);
            }
        }
        GameSfx.Instance?.PlayMagic();
        GameHud.Instance?.ShowToast("Flare!");
    }

    public void EnterCombat() { InCombat = true; _combatTimer = combatForgetTime; }
    public void ClearCombat() { InCombat = false; _combatTimer = 0f; }
    public void NotifyHitByEnemy() => EnterCombat();
}
