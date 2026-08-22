# Steam presentation baseline

**Project:** Ratna Bay  
**Target:** Windows Steam release  
**Current runtime:** MonoGame WindowsDX 3.8.5.1 / .NET 9  
**Status:** Implemented in the development shell

## Decision

The game starts in borderless fullscreen using the active display mode. The renderer uses
the real display viewport, while authored UI is designed against a stable 1280×720 logical
canvas.

This gives the project one layout to maintain while supporting 720p and larger desktop
displays. The interface is uniformly scaled and centered instead of being stretched. On a
non-16:9 display, the presentation may letterbox the logical canvas so panels and text keep
their intended proportions.

## Runtime behavior

| Mode | Behavior |
|---|---|
| Default | Borderless fullscreen at the active display resolution. |
| F11 | Toggle between borderless fullscreen and a 1280×720 windowed development view. |
| `--windowed` | Start the selected mode in a 1280×720 window. |
| 720p+ | UI scales uniformly from the 1280×720 authoring canvas. |
| Wider aspect ratios | Preserve the canvas aspect ratio and center it rather than distorting UI. |

The 3D camera continues to use the actual viewport aspect ratio. Only the SpriteBatch UI pass
uses the logical-canvas transform.

## Acceptance checks

- [x] Default executable opens without a title-bar window in the development environment.
- [x] Main menu remains readable in the fullscreen presentation.
- [x] The Northwatch scene and imported assets render in the fullscreen presentation.
- [x] Release build completes with zero warnings and zero errors.
- [x] Tool doctor and domain tests pass.
- [ ] Verify on a physical 1280×720 display.
- [ ] Verify on a physical 1920×1080 display.
- [ ] Verify on a physical ultrawide display.
- [ ] Verify F11 and settings persistence after the Settings screen exists.

## Design rules

1. Keep UI coordinates in logical 1280×720 space; do not add per-resolution coordinates.
2. Use fit-to-width or explicit line breaks for text blocks; never rely on accidental clipping.
3. Keep important information inside a conservative safe region so Steam overlay and display
   scaling do not hide it.
4. Test the default borderless presentation before approving a visual milestone.
5. Keep display settings separate from gameplay state and save data.

## Next presentation work

- Add a Settings screen with borderless/windowed choice, UI scale, and display selection.
- Add a safe-area preview/debug overlay.
- Add controller navigation for menu and Settings screens.
- Add automated screenshot captures at 1280×720, 1920×1080, and a wide aspect ratio.
- Check Steam overlay behavior and cursor visibility in a packaged build.
