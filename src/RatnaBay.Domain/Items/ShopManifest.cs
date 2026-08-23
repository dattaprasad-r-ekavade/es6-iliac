using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace RatnaBay.Domain;

public sealed class ShopManifest
{
    public int Version { get; set; } = 1;
    public string Id { get; set; } = string.Empty;
    public List<ShopDefinitionData> Shops { get; set; } = new();

    public static bool TryLoad(string path, out ShopManifest? manifest, out string error)
    {
        manifest = null;
        error = string.Empty;
        try
        {
            if (!File.Exists(path))
            {
                error = $"Shop manifest not found: {path}";
                return false;
            }
            return TryParse(File.ReadAllText(path), out manifest, out error);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            error = $"Could not read shop manifest: {exception.Message}";
            return false;
        }
    }

    public static bool TryParse(string json, out ShopManifest? manifest, out string error)
    {
        manifest = null;
        error = string.Empty;
        try { manifest = JsonSerializer.Deserialize<ShopManifest>(json, Options); }
        catch (JsonException exception)
        {
            error = $"Invalid shop manifest JSON: {exception.Message}";
            return false;
        }
        if (manifest is null)
        {
            error = "Shop manifest is empty.";
            return false;
        }

        var failures = manifest.Validate();
        if (failures.Count > 0)
        {
            error = string.Join(" ", failures);
            manifest = null;
            return false;
        }
        return true;
    }

    public IReadOnlyList<string> Validate()
    {
        var failures = new List<string>();
        if (Version != 1) failures.Add($"version must be 1, got {Version}.");
        if (string.IsNullOrWhiteSpace(Id)) failures.Add("id is required.");
        var shopIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var shop in Shops ?? new List<ShopDefinitionData>())
        {
            if (shop is null || string.IsNullOrWhiteSpace(shop.Id)) failures.Add("shop id is required.");
            else if (!shopIds.Add(shop.Id)) failures.Add($"duplicate shop id '{shop.Id}'.");
            if (shop is null || string.IsNullOrWhiteSpace(shop.DisplayName))
                failures.Add($"shop '{shop?.Id ?? "<null>"}' needs a displayName.");
            var itemIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in shop?.Items ?? new List<ShopItemData>())
            {
                if (item is null || string.IsNullOrWhiteSpace(item.Id) || !itemIds.Add(item.Id))
                    failures.Add($"shop '{shop?.Id ?? "<null>"}' has an invalid or duplicate item id.");
                if (item is null || string.IsNullOrWhiteSpace(item.Name) || string.IsNullOrWhiteSpace(item.Kind))
                    failures.Add($"shop item '{item?.Id ?? "<null>"}' needs a name and kind.");
                if (item is not null && (item.Price < 0 || item.Count <= 0))
                    failures.Add($"shop item '{item.Id}' needs a non-negative price and positive count.");
            }
        }
        return failures;
    }

    public IReadOnlyList<ShopDefinition> ToDefinitions() =>
        (Shops ?? new List<ShopDefinitionData>()).Select(shop => shop.ToDomain()).ToList();

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true
    };
}

public sealed class ShopDefinitionData
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public List<ShopItemData> Items { get; set; } = new();

    public ShopDefinition ToDomain() => new()
    {
        Id = Id,
        DisplayName = DisplayName,
        Items = (Items ?? new List<ShopItemData>()).Select(item => item.ToDomain()).ToList()
    };
}

public sealed class ShopItemData
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public int Price { get; set; }
    public int Count { get; set; } = 1;

    public ShopItemDefinition ToDomain() => new()
    {
        Id = Id, Name = Name, Kind = Kind, Price = Price, Count = Count
    };
}
