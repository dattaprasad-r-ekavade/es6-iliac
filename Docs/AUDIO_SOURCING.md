# Where the sound comes from

**Status:** decision pending. Written 2026-08-28, after the owner's judgement that the
synthesised effects do not sound good.

---

## 1. Soundsnap: yes, but it is a paid subscription

Checked against their own licence rather than from memory.

**The licence is genuinely good for this game.** Worldwide, non-exclusive and **perpetual**;
sounds may be edited and incorporated into creative works, and *video games* are named
explicitly; the resulting work may be monetised; and **attribution is not required.**

**But there is no free tier.** "Your subscription or account governs Your ability to access and
download Sounds." Unlimited downloads run about **$249/year** — roughly ₹21,000, and more than
twice the $100 Steam Direct fee that is also outstanding.

Two restrictions worth knowing:

- **No redistribution as sound.** You may not sell, sublicense or share a sound on a standalone
  basis, nor repackage them as a library. Shipping them baked into a game is exactly the
  permitted case; shipping a `Content/Audio` folder of loose WAVs alongside is closer to the
  prohibited one, and worth keeping in mind for a build that ships uncompressed assets.
- **No AI or ML training** without written permission. Relevant to how this project is built.

**My recommendation: not yet.** The licence is fine and the price is real money spent before a
single stranger has played the game. If the sound library is still the bottleneck after a
playtest, it is an easy purchase to justify then.

---

## 2. The one to use instead

### Sonniss — GameAudioGDC bundle

**Free, royalty-free, commercially usable, no attribution, perpetual, unlimited projects.**
Given away annually around GDC; roughly ten years of bundles are available at once.

This is not a compromise option. It is professional field-recorded material — the same libraries
that are sold commercially the rest of the year — and it is the standard answer for exactly this
situation. Same AI/ML training prohibition as Soundsnap.

**https://sonniss.com/gameaudiogdc**

### Worth having as well

| Source | Licence | Note |
|---|---|---|
| **freesound.org** | Mixed — filter to **CC0** | Enormous. Filter by licence *before* downloading, and keep a record of anything CC-BY |
| **Kenney.nl** | **CC0** | Game-focused, tiny, tidy, zero attribution. Good for interface cues |
| **Pixabay** | Pixabay licence | Free commercial, no attribution |
| **ZapSplat** | Free **with attribution**, or paid without | Only if the first three come up short |

**One rule if anything CC-BY is used:** record it at the time, in the repo, not later. A credits
file assembled at ship time from memory is how licence violations happen.

---

## 3. What to replace, and what to keep

There are twelve cues in `SoundBank`, all synthesised, and **not one audio file in the
repository.** That doctrine earned its place and does not have to be abandoned wholesale.

**Where synthesis fails is specific.** Filtered noise and swept tone produce a *plausible* sound
with no *character*: a thud without a room, a transient without grit. That is worst on the
impacts — and `SoundBank`'s own comment names the one that matters most:

> *"Steel into a body. The single most important sound in the game."*

**Where it succeeds is also specific**, and it is the reason not to throw it away. Per-play
variation is free. `Step` is described as "the most frequently played sound in the game by far",
and one buffer replayed at walking pace stops registering as a footstep and starts registering as
a machine. Getting that from samples means shipping many variants of every footstep.

### The split I would make

**Replace with samples — impacts and events, where character beats variety:**
`HitFlesh` · `Block` · `Death` · `Hurt` · `Cast` · `Door` · `Coin` · `Chime` · `Denied`

**Keep synthesised — high-frequency, low-character, variation matters most:**
`Swing` · `Step` · `Land`

Nine cues at three or four variants each is about thirty files and a few megabytes against a
120 MB build. Negligible.

### The one implementation note

`SoundBank` builds each `SoundEffect` from `forge.ToPcm()`. Loading from disk does **not** need
the content pipeline — `SoundEffect.FromStream` reads a WAV directly, so this is a second branch
in the same loader rather than an MGCB change.

Keep the interface exactly as it is. Everything in the game asks for `Sfx.HitFlesh`; nothing
should learn whether that arrived from a file or a formula.

---

## 4. Update the doctrine when this lands

`SoundBank`'s class comment currently argues for synthesis partly on the grounds that a generated
sound *"needs no licence."* That was true and it was a real advantage. If samples come in, the
comment should say what changed and why — that the licence was never the binding constraint, the
quality of an impact was — rather than being left to contradict the code beneath it.
