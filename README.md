# Ratna Bay

An open-world first-person RPG prototype set on Ratna Bay, between temperate Uttara in the
north and arid Maru in the south. A lawful jiva stone receives the pranic imprint released
through dāna while the jiva moves on; a black jiva cages a continuing person. Every point of
prana drawn still burdens the Stambha.

**The look is Elder Scrolls: Arena read through Rajput and Pahari miniature painting** —
flat high-chroma pigment, hard drawn contours, billboard sprite characters, and every
texture generated in code at 64 px rather than authored as a file. Locked 2026-08-12; see
the art direction section of [plan.md](plan.md).

> The repository directory, `.sln`, `Kessil*` class names, stable ids and editor menu root
> retain internal codenames. Player-facing product, world, story and dialogue names use Ratna
> Bay. Internal renames are deliberately deferred so they cannot invalidate saves or tooling.

## Contributing or resuming work

Start at [`Docs/AGENT_HANDOFF.md`](Docs/AGENT_HANDOFF.md) — it carries the authority map,
verification commands, invariants, known traps and the ordered work packets. `CLAUDE.md` is
the short version loaded automatically by Claude Code.

## Play
1. Open `Assets/Scenes/Bootstrap.unity`
2. Press **Play**
3. Click **START** on the menu
4. Skip the intro any time with **Space** / **SKIP**
5. The prologue plays, then you are standing in Ratnapur. The compass line gives written
   directions to your objective — walk there and press **E** at the door.

## Controls
WASD · mouse look · **Shift** walk (you run by default) · Space jump · Esc unlock cursor  
**M** map/FT · **J** journal · **I** inventory · **T** wait · **E** talk  
**LMB/1** melee · **RMB/LAlt** block · **2** cast · **Q** potion · **F5** save · **F9** load

Talk to the named cast with **E**, then pick a subject with **1–9**. Keywords you learn from
one person can be asked of another.

After gameplay starts, the VS2 grey thread opens the Raja's audience assignment panel. Enter a
name, then choose **City Guard**, **The Siddha Order**, **Docks / Commerce**, or **Refuse
Assignment**. Each route records its profile and ends at the Sabhapur council handoff.

The named cast are Raja Vikram, Senapati Karan, Acharya Meera, Harbourmaster Vasu, the
Registrar, Hari and Lekha in the prison, and Mantri Devan at the council gate.

## Future plan
The free-roam P0+P1 prototype foundation exists, but the active deliverable is now the
**complete Chapter 01 vertical slice** from `storyline.md`. VS0's 42-beat contract and VS1's
technical spine are complete. VS2 is also complete: regenerable grey rooms, a real audience
assignment UI, all 42 beat waypoints, the B640 title crawl, additive scene transitions and all
four route branches reach B830. The P1 runtime repair, adopted-setting migration, locked jiva
contract, content guard and first Arena Miniature street/dungeon proof are complete. The
external **Ratna World Builder MVP**, its one-button Unity preview and 14/14 Python tests are
also complete, and
Shantipur now has a baseline highland city and connecting road. The next world task is the
full W-12 dense-region rebuild; free-form heightmaps, marker import, a real-controller
walkthrough and the 45+ FPS acceptance pass remain open.

Current goals: **[Docs/FEATURES_ROADMAP.md](Docs/FEATURES_ROADMAP.md)**. Detailed gates,
risks and estimates: **[plan.md](plan.md)**. Beat contract:
**[Docs/CHAPTER01_BEATS.md](Docs/CHAPTER01_BEATS.md)**.

VS2 captures: [Ratnapur Palace](Docs/Screenshots/vs2-estmere-palace.png) and
[Sabhapur Sabha Gate](Docs/Screenshots/vs2-caldemar-arrival.png). Arena Miniature proof:
[Ratnapur dock street](Docs/Screenshots/arena-miniature-ratnapur-street.png) and
[Ratnapur prison](Docs/Screenshots/arena-miniature-prison.png). World-authoring preview:
[top-down PNG](Docs/Screenshots/world-builder-preview.png).

## Edit the world without opening Unity

Double-click `Tools/WorldBuilder/Launch World Builder.cmd`, or run:

```powershell
python Tools/WorldBuilder/world_builder.py
```

The MVP edits the current landmass centres/sizes/elevation parameters, biomes, roads, sites,
city gates and story-spawn metadata. It validates before saving, supports undo/redo, creates
backups, exports PNG/SVG previews and can launch the sanctioned headless Unity rebuild and
capture bridge with one button. See [`Tools/WorldBuilder/README.md`](Tools/WorldBuilder/README.md).

Its present boundary is deliberate: coastlines still use the runtime's elliptical landmass
model, and gate/story-spawn metadata is not consumed by runtime placement yet. Free-form
heightmap sculpting and the marker importer belong to W-12/tool expansion, not this MVP.

## Rebuild
**Kessil → Systems → Install P0+P1 + Rebuild World**  
(or Presentation → Setup Menu + Cutscene + Smooth Map)

The rebuild regenerates the Bootstrap/exterior scene architecture automatically. It can
also be refreshed explicitly with **Kessil → Architecture → Install Bootstrap + Additive
Scenes**.

> ⚠️ The rebuild **deletes every root object in `Main.unity`** and regenerates it.
> Anything hand-placed in the scene is lost. The scene is currently a build artifact,
> not an authoring surface — see plan.md.

## Working on the code

- **World data** (landmasses, cities, POIs and roads) is authored in
  `Assets/Resources/Data/World/kessil.world.json`. `WorldLayout` is the stable runtime API;
  the generator, map art, fast travel and content spawners all read through it. The filename
  and `Kessil*` types are retained internal codenames—do not re-hardcode coordinates.
- **Physics layers** are declared in `Assets/Scripts/World/GameLayers.cs` and assigned
  at runtime by `WorldTagger`. That tagger is the only place a GameObject's *name* is
  used to decide what something is; everything else queries layers.
- **Compile without opening Unity** (works while the editor holds the project lock —
  each assembly is checked against its own asmdef reference set):
  ```
  python Tools/compile-check.py
  ```

### Headless commands (Unity must be closed)

```bash
UNITY="C:/Program Files/Unity/Hub/Editor/6000.5.3f1/Editor/Unity.exe"

# Rebuild the world + systems into Main.unity
"$UNITY" -batchmode -quit -nographics -projectPath . -logFile Logs/rebuild.log \
         -executeMethod SetupP0P1Systems.InstallAndRebuild

# Run the edit-mode tests
"$UNITY" -batchmode -nographics -projectPath . -logFile Logs/tests.log \
         -runTests -testPlatform EditMode -testResults TestResults.xml

# Build the Windows player
"$UNITY" -batchmode -quit -nographics -projectPath . -logFile Logs/build.log \
         -executeMethod BuildPlayerCommand.BuildWindows

# Cap third-party texture import sizes
"$UNITY" -batchmode -quit -nographics -projectPath . -logFile Logs/tex.log \
         -executeMethod TextureBudget.Apply
```
