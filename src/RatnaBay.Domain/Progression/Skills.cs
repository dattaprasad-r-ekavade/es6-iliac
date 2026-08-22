namespace RatnaBay.Domain;

/// <summary>
/// The eight skill ids. Stable and save-persisted, so they follow the naming policy: they
/// never embed a display name and code branches on them rather than on labels.
///
/// Each route grants two. `route.refuse` grants none — the fastest route gives the least,
/// which is the continuing price of taking it.
/// </summary>
public static class Skills
{
    public const string Blade = "skill.blade";
    public const string Block = "skill.block";
    public const string Heavy = "skill.heavy";
    public const string Marksman = "skill.marksman";
    public const string Destruction = "skill.destruction";
    public const string Restoration = "skill.restoration";
    public const string Stealth = "skill.stealth";
    public const string Security = "skill.security";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Blade, Block, Heavy, Marksman, Destruction, Restoration, Stealth, Security
    };

    private static readonly Dictionary<string, string> Labels = new(StringComparer.Ordinal)
    {
        [Blade] = "Blade",
        [Block] = "Block",
        [Heavy] = "Heavy Weapons",
        [Marksman] = "Marksman",
        [Destruction] = "Destruction",
        [Restoration] = "Restoration",
        [Stealth] = "Stealth",
        [Security] = "Security"
    };

    /// <summary>Display only. Never branch on this.</summary>
    public static string Label(string skillId) =>
        !string.IsNullOrEmpty(skillId) && Labels.TryGetValue(skillId, out var label) ? label : skillId;

    public static bool Exists(string? skillId) =>
        !string.IsNullOrEmpty(skillId) && Labels.ContainsKey(skillId);

    /// <summary>The two skills a route grants at assignment. Refuse grants none.</summary>
    public static IReadOnlyList<string> GrantedBy(string? routeId) => routeId switch
    {
        "route.warrior" => new[] { Blade, Block },
        "route.mage" => new[] { Destruction, Restoration },
        "route.trade" => new[] { Stealth, Security },
        _ => Array.Empty<string>()
    };
}
