# Modularisation plan

Clearing the findings from the review of the `Game1` decomposition, in the order that makes
each step safe.

**The goal this served:** a client whose reusable half can be lifted into another project. Not
"fewer lines in one file" — that is a proxy. The test is whether `Engine/`, `Render/` and the
input plumbing can be taken away and used somewhere that has never heard of jiva stones.

**Status: complete, 2026-08-28.** Stages 0–5 are done. The reusable half is `src/RatnaBay.Engine`,
the compiler and `doctor` enforce the Domain boundary, and `PLAN.md` is this record.

---

## How to verify anything in this document

```powershell
.\verify.ps1                                       # build, doctor, 690 domain tests, content, sim
.\publish.ps1                                      # the above plus the packaged self-test (140 checks)
build\RatnaBay.exe --yard --script Docs\scripts\smoke.rbs   # a scripted playthrough; exits 1 on failure
```

A stage is done when all three are green **and** the stage's own check passes.

Run `verify.ps1` **without piping it**. In a shell pipeline the exit status you read is the last
command's, not PowerShell's, so a failed gate looks green. `-File` propagates a throw as exit 1
correctly on its own.

---

## Ground truth

| | 2026-08-28 review | After this plan |
|---|---:|---:|
| `Game1.cs` | 3,484 lines · 95 fields · 98 block-bodied methods | 3,108 lines; aliases and loaders gone |
| Longest method in `Game1` | `UpdateGameScreen`, 178 lines | already on `ScreenStack` before this plan |
| Client files | 74 | Game + Engine, one namespace per folder |
| Namespaces across them | **1** (`RatnaBay.Client`) | **12** (`RatnaBay.Engine*`, `RatnaBay.Client*`) |
| Code references to `Game1` outside itself | **1** (`Program.cs`) | still 1 |
| Files touching MonoGame's `Game` base | **1** (`EngineHost`) | still 1, now in `RatnaBay.Engine` |
| Build warnings | 0 | 0 |

---

### How `Game1` got here, commit by commit

Kept because the shape of it is the argument: no single cut was large, and the file more than
halved anyway.

| Commit | Lines |
|
| after the merge with the fort, bosses and clips | **3,216** |

---

## Stage 0 — Commit what is already done — done

Seventeen modified files and ten new ones were committed as `1813f12` (*Lift the remaining
Game1 coordinators into named types*). Porcelain was empty; `.\publish.ps1` was green.

---

## Stage 1 — Two deletions — done

### 1a. `ConsoleHost.LoadScript` is dead

Deleted. `LaunchOptions` owns `--script`. `grep` finds no `LoadScript` under `src`.

### 1b. `using RatnaBay.Domain;` in `LaunchOptions.cs`

The using was not unused: the file called `ConsoleRouter.ReadScript`. The Domain-free fix is
the one that matters for Stage 4 — script lines are trimmed in place, and `LaunchOptions`
moved to `Session/` so `Engine/` is not a game launcher. `src/RatnaBay.Engine` has no
`using RatnaBay.Domain`.

---

## Stage 2 — Retire the forwarding aliases in `Game1` — done

The eight `_scripts.X` forwarding properties are gone. Uses call `_scripts` directly. The
stone palette lives on `WorldPresenter.Stone` instead of a `ref` out-parameter from `Game1`.

---

## Stage 3 — A namespace per folder — done

| Folder | Namespace |
|---|---|
| `src/RatnaBay.Engine/Engine/` | `RatnaBay.Engine` |
| `src/RatnaBay.Engine/Audio/` | `RatnaBay.Engine.Audio` |
| `src/RatnaBay.Engine/Render/` | `RatnaBay.Engine.Render` |
| `src/RatnaBay.Engine/Input/` | `RatnaBay.Engine.Input` |
| `src/RatnaBay.Engine/Ui/` | `RatnaBay.Engine.Ui` |
| `src/RatnaBay.Game/Render/` | `RatnaBay.Client.Render` |
| `src/RatnaBay.Game/Input/` | `RatnaBay.Client.Input` |
| `src/RatnaBay.Game/Ui/` | `RatnaBay.Client.Ui` |
| `src/RatnaBay.Game/World/` | `RatnaBay.Client.World` |
| `src/RatnaBay.Game/Session/` | `RatnaBay.Client.Session` |
| `src/RatnaBay.Game/Combat/` | `RatnaBay.Client.Combat` |

`Game1`, `Program` and `ParkedFeatures` stay `RatnaBay.Client`. Global usings keep call sites
from spelling every folder.

---

## Stage 4 — Split `RatnaBay.Engine` into its own project — done

1. `src/RatnaBay.Engine/RatnaBay.Engine.csproj` — `net9.0-windows`, MonoGame, not Domain.
2. Appendix A engine files moved there. `LaunchOptions` stayed this game (`Session/`).
   `UiScreens` stayed this game (it constructs Domain renderers). `UiLayout` travelled with
   `OverlayInput` after dropping the `MineEntry` type (depth rows use `DepthMinTier = 1`).
3. `RatnaBay.Game` references `RatnaBay.Engine`. The reverse does not exist.
4. `AGENTS.md` has the Domain / Engine / Game rows.
5. `doctor` prints `[OK] no domain reference in the engine`.

`FramePresenter` no longer takes `GameScreen`; it takes a `bool worldScene` so the engine
does not own this game's screen enum.

---

## Stage 5 — Keep shrinking `Game1` — done for the listed cuts

| What | Where it went |
|---|---|
| `UpdateGameScreen` | already `ScreenStack` (Stage 0) |
| Content loaders | `Session/ContentLoader` |
| Run lifecycle (`EnterMine`, `SuspendDescent`, `ResumeSuspendedDescent`, `AbandonDescent`, plus the earlier enter/start/end) | `Session/SessionDirector` |
| `BuildPromptState` / `BuildWorldHudState` | `Ui/PromptBuilder`, `Ui/WorldHudBuilder` |
| `LoadContent` / `Initialize` | devices, fonts and canvas already on `EngineHost`; remaining lines are this game's `--faces`, models, consent and session bootstrap |

Extract further when a change needs to touch something. Do not rewrite `Game1` in one pass —
that is still Appendix B.

---

## Appendix A — Reuse inventory, as moved

Now in `src/RatnaBay.Engine`:

**`Audio/` — all three.** `AmbientAudio`, `SoundBank`, `SoundForge`.

**`Render/` — 9 of 16.** `BillboardRenderer`, `CharacterSprites`, `FaceField`, `ItemSprites`,
`ModelCache`, `PropTextures`, `SceneRenderer`, `SpriteForge`, `StoneTextures`.

**`Input/` — 5 of 7.** `ConsoleInput`, `InputRouter`, `ListPicker`, `OverlayInput`, `ScreenStack`.

**`Ui/` — canvas, theme, layout, overlay stack, prompt chips, frame presenter.** `UiScreens`
stayed in the game because it constructs Domain-aware renderers.

**`Engine/` — host, first-person view, capture.** `LaunchOptions` is this game.

---

## Appendix B — Deliberately not in this plan

- **Rewriting `Game1` in one pass.** The remaining coordinator (commands, `IConsoleTarget`,
  `--show`) is extracted when a change needs it.
- **Splitting `Ui/` renderers further.** The game-side files are still a fine size.
- **Making `Domain` reusable.** It already is — engine-free, headless, 690 tests.
- **The `--show fort` gap and the silent swing cooldown.** Gameplay, not structure.
