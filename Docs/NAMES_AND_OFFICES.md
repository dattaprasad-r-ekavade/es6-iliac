# Ratna Bay — Names, Offices, and Law

**Status:** research. Raw material for authoring, not a closed decision. Everything here is meant
to be cut down and used, not preserved.

**Companion to** [`SETTING.md`](SETTING.md), which says what the world is. This says what people
in it are called, what their jobs are, and what happens when they break the law.

---

## 0. One honest caveat, before anything is built on it

The *Arthashastra* is the source almost everything below rests on, and its date is contested.
Tradition attributes it to Kautilya at the Mauryan court around 300 BCE. **Current scholarship
largely rejects that**: Olivelle dates its source material to roughly 150 BCE–50 CE with a major
redaction around 175–300 CE; McClish puts the original compilation in the first century BCE.
Some historians now say it describes post-Mauryan practice and has lost its value as direct
evidence for the Mauryan state.

**This does not damage the game at all, and it changes how the game is talked about.**

- **For authoring:** use it freely. It is the richest surviving picture of early Indian statecraft,
  and Ratna Bay is a fictional province in a Mauryan-*analogue* empire. Nothing here needs to be
  true, only coherent.
- **For the store page, interviews, and any "historically accurate" claim:** do not make one. Say
  *inspired by*, and the claim is unfalsifiable and honest. Say *accurate*, and the first
  historian who plays it has a correction and a thread.

This corrects the confidence of [`SETTING.md`](SETTING.md) §1, which described the *Arthashastra*
as straightforwardly Mauryan. The setting argument survives — a state that monopolised mining,
deep lethal shafts, and inscribed pillars are all still good ground — but the text is a literary
source of the period's *ideas*, not a Mauryan civil-service handbook.

---

## 1. Personal names

The useful trick is not a list. It is **two registers**, because the empire had two:

- **Officials speak and write Sanskrit forms.** Imperial, formal, slightly foreign.
- **Everybody else has Prakrit forms.** These are the names that actually appear on donor
  inscriptions at Sanchi and Bharhut — real people, mostly ordinary ones.

The same name in both registers is the same person seen from two heights. *Dharmarakshita* on the
tally roll is *Dhamarakhita* to his neighbours. **That contrast is free characterisation**: an
official who uses the Prakrit form is being kind, and a neighbour who uses the Sanskrit one is
being sarcastic.

### Name elements, for generating rather than listing

Most period names are two elements joined. Pick one from each column.

| First element | Meaning | Second element | Meaning |
|---|---|---|---|
| Deva- | god | -gupta | protected |
| Naga- | serpent | -datta | given |
| Dhama- / Dharma- | law, duty | -mitra | friend |
| Budha- | awakened | -rakhita / -rakshita | guarded |
| Isi- (Rishi-) | seer | -sena | army |
| Siri- / Shri- | fortune | -pala | protector |
| Vasu- | wealth | -deva | god |
| Chandra- | moon | -nandi | joy |
| Suvarna- | gold | -bhuti | being |
| Loha- | iron | -giri | mountain |
| Ratna- | jewel | -vardhana | increasing |
| Jiva- | life | -shri | fortune |
| Gopa- | herdsman | -mita (Prakrit -mitra) | friend |

Which gives, without inventing anything: **Nagadatta, Suvarnapala, Jivamitra, Lohasena,
Ratnagupta, Dhamagiri, Gopabhuti, Isidata, Budharakhita, Vasumitra, Chandrashri.**

For a mining province, the ore elements — *Suvarna-*, *Loha-*, *Ratna-* — should be over-
represented among locals and absent among imperial appointees. Families name children after what
the mountain gives.

### Single names that need no compounding

Common, attested, short, and easy for a non-Indian player to hold onto — which matters, because a
player has to remember who to go back to:

**Tissa · Revati · Uttara · Yasa · Nandi · Siha · Bhadda · Visakha · Kanha · Sivali ·
Sanghamita · Ujjeni**

**Practical rule, and where it applies.** Ease of pronunciation is spent on the **title and the
key elements** — the words a player reads on every screen, says out loud to a friend, and searches
for: the stones, the spells, the risen, the ranks, and what the order calls itself. Those must be
sayable on sight.

**Character names are not on that list.** An NPC's name is a label the player points at, not a word
they have to carry, so a heavy compound like *Suvarnapala* or *Bhadrasena* costs nothing and buys
period texture. Let them be long.

What the ten fort occupants still owe the player is **distinctness**, not lightness: no two should
start with the same letter, because the player has to remember *who to go back to*, and that is a
recognition problem rather than a pronunciation one.

---

## 2. Offices and duties

The *Arthashastra* organises the state as a set of **adhyaksha** — superintendents, roughly thirty
of them, each running one department. This is the single most useful structure in the whole
research pile, because **a superintendent is a quest-giver with a jurisdiction**.

### The ones this game actually needs

