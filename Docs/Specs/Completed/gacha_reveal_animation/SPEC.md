# SPEC — `gacha_reveal_animation`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files.

## Status

See `STATUS.md`. `SPEC_READY` (Architect, 2026-08-31).

## Goal

Give a gacha pull a reveal moment. Today PULL x1 / PULL x10 on a banner card jump straight to
the Prizes screen. After this task: PULL → a **reveal modal** darkens the whole screen (nav bars
included, like every other modal) and shows the golf bag alone; the bag shakes; each prize card
pops out of the bag **one at a time** with particle effects that scale with the card's rarity;
when the last card has been shown the modal fades and the **Prizes screen enters** showing the
same prizes. A SKIP button jumps to the Prizes screen at any point. Sounds go through the existing
`SfxBus` / `SfxLibrary` pipeline with CC0 placeholder clips already in `Assets/Sounds/Gacha/`
(see §5) — Cesar swaps clips later if he wants; the mapping is part of this task.

Decisions of record (Cesar, 2026-08-31):
- Trigger = the banner card's PULL x1 / x10 (`GachaBannerCard.OnPullX1/OnPullX10`). The Prizes
  screen is the **result** screen; its PULL button means **pull again** and runs the same flow.
- x10 = one card at a time, each card replaces the last (no accumulating grid in the modal).
- Pacing = auto-play with SKIP. A tap anywhere on the modal fast-forwards the current card.

## Reference

- **Figma frame:** Golfin Game Redux / `Gacha Animation` / id `13997:4298`, file `5gEAHjl6xAtW8iYY7NMvWd`
- **Reference PNG:** `reference/figma_13997-4298_gacha_animation.png` (1170×2532)
- **Placeholder vs canonical:** the Figma shows the Rewards Center (GACHA tab) *undimmed* behind
  the bag — that is the mockup, not the target. Cesar's instruction is "nav bars and background
  darkened, see other modals for reference": the modal sits on a `ModalScrim` over everything.
  The card in the Figma is a `BagClubCard` (DRIVER G&F, Lv 10) — content is mock.
- **Bag art:** `Assets/Art/Gacha/Bag.png` (454×1303, currently imported as Default texture —
  switch to Sprite (2D and UI), single, mipmaps off; max size 2048 is fine).

## Figma Fidelity (Rule 18)

Positions are in the 1170×2532 frame, origin top-left. Convert to the canvas anchors used by
the other modals (centre-anchored, y up).

| Element | Figma node | Property → value |
|---|---|---|
| Scrim | — (not in Figma) | full-screen, `ModalScrim.MinAlpha` (0.80) black, sortingOrder lifted to 500 by `ModalScrim.Apply` so the PersistentUI bars are covered and untappable |
| Bag | `13997:4501` "Selection" | 350×1005 at (410, 1062) → centre (585, 1564.5); `Bag.png` preserves aspect (454:1303 = 350:1005). Idle scale 1.0 |
| Prize card | `13997:4503` "Frame 52" | 181×374 at (492, 535) → centre (582.5, 722). Reuse `Assets/Prefabs/UI/Inventory/BagClubCard.prefab` at its native 183×410 (scale 1) centred on that point — do NOT rebuild the card. Card's own buttons non-interactable (same as the Prizes screen binding) |
| SKIP button | `13997:4598` "Main Buttons" | 388×120 at (391, 2067) → centre (585, 2127). Gold pill, label "SKIP" — clone the Prizes screen's PULL button (the GO behind `GachaPrizesScreenController._pullButton`, same gold pill family) and relabel with a `LocalizedText` binder (`Assets/Localization/LocalizedText.cs`) on key `GACHA_SKIP` (§Text) |
| Rarity FX | — | Not in Figma; defined in §FX. Particle tints come from `RarityHelper.GetRarityColor(rarity)` — never hardcoded |

## Architecture context

- **Asmdef:** everything new is `Assembly-CSharp` (`GolfinRedux.UI.Gacha` namespace, same as the
  other gacha scripts). `Golfin.Audio.Events` gains enum members only. `QualityTier` /
  `QualityTierService` (`Golfin.Gameplay.UI`) are readable from Assembly-CSharp already.
