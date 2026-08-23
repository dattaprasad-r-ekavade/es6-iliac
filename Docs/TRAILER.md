# Ratna Bay — Trailer Script and Scope Contract

**Purpose:** two jobs at once. It is the 45 seconds that sell the game, and it is the list of
things the vertical slice must be able to show. Anything not in this document does not need to
exist for launch.

**Constraints, from Steam's own guidance:**

- 30–60 seconds. Drop-off is steep past 90 for an unknown indie.
- The **first three seconds** must be the game's visual identity. No logo, no title card, no
  black screen with text.
- Steam auto-generates a **six-second microtrailer** from the opening of this video, shown
  before anyone clicks through. The first six seconds therefore do double duty and must read
  with no context and no sound.
- The custom thumbnail must be an actual frame from the video, 1920×1080.

---

## The one sentence

> *A mining town digs up stones that raise the dead. You are hired to go down and clear them.*

Every shot below serves that sentence or the loop it implies. If a shot does neither, it is cut.

---

## Shot list

| # | Time | Shot | Sound | Status |
|---|---|---|---|---|
| 1 | 0:00–0:03 | Close on a jiva stone half-buried in lava-cave rock, glowing. Prana bleeds out of it and a **preta rises** from the floor in front of the camera | A low tone, one hit | **Build: rise animation** |
| 2 | 0:03–0:06 | Pull back: three preta in an orange-lit cave, the player's sword rising into frame | Ambient cave | Have |
| 3 | 0:06–0:12 | Melee: two swings, a guard that visibly absorbs a blow, a kill with the gold marker | Impacts | Have |
| 4 | 0:12–0:18 | Turn, cast **Rime** — bolt crosses the room in pale blue, target flashes cold and visibly slows | Cast whoosh | Have |
| 5 | 0:18–0:22 | Cut to a **water cave** — blue shading, different preta. Cast **Arc**, gold bolt, it chains to a second target | Crackle | **Build: themes** |
| 6 | 0:22–0:30 | The camp decision. Banked count on screen. Camera looks at the camp, then at the unopened door. **Beat.** The door opens | Silence, then one door sound | **Build: camp UI** |
| 7 | 0:30–0:35 | It goes wrong. Damage arc from behind, health dropping, the screen edge red. The player dies | Muffled, dropping out | Have |
| 8 | 0:35–0:41 | Quiet. A **new Deepankar** walks into the same cave and picks up the fallen one's cache. Amulets carry over on screen | A single held note | **Build: succession** |
| 9 | 0:41–0:45 | Cut to the fort. A door that was shut is open. Someone inside turns to look. Title: **RATNA BAY** | Music resolves | **Build: fort** |

**Total: 45 seconds.**

---

## Why this order

**Shots 1–2 are the microtrailer.** They have to carry the whole premise with no sound and no
context: a stone, a dead thing rising out of it, a sword. Someone who sees only these six seconds
should be able to repeat the pitch.

**Shots 3–5 answer "what do I do".** Melee, then magic, then a second cave that visibly plays
differently. Five exists to prove the game is not one room recoloured — it is the replayability
argument, made visually.

**Shot 6 is the game.** Eight seconds on a decision with no action in it is a deliberate risk: it
is the only part of the trailer that shows the loop rather than the combat, and it is what
separates this from every other sprite dungeon crawler. If it is dull on screen it is probably
dull to play, and that is worth learning here.

**Shots 7–8 are the differentiator.** Dying is common. Being *replaced*, and walking back to
collect your predecessor's body, is not. This gets six seconds — more than the combat — because
it is the thing no competitor in this niche has.

**Shot 9 is the reason to buy.** The town opens. There is something behind that.

---

## What this demands of the slice

Everything the trailer shows must exist and be filmable. Ranked by what is not yet built:

1. **Preta rising from a stone** (shot 1) — the single most important asset in the trailer, and
   currently enemies simply appear. Needs a spawn animation: the stone dims, the figure rises.
2. **Cave themes** (shot 5) — at minimum lava and water, with visible colour shading and
   different preta. Two themes will film; five ship.
3. **The camp decision UI** (shot 6) — banked count, the camp, the door, and a readable choice.
4. **Succession** (shot 8) — death, a successor, and the recoverable cache made visible.
5. **The fort** (shot 9) — one room opening is enough to film. Ten ship.

Already filmable: melee with guard and hit markers, elemental bolts with chaining, the damage
arc and death, the weapon in hand, sprite enemies, the HUD.

**Scope contract:** the trailer needs *two* cave themes and *one* fort room opening. Five themes
and ten rooms are for the release, not for the film. Do not build ten rooms to shoot one.

---

## Production notes

**Shoot it deterministically.** The game already takes reproducible captures — `--screenshot`,
`--yaw`, `--pitch`, `--swing`, `--cast`, `--show`. Extend that with a scripted camera path and
fixed seeds so a shot can be re-filmed identically after the game changes. Trailers get recut
many times; without this, every recut is a day of replaying runs hoping for a good one.

**Record over the top with OBS.** Capture at 1920×1080, 60fps.

**The thumbnail** must be a frame from the video. Shot 1 or shot 5 — a preta lit by a stone.

**Capsule art is a separate commission and matters as much as the trailer**, because it earns
the click that leads to it.

---

## Sequence

1. **Now** — this script. Free, and it has already produced a build list.
2. **Iterations 13–14** — generated mines and the run loop. Shot 6 comes out of this.
3. **Then the trailer shots** — rise animation, two themes, succession, one fort room.
4. **itch.io** — free, reversible. Put it in front of strangers and watch whether they push one
   room too far.
5. **Fix what they find.**
6. **Steam page and trailer.** One first impression; spend it on a loop that has been validated
   rather than one that is hoped for.
7. **Keep building, keep posting.** Wishlists compound with activity, not with age alone.
