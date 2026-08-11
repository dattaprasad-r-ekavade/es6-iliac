using UnityEngine;

public class PlayerCombat : MonoBehaviour
{
    public static PlayerCombat Instance { get; private set; }
    public bool InCombat { get; private set; }

    // Weapon numbers come from the equipped item via EquipmentCatalog. They used to be
    // hardcoded here, which is why the inventory was cosmetic.
    [SerializeField] private float combatForgetTime = 6f;

    /// <summary>The weapon currently driving attacks. Falls back to unarmed.</summary>
    public WeaponDefinition ActiveWeapon =>
        PlayerEquipment.Instance != null ? PlayerEquipment.Instance.Weapon : EquipmentCatalog.Unarmed;

    /// <summary>
    /// True while the block is held and the equipped weapon allows it. Two-handed weapons
    /// cannot block — that is the whole trade for their damage.
    /// </summary>
    public bool IsBlocking { get; private set; }

    /// <summary>Set the block state. Ignored when the equipped weapon cannot block.</summary>
    public void SetBlocking(bool blocking) => IsBlocking = blocking && ActiveWeapon.CanBlock;
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
        // Block is held. Attacking drops the guard, so it cannot be held through a swing.
        SetBlocking(GameInput.Block.IsPressed());

        if (GameInput.PrimaryAttack.WasPressedThisFrame())
        {
            SetBlocking(false);
            TryMelee();
        }
        if (GameInput.SecondaryAttack.WasPressedThisFrame()) SpellCaster.Instance?.Cast();
        if (GameInput.UsePotion.WasPressedThisFrame()) PlayerInventory.Instance?.UseHotPotion();
    }

    /// <summary>
    /// Swing whatever is equipped. Returns true when the swing actually landed on something
    /// that can fight back — which is what skill progression keys off, so that swinging at
    /// air trains nothing.
    /// </summary>
    public bool TryMelee()
    {
        if (_cd > 0f) return false;

        var weapon = ActiveWeapon;
        if (PlayerStats.Instance != null && !PlayerStats.Instance.SpendStamina(weapon.StaminaCost))
        {
            GameHud.Instance?.ShowToast("Too exhausted");
            return false;
        }

        _cd = weapon.Cooldown;
        GameSfx.Instance?.PlayMeleeSwing();
        var origin = _cam != null ? _cam.position : transform.position + Vector3.up;
        var dir = _cam != null ? _cam.forward : transform.forward;

        if (!Physics.SphereCast(origin, 0.35f, dir, out var hit, weapon.Range,
                GameLayers.CombatMask, QueryTriggerInteraction.Ignore))
            return false;

        var enemy = hit.collider.GetComponentInParent<EnemyBrain>();
        if (enemy == null) return false;

        float threat = enemy.MaxHealth;
        enemy.TakeDamage(weapon.Damage);
        GameSfx.Instance?.PlayMeleeHit();
        EnterCombat();
        // Advancement is use-based now, so the swing trains the weapon's skill rather than
        // paying flat XP. Only landed hits get here — swinging at air trains nothing.
        SkillSystem.Instance?.ReportUse(weapon.SkillId, weapon.Damage, threat);
        return true;
    }

    // The single hardcoded Flare that used to live here is replaced by SpellCatalog and
    // SpellCaster: five spells, each doing something mechanically different.

    public void EnterCombat() { InCombat = true; _combatTimer = combatForgetTime; }
    public void ClearCombat() { InCombat = false; _combatTimer = 0f; }
    public void NotifyHitByEnemy() => EnterCombat();
}
