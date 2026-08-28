# Modularisation plan

Clearing the findings from the review of the `Game1` decomposition, in the order that makes
each step safe.

**The goal this serves:** a client whose reusable half can be lifted into another project. Not
"fewer lines in one file" — that is a proxy. The test is whether `Engine/`, `Render/` and the
input plumbing can be taken away and used somewhere that has never heard of jiva stones.

---

## How to verify anything in this document

```powershell
.\verify.ps1                                       # build, doctor, 690 domain tests, content, sim
.\publish.ps1                                      # the above plus the packaged self-test (140 checks)
build\RatnaBay.exe --yard --script Docs\scripts\smoke.rbs   # a scripted playthrough; exits 1 on failure
```

A stage is done when all three are green **and** the stage's own check passes. Every stage below
names its own check, because "it still builds" is not evidence that a refactor preserved
behaviour — a scripted run through the yard, the shaft, a descent and a fight is.

---

## Ground truth, measured 2026-08-28

| | |
|---|---:|
| `Game1.cs` | 3,484 lines · 95 fields · 98 block-bodied methods |
| Longest method in `Game1` | `UpdateGameScreen`, 178 lines |
| Client files | 74 |
| Namespaces across them | **1** (`RatnaBay.Client`) |
| Code references to `Game1` outside itself | **1** (`Program.cs`) |
| Files touching MonoGame's `Game` base | **1** (`EngineHost`) |
| Build warnings | 0 |

Track the first row and the namespace count. They are the two numbers this plan moves.

---

## Stage 0 — Commit what is already done

**Why.** Seventeen modified files and ten new ones are uncommitted, including `Engine/EngineHost.cs`,
`Engine/LaunchOptions.cs`, `Combat/CombatDirector.cs`, `Session/SessionDirector.cs`,
`Session/ConsoleHost.cs`, `Ui/FramePresenter.cs` and four new `Input/` files. It all builds and
passes. Nothing below should start on top of an uncommitted base — if a later stage goes wrong
there is no clean point to return to.

**Do.** Commit as one change with a message saying what moved and what is now where.

**Check.** `git status --porcelain` is empty. `.\publish.ps1` green.

**Risk.** None. This is the step that removes risk.

---

## Stage 1 — Two deletions

Small, independent, and they stop the next reader inheriting a wrong impression.

### 1a. `ConsoleHost.LoadScript` is dead

[`src/RatnaBay.Game/Session/ConsoleHost.cs:34`](src/RatnaBay.Game/Session/ConsoleHost.cs) — defined,
called from nowhere. `LaunchOptions` took over `--script` parsing during the extraction and this
was left behind. Its `ref string? missing, ref string? exec` signature was shaped for a call site
that no longer exists, so leaving it invites someone to reuse an awkward shape for no reason.

**Do.** Delete the method.

**Check.** `grep -rn "LoadScript" src --include=*.cs` returns nothing.
`build\RatnaBay.exe --yard --script Docs\scripts\smoke.rbs` still passes — that is the path it
was pretending to serve.

### 1b. Unused `using RatnaBay.Domain;` in `LaunchOptions.cs`

No domain type is referenced in the file. It matters more than a usual stray using **because
that file lives in `Engine/`**: it is the single line making an otherwise game-agnostic folder
look as though it depends on the game. Stage 4 turns that impression into a compile error, so
clear it first.

**Do.** Remove the using.

**Check.** `Engine/` has no `using RatnaBay.Domain` in any file.

---

## Stage 2 — Retire the forwarding aliases in `Game1`

**Why.** Eight members in `Game1` exist only to let old code keep its old names after the state
moved to `ConsoleHost`:

```csharp
private ConsoleRouter? _console      { get => _scripts.Router;    set => _scripts.Router = value; }
private List<ConsoleLine> _consoleOutput => _scripts.Output;
private Queue<string> _scriptQueue       => _scripts.Queue;
private float _scriptWaitSeconds     { get => _scripts.WaitSeconds;   set => ... }
private bool  _scriptFailed          { get => _scripts.Failed;        set => ... }
private bool  _scriptQuitWhenDone    { get => _scripts.QuitWhenDone;  set => ... }
private List<string> _watches            => _scripts.Watches;
private List<string> _watchOutput        => _scripts.WatchOutput;
```

