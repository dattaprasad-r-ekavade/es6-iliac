namespace RatnaBay.Client;

/// <summary>
/// Every 2D screen renderer, constructed once against the shared canvas.
///
/// Game1 coordinates lifecycle and draw order. A change to a panel belongs in the named
/// renderer, not in Game1.
/// </summary>
internal sealed class UiScreens
{
    public UiScreens(UiCanvas canvas, Microsoft.Xna.Framework.Graphics.GraphicsDevice device)
    {
        Canvas = canvas;
        Hud = new HudRenderer(canvas);
        Overlay = new OverlayRenderer(canvas);
        Menu = new MenuRenderer(canvas);
        Character = new CharacterRenderer(canvas);
        Dialogue = new DialogueRenderer(canvas);
        Shop = new ShopRenderer(canvas);
        Journal = new JournalRenderer(canvas);
        Consent = new ConsentRenderer(canvas);
        Descent = new DescentRenderer(canvas);
        Fort = new FortRenderer(canvas, device);
        Markers = new MarkerRenderer(canvas);
        Console = new ConsoleRenderer(canvas);
    }

    public UiCanvas Canvas { get; }
    public HudRenderer Hud { get; }
    public OverlayRenderer Overlay { get; }
    public MenuRenderer Menu { get; }
    public CharacterRenderer Character { get; }
    public DialogueRenderer Dialogue { get; }
    public ShopRenderer Shop { get; }
    public JournalRenderer Journal { get; }
    public ConsentRenderer Consent { get; }
    public DescentRenderer Descent { get; }

    /// <summary>The fort between runs: ten doors, and who is behind them.</summary>
    public FortRenderer Fort { get; }

    /// <summary>Flat interface anchored to things in the world.</summary>
    public MarkerRenderer Markers { get; }

    public ConsoleRenderer Console { get; }
}
