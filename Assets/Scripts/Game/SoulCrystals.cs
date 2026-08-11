/// <summary>
/// The economy anchor for magic.
///
/// Magic runs on crystals; crystals run on souls. Mana is therefore a *charge* drawn from
/// a consumable, not a pool that refills itself — see Docs/GAMEPLAY_DESIGN.md § Resources.
///
/// Prices here are the early-game (Estmere) values. The arc's supply crisis raises them by
/// roughly 20x across the game, offset by skill mileage, so a committed mage pays about 5x
/// more per cast late on while a dabbler pays the full 20x.
/// </summary>
public static class SoulCrystals
{
    public const string LesserId = "soul_crystal_lesser";
    public const string LesserName = "Lesser Soul Crystal";
    public const string ItemKind = "crystal";

    /// <summary>Mana restored by one lesser crystal.</summary>
    public const float LesserCharge = 40f;

    /// <summary>Early-game price. Deliberately close to a meal or a health potion.</summary>
    public const int LesserBasePrice = 12;

    /// <summary>
    /// Floor on the skill discount. At mastery a spell costs 30% of its base, which is the
    /// 3–4x casts-per-crystal the design calls for.
    /// </summary>
    public const float MinCostMultiplier = 0.3f;
}
