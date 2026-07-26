# Prototype hardening plan

Goal: turn the Iliac Bay demo into a prototype that is **safe to change** — version
controlled, compile-verified, with one source of truth for world data and no
gameplay logic keyed on GameObject names.

Status legend: `[ ]` todo · `[~]` in progress · `[x]` done · `[!]` blocked/needs Unity

---

## Constraints this plan works under

- **The Unity editor is open**, so `-batchmode` can't take the project lock.
  Verification is via `python Tools/compile-check.py --editor`, which runs the
  Roslyn compiler shipped with Unity 6000.5.3f1 against the generated csprojs.
- **`Assets/Scenes/Main.unity` is a baked artifact** (2074 objects) produced by
  *Elder Scrolls 6 → Presentation → Setup Menu + Cutscene + Smooth Map*, which
  deletes every root object before regenerating. Therefore **every runtime fix
  must work on the already-baked scene**, not only on a freshly generated one.
  That rules out "assign layers in the generator" as the only mechanism — hence
  the `WorldTagger` runtime pass below.
- Fixes are code-only. Anything needing a scene rebuild or editor action is
  marked `[!]` and listed under *Needs a Unity session*.

---

## Phase 0 — Repository  ✅

- [x] `.gitattributes` — Unity YAML as text + `unityyamlmerge`, Git LFS for
      binary art (fbx/obj/png/jpg/wav/ttf/dll/zip)
- [x] `.gitignore` — added `_downloads/` (239 MB of raw downloads was untracked
      but not ignored), compile-check output
- [x] `git init` + LFS + remote `origin` → `dattaprasad-r-ekavade/es6-iliac`
- [x] Initial commit of existing state (3818 files, 1785 via LFS)
- [x] `Tools/compile-check.py` — compile without opening Unity
- [x] Push to origin

## Phase 1 — Foundations (single source of truth)

- [x] **`WorldLayout.cs`** — one static description of the world: water level,
      landmasses, cities, POIs, spawn points, safe zone. Replaces coordinates
      hand-copied into 5 files.
- [x] **`GameLayers.cs`** — named physics layers + query masks (Ground, Water,
      Void, Character, Prop) replacing `~0` and `name.Contains(...)` tests.
- [x] **`TagManager.asset`** — declare the layers so they exist in the editor.
- [x] **`WorldTagger.cs`** — `[RuntimeInitializeOnLoadMethod]` pass that assigns
      layers to generated world objects once per scene load. This is now the
      *only* place object names are interpreted, and it makes the layer-based
      fixes work on the existing baked scene without a rebuild.

## Phase 2 — Critical gameplay bugs

- [x] **`SnapToWalkable` ignored its argument** — returned the Daggerfall spawn
      pad whenever that pad existed, so every NPC and enemy spawned in one pile.
      Split into `SnapToGround(pos)` (raycast at that XZ, Ground mask) and
      `GetPlayerSpawn()`; updated all 5 call sites.
- [x] **Coastline teleported the player home** — guard fired at `y < 8` while
      terrain edges fall to `y = 2`. Now keyed to `WorldLayout.WaterLevel` and a
      real ground probe.
- [x] **Out-of-bounds check was name-matched `RaycastAll` over 900 m** every
      0.35 s → single layer-masked `Raycast`, with a grace period so a legitimate
      jump/fall isn't instantly rewound.
- [x] **Roads were single 4.2 km stretched cubes at fixed Y** (visibly flying
      through the sky) → segmented and projected onto the terrain.
- [x] **Melee/interact/prompt raycasts used `~0`** and could hit terrain, props
      and friendly NPCs → layer-masked queries.
- [x] Enemy melee could hit through walls → line-of-sight check before damage.
- [x] **The bandit camp and coastal ruin were authored in open water** — (-1750, 850)
      and (-2200, 700) are both several hundred metres off the southern edge of the
      Daggerfall peninsula. The spawn bug had hidden this completely. Moved onto the
      Glenumbra coast, outside the safe zone (986 m and 1379 m from the start plaza).
- [x] **The landmasses are disconnected islands** (Daggerfall ends at x≈-1559, Glenumbra
      starts at x≈-1380). The roads were the only link and they floated at a fixed
      y≈24. Road sections crossing water are now causeways at a fixed deck height, on
      the Ground layer, so the bay is actually walkable end to end (4.4 km route).
