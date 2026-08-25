# Getting the Build to Strangers, and the Data Back

**Written:** 2026-08-25
**Why:** the board's Next item is *"play it, then hand it to somebody"*, and iteration 14's exit
condition — **"you catch yourself pushing one room too far"** — cannot be met without strangers.
**Scope:** how to distribute on itch.io, what the obligations are, and how to get telemetry back.

> **This is not legal advice.** Every legal statement below is a report of what a named source says,
> with the source linked. Anything involving tax, data protection, or a contract should be checked
> with somebody qualified before money or personal data moves.

---

## 1. The one thing this is for

There is exactly one open question, and it is worth restating before any tooling decision, because it
disqualifies most of what a distribution-and-analytics setup would normally do.

The recorded descent so far reads: four real doors, **pressed on at all four**, median hesitation
**1.0 second**, one answer at 0.3s — not long enough to read the panel. Rated 6/10, with *"all the
rooms feel the same, I just had to power up."*

So the question is not "how many people played" or "where did they drop off". It is: **at a door,
with stones on the table, does anybody stop and think?** That is a handful of numbers per session,
from a handful of people. The whole pipeline below should be sized for *tens of sessions*, not
millions of events, and anything that costs more than that is the wrong tool.

The repository already answers the hard half of this. `RatnaBay.Domain/Telemetry/PlayRecording.cs`
records the events, `PlayReview` turns them into the numbers, `PlayRecorder` writes one JSON file per
sitting, and `RatnaBay.Tools review` reads it back and prints a verdict. **None of that needs
building.** The only missing piece is that the file never leaves the tester's machine.

---

## 2. Part one — pushing the build to itch.io

### 2.1 Why itch.io actually fits this

Not brand loyalty — three specific properties:

