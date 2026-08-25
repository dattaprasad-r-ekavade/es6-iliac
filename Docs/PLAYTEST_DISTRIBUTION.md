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

**As of May 2026 butler also has a GUI inside the itch app** (v26.12.0+): an Upload section with a
Builds page listing every build's project, channel, version, status and size, a push dialog with
channel creation, and the same change-preview before uploading. The bundled butler self-updates and
uses the logged-in account's credentials
([changelog](https://itch.io/updates/pushing-builds-with-butler-is-now-in-the-itch-app)). Worth
knowing it exists, but the CLI is still the right call here because the push should be scripted
behind the publish gates rather than done by hand.

One detail from the same release is directly useful: **games launched through the itch app get
`ITCHIO_APP=1` in their environment.** Since §2.4 argues for routing testers through the app, this is
a free way to find out whether they actually did — one environment-variable read, recordable as a
field on the recording, and it answers "did the tester see the scary dialog" without asking them.

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

**Revoking a key is access control, not recall.** It stops future access to the page; it does nothing
about a build already on someone's disk, and §3.2 explains why that is permanent by contract as well
as by physics. Worth internalising before the first push rather than after.

**And you cannot email your testers through itch.io.** Two documented facts combine into an
operational constraint that is easy to miss: "by default, if someone downloads your project for free
they do not become an owner" ([pricing docs](https://itch.io/docs/creators/pricing)), and the
mass-email tool is gated on having made a sale — "You won't be able to write an email before you make
your first sale" ([interact docs](https://itch.io/docs/creators/interact)). So a free playtest
generates no owner records and no way to contact the people who downloaded. **The feedback channel has
to be yours** — Discord, email, whatever — and the invitation has to carry it, because itch.io will
not let you follow up. That is an argument for inviting named people rather than posting a link.

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
is no longer justified". So a certificate does not solve the tester's first-run experience.

**But there is a second effect of not signing, and for iterative playtesting it is the worse one.**
Microsoft: *"When a file is not signed, SmartScreen reputation must build for each new version of your
files, starting with zero reputation. Reputation cannot transfer from previous versions unless both
were signed using the same publisher identity."*

**Every unsigned build starts from zero.** Build twelve is exactly as unknown as build one. Since the
whole reason to use butler and wharf is to push builds often, unsigned iteration means the warning
never gets less scary no matter how many clean installs accumulate. That is the real argument for
signing — not the first impression, which no certificate fixes, but stopping the per-build reset.

Whether that is worth buying depends on **where the developer is**, which is the one place location
changes the answer materially. Azure Artifact Signing is about $9.99/month with no hardware token and
CI integration, but eligibility is restricted: organisations in the US, Canada, EU and UK, and
**individual developers in the US and Canada only**. A solo developer in the UK or EU without a
registered company is not eligible and would need a traditional OV certificate instead, at roughly
$150–300/year and, since June 2023, with the private key on a hardware token or HSM
([code-signing-options](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/code-signing-options)).

**Two harder failure modes, both worth knowing before blaming the game.**

**Windows 11 Smart App Control does not warn, it blocks.** Microsoft: *"Smart App Control will block
execution of unsigned files unless the file has a positive reputation. Smart App Control signature
checks apply to all executable files, not just those downloaded from the Internet."* It is only active
on eligible clean-installed Windows 11 machines, so it will affect some testers and not others — and
the ones it affects cannot run the build at all. A tester reporting "it doesn't start" may not have a
bug to report.

**Defender false positives on .NET publishes are documented and unfixable from our side.** The
`dotnet/runtime` tracker has long-running reports of published .NET apps being *quarantined*, not
merely warned: `Behavior:Win32/DefenseEvasion.UM!ml` on single-file self-contained builds
([issue 46312](https://github.com/dotnet/runtime/issues/46312)), `Trojan:Script/Wacatac.B!ml` on
trimmed builds ([issue 33745](https://github.com/dotnet/runtime/issues/33745)), and a *blank* Native
AOT project flagged as a trojan where the equivalent C++ project was not
([issue 105959](https://github.com/dotnet/runtime/issues/105959)). Microsoft's own position is that it
cannot be fixed: *"Even if we had access to the AV we probably couldn't figure out why one binary gets
flagged and another binary is fine… I don't think there's anything we can reasonably do to make all
AVs happy"* ([issue 118300](https://github.com/dotnet/runtime/issues/118300)).

**Good news on this one, and it is worth not breaking.** Those reports cluster on **single-file**,
**trimmed** and **Native AOT** publishing. `publish.ps1` does none of those — it is a plain
`--self-contained` folder publish, with no `PublishSingleFile`, no `PublishTrimmed`, no `PublishAot`,
and `PublishReadyToRun` explicitly off. That is the least-affected configuration. If anyone is ever
tempted to shrink the 131 MB folder with trimming or single-file, this is the cost to weigh against
it — and either way the actual artifact should be run once on a machine that is not the dev box, since
this is empirical rather than predictable.

**The mitigation that costs nothing: route testers through the itch.io desktop app.** Installing via
the itch app — like the Microsoft Store or Steam — avoids the browser-download dialog. itch.io's own
founder put it plainly in 2017: *"If the game is installed through our app then no signing is
necessary. If you plan to have people download the game directly from their browser, then it will get
flagged for a warning"*
([forum](https://itch.io/t/92828/does-a-game-need-to-be-digitally-signed)). That is a first-party
statement but a nine-year-old one, and itch.io has never documented the mechanism, so treat it as
very likely rather than guaranteed — which is another reason the `ITCHIO_APP=1` check from §2.2 is
worth having: it tells you which route each tester actually took.

**Concrete recommendation, in priority order.** The tester instructions should lead with the itch app,
not the download button. Second, **tell testers in advance exactly what they might see** — this is
Microsoft's own advice for beta users, and for a handful of named people it is probably the
highest-value action available, because a forewarned tester clicks through and an ambushed one gives
up. Signing is a decision for a long series of builds going to a growing audience, not for a
five-person round where every drop-out is a fifth of the data.

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

A date worth noticing first: **the contract has not been revised since April 2023, while the privacy
policy was last modified in March 2026.** So the terms predate everything in §3.6 below — the platform
went through a payment-processor crisis in 2025 without the agreement changing. Since §18 lets itch.io
amend "at any time and without notice", that is a description of the current text rather than a
guarantee about next month's.

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

**The general compliance hook.** The same §3 list ends with "Violating any applicable laws or
regulations." Your own legal compliance is therefore imported into the contract as a term you can be
terminated for breaching — which is how the data-protection obligations in §3.4 stop being purely a
matter between you and a regulator and become a matter between you and itch.io as well.

**A separate, harsher grant on your own words** (§5). Anything you write as a *user* — comments,
reviews, devlogs, profile — is licensed to itch.io "worldwide, non-exclusive, **perpetual**, royalty
free… to use, reproduce, create derivative works, display, perform and distribute", with no
removal-terminates-it carve-out of the kind §4 gives Publisher content. Minor, but it means the
comment thread on a Restricted playtest page is not something you can take back either.

**You do keep ownership.** §4: "Publishers retain all ownership rights to the submitted content", and
the [Creator FAQ](https://itch.io/docs/creators/faq) restates it — "Does itch.io take ownership of the
content I upload? No… itch.io just asks for the minimum amount of rights necessary to run the site" —
and confirms there is no DRM: "itch.io lets users download the games exactly as you uploaded them. No
modifications are made to the files you upload."

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

**There is exactly one other mandatory disclosure, and it has a carve-out that matters here.** The
[quality guidelines](https://itch.io/docs/creators/quality-guidelines) require the **AI Disclosure**
section to be filled in if the project "contains materials produced by generative AI". The carve-out
is explicit and lands squarely on this game:

> "NPC pathfinding, enemy behavior patterns, **procedural level generation**, fuzzy logic systems, and
> dynamic difficulty adjustment, dynamic music, etc. are not considered generative AI and don't need
> tagging."

So the mine generator, the enemy pursuit behaviour and the archer's withdraw intent are all outside
the requirement — a generated roguelike does not need an AI tag for being generated. What *would*
trigger it is any shipped art, texture, music or written copy that came out of a generative model.
Enforcement is currently described as strict for asset pages specifically ("Failure to tag your asset
page may result in delisting") rather than for games, but the requirement is stated for both.

Two other quality-guideline provisions worth knowing, since both carry delisting or ban language:
projects containing "malware, spyware or adware" are prohibited outright, and pages built around
"shocking the viewer by showing potentially offensive images, flashing colors, loud or annoying
noises" may be delisted from browse and search. Neither describes this game, but the first one comes
back in §3.4.

### 3.4 Telemetry: the part that actually has legal weight

**itch.io's own position first, because it exists and is easy to miss.** There is no telemetry
disclosure requirement in the terms, the quality guidelines or the privacy policy — no data-safety
form of the kind Google Play has, no privacy-policy field on a project page. But the founder did
address it once, on the forums in 2018:

> "if your game has any sort of information collection at all, then you must provide a consent form.
> As it stands now, itch.io will not ask for consent on behalf of individual creators… this means that
> if you have any kind of sign in or analytics collection that includes personally identifiable
> information in your game (like username, email, ip address), then you must show your own dialog
> within the game."

([forum](https://itch.io/t/222345/gdpr-and-itchio-sellers-questions)) That is first-party but informal
and eight years old, it never made it into the documentation, and the "we may set up something to
streamline this" it promises never shipped. Treat it as itch.io's stated expectation rather than an
enforceable published policy — but note that it lands on exactly the recommendation the law reaches
independently below, and that it names IP address as the thing that makes an in-game consent dialog
necessary.

Two adjacent provisions sharpen it. The quality guidelines prohibit "malware, spyware or adware"
outright, and separately say: *"Please avoid putting third-party login walls in front of your game
unless necessary… **Avoid doing it for the sake of collecting personal information about a player**, or
trying to redirect traffic to a third-party service."* Neither is a disclosure rule and neither
describes a consented, identifier-free recorder. But "spyware" is undefined, and an undisclosed
uploader that silently exfiltrates data is closer to that word than anyone would want to argue in a
support ticket. Ask first, and there is nothing to argue about.

**Now the law, which is the part that actually binds.** If the game sends anything from a player's
machine, data-protection law is engaged for players in the EU/UK — independently of what itch.io asks
for, and, via the "violating any applicable laws or regulations" hook in §3.2, as a contract term too.
The practical shape of compliance, as consistently described across the sources below:

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

### 3.6 Platform risk: what 2025 demonstrated

No obligation for this game comes out of this, but it is the clearest available evidence about how the
platform behaves under pressure, and it argues for one concrete habit.

**What happened.** In April 2025 itch.io banned a game called *No Mercy*; the campaign group Collective
Shout then took the matter to itch.io's and Steam's payment processors rather than to the platforms.
On **24 July 2025** itch.io deindexed **all** adult NSFW content from browse and search with no
advance notice: *"Recently, we came under scrutiny from our payment processors… The situation developed
rapidly, and we had to act urgently to protect the platform's core payment infrastructure"*
([itch.io](https://itch.io/updates/update-on-nsfw-content)). Payment with Stripe for 18+ content was
suspended. On **31 July 2025** free adult content was re-indexed on condition that the project has "No
Payments" set, with Stripe's position relayed as being unable to support "content designed for sexual
gratification" ([itch.io](https://itch.io/t/5149036/reindexing-adult-nsfw-content)). Reporting puts the
number of delisted pages around 20,000, though itch.io never published a figure. A separate and
unrelated July 2025 change added geographic restrictions for UK users under the Online Safety Act,
again keyed to the adult flag plus specific tags
([itch.io](https://itch.io/t/5133739/our-update-on-the-uk-online-safety-act)).

**Direct relevance to a non-adult game: none.** Every mechanism in the story is keyed to the
sensitive-content flag combined with adult tags. The prohibited-themes list is entirely sexual
content. The UK restriction triggers on the same flag plus "porn", "adult", "hentai", "erotic", "sex".
The payment-processor hook in ToS §7 is conditioned on distributing for money, which a free playtest
is not.

**The generalisable lesson is one habit.** itch.io acted platform-wide, retroactively, and without
notice, and said so — *"it was not realistic to provide creators with advance notice"* — and the ToS
independently permits both amendment (§18) and termination (§3, §4) without notice. The founder's own
advice in the same thread is the right response, and it costs nothing: keep your own copies and do not
treat the platform as your archive. For this project that means **the build folder, the recordings, and
the tester correspondence live somewhere you control**, with itch.io as a distribution channel rather
than a system of record. Which is also why §2.3's point about the feedback channel having to be yours
matters more than it first appears.

One thing worth noting as an absence: itch.io has published no policy follow-up since July 2025 — every
blog post in 2026 is a feature changelog — and the promised "content warnings" system and additional
payment processors are still undocumented. So the July 2025 arrangement appears to have simply
persisted. That is a statement about the platform's communication, not about risk to this game.

### 3.7 The licences already in the build

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

### 3.8 What is still open

Answered above from primary sources: the ToS obligations and licence grants, the absence of an age
rating, the AI-disclosure carve-out for procedural generation, the fee and withholding model, what
itch.io does and does not know about downloaders, and the 2025 platform episode.

**Confirmed absences**, which are answers rather than gaps. The itch.io docs' Legal section contains
exactly three documents — Terms of Service, Privacy Policy, Cookie Policy. There is **no Data
Processing Agreement**, no separate creator agreement, and no controller/joint-controller language
anywhere; the words "controller", "joint controller", "data processing agreement" and "GDPR" do not
appear in the privacy policy at all, and the Creator FAQ's "Learn more about GDPR" link points at
Wikipedia. There is likewise **no requirement to disclose in-game telemetry and no privacy-policy
field** on a project page. Doing both anyway is the right call, but it is our decision rather than a
platform demand. A 2018 forum promise of an "itch.io publishers guide to GDPR" was never delivered.

Genuinely unresolved:

- **Whether itch.io would supply a DPA on request.** Nothing published either offers or refuses one.
  Only matters if itch.io ever passes personal data to the developer, which on the evidence it does
  not. A support email would settle it.
- **Whether §3's "collecting information about others" is intended to reach a game's own telemetry.**
  Almost certainly not — thousands of itch.io games ship analytics — but the clause is unqualified and
  the penalty is termination without notice. Worth asking before an uploader ships, not after.
- **Whether the itch app still bypasses SmartScreen.** The only source is a 2017 founder statement and
  itch.io has never documented the mechanism. Since the whole tester-onboarding recommendation rests
  on it, this is the single most worthwhile thing to verify empirically — install one build through
  the app on a clean machine and watch.
- **Trading-name and contact-details disclosure**, which varies by jurisdiction and bites the moment
  a privacy policy names a data controller — publishing a policy means publishing a contactable
  identity, and for a solo developer that is a real decision about what address to put on the
  internet.
- **The developer's own jurisdiction.** Everything above is written for a US or UK/EU developer. The
  withholding rate, VAT treatment, whether GDPR applies at all, and whether Azure Artifact Signing is
  even available all turn on it.
- **Anything involving actual money.** The moment there is revenue, the tax interview, TIN, treaty
  rate and local return are a matter for an accountant, not for this document.

---

## 4. Part three — getting the telemetry back

This section is the decision. The options behind it — every hosting free tier, every analytics vendor,
the .NET client details, and a consolidated free-tier table — are surveyed with sources in
[`TELEMETRY_RETURN_RESEARCH.md`](TELEMETRY_RETURN_RESEARCH.md). Read that when choosing; read this
when deciding whether to choose at all.

One number frames the whole thing: **thirty testers at five sessions each is 150 files and about
50,000 events, once.** Every free tier surveyed clears that by three to five orders of magnitude. So
capacity is not a criterion, and anything that reads like a capacity argument is a distraction —
the criteria are engineering cost and compliance surface.

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

- A **"reveal recordings folder"** button in settings — `explorer.exe /select,<newest file>`, which
  opens the folder *and* highlights the file. It converts "find AppData" into "click this", and
  `%APPDATA%` being hidden by default is the single biggest failure mode here.
- A **"copy summary to clipboard"** action that runs the existing `PlayReview` in-process and puts
  the report text on the clipboard. The tester pastes it into Discord. This is a genuinely good fit,
  because the thing being asked for is *tens of numbers*, not a file — and `PlayReview.Verdict`
  already produces the one-line conclusion.

For a five-person first pass, rung 0 plus the clipboard button is very likely the right answer, and
it has no legal surface at all: nothing is transmitted, so no lawful basis, no privacy policy, no IP
address. **Start here.**

One more free thing that belongs at this rung: **issue a named download key per tester** (§2.3). It
takes minutes and it distinguishes *"didn't play"* from *"played but the data never arrived"* — the
ambiguity that quietly ruins a small-sample playtest, because the two look identical from here and
imply opposite next actions.

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

**R2 is the right pick, and the reason is not capacity.** A Worker that does
`env.BUCKET.put(uuid, body)` stores **the exact JSON `PlayRecording.TryLoad` already parses**. Download
the bucket, point `RatnaBay.Tools review` at the folder, and everything `PlayReview` knows about forced
camps and re-advertised doors keeps working. No schema, no mapping, no dashboard to learn. It is
roughly fifteen lines of JavaScript and sixty of C#.

**Rung 2 — a hosted analytics product.** Worth knowing about; still not the answer here.

[`TELEMETRY_RETURN_RESEARCH.md`](TELEMETRY_RETURN_RESEARCH.md) surveys these properly, with verified
free tiers for every provider and a consolidated table at the end. The short version:

- **PostHog** is the strongest of them, and stronger than I first assumed: an **official MIT-licensed
  .NET SDK that works in a plain console app**, 1M events/month free, EU (Frankfurt) hosting, and the
  library explicitly disregards the server-side IP. If a dashboard were wanted without building one,
  this is the one to pick.
- **Aptabase** is the closest fit in spirit — desktop-first, privacy-first, EU residency, a DPA already
  in its terms, self-hostable — and the worst fit technically, because the only .NET SDK is
  `Aptabase.Maui`, which a MonoGame process cannot use, and the published package is 0.1.0 from
  September 2024.
- **GameAnalytics** is usable through its Collection API v2 if you write the client yourself, but its
  500-events-per-active-user-per-day ceiling would bind on raw event timelines.
- **Microsoft App Center is dead twice over** — retired March 2025, and its Analytics extension expired
  June 2026. Ignore any guide that recommends it.

The decisive objection is not cost or compliance, it is fit. These products aggregate across people.
This project needs *one specific derived number per door* — hesitation in seconds, classified reflex /
quick / weighed, with forced camps excluded — and `PlayReview` already computes it, with tests
asserting the classification and a documented history of the recorder lying before those tests
existed. Reshaping the event timeline to fit a generic product-analytics model risks losing exactly the
nuance that was expensive to get right.

**Recommendation: rung 0 now, rung 1 (Worker + R2) when asking gets lossy, rung 2 only if somebody
wants a dashboard more than they want the answer.**

### 4.3 If rung 1 gets built, the implementation notes

Inherit `PlayRecorder`'s posture, and add:

- **One static `HttpClient`** for the process. Never `using var` a new one per call — that exhausts
  sockets.
- **A short timeout, and treat every failure as success.** 5 seconds, catch everything, set a broken
  flag, move on. The game must not care whether the upload worked.
- **Never on the game thread, and never awaited during a frame.**
- **Do not upload on process exit.** `AppDomain.CurrentDomain.ProcessExit` looks like the obvious
  hook and is a trap. .NET Framework capped all exit handlers at two seconds; that cap **does not
  exist in .NET Core and .NET 5+**, so a network call there can hang the process on exit indefinitely
  — a much worse bug than a lost upload. It also does not run on a kill, a power-off, or many crash
  paths, which are precisely the sessions worth having. **Upload during play instead**, at the natural
  pauses the recorder already marks: when a decision is answered, and when `RunEnded` fires. By the
  time the tester alt-F4s, the data has already gone.
- **Queue offline, using the disk you already write to.** On launch, list `play_*.json` in
  `PlayRecorder.Directory`, POST anything without an `.uploaded` sidecar marker, write the marker on
  2xx, leave it alone on anything else. That is offline support, retry across restarts and crash
  resilience in about twenty lines, with no in-memory queue and no second persistence format —
  and `PlayRecorder.Newest()` already demonstrates the directory-listing pattern to copy. Never
  delete an uploaded file; the tester may still want to send it by hand and the disk cost is nil.
- **Barely retry.** If a POST fails the file stays on disk and gets retried next launch, which is a
  better backoff than any in-process loop. If you want in-session retry, two attempts a few seconds
  apart, only for transient conditions, and never for a 4xx other than 429 — a 4xx means the payload
  is wrong and retrying makes it wrong repeatedly.
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
2. **Set up your own feedback channel and put it in the invitation**, and **issue one named download
   key per tester.** itch.io cannot email people who downloaded a free project, so without the channel
   there is no way to follow up; without the keys you cannot tell "didn't play" from "played but no
   data arrived".
3. **Write the tester instructions around the itch.io app**, not the browser download, and **tell
   testers in advance what Windows might say.** SmartScreen's default answer is "don't run", Smart App
   Control on Windows 11 may refuse outright, and a lost tester is 20% of the sample.
4. **Decide the sensitive-content checkbox deliberately**, once, rather than leaving it at its
   default. It costs one click and getting it wrong is one of the few things itch.io says it will
   disable payments over. The AI-disclosure section needs no tag for procedural generation.
5. **Add two settings-menu buttons: reveal the recordings folder, and copy the review to clipboard.**
   This is the smallest change that closes the data gap, and it transmits nothing. While in there,
   record `ITCHIO_APP` so you know which route each tester took.
6. **Wire `butler push` into `publish.ps1` after the existing gates**, with `--userversion` from the
   assembly version so a recording can be traced to a build. Keep the build folder and the recordings
   somewhere you control; itch.io is a channel, not an archive.
7. **Run one build end to end on a machine that is not the dev box** before sending it to anybody —
   that is the only way to find out whether Defender takes exception to the publish.
8. **Run the playtest. Read the recordings by hand the first time.**
9. Only if step 5 loses too much data: build the rung-1 uploader — a Cloudflare Worker writing whole
   recordings to R2, the recordings folder as the queue — default off, with a consent toggle and a
   short privacy note linked from the page. Upload at decision points, never on process exit.
10. Revisit money, classification and code signing at iteration 21's slice lock, not before. Signing
    earns its cost when builds go out often to a growing audience, because unsigned reputation resets
    every build — not for one round with five named people.

The reason to stop at step 8 and re-read the notes is that this whole document is in service of one
sentence in the production plan: *"By iteration 14, a stranger should play three runs in a row
without being asked to."* Distribution and telemetry are how that sentence gets tested. They are not
themselves progress.

---

## 6. Sources

itch.io primary documentation:
[Terms of Service](https://itch.io/docs/legal/terms) ·
[Privacy Policy](https://itch.io/docs/legal/privacy-policy) ·
[Cookie Policy](https://itch.io/docs/legal/cookie-policy) ·
[content creator quality guidelines](https://itch.io/docs/creators/quality-guidelines) ·
[butler — pushing builds](https://itch.io/docs/butler/pushing.html) ·
[butler in the itch app](https://itch.io/updates/pushing-builds-with-butler-is-now-in-the-itch-app) ·
[access control](https://itch.io/docs/creators/access-control) ·
[pricing](https://itch.io/docs/creators/pricing) ·
[interact](https://itch.io/docs/creators/interact) ·
[getting indexed](https://itch.io/docs/creators/getting-indexed) ·
[limited playtests & releases](https://itch.io/docs/creators/limited-releases) ·
[payments](https://itch.io/docs/creators/payments) ·
[creator FAQ](https://itch.io/docs/creators/faq) ·
[getting started](https://itch.io/docs/creators/getting-started) ·
[seller/tax update](https://itch.io/updates/updates-to-itchio-seller-accounts-payouts-tax-interview) ·
[analytics update](https://itch.io/updates/updates-to-project-analytics-filtering-collections-impressions-and-more)

itch.io policy statements on the 2025 payment-processor episode (first-party, posted rather than
documented):
[update on NSFW content](https://itch.io/updates/update-on-nsfw-content) ·
[reindexing adult NSFW content](https://itch.io/t/5149036/reindexing-adult-nsfw-content) ·
[UK Online Safety Act](https://itch.io/t/5133739/our-update-on-the-uk-online-safety-act) ·
[on in-game consent, 2018](https://itch.io/t/222345/gdpr-and-itchio-sellers-questions) ·
[on code signing, 2017](https://itch.io/t/92828/does-a-game-need-to-be-digitally-signed)

Microsoft primary documentation:
[SmartScreen reputation](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/smartscreen-reputation) ·
[code signing options](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/code-signing-options)

.NET antivirus false positives (`dotnet/runtime`):
[#46312 single-file quarantine](https://github.com/dotnet/runtime/issues/46312) ·
[#33745 trimmed builds](https://github.com/dotnet/runtime/issues/33745) ·
[#105959 Native AOT](https://github.com/dotnet/runtime/issues/105959) ·
[#118300 Microsoft's position](https://github.com/dotnet/runtime/issues/118300)

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
