# Modularisation plan

**The goal this serves:** a client whose reusable half can be lifted into another project. Not
"fewer lines in one file" — that is a proxy. The test is whether `Engine/`, `Render/` and the
input plumbing can be taken away and used somewhere that has never heard of jiva stones.

---

## Before anything: the toolchain

This machine no longer has .NET 9. `dotnet --list-sdks` shows 8.0.424 and 10.0.400; every
project targets `net9.0`, and `global.json` pins `9.0.302` with `rollForward: latestFeature`.

- The solution **builds** clean on SDK 10 (0 warnings) if `rollForward` is relaxed to
  `latestMajor`.
- The 690 domain tests **cannot run** without the .NET 9 *runtime*. No SDK setting substitutes
  for it; a `net9.0` assembly needs a `Microsoft.NETCore.App 9.0.x` to launch.

Install the .NET 9 runtime (or SDK) before starting any stage below. Do not retarget to
`net10.0` to dodge this: MonoGame 3.8.5.1 on .NET 10 is unproven for this project, and a
refactor is the wrong change to bundle with a runtime jump.

## How to verify anything in this document

```powershell
.\verify.ps1                                       # build, doctor, 690 domain tests, content, sim
.\publish.ps1                                      # the above plus the packaged self-test
build\RatnaBay.exe --yard --script Docs\scripts\smoke.rbs   # a scripted playthrough; exits 1 on failure
```

Run `verify.ps1` **without piping it**. In a shell pipeline the exit status you read is the last
command's, not PowerShell's, so a failed gate looks green. `-File` propagates a throw as exit 1
correctly on its own.

A stage is done when all three are green **and** the stage's own check passes. Every stage below
names its own check, because "it still builds" is not evidence that a refactor preserved
behaviour — a scripted run through the yard, the shaft, a descent and a fight is.

---

## Ground truth, measured 2026-08-31 at `ac93a24`

| | |
|---|---:|
| `Game1.cs` | 4,470 lines · 111 field declarations · 128 block-bodied methods |
| Longest method in `Game1` | `UpdateGameScreen`, 219 lines |
| Client files | 69 |
| Namespaces across them | **1** (`RatnaBay.Client`) |
| Code references to `Game1` outside itself | **1** (`Program.cs:27`) |
| Files deriving from MonoGame's `Game` | **1** (`Game1` itself) |
| Build warnings | 0 |

Track the first row and the namespace count. They are the two numbers this plan moves.

`Game1.cs` by commit, for trend rather than a single reading:

| Commit | Lines |
|---|---:|
| `18780ff` before the decomposition began | 6,613 |
| `2374624` model cache out | 6,497 |
| `50afe00` scene renderer out | 6,060 |
| `2b5eb9c` canvas owns its primitives | 5,816 |
| `5265825` 2D overlays out | 5,666 |
| `5c1aa60` first-person view and world presenter out | 5,472 |
| `ae5fb3e` figures and spike scenes out | 4,853 |
| `66a1ac3` capture host, cover, face sheet out | 4,553 |
| `c835269` overlay-stack input out | 4,470 |

---

## Stage 1 — A namespace per folder

**Why this is the first real blocker.** All 69 client files share `RatnaBay.Client`. The folders
imply modules; the namespaces do not. Two consequences:

- `Engine/` cannot move to another assembly without touching every file that consumes it.
- Nothing **enforces** the boundary. `Engine/` is clean today by discipline alone. One `using`
  and one `GameSession` reference would break it and the compiler would say nothing.

**Do.** One namespace per folder, in this shape:

| Folder | Namespace | Domain-free today |
|---|---|---:|
| `Engine/` | `RatnaBay.Engine` | **2 of 2** |
| `Audio/` | `RatnaBay.Engine.Audio` | **3 of 3** |
| `Input/` | `RatnaBay.Engine.Input` | **3 of 3** |
| `Render/` | `RatnaBay.Engine.Render` + `RatnaBay.Client.Render` | 8 of 16 |
| `Ui/` | `RatnaBay.Engine.Ui` + `RatnaBay.Client.Ui` | 10 of 23 |
| `Session/` | `RatnaBay.Client.Session` | 2 of 6 |
| `World/` | `RatnaBay.Client.World` | 1 of 9 |