- **It costs nothing and requires no store approval.** Pages are free to create and there are no ads
  placed on them ([Creator FAQ](https://itch.io/docs/creators/faq)).
- **Uploads are incremental.** itch.io's own distribution layer, *wharf*, diffs each build against
  the last and uploads only what changed — "most of the time you'll be somewhere in the middle, with
  maybe 5% to 20% fresh data" ([butler manual](https://itch.io/docs/butler/pushing.html)). For a
  **131 MB self-contained build** that is the difference between a viable and an unviable iteration
  loop. itch.io explicitly frames this as not penalising you for sending testers a new build.
- **Access control is granular enough for a closed test**, which matters because the game should not
  be public while the central question is still open. itch.io's own guidance on early access is
  blunt about this: launching to collect playtest feedback risks "feedback and bad reviews that may
  damage the future success of your game", and the launch day is the one that matters most
  ([Limited Playtests & Releases](https://itch.io/docs/creators/limited-releases)).

### 2.2 The workflow

The page is created in the browser; every build goes up through `butler`, itch.io's CLI. Download it
from `itchio.itch.io/butler`, then:

```bash
butler login                                   # opens a browser to authorise
butler push build ratnabay/ratna-bay:windows   # directory-or-zip  user/game:channel
```

The channel name is not cosmetic — it sets the platform tag. A name containing `win`/`windows` is
tagged a Windows executable, and one containing `android` is tagged an Android application. The
convention is lower-case kebab-case ([butler manual](https://itch.io/docs/butler/pushing.html)).

The flags that matter here:

| Flag | What it does | Why it matters |
|---|---|---|
| `--userversion 1.1.0` | Sets your own version string instead of itch.io's auto-incrementing integer | The build already stamps `PlayRecorder.Build` from the assembly version — pass the same string and a recording can be traced to a build |
| `--userversion-file f.txt` | Reads the version from a file | Fits a scripted `publish.ps1` better than a literal |
| `--if-changed` | Skips the push entirely if nothing changed | Stops no-op patches when re-running the pipeline |
| `--dry-run` / `push-preview` | Lists what would upload / classifies each file NEW·MODIFIED·DELETED·SAME | Worth running once against the 131 MB folder to see what is actually being shipped |
| `--ignore '*.pdb'` | Excludes patterns | The recommended practice is still to push a folder that is *exactly* the release build |
| `--hidden` | Creates the channel without making it visible | **Only works on the first push to a new channel.** Pushing `--hidden` to an existing channel is an error |

Two limits worth knowing: builds over **30 GB uncompressed** are rejected, and there is no way to tag
a channel by architecture (32- vs 64-bit).

For CI, `butler` reads `BUTLER_API_KEY` from the environment instead of an interactive login. This
slots into `publish.ps1` as one more gate: the publish already refuses to produce a folder whose
domain tests, sim, or self-test failed, and the push should sit *after* that, never beside it.

There is also a free, unauthenticated update-check endpoint:

```
GET https://api.itch.io/wharf/latest?target=user/game&channel_name=windows
```

It returns `{ latest: "106" }`. The game could tell a tester they are on a stale build — worth
remembering, because a playtest where half the notes came from last week's balance is a playtest
wasted. Note it returns "invalid game" for pages set to Private.

### 2.3 Access control — and one trap

itch.io has three visibility levels ([access control docs](https://itch.io/docs/creators/access-control)):

| Level | Who sees it | Downloads |
|---|---|---|
| **Draft** (default) | People who can edit, plus anyone with the secret URL | **No** |
| **Restricted** | Only people you approve | Yes |
| **Public** | Everyone; listed in browse and search, indexed by search engines | Yes |

**The trap:** the secret-URL trick that gets recommended everywhere is a Draft-mode feature, and
**Draft does not serve downloadable files.** itch.io says so directly — "We don't recommend
distributing your project in draft mode since others will not be able to download or purchase it" —
and the community advice is explicit that draft works for HTML5 games but "will not work for
downloadable games"
([forum](https://itch.io/t/264418/sharing-restricted-games)). For a downloadable Windows build,
Draft is for finishing the page, not for testing.

**So the answer for this game is Restricted.** Two ways to let people in:

- **A page password.** Restricted pages can carry one, and it can be pre-filled in the link as
  `https://ratnabay.itch.io/ratna-bay?password=MYPASSWORD` so the tester clicks once and is in. This
  is the low-friction option and needs no itch.io account from the tester.
- **Download keys.** Individual keys generated from the dashboard, attached to a tester's itch.io
  account. Revoking a key removes access. Keys also carry access to the page's community features,
  and a key-holder never needs the password.

Restricted pages are not visible publicly, do not appear in itch.io search, and are not indexed by
search engines ([forum](https://itch.io/t/54982/how-can-i-host-a-closed-beta-for-my-game)).

For 3–30 testers: **Restricted + a password in the URL**, with download keys held in reserve for
anyone whose feedback is valuable enough to want the comment thread attached to their name.

### 2.4 The unsigned-binary problem, and the one clean fix

This is the most under-appreciated practical risk in the whole plan, and it is worth more attention
than the choice of analytics backend.

The build is an unsigned, self-contained 131 MB `.exe`. Downloaded through a browser, Windows shows
**"Windows protected your PC"**, and continuing requires finding a *More info* link that does not
look like a button. Microsoft's own documentation is unambiguous about the options
([smartscreen-reputation](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/smartscreen-reputation)):

| Signing | First-download behaviour |
|---|---|
| Microsoft Store | No warning — re-signed by Microsoft |
| Valid OV/EV certificate | **Still warns** until reputation accumulates; your name is shown instead of "Unknown publisher" |
| No signature | "Windows protected your PC"; user must choose *Run anyway* |
| Self-signed | Same as no signature |

Note the second row. **EV certificates no longer bypass SmartScreen** — Microsoft states the old
behaviour "no longer exists" and that "paying a premium for EV solely to avoid SmartScreen warnings
is no longer justified". Certificates cost roughly $150–300/year for OV, or about $9.99/month for
Azure Artifact Signing, which is limited to organisations in the US/Canada/EU/UK and individuals in
the US/Canada only
([code-signing-options](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/code-signing-options)).
So buying a certificate does not solve the tester's first-run experience; it only stops them seeing
"Unknown publisher" while they still see the warning.

**The fix that does work costs nothing: have testers install through the itch.io desktop app.**
Installing via the itch app — like the Microsoft Store or Steam — avoids the dialog, where a browser
download does not ([discussion of the tradeoff](https://assethoard.com/blog/windows-protected-your-pc-indie-game)).
It also gets wharf's incremental updates for free, which is the whole point of pushing with butler.

**Concrete recommendation:** the tester instructions should lead with the itch app, not the download
button. A tester who abandons at a scary dialog contributes nothing, and this is a five-person
playtest where every drop-out is a fifth of the data.

### 2.5 What itch.io tells you about your players — and what it cannot

The project dashboard gives an analytics tab per project with views, downloads, browser plays,
purchases, earnings, referring domains, collections, comments, ratings, and (for the last week)
impressions and click-through rate, over a selectable window of up to a year
([analytics update](https://itch.io/updates/updates-to-project-analytics-filtering-collections-impressions-and-more)).

None of that is play data, and it structurally cannot be. As put on the itch.io forums: itch.io is
essentially a file-hosting service — it hands over the file and does not know what happens next, so
it "cannot provide you with things it does not collect"
([forum](https://itch.io/t/5281744/adding-actual-analytics-on-itch-for-our-own-releases)). The itch
app does record playtime, but only for people using the app, so it can never represent the whole
tester group.

**Conclusion: itch.io tells you a build was downloaded. It can never tell you whether anybody
hesitated at a door.** The telemetry in Part three is not optional if the question is to be answered.

---

## 3. Part two — the legals

### 3.1 Money: the easiest version is free

Distributing free, with no payment and no donations, avoids the entire financial-compliance surface.
For contrast, here is what applies the moment money is involved:

- **Open Revenue Sharing.** The seller chooses the percentage itch.io takes, from 0% to 100%
  ([payments docs](https://itch.io/docs/creators/payments)).
- **Payment processor fees are passed to the seller**, generally **$0.30 + 2.9%** per transaction.
  itch.io explains this as a consequence of open revenue sharing: since it is not guaranteed a cut,
  it passes operating costs on.
- **A tax interview is mandatory** for sellers using *Collected by itch.io* payouts, because itch.io
  is legally obliged to collect tax information from people it pays
  ([seller account update](https://itch.io/updates/updates-to-itchio-seller-accounts-payouts-tax-interview)).
- **Withholding defaults to 30%.** itch.io is a US company, so funds sent to foreign entities are
  subject to US withholding tax. The default is 30% and applies *even if you are in the US or in a
  treaty country* unless you supply a valid Tax Identification Number. A reduced treaty rate requires
  a TIN — for many countries the domestic tax number works; otherwise an EIN or ITIN may be needed.
  A non-US individual establishes treaty eligibility with a **W-8BEN** (W-8BEN-E for entities)
  ([Stripe explainer](https://stripe.com/resources/more/w-8-ben-tax-form)).
- **The tax interview carries a one-time $3 fee**, deducted from the first payout
  ([community worked example](https://itch.io/t/2604718/any-worked-examples-of-end-to-end-payout-fees)).
- **EU VAT is handled automatically** under *Collected by itch.io, paid later*. Under *Direct
  payments* it is more work on the seller's part ([Creator FAQ](https://itch.io/docs/creators/faq)).

One more clause worth knowing even though it only applies once money exists: **unclaimed earnings
decay.** Revenue from a transaction older than twelve months that has not been withdrawn may be
debited at **10% of the original revenue per month** until the balance reaches zero — so after ten
further months it is gone. It cannot make the balance negative
([ToS §10](https://itch.io/docs/legal/terms)).

**Recommendation: keep the playtest free.** Not because money is hard, but because charging changes
what the tester is. Somebody who paid has bought a product and will review it; somebody invited has
been asked a question and will answer it. The 6/10 rating already in `PLAYTEST_NOTES.md` came from a
question, not a purchase, and that is the mode that produces the finding about levelling being a full
heal. Charging can wait for iteration 21's slice lock.

### 3.2 The Terms of Service, in the parts that bite

From the [Terms of Service](https://itch.io/docs/legal/terms) (page states last updated 15 April
2023). Section numbers are itch.io's.

**Who you are.** Uploading makes you a **Publisher**, and Publishers must be at least 18, or have
parental/guardian consent, and be competent to enter the agreement (§2). Users — the people
downloading — must be 13 or over, and itch.io's privacy policy states the Service is not intended for
under-13s and that it does not knowingly collect their personal information. That effectively sets the
floor audience age, and it interacts with the telemetry question below: a data-protection regime is
much harsher about children's data, and itch.io's own terms already exclude under-13s.

**You are solely responsible for what you upload** (§4), and you warrant that you "own or have the
rights, licenses, permissions and consents necessary to publish, duplicate, and distribute" it. This
is the clause that makes §3.6's font and model licences a contractual matter and not just good
manners.

**The two licences you grant, and the asymmetry between them** (§4):

- **To itch.io:** worldwide, non-exclusive, royalty-free, **sublicensable and transferable**, to use,
  reproduce, distribute, prepare derivative works of, display and perform the content in connection
  with the Service, including promotion and redistribution in any media format. This one terminates
  "within a commercially reasonable time" after you remove the content.
- **To Users:** non-exclusive and **perpetual** — and explicitly, "Users shall retain a license to
  this content even after the content is removed from the Service."

**That second one is the single most important clause for a playtest.** A build you hand to a tester
cannot be un-handed. Delete the page, revoke the download key, take the game private: everyone who
already downloaded keeps a perpetual licence to that build. Combined with itch.io's own advice that
launching early to collect feedback risks feedback and reviews that damage the eventual launch, the
conclusion is the same one §2.3 reached from a different direction — **keep the group small, keep the
page Restricted, and treat every build you push as permanent.**

**Indemnity, and why it touches telemetry** (§16). You agree to defend and indemnify itch.io against
claims arising from your use of the service, your breach of the terms, **your violation of any third
party right "including without limitation any copyright, trademark, property or privacy right"**, and
any claim that your content caused damage to a third party. So if the game's data collection went
wrong in a way that harmed a player, that is contractually yours to carry, not itch.io's. This is the
sharpest reason to keep the telemetry payload boring.

**One clause to read carefully before shipping an uploader.** The Acceptable Use list in §3 prohibits,
among other things, "Soliciting, harvesting or collecting information about others." In context the
list is about conduct on the platform — spam, harassment, impersonation, malicious code — rather than
about a game's own analytics, and thousands of games on itch.io ship analytics. But it is not
qualified, and it sits in a section whose stated consequence is "account termination without prior
notice". If an uploader is ever built, this is worth a support email rather than an assumption.

**The boilerplate, briefly.** Warranty disclaimed and liability limited to the maximum extent
permitted (§11, §12). California law, San Francisco jurisdiction, and the Service is deemed solely
based in California (§14). Class actions waived (§15). itch.io "reserves the right to modify and
amend these Terms of Service at any time and without notice", and continued use signifies acceptance
(§18) — so this section has a shelf life. Any cause of action must be commenced within one year.
Sections 4, 5, 8, 9, 11, 12, 14, 15, 16 and 18 survive termination (§13), which is why the perpetual
user licence outlives the account.

### 3.3 There is no age rating to get

Worth stating plainly because it is a common assumption from Steam and Google Play: **itch.io has no
age-rating system.** No IARC, no ESRB/PEGI submission, no numeric age band. What exists is a single
binary flag under **Metadata » Classification** on the project's edit page:

- **"Has sensitive content — This project is not suited for minors or the workplace"** — checking it
  puts the project behind the adult filter, so it is only shown to accounts that have opted in.
- **"Show content warning"** — optional, and the publisher's choice; it gates the page behind an
  interstitial.

The [quality guidelines](https://itch.io/docs/creators/quality-guidelines) are firm about getting it
right: "If we see that you do not respect the adult content classification, we may remove all of your
pages from being indexed on our discovery features, or permanently disable payments on your account."
They also warn against selecting irrelevant classifications to game discovery, which may be treated
as spam.

**For this game that is a judgement call, not a lookup.** Ratna Bay is about undead dug out of a mine,
with melee combat, floating damage numbers and death. There is no sexual content. The flag's wording
is "not suited for minors", and plenty of purely violent or frightening games are marked under it by
their own developers. The decision is the developer's, it is one checkbox, and it should be made
deliberately rather than left at its default. It also barely matters while the page is Restricted,
since the adult filter governs discovery and a Restricted page is not discoverable at all.

Other page fields are about discoverability rather than obligation: kind, genre, tags, languages,
accessibility flags, a cover image, screenshots, and actual playable files are what it takes to be
indexed. None of that applies to a Restricted playtest page, which is a reason not to spend time on
the page until the game is worth being found.

### 3.4 Telemetry: the part that actually has legal weight

If the game sends anything from a player's machine, data-protection law is engaged for players in the
EU/UK. The practical shape of compliance, as consistently described across the sources below:

**A lawful basis is required, and which one depends on what the data is for.** The pattern used
across the industry splits the two:

- **Crash/stability data → legitimate interest** (GDPR Art. 6(1)(f)). The argument is that fixing
  crashes is necessary to provide a working product and players reasonably expect it. Using this
  basis requires documenting a **Legitimate Interests Assessment (LIA)**, disclosing the processing
  and its basis in a privacy policy, and providing a meaningful opt-out
  ([Bugnet](https://bugnet.io/blog/crash-reporting-gdpr-indie-games)).
- **Optional gameplay analytics → consent** (Art. 6(1)(a)). Consent must be freely given, specific,
  informed and unambiguous. A pre-ticked box does not count. Bundling it into acceptance of the terms
  of service does not count. It must be an opt-in *before* the collection starts, it must be
  withdrawable, and the game must still work if declined. A published, real-world example of exactly
  this split — bug reports on legitimate interest, analytics on consent, requested at first launch and
  withdrawable from the options menu — is
  [Lou's Lagoon's policy](https://megabit-publishing.com/policies/louslagoon-privacy-policy).

One important nuance from the guidance: **aggregated, genuinely non-identifiable analytics can often
rest on legitimate interest**, but the moment there is a unique identifier or anything
fingerprint-like, the ePrivacy Directive tends to require consent regardless of the GDPR basis chosen
([Usercentrics](https://usercentrics.com/knowledge-hub/gdpr-legitimate-interest/)). Third-party
analytics vendors take the strict line: GameAnalytics requires developers to obtain freely given,
specific, informed and unambiguous consent **before any data is sent**, to have a publicly available
privacy notice, and it reserves the right to audit developers for it
([GameAnalytics developer policy](https://www.gameanalytics.com/trust/privacy-faq)).

**What that means for this game specifically, and it is good news:** the existing recording contains
no identifiers at all. Look at what a `PlayEvent` actually holds — `At`, `Kind`, `Detail`, `Value`,
`Extra`, `Health`, `Prana` — plus a UTC start time and a build version on the recording. There is no
name, no machine ID, no account, no IP, no file paths. It is a list of times and numbers about a
fictional character.

That is not an accident of design, it is the design: the recorder exists to answer one question and
nothing else. Keeping it that way is the single largest reduction in legal burden available, and it
costs nothing because it is already true. **The rule to write down is: nothing goes into a
`PlayEvent` that could identify the person holding the mouse.**

The residual exposure is that **any HTTP endpoint sees the sender's IP address**, and an IP is
personal data under GDPR. That is addressed by configuration, not by code: do not log it, do not
store it, and say so.

**Recommended posture for the playtest**, which is also the least work:

1. Ask for consent, once, in plain words, before anything is sent. At a 3–30 person invited
   playtest you are talking to these people anyway — consent can be a sentence in the invitation
   *and* a toggle in the game, and it is honest either way.
2. Default the upload **off** and let the tester turn it on. With this few people, an explicit "yes"
   is easy to get and removes the entire argument about what basis applies.
3. Keep the local file regardless. It is already written, costs nothing, and means a tester who
   declines upload can still zip it and send it if they want to.
4. Write a short privacy note — what is collected, what it is for, how long it is kept, how to opt
   out, and who to contact — and link it from the itch.io page. The retention periods used in real
   policies for this class of data run around 13 months for raw telemetry and 90 days for crash logs
   ([example](https://nanoreality.com/privacy)); for a playtest, "deleted when the question is
   answered" is both shorter and more honest.
5. An in-game opt-out that persists is what the guidance repeatedly names as the minimum. A settings
   toggle is sufficient — `F2` already opens settings.

### 3.5 What itch.io knows about your testers, and what you are for that data

From the [privacy policy](https://itch.io/docs/legal/privacy-policy) (last modified 26 March 2026).

itch.io collects, for its own purposes: information you provide (name, postal address, email,
telephone, payment-provider account details, correspondence, survey responses, transaction details)
and information collected automatically — "traffic data, location data, logs", and device information
"including the user's IP address, and browser type". It uses cookies and web beacons, names **Google
Analytics** as an analytics provider, and names **Cloudflare** as a CDN and security provider that may
itself collect IP address and browser type.

For EU users it states the legal bases it relies on: **performance of a contract**; **consent** for
technical information such as cookie and IP geo-location data and for marketing; and **legitimate
interests** for improving the service, security, fraud prevention and internal administration.

**Two conclusions matter for the developer, and both are conclusions of absence.**

First, **none of that data reaches you.** The policy contains no mechanism for passing player-level
data to publishers, and the dashboard only exposes the aggregate counts described in §2.5. So the
developer is not handling itch.io's player data at all, which is the cleanest possible position: you
cannot be a controller or processor for data you never receive. That is worth being pleased about —
it is why the itch.io route adds almost no data-protection burden by itself, and why every obligation
in §3.4 comes from *your own* telemetry rather than from the platform.

Second, **no Data Processing Agreement for creators appears anywhere in the primary sources.** The
privacy policy describes itch.io's own controller relationship with its users and says nothing about
offering creators a DPA; neither does the ToS. Absence from the documentation is not proof one does
not exist, so this stays on the open list below — but it is only needed if itch.io ever *does* pass
personal data to the developer, which on the evidence above it does not.

### 3.6 The licences already in the build

Independent of itch.io, the shipped folder carries third-party assets, and the repository is already
set up correctly for this — which is worth noting so it does not get broken:

- **Cinzel** and **Noto Sans / Noto Sans Devanagari** are SIL Open Font License 1.1, with `OFL.txt`
  and the family description kept beside each font and copied into the build by the csproj. The
  `Content/Feasibility/ATTRIBUTIONS.md` and `FONT_README.md` files record source and licence.
- The `Kenney` and `PolyHaven` FBX models under `Content/Feasibility` are third-party assets with
  their own terms.

The publish gate already verifies `Cinzel-wght.ttf` reaches the build folder. It does **not** verify
that the licence files travel with it. If the eight FBX models are dropped (see
[`ANDROID_FEASIBILITY.md`](ANDROID_FEASIBILITY.md) §3.3, which notes they are also the props that
occluded the Northwatch yard), that is one fewer set of terms to honour.

### 3.7 What is still open

Answered above from primary sources: the ToS obligations and licence grants, the absence of an age
rating, the fee and withholding model, and what itch.io does and does not know about downloaders.
These remain genuinely unresolved:

- **Whether itch.io offers creators a Data Processing Agreement.** Not mentioned in either the ToS or
  the privacy policy. Only matters if itch.io ever passes personal data to the developer, which on
  the evidence it does not. A support email would settle it.
- **Whether §3's "collecting information about others" is intended to reach a game's own telemetry.**
  Almost certainly not, but the clause is unqualified and the penalty is termination without notice.
  Worth asking before an uploader ships, not after.
- **Whether itch.io requires disclosure of data collection by an uploaded game, or a privacy-policy
  link on the page.** No such requirement was found in the ToS, the quality guidelines, or the
  privacy policy. Doing it anyway is the right call regardless of whether itch.io demands it.
- **Trading-name and contact-details disclosure**, which varies by jurisdiction and bites the moment
  a privacy policy names a data controller — publishing a policy means publishing a contactable
  identity, and for a solo developer that is a real decision about what address to put on the
  internet.
- **The developer's own jurisdiction.** Everything above is written for a US or UK/EU developer.
  Withholding rate, VAT treatment, and whether GDPR applies at all depend on where the developer
  actually is, and that determines which half of §3.1 and §3.4 is relevant.
- **Anything involving actual money.** The moment there is revenue, the tax interview, TIN, treaty
  rate and local return are a matter for an accountant, not for this document.

---

## 4. Part three — getting the telemetry back

### 4.1 What exists, and the exact size of the gap

```
RatnaBay.Domain/Telemetry/PlayRecording.cs   PlayEvent, PlayRecording, PlayReview, the verdict
RatnaBay.Game/Session/PlayRecorder.cs        writes one file per sitting, flushes every 25 events
RatnaBay.Tools  review [path]                reads the newest recording and prints the report
```

Files land in `%APPDATA%\RatnaBay\recordings\play_<yyyyMMdd_HHmmss>.json`.

`PlayRecorder` is already written with the right instincts for something that will eventually talk to
a network: it never throws, never blocks, and on any IO failure it sets `_broken` and silently stops
rather than reporting itself, on the stated grounds that "a player mid-descent has no use for a
message about telemetry". An uploader should inherit exactly that posture.

**The gap is one step: the file is on their disk, not yours.** Everything below is only about closing
that step.

### 4.2 The option ladder, cheapest first

**Rung 0 — ask for the file.** Tell the tester where it is, have them zip and send it. Zero
engineering, works today.

The failure modes are real though: `%APPDATA%` is a hidden path most people cannot find, they forget,
they send the wrong file, or they send the *first* file rather than the newest. Two cheap mitigations
that stay entirely offline:

- A **"reveal recordings folder"** button in settings. On Windows that is a single shell open on
  `PlayRecorder.Directory`. It converts "find AppData" into "click this".
- A **"copy summary to clipboard"** action that runs the existing `PlayReview` in-process and puts
  the report text on the clipboard. The tester pastes it into Discord. This is a genuinely good fit,
  because the thing being asked for is *tens of numbers*, not a file — and `PlayReview.Verdict`
  already produces the one-line conclusion.

For a five-person first pass, rung 0 plus the clipboard button is very likely the right answer, and
it has no legal surface at all: nothing is transmitted, so no lawful basis, no privacy policy, no IP
address. **Start here.**

**Rung 1 — one HTTP POST to an endpoint you own.** When the tester count or the round count makes
asking too lossy. The payload is already JSON and already small.

Cloudflare Workers is the natural fit at this scale, and the free tier is far beyond what this needs
([Workers limits](https://developers.cloudflare.com/workers/platform/limits/),
[pricing](https://developers.cloudflare.com/workers/platform/pricing/)):

| Resource | Free tier | This use case needs |
|---|---|---|
| Worker requests | 100,000/day | ~1 per session |
| CPU per invocation | 10 ms | Storing a blob |
| R2 storage | 10 GB-month, 1M Class A ops/month | Kilobytes |
| D1 | 5M rows read/day, 100k written/day, 5 GB | Tens of rows |
| KV | 100k reads/day but only **1,000 writes/day** | Fine, but note the write asymmetry |

R2 for whole-file drops, or D1 if the events should be queryable. The KV write limit of 1,000/day is
the only figure that could surprise anyone, and it is still three orders of magnitude clear of this
workload.

**Rung 2 — a hosted analytics product.** Worth knowing about, mostly worth skipping.

- **Aptabase** is the closest fit in spirit: open source (AGPLv3, MIT SDKs), explicitly built for
  desktop and mobile apps rather than websites, self-hostable, EU or US data residency, and
  deliberately **no device identifiers, cookies, or fingerprinting**, which is exactly the posture
  this game's data already has ([aptabase.com](https://aptabase.com/),
  [GitHub](https://github.com/aptabase/aptabase)). The catch is the .NET story: the only .NET SDK is
  `Aptabase.Maui`, which is MAUI-specific and targets .NET 8
  ([NuGet](https://www.nuget.org/profiles/aptabase)). For MonoGame it would be a plain `HttpClient`
  call against the HTTP API — at which point it is rung 1 with somebody else's dashboard.
- **GameAnalytics** has no .NET-desktop SDK path worth the trouble and imposes a consent-before-any-
  data requirement plus a right to audit
  ([policy](https://www.gameanalytics.com/trust/privacy-faq)).

The decisive objection to all of them is not cost or compliance, it is fit. These products aggregate.
This project needs *one specific derived number per door* — hesitation in seconds, classified reflex
/ quick / weighed, with forced camps excluded — and `PlayReview` already computes it, with tests
asserting the classification and a documented history of the recorder lying before those tests
existed. Pushing raw events into a generic funnel dashboard would replace a purpose-built, tested
reader with a worse one.

**Recommendation: rung 0 now, rung 1 when it hurts, rung 2 never — or at least not for this
question.**

### 4.3 If rung 1 gets built, the implementation notes

Inherit `PlayRecorder`'s posture, and add:

- **One static `HttpClient`** for the process. Never `using var` a new one per call — that exhausts
  sockets.
- **A short timeout, and treat every failure as success.** 5 seconds, catch everything, set a broken
  flag, move on. The game must not care whether the upload worked.
- **Never on the game thread, and never awaited during a frame.** Upload on exit, or on run end,
  which is a natural pause and is where `PlayEventKind.RunEnded` already fires.
- **Queue offline.** The file is already on disk, which *is* the queue. Try to send unsent recordings
  at next launch and mark them sent by renaming. A tester playing on a train should not lose the
  session.
- **Accept that any key in the binary is public.** A shipped client cannot hold a secret. The
  mitigations are to make the endpoint write-only, rate-limit it, cap the body size, and be willing
  to rotate it. This is acceptable precisely because the data is not sensitive and the audience is
  people you invited.
- **Do not log the IP.** Say so in the privacy note. This is the one line of configuration that does
  most of the compliance work.
- **Send the build version.** `PlayRecording.Build` already carries it. A finding attributed to the
  wrong build is worse than no finding — the recorder's own history in `PLAYTEST_NOTES.md` is the
  cautionary tale.

### 4.4 The rule the recorder's own history argues for

`PLAYTEST_NOTES.md` records three defects in the recorder found by reading raw events rather than the
report, and draws the lesson: *"a log that has no word for something will report its absence as a
fact."* One session was reported as having no melee because melee was not recorded at all.

That lesson applies with more force once the data arrives over a network from somebody you cannot ask.
Two things follow, and they are cheap:

1. **Check the first uploaded recording against its raw events by hand**, exactly as was done locally.
   A silent uploader that drops the last flush would produce plausible, wrong reports forever.
2. **Make the uploader's own success visible to the developer, not the player.** A count of
   recordings received, compared against the number of testers who said they played, is the only
   thing that catches silent loss.

---

## 5. The recommended sequence

Nothing here needs new packages or a new subsystem, which is the point.

1. **Keep the game free and the page Restricted**, with the password in the invite link. Treat every
   build pushed as permanent — the licence granted to anyone who downloads it is perpetual and
   survives you deleting the page.
2. **Write the tester instructions around the itch.io app**, not the browser download, because
   SmartScreen's default answer is "don't run" and a lost tester is 20% of the sample.
3. **Decide the sensitive-content checkbox deliberately**, once, rather than leaving it at its
   default. It costs one click and getting it wrong is one of the few things itch.io says it will
   disable payments over.
4. **Add two settings-menu buttons: reveal the recordings folder, and copy the review to clipboard.**
   This is the smallest change that closes the data gap, and it transmits nothing.
5. **Wire `butler push` into `publish.ps1` after the existing gates**, with `--userversion` from the
   assembly version so a recording can be traced to a build.
6. **Run the playtest. Read the recordings by hand the first time.**
7. Only if step 4 loses too much data: build the rung-1 uploader, default off, with a consent toggle
   and a short privacy note linked from the page.
8. Revisit money, classification and code signing at iteration 21's slice lock, not before.

The reason to stop at step 6 and re-read the notes is that this whole document is in service of one
sentence in the production plan: *"By iteration 14, a stranger should play three runs in a row
without being asked to."* Distribution and telemetry are how that sentence gets tested. They are not
themselves progress.

---

## 6. Sources

itch.io primary documentation:
[Terms of Service](https://itch.io/docs/legal/terms) ·
[Privacy Policy](https://itch.io/docs/legal/privacy-policy) ·
[content creator quality guidelines](https://itch.io/docs/creators/quality-guidelines) ·
[butler — pushing builds](https://itch.io/docs/butler/pushing.html) ·
[access control](https://itch.io/docs/creators/access-control) ·
[limited playtests & releases](https://itch.io/docs/creators/limited-releases) ·
[payments](https://itch.io/docs/creators/payments) ·
[creator FAQ](https://itch.io/docs/creators/faq) ·
[getting started](https://itch.io/docs/creators/getting-started) ·
[seller/tax update](https://itch.io/updates/updates-to-itchio-seller-accounts-payouts-tax-interview) ·
[analytics update](https://itch.io/updates/updates-to-project-analytics-filtering-collections-impressions-and-more)

Microsoft primary documentation:
[SmartScreen reputation](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/smartscreen-reputation) ·
[code signing options](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/code-signing-options)

Cloudflare primary documentation:
[Workers limits](https://developers.cloudflare.com/workers/platform/limits/) ·
[Workers pricing](https://developers.cloudflare.com/workers/platform/pricing/) ·
[KV pricing](https://developers.cloudflare.com/kv/platform/pricing/)

Data protection and vendor policy:
[GameAnalytics developer policy](https://www.gameanalytics.com/trust/privacy-faq) ·
[Usercentrics on legitimate interest](https://usercentrics.com/knowledge-hub/gdpr-legitimate-interest/) ·
[Bugnet on crash reporting and GDPR](https://bugnet.io/blog/crash-reporting-gdpr-indie-games) ·
[Lou's Lagoon privacy policy (worked example)](https://megabit-publishing.com/policies/louslagoon-privacy-policy) ·
[Nano Reality privacy policy (retention table)](https://nanoreality.com/privacy) ·
[Stripe on W-8BEN](https://stripe.com/resources/more/w-8-ben-tax-form)

Telemetry tooling:
[Aptabase](https://aptabase.com/) · [Aptabase source](https://github.com/aptabase/aptabase) ·
[Aptabase NuGet](https://www.nuget.org/profiles/aptabase)

Secondary reporting, used only where labelled:
[SmartScreen cost to indies](https://senticmoney.com/blog/smartscreen-cost-indie-developer) ·
[SmartScreen and indie games](https://assethoard.com/blog/windows-protected-your-pc-indie-game) ·
[itch.io forum: closed beta](https://itch.io/t/54982/how-can-i-host-a-closed-beta-for-my-game) ·
[itch.io forum: sharing restricted games](https://itch.io/t/264418/sharing-restricted-games) ·
[itch.io forum: what analytics itch can provide](https://itch.io/t/5281744/adding-actual-analytics-on-itch-for-our-own-releases) ·
[itch.io forum: payout worked example](https://itch.io/t/2604718/any-worked-examples-of-end-to-end-payout-fees)
