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
    }

    public readonly List<Location> Locations = new();
    public event Action<Location> OnDiscovered;
    public event Action OnChanged;

    private Transform _player;
    private bool _traveling;

    private void Awake() => Instance = this;

    public void Configure(Transform player)
    {
        _player = player;
        if (Locations.Count == 0) BootstrapDefaultLocations();
        // Daggerfall starts discovered
        Discover("daggerfall", silent: true);
    }

    public void BootstrapDefaultLocations()
    {
        Locations.Clear();
        Add("daggerfall", "Daggerfall", new Vector3(-2000f, 24f, 1600f), new Vector3(-2000f, 25.2f, 1450f), true);
        Add("wayrest", "Wayrest", new Vector3(2200f, 22f, 1800f), new Vector3(2200f, 23.2f, 1550f), true);
        Add("sentinel", "Sentinel", new Vector3(-1600f, 18f, -2200f), new Vector3(-1600f, 19.2f, -1950f), true);
        Add("betony", "Betony", new Vector3(-2800f, 16f, 200f), new Vector3(-2800f, 17.2f, 200f), false);
        Add("balfiera", "Balfiera", new Vector3(150f, 28f, -100f), new Vector3(150f, 29.2f, -100f), false);
        Add("cybiades", "Cybiades", new Vector3(-900f, 14f, -700f), new Vector3(-900f, 15.2f, -700f), false);
        Add("bandit_camp", "Glenumbra Bandit Camp", new Vector3(-1750f, 24f, 850f), new Vector3(-1750f, 25.2f, 850f), false);
        Add("coastal_ruin", "Coastal Ruin", new Vector3(-2200f, 24f, 700f), new Vector3(-2200f, 25.2f, 700f), false);
    }

    private void Add(string id, string name, Vector3 world, Vector3 travel, bool city)
    {
        Locations.Add(new Location
        {
            Id = id,
            DisplayName = name,
            WorldPosition = world,
            TravelPosition = travel,
            IsCity = city,
            Discovered = false
        });
    }

    private void Update()
    {
        if (_player == null) return;
        foreach (var loc in Locations)
        {
            if (loc.Discovered) continue;
            if (Vector3.Distance(_player.position, loc.WorldPosition) < (loc.IsCity ? 280f : 90f))
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
        GameHud.Instance?.ShowFade(true);
        yield return new WaitForSecondsRealtime(0.45f);

        var player = _player != null ? _player.gameObject : GameObject.Find("Player");
        if (player != null)
        {
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            player.transform.position = loc.TravelPosition;
            if (cc != null) cc.enabled = true;
        }

        float dist = _player != null ? Vector3.Distance(_player.position, loc.TravelPosition) : 1000f;
        float hours = Mathf.Clamp(dist / 500f, 0.5f, 8f);
        TimeWeatherSystem.Instance?.AdvanceHours(hours);

        yield return new WaitForSecondsRealtime(0.2f);
        GameHud.Instance?.ShowFade(false);
        GameHud.Instance?.ShowToast($"Traveled to {loc.DisplayName}");
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
