using UnityEngine;

/// <summary>
/// Simple bandit / wildlife enemy. Uses CharacterController chase (no NavMesh required).
/// </summary>
public class EnemyBrain : MonoBehaviour
{
    [SerializeField] private float maxHealth = 55f;
    /// <summary>
    /// Must stay above the player's default travel speed (5.25 m/s, see
    /// <see cref="SimplePlayerController"/>). This was 4.2 when the player walked at 3.5 by
    /// default; making run the default would silently have made every fight optional, because
    /// nothing in the world could close the distance any more.
    /// </summary>
    [SerializeField] private float moveSpeed = 5.6f;
    [SerializeField] private float aggroRange = 14f;
    [SerializeField] private float attackRange = 2.1f;
    [SerializeField] private float attackDamage = 4f;
    [SerializeField] private float attackCooldown = 1.4f;
    [SerializeField] private string displayName = "Bandit";
    [SerializeField] private int xpReward = 20;
    [SerializeField] private bool dropsLoot = true;

    /// <summary>Stable id so a save can remember this one stayed dead.</summary>
    public string SpawnId { get; private set; }

    public string DisplayName => displayName;
    public float Health => _hp;
    public float MaxHealth => maxHealth;

    private float _hp;
    private float _atkCd;
    private CharacterController _cc;
    private Vector3 _home;
    private float _leashRange;

    // Elemental status. Each element does something different rather than being a damage
    // type with a different colour — see Docs/GAMEPLAY_DESIGN.md § Spells.
    private float _burnDps, _burnUntil;
    private float _chillFactor = 1f, _chillUntil;
    private float _staggerUntil;

    public bool IsBurning => Time.time < _burnUntil;
    public bool IsChilled => Time.time < _chillUntil;
    public bool IsStaggered => Time.time < _staggerUntil;

    /// <summary>Current move speed after chill. Never negative.</summary>
    public float CurrentMoveSpeed => moveSpeed * (IsChilled ? _chillFactor : 1f);

    /// <summary>Fire: damage over time. Beats groups and the unarmoured.</summary>
    public void ApplyBurn(float damagePerSecond, float duration)
    {
        _burnDps = Mathf.Max(_burnDps, damagePerSecond);
        _burnUntil = Mathf.Max(_burnUntil, Time.time + duration);
    }

    /// <summary>Frost: slows. Beats chargers.</summary>
    public void ApplyChill(float speedFactor, float duration)
    {
        _chillFactor = Mathf.Clamp(Mathf.Min(_chillFactor, speedFactor), 0.1f, 1f);
        _chillUntil = Mathf.Max(_chillUntil, Time.time + duration);
    }

    /// <summary>Shock: interrupts. Beats anything mid-action.</summary>
    public void ApplyStagger(float duration)
    {
        _staggerUntil = Mathf.Max(_staggerUntil, Time.time + duration);
    }

    private void Awake()
    {
        _hp = maxHealth;
        _home = transform.position;
        _leashRange = aggroRange * 2.8f;
        _cc = GetComponent<CharacterController>();
        if (_cc == null)
        {
            _cc = gameObject.AddComponent<CharacterController>();
            _cc.height = 1.8f;
            _cc.radius = 0.4f;
            _cc.center = new Vector3(0f, 0.9f, 0f);
        }
    }