- **Existing code referenced:**
  - `Assets/Scripts/UI/Gacha/GachaBannerCard.cs` — `OnPullX1()`, `OnPullX10()` (the trigger).
  - `Assets/Scripts/UI/Gacha/GachaPrizesScreenController.cs` — `SetPendingPullCount(int)`,
    `OnEnable`/`ApplyMode`, `BindGridCards`, `BindX1Card`, `BindCard`, `OnPull()` (stub), `_gridCards`.
  - `Assets/Scripts/UI/Gacha/GachaMockPrizePool.cs` — `PrizeRecord`, `GetMockPrizes()`, `GetX1Prize()`.
  - `Assets/Scripts/UI/Gacha/GachaTabController.cs` — its `WirePullButtons` paths
    (`ContentArea/GachaTabContent/PullSection/...`) do not exist in the scene; the live PULL
    buttons are on the banner card. Leave that dead wiring alone (out of scope).
  - `Assets/Scripts/UI/Modals/ModalController.cs` — base class (`Show`, `Hide`, `OnShow`,
    `OnHide`, `OpenModalCount`, `useAnimation` fade via `CanvasGroup`).
  - `Assets/Scripts/UI/Modals/ModalScrim.cs` — applied automatically by `ModalController.Show`.
  - `Assets/Scripts/UI/Inventory/BagClubCard.cs` — `Initialize(PlayerClubData, ClubDataRuntime, string)`.
  - `Assets/Scripts/UI/Inventory/ClubDatabaseCSV.cs` — `Instance.GetClub(id)`; `ClubDataRuntime.rarity` (`CharacterRarity`).
  - `Assets/Scripts/UI/Roster/Managers/CharacterDatabase.cs` — `CharacterRarity` enum, `RarityHelper.GetRarityColor`.
  - `Assets/Scripts/Audio/Events/SfxId.cs`, `SfxBus.Play(SfxId)`, `Assets/Scripts/Audio/SfxLibrary.cs`,
    `Assets/Scripts/Audio/SfxPlayer.cs` (unmapped id → warning + skip, which is the intended
    behaviour while clips are missing), `Assets/Resources/Data/sfx.csv`.
  - `Assets/Scripts/UI/TapFeedbackFX.cs` + `Assets/Resources/UI/TapFeedbackFX.prefab` — the
    existing precedent for `UIParticle` (`com.coffee.ui-particle` 4.12.1, already in
    `Packages/manifest.json`) + `ParticleSystem` inside a Canvas. Copy its setup (UIParticle
    scale 10, PS units ÷ scale) for every emitter here.
  - `Assets/Scripts/Gameplay/UI/ShotUI/Quality/QualityTierService.cs` — `Current` (Low/Mid/High).
  - `Assets/Scripts/UI/ScreenManager.cs` — `ShowScreen(ScreenId.GachaPrizes)`.
- **Tweening:** none in the project (project standard — coroutines, see
  `VersusResultModalController.cs` header). Use coroutines + `Mathf`/`AnimationCurve`
  easing; **`Time.unscaledDeltaTime`** throughout, as `ModalController` does.
- **Scene:** `Assets/Scenes/ShellScene.unity`. Modals are scene instances on the shell
  `Canvas` (e.g. `TournamentSignupModal`). Banner cards are cloned by the carousel, so the
  reveal modal exposes a static `Instance` (project convention) rather than a serialized ref.

## Implementation

### 1. Pull result plumbing (`GachaPullFlow`)

New static class `Assets/Scripts/UI/Gacha/GachaPullFlow.cs`:

```csharp
public static class GachaPullFlow
{
    /// The prizes of one pull, in reveal order. Today built from GachaMockPrizePool;
    /// NOTE: the real server pull plugs in here (blocked on content — not this task).
    public static IReadOnlyList<PrizeRecord> BuildResult(int count)
        => count == 1 ? new[] { GachaMockPrizePool.GetX1Prize() } : GachaMockPrizePool.GetMockPrizes();

    /// Entry point for every PULL button. Opens the reveal modal, then the Prizes screen.
    public static void Pull(int count)
    {
        var result = BuildResult(count);
        var modal = GachaRevealModalController.Instance;
        if (modal == null) { ShowPrizes(result); return; }          // degrade: no modal in scene
        modal.Play(result, onFinished: () => ShowPrizes(result));
    }

    private static void ShowPrizes(IReadOnlyList<PrizeRecord> result)
    {
        GachaPrizesScreenController.SetPendingResult(result);
        ScreenManager.Instance?.ShowScreen(ScreenId.GachaPrizes);
    }
}
```

- `GachaBannerCard.OnPullX1/OnPullX10` → `GachaPullFlow.Pull(1 / 10)` (keep the existing
  null-`ScreenManager` toast fallback inside `ShowPrizes`).
