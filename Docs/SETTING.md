# Ratna Bay — Setting

**Status:** proposed. This document exists to be argued with once and then closed, like the
others. It is written for the two people who need it most: whoever draws the game, and whoever
decides what a room contains.

**What it is for.** [`design_pivot.md`](design_pivot.md) says what the player *does*.
[`TRAILER.md`](TRAILER.md) says what the game must be able to *show*. This says what the world
*is*, so that both of those stop needing to be invented one prop at a time.

---

## 1. The period, and why it is this one

The game is set in a mining province of an empire modelled on the **Maurya, c. 300 BCE**.

Not a fantasy kingdom with Indian decoration. A specific century, chosen because that century
already contains the game's mechanics as documented fact:

**The state owned the mines.** The *Arthashastra* devotes chapters to them and to the
officials who ran them — an **Akaradhyaksha**, superintendent of mines, alongside superintendents
of gold, of metals, and of the mint. The text is explicit about why the crown cares: mines fill
the treasury, the treasury pays the army, and the army is the state. That is not a theme somebody
bolted onto a roguelike. It is a surviving policy document that happens to describe Ratna Bay's
economy exactly.

> **One caveat, and it only affects what you may claim in public.** The *Arthashastra*'s date is
> contested — current scholarship puts its composition well after the Maurya and reads it as
> evidence for post-Mauryan practice. That is fine for a fictional province in a Mauryan-analogue
> empire, and it is not fine in a store-page sentence beginning "historically accurate." Say
> *inspired by*. See [`NAMES_AND_OFFICES.md`](NAMES_AND_OFFICES.md) §0.

**The mining was real, deep, and lethal.** There is archaeological evidence of ancient workings
in the goldfields of what is now Karnataka — Hutti and Kolar among them — with narrow shafts
driven by hand far below the surface. The dating is debated, which is useful: it leaves room to
invent without contradicting anything settled.

**The empire's signature object is a carved pillar.** Ashoka's pillars are monolithic polished
sandstone, twelve to fifteen metres, topped with animal capitals, and inscribed with edicts
addressed to whoever walks past. A state that writes its conscience onto stone columns and plants
them across its territory is a state that has already built the game's central prop.

So the setting is not a costume. Every one of the three things the design most needed to justify
— a taxed and guarded mining town, a state that treats extraction as its lifeblood, and a
speaking pillar in a cave — is load-bearing history.

**Fictionalise the polity.** Use a named province and a named governor, in an empire that is
recognisably Mauryan but is not the Maurya, and never name Ashoka. This buys freedom to invent
and avoids putting invented words in a real emperor's mouth. Real Sanskrit and Prakrit verses
quoted as found text are a different matter and are fine.

---

## 2. Ratna Bay

A river province at the edge of the empire's reach. Far enough from the capital that the
governor's word is the empire's word; close enough that the tax convoys leave on schedule and
the accounting is real.

The mountain gives up **jiva stones**. Empty, they are crystal and worth what crystal is worth.
Filled, they hold **prana**, and prana is what the province actually exports — to the capital's
lamps, its foundries, its physicians, and its army.

The province is therefore three things stacked on one another:

- a **revenue instrument**, audited from a thousand miles away;
- a **garrison**, because a revenue instrument on a frontier needs one;
- and a **town of people** who live on top of a mountain that kills them.

The tax is not villainy. It is arithmetic, and the officials collecting it mostly believe in it.
That is what makes it worth writing.

### Why the dead rise

Stones left in the rock do not stay empty. They draw prana from what is around them, and what is
around them, in a mountain that has been worked for centuries, is the dead.

A filled stone in a worked seam gives that prana back to what it took it from. **Preta** rise:
the past lives the mountain has taken — miners, soldiers, the convict labour the state sent down
in earlier reigns, and older things from before the province had a name.

This makes mining lethal in a way the state cannot tax its way around, so it does what states do
and contracts the problem out.

### The Deepankar

The **Deepankar** — light-givers — are the order the province hires to go down and clear the
caves before the miners follow.

They are paid in what they bring up. They are not soldiers, not priests, and not quite
respectable. The order continues when a member does not, which is both the fiction and the
meta-progression: when you die, another takes up the lamp, inherits the amulets, and goes down to
fetch your body.

> **Author the irony, do not stumble into it.** *Dipankara* is the name of a Buddha of the past.
> An order that takes that name and then clears caves for a cut of the proceeds is either sharp
> writing or an accident. Make it sharp: they were founded as something else, and the name is
> older than what they have become. One line of dialogue, somewhere in the fort, should know it.

