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

    private void Awake() => Instance = this;
    private void OnDestroy() { if (Instance == this) Instance = null; }

    private void Update()
    {
        if (Health <= 0f) return;
        Mana = Mathf.Min(MaxMana, Mana + 4f * Time.deltaTime);
        if (!PlayerCombat.Instance || !PlayerCombat.Instance.InCombat)
            Stamina = Mathf.Min(MaxStamina, Stamina + 12f * Time.deltaTime);
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
            Mana = MaxMana;
            Stamina = MaxStamina;
            GameSfx.Instance?.PlayLevelUp();
            GameHud.Instance?.ShowToast($"Level Up! You are now level {Level}");
        }
        OnChanged?.Invoke();
    }

    public void Damage(float amount)
    {
        Health = Mathf.Max(0f, Health - amount);
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

    public bool SpendMana(float amount)
    {
        if (Mana < amount) return false;
        Mana -= amount;
        OnChanged?.Invoke();
        return true;
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

    public void FullRestore()
    {
        Health = MaxHealth;
        Mana = MaxMana;
        Stamina = MaxStamina;
        OnChanged?.Invoke();
    }
}