| Title | Department | What they want from the player |
|---|---|---|
| **Akaradhyaksha** | Mines | The reason the game exists. Required to know mineralogy and metallurgy, and to judge ore by colour, streak, weight and smell. Opens mines; assesses what you bring up |
| **Suvarnadhyaksha** | Gold | Buys, weighs, and suspects you |
| **Lohadhyaksha** | Metals | The smith's superior; supplies tools and picks |
| **Lakshanadhyaksha** | The mint | Ensures no bad coin leaves the mint and no coin leaves unauthorised. A natural source of counterfeiting plots |
| **Samaharta** | Collector-general | Revenue. The tax that makes the province hated |
| **Sannidhata** | Treasurer | Custody of what the Samaharta collects |
| **Nagaraka** | The city | Order, fire, lodging, and who is sleeping where |
| **Amatya** | Minister / high official | Selected for demonstrated capacity and character. The governor's staff |
| **Mahamatra** | High officer | Above the rest; an imperial presence the province cannot ignore |

### The district and village layer, from Ashoka's own edicts

Unlike the *Arthashastra*, these titles appear in the inscriptions themselves, which makes them
the most securely attested words in this document:

| Title | Duty |
|---|---|
| **Rajuka** | Originally surveying and measuring land; Ashoka extended their remit to justice |
| **Pradeshika** | District head — land revenue, and law and order |
| **Yukta** | Subordinate officer assisting the above. The junior rank |
| **Dhamma-mahamatta** | Officers of *dhamma*: propagate it, and keep the emperor in touch with opinion |

Ashoka ordered the yuktas, rajukas and pradeshikas onto **inspection tours every five years**.
That is a ready-made plot engine: an inspection is coming, and the province's books do not add up.

### The intelligence service

The *Arthashastra* treats espionage as ordinary administration, and the vocabulary maps almost
exactly onto the watcher and detection systems already in the codebase.

**Gudhapurusha** — secret agents — split two ways:

- **Samstha**, stationary, working a fixed post under cover: *kapatika* (a student), *udasthita*
  (a recluse), *grihapatika* (a householder), *vaidehaka* (a merchant), *tapasa* (an ascetic).
- **Sanchara**, roving, moving between places and carrying what they learn.

**Use this directly.** The traders and townsfolk already standing around the fort should include
one of each cover. A merchant who is also a *vaidehaka* explains why prices are strange and why
the garrison knew where you were. It costs one flag on an existing NPC.

---

## 3. The judicial system

Two court systems, and the distinction between them is unusually clean.

### Dharmasthiya — the civil courts