- `GachaPrizesScreenController.OnPull()` (the stub) → `GachaPullFlow.Pull(_pullCount)` — pull again
  with the same count. While the modal is open the Prizes screen stays where it is underneath.
- `GachaPrizesScreenController.SetPendingResult(IReadOnlyList<PrizeRecord>)` replaces the
  mock-pool reads: `BindGridCards` / `BindX1Card` bind from the pending result (count 1 → x1
  mode, otherwise x10 mode; pad/truncate to the 10 grid cards). Keep `SetPendingPullCount(int)`
  as a thin wrapper (`SetPendingResult(BuildResult(n))`) — `GachaTabController`'s dead
  `OnPullX1/OnPullX10` still call it and are out of scope. `_pullCount` is derived from the result length.
- No ticket spend, no history record, no server call — the pull itself is still a stub
  (`Docs/TellCode.md`: gacha rates/content blocked on content). Out of scope below.

### 2. Reveal modal (`GachaRevealModalController : ModalController`)

New `Assets/Scripts/UI/Gacha/GachaRevealModalController.cs`, new prefab
`Assets/Prefabs/UI/Modals/GachaRevealModal.prefab`, one instance on the ShellScene canvas next to
the other modals. Hierarchy:

```
GachaRevealModal                 (ModalController.modalPanel = Panel; backdrop = Scrim)
├─ Scrim                         Image, black, alpha 0.80, raycastTarget ON, Button → OnTapAnywhere
├─ Panel                         CanvasGroup (fade driven by ModalController)
│  ├─ BagGlow                    Image, soft radial sprite, additive material (TapSparkle_Additive.mat), alpha 0 idle
│  ├─ BagRays                    Image, ray sprite (see §FX assets), alpha 0 idle, rotates while active
│  ├─ BagPivot                   empty RectTransform at the bag's BOTTOM centre (0, −801)*; the shake rotates THIS
│  │  └─ Bag                     Image = Bag.png, 350×1005, anchoredPosition (0, +502.5) so its centre lands at (0, −298)*
│  ├─ BagMouthFX                 UIParticle + ParticleSystem (puff / launch burst)
│  ├─ CardAnchor                 RectTransform 183×410 centred at (0, +544)*; card instances parent here
│  │  ├─ CardRays                Image ray sprite behind the card, alpha 0 idle
│  │  ├─ CardBurstFX             UIParticle + ParticleSystem (radial burst on arrival)
│  │  ├─ CardIdleFX              UIParticle + ParticleSystem (ambient sparkle during hold)
│  │  └─ CardRainFX              UIParticle + ParticleSystem (falling glitter, Legendary+)
│  ├─ Flash                      Image white full-screen, alpha 0, raycastTarget OFF
│  └─ SkipButton                 388×120 centred at (0, −861)*, label GACHA_SKIP
```

\* canvas units for a 1170×2532 reference: Figma y → canvas y = 1266 − y_centre.
Bag 1564.5 → −298.5; card 722 → +544; SKIP 2127 → −861. Use the anchors the other modals use
and confirm on the 1170×2532 game view. All FX graphics `raycastTarget = false`.

Public API:

```csharp
public static GachaRevealModalController Instance { get; private set; }   // set in Awake, cleared in OnDestroy
public void Play(IReadOnlyList<PrizeRecord> prizes, Action onFinished);
```

`Play` calls `Show()` (base — scrim + fade in), then runs `RevealSequence(prizes)` as a coroutine.
`onFinished` is invoked exactly once, either at the natural end or on SKIP, **after** `Hide()` has
started. If `Play` is called while a sequence is running, ignore it (log).

Timeline (all durations `[SerializeField]`, defaults below, unscaled time):