The split inside `Render/` and `Ui/` is the interesting part: those two folders each hold a
genuinely reusable half and a game-specific half, and the namespace is where that stops being a
matter of opinion. `Engine/`, `Audio/` and `Input/` are already wholly clean and move intact.
Appendix A lists which file is which, measured rather than guessed.

**Do it as a pure rename.** No behaviour change, no file moves beyond what the namespace
demands, nothing else in the commit. The diff will be large and boring, which is the point: a
reviewer should be able to skim it.

**Check.** `.\verify.ps1` and `.\publish.ps1` green, `smoke.rbs` passes, and the diff contains
no line that is not a namespace, a using, or a brace.

**Risk.** Mechanical but wide. Two traps, both checked:
- `RatnaBay.Domain` really does declare `namespace RatnaBay.Domain`. Do not introduce a
  `RatnaBay.Engine.Domain` or the resolution rules get confusing for no gain.
- Reflective type names: `AmbientAudio`, `SoundBank`, `ModelCache` and `SceneRenderer` each call
  `exception.GetType().Name` to report *why* something failed to load. They name exception
  types, not ours, so a namespace move cannot touch them. Nothing else keys on a type name —
  no `Type.GetType`, no string-keyed factory.

---

## Stage 2 — Split `RatnaBay.Engine` into its own project

**Why.** This is what "use the rest of it in different projects" actually asks for. After
Stage 1 the boundary is documented; after Stage 2 it is enforced by the compiler, exactly as
`RatnaBay.Domain` already enforces "no MonoGame in the game rules".

**Do.**
1. New `src/RatnaBay.Engine/RatnaBay.Engine.csproj`, `net9.0-windows`, referencing MonoGame but
   **not** `RatnaBay.Domain`.
2. Move the files Appendix A marks as engine — after reading each one for the caveat below.
3. `RatnaBay.Game` references `RatnaBay.Engine`. The reference must not go the other way — that
   is the whole value.
4. `AGENTS.md` gains a row: rules in `Domain`, reusable presentation in `Engine`, this game in
   `Game`.

**Check.** Add `[OK] no domain reference in the engine` to `RatnaBay.Tools doctor`, so the
boundary is asserted by the gate rather than by memory. The gate has caught several "switch that
reports success and does nothing" bugs; a boundary held only by good intentions is the same
category.

**Risk — read before starting.** Real, and worth planning for:
- The MonoGame content pipeline (`Content.mgcb`) and the bundled fonts live in
  `RatnaBay.Game/Content`, and `publish.ps1` verifies they survive single-file publish. Confirm
  the published folder still contains everything after the split — that check has caught the
  fonts being swallowed once already.
- `--selftest` and `--dump-sfx` are entry points in `Program.cs` that reach into both halves.
- Do Stage 2 on its own branch. It is the only stage here that can leave the build unbuildable
  halfway.

---

## Stage 3 — Keep shrinking `Game1` (ongoing, not a sprint)

4,470 lines and 128 methods. Extract when a change needs to touch something, not speculatively —
the roadmap already says this and it has served well. In rough order of payoff:

| What | Where it goes | Size |
|---|---|---:|
| `UpdateGameScreen` — the screen-state switch | its own screen-stack type | 219 lines |
| `UpdateCombat` | `Combat/`, which does not exist yet | 130 lines |
| Run lifecycle — `EnterMine`, `EnterWorld`, `StartRun`, `EndRun`, `SuspendDescent`, `ResumeSuspendedDescent`, `AbandonDescent`, `ReturnToTheSurface` | a `Session/` director | one coherent job |
| Content loaders — `LoadWorldManifest`, `LoadShop`, `LoadDialogueManifest`, `LoadQuestManifest`, `LoadWatchers`, `LoadPickups` | a `Session/ContentLoader` | one coherent job |
| `BuildPromptState` (77) and `BuildWorldHudState` (62) | beside the states they build, in `Ui/` | 139 lines |
| `LoadContent` (108) and `Update` (84) | mostly host work | 192 lines |

All fourteen lifecycle and loader methods above were confirmed present in `Game1.cs`.