They are not a bug. They are worse than a bug in one specific way: `Game1` reads as though it
still owns console state it has handed away, so the next person to touch it will reason about
the wrong owner. This is the same shape as the `_stone` field, which is assigned by
`WorldPresenter` through a `ref` parameter — I mis-read that as a dead field during review and
had to check a screenshot to disprove myself.

**Do.**
1. Replace each use of an alias with `_scripts.X` directly.
2. Delete the aliases.
3. While there: `_stone` is passed as `ref` into `WorldPresenter.Draw`. Consider returning the
   palette instead, or moving the field onto the presenter that decides it. A `ref` field
   crossing a module boundary is an invisible out-parameter.

**Check.** `grep -n "_scripts\." src/RatnaBay.Game/Game1.cs` shows only real calls, no
property bodies. Field count drops by roughly eight. `.\verify.ps1` green.

**Risk.** Low and mechanical. Do it in one commit so the diff is obviously a rename.

---

## Stage 3 — A namespace per folder

**Why this is the real blocker.** All 74 client files share `RatnaBay.Client`. The folders imply
modules; the namespaces do not. Two consequences:

- `Engine/` cannot move to another assembly without touching every file that consumes it.
- Nothing **enforces** the boundary. `Engine/` is clean today by discipline alone. One `using`
  and one `GameSession` reference would break it and the compiler would say nothing.

**Do.** One namespace per folder, in this shape:

| Folder | Namespace | Domain-free today |
|---|---|---:|
| `Engine/` | `RatnaBay.Engine` | 3 of 4 (Stage 1b makes it 4 of 4) |
| `Audio/` | `RatnaBay.Engine.Audio` | **3 of 3** |
| `Render/` | `RatnaBay.Engine.Render` + `RatnaBay.Client.Render` | 9 of 16 |
| `Input/` | `RatnaBay.Engine.Input` + `RatnaBay.Client.Input` | 5 of 7 |
| `Ui/` | `RatnaBay.Engine.Ui` + `RatnaBay.Client.Ui` | 11 of 24 |
| `World/` | `RatnaBay.Client.World` | 1 of 9 |
| `Session/` | `RatnaBay.Client.Session` | 2 of 8 |
| `Combat/` | `RatnaBay.Client.Combat` | 0 of 1 |

The split inside `Render/`, `Input/` and `Ui/` is the interesting part: those folders each hold
a genuinely reusable half and a game-specific half, and the namespace is where that stops being
a matter of opinion. Appendix A lists which file is which, measured rather than guessed.

**Do it as a pure rename.** No behaviour change, no file moves beyond what the namespace
demands, nothing else in the commit. The diff will be large and boring, which is the point: a
reviewer should be able to skim it.

**Check.** `.\verify.ps1` and `.\publish.ps1` green, `smoke.rbs` passes, and the diff contains
no line that is not a namespace, a using, or a brace.

**Risk.** Mechanical but wide. Two traps, both checked:
- `RatnaBay.Domain` really does declare `namespace RatnaBay.Domain`. Do not introduce a
  `RatnaBay.Engine.Domain` or the resolution rules get confusing for no gain.
- Reflective type names: there are four, and all four are safe. `AmbientAudio:56`,
  `SoundBank:90`, `ModelCache:70` and `SceneRenderer:106` each call
  `exception.GetType().Name` to report *why* something failed to load. They name exception
  types, not our own, so a namespace move cannot touch them. Nothing else keys on a type name —
  no `Type.GetType`, no string-keyed factory.

---

## Stage 4 — Split `RatnaBay.Engine` into its own project

**Why.** This is what "use the rest of it in different projects" actually asks for. After
Stage 3 the boundary is documented; after Stage 4 it is enforced by the compiler, exactly as
`RatnaBay.Domain` already enforces "no MonoGame in the game rules".

**Do.**
1. New `src/RatnaBay.Engine/RatnaBay.Engine.csproj`, `net9.0-windows`, referencing MonoGame but
   **not** `RatnaBay.Domain`.
2. Move the files Appendix A marks as engine.
3. `RatnaBay.Game` references `RatnaBay.Engine`. The reference must not go the other way — that
   is the whole value.
4. `AGENTS.md` gains a row: rules in `Domain`, reusable presentation in `Engine`, this game in
   `Game`.

