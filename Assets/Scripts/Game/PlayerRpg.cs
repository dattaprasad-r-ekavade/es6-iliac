using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Player RPG: health / magicka / stamina, XP, inventory, melee combat.
/// </summary>
public class PlayerStats : MonoBehaviour
{
    public static PlayerStats Instance { get; private set; }

    public int Level { get; private set; } = 1;
    public int Xp { get; private set; }
    public int XpToLevel => 40 + Level * 35;
    public float Health = 100f;
    public float MaxHealth = 100f;
    public float Magicka = 80f;
    public float MaxMagicka = 80f;
    public float Stamina = 100f;
    public float MaxStamina = 100f;
    public int Gold;

    public event Action OnChanged;

    private void Awake() => Instance = this;

    private void Update()
    {
        // Regen
        if (Health > 0f)
        {
            Magicka = Mathf.Min(MaxMagicka, Magicka + 4f * Time.deltaTime);
            if (!PlayerCombat.Instance || !PlayerCombat.Instance.InCombat)
                Stamina = Mathf.Min(MaxStamina, Stamina + 12f * Time.deltaTime);
        }
    }

    public void AddXp(int amount)
    {
        Xp += amount;
        while (Xp >= XpToLevel)
        {
            Xp -= XpToLevel;
            Level++;
            MaxHealth += 12f;
            MaxMagicka += 8f;
            MaxStamina += 8f;
            Health = MaxHealth;
            Magicka = MaxMagicka;
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

    public bool SpendMagicka(float amount)
    {
        if (Magicka < amount) return false;
        Magicka -= amount;
        OnChanged?.Invoke();
        return true;
    }

    private void Die()
    {
        GameHud.Instance?.ShowToast("You were defeated — returned to Daggerfall.");
        var player = GameObject.Find("Player");
        if (player != null)
            PlayerSafetyGuard.TeleportToSpawn(player.transform);
        else
            FullRestore();
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
        Magicka = MaxMagicka;
        Stamina = MaxStamina;
        OnChanged?.Invoke();
    }
}

[Serializable]
public class InvItem
{
    public string Id;
    public string Name;
    public int Count;
    public string Kind; // weapon, potion, loot, misc
}

public class PlayerInventory : MonoBehaviour
{
    public static PlayerInventory Instance { get; private set; }
    public readonly List<InvItem> Items = new();
    public event Action OnChanged;

    private void Awake()
    {
        Instance = this;
        if (Items.Count == 0)
        {
            Add("iron_sword", "Iron Sword", 1, "weapon");
            Add("health_potion", "Health Potion", 3, "potion");
            Add("torch", "Torch", 1, "misc");
        }
    }

    public void Add(string id, string name, int count, string kind)
    {
        var existing = Items.Find(i => i.Id == id);
        if (existing != null) existing.Count += count;
        else Items.Add(new InvItem { Id = id, Name = name, Count = count, Kind = kind });
        if (Time.frameCount > 2) GameSfx.Instance?.PlayPickup();
        OnChanged?.Invoke();
    }

    public bool Consume(string id, int count = 1)
    {
        var existing = Items.Find(i => i.Id == id);
        if (existing == null || existing.Count < count) return false;
        existing.Count -= count;
        if (existing.Count <= 0) Items.Remove(existing);
        OnChanged?.Invoke();
        return true;
    }

    public void UseHotPotion()
    {
        if (!Consume("health_potion"))
        {
            GameHud.Instance?.ShowToast("No health potions");
            return;
        }
        PlayerStats.Instance?.Heal(40f);
        GameHud.Instance?.ShowToast("Used Health Potion");
    }
}

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

    private void Start()
    {
        var cam = GetComponentInChildren<Camera>(true);
        _cam = cam != null ? cam.transform : transform;
    }

    private void Update()
    {
        if (!enabled) return;
        if (GameHud.Instance != null && GameHud.Instance.AnyMenuOpen) return;

        _cd -= Time.deltaTime;
        if (_combatTimer > 0f)
        {
            _combatTimer -= Time.deltaTime;
            if (_combatTimer <= 0f) InCombat = false;
        }

        var mouse = Mouse.current;
        var kb = Keyboard.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame) TryMelee();
        if (kb != null && kb.digit1Key.wasPressedThisFrame) TryMelee();
        if (kb != null && kb.digit2Key.wasPressedThisFrame) CastFlare();
        if (kb != null && kb.qKey.wasPressedThisFrame) PlayerInventory.Instance?.UseHotPotion();
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
        if (Physics.SphereCast(origin, 0.35f, dir, out var hit, meleeRange, ~0, QueryTriggerInteraction.Ignore))
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
        if (PlayerStats.Instance == null || !PlayerStats.Instance.SpendMagicka(16f))
        {
            GameHud.Instance?.ShowToast("Not enough magicka");
            return;
        }

        var origin = _cam != null ? _cam.position : transform.position + Vector3.up;
        var dir = _cam != null ? _cam.forward : transform.forward;
        if (Physics.SphereCast(origin, 0.5f, dir, out var hit, 18f))
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

    public void EnterCombat()
    {
        InCombat = true;
        _combatTimer = combatForgetTime;
    }

    public void ClearCombat()
    {
        InCombat = false;
        _combatTimer = 0f;
    }

    public void NotifyHitByEnemy()
    {
        EnterCombat();
    }
}
