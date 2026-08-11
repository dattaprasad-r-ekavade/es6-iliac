using System;
using UnityEngine;

/// <summary>Player health, resources, XP and death recovery.</summary>
public class PlayerStats : MonoBehaviour
{
    public static PlayerStats Instance { get; private set; }
    public int Level { get; private set; } = 1;
    public int Xp { get; private set; }
    public int XpToLevel => 40 + Level * 35;
    public float Health = 100f;
    public float MaxHealth = 100f;
    public float Mana = 80f;
    public float MaxMana = 80f;
    public float Stamina = 100f;
    public float MaxStamina = 100f;
    public int Gold;
    public event Action OnChanged;

    /// <summary>
    /// Lifetime crystals burned. Tracked, never punished — it changes what the world says to
    /// the player, never how hard enemies hit. See Docs/GAMEPLAY_DESIGN.md.
    /// </summary>
    public int Channeled { get; private set; }

    /// <summary>Stamina regained per second while a fight is live.</summary>
    public const float CombatStaminaRegen = 4f;

    /// <summary>Stamina regained per second out of combat.</summary>
    public const float RestStaminaRegen = 12f;

    private void Awake() => Instance = this;
    private void OnDestroy() { if (Instance == this) Instance = null; }

    private void Update()
    {
        if (Health <= 0f) return;

        // Mana does not regenerate. It is charge drawn from soul crystals — the setting's
        // scarcity is not real if the player's own bar refills for free.

        // Combat regen used to be zero, which gave 12 swings and then six seconds of standing
        // there unable to attack. Reduced, not absent.
        bool fighting = PlayerCombat.Instance != null && PlayerCombat.Instance.InCombat;
        float regen = fighting ? CombatStaminaRegen : RestStaminaRegen;
        Stamina = Mathf.Min(MaxStamina, Stamina + regen * Time.deltaTime);
    }

    public void AddXp(int amount)
    {
        Xp += amount;
        while (Xp >= XpToLevel)
        {
            Xp -= XpToLevel;
            Level++;
            MaxHealth += 12f;
            MaxMana += 8f;
            MaxStamina += 8f;
            Health = MaxHealth;
            Stamina = MaxStamina;
            // Mana is charge, not a pool: levelling raises the ceiling but does not hand out
            // free crystals. Refilling here would make every level-up a silent resupply.
            GameSfx.Instance?.PlayLevelUp();
            GameHud.Instance?.ShowToast($"Level Up! You are now level {Level}");
        }
        OnChanged?.Invoke();
    }

    /// <summary>
    /// Take a hit. Worn armour is flat reduction, and blocking halves what gets through —
    /// armour has no skill of its own because Block is the active defensive verb.
    ///
    /// A minimum of 1 always lands, so armour can never make the player invulnerable.
    /// </summary>
    public void Damage(float amount)
    {
        float incoming = amount;

        var combat = PlayerCombat.Instance;
        if (combat != null && combat.IsBlocking) incoming *= 0.5f;

        var equipment = PlayerEquipment.Instance;
        if (equipment != null) incoming -= equipment.ArmourValue;

        incoming = Mathf.Max(1f, incoming);

        Health = Mathf.Max(0f, Health - incoming);
        GameHud.Instance?.FlashDamage();
        OnChanged?.Invoke();
        if (Health <= 0f) Die();
    }

    public void Heal(float amount)
    {
        Health = Mathf.Min(MaxHealth, Health + amount);
        OnChanged?.Invoke();
    }

    public bool SpendStamina(float amount)
    {
        if (Stamina < amount) return false;
        Stamina -= amount;
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Spend charge, drawing on a crystal if the reserve is short. Returns false only when
    /// the player has neither enough charge nor a crystal to burn.
    /// </summary>
    public bool SpendMana(float amount)
    {
        if (Mana < amount && !TryDrawCrystal()) return false;
        if (Mana < amount) return false;
        Mana -= amount;
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Burn one crystal for charge. Deliberately announced: the player should feel every one
    /// of these, because the whole arc is an argument about what they cost.
    /// </summary>
    public bool TryDrawCrystal()
    {
        var inventory = PlayerInventory.Instance;
        if (inventory == null || !inventory.Consume(SoulCrystals.LesserId)) return false;

        Mana = Mathf.Min(MaxMana, Mana + SoulCrystals.LesserCharge);
        Channeled++;
        GameSfx.Instance?.PlayMagic();
        GameHud.Instance?.ShowToast("You draw on a soul crystal.");
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>Restore the lifetime channel count from a save.</summary>
    public void RestoreChanneled(int channeled)
    {
        Channeled = Mathf.Max(0, channeled);
        OnChanged?.Invoke();
    }

    private void Die()
    {
        var gameState = GameStateService.Instance;
        gameState?.SetState(GameState.Death);
        GameHud.Instance?.ShowToast("You were defeated — returned to Caldemar.");
        if (PlayerRef.TryGet(out var player)) PlayerSafetyGuard.TeleportToSpawn(player);
        else FullRestore();
        gameState?.SetState(GameState.Gameplay);
    }

    public void RestoreProgress(int level, int xp)
    {
        Level = Mathf.Max(1, level);
        Xp = Mathf.Max(0, xp);
        OnChanged?.Invoke();
    }

    /// <summary>
    /// Recovery after death or rescue. Health and stamina come back; charge does not, because
    /// dying is not a way to refill crystals you did not spend gold on.
    /// </summary>
    public void FullRestore()
    {
        Health = MaxHealth;
        Stamina = MaxStamina;
        OnChanged?.Invoke();
    }
}
