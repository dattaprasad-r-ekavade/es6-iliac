using UnityEngine;

/// <summary>
/// Day/night cycle + regional weather (High Rock wetter, Hammerfell dustier).
/// </summary>
public class TimeWeatherSystem : MonoBehaviour
{
    public static TimeWeatherSystem Instance { get; private set; }

    public enum Weather
    {
        Clear,
        Cloudy,
        Rain,
        Storm,
        Fog,
        Dust
    }

    [SerializeField] private float minutesPerRealSecond = 2f;
    [SerializeField] private float startHour = 9f;
    [SerializeField] private Light sun;

    public float TimeOfDay01 { get; private set; }
    public float Hour => TimeOfDay01 * 24f;
    public Weather CurrentWeather { get; private set; } = Weather.Clear;
    public string CurrentRegion { get; private set; } = "HighRock";

    private ParticleSystem _rain;
    private ParticleSystem _dust;
    private Transform _player;
    private float _weatherTimer;
    private Color _baseSun = new Color(1f, 0.96f, 0.88f);

    private void Awake()
    {
        Instance = this;
        TimeOfDay01 = Mathf.Repeat(startHour / 24f, 1f);
    }

    public void Configure(Light directional, Transform player)
    {
        sun = directional;
        _player = player;
        if (sun != null) _baseSun = sun.color;
        EnsureFx();
        UpdateRegion();
        RollWeatherForRegion(true);
        ApplyVisuals();
    }

    public void AdvanceHours(float hours)
    {
        TimeOfDay01 = Mathf.Repeat(TimeOfDay01 + hours / 24f, 1f);
        UpdateRegion();
        RollWeatherForRegion(true);
        ApplyVisuals();
    }

    public void SetTimeOfDay01(float t)
    {
        TimeOfDay01 = Mathf.Repeat(t, 1f);
        UpdateRegion();
        RollWeatherForRegion(true);
        ApplyVisuals();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (_player == null) _player = PlayerRef.Transform;

        float dayFractionPerSecond = minutesPerRealSecond / (24f * 60f);
        TimeOfDay01 = Mathf.Repeat(TimeOfDay01 + dayFractionPerSecond * Time.deltaTime, 1f);

        bool regionChanged = UpdateRegion();
        _weatherTimer -= Time.deltaTime;
        if (regionChanged || _weatherTimer <= 0f) RollWeatherForRegion(regionChanged);

        ApplyVisuals();
        UpdateFxFollow();
    }

    private bool UpdateRegion()
    {
        if (_player == null) return false;

        var p = _player.position;
        string region;
        if (p.z < -800f) region = "Hammerfell";
        else if (Mathf.Abs(p.x) < 500f && Mathf.Abs(p.z) < 500f) region = "Bay";
        else region = "HighRock";

        if (CurrentRegion == region) return false;
        CurrentRegion = region;
        return true;
    }

    private void RollWeatherForRegion(bool force)
    {
        _weatherTimer = force ? 45f : Random.Range(40f, 90f);
        float r = Random.value;
        switch (CurrentRegion)
        {
            case "Hammerfell":
                CurrentWeather = r < 0.55f ? Weather.Clear : r < 0.8f ? Weather.Dust : Weather.Cloudy;
                break;
            case "Bay":
                CurrentWeather = r < 0.35f ? Weather.Fog : r < 0.7f ? Weather.Cloudy : Weather.Clear;
                break;
            default:
                CurrentWeather = r < 0.25f ? Weather.Clear : r < 0.5f ? Weather.Cloudy : r < 0.8f ? Weather.Rain : Weather.Storm;
                break;
        }
    }

    private void EnsureFx()
    {
        if (_rain == null) _rain = CreateSpray("RainFx", new Color(0.7f, 0.75f, 0.85f, 0.65f), 900);
        if (_dust == null) _dust = CreateSpray("DustFx", new Color(0.75f, 0.6f, 0.35f, 0.45f), 350);
    }