### What a preta is, and how to treat it

This matters for tone and it matters for how the game reads to a large part of its audience.

**Preta are not zombies.** In both Buddhist and Hindu cosmology they are beings in a state of
suffering — hunger that cannot be satisfied, want that outlives the body. They are objects of
compassion in the source material, not of disgust.

**So the verb is release, not exterminate.** The Deepankar do not kill preta; they finish
something that was left unfinished. That is what a light-giver is for. This costs nothing
mechanically — the fight is the same fight — and it buys three things:

- a moral spine that actually resolves, rather than a body count;
- a reason the order is tolerated by the province's priests;
- and distance from the reading where an Indian studio farms hungry ghosts for loot.

A preta should read as **someone**, briefly, at the moment it goes. That is a two-second art
problem and it is the most valuable two seconds in the game.

---

## 3. The moral spine

Prana can be gathered three ways, and the difference between them is the story:

| Method | What it costs | Standing |
|---|---|---|
| **Caught at release** | Nothing. The almost-dead give it up anyway | Lawful, and the state's official position |
| **Drip-fed from the living** | Leaves the donor half dead | Illegal, lucrative, and everywhere |
| **Taken from animals** | Barely works | Legal, pitiful, the resort of the desperate |

The province's official line is the first. Its actual supply depends on the second. Everyone
above a certain rank knows this, and the fort's ten rooms are where that is gradually admitted.

The carved verse is the argument, not the decoration:

> **मा गृधः कस्य स्विद्धनम्** — *"Covet not; for whose is wealth?"* (Isha Upanishad 1)

It is carved by the state, in a mine the state opened, to extract wealth. Nobody in the fiction
finds this funny. That is the point.

---

## 4. Art direction

This is the section to steal from. Everything here is cheap in a flat-pigment, box-geometry
renderer, and none of it is generic fantasy.

### The one material that defines the look: Mauryan polish

The period's stonework has a famous mirror-bright finish — a hard specular sheen on sandstone
that later centuries did not reproduce.

**This is a gift to this renderer.** The world is flat pigment with no specular. Give *worked
imperial stone* — and only that — a hard specular highlight, and you get a material language for
free:

- **Dull, matte, granular** — the mountain, raw rock, the miners' own work
- **Polished, and it catches the light** — anything the state cut

The player learns to read "the empire was here" from a highlight, with no UI and no text. A
pillar in a cave two hundred metres down means somebody official came this far, once.

### Palette

| | |
|---|---|
| **Rock** | Cold greys and iron browns, desaturated, matte |
| **Imperial stone** | Warm buff sandstone, polished, the one specular surface |
| **Jiva stone** | Warm amber-gold, emissive, the only true light source below ground |
| **Prana** | Pale gold in the stone; sickly green-white when drawn from the living |
| **Preta** | Drained versions of living palettes — the same person, with the colour taken out |
| **Cave themes** | Shift the *rock* hue per theme; keep stone and prana constant so the reads stay stable |

### Props and objects, all period-specific and all cheap

- **Punch-marked coins** (*karshapana*) — irregular silver blanks stamped with several small
  symbols. Nothing like a generic round gold coin, and a far better currency icon.
- **Northern Black Polished Ware** — the era's luxury ceramic, lustrous near-black. One shape,
  one shader, instantly period.
- **Ring-wells**, stacked terracotta rings — a silhouette nobody has seen in a game.
- **Iron tools**: hand-hammered picks, wedges, and the timber cribbing of a shaft.
- **Palm-leaf records** and clay sealings — the fort's paperwork, and the province's real weapon.
- **Oil lamps** — the Deepankar's own emblem, and a light source that justifies itself.

### The fort

Not a castle. The period's fortifications are **timber palisade, rammed earth, and mud brick**,
with stone reserved for what matters. Megasthenes described the Mauryan capital as a vast wooden
palisade with towers, and that is a far more interesting silhouette than a keep — and cheaper to
build out of boxes.

Ten rooms, per the design. Suggested occupants, each carrying one turn of the story:

1. The gate, and the tally-keeper who writes down what you bring up
2. The order's own hall — lamps, and the names of the fallen
3. The assayer, who weighs stones and does not ask where they came from
4. The smith
5. The physician, who buys prana and will not say from whom
6. The mine registrar — an imperial officer, not a local
7. The shrine, and a priest with an opinion about what you do for a living
8. The barracks, and a garrison captain who resents you
9. The governor's clerk, who has the accounts
10. The governor

Rank and gold open them in roughly that order, which is also the order in which the province
stops being able to lie to you.

