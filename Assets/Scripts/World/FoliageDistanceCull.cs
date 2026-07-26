using UnityEngine;

/// <summary>
/// Hides foliage renderers when far from the player for large-map performance.
/// </summary>
public class FoliageDistanceCull : MonoBehaviour
{
    public float maxDistance = 500f;
    private Transform _player;
    private Renderer[] _renderers;
    private float _timer;
    private float _phase;

    private void Awake()
    {
        _renderers = GetComponentsInChildren<Renderer>();
        _phase = UnityEngine.Random.Range(0f, 0.4f);
    }

    private void Update()
    {
        _timer -= Time.deltaTime;
        if (_timer > 0f) return;
        _timer = 0.35f + _phase;

        if (_player == null)
        {
            var p = GameObject.Find("Player");
            if (p != null) _player = p.transform;
            else return;
        }

        bool show = (_player.position - transform.position).sqrMagnitude < maxDistance * maxDistance;
        if (_renderers == null) return;
        foreach (var r in _renderers)
        {
            if (r != null) r.enabled = show;
        }
    }
}