    private ParticleSystem CreateSpray(string name, Color c, float rate)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startSpeed = 14f;
        main.startLifetime = 1.2f;
        main.startSize = 0.07f;
        main.startColor = c;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 5000;
        main.gravityModifier = name.Contains("Rain") ? 1.2f : 0.05f;
        var emission = ps.emission;
        emission.rateOverTime = rate;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(45f, 2f, 45f);
        var rend = go.GetComponent<ParticleSystemRenderer>();
        var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                 ?? Shader.Find("Particles/Standard Unlit")
                 ?? Shader.Find("Sprites/Default");
        rend.material = new Material(sh);
        if (rend.material.HasProperty("_BaseColor")) rend.material.SetColor("_BaseColor", c);
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return ps;
    }

    private void UpdateFxFollow()
    {
        if (_player == null) return;
        var pos = _player.position + Vector3.up * 16f + _player.forward * 6f;
        if (_rain != null) _rain.transform.position = pos;
        if (_dust != null) _dust.transform.position = pos;
    }

    private void ApplyVisuals()
    {
        float hour = Hour;
        float dayFactor = Mathf.Clamp01(1f - Mathf.Abs(hour - 12f) / 12f);
        bool night = hour < 5.5f || hour > 20.5f;

        if (sun != null)
        {
            float sunAngle = (TimeOfDay01 * 360f) - 90f;
            sun.transform.rotation = Quaternion.Euler(sunAngle, -30f, 0f);
            float intensity = night ? 0.08f : Mathf.Lerp(0.35f, 1.25f, dayFactor);
            if (CurrentWeather == Weather.Storm || CurrentWeather == Weather.Rain) intensity *= 0.55f;
            if (CurrentWeather == Weather.Fog) intensity *= 0.7f;
            if (CurrentWeather == Weather.Dust) intensity *= 0.85f;
            sun.intensity = intensity;
            sun.color = night ? new Color(0.55f, 0.6f, 0.85f) : _baseSun;
        }

        Color fogColor;
        float fogDensity;
        switch (CurrentWeather)
        {
            case Weather.Storm:
                fogColor = new Color(0.25f, 0.28f, 0.32f);
                fogDensity = 0.012f;
                break;
            case Weather.Rain:
                fogColor = new Color(0.35f, 0.38f, 0.42f);
                fogDensity = 0.008f;
                break;
            case Weather.Fog:
                fogColor = new Color(0.55f, 0.58f, 0.6f);
                fogDensity = 0.02f;
                break;
            case Weather.Dust:
                fogColor = new Color(0.55f, 0.45f, 0.28f);
                fogDensity = 0.01f;
                break;
            case Weather.Cloudy:
                fogColor = new Color(0.45f, 0.48f, 0.52f);
                fogDensity = 0.005f;
                break;
            default:
                fogColor = night ? new Color(0.05f, 0.06f, 0.1f) : new Color(0.55f, 0.65f, 0.75f);
                fogDensity = night ? 0.004f : 0.0015f;
                break;
        }

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogDensity = fogDensity;
        var cam = Camera.main;
        if (cam != null) cam.backgroundColor = Color.Lerp(fogColor, Color.black, night ? 0.45f : 0f);

        bool raining = CurrentWeather == Weather.Rain || CurrentWeather == Weather.Storm;
        bool dusty = CurrentWeather == Weather.Dust;
        if (_rain != null)
        {
            if (raining && !_rain.isPlaying) _rain.Play();
            if (!raining && _rain.isPlaying) _rain.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            var e = _rain.emission;
            e.rateOverTime = CurrentWeather == Weather.Storm ? 2000f : 900f;
        }

        if (_dust != null)
        {
            if (dusty && !_dust.isPlaying) _dust.Play();
            if (!dusty && _dust.isPlaying) _dust.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }
}
