# Technical suggestions: 1–4-player online co-op

**Date:** 8 September 2026  
**Status:** proposal and implementation plan; no networking or dependencies added.  
**Scope:** Windows PC, optional online PvE for one host and up to three guests. Offline single-player remains supported.

## 1. Recommendation and commercial rationale

**Co-op is a good fit for Ratna Bay, provided it is treated as a substantial feature rather than a transport add-on.** Recovering a teammate, deciding whether to bank together, and returning to the same crew make the rescue story playable. The order already supplies a natural explanation for multiple protagonists.

Commercially, friends can introduce each other to the game, shared rescues create useful trailer moments, and a group may choose a game they can play together. These are product hypotheses, not a sales forecast. No evidence reviewed establishes a causal sales uplift for this project. Development delay, poor connections, and difficulty assembling a group can offset the benefit. Keep solo enjoyable and do not advertise co-op as ready before external groups complete a session reliably.

Validate the proposition with a two-player room prototype, then four-player external sessions. Ask whether players would invite a friend and observe whether they actually return together. Do not infer four purchases from one interested player.

`Docs/PRODUCTION_PLAN.md` currently excludes multiplayer from the first release. This document proposes a scope change at the user's request; it does not silently rewrite that release plan. Decide whether co-op belongs at launch after the prototype gate below.

## 2. Keep the first version deliberately bounded

| Decision | Recommended first release |
|---|---|
| Topology | Host-authoritative listen server: one player's PC runs the world |
| Maximum party | Four humans total, including the host |
| Internet distribution | itch.io and Steam; identical standalone GNS transport |
| Discovery | Private invite-code/link lobby, no public matchmaking |
| Joining | Yard before a descent; new guests wait until the next yard return |
| Reconnection | Existing guest may reconnect to their reserved character during a descent |
| Campaign ownership | One host-owned co-op campaign, including guest character records |
| Progress portability | No importing/exporting gear or merging independent campaigns in v1 |
| Host departure | Session ends; restore last committed checkpoint on restart |
| Host migration | Explicitly excluded, including automatic lobby-owner transfer |
| Combat | PvE; friendly fire off; no mandatory tank/healer/classes |
| Communication | Pings and short preset messages; external voice chat |
| Other exclusions | Split-screen, cross-platform play, dedicated servers, ranked modes, trading, mods |

Call this **host-led co-op over P2P connections**. Clients connect to the host, not to every other client. A relay may carry packets while the host still owns the simulation. P2P does not mean guaranteed direct connectivity, zero platform dependencies, or automatic state synchronisation.

## 3. Transport choice and prerequisites

### Selected: standalone GameNetworkingSockets (GNS)

**User decision:** use the open-source GameNetworkingSockets library for both itch.io and Steam builds. No Steam client, App ID, Steam authentication, Steam lobby, or Valve relay service is required for the shared multiplayer path. Matching Windows builds from either store must play together.