**Check for each.** `.\verify.ps1`, plus `smoke.rbs` — the script walks the yard, the shaft, a
descent, a fight and back up, which is the cheapest proof that a lifecycle extraction did not
quietly break the loop.

---

## Appendix A — Reuse inventory, measured

Files with no `using RatnaBay.Domain`, and therefore candidates for the engine assembly.

**`Engine/` — 2 of 2.** `CaptureHost`, `FirstPersonView`.

**`Audio/` — 3 of 3.** `AmbientAudio`, `SoundBank`, `SoundForge`. Synthesis is arithmetic; it
knows nothing about this game.

**`Input/` — 3 of 3.** `InputRouter`, `ListPicker`, `OverlayInput`.

**`Render/` — 8 of 16.** `BillboardRenderer`, `CharacterSprites`, `FaceField`, `ItemSprites`,
`ModelCache`, `PropTextures`, `SceneRenderer`, `SpriteForge`.
Not engine: `CombatFeedback`, `FaceSheet`, `PortraitForge`, `Portraits`, `StambhaCarving`,
`StoneTextures`, `WeaponSprites`, `WeaponView`.

**`Ui/` — 10 of 23.** `ConsentRenderer`, `MenuRenderer`, `OverlayRenderer`, `OverlayState`,
`PromptRenderer`, `PromptState`, `UiCanvas`, `UiScreens`, `UiTheme`, `WorldProjector`.

**`Session/` — 2 of 6.** `Telemetry` genuinely travels. **`Coach` does not** — see the caveat.

**`World/` — 1 of 9**, and that one is a false positive. See the caveat.

That is roughly 26 files carrying no *typed* knowledge of Ratna Bay: a first-person camera, a
sprite forge, a procedural texture set, a UI canvas with themed primitives, an audio
synthesiser, deterministic screenshot capture, and input routing with an overlay stack. It is a
small engine, and it is already written.

**The caveat, with names.** "No `using RatnaBay.Domain`" is necessary, not sufficient. Two files
pass the mechanical test and are still game-specific, and both say so in their own summaries:

- `World/SpikeScenes` — "Game-specific lighting studies, not engine… A second game does not take
  these." It is the moodboard and the trailer shot.
- `Session/Coach` — the first-time lines a player is shown. Domain-free by types, entirely about
  jiva stones and the shaft by content.

Read each file's summary before moving it. Conversely, one caveat the inventory used to carry is
already resolved: `UiCanvas` takes `logicalWidth`/`logicalHeight` as constructor parameters
rather than hard-coding 1280×720, so it travels as-is. `UiLayout` holds this game's constants
and stays.

---

## Appendix B — Deliberately not in this plan

- **Rewriting `Game1` in one pass.** The roadmap's rule is to extract when a change needs to
  touch something. Every stage above is small enough to abandon halfway.
- **Splitting `Ui/` renderers further.** They are 23 files averaging 141 lines. That is fine.
- **Making `Domain` reusable.** It already is — engine-free, headless, 690 tests.
- **Retargeting to `net10.0`.** See the toolchain note. It is a separate decision from this
  plan and should not ride along with it.
- **The `--show fort` gap and the silent swing cooldown.** Both real, both gameplay rather than
  structure. They belong on the production board, not here.

---

## What this file replaced

The previous revision opened with a "Ground truth, measured 2026-08-28" table whose every row
was wrong — `Game1.cs` at 3,484 lines against an actual 4,470, 95 fields against 111, 98 methods
against 128, `UpdateGameScreen` at 178 lines against 219, 74 client files against 69. The figure
3,484 matches no commit in this repository's history.

Its first three stages were built on files that have never existed here: `EngineHost`,
`LaunchOptions`, `CombatDirector`, `SessionDirector`, `ConsoleHost`, `FramePresenter`,
`ScreenStack`, `ConsoleInput`, and a `Combat/` folder — some cited with line numbers. Stage 0
asked for a commit of a clean tree, Stage 1 for two deletions of absent code, and Stage 2 for
the retirement of eight forwarding aliases that do not appear in `Game1.cs`.

The surviving stages are the last three, renumbered, with the inventory re-measured. Recording
this so the next reader does not go looking for the deleted stages, and because a plan is only
worth as much as its ground truth.