### Figures

Unstitched cloth — *antariya* and *uttariya*, wrapped and draped, turbans, heavy jewellery, bare
torsos common for men. This is genuinely useful: **draped cloth and a strong jewellery silhouette
read better at sprite scale than plate armour does**, because the shape is large and simple and
the highlights are few and bright.

Weapons of the period: the long bamboo bow, iron swords, the small round shield.

---

## 5. The script problem, and the recommendation

**The verse is right. The script is a thousand years early.**

- **Devanagari** developed around the 7th–10th century CE. In a c. 300 BCE cave it is as
  anachronistic as a printed sign.
- The Mauryan inscriptional script is **Brahmi** — which is the ancestor Devanagari eventually
  descends from — and the language of the edicts is Prakrit, not Sanskrit.
- Sanskrit itself is fine as *spoken and recited* language in this period. The Isha Upanishad's
  text belongs to the era's religious tradition. It is putting it on a wall **in Devanagari** that
  is the error.

### The recommendation: carve in Brahmi

Switch in-world carved text to Brahmi, and keep Devanagari and English in the interface layer.

This is not only the accurate choice. It is the **better-looking and more commercial** one:

1. **It is more distinctive, not less.** Devanagari is the script of modern Hindi and Marathi
   signage. To a global audience it reads as "Indian game." Brahmi reads as *"what is that"* —
   which is precisely the job of six muted seconds in a Steam grid.
2. **It suits stone and it suits this renderer.** Brahmi is angular, open, and largely free of the
   dense stacked conjuncts that turn Devanagari to mush at low resolution — a problem
   `StambhaCarving.cs` already carries a comment about.
3. **Nobody has used it.** It is a genuinely unclaimed visual identity.
4. **Comprehension is unaffected.** The trailer already puts the translation in the lower third,
   and the game already shows the English beneath the pillar. The carving was never the thing
   being read; it is the thing being *seen*.

**Practicalities, in advance:**

- Noto Sans Brahmi exists under the OFL, matching the licensing already used for Cinzel and Noto.
- The Brahmi Unicode block sits above the Basic Multilingual Plane, so each character is a
  surrogate pair in a C# string. Anything indexing a verse by `char` needs to know that.
- Text shaping for Brahmi is less well supported than for Devanagari, and FontStashSharp may not
  form every ligature correctly. This matters less than it sounds: `StambhaCarving` rasterises a
  verse once into a cached texture, so if shaping misbehaves the glyphs can be placed
  individually. A carving is authored art, not live text.
- Keep the Devanagari master in source as the readable form and transliterate at rasterisation
  time, so the verse stays legible to whoever maintains it.

**If Brahmi proves too expensive**, the fallback is not Devanagari — it is to carve *symbols*
rather than script: the punch-marked-coin marks, which are period-correct, trivially drawable, and
carry meaning without needing to be read.

---

## 6. What this changes, and what it does not

**Unchanged.** Every closed decision in `PRODUCTION_PLAN.md` §1. The loop, the run length, the
payout curve, the classes, succession, the fort's ten rooms. This is a setting, not a redesign.

**Changed.**

| | From | To |
|---|---|---|
| Period | Unspecified fantasy | A province of a Mauryan-analogue empire, c. 300 BCE |
| Carved script | Devanagari | Brahmi in the world; Devanagari and English in the UI |
| Stone material | Uniformly matte | Imperial worked stone is the one specular surface |
| Currency art | Gold coins | Punch-marked silver |
| Fort architecture | Unspecified | Timber palisade, rammed earth, mud brick |
| Preta framing | Enemies | Beings to be released — same fight, different verb |

**Open, and worth one decision each:**

- The province's and governor's names, and the empire's.
- Whether the player's life path maps to a *varna* or deliberately refuses to. Refusing is
  probably better and definitely safer.
- Whether the deepest verse stays the Gita line currently in `StambhaCarving.DeepVerse`. It comes
  from a text usually dated later than this setting; recited it is defensible, carved it has the
  same problem the script does.

---

## 7. Sources worth an afternoon

- Kautilya, *Arthashastra*, Book II — the superintendents, the mines, the mint. Read it for the
  vocabulary of the fort as much as for the economics.
- Megasthenes, *Indica*, as preserved in later Greek writers — an outsider's account of the
  Mauryan capital. Useful both as description and as a narrative device.
- The Ashokan pillar edicts — for what a state sounds like when it addresses strangers on stone.
- Photographs of the Sarnath and Rampurva capitals, for the polish. The polish is the look.