Personal disputes: marriage, inheritance, *stridhana* (a wife's own property), contracts, debt.

Bench: **three dharmastha** (jurists learned in the sacred law) sitting with **three amatya**.

### Kantakashodhana — literally, "the removal of thorns"

Criminal matters, and offences between a person and the state: theft, murder, wages, fraud,
sedition. Presided over by **pradeshtri**.

**That name is the best single piece of writing in the research.** A criminal court called
*the removal of thorns* tells you exactly how the state sees the accused — not as a citizen with a
case, but as an obstruction in the road. Use the phrase in dialogue verbatim.

Critically, these courts **ran on the spy network**. Criminal justice and intelligence were one
apparatus. In a province where the player pickpockets, steals, and carries contraband stones, that
is the whole tension in one sentence.

### Where the courts sat

Village assembly at the bottom; courts at **sangrahana**, **dronamukha**, and **janapada** level
above it; the **king** as the highest judicial authority. For Ratna Bay, collapse this to two:
the fort's court, and an appeal that goes to the capital and never comes back.

### Punishment

**Danda** — the rod, punishment, coercion. *Dandaniti*, the science of it, is one of the classical
branches of learning. Punishments are severe and graded by caste, status, and intent, with fines
the ordinary instrument and mutilation and death available above them.

Two provisions from the mining chapter matter enormously here:

> A mine labourer who steals mineral products **except precious stones** shall be fined **eight
> times their value**. Theft of **precious stones** carries **death**.

**This is the game's criminal law, already written, and it is perfect.** Ore is a fine. A jiva
stone is your life. The province's entire moral economy — that the thing everyone needs is the
thing nobody may take — is a footnote in a 2,000-year-old text about mine management.

Also usable: mining without authorisation draws imprisonment and forced labour. That is what
happens to a Deepankar who opens a mine they did not pay for.

### Two Ashokan reforms worth stealing

From Pillar Edict IV, the emperor:

- gave the **rajukas independence** in judicial matters — deliberately putting judgement beyond
  his own reach;
- granted the condemned **three days' respite** before execution, to settle their affairs and
  prepare.

The second is extraordinary game material. **A three-day clock on a death sentence is a quest.**
And an empire that invents mercy as policy while running a lethal extraction economy is exactly
the contradiction the game is about.

---

## 4. The renames

### Preta → **Chhaya**

The problem with *preta*, from [`SETTING.md`](SETTING.md) §2: they are a live cosmological
category in two religions, beings of suffering meant to be pitied. Farming them reads badly, and
the word carries doctrinal weight the game does not want to argue with.

**Recommendation: छाया / Chhaya — "shadow."** Two syllables, pronounceable on sight by a player
with no Sanskrit, unambiguous in meaning, and doctrinally inert. A shadow is what is left when the
thing that cast it has gone, which is precisely what the mountain is producing.

And it gives a **tier ladder for free**, which the enemy catalogue currently lacks:

| Tier | Name | Meaning | Read |
|---|---|---|---|
| Common | **Chhaya** | shadow | Barely a person any more. Most of a mine |
| Elite | **Vetala** | the corpse-animating spirit of Indian folklore — the *Baital* of the *Baital Pachisi* | Deliberate, and it knows you are there |
| Boss | **Kravyada** | "flesh-eater" | Old, and it was something before it was this |

*Vetala* is the strongest of the three and worth noting specially: it is a genuine folklore
category **whose entire definition is a spirit that animates a corpse**. It is exactly the game's
mechanic, it is already famous enough to feel real, and unlike *preta* it belongs to story rather
than to doctrine.

**Keep the word *preta* — for one character.** The fort's priest, and only the priest, calls them
that, and objects to the other words. One line, and the game has acknowledged what it is doing
without building its loot economy on it.

### Deepankar → **Dipadhara**, with a second name

The problem: *Dipankara* is the name of a Buddha of the past.

**Recommendation: two names, because a real bureaucracy always produces two.**

- **Formally, on the tally roll: `Akara-shantika`** — "mine-pacifiers." *Shantika* is a real
  category: one who performs *shanti* rites, the pacification of what is dangerous or unquiet.
  It is period-plausible, it is precisely their job, and it is what a state calls a contractor.
- **Commonly, to everyone else: `Dipadhara`** — "lamp-bearer." A clean compound with no existing
  referent, and it keeps the lamp, which the trailer's closing line depends on: *"When one falls,
  another takes the lamp."*

The gap between the two names is characterisation for nothing. The registrar says *akara-shantika*.
The miners say *dipadhara*. What the order calls itself when nobody official is listening is a
question worth one line of dialogue.

### The rank ladder

The fort opens by rank, so the ranks should be the empire's own words, bottom to top:

| Rank | Meaning | Fort rooms opened |
|---|---|---|
| **Yukta** | subordinate officer | 1–2 |
| **Sthanika** | district officer | 3–4 |
| **Pradeshika** | district head, revenue and order | 5–7 |
| **Adhyaksha** | superintendent | 8–9 |
| **Mahamatra** | high officer | 10 |

A player promoted from *yukta* to *sthanika* has been promoted inside a real civil service, and the
words do the worldbuilding without a codex entry.

---

## 5. What to do with this

Small, cheap, and worth doing in this order:

1. **Rename in code.** `EnemyCatalog.PretaId` → `chhaya`, and add `vetala` as the elite tier the
   catalogue is missing. Save-compatible via the existing versioned save.
2. **Rename the order** in `design_pivot.md`, `TRAILER.md`, and dialogue content.
3. **Adopt the rank words** as the fort's gate, replacing whatever placeholder rank exists.
4. **Flag three existing NPCs** as *vaidehaka*, *grihapatika* and *tapasa* covers. No new content —
   one field, and the watcher system already does the rest.
5. **Write the criminal law into the shop and crime systems**: ore is a fine of eight times value;
   a jiva stone is death. That single rule makes the pickpocketing system carry the setting.
6. **Keep the three-day respite in a drawer** until there is a quest that needs a clock.

---

## Sources

- [Arthashastra — Conducting Mining Operations and Manufacture (Book II, ch. 12)](https://www.wisdomlib.org/hinduism/book/kautilya-arthashastra/d/doc366058.html)
- [Arthashastra — Wikipedia](https://en.wikipedia.org/wiki/Arthashastra) (composition date, Olivelle and McClish)
- [Maurya Empire — Wikipedia](https://en.wikipedia.org/wiki/Maurya_Empire)
- [Edicts of Ashoka — Wikipedia](https://en.wikipedia.org/wiki/Edicts_of_Ashoka)
- [Mahamatra — Wikipedia](https://en.wikipedia.org/wiki/Mahamatra)
- [The Edicts of King Asoka, tr. Dhammika](https://www.accesstoinsight.org/lib/authors/dhammika/wheel386.html)
- [Judicial Administration of the Mauryan Empire](https://lawfullegal.in/judicial-administration-of-mauryan-empire/)
- [Mauryan Administration — central, provincial, judicial](https://pwonlyias.com/udaan/mauryan-administration/)
- [The Akara-adhyaksha: Managing Mauryan Mineral Wealth](https://www.swaveda.com/articles/the-akara-adhyaksha-managing-mauryan-mineral-wealth-541/)
- [Spies in the Arthaśāstra: Saṃsthā](https://www.wisdomlib.org/hinduism/essay/shishupala-vadha-study/d/doc1150171.html)
