# Community Tools Baseline

This document records the community packages prepared for the MonoGame pivot. The
packages are pinned to exact versions so a clean checkout restores the same development
baseline.

## Installed in the game client

| Package | Version | Preparation purpose |
| --- | ---: | --- |
| `MonoGame.Framework.WindowsDX` | `3.8.5.1` | WindowsDX runtime |
| `MonoGame.Content.Builder.Task` | `3.8.5.1` | Content build integration |
| `MonoGame.Extended` | `6.1.1` | Selective MonoGame utilities |
| `MonoGame.Extended.Content.Pipeline` | `6.1.1` | Optional MGCB importers/processors |
| `Gum.MonoGame` | `2026.8.3.1` | Player UI layout and controls |
| `ImGui.NET` | `1.91.6.1` | Developer/debug tools |
| `FontStashSharp.MonoGame` | `1.5.6` | Bundled runtime font rendering with high-resolution glyph atlases |
| `BepuPhysics` | `2.4.0` | Future 3D collision and character physics |
| `DotRecast.Recast` | `2026.3.1` | Future navmesh generation |
| `DotRecast.Detour` | `2026.3.1` | Future runtime navigation |
| `Ink` | `0.14.0` | Future dialogue and narrative authoring |

## Installed in the tools project

| Package | Version | Preparation purpose |
| --- | ---: | --- |
| `SharpGLTF.Core` | `1.0.6` | Read and inspect glTF/GLB assets |
| `SharpGLTF.Toolkit` | `1.0.6` | Future conversion and generated-scene tools |

## Pipeline setup

The game project defines `MonoGameExtendedPipelineReferencePath`. A build copies the
pipeline extension assemblies into `src/RatnaBay.Game/pipeline-references/`, and
`Content/Content.mgcb` references the main extension assembly from that stable project
relative path. The generated directory is ignored by Git and is recreated by restore/build.

The current runtime asset path remains FBX through MGCB. glTF/GLB is prepared for future
tooling and conversion work; it is not yet a second runtime renderer.

The feasibility UI now uses a reproducible two-font runtime stack: Cinzel for fantasy
headings and Noto Sans for dense body/UI copy. Both TTFs are bundled under
`src/RatnaBay.Game/Content/Feasibility/Fonts/` with their SIL Open Font License files.
The font systems rasterize at 2× logical resolution before the 1280×720 UI canvas is
scaled to the display.

## Tool commands

Run the baseline check after a restore/build:

```powershell
dotnet run --project tools\RatnaBay.Tools -- doctor
```

Inspect a glTF or GLB candidate asset:

```powershell
dotnet run --project tools\RatnaBay.Tools -- asset-info path\to\asset.glb
```

## Intentionally deferred

- Steamworks.NET: add after save/load contracts and native release packaging are stable.
- Tiled: use as an external authoring tool; add a custom JSON-to-world-manifest importer.
- Khronos glTF Validator: use as an external CI/toolchain command rather than adding an
  unstable package reference.
- DefaultEcs: revisit after entity counts and streaming requirements justify ECS adoption.
- Myra: not installed because Gum is the current player-UI candidate; do not maintain two
  player UI frameworks.
- Procedural dungeons, mod support, and time-bound quests remain outside the current scope.

## Adoption rule

Adding a package does not mean its systems are active in gameplay. Each package must earn
integration through a small vertical spike, a clear owner boundary, and a passing release
build before it becomes part of the game architecture.