| Step | What | Default |
|---|---|---|
| A. Enter | Bag scales 0.6 → 1.06 → 1.0 (ease-out-back), `SfxId.GachaBagDrop` at t=0 | 0.35 s |
| B. Shake | Bag rocks around its bottom pivot: angle = ±A·sin(2π·f·t), A ramps 2°→7°, f ramps 6→14 Hz; `GachaBagShake` at t=0. Tier `bagGlow` on → BagGlow fades to tint over the shake | `shakeDuration` per tier |
| C. Pop | Card instantiated at the bag mouth (centre (0, +205), scale 0.25, alpha 0), rises to CardAnchor along a slight arc (x offset ±40 → 0), scale → 1.1 → 1.0 (overshoot), alpha → 1 in the first 30 %; BagMouthFX burst at t=0; `GachaCardPop` at t=0; bag recoils (scale y 0.94 → 1.0, 0.15 s). Tier `slowPop` on → duration × 2 | 0.45 s |
| D. Land | Tier FX fire (§FX): CardBurstFX, CardRays fade in, Flash (if tier), panel shake (if tier), tier stinger SFX. CardIdleFX loops for the hold | — |
| E. Hold | Card rests; a tap anywhere ends the hold early | `holdDuration` per tier |
| F. Exit (not last card) | Card scales to 0.6 and fades out while drifting up 60 px; CardRays/Rain fade out; `GachaCardExit` | 0.25 s |
| Next card | back to B with `shakeDurationNext` (shorter than the first shake) | — |
| G. Finish (last card) | After its hold: `GachaRevealComplete` fanfare; **first** `onFinished()` (Prizes screen binds and shows *under* the scrim — sortingOrder 500 covers it), then `Hide()` (0.2 s fade) reveals it; Prizes screen entrance (§3) | — |

- **Tap anywhere** (Scrim button, also the card): if in Hold → end the hold now; if in Shake/Pop
  → no-op (let the reveal land — no half-drawn cards). SkipButton is a sibling above the scrim so
  it gets the tap instead.
- **SKIP**: stop the coroutine, destroy any live card, stop all emitters (`Clear()`), reset
  bag/glow/rays/flash to idle, call `onFinished()` then `Hide()`. `GachaSkip` SFX.
- **OnDisable / OnHide**: same cleanup so a force-close (screen change, scene unload) never
  leaves a card or a running emitter behind.
- Cards: instantiate `BagClubCard.prefab` under CardAnchor, bind with the same code the Prizes
  screen uses — move `GachaPrizesScreenController.BindCard` to an `internal static` helper (or a
  small `GachaPrizeCardBinder` static class) so both call one function; disable the card's
  buttons the same way. Destroy on Exit. Ten instantiations per x10 is fine; no pooling.

### 3. Prizes screen entrance

`GachaPrizesScreenController`: after `ApplyMode` binds the cards, run `PlayEntrance()` —
each visible card (`_gridCards` in order, or `_x1Card`) starts at scale 0 / alpha 0 and pops to
1 with ease-out-back over 0.25 s, staggered 45 ms apart (x1: a single pop, 0.3 s). Do it only when
the screen was opened by `GachaPullFlow` (a `s_pendingEntrance` flag set by `SetPendingResult`),
not on BACK-navigation returns. Cards need a `CanvasGroup` for the alpha — add at runtime if missing
(`GetComponent ?? AddComponent`), no prefab edit.

### 4. FX tiers

`[Serializable] class RarityFxTier` array of 6 on the modal (index = `(int)CharacterRarity`,
enum order Common…Supreme), Inspector-tunable. Defaults:

| Rarity | shake (first / next) | hold | bagGlow | burst count | rays | flash | panel shake | rain | slowPop | stinger |
|---|---|---|---|---|---|---|---|---|---|---|
| Common | 0.6 / 0.35 s | 0.9 s | — | 12 (mouth puff only) | — | — | — | — | — | — (land ding only: `GachaCardLand`) |
| Uncommon | 0.6 / 0.35 | 1.0 | — | 20 | — | — | — | — | — | `GachaRevealUncommon` |
| Rare | 0.7 / 0.4 | 1.2 | ✓ | 32 | ✓ | — | — | — | — | `GachaRevealRare` |
| Mythic | 0.9 / 0.5 | 1.6 | ✓ | 48 | ✓ | ✓ (0.08 s, alpha 0.6) | — | — | — | `GachaRevealMythic` |
| Legendary | 1.1 / 0.6 | 2.0 | ✓ | 72 | ✓ | ✓ (0.12 s, 0.8) | ✓ (±8 px, 0.3 s) | ✓ | — | `GachaRevealLegendary` |
| Supreme | 1.3 / 0.7 | 2.4 | ✓ | 96 | ✓ | ✓ (0.15 s, 1.0) | ✓ (±12 px, 0.4 s) | ✓ | ✓ | `GachaRevealSupreme` |

- Tint for glow, rays, burst, idle and rain = `RarityHelper.GetRarityColor(rarity)`
  (start colour; burst uses a white → tint gradient over lifetime).
- **Quality tier:** read `QualityTierService.Current` once per `Play`. `Low`: burst counts × 0.5,
  rain and rays off, no flash. `Mid`: burst counts × 0.75. `High`: as table. Timings never change
  with tier.
