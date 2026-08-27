using Microsoft.Xna.Framework;
using RatnaBay.Domain;
using System;
using System.Collections.Generic;

namespace RatnaBay.Client;

public enum HairStyle { Shaven, Cropped, Bound, Long, Braid, Cloth }

public enum Beard { None, Stubble, Moustache, Short, Full }

public enum Headwear { None, Cap, Turban, Helmet }

public enum Ornament { None, Earrings, Tilaka, Necklace }

/// <summary>The colours one face is drawn from.</summary>
public readonly record struct FacePalette(
    Color Skin, Color Hair, Color Garment, Color Trim, Color Eye);

/// <summary>
/// One person, as numbers.
///
/// Every field is something a player can tell two characters apart by while reading text
/// underneath them: silhouette first (hair, headwear, shoulders), then colour, then the small
/// stuff. That ordering is why <see cref="Hair"/> and <see cref="Headwear"/> carry more weight
/// than <see cref="NoseLength"/>, and why no two fort occupants may share the first two.
/// </summary>
public sealed record FaceDescription(
    FacePalette Palette,
    HairStyle Hair,
    Beard Beard,
    Headwear Headwear,
    Ornament Ornament,

    /// <summary>0 is a narrow face, 1 is a broad one. Scales skull and jaw together.</summary>
    float Width = 0.5f,

    /// <summary>0 is twenty, 1 is seventy. Drives the brow, the jowl, and greying.</summary>
    float Age = 0.5f,

    /// <summary>Shoulder span. A smith and a clerk are different shapes at any distance.</summary>
    float Build = 0.5f,

    /// <summary>Heavier brows read as older and more severe whatever the expression.</summary>
    float BrowWeight = 0.5f,

    float NoseLength = 0.5f,

    /// <summary>
    /// The face they wear when they are not saying anything in particular.
    ///
    /// Shown against the room's standing greeting, so a player who walks into the shrine and
    /// the barracks on the same visit meets two different people before either has spoken a
    /// line they have not heard before. Neutral for anyone whose default really is neutral.
    /// </summary>
    Expression Resting = Expression.Neutral);