    private void Update()
    {
        if (_hp <= 0f) return;

        if (IsBurning)
        {
            TakeDamage(_burnDps * Time.deltaTime);
            if (_hp <= 0f) return;
        }
        if (!IsChilled) _chillFactor = 1f;

        if (!PlayerRef.TryGet(out var player)) return;

        // Staggered enemies neither close nor swing. This is what makes shock a control
        // element rather than a third damage number.
        if (IsStaggered) return;

        _atkCd -= Time.deltaTime;
        var to = player.position - transform.position;
        to.y = 0f;
        float dist = to.magnitude;
        if (dist > aggroRange) return;
        if (WorldSafeZone.Contains(player.position)) return;
        if (PlayerSafetyGuard.Instance != null && PlayerSafetyGuard.Instance.IsInvulnerable) return;
        if (Vector3.Distance(transform.position, _home) > _leashRange) return;

        PlayerCombat.Instance?.EnterCombat();

        if (dist > attackRange)
        {
            var dir = to.normalized;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 6f);
            var move = dir * CurrentMoveSpeed;
            move.y = -9f;
            _cc.Move(move * Time.deltaTime);
        }
        else if (_atkCd <= 0f)
        {
            _atkCd = attackCooldown;
            if (PlayerSafetyGuard.Instance != null && PlayerSafetyGuard.Instance.IsInvulnerable) return;
            if (!HasLineOfSightTo(player)) return;
            PlayerCombat.Instance?.NotifyHitByEnemy();
            PlayerStats.Instance?.Damage(attackDamage);
        }
    }

    /// <summary>Don't let enemies hit the player through a city wall.</summary>
    private bool HasLineOfSightTo(Transform player)
    {
        var from = transform.position + Vector3.up * 1.2f;
        var to = player.position + Vector3.up * 1.0f;
        var delta = to - from;
        return !Physics.Raycast(from, delta.normalized, delta.magnitude,
            GameLayers.SightBlockerMask, QueryTriggerInteraction.Ignore);
    }

    public void TakeDamage(float amount)
    {
        if (_hp <= 0f) return;
        _hp -= amount;
        GameHud.Instance?.ShowEnemyHealth(displayName, Mathf.Max(0f, _hp), maxHealth);
        if (_hp <= 0f) Die();
    }

    private void Die()
    {
        PlayerStats.Instance?.AddXp(xpReward);
        if (dropsLoot)
        {
            PlayerInventory.Instance?.Add("bandit_loot", "Bandit Satchel", 1, "loot");
            if (PlayerStats.Instance != null) PlayerStats.Instance.Gold += Random.Range(5, 18);
            GameHud.Instance?.ShowToast($"Looted {displayName}");
        }
        QuestSystem.Instance?.NotifyEnemyKilled(displayName);
        WorldState.MarkKilled(SpawnId);
        Destroy(gameObject);
    }

    public void Setup(string name, float hp, string spawnId)
    {
        displayName = name;
        maxHealth = hp;
        _hp = hp;
        SpawnId = spawnId;
    }

    /// <summary>
    /// Spawn an enemy at a world position. <paramref name="spawnId"/> must be stable
    /// across sessions so a loaded save can skip enemies the player already killed.
    /// </summary>
    public static GameObject Spawn(string name, Vector3 pos, Color color, float hp = 55f,
        string modelId = null, string spawnId = null)
    {
        spawnId ??= $"{name}@{pos.x:0}_{pos.z:0}";
        if (WorldState.IsKilled(spawnId)) return null;

        GameObject go = CharacterLibrary.Instantiate(modelId, 2.2f);
        if (go == null)
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Object.Destroy(go.GetComponent<Collider>());
            var r = go.GetComponent<Renderer>();
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color); else m.color = color;
            r.sharedMaterial = m;
        }

        go.name = name;
        go.transform.position = pos;
        WorldTagger.SetLayerRecursive(go, GameLayers.Enemy);

        // CharacterLibrary strips colliders off the model, so give it a hit volume
        // the player's layer-masked melee cast can actually find.
        if (go.GetComponent<Collider>() == null)
        {
            var capsule = go.AddComponent<CapsuleCollider>();
            capsule.height = 1.8f;
            capsule.radius = 0.4f;
            capsule.center = new Vector3(0f, 0.9f, 0f);
        }

        var brain = go.AddComponent<EnemyBrain>();
        brain.Setup(name.Replace("_", " "), hp, spawnId);

        return go;
    }
}