**Check.** Add `[OK] no domain reference in the engine` to `RatnaBay.Tools doctor`, so the
boundary is asserted by the gate rather than by memory. The gate has caught three "switch that
reports success and does nothing" bugs this month; a boundary held only by good intentions is
the same category.

**Risk — read before starting.** Real, and worth planning for:
- The MonoGame content pipeline (`Content.mgcb`) and the bundled fonts live in
  `RatnaBay.Game/Content`. `EngineHost` loads fonts from `AppContext.BaseDirectory`, so it is a
  runtime path rather than a build dependency — but confirm the published folder still contains
  everything `publish.ps1` verifies.
- `--selftest` and `--dump-sfx` are entry points in `Program.cs` that reach into both halves.
- Do Stage 4 in its own branch. It is the only stage in this plan that can leave the build
  unbuildable halfway.

---

## Stage 5 — Keep shrinking `Game1` (ongoing, not a sprint)

3,484 lines and 98 methods. Extract when a change needs to touch something, not speculatively —
the roadmap already says this and it has served well. In rough order of payoff:

| What | Where it goes | Size |
|---|---|---:|
| `UpdateGameScreen` — the screen-state switch | `Input/ScreenStack` already exists to receive it | 178 lines |
| Content loaders — `LoadWorldManifest`, `LoadShop`, `LoadDialogueManifest`, `LoadQuestManifest`, `LoadWatchers`, `LoadPickups` | a `Session/ContentLoader` | one coherent job |
| Run lifecycle — `EnterMine`, `EnterWorld`, `StartRun`, `EndRun`, `SuspendDescent`, `ResumeSuspendedDescent`, `AbandonDescent`, `ReturnToTheSurface` | `Session/SessionDirector`, which exists and is under-used | one coherent job |
| `BuildPromptState` (77) and `BuildWorldHudState` (62) | beside the states they build, in `Ui/` | 139 lines |
| `LoadContent` (95) and `Initialize` (58) | mostly `EngineHost` work already | 153 lines |

**Check for each.** `.\verify.ps1`, plus `smoke.rbs` — the script walks the yard, the shaft, a
descent, a fight and back up, which is the cheapest proof that a lifecycle extraction did not
quietly break the loop.

---

## Appendix A — Reuse inventory, measured

Files with no `using RatnaBay.Domain`, and therefore candidates for the engine assembly:

**`Audio/` — all three.** `AmbientAudio`, `SoundBank`, `SoundForge`. Synthesis is arithmetic; it
knows nothing about this game.

**`Render/` — 9 of 16.** `BillboardRenderer`, `CharacterSprites`, `FaceField`, `ItemSprites`,
`ModelCache`, `PropTextures`, `SceneRenderer`, `SpriteForge`, `StoneTextures`.

**`Input/` — 5 of 7.** `ConsoleInput`, `InputRouter`, `ListPicker`, `OverlayInput`, `ScreenStack`.

**`Ui/` — 11 of 24.** `ConsentRenderer`, `FramePresenter`, `MenuRenderer`, `OverlayRenderer`,
`OverlayState`, `PromptRenderer`, `PromptState`, `UiCanvas`, `UiScreens`, `UiTheme`,
`WorldProjector`.

**`Engine/` — 3 of 4**, and 4 of 4 after Stage 1b.

That is roughly 32 files carrying no knowledge of Ratna Bay: a first-person camera, a scripting
console, a sprite forge, a procedural texture set, a UI canvas with themed primitives, an audio
synthesiser, deterministic screenshot capture, and input routing with an overlay stack. It is a
small engine, and it is already written.

**Being honest about the caveats.** "No `using RatnaBay.Domain`" is necessary, not sufficient.
Before moving each file, check it for game assumptions that are not expressed as a type — a
hard-coded 1280×720 canvas, colours named for this game's fiction, `Surface.Spawn` reached
indirectly. Expect a handful to need a parameter before they travel.

---

## Appendix B — Deliberately not in this plan

- **Rewriting `Game1` in one pass.** The roadmap's rule is to extract when a change needs to
  touch something. Every stage above is small enough to abandon halfway.
- **Splitting `Ui/` renderers further.** They are 24 files averaging 127 lines. That is fine.
- **Making `Domain` reusable.** It already is — engine-free, headless, 690 tests.
- **The `--show fort` gap and the silent swing cooldown.** Both real, both gameplay rather than
  structure. They belong on the production board, not here.
