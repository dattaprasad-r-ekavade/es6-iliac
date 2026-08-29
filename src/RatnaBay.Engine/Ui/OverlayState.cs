using System.Collections.Generic;

namespace RatnaBay.Engine.Ui;

/// <summary>Pause, help and settings, as the renderer is allowed to see them.</summary>
public sealed record OverlayState(
    bool InRun,
    int RoomsCleared,
    int PendingStones,
    IReadOnlyList<string> PauseItems,
    int PauseSelection,
    IReadOnlyList<string> SettingsOptions,
    int SettingsSelection,
    string RecordingDirectory);
