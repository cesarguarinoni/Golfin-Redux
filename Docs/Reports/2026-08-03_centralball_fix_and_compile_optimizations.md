# CentralBall device fix + compile-time optimizations — session write-up

**Date:** 2026-08-03 · **Branch:** main · **For:** architect review

**Commits landed (pushed to origin/main):**

| Commit | Summary |
|---|---|
| `1a4ad15ca` | fix(shot_ui): CentralBall invisible on device — stale CanvasGroup alpha=0 in LabScaffold |
| `4db19e7fc` | chore(compile): remove unreferenced sample/demo scripts from Assembly-CSharp |
| `35beb2723` | perf(build): faster iOS iteration — incremental il2cpp + Dev-iOS development build |

---

## Part 1 — CentralBall invisible on device (bug fix)

### Symptom
The 2D golf ball at the centre of the shot UI (`CentralBallWidget`) rendered **blank on device (iOS)** on hole entry. Opening the map view and closing it made it appear. The ball-selector button (bottom-left) rendered fine throughout. Not reproducible in the editor.

### Investigation (how the field was narrowed)
The original hypothesis was a "null-sprite latch" in `RefreshSprite()` (`_image.enabled = sprite != null`). I falsified it with a discriminator-first approach rather than patching the suspected line:

1. **The `Resources.Load` fallback resolves** (importer is Sprite) → `sprite` is essentially never null in-editor → the enabled-latch cannot blank it. H1 dead in the editor.
2. **`ShotController.PublishState` fires `Idle` every frame** and the GameObject is authored active → a simple "stuck inactive" (H3) also can't persist.
3. **Warm real-flow hole entry in the editor rendered the ball correctly** (active, image enabled, real sprite, BallContext hydrated) — a genuine non-reproduction.
4. Added a one-shot **render-state diagnostic** (CanvasRenderer alpha/cull, Image color, RectTransform geometry, full ancestor `CanvasGroup`/`Canvas` chain) and reproduced on the **iOS Simulator** (which applies device safe-areas). Result:

   ```
   crAlpha=1.00 crInherited=0.00 cull=False imgColor=(...,a=1.00) worldArea=22500
   CHAIN CentralBall(self=True, CG.a=0, blocks=True) < ShotUI_Canvas(...) < LabRoot(...)
   ```

   Everything healthy **except `crInherited=0.00`** — a `CanvasGroup` with `alpha=0` **on the CentralBall GameObject itself**.

### Root cause
`LabScaffold.unity` had an **authored `CanvasGroup(m_Alpha: 0)` baked onto the CentralBall GameObject** (fileID 2200000006 on GO 2200000001). It was almost certainly saved by accident during a `MapViewController` suppression session — the map adds exactly that CanvasGroup and sets it to `0` on open, `1` on close. That is precisely why opening/closing the map "fixed" it (`RestoreShotUIChrome` sets alpha back to 1). The **editor happened to mask it**; the iOS player rendered it faithfully invisible.

`PhysicsLab_Hole1.unity` (the other authored scene) has no such CanvasGroup — the defect was unique to LabScaffold, the shipping-flow scene.

### Fix
- **`LabScaffold.unity`: CentralBall CanvasGroup `m_Alpha 0 → 1`.** The map still manages 0/1 at runtime; `CentralBallWidget` deliberately never touches alpha, so nothing fights the map's suppression.
- **`CentralBallWidget.cs`: added a serialized `_defaultThumbnail` fallback** (parity with the working `BallButtonWidget` selector) so a null `BallContext` can't blank the ball independent of `Resources`. Not the root cause, but low-risk hardening; wired to `S_Controls_Ball_GOLFIN` in LabScaffold.

### Verification
Built and ran on the **iOS Simulator** (iPhone 14) via the real player flow (title → PRACTICE → Hole 1). Post-fix render dump: `crInherited 0.00 → 1.00`, `CG.a=1`; the ball renders on hole entry **without ever opening the map**. Confirmed both by the log and a screenshot.

### Note for review
The LabScaffold diff also carries two incidental null-default fields (`holeTeesCsv: {fileID: 0}`, `_resultModal: {fileID: 0}`) that Unity wrote when the scene was saved — harmless no-ops (newer serialized fields being written out), flagged for transparency.

---

## Part 2 — Compile-time optimizations (done)

