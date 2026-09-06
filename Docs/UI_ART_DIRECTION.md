# Portraits and interface — September 2026

The user requested a younger Revati, Uttara on the character sheet, large Fallout-style dialogue faces, and a GUI/HUD revamp. This supersedes the prior requirement to display generated faces and the pixel typeface by default.

## Implemented

- Close-up painted portraits for the ten fort occupants, at 432×432 logical pixels, separate from the 32×48 world billboards.
- Revati is art-directed as approximately 27; her procedural reference age also matches.
- Uttara appears on the revised sheet as a mature mine superintendent/rescuer, with indigo work wraps, a tally stick and practical tools. Her atlas cell is available for future story content. No new encounter or lore event is activated by this art change.
- Fort dialogue shows the greeting and then one unlocked passage at a time. Enter/Right continues, Left returns, Escape leaves the conversation. Buttons share layout with mouse hit-testing. Only passages actually reached are marked heard; drawing is read-only.
- Other topic dialogue uses the same portrait frame and displays the selected page of topic rows.
- Charcoal surfaces, bronze frames, warm paper text; Noto Sans body and Cinzel headings by default. `--pixel-font` remains an explicit comparison option; `--book-font` selects the default.
- Compact health/prana/stamina gauges, a separate at-risk jiva ledger, restrained spell/gear displays and a centred location caption. Prana now uses amber consistently with the energy fiction.
- Shared panels, menus, inventory, shop and overlays follow the new palette and frame treatment. Existing navigation and gameplay rules remain in their established handlers.

## Art and limits

September 6 follow-up: the repeated bronze boxes were too stiff. Conversation portraits
now occupy a 496-pixel square with softened edges and reduced surrounding chrome. Shared
panels have subdued wear and rows use selection strokes. See `CAVE_ART_DIRECTION.md` for
the environment revision and the remaining distinction between this pass and a bespoke HUD.

`Art/characters-revati-uttara.png` is the revised seven-character reference sheet. `../src/RatnaBay.Game/Content/Art/Portraits/cast.png` is the 4×3 close-up atlas; its README records the cell mapping and provenance.

Portraits are static illustrations. This pass does not claim animated mouths, lip sync, blinking, or six painted expressions per person. The old six-expression generator remains available for authoring comparisons; the new dialogue faces do not pretend to change mood by tinting the whole face. Unique expression art is the next asset task if required.

The non-fort dialogue actors currently share a generic trader portrait until each receives an authored identity. The sheet's Uttara represents the remembered rescuer, not a final endgame guardian design.

The character sheet is a concept reference and not a correctly packed world-sprite atlas. World sprites have not been replaced by scaled-down portraits.

## Checking it

Use `--yard --show revati --screenshot <path>` or `--yard --show ganaka --screenshot <path>` to inspect the actual opening conversations. `--show dialogue` covers topic dialogue, and the existing `--show character`, `shop`, `fort`, `pause`, and `help` cover the surrounding interface.

The doctor and packaged self-test check that the portrait atlas is present. Session self-tests exercise conversation keys, the drawn button bounds and topic-dialogue exit. The regular smoke script also captures the restyled populated HUD. Run `verify.ps1 -Pack`, then inspect packaged conversation captures; do not infer visual correctness from the file-presence check.
