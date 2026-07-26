# Elder Scrolls 6 — Iliac Bay Homage

## Play
1. Open `Assets/Scenes/Main.unity`
2. Press **Play**
3. Click **START** on the menu
4. Skip dialogue anytime (**Space** / **SKIP**) → scenic flyover → play at Daggerfall

## Controls
WASD · mouse look · **Shift** sprint · Space jump · Esc unlock cursor  
**M** map/FT · **J** journal · **I** inventory · **T** wait · **E** talk  
**LMB/1** melee · **2** flare · **Q** potion · **F5** save · **F9** load

## Future plan (Skyrim-like features)
See **[Docs/SKYRIM_FEATURES_ROADMAP.md](Docs/SKYRIM_FEATURES_ROADMAP.md)** — P0+P1 vertical slice is in; P2 is next (interiors, audio, denser art).
Current hardening work and what's left: **[plan.md](plan.md)**.

## Rebuild
**Elder Scrolls 6 → Systems → Install P0+P1 + Rebuild World**  
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