/// <summary>
/// The ten people in the fort.
///
/// **Silhouette is the whole budget.** A dialogue portrait is looked at for two seconds by a
/// player who is reading underneath it, so what separates two characters has to be the shape
/// they cut against the panel — never the shade of their skin, and never a detail that only
/// survives at full zoom. Ten distinct outlines is the requirement, and <c>PortraitTests</c>
/// asserts it rather than trusting this paragraph.
/// </summary>
public static class FaceCatalog
{
    private static readonly Dictionary<string, FaceDescription> Faces =
        new(StringComparer.Ordinal)
        {
            // Ganaka. Eleven years at the same table, and it shows before he speaks.
            ["fort.gate"] = new(
                new FacePalette(new Color(196, 148, 106), new Color(96, 92, 88),
                    new Color(104, 98, 86), new Color(72, 66, 58), new Color(58, 44, 34)),
                HairStyle.Cropped, Beard.Short, Headwear.None, Ornament.None,
                Width: 0.34f, Age: 0.76f, Build: 0.38f, BrowWeight: 0.62f, NoseLength: 0.60f, Resting: Expression.Wary),

            // Revati. The only warm palette in the building, and that is deliberate.
            ["fort.hall"] = new(
                new FacePalette(new Color(184, 130, 92), new Color(44, 34, 30),
                    new Color(150, 96, 44), new Color(134, 66, 56), new Color(52, 38, 28)),
                HairStyle.Cloth, Beard.None, Headwear.None, Ornament.Earrings,
                Width: 0.44f, Age: 0.48f, Build: 0.40f, BrowWeight: 0.34f, NoseLength: 0.44f, Resting: Expression.Warm),

            // Nagadatta. Weighs, does not ask, and never leaves the room.
            ["fort.assay"] = new(
                new FacePalette(new Color(172, 122, 84), new Color(38, 34, 32),
                    new Color(62, 66, 72), new Color(96, 88, 72), new Color(46, 36, 30)),
                HairStyle.Shaven, Beard.Moustache, Headwear.None, Ornament.None,
                Width: 0.78f, Age: 0.54f, Build: 0.62f, BrowWeight: 0.70f, NoseLength: 0.52f),

            // Lohasena. Widest shoulders in the fort, and soot worked into the skin.
            ["fort.forge"] = new(
                new FacePalette(new Color(150, 100, 68), new Color(30, 26, 24),
                    new Color(84, 52, 38), new Color(58, 44, 36), new Color(40, 30, 24)),
                HairStyle.Bound, Beard.Full, Headwear.None, Ornament.None,
                Width: 0.70f, Age: 0.42f, Build: 0.92f, BrowWeight: 0.76f, NoseLength: 0.56f),

            // Visakha. Undyed linen, because everything she owns has been boiled.
            ["fort.physician"] = new(
                new FacePalette(new Color(198, 152, 116), new Color(52, 40, 36),
                    new Color(198, 194, 180), new Color(140, 148, 142), new Color(56, 44, 34)),
                HairStyle.Long, Beard.None, Headwear.None, Ornament.Tilaka,
                Width: 0.40f, Age: 0.54f, Build: 0.36f, BrowWeight: 0.34f, NoseLength: 0.48f, Resting: Expression.Warm),

            // Suvarnapala. The only person here dressed by the capital rather than the province.
            ["fort.registrar"] = new(
                new FacePalette(new Color(206, 164, 122), new Color(46, 38, 32),
                    new Color(72, 60, 108), new Color(198, 162, 76), new Color(50, 40, 30)),
                HairStyle.Cropped, Beard.None, Headwear.Turban, Ornament.Necklace,
                Width: 0.66f, Age: 0.50f, Build: 0.54f, BrowWeight: 0.44f, NoseLength: 0.62f),

            // Isidata. Tolerated, and only while he stays quiet.
            ["fort.shrine"] = new(
                new FacePalette(new Color(178, 132, 96), new Color(70, 66, 62),
                    new Color(178, 106, 42), new Color(146, 82, 32), new Color(48, 38, 30)),
                HairStyle.Shaven, Beard.None, Headwear.None, Ornament.Tilaka,
                Width: 0.30f, Age: 0.66f, Build: 0.30f, BrowWeight: 0.36f, NoseLength: 0.66f, Resting: Expression.Grieved),

            // Bhadrasena. The only helmet, so he is identifiable at any size at all.
            ["fort.barracks"] = new(
                new FacePalette(new Color(164, 116, 82), new Color(34, 32, 34),
                    new Color(60, 78, 104), new Color(148, 140, 128), new Color(44, 36, 32)),
                HairStyle.Cropped, Beard.Stubble, Headwear.Helmet, Ornament.None,
                Width: 0.72f, Age: 0.44f, Build: 0.80f, BrowWeight: 0.82f, NoseLength: 0.50f, Resting: Expression.Wary),

            // Chandrashri. The youngest person in the fort and the only one still arguing.
            ["fort.clerk"] = new(
                new FacePalette(new Color(190, 142, 104), new Color(36, 28, 26),
                    new Color(88, 104, 96), new Color(126, 138, 128), new Color(48, 38, 30)),
                HairStyle.Braid, Beard.None, Headwear.None, Ornament.Earrings,
                Width: 0.38f, Age: 0.26f, Build: 0.38f, BrowWeight: 0.42f, NoseLength: 0.42f, Resting: Expression.Wary),

            // Vasumitra. Thirty years of choosing none of the options.
            ["fort.governor"] = new(
                new FacePalette(new Color(202, 158, 118), new Color(180, 176, 168),
                    new Color(96, 44, 52), new Color(196, 160, 82), new Color(56, 46, 36)),
                HairStyle.Long, Beard.Full, Headwear.Cap, Ornament.Necklace,
                Width: 0.74f, Age: 0.90f, Build: 0.58f, BrowWeight: 0.58f, NoseLength: 0.64f, Resting: Expression.Grieved)
        };

    public static IReadOnlyDictionary<string, FaceDescription> All => Faces;

    public static FaceDescription? Find(string? roomId) =>
        roomId is not null && Faces.TryGetValue(roomId, out var face) ? face : null;
}
