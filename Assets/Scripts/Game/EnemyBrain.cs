using UnityEngine;

/// <summary>
/// Simple bandit / wildlife enemy. Uses CharacterController chase (no NavMesh required).
/// </summary>
public class EnemyBrain : MonoBehaviour
{
    [SerializeField] private float maxHealth = 55f;
    [SerializeField] private float moveSpeed = 4.2f;
    [SerializeField] private float aggroRange = 14f;
    [SerializeField] private float attackRange = 2.1f;
    [SerializeField] private float attackDamage = 4f;
    [SerializeField] private float attackCooldown = 1.4f;
    [SerializeField] private string displayName = "Bandit";
    [SerializeField] private int xpReward = 20;
    [SerializeField] private bool dropsLoot = true;

    private float _hp;
    private float _atkCd;
    private CharacterController _cc;
    private Transform _player;
    private Vector3 _home;
    private float _leashRange;

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
        if (_player == null)
        {
            var p = GameObject.Find("Player");
            if (p != null) _player = p.transform;
            else return;
        }

        _atkCd -= Time.deltaTime;
        var to = _player.position - transform.position;
        to.y = 0f;
        float dist = to.magnitude;
        if (dist > aggroRange) return;
        if (WorldSafeZone.Contains(_player.position)) return;
        if (PlayerSafetyGuard.Instance != null && PlayerSafetyGuard.Instance.IsInvulnerable) return;
        if (Vector3.Distance(transform.position, _home) > _leashRange) return;

        PlayerCombat.Instance?.EnterCombat();

        if (dist > attackRange)
        {
            var dir = to.normalized;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 6f);
            var move = dir * moveSpeed;
            move.y = -9f;
            _cc.Move(move * Time.deltaTime);
        }
        else if (_atkCd <= 0f)
        {
            _atkCd = attackCooldown;
            if (PlayerSafetyGuard.Instance != null && PlayerSafetyGuard.Instance.IsInvulnerable) return;
            PlayerCombat.Instance?.NotifyHitByEnemy();
            PlayerStats.Instance?.Damage(attackDamage);
        }
    }

    public void TakeDamage(float amount)
    {
        if (_hp <= 0f) return;
        _hp -= amount;
        GameHud.Instance?.ShowToast($"{displayName} hit ({Mathf.CeilToInt(_hp)} hp)");
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
        Destroy(gameObject);
    }

    public void Setup(string name, float hp)
    {
        displayName = name;
        maxHealth = hp;
        _hp = hp;
    }

    public static GameObject Spawn(string name, Vector3 pos, Color color, float hp = 55f, string modelId = null)
    {
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
        var brain = go.AddComponent<EnemyBrain>();
        brain.Setup(name.Replace("_", " "), hp);
        var labelGo = new GameObject("Name");
        labelGo.transform.SetParent(go.transform, false);
        labelGo.transform.localPosition = new Vector3(0f, 2.2f, 0f);
        var tm = labelGo.AddComponent<TextMesh>();
        tm.text = brain.displayName;
        tm.characterSize = 0.12f;
        tm.fontSize = 48;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.color = Color.white;
        return go;
    }
}
