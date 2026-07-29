using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Discoverable locations, fog-of-war discovery, and fast travel.
/// </summary>
public class DiscoveryTravelSystem : MonoBehaviour
{
    public static DiscoveryTravelSystem Instance { get; private set; }

    [Serializable]
    public class Location
    {
        public string Id;
        public string DisplayName;
        public Vector3 WorldPosition;
        public Vector3 TravelPosition;
        public bool IsCity;
        public bool Discovered;
        public float DiscoverRadius = 90f;
    }

    public readonly List<Location> Locations = new();
    public event Action<Location> OnDiscovered;
    public event Action OnChanged;

    private Transform _player;
    private bool _traveling;

    private void Awake() => Instance = this;

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Configure(Transform player)
    {
        _player = player;
        if (Locations.Count == 0) BootstrapDefaultLocations();
        // Caldemar starts discovered
        Discover("city_west", silent: true);
    }

    /// <summary>Locations come from <see cref="WorldLayout"/> — one shared definition
    /// with the world generator, the map art and the spawners.</summary>
    public void BootstrapDefaultLocations()
    {
        Locations.Clear();
        foreach (var site in WorldLayout.Sites)
        {
            Locations.Add(new Location
            {
                Id = site.Id,
                DisplayName = site.DisplayName,
                WorldPosition = site.WorldPosition,
                TravelPosition = site.TravelPosition,
                IsCity = site.IsCity,
                DiscoverRadius = site.DiscoverRadius,
                Discovered = false
            });
        }
    }

    private void Update()
    {
        if (_player == null)
        {
            _player = PlayerRef.Transform;
            if (_player == null) return;
        }

        foreach (var loc in Locations)
        {
            if (loc.Discovered) continue;
            float r = loc.DiscoverRadius;
            if ((_player.position - loc.WorldPosition).sqrMagnitude < r * r)
            {
                Discover(loc.Id);
            }
        }
    }

    public void Discover(string id, bool silent = false)
    {
        var loc = Get(id);
        if (loc == null || loc.Discovered) return;
        loc.Discovered = true;
        if (!silent)
        {
            OnDiscovered?.Invoke(loc);
            GameHud.Instance?.ShowToast($"Discovered: {loc.DisplayName}");
        }
        OnChanged?.Invoke();
    }

    public Location Get(string id)
    {
        foreach (var l in Locations)
            if (l.Id == id) return l;
        return null;
    }

    public bool CanFastTravel(string id)
    {
        if (_traveling) return false;
        var loc = Get(id);
        if (loc == null || !loc.Discovered) return false;
        if (PlayerCombat.Instance != null && PlayerCombat.Instance.InCombat) return false;
        return true;
    }

    public void FastTravel(string id)
    {
        if (!CanFastTravel(id)) return;
        var loc = Get(id);
        StartCoroutine(TravelRoutine(loc));
    }

    private System.Collections.IEnumerator TravelRoutine(Location loc)
    {
        _traveling = true;

        var player = _player != null ? _player : PlayerRef.Transform;

        // Measure the journey *before* teleporting. This used to be read after the
        // move, so the distance was always ~0 and every trip cost the 0.5 h minimum
        // no matter how far across the bay it went.
        float dist = player != null
            ? Vector3.Distance(player.position, loc.TravelPosition)
            : 1000f;

        GameHud.Instance?.ShowFade(true);
        yield return new WaitForSecondsRealtime(0.45f);

        if (player != null)
        {
            // Land on real ground: the authored travel Y is an estimate, and terrain
            // height is generated from noise.
            var cc = player.GetComponent<CharacterController>();
            var dest = KessilWorldGenerator.SnapCharacterToGround(loc.TravelPosition, cc);
            if (cc != null) cc.enabled = false;
            player.position = dest;
            if (cc != null) cc.enabled = true;
        }

        float hours = Mathf.Clamp(dist / 500f, 0.5f, 8f);
        TimeWeatherSystem.Instance?.AdvanceHours(hours);

        yield return new WaitForSecondsRealtime(0.2f);
        GameHud.Instance?.ShowFade(false);
        GameHud.Instance?.ShowToast($"Traveled to {loc.DisplayName} — {hours:0.#} h passed");
        _traveling = false;
        OnChanged?.Invoke();
    }

    public List<string> GetDiscoveredIds()
    {
        var list = new List<string>();
        foreach (var l in Locations) if (l.Discovered) list.Add(l.Id);
        return list;
    }

    public void LoadDiscovered(IEnumerable<string> ids)
    {
        var set = new HashSet<string>(ids ?? Array.Empty<string>());
        foreach (var l in Locations) l.Discovered = set.Contains(l.Id);
        OnChanged?.Invoke();
    }
}
