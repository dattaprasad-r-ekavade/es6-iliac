namespace RatnaBay.Domain;

/// <summary>
/// What a face is doing while a line is said.
///
/// **This lives in the domain because it is writing, not rendering.** Whoever writes a fragment
/// decides how it is delivered, in the same place and at the same moment they choose the words;
/// a table of moods maintained next to the renderer would drift away from the text inside a
/// week. The render layer reads this and draws it, and knows nothing else about it.
///
/// Six, and adding a seventh needs an argument. Every one of these has to be legible at a
/// glance in a portrait a player is not really looking at, and the more of them there are the
/// closer together they sit.
/// </summary>
public enum Expression
{
    Neutral,

    /// <summary>Pleased to see you. The eyes narrow rather than the mouth widening.</summary>
    Warm,

    /// <summary>Not hostile. Deciding how much to say.</summary>
    Wary,

    /// <summary>Sad. The one expression that must never read as merely tired.</summary>
    Grieved,

    Angry,
    Afraid
}
