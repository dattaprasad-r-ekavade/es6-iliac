# Ratna World Builder

A small external editor for `Assets/Resources/Data/World/kessil.world.json`. It runs on the
standard Python 3 included on this workstation and does not need Unity, Tiled, Pillow, or a
pip install.

## Start it

Double-click `Launch World Builder.cmd`, or run:

```powershell
python Tools/WorldBuilder/world_builder.py
```

Use the toolbar to place landmasses, cities, roads, POIs, city gates, and story spawns. A new
generated city needs a runtime city site plus matching **City stable ID** and **City display
name** on its landmass; validation blocks incomplete pairings. Select and
drag an item to move it; Shift-drag a selected landmass to resize its coastline. Select a
road and drag its circular handles to edit its polyline. Exact centre, base height,
elevation/relief, size, biome, marker, and spawn values are available in the Properties
panel. Ctrl+Z/Ctrl+Y undo and redo.

After a valid save, **Unity preview** is the one-button shipping check. It starts Unity hidden,
reimports this JSON, rebuilds `Main` through the production generator, and writes a top-down
and an approach view to `Docs/Screenshots/WorldBuilder/`. If Unity is installed elsewhere,
set `RATNA_UNITY_PATH` to the full path of `Unity.exe`.

`city` and `poi` entries remain normal Unity runtime `Sites`. `gate` and `story_spawn`
entries are stored under `_WorldBuilder.Markers`, so the current Unity Version 1 loader
ignores them safely. Stable road IDs and site kinds also live under `_WorldBuilder`.

Saving is blocked when a spawn is underwater/off land, an ID is missing or duplicated, a
road has fewer than two distinct points, a road endpoint is over water, or a coastline
crosses the map safety margin. Messages name the item and say what to move or repair. Every
overwrite first creates a timestamped copy in the ignored `WorldBuilderBackups/` folder, then
replaces the JSON atomically. Stable IDs are editable for initial authoring, but changing an ID
already used by a save, quest or story reference is a migration and must be reviewed in code.

> **Unity preview destructively regenerates `Assets/Scenes/Main.unity`.** Main is a checked-in
> build artifact, not a hand-authoring surface. Never place unique work in that scene.

## Headless checks and previews

```powershell
python Tools/WorldBuilder/world_builder.py --validate
python Tools/WorldBuilder/world_builder.py --preview Docs/Screenshots/world-builder-preview.png
python Tools/WorldBuilder/world_builder.py --preview Docs/Screenshots/world-builder-preview.svg
python -m unittest discover -s Tools/WorldBuilder/tests -v
```

`--validate` returns exit code 0 on success, 1 for an invalid map, and 2 when the input or
preview cannot be opened. Pass a different world JSON as the final argument. PNG rendering
is implemented with the standard library; SVG additionally contains readable labels.

## Current boundary

This MVP edits the runtime's existing elliptical landmass vocabulary. It does not sculpt a
free-form heightmap. Base Y plus elevation/relief control the current terrain generator;
PNG/SVG give instant external previews and the Unity button supplies the slower production
proof. Gates and story spawns remain metadata until a later Unity importer consumes them.
