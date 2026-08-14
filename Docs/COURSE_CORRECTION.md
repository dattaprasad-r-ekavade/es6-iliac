# Course correction — plan of action

**Written:** 2026-08-15 · **Supersedes the packet order in** [`AGENT_HANDOFF.md`](AGENT_HANDOFF.md) §7

Read this before picking any work. The packet list in the handoff is still accurate about what
was *built*; this file is what to build *next* and in what order.

---

## The diagnosis

**We optimised the work that could be verified without running the game, and the game was never
run.**

Every defect that made this unplayable was found by the developer pressing Play:

| Found by playtest | Found by 255 passing tests |
|---|---|
| Drowning on entering any interior | Contour blending flaw |
| A 4 m quay trench at the spawn | Tiling seam at the texture wrap |
| No way to quit without Alt+F4 | Coast model / mesh mismatch |
| A cutscene written for a different game | Three POIs buried under their terrain |
| 250 m of nothing between spawn and the only authored street | |
| No items, no characters, ovals on the map | |

The tests catch *internal consistency*. They cannot discover that something never worked,
because they were written by the same process that wrote the code and inherit its blind spots.
`PlayerSafetyGuardTests` did not exist until after the developer drowned.

---

## Decisions made — do not reopen

- **Unity stays.** Paid Windows/Steam product. A web stack iterates faster and ships worse.
  The iteration cost here is self-inflicted — destructive scene regeneration, 10 prefabs across
  19 scenes, no play harness — not the engine's.
- **Arena Miniature** remains the art direction (locked 2026-08-12, `plan.md`).
- **Indic setting** remains (adopted 2026-08-12, `STORY_ARC_INDIC.md`).

---

## Phase 0 · Sight — **do this first**

Nothing else is worth starting until the game can be observed without the developer acting as
the renderer.

- Headless playthrough harness: boot, walk the chapter route, capture a frame per beat.
  `WorldBuilderPreviewCommand` already proves the capture path works.
- Six playability assertions, all measured **from the player's position**:
  1. after entering every interior, the player is still inside it 5 s later
  2. in every scene the player is never below the floor
  3. every interior has an exit that returns to the region
  4. a talker, an item and a door are each reachable from spawn
  5. Esc opens pause
  6. pause can quit
- A fixed screenshot set captured each build.

**Gate:** an agent can see the running game, and the class of bug listed above fails a test
instead of reaching the developer.

---

## Phase 1 · Faces

`CharacterSprite` draws figures with arithmetic, which is why they read as programmer art.

- Blender script in `Tools/Blender/`: one low-poly figure, rendered at 8 yaw angles to a sprite
  sheet. This is what Arena and Daggerfall actually did.
- Six archetypes — guard, vendor, official, prisoner, mage, commoner — varied by garment
  colour, not by mesh.
- `CharacterSprite` becomes a sheet renderer selecting the frame from actor-to-camera angle.

**Gate:** NPCs read as painted figures that turn to face you.

> **Blocked on the developer.** Blender MCP is configured on the machine but was not connected
> to the session that wrote this plan. Write the pipeline; the developer runs it, or connects
> the MCP. This is the only hard external dependency in the plan.

---

## Phase 2 · A loop

The street is a place, not a game. A demo needs something that starts and finishes.

- One errand: an NPC asks, you fetch, you return, something changes.
- Objective line, compass bearing and journal entry wired to it — all three systems exist.
- The Order Hall authored as a real interior instead of a generated grey room.

**Gate:** a five-minute route with a beginning, middle and end.

---

## Phase 3 · Authoring

This is what makes the Unity decision correct rather than merely defensible. Today
`Main.unity` is destructively regenerated and hand-authoring is forbidden, which removes the
reason to use an engine with an editor.

- Scenes stop being destructively regenerated; generated content becomes additive.
- Prefabs for anything repeated — stalls, doors, figures, props.
- ProBuilder or hand-authoring for palace, prison and the Stambha.

**Gate:** something placed by hand survives the next rebuild.

---

## Phase 4 · The map

Deliberately last — most visible remaining wrongness, least demo-critical.

- Replace the ellipse-plus-wobble model with authored coastline polygons.
- Trace them from the developer's drawn map.

**Gate:** the in-game map is recognisably the map that was handed over.

---

## What we stop doing

1. **Quoting test counts as progress.** 255 → 270 says nothing about whether the game works.
2. **Adding regression tests to unverified behaviour.** Validation first.
3. **Generating in C# what the editor should author.** Every new builder function makes Unity
   less worth using.
4. **Documentation and lore work.** Both bibles, the beat contract and the handoff are far
   ahead of the game. Nothing more until there is a loop to describe — including this file.
5. **Shipping without looking.** After Phase 0 every change gets a frame attached.

---

## Reordering

The sequence is by dependency, not preference. The one defensible change: **Phase 2 can move
ahead of Phase 1.** A loop with placeholder figures demos better than good figures with nothing
to do.
