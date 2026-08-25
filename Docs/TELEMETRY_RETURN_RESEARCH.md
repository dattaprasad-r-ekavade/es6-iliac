# Getting playtest telemetry back from external testers

Research notes, 25 August 2026. Every pricing figure and API detail below carries the URL it came
from. Where something could not be verified against a primary source it is labelled **UNVERIFIED**.

## The question this has to answer

RatnaBay already writes `%APPDATA%\RatnaBay\recordings\play_<timestamp>.json` per sitting
(`src/RatnaBay.Game/Session/PlayRecorder.cs`), and `PlayReview` already turns those files into a
`Hesitation` number per decision (`src/RatnaBay.Domain/Telemetry/PlayRecording.cs`). The only thing
missing is transport: getting 3–30 testers' files onto the developer's machine.

Scale matters here and it is worth stating plainly. Thirty testers doing five sessions each is
**150 files**. A 6-minute session at the current event rate is a few hundred events, so a whole
playtest round is on the order of **50,000 events and a handful of megabytes, once**. Every free
tier below is oversized by three to five orders of magnitude for this. That means the ranking
criterion is not capacity or price — it is *how much engineering and how much compliance surface*
each option costs.

---

## 1. The simplest options first

### 1.1 Just ask for the file (zero engineering)

The tester zips `%APPDATA%\RatnaBay\recordings\` and sends it over Discord or email.

**What actually goes wrong:**

- **Finding the folder.** `%APPDATA%` is hidden by default in Windows Explorer. Non-technical
  testers will not know that pasting `%APPDATA%\RatnaBay\recordings` into the Explorer address bar
  or the Run dialog works. Instructions that say "navigate to AppData" fail more often than they
  succeed.
- **Forgetting.** The moment the game closes, sending the file is a chore competing with everything
  else in the tester's evening. Response rates on "please also email me a file" are poor and there is
  no way to tell "didn't play" from "played and forgot".
- **Sending the wrong thing.** Testers send one file when they played five sessions, or send the
  newest file when the interesting run was the third one.
- **Silence looks like absence.** This is already a hazard the codebase has been bitten by — the
  `MeleeSwing` comment in `PlayEventKind` notes exactly this failure mode. A missing upload and a
  tester who never reached a decision point produce the same empty dataset.
- **Discord mangles things.** Discord will accept a `.json` file, but some testers will paste the
  contents into a message, which truncates at 2,000 characters. A 6-minute session's JSON is far
  larger than that.

**Mitigations that are worth the small amount of code:**

1. **An in-game "Open recordings folder" button.** On Windows this is
   `Process.Start("explorer.exe", $"/select,\"{path}\"")` — one line, opens Explorer with the newest
   file already highlighted. This removes the single biggest failure mode. It should live somewhere a
   tester will find it, not behind a debug key combination.
2. **Write next to the executable instead of (or as well as) `%APPDATA%`.** For a playtest build
   specifically, a `recordings/` folder beside `RatnaBay.exe` is trivially findable: the tester
   already knows where they unzipped the game. The usual objection — Program Files is not writable —
   does not apply to an itch.io zip that testers extract to their Desktop or Downloads. Doing this
   *only* in a `PLAYTEST` build configuration keeps the shipping build's behaviour clean. Given
   `PlayRecorder.Directory` is a single static property, this is a config switch, not a refactor.
3. **A "copy report to clipboard" button.** The companion CLI in `tools/RatnaBay.Tools` already
   produces a compact text report. If the game can produce the same summary and put it on the
   clipboard via `System.Windows.Forms.Clipboard` or a small P/Invoke, the tester pastes it straight
   into Discord. This throws away the raw events but keeps the `Hesitation` numbers — which is the
   entire question. It is by far the highest ratio of answer-obtained to code-written.
4. **A short shareable code.** Tempting, but do the arithmetic before building it. The decision
   summary for one session is maybe 5–10 decisions × (rooms, pending, nextPays, health, hesitation,
   pressedOn). Even packed tightly and Base64'd that is 100+ characters — too long to read over
   voice, fine to paste. At that point it is a worse version of "copy report to clipboard" with extra
   encode/decode code and a versioning problem. **Not recommended.**
5. **Zip the folder from inside the game.** `System.IO.Compression.ZipFile.CreateFromDirectory` into
   `%USERPROFILE%\Desktop\ratnabay-playtest-<date>.zip`, then reveal it. Removes the "which files?"
   ambiguity entirely.

**Verdict:** the manual route with mitigations 1 and 3 costs perhaps an hour of work and answers the
question for a 3–10 person test. It scales badly past that, mostly on nagging cost rather than
technical cost.

---

### 1.2 A single HTTP POST to an endpoint you run

The game POSTs the JSON at end of session. This is the right answer above ~10 testers because it
converts "will the tester remember" into "did the machine have network access".

What you are building is: *accept a JSON body, append it to durable storage, return 200*. That is
about 15 lines of server code. The hosting options below are ranked by how little of your attention
they consume.

#### Cloudflare Workers + R2 — the best fit

A Worker that does `env.BUCKET.put(crypto.randomUUID() + '.json', request.body)` and returns 204.
Deploy with `npx wrangler deploy`. No build step, no framework, no container.

| Resource | Workers Free plan | Source |
|---|---|---|
| Worker requests | 100,000 / day, resets 00:00 UTC (error 1027 past that) | [Workers limits](https://developers.cloudflare.com/workers/platform/limits/) |
| CPU time | 10 ms per invocation | [Workers pricing](https://developers.cloudflare.com/workers/platform/pricing/) |
| Memory | 128 MB | [Workers limits](https://developers.cloudflare.com/workers/platform/limits/) |
| Worker script size | 3 MB | [Workers limits](https://developers.cloudflare.com/workers/platform/limits/) |
| R2 storage | 10 GB-month | [R2 pricing](https://developers.cloudflare.com/r2/pricing/) |
| R2 Class A ops (PUT/POST/LIST) | 1,000,000 / month | [R2 pricing](https://developers.cloudflare.com/r2/pricing/) |
| R2 Class B ops (GET/HEAD) | 10,000,000 / month | [R2 pricing](https://developers.cloudflare.com/r2/pricing/) |
| R2 egress | Free (Standard storage) | [R2 pricing](https://developers.cloudflare.com/r2/pricing/) |

The R2 free tier applies to Standard storage only, not Infrequent Access
([source](https://developers.cloudflare.com/r2/pricing/)). 150 uploads against a 1,000,000/month
Class A allowance is 0.015% of the quota.

**Storage alternatives on the same platform, and why R2 wins here:**

- **Workers KV free tier is 1,000 writes per day** and 100,000 reads per day, 1 GB stored
  ([KV pricing](https://developers.cloudflare.com/kv/platform/pricing/)). Fine for this volume, but
  KV is a key-value store with eventual consistency and it is the wrong shape for "append blobs".
- **D1 free tier is 5,000,000 rows read/day and 100,000 rows written/day**, 5 GB total account
  storage, **500 MB max per database on Free**, and only **50 queries per Worker invocation on Free**
  ([D1 pricing](https://developers.cloudflare.com/d1/platform/pricing/),
  [D1 limits](https://developers.cloudflare.com/d1/platform/limits/)). D1 is genuinely attractive if
  you want to insert one row per *event* and query with SQL afterwards — but note that inserting
  ~300 events as 300 rows in one request would blow through the 50-queries-per-invocation Free limit
  unless you batch. Store the blob in R2; if you later want SQL, load the blobs locally into SQLite.
- **Rate limiting** is available via the `ratelimits` binding. The window (`period`) must be either
  10 or 60 seconds, and the Cloudflare docs describe the limit as the tokens allowed in a given period
  "in a single Cloudflare location" — i.e. **per PoP, not globally**
  ([Rate Limiting API](https://developers.cloudflare.com/workers/runtime-apis/bindings/rate-limit/)).
  That is exactly right for abuse prevention and wrong for accounting, which is the correct trade
  here. The Cloudflare docs page does not state plan availability; a third-party write-up says it
  works on all plans including Free
  ([toolchew](https://toolchew.com/en/how-to-rate-limit-cloudflare-workers/)) — treat
  **Free-plan availability as UNVERIFIED** against a primary source.

#### Google Apps Script web app → Google Sheet or Drive

A `doPost(e)` function that appends `e.postData.contents` to a Sheet or writes a file to Drive,
deployed as a web app with "Anyone" access. Zero infrastructure, and the data lands somewhere the
developer can already read.

The important and frequently-misreported detail: **the published Apps Script quota table does not
contain an inbound-request-per-day limit for web apps.** The "URL Fetch calls — 20,000/day
(consumer) / 100,000/day (Workspace)" number that gets quoted for this is the quota for *outbound*
`UrlFetchApp` calls made *by* your script, not for requests arriving at your web app
([Quotas for Google Services](https://developers.google.com/apps-script/guides/services/quotas)).
The limits that actually bind a `doPost` handler are:

| Limit | Consumer (gmail.com) | Workspace | Source |
|---|---|---|---|
| Script runtime | 6 min / execution | 6 min / execution | [Apps Script quotas](https://developers.google.com/apps-script/guides/services/quotas) |
| Simultaneous executions per user | 30 | 30 | same |
| Simultaneous executions per script | 1,000 | 1,000 | same |
| Properties total storage | 500 KB per property store | 500 KB | same |
| URL Fetch POST size (outbound only) | 50 MB / call | 50 MB / call | same |

Google states these quotas "are subject to elimination, reduction, or change at any time, without
notice" ([source](https://developers.google.com/apps-script/guides/services/quotas)). For 150
requests this is a non-issue either way, but treat the absence of a documented inbound cap as
*undocumented*, not as *unlimited*.

Two real drawbacks: Apps Script web apps issue a 302 redirect to `script.googleusercontent.com` on
POST, so your HTTP client must follow redirects (`HttpClient` does by default, but it drops the body
on a 302→GET, so the *response* is not useful — fine for fire-and-forget). And you are writing
JavaScript in a browser editor with no version control, which is a worse developer experience than
`wrangler deploy` for the same amount of code.

#### Vercel Hobby

| Resource | Hobby included usage | Source |
|---|---|---|
| Function Invocations | first 1,000,000 | [Vercel Hobby plan](https://vercel.com/docs/plans/hobby) |
| Active CPU | 4 CPU-hrs | same |
| Provisioned Memory | 360 GB-hrs | same |
| Edge Requests | up to 1,000,000 | same |
| Runtime Logs retention | 1 hour | same |

**The blocker: "the Hobby plan restricts users to non-commercial, personal use only"**
([Vercel Hobby plan](https://vercel.com/docs/plans/hobby), citing the
[fair use guidelines](https://vercel.com/docs/limits/fair-use-guidelines#commercial-usage)). A
playtest for a game you intend to sell is arguably commercial. This is a judgement call the developer
has to make, but it is a real term and it makes Vercel a worse choice than Cloudflare for something
this trivial. Vercel also has no included durable storage in that table — you would still need Blob
or an external database.

#### Netlify Free

Netlify moved to credit-based pricing and **there is no longer a separate invocation limit for
functions**. The Free plan gets **300 credits/month as a hard limit** with no option to buy more, and
compute consumes **10 credits per GB-hour**
([how credits work](https://docs.netlify.com/manage/accounts-and-billing/billing/billing-for-credit-based-plans/how-credits-work/),
[credit-based pricing plans](https://docs.netlify.com/manage/accounts-and-billing/billing/billing-for-credit-based-plans/credit-based-pricing-plans/)).

The sharp edge: when credits run out, **all projects on the account are paused**, not just the
offending one, and on Free you cannot buy your way out — you wait for the next billing cycle
([billing FAQ](https://docs.netlify.com/manage/accounts-and-billing/billing/billing-for-credit-based-plans/billing-faq-for-credit-based-plans)).
150 short function invocations will not come close to 300 credits, but if the developer also hosts a
game website on the same Netlify account, a telemetry endpoint sharing that blast radius is an
unnecessary coupling.

#### Supabase

| Resource | Free plan | Source |
|---|---|---|
| Active projects | 2 | [Supabase pricing](https://supabase.com/pricing) |
| Database size | 500 MB per project | same |
| API requests | Unlimited | same |
| Egress | 5 GB | same |
| File storage | 1 GB | same |
| Edge Function invocations | 500,000 | [billing docs](https://supabase.com/docs/guides/platform/billing-on-supabase) |
| Pausing | **after 1 week of inactivity** | [Supabase pricing](https://supabase.com/pricing) |

The pausing behaviour is the thing that matters for a playtest. A Free project is paused if it
"does not receive sufficient user database activity over the past week"; Supabase says "typically a
few user requests to the database each day over the previous week is enough" to avoid it
([free project pausing](https://supabase.com/docs/guides/platform/free-project-pausing)). A playtest
round with a two-week gap in the middle will silently come back to a paused project and dropped
uploads. You can restore within 1 year, but the data from the gap is gone. Paid projects are never
paused.

#### A plain VPS

Cheapest current Hetzner Cloud x86 instance after the 15 June 2026 price adjustment is **CX23 at
€5.49/month excluding VAT** (was €3.99); the ARM **CAX11 is €5.99/month excl. VAT**, both in the
German/Finnish regions
([Hetzner price adjustment, 15 June 2026](https://docs.hetzner.com/general/infrastructure-and-availability/price-adjustment/)).
German list prices exclude 19% VAT.

For this workload a VPS is strictly worse than a Worker: you now own OS patching, TLS certificate
renewal, a firewall, and an uptime problem, in exchange for capacity you will use 0.01% of. The one
genuine argument for it is that you control the whole stack including whether an IP address is ever
written to disk — but you can achieve that on Workers too (see §4).

---

### 1.3 Using an existing form or database as the sink (no server code at all)

#### Google Forms

The **official Google Forms REST API cannot create responses.** The `forms.responses` resource
exposes only `get` and `list`
([Forms API reference](https://developers.google.com/forms/api/reference/rest/v1/forms.responses)).

The widely-used workaround is to POST `application/x-www-form-urlencoded` to the form's
`/formResponse` URL (swap `viewform` for `formResponse`) with `entry.<id>=value` pairs harvested from
a pre-filled link. This works and people rely on it
([Stack Overflow](https://stackoverflow.com/questions/71714110/can-you-submit-a-restful-request-to-a-google-forms-api),
[worked example](https://theconfuzedsourcecode.wordpress.com/2019/11/11/you-may-restfully-submit-to-your-google-forms/)),
but it is **entirely undocumented and unsupported**, so Google can break it without notice. A form
field also has a practical length ceiling that a full session JSON may exceed. **Not recommended as
the primary sink** — but it is a perfectly good *fallback*: a Google Form with one long-answer field
that testers paste the clipboard summary into, which pairs neatly with mitigation 3 above and needs
no code in the game at all.

#### Airtable

| Limit | Free plan | Source |
|---|---|---|
| Records per base | 1,000 (cumulative across all tables in the base) | [Airtable plans](https://support.airtable.com/docs/en/airtable-plans) |
| API calls per workspace per month | **1,000** | same |
| API rate limit | 5 requests/sec per base (all plans) | [managing API call limits](https://support.airtable.com/v1/docs/managing-api-call-limits-in-airtable) |
| API batch size | max 10 records per request | [workspace settings overview](https://support.airtable.com/docs/workspace-settings-page-overview) |
| Attachment storage | 1 GB per base | [Airtable plans](https://support.airtable.com/docs/en/airtable-plans) |

**1,000 API calls per workspace per month is the binding constraint** and it is tighter than it
looks. One call per session upload is fine at 150 sessions — but if you ever decide to write one
record per *event*, the 10-records-per-request batch cap means 300 events costs 30 calls, and thirty
sessions exhausts the monthly quota. Exceeding it triggers a one-time 30-day grace period, after
which calls are blocked until the month resets
([managing API call limits](https://support.airtable.com/v1/docs/managing-api-call-limits-in-airtable)).
Airtable is viable *only* if you store one row per session with the JSON in a long-text field.

#### Supabase REST (PostgREST) directly from the game

This genuinely needs no server code: `POST /rest/v1/recordings` with the `anon` key in the `apikey`
header. Three things to get right:

- Supabase grants `select, insert, update, delete` to `anon` by default on tables in `public` — and
  "adding policies doesn't take those grants back"
  ([RLS docs](https://supabase.com/docs/guides/database/postgres/row-level-security)). You must
  `revoke all ... from anon` and then `grant insert` only.
- Enable RLS and add an insert-only policy: `create policy "..." on public.recordings for insert to
  anon with check (true);` — and deliberately do **not** add a select policy, so the shipped key
  cannot read other testers' data back.
- The client must not ask for the inserted row back. PostgREST performs a SELECT after INSERT when
  `Prefer: return=representation` is set; without a select policy that fails. Send
  `Prefer: return=minimal`.
- The one-week inactivity pause (above) still applies.

---

## 2. Purpose-built game/app analytics services

Summary table first; details follow. "Plain .NET desktop" means a non-Unity, non-Unreal,
non-MAUI process — i.e. what MonoGame actually is.

| Service | Works from plain .NET desktop? | Official .NET SDK? | Free tier | Open source / self-host | GDPR posture |
|---|---|---|---|---|---|
| **Aptabase** | Yes, documented HTTP API | Only `Aptabase.Maui` (MAUI-bound) | 20,000 events/mo | Server AGPLv3, SDKs MIT, Docker self-host | Art. 28 processor, DPA in ToS, EU or US residency |
| **GameAnalytics** | Yes, Collection API v2 | No | No MAU cap on Free | No | Art. 28 processor, EU DPA + SCCs, AWS/GCP |
| **Unity Analytics** | Yes, REST API for non-Unity games | No (SDK is Unity-only) | UNVERIFIED | No | Requires player consent per their docs |
| **PostHog** | **Yes — `PostHog` NuGet package** | **Yes, official, MIT** | 1M events/mo, 1 project, 1yr retention | MIT core, self-host possible but discouraged by vendor | Processor, EU cloud in Frankfurt, DPA on request |
| **Countly** | Yes | **Yes, `countly-sdk-windows`, netstandard2.0** | Lite is free, self-host only | AGPL-3.0 w/ modified §7, non-commercial | You are the processor when self-hosted |
| **Umami** | Awkward (web-shaped) | No | Cloud 100K events/mo, 1 website | MIT, self-host free | No cookies, claims no personal data |
| **Plausible** | Awkward (web-shaped) | No | **None** (30-day trial) | AGPL-3.0 CE self-host | EU-based, no cookies |
| **Sentry** | Yes | **Yes, official** | 5,000 errors/mo, 1 user | Self-hostable | Processor, DPA, EU region `de.sentry.io` |
| **MS App Center** | **Dead** | — | — | — | — |

### Aptabase — closest thing to a purpose-built fit

Built specifically for desktop and mobile apps rather than websites, which is the right category.

- **Free tier: 20,000 events/month.** The pricing page also lists "Unlimited Apps", EU or US data
  residency, built-in dashboard, live view, session timeline, and CSV export on every plan
  ([aptabase.com](https://aptabase.com/)). Paid starts at $10/month for 200,000 events/month. Going
  over the limit does not incur overage fees — analytics are temporarily disabled until the next
  month ([same](https://aptabase.com/)). *Note:* third-party review sites variously claim the free
  tier is limited to 1 app and 90-day retention
  ([MakerStack](https://makerstack.co/reviews/aptabase-review/)); the official pricing page says
  Unlimited Apps and does not state a free-tier retention figure. **Treat the 1-app and 90-day
  numbers as UNVERIFIED and trust the vendor page.**
- **.NET support is the catch.** The only official .NET SDK is `Aptabase.Maui`, which requires the
  MAUI app-building pipeline (`MauiApp.CreateBuilder().UseAptabase(...)`)
  ([aptabase/aptabase-maui](https://github.com/aptabase/aptabase-maui/)). NuGet shows
  `Aptabase.Maui` targeting .NET 8.0, latest published version **0.1.0, last updated 1 Sep 2024**
  ([NuGet profile](https://www.nuget.org/profiles/aptabase)), while the repo README references
  `0.2.0` — so the published package is somewhat stale. **A MonoGame process cannot use this SDK.**
- **But the HTTP API is documented and trivial.** `POST {host}/api/v0/events`, `App-Key` header,
  `Content-Type: application/json`, body is a JSON array of at most **25 events per request**. Host
  is derived from the key prefix: `A-EU-*` → `https://eu.aptabase.com`, `A-US-*` →
  `https://us.aptabase.com`, `A-SH-*` → your self-hosted host
  ([How to build your own SDK](https://github.com/aptabase/aptabase/wiki/How-to-build-your-own-SDK)).
  Each event is `{timestamp, sessionId, eventName, systemProps, props}`; `props` values may only be
  strings and numbers. Their own SDK-authoring guidance says explicitly: "Let the application
  continue running even if a tracking request fails (log errors without raising them)."
- **Open source and self-hostable.** Server is **AGPLv3**, SDKs are **MIT**
  ([aptabase/aptabase](https://github.com/aptabase/aptabase)). Self-hosting is `git clone
  https://github.com/aptabase/self-hosting && docker-compose up -d`, requiring PostgreSQL plus
  ClickHouse ([self-hosting repo](https://github.com/aptabase/self-hosting)).
- **GDPR.** "Where you use Aptabase to process personal data of your applications' end users, you act
  as the controller and we act as your processor under Article 28 GDPR. Our Data Processing Agreement
  is incorporated into our Terms of Service." EU-region accounts have data "processed and stored
  exclusively within the European Union." Analytics data is stored for **up to 5 years**, and they
  state you cannot request deletion of a specific person's data "simply because it's impossible to
  know what data relates to you"
  ([Aptabase privacy policy](https://aptabase.com/legal/privacy)). That last sentence is a feature,
  not a bug, for this use case — but note the DPA is not something you separately sign, it comes with
  the ToS.

**The mismatch worth naming:** Aptabase's model is one row per named event with a small properties
bag. RatnaBay's model is a dense timeline where `Hesitation` is computed by `PlayReview` from the gap
between `decision.offered` and `decision.pressed`. You would either send every event (fine at 20k/mo
for ~65 sessions of 300 events) or send one pre-computed `decision_made` event per decision with
`hesitation` as a numeric prop (far better — maybe 10 events per session, and the dashboard can
actually chart it). The second shape means moving the `PlayReview` computation into the client, which
you probably do not want, since the whole point of `PlayReview` being engine-free and tested is that
the analysis stays honest and revisable after the fact.

### GameAnalytics — usable, but you write the client

No .NET/C# SDK for desktop; the SDK list is Unity, Unreal, Roblox, iOS, Android and similar. But the
**Collection API v2** is fully documented for custom integrations:

- `POST /v2/<game_key>/events` (production `api.gameanalytics.com`, sandbox
  `sandbox-api.gameanalytics.com`), HTTPS only, JSON, gzip strongly recommended
  ([Collection API setup](https://docs.gameanalytics.com/event-tracking-and-integrations/sdks-and-collection-api/api/setup)).
- Auth is a **base64-encoded HMAC-SHA256 digest of the (optionally gzipped) request body, keyed with
  the game secret key**, in the `Authorization` header. POST size limit is **1 MB**
  ([OpenAPI spec](https://raw.githubusercontent.com/api-evangelist/gameanalytics/refs/heads/main/openapi/gameanalytics-collection-api-openapi.yml)).
- For custom integrations `sdk_version` must be the literal string `"rest api v2"`
  ([Event Types](https://docs.gameanalytics.com/event-tracking-and-integrations/sdks-and-collection-api/api/event-types)).
  Pleasingly, the shared-event JSON schema on that page accepts `platform: "windows"`, `os_version`
  matching `^windows [0-9...]`, and — genuinely — `engine_version` matching `monogame X.Y.Z`.
- Recommended client behaviour from their docs: cache events locally (they suggest a local DB),
  submit every ~20 seconds, and queue while offline.
- **Free plan: no MAU cap** ([pricing](https://www.gameanalytics.com/pricing)); paid AnalyticsIQ Pro
  is $49/mo. The limits that bind are technical: **500 events per active user per day**, design event
  cardinality 15,000/day, progression 8,000/day, resource 4,000/day. Since 1 Oct 2025, exceeding
  cardinality does not lose data but nulls the event identifiers in the UI and MetricsAPI
  ([event tracking and cardinality limits](https://docs.gameanalytics.com/event-tracking-and-integrations/data-retention-and-limits/event-tracking-and-cardinality-limits)).
  **500 events/user/day is a real constraint for RatnaBay** — a tester doing three 6-minute sessions
  in an evening could plausibly generate ~900 raw events. You would send summarised design events,
  not the raw timeline.
- **Retention degrades with age:** full metrics and filters for 0–1 month; 1–3 months loses error
  stack traces; 3–12 months loses some event types (including *design* and *progression* events) and
  restricts filtering; beyond 12 months only aggregate counts for limited event types
  ([data retention and deletion policy](https://www.gameanalytics.com/trust/data-retention-and-deletion-policy)).
  Also note §5.7 of the terms: an account inactive for 90 consecutive days may have its data deleted
  at GameAnalytics' discretion ([terms](https://www.gameanalytics.com/trust/terms)).
- **GDPR:** "GameAnalytics acts as a Data Processor under the meaning of Art. 28 GDPR"; the developer
  is controller ([Developer Policy](https://www.gameanalytics.com/trust/privacy-faq)). The EU DPA and
  EU SCCs are incorporated into the terms by §9.1
  ([terms](https://www.gameanalytics.com/trust/terms),
  [EU DPA](https://www.gameanalytics.com/trust/eu-data-processing-addendum)). Data resides in AWS and
  GCP with SCCs for third-country transfers. They hold an ePrivacyseal — note this "is not an
  accredited certification procedure within the meaning of Art. 42 GDPR" and the listed validity
  runs 14.07.2023–13.07.2026, i.e. **it has lapsed as of today**
  ([ePrivacy listing](https://www.eprivacy.eu/en/customers/awarded-seals/company/gameanalytics-aps/)).
  Not closed-source-self-hostable.

### Unity Analytics — yes, it works outside Unity

Unity explicitly supports this: "If you have a non-Unity game, you can still use Unity Analytics via
the REST API. This set of web endpoints provides complete flexibility to record events using your
chosen game development engine, but you must implement all of the necessary logic for yourself"
([The REST API](https://docs.unity.com/en-us/analytics/rest-api/rest-api)). Their FAQ confirms:
"Yes. Although we only provide the SDK for Unity projects, you can still use the REST API to upload
events from other sources" ([Analytics FAQ](https://docs.unity.com/en-us/analytics/FAQ)).

Endpoint is
`https://collect.analytics.unity3d.com/api/analytics/collect/v1/projects/{projectId}/environments/{environmentName}`
([Record events with the REST API](https://docs.unity.com/en-us/analytics/rest-api/record-event-rest-api)).
Batching and retries that the SDK does automatically become your problem
([Web API docs](https://services.docs.unity.com/analytics/v1/)).

Two reasons to skip it here. First, their docs carry an explicit warning: "You must not record or
upload events through the REST API unless you have appropriate consent from the player under relevant
data privacy legislation" — which pushes you into building a consent flow, the opposite of minimum
engineering. Second, **I could not verify the current Unity Gaming Services Analytics free-tier
limits from Unity's own pricing pages — treat those as UNVERIFIED.** Signing a non-Unity indie game
up to Unity's platform for one question is poor value.

### PostHog — the strongest "real SDK" option

The surprise finding, and the only vendor here with an **official, MIT-licensed .NET SDK that works
in a plain console/desktop process**.

- Package: `PostHog` on NuGet (the ASP.NET-specific conveniences live in `PostHog.AspNetCore`, which
  you do not want). "The `PostHog` package supports any .NET platform that targets .NET Standard 2.1
  or .NET 8+, including MAUI, Blazor, and console applications"
  ([.NET library docs](https://posthog.com/docs/libraries/dotnet)). Repo is
  [PostHog/posthog-dotnet](https://github.com/PostHog/posthog-dotnet), MIT licensed, actively
  released (PostHog-v2.13.3 at time of writing).
- The library "uses an internal queue to make calls fast and non-blocking. It also batches requests
  and flushes asynchronously" ([same](https://posthog.com/docs/libraries/dotnet)) — which is exactly
  the fire-and-forget behaviour §3 describes, already written and tested by someone else.
- **Privacy detail that matters here:** "The `posthog-dotnet` library disregards the server IP, does
  not add the GeoIP properties, and does not use the values for feature flag evaluations"
  ([same](https://posthog.com/docs/libraries/dotnet)).
- **Free tier: 1,000,000 analytics events/month**, 1 project, 1-year data retention, unlimited team
  members, community support, no credit card ([PostHog pricing](https://posthog.com/pricing)). Also
  100,000 error-tracking exceptions/month on the same free tier ([FAQ](https://posthog.com/faq)).
  A million events per month against 50,000 for a whole playtest round is comically generous.
- **Open source**, MIT-licensed core. PostHog themselves say self-hosting "is not recommended" and
  requires significant infrastructure expertise ([FAQ](https://posthog.com/faq)) — believe them; the
  stack is PostgreSQL + ClickHouse + Redis + Kafka.
- **GDPR:** PostHog Cloud EU runs on AWS `eu-central-1` in Frankfurt and is "an entirely independent
  instance"; PostHog Cloud US is `us-east-1` in Virginia
  ([Cloud EU announcement](https://posthog.com/blog/posthog-cloud-eu)). "If a customer is using
  PostHog Cloud, then PostHog is acting as Data Processor and the customer is the Data Controller."
  DPAs are entered into **on request** via a generator, not automatically
  ([security handbook](https://posthog.com/handbook/company/security), [DPA](https://posthog.com/dpa))
  — so unlike Aptabase and GameAnalytics you must actively go and get one. PostHog also self-certifies
  to the EU-U.S. Data Privacy Framework ([privacy policy](https://posthog.com/privacy)).
- **Caveat:** PostHog is a product-analytics platform with a person-centric data model
  (`distinctId` per user). You can feed it an anonymous per-install GUID, but you are opting into a
  tool whose defaults are built around identifying and profiling users over time. That is more
  compliance surface than a Worker writing blobs to R2, for the same answer.

### Countly

- **Official Windows/.NET SDK exists**: [`Countly/countly-sdk-windows`](https://github.com/countly/countly-sdk-windows),
  targeting **.NET Standard 2.0**, .NET Framework 3.5 and 4.5, with sample projects for WPF, WinForms
  and console ([Countly Windows SDK docs](https://support.countly.com/hc/en-us/articles/360037754691-Windows)).
  netstandard2.0 loads fine into .NET 9, so a MonoGame project can reference it. API is
  `Countly.Instance.Init(new CountlyConfig{...})` then `await Countly.RecordEvent("name", count,
  sum, duration, segmentation)` — the segmentation dictionary would carry `hesitation`.
- **Countly Lite is free forever but self-hosted only** — there is no free hosted tier. It is
  "free to use under an open-source, non-commercial license", self-hosted deployment, "ideal for
  individuals and small teams" ([countly-server](https://github.com/Countly/countly-server/)).
  Managed offerings are Flex and Enterprise, both paid. License is **AGPL-3.0 with a modified Section
  7** ([same](https://github.com/Countly/countly-server/)) — the "non-commercial" framing on the Lite
  tier deserves a careful read before shipping it in a game you intend to sell.
- Running it means MongoDB + Node.js + nginx on a Linux box, which is the VPS problem from §1.2 plus
  a database. **The self-hosting cost is the whole story here**: you would spend more time on the
  Countly instance than on the analysis. Right answer if you already run servers; wrong answer
  otherwise.
- **GDPR:** self-hosting makes you the controller *and* processor, which is the maximum-control,
  maximum-responsibility position ([Countly Lite](https://countly.com/lite)).

### Umami and Plausible — both web-shaped, both a poor fit

- **Umami Cloud Hobby is free: up to 100K events/month, 1 website, 6-month data retention, community
  support.** Crucially, **API access is a Pro ($20/mo) feature — the Hobby row for "API access" is
  blank** ([umami.is/pricing](https://umami.is/pricing)). That matters if you want to pull data back
  out programmatically. Self-hosted Umami is **MIT** licensed and needs only PostgreSQL or MySQL,
  which is genuinely the simplest self-host stack of anything in this document. Umami states it does
  not use cookies, does not collect personal data, and needs no cookie banner
  ([same](https://umami.is/pricing)). Note some comparison sites claim a 1M-event Umami free tier;
  the vendor page says 100K, so those are wrong.
- **Plausible has no free tier** — a 30-day trial with no credit card, then **$9/month Starter for up
  to 10k monthly pageviews and one site**; Growth $14, Business $19, and the **Stats API is a Business
  feature at 600 requests/hour** ([plausible.io](https://plausible.io/#pricing)). It is genuinely the
  best-positioned of these on privacy — "no cookies, no persistent identifiers, no cross-site or
  cross-device tracking... All visitor data is exclusively processed on servers owned and operated by
  European companies and never leaves the EU" ([same](https://plausible.io/#pricing)) — which would
  matter if it were the right shape. Self-hostable Community Edition is AGPL-3.0 and needs PostgreSQL
  *plus* ClickHouse.
- Both count *pageviews* as their base unit and model the world as websites with URLs. Umami's own
  FAQ: "Usage is measured by counting pageviews to a website plus any custom events or custom event
  properties stored... each data property stored counts as one event"
  ([umami.is/pricing](https://umami.is/pricing)). You can post custom events from a desktop app, but
  you will be fighting the data model, the dashboard will show nonsense in half its panels, and a
  "website" field will be a lie. **Skip both.**

### Sentry — for crashes, and it does do custom events

- Official, mature .NET SDK ([docs.sentry.io/platforms/dotnet](https://docs.sentry.io/platforms/dotnet/)).
- **Free Developer plan: 5,000 errors/month, 1 user** ([sentry.io/pricing](https://sentry.io/pricing/)).
- **It does more than exceptions.** `SentrySdk.CaptureMessage` sends arbitrary textual events. There
  is structured logging via `options.EnableLogs = true` and `SentrySdk.Logger`, and a **Metrics API
  which became stable in the .NET SDK in March 2026** — `SentrySdk.Metrics.EmitCounter(...)` and
  `SentrySdk.Metrics.EmitDistribution(name, value, MeasurementUnit.Duration.Second, tags)`
  ([PR #5023 removing Experimental](https://github.com/getsentry/sentry-dotnet/pull/5023),
  [console sample](https://github.com/getsentry/sentry-dotnet/blob/main/samples/Sentry.Samples.Console.Basic/Program.cs)).
  `EmitDistribution` on hesitation-in-seconds is, honestly, a rather elegant way to get a histogram
  of exactly the number this project cares about.
- Attachments work too: at most 40 MB compressed per request and 200 MB uncompressed per event, with
  `SentryOptions.MaxAttachmentSize` defaulting to 20 MiB, and attachments count toward quota as soon
  as they are stored
  ([attachments docs](https://docs.sentry.io/platforms/dotnet/guides/aspnetcore/enriching-events/attachments/)).
  You could attach the whole recording JSON to a synthetic event.
- **GDPR:** EU data storage in **Frankfurt** via the `de.sentry.io` API domain; US is Iowa. The region
  is chosen at organisation creation and **cannot be changed afterwards** — you would have to create a
  new organisation ([data storage location](https://docs.sentry.io/organization/data-storage-location/),
  [EU region FAQ](https://www.sentry.help/en/articles/13964378-sentry-s-eu-region-faq)). Even with the
  EU region, "some account, integration, and organization metadata may still be stored in the US", and
  Sentry "doesn't currently offer an EU legal entity for customer contracting"
  ([EU region FAQ](https://www.sentry.help/en/articles/13964378-sentry-s-eu-region-faq)). There is a
  standard DPA ([sentry.io/legal/dpa](https://sentry.io/legal/dpa/)) and Sentry self-certifies to the
  EU-U.S. DPF ([trust/privacy](https://sentry.io/trust/privacy/)). One thing to actively turn **off**:
  `options.SendDefaultPii = true` "adds request URL and headers, IP and name for users"
  ([.NET docs](https://docs.sentry.io/platforms/dotnet/)) — leave it false.

**Recommendation on Sentry:** worth adopting *for crash reporting* regardless of what you choose for
telemetry — a playtest build that crashes silently on a tester's machine is a wasted tester. But
using it as the primary sink for design telemetry means bending an error tracker into an analytics
tool, and the 5,000 events/month free ceiling is the tightest of any hosted option listed.

### Microsoft App Center — confirmed dead, twice over

Your recollection is right and it has since gone further. **App Center was retired on 31 March 2025**,
after which "it will not be possible to sign in with your user account nor make API calls". Analytics
and Diagnostics got a stay of execution "until the end of June 2026" to allow migration to Azure
Monitor ([App Center retirement](https://learn.microsoft.com/en-us/appcenter/retirement)). Today is
25 August 2026, so **that extension has also now expired.** Do not consider it.

### Anything else worth knowing about

- **Rolling your own on Cloudflare** (§1.2) is not really "not analytics" — you already have
  `PlayReview` and the CLI in `tools/RatnaBay.Tools` doing the analysis. You are not missing a
  dashboard; you have a better one, because it understands `Forced` decisions and re-advertised doors.
- **Steamworks** is out of scope (no Steam release) but for completeness: Steam gives playtime and
  achievement stats free with a released app, and does not require a third-party analytics vendor.

---

## 3. C#/.NET 9 implementation notes

The existing `PlayRecorder` already embodies the right philosophy — its class comment says a recorder
"that can cost a frame or crash a run would be worse than having none". The uploader must hold the
same line, more strictly, because it adds *network* failure modes to *disk* ones.

### HttpClient lifetime

Microsoft's current guidance for .NET Core / .NET 5+ is unambiguous: use **a `static` or singleton
`HttpClient` with `PooledConnectionLifetime` set**, or short-lived clients from `IHttpClientFactory`.
For a game with no DI container, the first is correct:

```csharp
private static readonly HttpClient Http = new(new SocketsHttpHandler
{
    PooledConnectionLifetime = TimeSpan.FromMinutes(2),
})
{
    Timeout = TimeSpan.FromSeconds(10),
};
```

Two problems are being solved at once. `HttpClient` "only resolves DNS entries when a connection is
created" and ignores TTLs, so a long-lived client without `PooledConnectionLifetime` pins a stale IP.
And creating a client per request exhausts ephemeral ports because "TCP ports aren't released
immediately after connection closure". Both from the
[HttpClient guidelines](https://learn.microsoft.com/en-us/dotnet/fundamentals/networking/http/httpclient-guidelines),
which suggests 2 minutes as a reasonable interval. `IHttpClientFactory` buys you nothing here and
drags in `Microsoft.Extensions.*` — skip it.

### Timeouts, layered

- `HttpClient.Timeout` is the outer bound on the whole request including response read. 10 seconds is
  generous for a few-KB POST and short enough that nothing user-visible hangs.
- Additionally pass a `CancellationToken` from a `CancellationTokenSource` you can cancel on shutdown,
  so a pending upload dies immediately rather than blocking exit.
- Do **not** rely on `Timeout` alone during shutdown. A 10-second timeout on a 300 ms exit path is a
  10-second freeze the tester will report as "the game hung when I quit".

### Fire-and-forget, done properly

Naked `_ = SomeAsync();` is genuinely dangerous: an exception in an un-awaited `Task` that nobody
observes is fine in .NET Core (unobserved task exceptions no longer crash the process by default),
but *any* synchronous exception thrown before the first `await` propagates straight into the caller's
frame — which in MonoGame is your `Update` loop. Wrap the whole thing:

```csharp
private static void FireAndForget(Func<CancellationToken, Task> work, CancellationToken token)
{
    _ = Task.Run(async () =>
    {
        try { await work(token).ConfigureAwait(false); }
        catch { /* telemetry must never be the reason a run ends */ }
    }, token);
}
```

The bare `catch` is correct here, and is consistent with how `PlayRecorder.Flush` already sets
`_broken = true` and stops rather than reporting itself. A telemetry uploader is one of the few
places where swallowing every exception is the right engineering decision. Also add a
`TaskScheduler.UnobservedTaskException` handler that does nothing but mark itself observed, so a
missed edge case cannot surface as a finalizer-thread surprise.

### Do not flush on process exit

`AppDomain.CurrentDomain.ProcessExit` looks like the obvious hook. It is not. In .NET Framework there
was a hard 2-second budget for all `ProcessExit` handlers; Microsoft's docs note "This time limit does
not exist in .NET Core and .NET 5+"
([AppDomain.ProcessExit](https://learn.microsoft.com/en-us/dotnet/api/system.appdomain.processexit?view=net-10.0)).
That removal is a trap, not a licence: with no runtime timeout, a network call in `ProcessExit` can
hang the process indefinitely on exit, which is a far worse bug than a lost upload. And the handler
does not run at all on `Process.Kill`, a hard power-off, or many crash paths — precisely the sessions
you most want data from.

**Upload during play, not at exit.** The existing `FlushEvery = 25` cadence is the model: flush to
disk every N events (already done), and attempt an upload at natural pauses — the moment
`decision.pressed` or `decision.camped` is recorded, or when `run.ended` fires. By the time the tester
alt-F4s, the data is already gone.

### Offline queueing, which you almost get for free

Because `PlayRecorder` already writes every session to disk, the queue is the filesystem. The
uploader becomes:

1. On launch, list `play_*.json` in `PlayRecorder.Directory`.
2. For each file not in a small `uploaded.txt` / `.uploaded` sidecar marker set, POST it.
3. On HTTP 2xx, write the marker. On anything else, leave it and try next launch.

That is offline support, retry-across-restarts, and crash-resilience in about twenty lines, with no
in-memory queue and no separate persistence format. It also means a tester who plays five sessions
offline on a train uploads all five on next launch. `PlayRecorder.Newest()` already demonstrates the
directory-listing pattern to copy.

Do not delete uploaded files — the tester might still want to send them manually, and disk cost is
nil.

### Retry and backoff

At this scale, retry is nearly pointless: if the POST fails, the file stays on disk and gets retried
at next launch, which is a better backoff than any in-process loop. If you want in-session retry
anyway, keep it to **two attempts with a few seconds between them**, and only for transient
conditions (`HttpRequestException`, `TaskCanceledException` from timeout, HTTP 5xx and 429). Never
retry a 4xx other than 429 — that means your payload is wrong and retrying makes it wrong repeatedly.

If you want this off the shelf, `Microsoft.Extensions.Http.Resilience` composes a Polly retry pipeline
into a `SocketsHttpHandler` via `ResilienceHandler`, with an example in the
[HttpClient guidelines](https://learn.microsoft.com/en-us/dotnet/fundamentals/networking/http/httpclient-guidelines).
For 150 uploads, hand-rolled two-attempt logic is smaller than the dependency.

### Payload size and compression

A 6-minute session's event list is single-digit kilobytes as raw JSON — the `WriteIndented = true`
already set in `PlayRecording.JsonOptions` roughly doubles it, which matters for the file on disk not
at all and for the upload not much. If you do compress, note GameAnalytics' 1 MB POST cap and that
gzip is "strongly recommended" there
([Collection API setup](https://docs.gameanalytics.com/event-tracking-and-integrations/sdks-and-collection-api/api/setup)).
For a Worker endpoint, don't bother; the complexity is not repaid.

### Keeping a key out of a shipped binary — you can't, so plan for that

**Accept the premise.** Any secret in a client binary is public. Obfuscation, string-splitting,
XOR-with-a-constant, and embedding in a resource all fail to a determined person with `strings` and
ten minutes; .NET makes it worse because IL decompiles cleanly with ILSpy or dnSpy. Even
GameAnalytics' HMAC scheme, which never sends the secret over the wire, requires the secret to be
*in* the binary to compute the digest — so it protects against passive network observers, not against
whoever owns the machine.

What actually helps, in rough order of value:

1. **Make the endpoint write-only.** The credential can create data and cannot read it. On Cloudflare
   this is one `if (request.method !== 'POST') return new Response(null, {status: 405})`. On Supabase
   it is `revoke all from anon` + an insert-only RLS policy with no select policy. The worst case then
   becomes "a stranger posts junk", not "a stranger downloads all my testers' sessions".
2. **Rate limit.** Cloudflare's `ratelimits` binding gives 10s or 60s windows enforced per PoP
   ([Rate Limiting API](https://developers.cloudflare.com/workers/runtime-apis/bindings/rate-limit/));
   see the caveat above about Free-plan availability. Cap payload size too — reject bodies over, say,
   256 KB before parsing.
3. **Scope the secret per build.** Bake a distinct key or a `build` string into each playtest build.
   You can revoke a leaked one without breaking the others, and you get free build attribution.
   `PlayRecording.Build` already carries the assembly version, so the field exists.
4. **Make the endpoint temporary.** A playtest is a window of weeks. Hard-code an expiry date in the
   Worker; after it, return 410. A leaked key to a dead endpoint is not a problem.
5. **Have nothing worth stealing.** This is the real defence and it loops back into §4: if the payload
   contains no personal data, a leaked write-only key is an annoyance rather than an incident.

Do **not** put an itch.io API key, a Cloudflare account token, or a Supabase `service_role` key in the
client. The `service_role` key in particular bypasses RLS entirely — Supabase's own docs say "Never
use a secret key in the browser or [ship] it to customers"
([RLS docs](https://supabase.com/docs/guides/database/postgres/row-level-security)).

### Do not let the uploader touch the game loop

`PlayRecorder.Record` is called from gameplay code. The uploader must never be called synchronously
from there, must never take a lock the game thread also takes, and must never allocate a large buffer
on the game thread. Read the file and serialize on the background task. MonoGame's `Update`/`Draw`
run on one thread; anything that blocks it for 50 ms is a visible hitch during a fight.

---

## 4. Privacy by design for this specific case

### What you actually need to answer the question

`DecisionReview` is `(RoomsCleared, Pending, NextPays, Health, Hesitation, PressedOn)`. Not one of
those six fields relates to a person. `PlayReview.Verdict` needs nothing more. So:

- **A per-install random GUID is optional, not required.** You need it only to distinguish "one tester
  hesitated ten times" from "ten testers hesitated once" — a real and worthwhile distinction for a
  sample of 3–30, since one outlier tester could otherwise dominate the verdict. Generate it with
  `Guid.NewGuid()` on first launch, store it in the save directory, and never derive it from anything
  about the machine.
- **A per-session GUID alone might be enough.** With sessions already separated into files, a random
  session ID gives you within-session grouping without any cross-session linkage at all. If you can
  live without "is this the same tester", this is strictly better privacy.
- **Build version**, already present as `PlayRecording.Build`. Essential — comparing hesitation across
  balance changes is the entire reason to run more than one round.

### What must not be collected

- Machine name, username, `Environment.UserName`, `Environment.MachineName`, the user's home
  directory path (which usually contains their real name), or any file path at all.
- Hardware IDs, MAC addresses, motherboard serials, Windows product ID, or anything else stable
  across reinstalls. The ICO lists MAC addresses and device fingerprints as online identifiers
  ([PECR/UK GDPR guidance](https://ico.org.uk/for-organisations/direct-marketing-and-privacy-and-electronic-communications/guidance-on-the-use-of-storage-and-access-technologies/how-do-the-pecr-rules-relate-to-the-uk-gdpr/)).
- Email, Discord handle, itch.io username — you already know who your 3–30 testers are, out of band.
  Do not put it in the payload; keep the mapping in your head or a local note.
- Geolocation, timezone, locale. Timezone plus a session timestamp narrows a person surprisingly well
  in a 30-person sample. `PlayRecorder` currently names files with `DateTime.Now` (local time) — the
  *filename* leaks approximate timezone even though `StartedUtc` inside is UTC. For uploads, send the
  UTC field, not the filename.
- Free text of any kind. There is no free-text field in `PlayEvent` today and there should not be one;
  the moment a tester can type, they can type something identifying.

### Why this is worth being deliberate about

Under UK/EU GDPR, "'personal data' means any information relating to an identified or identifiable
natural person", and an identifiable person is one identifiable "directly or indirectly, in particular
by reference to an identifier such as... an online identifier"
([ICO: What is personal data?](https://ico.org.uk/for-organisations/uk-gdpr-guidance-and-resources/personal-information-what-is-it/what-is-personal-data/what-is-personal-data/)).
Recital 30 names IP addresses and cookie identifiers as examples
([ICO: identifiers and related factors](https://ico.org.uk/for-organisations/uk-gdpr-guidance-and-resources/personal-information-what-is-it/what-is-personal-data/what-are-identifiers-and-related-factors/)).

A random install GUID that you never link to a name is **pseudonymised, not anonymised** — but only
in the hands of someone who holds the linking information. The ICO is explicit that "Recital 26 makes
it clear that pseudonymised personal data remains personal data", while truly anonymised data falls
outside GDPR entirely
([ICO: pseudonymisation](https://ico.org.uk/for-organisations/uk-gdpr-guidance-and-resources/data-sharing/anonymisation/pseudonymisation/)).
The practical consequence: if you keep no mapping from GUID to person anywhere, and the payload
contains nothing else identifying, and the IP is never stored, your dataset is a strong candidate for
being outside GDPR's scope. That is worth a lot to a solo developer — it means no DPA to chase, no
data subject access request process, no retention policy to write.

None of this removes the ordinary decency obligation: **tell your testers.** A line in the itch.io
description and a line on the game's title screen — "this playtest build records timings of in-game
decisions and uploads them anonymously; there's a button in Options to turn it off and to open the
folder where the files live" — costs nothing and is the difference between a tester who is fine with
it and one who feels spied on. For a named group of 3–30 people you invited personally, you should
have asked anyway.

### The IP address problem, and what to do about it

**Every HTTP endpoint sees the client's IP.** This is unavoidable at the protocol level; the question
is only whether it is *stored*.

On Cloudflare, the client IP arrives as `CF-Connecting-IP`, which "provides the client IP address
connecting to Cloudflare to the origin web server"
([Cloudflare HTTP headers](https://developers.cloudflare.com/fundamentals/reference/http-headers/)).
Inside a Worker it is also on `request.headers` and in `request.cf`. Mitigations:

1. **Never read it, never log it.** In a Worker you control every write. Don't put the IP in the R2
   object key, the object metadata, or a `console.log`. Cloudflare's own DPA identifies IP addresses
   in Customer Logs as personal data processed on your behalf
   ([Cloudflare DPA](https://www.cloudflare.com/cloudflare-customer-dpa/)), so avoiding the write is
   avoiding the whole question.
2. **Strip it at the edge if you want belt and braces.** Cloudflare offers a "Remove visitor IP
   headers" Managed Transform that suppresses `CF-Connecting-IP` and any header that may contain the
   visitor's IP ([Cloudflare HTTP headers](https://developers.cloudflare.com/fundamentals/reference/http-headers/)).
3. **Accept that the provider still sees it transiently.** Cloudflare, Google, Vercel and everyone
   else will have it in their own edge logs for their own retention period. You cannot avoid this
   short of running your own server with logging disabled — and even then, the network path sees it.
   This is a residual you accept, not one you eliminate. It is also exactly what the provider's DPA
   exists to cover.
4. **If you need a per-request identifier for de-duplication, use a client-generated one.** Have the
   game send an upload UUID rather than deriving a key from IP+timestamp.

A hosted analytics vendor takes this decision out of your hands, which is one more argument for the
self-run endpoint. Aptabase says it does not store IP addresses; PostHog's .NET library "disregards
the server IP" ([PostHog .NET docs](https://posthog.com/docs/libraries/dotnet)) — both good, but both
are the vendor's promise rather than your code.

### Retention

Set a lifecycle rule on the R2 bucket (or a calendar reminder) to delete the raw uploads once the
question is answered. `PlayReview` output — the aggregate hesitation distribution — is what you
actually keep. Note by contrast that Aptabase retains analytics data for up to 5 years
([privacy policy](https://aptabase.com/legal/privacy)) and GameAnalytics for 12 months rolling
([retention policy](https://www.gameanalytics.com/trust/data-retention-and-deletion-policy)); with
your own bucket, retention is a one-line rule you choose.

---

## 5. What itch.io gives you

### The creator dashboard

itch.io's own description: "Usage information is collected and displayed to you, such as how many
times your pages are being viewed, downloaded, and purchased"
([Creator FAQ](https://itch.io/docs/creators/faq)). The Analytics tab per project shows views,
downloads, browser plays, and referrers, with a graph over a selectable range; per-project totals
cover views, downloads, revenue, payments, ratings and collections
([community thread with a good enumeration](https://itch.io/t/5281744/adding-actual-analytics-on-itch-for-our-own-releases)).
The graph range goes back at most 365 days
([forum](https://itch.io/t/5132885/in-a-games-analytics-are-the-top-stats-for-1-year-or-lifetime-)).

**It gives you nothing about gameplay.** No playtime, no sessions, no in-game events. That is not an
oversight; itch.io hands users the file unmodified and has no visibility past the download.

Two things that are more useful than the dashboard for a small playtest:

- **Download keys.** "A download key is a special URL that gives someone full access to download your
  game without having to buy it... You can track how many times the key has been used, give the key a
  name to identify it, and revoke it" ([Creator FAQ](https://itch.io/docs/creators/faq)). Issue one
  named key per tester and you get a free, reliable "did this specific person actually download the
  build" signal — which lets you distinguish "no data because they didn't play" from "no data because
  the upload failed", the exact ambiguity that sinks small playtests.
- **The server-side API.** `GET https://api.itch.io/profile/games` with your API key returns per-game
  `downloads_count`, `views_count`, `purchases_count` and earnings
  ([Serverside API reference](https://itch.io/docs/api/serverside)). There is also an undocumented
  endpoint an itch.io admin pointed people at for daily graph data:
  `https://itch.io/game/summary/GAME_ID?group_by=daily&range_left=...&range_right=...` with a JSON
  accept header — explicitly offered with "this is not a documented API, so I can't guarantee it will
  be available at that URL or in that format forever"
  ([forum, leafo](https://itch.io/t/653777/number-of-downloads-in-a-time-range)).

You can also attach a **Google Analytics 4 Measurement ID** to your itch.io page, account-wide at
`itch.io/user/settings/analytics` or per-project under Interact
([GA4 update guide](https://itch.io/t/2864151/google-analytics-4-update-guide)). This gives you page
analytics, not game analytics, and drags GA4's consent obligations onto your store page. For this
question it is irrelevant.

### The itch desktop app

The app **does track playtime locally**, per profile, and shows it in the library — "Installed items
show install size, version, and play time inline... The installed page can be sorted by last played,
play time, size on disk, and install date" and "Play time and last played are now tracked per
profile" ([itch app changelog](https://github.com/itchio/itch/blob/master/CHANGELOG.md),
[release notes](https://itch.io/updates/itch-app-bundle-support-launch-args-and-more)).

**This data is not exposed to developers.** There is a long-standing feature request for aggregate
playtime analytics that has gone nowhere
([Average Playtime and Play Session Analytics](https://itch.io/t/1137480/average-playtime-and-play-session-analytics)),
and the standard objection is sound anyway: only the fraction of players using the desktop app would
be counted.

Architecturally the app is an Electron shell over **butler**, a Go daemon exposing JSON-RPC 2.0
("butlerd") that handles installs, launches and the local SQLite database
([itchio/itch](https://github.com/itchio/itch)). In principle a game could talk to butlerd, but it
would only work for testers who use the app, it is not a documented integration surface for games,
and playtime is not the number you want. **Nothing here is useful for this project.**

One itch.io API endpoint *is* worth knowing about for a different reason:
`https://api.itch.io/wharf/latest` "returns the latest user-version for a given channel. Useful for
notifying players from within a game when a new version of a build is available, without having to
bundle the itch app" ([Serverside API reference](https://itch.io/docs/api/serverside)). For a
playtest that iterates, an in-game "there's a newer playtest build" nag is more valuable than any
analytics dashboard.

---

## 6. Recommendation

Ranked for *this* case: one question, 3–30 testers, a solo developer, minimum engineering.

### Tier 1 — do these now, they are hours not days

**1. Add the local-access affordances (≈1 hour, zero infrastructure, zero compliance surface).**

- An "Open recordings folder" button that runs `explorer.exe /select,<newest file>`.
- A "Copy summary to clipboard" button producing the same text as `tools/RatnaBay.Tools`.
- For playtest builds only, point `PlayRecorder.Directory` next to the executable.

This alone answers the question for a 3–10 person round and every later option is strictly better
with these present. If a tester's upload fails, this is the fallback that saves the session.

**2. Issue one named itch.io download key per tester (≈10 minutes).**
Free, and it distinguishes "didn't play" from "played, no data" — the ambiguity that ruins small-N
playtests ([Creator FAQ](https://itch.io/docs/creators/faq)).

### Tier 2 — the actual answer for 10–30 testers

**3. A Cloudflare Worker writing to R2, plus the disk-as-queue uploader.**

Roughly 15 lines of JavaScript and 60 lines of C#. The Worker: reject non-POST, cap body size, apply
the free-tier rate limit binding, `env.BUCKET.put(uuid, body)`, return 204, never log an IP. The
client: on launch, POST any `play_*.json` lacking an `.uploaded` marker, on a background task, with a
static `HttpClient` (`PooledConnectionLifetime` 2 min, `Timeout` 10 s), all exceptions swallowed, and
another attempt after each decision event.

Why this wins:

- **Free tier is 100,000 Worker requests/day and 1,000,000 R2 Class A ops/month against your ~150
  uploads.** You will not think about quota again
  ([Workers limits](https://developers.cloudflare.com/workers/platform/limits/),
  [R2 pricing](https://developers.cloudflare.com/r2/pricing/)).
- **No commercial-use restriction** (unlike Vercel Hobby), **no inactivity pause** (unlike Supabase
  Free), **no shared-account blast radius** (unlike Netlify's credit model pausing all projects).
- **The data lands as the exact JSON `PlayRecording.TryLoad` already reads.** Download the bucket,
  point the existing CLI at the folder, done. No schema mapping, no dashboard to learn, no data model
  to fight, and `PlayReview`'s hard-won correctness about `Forced` decisions and re-advertised doors
  survives intact.
- **You control every byte written**, so "don't store the IP" is a decision you make rather than a
  vendor promise you trust.

### Tier 3 — reasonable alternatives, with the reason each loses

**4. PostHog Cloud EU with the official `PostHog` NuGet package.** The best option if you want a
dashboard without building one, and the SDK's internal queue and async batching is the fire-and-forget
uploader from §3 already written. 1M events/month free ([pricing](https://posthog.com/pricing)),
official MIT .NET SDK working in console apps
([.NET docs](https://posthog.com/docs/libraries/dotnet)), Frankfurt hosting. Loses because you must
request a DPA separately, because a person-centric product-analytics platform is more compliance
surface than blobs in a bucket, and because reshaping the event timeline to fit its model risks
losing the nuance `PlayReview` encodes.

**5. Aptabase.** The most philosophically aligned vendor — desktop-first, privacy-first, EU residency,
DPA already in the ToS, self-hostable. Loses on one specific fact: **the only .NET SDK is
`Aptabase.Maui`, which a MonoGame process cannot use**, and the published NuGet package is at 0.1.0
from September 2024 ([NuGet](https://www.nuget.org/profiles/aptabase)). You would hand-roll against
the HTTP API — at which point you have written the same client as option 3 but pointed at someone
else's dashboard and 20,000 events/month instead of your own bucket and effectively no limit.

**6. Google Apps Script → Sheet.** Genuinely the lowest infrastructure of anything here, and the data
lands somewhere the developer can already read. Loses on the POST redirect awkwardness, no version
control for the server code, and undocumented inbound limits. A perfectly defensible choice if the
developer already lives in Google Sheets.

**7. Add Sentry alongside whatever you choose** — not as the telemetry sink, but for crashes. Free
Developer plan is 5,000 errors/month ([pricing](https://sentry.io/pricing/)); set
`SendDefaultPii = false` and pick the EU region at organisation creation, since it cannot be changed
later ([data storage location](https://docs.sentry.io/organization/data-storage-location/)). A
playtest build that crashes silently is a wasted tester, and that is a different failure from the one
this document is about.

### Do not

- **Microsoft App Center** — retired 31 March 2025; the Analytics & Diagnostics extension ran only to
  30 June 2026 and has now expired
  ([retirement notice](https://learn.microsoft.com/en-us/appcenter/retirement)).
- **Plausible or Umami** — website analytics; you would be lying to the data model in every field, and
  Plausible has no free tier at all.
- **Airtable as an event sink** — 1,000 API calls per workspace per month
  ([plans](https://support.airtable.com/docs/en/airtable-plans)) is a cliff you would hit the first
  time you decided one row per event was a good idea.
- **Countly self-hosted, Unity Analytics, or a VPS** — each costs more setup than the entire question
  is worth at this scale.
- **A "shareable code"** — a decision summary does not fit in a code short enough to read aloud, so it
  degenerates into a worse clipboard copy with an encoding scheme to version.
- **Any secret you actually care about in the shipped binary.** Write-only endpoint, per-build key,
  rate limit, expiry date, and nothing worth stealing in the payload.

---

## Appendix: consolidated free-tier figures

All verified against the linked primary sources on 25 August 2026. Free tiers change; re-check before
committing.

| Provider | Key free-tier numbers | Source |
|---|---|---|
| Cloudflare Workers | 100,000 req/day (resets 00:00 UTC), 10 ms CPU/invocation, 128 MB, 3 MB script | [limits](https://developers.cloudflare.com/workers/platform/limits/) |
| Cloudflare R2 | 10 GB-mo, 1M Class A ops/mo, 10M Class B ops/mo, free egress | [R2 pricing](https://developers.cloudflare.com/r2/pricing/) |
| Cloudflare KV | 100,000 reads/day, 1,000 writes/day, 1,000 deletes/day, 1 GB | [KV pricing](https://developers.cloudflare.com/kv/platform/pricing/) |
| Cloudflare D1 | 5M rows read/day, 100,000 rows written/day, 5 GB account, 500 MB/DB, 10 DBs, 50 queries/invocation | [D1 pricing](https://developers.cloudflare.com/d1/platform/pricing/), [D1 limits](https://developers.cloudflare.com/d1/platform/limits/) |
| Vercel Hobby | 1,000,000 function invocations, 4 CPU-hrs, 360 GB-hrs, 1,000,000 edge requests — **non-commercial only** | [Hobby plan](https://vercel.com/docs/plans/hobby) |
| Netlify Free | 300 credits/mo hard limit; compute 10 credits/GB-hour; all projects pause when exhausted | [how credits work](https://docs.netlify.com/manage/accounts-and-billing/billing/billing-for-credit-based-plans/how-credits-work/) |
| Supabase Free | 2 projects, 500 MB DB, unlimited API req, 5 GB egress, 1 GB storage, 500k edge fn invocations, **paused after 1 week idle** | [pricing](https://supabase.com/pricing), [pausing](https://supabase.com/docs/guides/platform/free-project-pausing) |
| Google Apps Script | 6 min/execution, 30 simultaneous/user, 1,000 simultaneous/script; **no documented inbound web-app cap** | [quotas](https://developers.google.com/apps-script/guides/services/quotas) |
| Airtable Free | 1,000 records/base, 1,000 API calls/workspace/mo, 5 req/s per base, 10 records/batch | [plans](https://support.airtable.com/docs/en/airtable-plans) |
| Hetzner (no free tier) | CX23 €5.49/mo excl. VAT; CAX11 €5.99/mo excl. VAT (from 15 Jun 2026) | [price adjustment](https://docs.hetzner.com/general/infrastructure-and-availability/price-adjustment/) |
| Aptabase | 20,000 events/mo, unlimited apps, EU or US residency | [aptabase.com](https://aptabase.com/) |
| GameAnalytics | No MAU cap; 500 events/active user/day; design cardinality 15,000/day; 12-mo degrading retention | [pricing](https://www.gameanalytics.com/pricing), [limits](https://docs.gameanalytics.com/event-tracking-and-integrations/data-retention-and-limits/event-tracking-and-cardinality-limits) |
| PostHog | 1,000,000 events/mo, 1 project, 1-yr retention, 100k exceptions/mo | [pricing](https://posthog.com/pricing), [FAQ](https://posthog.com/faq) |
| Umami Cloud | 100,000 events/mo, 1 website, 6-mo retention, **no API access on Hobby** | [pricing](https://umami.is/pricing) |
| Plausible | **No free tier** — 30-day trial (no card), then $9/mo Starter (10k pageviews, 1 site) | [plausible.io](https://plausible.io/#pricing) |
| Sentry | 5,000 errors/mo, 1 user | [pricing](https://sentry.io/pricing/) |
| Countly Lite | Free but **self-hosted only**, AGPL-3.0 w/ modified §7, non-commercial | [countly-server](https://github.com/Countly/countly-server/) |
| Unity Analytics | **UNVERIFIED** — could not confirm current UGS Analytics free-tier limits from a primary source | — |
