namespace RatnaBay.Domain;

/// <summary>
/// What a life path is worth in a fight and at a stall.
///
/// The three paths are not three power levels; they are three curves. The warrior is strongest
/// first and stops scaling, the mage is fragile and compounds, and the trader is given no gift
/// with either weapon or spell — what it has is price, and price compounds hardest of all.
/// </summary>
public sealed class LifePath
{
    /// <summary>What the gifted discipline is multiplied by.</summary>
    public const float Gifted = 2f;

    /// <summary>What the second discipline is multiplied by.</summary>
    public const float Secondary = 1.25f;

    /// <summary>
    /// Trader prices are the list price raised to this power.
    ///
    /// The exponent applies to a number carrying units, so the gold scale is load-bearing: a
    /// price of 12 costs a trader 6, and a price of 20,000 costs them 1,682. Rescaling gold
    /// would silently rewrite every discount in the game.
    /// </summary>
    public const float TraderPriceExponent = 0.75f;

    public string RouteId { get; private set; } = string.Empty;

    public float WeaponMultiplier { get; private set; } = 1f;
    public float SpellMultiplier { get; private set; } = 1f;
    public float PriceExponent { get; private set; } = 1f;

    public void Select(string? routeId)
    {
        RouteId = routeId ?? string.Empty;

        (WeaponMultiplier, SpellMultiplier, PriceExponent) = routeId switch
        {
            StoryDirector.RouteWarrior => (Gifted, Secondary, 1f),
            StoryDirector.RouteMage => (Secondary, Gifted, 1f),
            StoryDirector.RouteTrade => (1f, 1f, TraderPriceExponent),

            // Refusing the route is the fastest start and the one that grants least.
            _ => (1f, 1f, 1f)
        };
    }

    /// <summary>What this path actually pays for something on a shelf.</summary>
    public int PriceOf(int listPrice)
    {
        if (listPrice <= 1 || PriceExponent >= 1f) return listPrice;
        return Math.Max(1, (int)MathF.Round(MathF.Pow(listPrice, PriceExponent)));
    }
}
