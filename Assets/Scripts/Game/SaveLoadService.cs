using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

[Serializable]
public class SaveData
{
    public float Px, Py, Pz, Pyaw;
    public int Level, Xp, Gold;
    public float Health, Magicka, Stamina;
    public float MaxHealth, MaxMagicka, MaxStamina;
    public float TimeOfDay01;
    public List<string> Discovered = new();
    public List<InvItem> Items = new();
}

public class SaveLoadService : MonoBehaviour
{
    public static SaveLoadService Instance { get; private set; }

    private string Path => System.IO.Path.Combine(Application.persistentDataPath, "iliac_bay_save.json");
    private Vector3 _checkpoint;
    private bool _hasCheckpoint;

    private void Awake() => Instance = this;

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb.f5Key.wasPressedThisFrame) Save();
        if (kb.f9Key.wasPressedThisFrame) Load();
    }

    public void SetCheckpoint(Vector3 pos)
    {
        _checkpoint = pos;
        _hasCheckpoint = true;
    }

    public void Save()
    {
        var player = GameObject.Find("Player");
        var stats = PlayerStats.Instance;
        if (player == null || stats == null) return;

        var data = new SaveData
        {
            Px = player.transform.position.x,
            Py = player.transform.position.y,
            Pz = player.transform.position.z,
            Pyaw = player.transform.eulerAngles.y,
            Level = stats.Level,
            Xp = stats.Xp,
            Gold = stats.Gold,
            Health = stats.Health,
            Magicka = stats.Magicka,
            Stamina = stats.Stamina,
            MaxHealth = stats.MaxHealth,
            MaxMagicka = stats.MaxMagicka,
            MaxStamina = stats.MaxStamina,
            TimeOfDay01 = TimeWeatherSystem.Instance != null ? TimeWeatherSystem.Instance.TimeOfDay01 : 0.4f,
            Discovered = DiscoveryTravelSystem.Instance != null ? DiscoveryTravelSystem.Instance.GetDiscoveredIds() : new List<string>(),
            Items = PlayerInventory.Instance != null ? new List<InvItem>(PlayerInventory.Instance.Items) : new List<InvItem>()
        };

        File.WriteAllText(Path, JsonUtility.ToJson(data, true));
        SetCheckpoint(player.transform.position);
        GameHud.Instance?.ShowToast("Game saved (F5)");
    }

    public void Load()
    {
        if (!File.Exists(Path))
        {
            GameHud.Instance?.ShowToast("No save found");
            return;
        }

        var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(Path));
        Apply(data);
        GameHud.Instance?.ShowToast("Game loaded (F9)");
    }

    public void ReloadOrCheckpoint()
    {
        if (File.Exists(Path)) Load();
        else if (_hasCheckpoint)
        {
            var player = GameObject.Find("Player");
            if (player != null)
            {
                var cc = player.GetComponent<CharacterController>();
                if (cc) cc.enabled = false;
                player.transform.position = _checkpoint;
                if (cc) cc.enabled = true;
            }
            PlayerStats.Instance?.FullRestore();
        }
        else
        {
            PlayerStats.Instance?.FullRestore();
            var pad = GameObject.Find("SpawnPad_Daggerfall");
            var player = GameObject.Find("Player");
            if (pad && player)
            {
                var cc = player.GetComponent<CharacterController>();
                if (cc) cc.enabled = false;
                player.transform.position = pad.transform.position + Vector3.up;
                if (cc) cc.enabled = true;
            }
        }
    }

    private void Apply(SaveData data)
    {
        var player = GameObject.Find("Player");
        if (player != null)
        {
            var cc = player.GetComponent<CharacterController>();
            if (cc) cc.enabled = false;
            player.transform.position = new Vector3(data.Px, data.Py, data.Pz);
            player.transform.rotation = Quaternion.Euler(0f, data.Pyaw, 0f);
            if (cc) cc.enabled = true;
        }

        var stats = PlayerStats.Instance;
        if (stats != null)
        {
            // write via reflection-free public fields
            stats.Gold = data.Gold;
            stats.Health = data.Health;
            stats.Magicka = data.Magicka;
            stats.Stamina = data.Stamina;
            stats.MaxHealth = data.MaxHealth;
            stats.MaxMagicka = data.MaxMagicka;
            stats.MaxStamina = data.MaxStamina;
            // Level/Xp are private setters — use a restore helper
            stats.RestoreProgress(data.Level, data.Xp);
        }

        DiscoveryTravelSystem.Instance?.LoadDiscovered(data.Discovered);
        if (PlayerInventory.Instance != null && data.Items != null)
        {
            PlayerInventory.Instance.Items.Clear();
            PlayerInventory.Instance.Items.AddRange(data.Items);
        }

        if (TimeWeatherSystem.Instance != null)
            TimeWeatherSystem.Instance.SetTimeOfDay01(data.TimeOfDay01);
    }
}
