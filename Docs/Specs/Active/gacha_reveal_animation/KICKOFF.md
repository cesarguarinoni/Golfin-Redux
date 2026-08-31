- **`gacha_reveal_animation`** (filed 2026-08-31, Architect via Cowork) — **SPEC_READY, kickoff pasteable.**
  `Docs/Specs/Active/gacha_reveal_animation/SPEC.md`. PULL x1/x10 on a banner card (and PULL-again on
  the Prizes screen) now opens a reveal modal (Figma `13997:4298`, bag art `Assets/Art/Gacha/Bag.png`):
  scrim over everything incl. the persistent bars, bag alone → shakes → each prize card pops out one
  at a time with rarity-scaled particle FX (UIParticle, tint from `RarityHelper`), auto-play + SKIP,
  then the Prizes screen enters with a staggered card entrance. New `GachaPullFlow` is the single
  seam for the real pull later (still mock). 12 new `SfxId`s wired through `SfxBus`/`sfx.csv`/
  `SfxLibrary.asset`, mapped to the 12 CC0 placeholder clips the Architect committed to
  `Assets/Sounds/Gacha/` (`CREDITS.md` there). One string `GACHA_SKIP` (EN+JA, importer path).

### Kickoff · gacha_reveal_animation (issued 2026-08-31)

```
Read Docs/Specs/Active/gacha_reveal_animation/SPEC.md and implement it.

Context:
- Adds the gacha reveal: PULL x1/x10 (GachaBannerCard.OnPullX1/OnPullX10) and the
  Prizes screen's PULL (pull again) go through a new GachaPullFlow.Pull(count) ->
  GachaRevealModalController (new, : ModalController, scene instance on the
  ShellScene canvas, static Instance) -> GachaPrizesScreenController.SetPendingResult
  + ShowScreen(GachaPrizes). Bag alone, shake, cards pop one at a time (x10: each
  replaces the last), rarity FX tiers, auto-play + SKIP, tap-to-fast-forward the hold,
  Prizes screen staggered entrance.
- Look at: GachaBannerCard.cs, GachaPrizesScreenController.cs (BindCard becomes the
  shared binder), GachaMockPrizePool.cs, ModalController.cs + ModalScrim.cs (scrim
  covers the bars for free), TapFeedbackFX.cs + TapFeedbackFX.prefab (the UIParticle
  precedent), SfxId.cs / sfx.csv / SfxLibrary.asset, QualityTierService.Current,
  RarityHelper.GetRarityColor. Coroutines + unscaled time — no tween library.
- Minimal diff. Reuse BagClubCard.prefab for the reveal card (no rebuild), the
  TapSparkle_Additive.mat, the Prizes PULL button atom for SKIP. Bag.png -> Sprite import.
- Audio: add the 12 SfxIds + sfx.csv rows and map each to its clip in
  Assets/Sounds/Gacha/ (already in the repo, Gacha_<Id>.ogg; import settings like
  Assets/Sounds/Hit/). Zero "No clip mapped" warnings for Gacha* ids. Do NOT download
  or generate other clips.
- Strings: GACHA_SKIP EN+JA via LocalizationText.csv -> import_content.py (plan -> apply
  -> publish texts -> export --check clean). Never code-only.
- Out of scope: ticket spend / server pull / history / pity, music ducking, a progress
  counter or any UI not in the Figma, GachaTabController's dead PullSection paths.

When done: list changed files with a 1-line summary each, run the acceptance
checklist in the spec, produce the smoke evidence (5 screenshots + x10 recording WITH
audio + step A-G prose + the SFX order excerpt), flag which need manual on-device
verification, update STATUS.md + IMPLEMENTER_REPORT.md in the spec folder, and update
Docs/AI_CONTEXT.md.
```

