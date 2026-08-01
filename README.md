# Kessil Bay

An open-world first-person RPG prototype set on Kessil Bay, between temperate Halbrand
in the north and arid Sarrakh in the south.

> The repository directory and `.sln` are still named `Elder Scrolls 6` from the original
> scaffolding. Everything inside the project has been renamed; the folder itself is the
> last step and needs Unity closed. See the naming policy in [plan.md](plan.md).

## Contributing or resuming work

Start at [`Docs/AGENT_HANDOFF.md`](Docs/AGENT_HANDOFF.md) — it carries the authority map,
verification commands, invariants, known traps and the ordered work packets. `CLAUDE.md` is
the short version loaded automatically by Claude Code.

## Play
1. Open `Assets/Scenes/Main.unity`
2. Press **Play**
3. Click **START** on the menu
4. Skip dialogue anytime (**Space** / **SKIP**) → scenic flyover → play at Caldemar

## Controls
WASD · mouse look · **Shift** sprint · Space jump · Esc unlock cursor  
**M** map/FT · **J** journal · **I** inventory · **T** wait · **E** talk  
**LMB/1** melee · **2** flare · **Q** potion · **F5** save · **F9** load

## Future plan
The free-roam P0+P1 prototype foundation exists, but the active deliverable is now the
**complete Chapter 01 vertical slice** from `storyline.md`. VS0 is in progress: its
42-beat contract is complete, while screenplay, blocking narrative locks, the regression
snapshot and asset ledger remain. VS1 (persistent story architecture) is next, followed by
VS2 (the end-to-end grey thread).

Current goals: **[Docs/FEATURES_ROADMAP.md](Docs/FEATURES_ROADMAP.md)**. Detailed gates,
risks and estimates: **[plan.md](plan.md)**. Beat contract:
**[Docs/CHAPTER01_BEATS.md](Docs/CHAPTER01_BEATS.md)**.

## Rebuild
**Kessil → Systems → Install P0+P1 + Rebuild World**  
(or Presentation → Setup Menu + Cutscene + Smooth Map)

> ⚠️ The rebuild **deletes every root object in `Main.unity`** and regenerates it.
> Anything hand-placed in the scene is lost. The scene is currently a build artifact,
> not an authoring surface — see plan.md.

## Working on the code

- **World data** (landmasses, cities, POIs, roads, spawn/safe-zone geometry) lives in
  one place: `Assets/Scripts/World/WorldLayout.cs`. The generator, map art, fast travel
  and content spawners all read from it — don't re-hardcode coordinates.
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