GNS is a C++ transport with a plain C interface, reliable/unreliable messages and pluggable P2P signalling. It does not supply game replication or our player identity service. The Steam names in its interfaces do not make standalone use require Steam. [Official repository](https://github.com/ValveSoftware/GameNetworkingSockets).

### C# integration and packaging

Evaluate ValveSockets-CSharp as a binding candidate, not an approved dependency. Confirm its coverage of the selected GNS revision, custom signalling, ICE configuration, connection callbacks and message ownership. If coverage is insufficient, use a narrow maintained native bridge/PInvoke layer over GNS; do not substitute Steamworks.NET. Pin native and managed versions together. Test x64 DLL dependencies, calling conventions, struct layout, callback lifetime, disposal and the published single-file executable. [Binding repository](https://github.com/nxrighthere/ValveSockets-CSharp).

### Connection services we must supply

Use a small independently hosted HTTPS/WebSocket rendezvous service: create a private lobby, issue an expiring invite, admit guests, and exchange GNS signalling messages. It carries connection setup and presence, not normal gameplay packets. Start with one deployable service and no public lobby browser.

Provide configured STUN and a supported TURN relay fallback. Direct ICE connectivity is preferred; a relay carries gameplay when direct connectivity fails. Verify TURN interoperability with the exact GNS build in the spike, including a forced-relay test. Do not assume that compiling GNS supplies a running relay or that every blocked network will work. The upstream P2P guide describes these separate prerequisites. [GNS P2P guide](https://github.com/ValveSoftware/GameNetworkingSockets/blob/master/README_P2P.md).

A self-hosted or replaceable rented relay is infrastructure, but not a store restriction. Budget its bandwidth, availability, region selection, abuse limits and maintenance. Keep relay secrets server-side and issue short-lived scoped credentials. Do not relay gameplay over the signalling WebSocket as an improvised fallback. Offline solo and explicit direct-IP LAN play should remain available when the rendezvous service is unavailable; internet invites/reconnect setup may not be.

### Identity without store accounts

Create a persistent random local profile and device credential using an established authentication library. Register it with the rendezvous service over TLS; authenticate subsequent connections with a nonce challenge or short-lived signed admission ticket bound to that profile, lobby, host and expiry. A display name or caller-supplied UUID is not authentication. Bind host admission to the actual gameplay handshake and prevent ticket replay; encrypted transport alone is not proof of player identity.

The invitation is an expiring capability with a high-entropy secret; a short lookup code, if used, needs rate limiting and host approval. Reconnection uses the same authenticated profile and campaign mapping, not the invitation as proof of ownership. No mandatory email account in v1. Explain that losing local credentials requires host-approved reassignment of the saved member; never grant a character merely because someone claims its name.

### Staged delivery

1. Loopback tests and direct-IP LAN GNS transport.
2. Store-independent lobby/invite signalling and authenticated admission.
3. Direct internet ICE and verified relay fallback on restrictive networks.
4. Mixed itch.io/Steam build sessions with Steam completely closed on the itch.io machines.

LAN-only success is not internet-co-op completion. Optional Steam overlay invite convenience can be added later by sharing the same invitation; it must never replace the common transport, identity or lobby service.

## 4. What the current repository permits—and what must change

Reviewed source uses .NET 9, a `net9.0-windows` win-x64 client, and MonoGame WindowsDX 3.8.5.1. No multiplayer transport dependency was found in the reviewed projects.

| Existing area | Present assumption | Required adaptation |
|---|---|---|
| `src/RatnaBay.Domain` | Rules are independent of MonoGame | Preserve this advantage; add party rules without platform SDK types |
| `GameSession.Player` and `SaveData` | One character, position, inventory and legacy | Separate local presentation from a host campaign and member records |
| `PlayerCharacter` | Also owns Story, World and Legacy | Split shared campaign facts from personal vitals, skills, inventory and succession |
| `Encounter` | One camera/player position, focus and reward recipient | Host simulation over a member collection; explicit attacker and target IDs |
| `RunRuntime` | One player crossing a threshold wakes a room and closes the door | Party readiness, gathering and acknowledged scene transitions |
| `FirstPersonView` | Movement and camera state advance together | Extract a reusable movement calculation for authority and local prediction; keep camera/rendering local |
| `Game1`, `ScreenStack` | Local panels can stop world updates | Network pump and authoritative simulation must survive local menus |
| `CombatFeel` | Hitstop belongs to the local fight | Co-op hitstop freezes only presentation, never the whole host world |
| `MineGenerator` | Seeded generation and stable spawn IDs | Useful for content verification; not proof of deterministic multiplayer simulation |
| `FigurePresenter` | Enemies/NPCs, no remote player presentation | Add distinguishable remote characters, equipment poses, downed states and name markers |

The hardest part is ownership, not sending position packets. Creating four unchanged `PlayerCharacter` objects would also create four competing story/world records. Resolve that before network replication.

## 5. Proposed architecture

```text
Local input -> PlayerCommand -> HostSimulation -> snapshots/events
                                   ^                  |
Guest input -> transport ----------|                  v
                                          Local/remote presentation

Only the host writes campaign checkpoints.
```

Suggested types and locations (new names, not existing APIs):

- `RatnaBay.Domain/Coop`: PartyState, MemberId, DownedRules, PartyEconomy, RoomCommit, CoopBalanceProfile.
- `RatnaBay.Game/Simulation`: HostSimulation and the game-specific bridge to collision/world rules. Extract it from Encounter incrementally; no graphics device or camera needed to tick it.
- A small `RatnaBay.Networking` project: transport-neutral message DTOs, codec, connection state machine, sequence handling and `INetworkTransport`. It must not reference Game or Engine.
- `RatnaBay.Game/Networking`: standalone GNS adapter, native bridge and session coordinator. Transport initialisation stays outside Domain.
- `RatnaBay.Game/Session`: CoopCampaignStore, member authentication mapping, local member selection, lobby flow.
- `RatnaBay.Game/World` and `Ui`: remote actor presentation, party HUD, pings and ready prompts.

Shared CampaignState owns story beats, evidence, service rank, deepest progress, run pot, doors and killed-world IDs. MemberState owns equipment, personal resources, skills, character name, and member-specific successor history. Shared amulets can apply to each member without duplicating the campaign's legacy object. Local UI read position remains separate from the campaign's committed story state.

Offline solo should eventually submit commands to the same simulation using loopback. Preserve its saves and gameplay through a migration adapter; do not require Steam initialisation for solo. Keep `RatnaBay.Engine` free of Domain references as required by AGENTS.md.

## 6. Simulation and replication specification

The following numbers are initial engineering targets, not measured requirements.

| Item | Initial target |
|---|---|
| Host simulation | Fixed 30 Hz, 33.3 ms steps, bounded catch-up |
| Client input sending | 30 Hz, including recent movement input redundancy |
| Host snapshots | 15 Hz per connected client |
| Rendering | Independent; target 60 FPS on the selected baseline machine |
| Remote interpolation | Start at 100 ms; adapt within roughly 67–150 ms |
| Transport | Reliable ordered control/events; unreliable sequenced movement/snapshots |
| Application snapshot chunks | Aim at or below 1,000 bytes; split large state, never assume one whole-world packet |
| Baseline interest set | Party + active room + relevant nearby projectiles/doors |
| Temporary extrapolation | At most 100 ms, then stop extrapolating and indicate poor connection |

Use an accumulator independent of render FPS and GNS callback polling. Limit catch-up to three ticks in a frame; log overload and pause/recover explicitly if persistently behind rather than silently allowing a client to choose elapsed time. Validate 30 Hz collision with swept projectile tests. If the combat feel requires 60 Hz authority, measure its host cost before changing the target.

**Commands:** protocol version, session epoch, member association, increasing input sequence, estimated host tick, bounded move axes/look, buttons and action IDs. Bind identity to the authenticated connection; never trust a client-supplied MemberId on its own. The host computes speed, collision, hits, cooldowns, resource spend, loot and progression. Clients request an attack, not “deal 500 damage.”

Predict local movement and immediate weapon animation. Reconcile local body position from the host's last processed input sequence and replay remaining inputs. Shared movement math must use compatible collision data. Smooth small corrections, snap major illegal movement; never predict permanent loot or kills. Remote characters and enemies interpolate snapshots.

For v1, resolve melee and projectiles using host state. No competitive rewind system. Acknowledge that high latency can make hits less responsive; assess at 150 ms RTT before committing. Input buffering and local effects help but do not eliminate round-trip delay. Rewind is a later addition only if testing demands it.

Snapshots include entity IDs, tick, positions, health/resource values, animation/status state, current target, run/room state and authoritative action acknowledgments. Use full snapshots initially with bounded interest sets; delta compression can follow measurement. Scene changes, spawns, despawns, purchases, revives and banking are reliable events with IDs. Consumers deduplicate them; snapshots repair lost transient state. Do not play effects twice when a snapshot acknowledges a predicted action.

Specify a small schema, not general object serialisation. Validate lengths, enum values, finite coordinates, object counts and per-peer message rates before allocation. Use bounded queues. Do not deserialize arbitrary types or accept file paths over the network. Record disconnect reasons and bandwidth without exposing credentials.

### World construction and scene transitions

For the first working version, the host generates the authoritative manifest and sends it in bounded reliable chunks, with a content hash, generator version, seed, tier and segment index. Clients acknowledge construction and collision readiness. Later seed-only reconstruction is an optimisation, allowed only with identical build/content hashes and a hash check; generated geometry alone still does not synchronise combat.

Host spawns enemies only after the party enters through a committed room transition. Persistent IDs include campaign/run/segment identity so appended rooms cannot reuse a killed entity's ID. Send door states and active status effects, not merely the seed.

Freeze world simulation during a group scene load, continue networking, and start only after connected participants acknowledge readiness. Use a 30-second load timeout with a clear retry/leave choice. Do not let the fastest machine wake enemies while someone is loading.

## 7. Session lifecycle and failure policy

Flow: **Host/Join -> lobby -> protocol/content handshake -> member record -> full state -> ready -> yard -> descent.** Reject incompatible versions before entering the game with a useful “update required” message. Reserve four slots including the host; distinguish waiting guests from active members.

No fresh joining mid-descent. Existing members reconnect using authenticated identity, not a name or lobby index. On a connection loss, stop their input after 250 ms, keep their body vulnerable, and reserve the slot for 90 seconds. The host continues to simulate status, inventory and downed state. Reconnecting restores that exact state; it grants no healing, invulnerability, fresh loot or reset cooldown.

If the reservation expires, the character remains an incapacitated expedition member until the room resolves, then is marked withdrawn. Do not reduce the current room's difficulty or recalculate its payouts. Adjust only future rooms at an explicit boundary. If no connected standing human remains after the grace period, resolve a party wipe; a disconnected body must not keep the expedition alive indefinitely.

On host loss, every guest returns to the menu with the last checkpoint revision displayed. The rendezvous service must not elect a replacement simulation host. Stop or recreate the lobby; do not claim host migration. New host attempts cannot silently resume someone else's campaign.

## 8. Saves, progression, and reward ownership

Use a separate host-owned co-op save slot. A guest's character is stored in that campaign by authenticated store-independent profile ID, so returning guests recover the same gear and name. New guests receive a campaign-appropriate starter kit, not imported high-level equipment. Their solo campaign is untouched. Tell guests plainly: **“Progress stays in this host's campaign.”** This is simpler but may reduce the feature's appeal; validate it with players.

Save one versioned transaction containing shared story/legacy, the member roster and personal records, banked resources, run state, world changes, checkpoint revision and applied transaction IDs. Maintain backup and atomic replace as in the existing save approach. Migrate old solo saves separately; never overwrite them to try co-op.

Checkpoint at yard departure, each cleared room before opening the next, banking/wipe, and major story commits. A mid-room host crash restores the previous complete checkpoint, rolling back that room's inventory use and rewards together. Disclose this bounded rollback; no arbitrary mid-combat suspend is required for v1. This cannot prevent a dishonest host deliberately restoring backups, which is acceptable for private PvE with no competitive economy.

The host applies a reward transaction once, commits the revision, then acknowledges it. Retrying a purchase/bank request with the same ID returns its recorded result. Never let both an enemy death callback and a replicated death grant XP. No cross-campaign exports means a rollback cannot duplicate rewards into a different save.

One shared bank and run pot fund party entry/camp costs. Each member has a personal combat reserve and inventory. Use per-member consumable allowances allocated in the safe yard rather than letting four clients race to spend a shared shop balance. Final purchases remain host-validated. Major shared spending is host-confirmed after a visible proposal. No item trading in v1.

## 9. Team rules that preserve the run loop

### Doors and banking

Do not close a door when the first player crosses it. At a cleared exit, use **Bank / Press / Not ready**. Press requires all connected active members ready in the gather zone. Any member can request Bank; the host confirms it, and cannot force Press over a member's Bank vote. No timeout defaults to risking the pot. Disconnects use the lifecycle policy rather than counting as automatic consent.

Once committed, place everyone at non-overlapping safe entrance positions, acknowledge the room, close the prior door, and awaken the encounter. No player-player body collision or friendly projectile obstruction in v1, to avoid narrow corridors becoming griefing tools. Retain enemy and world collision.

### Downed, revival and succession

- Co-op characters at zero health become downed, not immediately replaced. Start with a 25-second bleedout, 3-second uninterrupted revive at 2 metres, and 35% health restored.
- Each member can be revived once per room; a second down or bleedout means incapacitated until a cleared-room rescue or wipe. No multiple revivers accelerating the timer. Damage to the reviver interrupts; the downed body is not an aggro target.
- A cleared-room rescue restores the incapacitated member at 25% health, with unchanged prana and no refunded items. This avoids long spectating in ordinary rooms. Review the cost if deliberate rescue cycling becomes optimal.
- If all remaining eligible members are downed/incapacitated, wipe immediately: no waiting through four timers and no free self-revive. Offline solo keeps its existing death rules.
- Actual succession happens at party wipe, once for each active expedition member. Shared story/rank/amulets persist; personal death losses follow an explicitly tested member rule. Use one recoverable party cache to avoid four separate recovery errands.
- Disconnect is never narrated as death by itself. A continuing party can withdraw the missing member at the next safe boundary.

## 10. Narrative, conversation and menus

The order remains the protagonist. NPCs address “your crew” and acknowledge named members, rather than making four people each the chosen rescuer. All humans can help in the relief; any NPC support and task duration scale so the ending remains solo-completable. Do not require four simultaneous switches.

Optional lore is local and replayable. Reading it does not pause another player's fight or teleport them. Major scenes start only in a safe shared phase, send a stable beat ID, show the same choice outcome, and commit shared state once. Each player advances text locally; the next stage waits for readiness. Host selects campaign choices after non-binding party votes; disclose that rule. Skipping a major scene requires all participants ready. A host can remove a disruptive guest, but that is a visible session action, not an invisible timer skipping their text.

Combat inventory, settings, Steam overlay and a guest's pause menu do not pause authority. In the safe hub, an explicit host pause stops simulation but never heartbeats or callbacks. Cosmetic hitstop is local. Protect story pacing with safe scene phases instead of forcing everyone to read during a rescue timer.

Add health/downed indicators, names and distinguishable outlines/icons for three teammates, revive progress, ping target and distance, connection quality and a ready-state display. Do not depend on colour alone. Use existing sprite presentation initially with recognisable equipment and a few action poses; remote players need their own readable assets and animation work.

## 11. Initial balancing model

All numbers below are starting hypotheses, not proven balance. Apply co-op scaling **after** current tier/depth scaling, once. Let N be the roster committed at room entry; a down does not reduce N. A departed member changes the next room only. Solo uses the current baseline.

| Players | Ordinary enemy count factor | Ordinary HP factor | Approx. total HP work | Boss HP factor | Enemy damage factor |
|---:|---:|---:|---:|---:|---:|
| 1 | 1.00 | 1.00 | 1.00 | 1.00 | 1.00 |
| 2 | 1.35 | 1.25 | 1.69 | 1.80 | 1.00 |
| 3 | 1.65 | 1.45 | 2.39 | 2.60 | 1.00 |
| 4 | 1.95 | 1.65 | 3.22 | 3.40 | 1.00 |

Round counts deterministically with the host's room RNG. Keep a strict simultaneous-enemy cap based on actual floor area, initially 12; stage surplus enemies in telegraphed waves. Room clearance requires the spawn queue empty as well as no living enemies. Avoid multiplying count, HP and damage by party size simultaneously. Four-player teams should feel stronger, but not erase every encounter before an enemy acts.

Bosses need target switching and spatial pressure, not fourfold damage. Choose reachable standing players, retain target briefly to avoid jitter, and telegraph changes. Initially limit committed melee attackers to two per player; ranged attacks have a separate readable pressure budget. Avoid permanent nearest-player targeting that lets three people attack without risk.

### Crowd control and build interactions

Audit combined stagger before tuning HP. Multiple maces/Arc casts can otherwise prevent a boss acting forever. Proposed co-op-only boss rule: after 1.5 cumulative seconds of hard stagger in a rolling 4-second window, show a visible 2-second resolve period that reduces incoming stagger duration to 25%. Damage still lands. Test whether this is legible before shipping. Ordinary enemies retain generous stagger; do not remove satisfying teamwork everywhere.

Chill uses strongest effect rather than multiplying four slows. Burns have explicit source/refresh rules; choose strongest burn plus refreshed duration initially, not four unbounded stacks. Credit the relevant attacker for skills but award the kill once. Vessel-like refunds must follow a declared per-member rule—start with the killing member only—and compare it with assist-based alternatives during balance tests. Do not let one kill invoke every personal reward callback accidentally.

### Economy and XP

Use a shared campaign bank. To supply more combatants without accelerating shared rank four times, separate **resource reward** from **service credit**:

- Let B be a room's original stone payout (room number × tier). Actual party payout starts at `round(B × [1 + 0.75 × (N − 1)])`; four players receive about 3.25× resources for the shared budget.
- Track B separately as pending service credit. A successful bank commits that original credit once; a wipe forfeits it. Boss service bonus likewise counts once. Additional loot, member allowances and sales do not multiply rank credit.
- Entry and trader-call fees scale by `1 + 0.5 × (N − 1)`, calculated at the appropriate commitment point. Item prices remain consistent; consumable quantities grow with party needs.
- XP for extra co-op spawns is normalised: sum the baseline encounter XP and share an equal award of that baseline total to every eligible member, once. Do not divide it four ways and do not award each member the expanded encounter's entire XP.
- Equipment rewards are selected once and assigned explicitly at a safe point. Duplicate equipment rolls are not automatically four permanent purchases. Run-only stones may be offered per member, bounded by the same eligibility roster.

This changes the current “stones actually banked” meaning of service progression, so implement a separate named service-credit field and explain it in the results screen. Do not silently make the rank UI claim a physical inventory count it no longer measures. Use host-side fractional accumulation for low payouts if rounding distorts early rooms.

Eligibility is captured at room entry. New spectators receive nothing; active members who become downed still qualify. Temporary disconnects retain rewards in their host record. Leaving/rejoining never creates a second member or a new grant. Freeze the current room's payout factors even if someone departs.

### Pacing and success criteria

Compare matched solo, duo and four-player parties at the same progression. Initial goals: normal co-op room clear time around 75–110% of solo, boss time 80–120%, and permanent progression per hour within roughly 20% before story-gate tuning. These are evaluation bands, not timers enforced on players. Experienced friends can finish faster than the 8–10-hour solo target; do not add artificial waiting to prevent that.

Measure room time, downs/revives, consumables per member, damage and kill shares, boss time unable to act, bank frequency, and story skips. Check mixed gear/skill groups and the weakest player's ability to contribute. If four-player rooms are too easy, improve pressure and target distribution before increasing damage.

## 12. Technical budgets and verification equipment

These are engineering budgets to validate, not published hardware minimums. Target the host's 30 Hz world step below 10 ms at the 95th percentile with four players, a boss, the active enemy cap and worst-case projectiles. Measure graphics separately; the host renders as well as simulates. Choose the baseline CPU/GPU from the existing single-player performance pass, then derive multiplayer requirements from profiling.

Example bandwidth budget: a 1.5 KB logical snapshot at 15 Hz is 22.5 KB/s per client before overhead, or 67.5 KB/s host outbound for three guests. Reserve up to 50 KB/s steady gameplay outbound per guest as an initial ceiling, about 1.2 Mbit/s aggregate for three; cap larger reliable manifest transfers separately and prioritise inputs. These are arithmetic budgets, not packet captures. Measure actual GNS transport overhead and direct/TURN relay behaviour.

Test four separate machines/profiles for the internet gate, including mixed itch.io and Steam builds. Four local processes are useful for simulation, but cannot establish NAT traversal, relay reliability, focus behaviour, or real guest feel. Include Wi-Fi, distinct home networks, restrictive NAT, and a lower-performance host. Investigate 0/50/100/150/250 ms RTT, jitter up to 30 ms, 1/3/5% packet loss, temporary outage and reconnect. Aim for good play at 150 ms RTT and 3% loss; beyond that, fail gracefully rather than promising equivalent feel.

## 13. Implementation phases and acceptance gates

| Phase | Deliverable | Must prove before proceeding |
|---|---|---|
| 0 — Transport spike | Two machines exchange authenticated commands/state through standalone GNS; loopback harness, signalling and relay spike | Packaged native loading, invites, rejection, relay-capable connection and clean teardown |
| 1 — Ownership | Party/campaign split, stable IDs, host simulation, solo adapter | Existing solo save and domain behaviour preserved; two simulated members do not duplicate rewards |
| 2 — Movement | Two players in one test room; snapshots, prediction, remote sprites | No host-menu pause bug; tolerable correction under 150 ms RTT; no illegal movement accepted |
| 3 — One fight | Host AI/hits/resources, two-to-four players, down/revive | Same enemy health and outcome on all peers; simultaneous kills and duplicate requests award once |
| 4 — Full descent | Party doors, bank decision, append-room sync, reconnect | Four people finish a real descent; nobody locked outside; no spawn/loot divergence |
| 5 — Persistence and story | Separate campaign saves, returning guests, safe group scenes | Host crash rollback coherent; solo untouched; shared beats fire once |
| 6 — Balance | Party-size profiles and pressure tuning | Solo/duo/four-player metrics and mixed-skill feedback meet agreed goals |
| 7 — Release hardening | External mixed-store sessions and packaged regression | Long-session stability, network-failure matrix, acceptable invite success and understandable recovery |

Planning allowance for one experienced developer: roughly **14–26 focused engineering weeks** across these phases, with additional calendar time for external testing, remote-player art, narrative content and iteration. This is an estimate from the scope, not a quote or measured velocity. The initial spike should take roughly 2–3 weeks and determine whether the larger investment is justified. Do not promise launch timing from this estimate.

After code changes, use the repository's `verify.ps1` and packaged gate as required by AGENTS.md. Add meaningful tests for authority, malformed/duplicate commands, version mismatch, snapshot recovery, simultaneous damage, revival races, disconnects, room barriers, reward transactions and save migration. Add a headless multi-peer simulation harness with an in-memory transport that injects loss, latency, duplication and reordering. Existing console scripts alone do not cover lobbies, networking, story panels or multi-account sessions.

No gameplay build/test was run for this document-only proposal. No service deployment, native library installation, live multiplayer connection, or performance measurement was performed.

## 14. Decision to make after the spike

Proceed only if two-player combat feels good over ordinary internet connections, the authoritative split remains manageable, and external players actually prefer returning with friends. Then prove four players in one complete descent before expanding campaign integration.

If that gate fails, keep the completed solo architecture improvements and defer co-op. If it succeeds, update the release scope and the narrative suggestions explicitly. The strongest pitch is simple: **go down together, decide together, bring your crew home.**