- **FX assets** (new, `Assets/Art/Gacha/FX/`): `S_Gacha_Glow.png` (soft radial, 256²),
  `S_Gacha_Rays.png` (12-spoke ray burst, 512², transparent), `S_Gacha_Spark.png` (4-point star,
  64²), `S_Gacha_Confetti.png` (small rect, 16×24). Generate them procedurally in an Editor
  script or author in the editor — white, alpha-only, tinted at runtime. Material:
  `Assets/Prefabs/UI/TapSparkle_Additive.mat` (already in the project) for sparks/glow/rays.
- ParticleSystems: `simulationSpace = Local`, `playOnAwake = false`, `scalingMode = Hierarchy`,
  `stopAction = None`; the burst is a single `Emit(count)` call, the idle/rain emitters are
  `Play()`/`Stop()` around the hold. Follow `TapFeedbackFX` for the UIParticle scale convention.

### 5. Audio

New `SfxId` members (append at the end of the enum, own `// ── Gacha reveal` section — enum order
is not persisted anywhere, but appending keeps the sfx.csv diff clean):

| SfxId | Fires | Suggested character |
|---|---|---|
| `GachaBagDrop` | A | soft leather/fabric thud + short low whoosh |
| `GachaBagShake` | B | fabric rustle / clubs clinking, ~1 s, tail cut by the pop |
| `GachaCardPop` | C | pop + rising whoosh (cork / card flick) |
| `GachaCardLand` | D, every card | short bright "ding" / card snap |
| `GachaRevealUncommon` … `GachaRevealSupreme` | D | escalating stingers: 2-note chime → 3-note sparkle → magical shimmer → short brass/orchestral hit → full fanfare with shimmer tail |
| `GachaCardExit` | F | quick down-whoosh |
| `GachaSkip` | SKIP | `UiCancel` is acceptable here — map the same clip if nothing better |
| `GachaRevealComplete` | G | end-of-pull fanfare (x1 and x10 alike) |

- Add one row per id to `Assets/Resources/Data/sfx.csv` (`baseVolume` 0.8–1.0, `loop=false`,
  `velocityGateMin=0`, `playRateCap=99.0`, `minIntervalSec=0`). `sfx.csv` is NOT a content
  catalog (not in `Tools/content`), so it is edited directly.
- **Placeholder clips are already in the repo**: `Assets/Sounds/Gacha/Gacha_<SfxId minus the
  `Gacha` prefix>.ogg` — twelve files, one per id (`Gacha_BagDrop.ogg` → `GachaBagDrop`, …,
  `Gacha_RevealComplete.ogg` → `GachaRevealComplete`), all CC0, loudness-matched; provenance in
  `Assets/Sounds/Gacha/CREDITS.md`. Map every one of them in `Assets/Audio/SfxLibrary.asset`.
  Import settings: copy the existing SFX metas in `Assets/Sounds/Hit/` (Decompress On Load,
  Vorbis, quality 1.0, mono off, preload off). **Do not download or generate other audio.** A
  `[SfxPlayer] No clip mapped` warning for any `Gacha*` id is a FAIL, not an expected state.
- Do not touch music volume (no ducking) — out of scope.

### 6. Text

One new string: `GACHA_SKIP` — EN `SKIP`, JA `スキップ`. Add to
`Assets/Localization/LocalizationText.csv` (EN **and** JA in the same commit) →
`python3 Tools/content/import_content.py --env-file … --catalogs texts` (PLAN, read the verdicts;
STOP and report on CONFLICTS) → `--apply` → publish `texts` from the admin →
`export_content.py --check` clean. Never code-only. The SKIP label is a `LocalizedText` binder
(`Assets/Localization/LocalizedText.cs`), not a `.text` literal. Note the binder rule from the
localization sweep: the reveal card is a `BagClubCard` **instance** — do not add binders to it.

## Acceptance checklist (Implementer fills in `IMPLEMENTER_REPORT.md`)