### Core finding
The project has 691 `.cs` files and 46 asmdefs, but **~220 runtime files still compile into the default `Assembly-CSharp`** (+123 editor files in `Assembly-CSharp-Editor`) — roughly half the codebase in the two predefined monolithic assemblies. Because `Assembly-CSharp` auto-references every asmdef, it recompiles as a downstream dependent on nearly every code change. **Shrinking it is the highest-leverage compile-time lever.**

### 2a. Removed unreferenced sample/demo scripts (`4db19e7fc`)
Deleted self-contained sample content that no shipping scene/prefab/script references (verified by GUID search); project recompiles clean:

| Removed | Notes |
|---|---|
| `Assets/TextMesh Pro/Examples & Extras` | TMP sample scripts + scenes + assets (~300 files; ~34 scripts out of Assembly-CSharp) |
| `Assets/Scripts/Editor/Archive` | Dead one-shot UI builder/patcher editor tools (15 files) |
| `Assets/Packs/TreePackVol.1/Scripts` | Vendor demo script |

Tree-pack **models/prefabs used by the holes were untouched** — only demo scripts. (`Assets/Packs/Mobile_Tree_Bundle/Scripts` was also deleted locally but is under a gitignored path, so not in the commit — helps local compile only.)

### 2b. iOS il2cpp build-profile iteration settings (`35beb2723`)
Targets the ~5-min iOS `GameAssembly` (il2cpp C++) compile that dominates device build time:

- **`ProjectSettings.asset`: `incrementalIl2cppBuild → { iPhone: 1 }`.** il2cpp now caches generated C++ between builds, so repeat iOS builds recompile only changed translation units. Zero runtime impact; applies to all iOS profiles. (First build after enabling is still full; subsequent builds are much faster. Rare caveat: an incremental build can go stale after a big refactor — a one-off clean build fixes it.)
- **`Dev-iOS` build profile: `m_Development 0 → 1`.** The iteration profile now builds as a development build → uses il2cpp's **Debug** compiler configuration (faster C++ compile) and enables the profiler / on-screen dev console. **Scoped to Dev-iOS only; the shipping `iOS-Full` profile is untouched.** Behaviour change to be aware of: Dev-iOS device builds now show the dev-console overlay and carry debugging hooks (expected for an iteration profile).

### Reusable win discovered (tooling)
An **end-to-end iOS Simulator build/run/verify pipeline** was established (I can now reproduce device-only bugs myself, no manual build&run):
- Two inherent stages: Unity export (`BuildPipeline.BuildPlayer`, ~45s) → `xcodebuild` (first build ~5 min).
- **Fastest path for a scene/asset-only change:** swap the changed `Data/level<N>` into the already-built `.app` and relaunch — **seconds, no recompile** (used to verify the CentralBall fix).
- **For code changes:** Unity append mode (`BuildOptions.AcceptExternalModificationsToPlayer`) preserves file mtimes so `xcodebuild` stays incremental instead of recompiling all of GameAssembly.

---

## Part 3 — Pending proposals (for architect review)

Ranked by value/effort. None of these are started.

### P1 — Extract `Golfin.Core` + subsystem asmdefs  ·  highest structural value, medium effort
Split the big cohesive UI subsystems out of `Assembly-CSharp` so an edit inside a subsystem recompiles a small assembly instead of the 220-file monolith, and the monolith itself shrinks.

- **Evidence (files currently in `Assembly-CSharp`, no asmdef):** Inventory 31, Roster 17, Gacha 15, Shop 10, Tournaments 9, Rankings 6, HoleSelection/ModeSelect/Account 5 each, HUD 5.
- **Required sequencing:** an asmdef **cannot reference `Assembly-CSharp`**, and these UIs all call managers (`CharacterManager`, `ClubManager`, `BagManager`, `BallManager`, `ItemManager`, `RewardPointsManager`, `ScreenManager`, `PersistentUIManager`, `FadeController`) that live in `Assembly-CSharp` root. So **first** create a `Golfin.Core` assembly for the managers/singletons; **then** each subsystem can reference it and leave the monolith.
- **Impact:** compounding — smaller monolith recompiles on every edit + subsystem edits recompile only ~15–31 files.
- **Risk/caveat:** watch for circular references (a manager and a subsystem referencing each other → the shared interface has to move into `Golfin.Core`). Do it incrementally, one subsystem at a time, starting with the most self-contained (Inventory / Roster / Gacha / Shop). Mirrors the existing `Golfin.Inventory` / `Golfin.Roster` namespaces and the already-done `Golfin.Localization` extraction.

