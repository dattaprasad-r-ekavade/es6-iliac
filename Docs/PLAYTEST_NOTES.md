# Ratna Bay Slice Playtest Notes

Date: 2026-08-23
Build: .\publish.ps1 -Clean
Artifact: build\RatnaBay.exe (self-contained, 128.7 MB)

## Automated release gates

- 295 domain tests passed.
- dotnet run --project tools\RatnaBay.Tools -- sim passed the dialogue, quest, two
  combat events, reward, save, and reload path.
- Published content checks passed for world, dialogue, quest, shop, and fonts.
- Published RatnaBay.exe --selftest passed world loading and hot-reload fallback, collision,
  doors, two-trader dialogue, quest save state, stealth sight blocking, pickpocketing,
  pickup manifest, and shop purchase behavior.
- Packaged scene capture: [iteration12_packaged_scene_fixed.png](../artifacts/iteration12_packaged_scene_fixed.png).

## Human/external playtest status

Three external playthroughs are still required by the Iteration 12 definition of done. They
were not fabricated in this coding pass. The first Windows automation attempt was interrupted
by a physical Escape key before interaction could be inspected, so it is not counted as a
successful manual run.

### Owner manual pass — 2026-08-23

The owner reported four concrete issues during a local playtest: the Northwatch yard was
occluded by oversized imported prop/pickup meshes; bandits could not be seen reliably; Space
was free-flight rather than jump and Ctrl did not produce a readable sneak state; and stealth
feedback was too subtle. The follow-up fix widened the entry lane, corrected model bounds using
absolute bone transforms, changed Space to grounded jump physics, accepts either Ctrl key, and
adds an eye crosshair plus edge vignette while sneaking. A deterministic `--sneak` screenshot
now verifies the new HUD treatment.

The next owner pass found that placing the bandits in the open approach made them attack at
spawn. The authored progression is now explicit: room one is empty, room two contains only
Mara and Vesa as traders, and room three contains two bandits at its far end. The quest and
release simulation were updated to require those two kills.

### Owner playtest round two — 2026-08-23

Testers reported: the mouse pointer disappearing as menus opened and closed; no way to tell
whether a blow had landed or where a blow had come from; spells unusable because nothing on
screen said they existed; enemies lost the moment they left the view; the inventory being a
list that did nothing; and pickpocketing never exercised.

Investigating the last two found a hard content blocker. The pockets were authored at
difficulty 0, which always succeeds but trains nothing (a trivial target trains nothing), and
nothing else in the slice trains Security. Security therefore stayed at 0 forever, while the
watchpost door needed 15 and had no key — **the dungeon cache was unreachable in normal
play**, and pickpocketing had no purpose. Both are now the same answer: Vesa carries the
watchpost key in his pocket, and two dialogue topics point at it.

Fixed in this pass:

- The game draws its own pointer and never shows the system cursor, so it no longer blinks
  in and out with menu state.
- Hit markers on the crosshair, floating damage numbers over the target, and an arc pointing
  at whatever just hit the player.
- A readied-spell panel showing the spell, its prana cost, and whether it can be paid for.
- Threat markers around the crosshair for living enemies off to the sides.
- The inventory is selectable and usable: equip, wear, drink, draw, each with a description
  and the verb that applies. A potion is no longer spent at full health.
- Content that fails to load now says so on screen instead of dropping the player into a
  silent void.
- Vertical collision, so a jump cannot pass through a ceiling.
- The frame-rate readout shows nothing until its first window closes rather than a
  misleading opening figure.

Use this short script for each tester:

1. Start a new game and confirm the first room is empty and combat does not begin.
2. Enter room two, talk to Mara, buy one item, and accept the two-bandit quest.
3. Enter room three and fight the two bandits waiting at the far end.
4. Ask Mara or Vesa about the **watchpost**, pickpocket Vesa for the key, then open the
   watchpost door and take the cache.
5. Open the inventory with I, drink a potion and equip something, then cast a spell with Q.
6. Trigger death/recovery once, then save, quit, relaunch, and choose Continue.
7. Record where the tester hesitated, any missing prompt, and whether they could finish without
   developer help.

| Tester | Result | Notes |
|---|---|---|
| Owner pass two | Findings fixed | Pointer, hit feedback, spell bar, threat markers, usable inventory, and the key-in-pocket route through the watchpost door. |
| Owner manual pass | Findings fixed | Safe empty spawn, trader-only second room, and two-bandit third room are now enforced by the release self-test. Full live input pass still recommended. |
| External 1 | Pending | |
| External 2 | Pending | |
| External 3 | Pending | |

The release is implementation-complete but should not be called fully closed until these
three rows contain real player observations.