- [ ] PULL x1 on a banner card → scrim covers top bar + bottom nav (measure the nav bar pixel before/after, as `ModalScrim.cs` documents) and neither is tappable while the modal is open.
- [ ] Bag appears alone, shakes, ONE card pops from the bag mouth to the card position (582, 722 in the 1170×2532 frame ± 10 px), holds, then the modal fades and the Prizes screen shows in x1 mode with the same club.
- [ ] PULL x10 → ten cards, one at a time, each replacing the last; the Prizes screen then shows the same ten in the same order in the 4/4/2 grid.
- [ ] PULL on the Prizes screen re-runs the reveal (pull again) with the same count and re-binds the grid afterwards.
- [ ] Rarity escalation visible: a Common and a Legendary card side-by-side screenshots show the difference (glow, rays, burst, rain); tints match `RarityHelper.GetRarityColor`.
- [ ] SKIP during card 3 of 10 → immediate cut to the Prizes screen; no leftover card, emitter, glow, or rotated bag on the next open. Verify by pulling again straight after.
- [ ] Tap anywhere during a hold ends the hold; a tap during the pop does nothing.
- [ ] `QualityTierService.Current = Low` (Graphics submenu) → rain/rays/flash absent, timings identical (stopwatch the x1 total: same ±0.1 s as High).
- [ ] Every new `SfxId` is mapped to its `Assets/Sounds/Gacha/` clip in `SfxLibrary.asset` and audibly fires at its step; zero `No clip mapped for SfxId=Gacha…` warnings in a full x10 pull (quote the Console filter result). Instrument the order with a temporary log or the Console's `SfxBus` trace — one x1 pull must show BagDrop → BagShake → CardPop → CardLand → (stinger) → RevealComplete.
- [ ] Prizes screen entrance staggers the cards in after a pull; BACK from Gacha History → Prizes (history stack) does NOT replay the entrance.
- [ ] `GACHA_SKIP` present in `LocalizationText.csv` EN+JA; `export_content.py --check` clean for `texts`; zero new hardcoded `.text` literals (quote the grep).
- [ ] `Bag.png` imported as Sprite; no white-box placeholders; all `[SerializeField]` refs wired; no Console errors related to this task.
- [ ] Spec deviations flagged at the bottom of the report with justification.

## Files / hierarchy this task touches

**New**
- `Assets/Scripts/UI/Gacha/GachaPullFlow.cs`
- `Assets/Scripts/UI/Gacha/GachaRevealModalController.cs` (+ `RarityFxTier`)
- `Assets/Prefabs/UI/Modals/GachaRevealModal.prefab` + instance in `Assets/Scenes/ShellScene.unity`
- `Assets/Art/Gacha/FX/S_Gacha_{Glow,Rays,Spark,Confetti}.png`
- `Assets/Sounds/Gacha/*.ogg` + `CREDITS.md` — already committed by the Architect (12 CC0 placeholders); this task adds their `.meta` files + the `SfxLibrary.asset` mappings

**Modified**
- `Assets/Scripts/UI/Gacha/GachaBannerCard.cs` — `OnPullX1/OnPullX10` → `GachaPullFlow.Pull`
- `Assets/Scripts/UI/Gacha/GachaPrizesScreenController.cs` — `SetPendingResult`, result-driven binding, `OnPull` → pull again, `PlayEntrance`, shared card binder
- `Assets/Scripts/Audio/Events/SfxId.cs`, `Assets/Resources/Data/sfx.csv`, `Assets/Audio/SfxLibrary.asset`
- `Assets/Art/Gacha/Bag.png.meta` — Sprite import
- `Assets/Localization/LocalizationText.csv` (+ regenerated `LocalizationTextTable.asset`)
- `Docs/AI_CONTEXT.md`, `Docs/TellCode.md`

## Smoke evidence

Presentation feature — Lesson O applies. Required:
- Screenshots at 1170×2532: (1) modal open, bag alone mid-shake; (2) Common card at hold;
  (3) Legendary card at hold; (4) Prizes screen right after the modal, x10; (5) Low tier
  Legendary hold (no rays/rain).
- A short screen recording (`videos/`) of one x10 pull with mixed rarities end-to-end and one SKIP.
- Human-in-the-loop prose in the report: what the bag, card and particles visibly did, per step
  A–G, and the measured total duration of an x1 Common pull and an x1 Legendary pull.
- A Console excerpt proving the SFX order for one x1 pull (temporary `Debug.Log` on
  `SfxBus.Play` call sites is fine; remove before the report) and a clean filter for
  `No clip mapped`.

## Out of scope (do NOT do these)

- The real pull: ticket spend (`GachaTicketManager.SpendTickets`), server call, pity, odds,
  writing `GachaHistoryStore` — all blocked on content (TellCode). `GachaPullFlow.BuildResult`
  is the single seam for it later.
- Sourcing / downloading any further audio (placeholders are in), music ducking.
- A "3/10" progress counter or any UI element not in the Figma frame.
- `GachaTabController.WirePullButtons` dead paths.
- Prizes screen layout changes beyond the entrance animation.
- Ball / character / item prize types (`PrizeRecord` is club-only today).
