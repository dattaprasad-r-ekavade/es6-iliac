# Kessil Bay — agent brief

**Read [`Docs/AGENT_HANDOFF.md`](Docs/AGENT_HANDOFF.md) before doing anything else.** It is
the project's memory across sessions: authority map, verification commands, invariants, known
traps, and the ordered work packets. This file is only the pointer plus the things that cause
damage if you don't know them.

## What this is

A first-person fantasy RPG. Unity 6000.5.3f1, URP 17.5, Windows. Solo developer, ~8–10 h/week,
1+ year horizon. Original setting — no third-party game's names or assets in the deliverable.

Current deliverable: **Chapter 01 as an internal proof of concept**, not a product. The
eventual product is 8 chapters, paid.

Design north star is Morrowind, for flow as well as look: reading-driven quests, directions
over markers, topic dialogue, in-fiction travel.

## Verify before claiming anything works

Unity must be **closed** for headless runs. Exit code 0 = pass; parse the results XML rather
than trusting console output.

```bash
python Tools/compile-check.py    # fast, run constantly

"/c/Program Files/Unity/Hub/Editor/6000.5.3f1/Editor/Unity.exe" -batchmode \
  -projectPath "D:/Projects/Elder Scrolls 6" -runTests -testPlatform EditMode \
  -testResults "<scratch>/em.xml" -logFile "<scratch>/em.log"     # 61/61

"/c/Program Files/Unity/Hub/Editor/6000.5.3f1/Editor/Unity.exe" -batchmode \
  -projectPath "D:/Projects/Elder Scrolls 6" -runTests -testPlatform PlayMode \
  -testResults "<scratch>/pm.xml" -logFile "<scratch>/pm.log"     # 101/101
```

**Done means:** compile-check clean, EditMode green, PlayMode green. Never report success
without the command output. `compile-check.py` cannot see a brand-new `.asmdef` until Unity
refreshes — a clean result does not prove new test files compile.

## Things that cause damage

- **Never revert `McpBootstrap`'s batch-mode early return.** Without it the MCP package logs
  connection errors headlessly, and Unity fails whichever test fixture is active. The suite
  becomes non-deterministic.
- **`SaveLoadService.SaveFilePath` is a static at `persistentDataPath`.** Tests that save will
  overwrite the developer's real save unless they back it up, as `SmokeTestFixture` does.
- **`Assets/Scenes/Main.unity` is destructively regenerated** by editor tools. Never
  hand-author anything into it you cannot regenerate.
- **Save-persisted ids must never embed display names** (`city_west`, not `Caldemar`). Enforced
  by `WorldLayoutTests`.
- **Chapter 01 must never hint that the Everspire is an alarm** — that is the Chapter 06
  reveal.

## Working style

- Update the docs in the same commit as the change they describe. A stale
  `AGENT_HANDOFF.md` is worse than none.
- Commit messages explain *why*, not just what.
- The codebase is AI-generated and AI-audited by design. Human comprehension is not the
  maintenance model — the tests and the handoff doc are. Do not propose "someone should
  understand this" as a remedy.