- [x] **Terrain height is Perlin noise**, so an authored coordinate inside a landmass
      can still be below sea level → `PlaceOnLand` spirals outward for a dry spot
      before spawning anything.
- [x] Fast travel always charged the 0.5 h minimum: distance was measured *after* the
      teleport, so it was always ~0.
- [x] `QuestSystem` subscribed to discovery events in both `OnEnable` and `Start`,
      handling every discovery twice.

## Phase 3 — Robustness & performance

- [x] **639 foliage props each ran their own `Update` + `GameObject.Find`** →
      one time-sliced `FoliageCullingSystem`; the per-prop component is now a
      registration-only data holder (keeps the baked scene valid).
- [x] **Player reference resolved by `GameObject.Find("Player")` in 8 places**,
      several per-frame → `PlayerRef` service with a cached transform.
- [x] **Camera far plane 1000 on a 6800-unit world** → set from `WorldLayout`.
- [x] **`GameFlowController._started` never cleared**, so every jump press during
      gameplay set `_skipRequested` → explicit `IsInGameplay` state.
- [x] **`GameHud.ShowDialogue` called `StopAllCoroutines()`** on the HUD → single
      tracked coroutine handle.
- [x] **Save had no version and no quest state** → `Version` field, quest
      progress, weather, and world-spawn flag; killed enemies stay dead on load.
- [x] Statics not cleared on scene reload (`ReturnToMainMenu` reloads the scene)
      → `OnDestroy` null-out on every singleton.
- [x] `[RuntimeInitializeOnLoadMethod]` fires once per *play session*, not per scene
      load — so the tagger and the culling system would have silently stopped working
      after "Main Menu". Both now also hook `SceneManager.sceneLoaded`.
- [x] Enemy hits spammed one toast per swing → target health bar in the HUD.
- [x] Save/load wrapped in try/catch; a corrupt or older file now reports instead of
      throwing mid-frame.

## Phase 4 — Cleanup

- [x] Delete `IslandWorldGenerator.cs` (351 lines, zero references)
- [x] Delete dead duplicate `PlayerSafetyGuard.IsInDaggerfallSafeZone`
      (a second safe-zone radius that disagreed with `WorldSafeZone`)
- [x] Assembly definitions (`Game.Runtime`, `Game.Editor`) so a script edit stops
      recompiling the whole project
- [x] Update `README.md` / roadmap claims that no longer match the code

---

## Verify these in the editor (first play-test after this pass)

1. **Re-run the world rebuild** — *Elder Scrolls 6 → Systems → Install P0+P1 + Rebuild
   World*. Required for the causeway roads and the relocated POIs; the runtime fixes
   (spawning, layers, culling, save) work on the current baked scene without it.
2. **Confirm the NPCs are spread out** — one merchant, one guard and one quest giver
   around the Daggerfall plaza; greeters at Wayrest and Sentinel; three bandits at the
   Glenumbra camp. If they're still in a pile, the tagger didn't run.
3. **Walk the Daggerfall → Wayrest road** end to end; the causeway sections should be
   walkable and the safety guard should not fire while crossing.
4. **Check POI terrain** — the camp and ruin snap to noise-generated terrain, so their
   exact spots are worth eyeballing even though `PlaceOnLand` guarantees dry ground.

## Needs a Unity session (cannot be done headless while the editor is open)
2. **Prefabs.** Player / Enemy / NPC / GameSystems / HUD are still built by
   `AddComponent` at runtime, so values can't be tuned without editing C#. This is
   the biggest remaining iteration-speed problem and needs editor authoring.
3. **Texture budget.** PolyHaven 2K JPEGs are 44.6 MB of the ~60 MB the scene
   pulls in; set max size 512–1024 in the importer.
4. **Test assembly.** `com.unity.test-framework` is installed with no tests; the
   asmdefs added here are the prerequisite.

## Deliberately not done

- **Physics collision matrix** left at "everything collides". The new layers are
  used for *query* masks only, which is where the bugs were. Changing the matrix
  without being able to playtest risks silently dropping collisions.
- **ScriptableObject-ising world/quest data.** `WorldLayout` is a static C# class
  rather than an asset: creating `.asset` files by hand is fragile, and this
  already removes the duplication. Converting to ScriptableObjects is a natural
  follow-up once someone is in the editor.
- **UI rebuild (TextMeshPro / prefabs).** Too large to do blind; needs visual
  iteration.