### P2 — Enable `DisableDomainReload`  ·  largest *daily* editor win, but gated on prep
Turning domain-reload off makes Play-mode entry near-instant (skips the multi-second assembly reload). **Not safe as-is.**

- **Current:** `EnterPlayModeOptions = 2` (scene reload disabled, domain reload still on).
- **Blockers found:** the codebase relies on domain reload clearing statics. ~25 static events across ~14 classes (`GameSession` has 5; plus the `BallContext`/`ClubContext`/`HoleContext`/`WindContext`/`ShotModeContext`/`MatchContext`/`SpinContext`/`PlayerContext` HUD family, `ScreenManager.ScreenChanged`, `ModalController.ModalStackEmptied`, `SfxBus.OnPlay`, `ClubSelectionBroadcast.OnClubChanged`) would retain stale subscribers; ~23 `static Instance` singletons could carry state or misfire the `if (Instance != this) Destroy(...)` guard against a destroyed prior-session object. One editor script (`MapViewCaptureBotMenu.cs:38`) explicitly documents depending on the reload clearing subscriptions. Only 10 files use `[RuntimeInitializeOnLoadMethod]` today (`SfxBusReset` does it correctly as the pattern to follow).
- **Prep required first:** add `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` resets to the ~14 context/session classes (null out subscriber lists + re-init fields — most already have a `Reset()` that only resets data, not subscribers) and audit the ~23 singletons.
- **Impact:** seconds off every Play-mode entry, dozens of times a day. Bounded, mechanical prep.

### P3 — Remove `com.unity.visualscripting`  ·  low effort, low risk
`manifest.json` pins `com.unity.visualscripting 1.9.9`; **0 code references, 0 ScriptGraph/StateGraph assets.** It ships editor assemblies + a codegen step that loads on every domain reload. Removing it (via Package Manager) trims editor-side reload churn. Also worth reviewing `com.unity.postprocessing 3.5.1` — URP 17 has its own post-processing stack; remove if the legacy one is unused. (Held this session only because `manifest.json` had uncommitted WIP at the time — now clear.)

### P4 — `Il2CppCodeGeneration = Faster (smaller) builds` on iteration profiles  ·  small, per-profile
`OptimizeSize` generates less C++ (shared generics) → faster C++ compile. **Must be per-profile** — set globally it slows shipping runtime. Marginal gain on top of the incremental-il2cpp + Dev-build changes already landed; deferred as it needs a per-profile PlayerSettings override. Low priority.

### P5 — Split the 123-file `Assembly-CSharp-Editor`  ·  editor-only, medium value
Editing any one editor script recompiles all 123. Give standalone tools their own editor asmdefs — `Assets/Scripts/Editor/CourseImporter` (19), `Assets/Scripts/Editor/CanvasScalerMigration` (8), `Assets/Scripts/UI/Editor` (12). (`Editor/Archive` was already deleted in `4db19e7fc`.)

### Confirmed NON-issues (checked, no action)
- **No Roslyn analyzers / source generators** in the project (a common compile-time culprit — confirmed absent: no `csc.rsp` / `Directory.Build.props`, `allowUnsafeCode: 0`).
- Asset **Cache Server is off** — enabling it speeds asset *reimport*, not compile; a team-level call, not a compile lever.
- The Unity MCP `RecompileGate` re-adds `UNITY_MCP_READY` to all targets on every editor load (known — memory `project_mcp_define_auto_readded`); contributes editor-side churn but is tied to the "remove MCP package at build time" plan already tracked.

---

## Suggested execution order for the pending work
1. **P3** (remove visualscripting) + **P5-CourseImporter/CanvasScalerMigration** asmdefs — quick, low-risk.
2. **P1** — `Golfin.Core` extraction, then Inventory/Roster/Gacha/Shop asmdefs, one at a time. Biggest structural win.
3. **P2** — the `[RuntimeInitializeOnLoadMethod]` static-reset pass across the ~14 context/session classes, *then* flip `DisableDomainReload`.
4. **P4** — optional final squeeze.

Each of P1–P2 is substantial enough to warrant its own spec/task rather than a drive-by change.
