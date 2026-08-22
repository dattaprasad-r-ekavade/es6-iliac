# Daggerfall reference study plan

**Date:** 2026-08-22  
**Purpose:** learn from Daggerfall’s data, presentation, and system design without using
its assets or shipping copied content

## Does this make sense?

Yes. Studying the original Daggerfall files can help us understand how a large retro
first-person RPG was represented compactly: map blocks, location records, sprites,
materials, text, quests, dungeon modules, and world coordinates.

It is especially useful for Ratna Bay because our target also values:

- first-person low-detail 3D;
- large authored/procedural spaces;
- billboard characters;
- modular interiors and dungeons;
- data-driven quests and dialogue;
- a dense information-heavy UI;
- deterministic saves and world state.

Daggerfall Unity is the most useful public reference because it documents and implements
systems such as asset import, billboards, procedural terrain and locations, world streaming,
interior/exterior transitions, action objects, UI, JSON, serialization, character creation,
inventory, automaps, and quest systems. See the [Daggerfall Unity roadmap](https://www.dfworkshop.net/projects/daggerfall-unity/roadmap/)
and the [open-source repository](https://github.com/Interkarma/daggerfall-unity).

The original game files should be treated as a private, read-only reference dataset. They
are not a replacement for an original Ratna Bay content pipeline.

## Boundaries

This study is for technical and design learning. It is not a plan to copy Daggerfall.

Rules:

1. Do not place original Daggerfall textures, models, sprites, sounds, videos, fonts, or
   executable files in this repository.
2. Do not copy original dialogue, quests, names, lore, map labels, or UI text into Ratna Bay.
3. Do not copy Daggerfall Unity implementation files into the game. Use public source as an
   architectural reference and write Ratna Bay-specific equivalents.
4. Keep any legally obtained original game files outside the repository and outside release
   artifacts.
5. Store only neutral notes, schemas, measurements, algorithms, and Ratna Bay-owned test
   fixtures in this repository.
6. Keep a source and license note for every external tool or reference used.

This is a technical workflow boundary, not legal advice. Distribution rights vary by
country and by the particular file or code being studied. If the project later ships
tools that read original Daggerfall files, review that distribution decision separately.

## Reference workspace

The original files are not currently present in this workspace. When available, point the
tools at a separately installed, legally obtained copy, for example:

```text
D:\ReferenceGames\Daggerfall\
```

That path must be supplied through a local configuration file or command-line option and
must never be hard-coded into the repository. The analyzer should fail clearly when the
reference path is absent.

Suggested local-only configuration:

```powershell
$env:RATNABAY_DAGGERFALL_REFERENCE = 'D:\ReferenceGames\Daggerfall'
```

The build and CI pipeline must not require this path. Normal game builds must work without
the reference files.

## What to study

### 1. World representation

Document:

- how exterior maps are divided into regions or tiles;
- how locations are identified and connected to exterior maps;
- coordinate systems, scale, elevation, and climate/terrain categories;
- which information is authored and which is generated;
- what can be streamed and what must remain persistent;
- how a location is found from a world position.

Ratna Bay output: a neutral `WorldManifest` and region/chunk schema. Do not reproduce
Daggerfall’s file layout; use the lessons to design our own versioned JSON format.

Daggerfall Workshop’s streaming-world research is particularly relevant because it describes
turning map data into streaming terrain samples and handling the transition between wilderness
and location tiles through a custom terrain system. [Streaming World – Part 2](https://www.dfworkshop.net/streaming-world-part-2/)

### 2. Modular locations and dungeons

Study:

- block/module dimensions;
- connector and doorway conventions;
- room adjacency and graph construction;
- special action objects such as lifts, levers, doors, and teleporters;
- how quest targets are injected into generated locations;
- how automaps can be derived from the same topology.

Ratna Bay output: our own modular room format with explicit connectors, reachability tests,
story markers, and collision generation from the same source graph.

### 3. Rendering constraints

Measure rather than copy:

- sprite and billboard dimensions;
- texture atlas conventions;
- palette and shading limitations;
- visibility and draw-distance strategies;
- how geometry, sprites, effects, and UI are ordered;
- which visual details are repeated through data.

Ratna Bay output: a renderer target document and Blender export rules for low-poly meshes,
billboard sheets, material IDs, palette groups, and collision proxy meshes.

### 4. Character and sprite presentation

Study the separation between actor identity, visual appearance, equipment, and facing. The
important lesson is that a Daggerfall-style character can be represented as data plus a small
visual set rather than requiring a fully animated modern humanoid rig.

Ratna Bay output:

- actor archetype schema;
- billboard rotation policy;
- sprite-sheet naming convention;
- deterministic variation seed;
- equipment-to-visual mapping;
- camera-facing and occlusion tests.

### 5. UI and information architecture

Study the relationships between:

- HUD and character state;
- character sheet and skills;
- inventory, equipment, and encumbrance;
- map, travel, and location discovery;
- journal, quest state, and time limits;
- dialogue options and faction/skill conditions;
- settings, keybinds, save/load, and modal input.

Ratna Bay output: view-model contracts and screen-flow tests. We should copy the principle of
information density, not the artwork, text, layout, or typography.

### 6. Simulation and persistence

Study which state is global, regional, location-local, actor-local, or quest-instance-local.
Pay special attention to:

- unique quest-instance identities;
- time and travel progression;
- actor and item persistence;
- generated locations that must remain stable after saving;
- migration/versioning of saved state.

Daggerfall Unity’s quest source is useful here because it distinguishes reusable quest
definitions from live quest instances with unique identities. [Quest source](https://github.com/Interkarma/daggerfall-unity/blob/master/Assets/Scripts/Game/Questing/Quest.cs/)

Ratna Bay output: an engine-free save schema and mutation ledger keyed only by stable Ratna
Bay IDs.

## Analysis workflow

The study should proceed in four passes.

### Pass A — inventory, read-only

Build a local analyzer that reports file names, sizes, headers, dimensions, counts, and
relationships. It must not modify the reference files.

Deliverable: `reference inventory.json` stored outside Git or ignored locally.

### Pass B — format notes

For each format, record:

- purpose;
- byte layout or logical fields;
- endianness and encoding;
- references to other records;
- coordinate conventions;
- unknown fields;
- confidence level;
- a Ratna Bay equivalent or explicit decision not to reproduce it.

Deliverable: human-readable notes under a local reference-notes directory. Only sanitized,
general technical notes belong in Git.

### Pass C — neutral test fixtures

Create tiny Ratna Bay-owned fixtures that exercise the same class of problem:

- one exterior map;
- one location record;
- one modular dungeon graph;
- one billboard actor;
- one quest instance;
- one save snapshot.

Deliverable: tests that validate our own formats without loading Daggerfall files.

### Pass D — implementation comparison

For every proposed feature, compare:

```text
Daggerfall lesson → Ratna Bay requirement → own data format → own runtime implementation → test
```

No feature enters the game merely because the original game contained it. It enters because
it supports the Ratna Bay design and passes a current slice gate.

## Pipeline integration

The reference study is a separate optional pipeline stage:

```text
Optional external reference path
        ↓
Read-only reference analyzer
        ↓
Sanitized notes / research outputs
        ↓
Ratna Bay-owned schema design
        ↓
Ratna Bay fixtures and tests
        ↓
Normal game build
```

The normal build remains independent:

```text
Source JSON/assets
        ↓
RatnaBay.Tools validate
        ↓
RatnaBay.Tools compile
        ↓
MGCB content build
        ↓
MonoGame build
        ↓
Domain tests + runtime capture tests
```

The Daggerfall reference must never become a hidden build dependency.

## First reference-study deliverables

1. `reference inspect` command with an external input path.
2. Inventory report that contains metadata only.
3. Format-notes template with confidence and licensing fields.
4. Ratna Bay-owned mini fixtures for one map, one dungeon graph, one actor, and one quest.
5. A renderer note comparing billboard, mesh, and hybrid actor strategies.
6. A UI note mapping Daggerfall information screens to Ratna Bay view models.
7. A decision record for each feature we adopt, reject, or postpone.

## References

- [Daggerfall Unity roadmap](https://www.dfworkshop.net/projects/daggerfall-unity/roadmap/)
- [Daggerfall Unity source repository](https://github.com/Interkarma/daggerfall-unity)
- [Daggerfall Unity quest source](https://github.com/Interkarma/daggerfall-unity/blob/master/Assets/Scripts/Game/Questing/Quest.cs/)
- [Daggerfall Workshop streaming-world research](https://www.dfworkshop.net/streaming-world-part-2/)
- [MonoGame content pipeline](https://docs.monogame.net/articles/getting_to_know/whatis/content_pipeline/CP_Overview.html)
