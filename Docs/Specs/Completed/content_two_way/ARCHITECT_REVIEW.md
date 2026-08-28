# Architect Review — `content_two_way`

> Architect (Cowork session), 2026-08-27. Read SPEC.md, IMPLEMENTER_REPORT.md, and the
> code itself — not the report alone.

## Verdict

**PASS** — ready for Cesar's final approval.

## Verified in the repo (not from the report)

- `renderable` exists on `CharacterDataRuntime` / `ItemDataRuntime` / `BallDataRuntime`, set from
  the primary sprite at load (`CharacterDatabaseCSV.cs:320`, `ItemDatabaseCSV.cs:212`,
  `BallDatabaseCSV.cs:210`); `GetAvailable…()` = `isActive && renderable` in all three;
  `GetAll…()` untouched; one summary warning per loader naming the withheld ids.
- Visible-list consumers switched: `CharacterManager.cs:86`, `MatchmakingModalController.cs:258`,
  `ItemManager.cs:59`, plus `BallManager.cs:58` (not in the spec's list; correct by the spec's
  own "grep every call site" instruction).
- `GeneralShopModel.UnrenderableReason` reads `renderable` for character/item/ball; the club branch
  keeps Placeholder-by-name. `ClubDatabaseCSV` has zero diff — the decision of record holds
  (799 rows, 150 Placeholder, 0 nulls measured).
- `Assets/Editor/ContentArtValidator.cs` + `CIBuild.cs:163` — report only.
- `Tools/content/tests/{fakes,test_export_check,test_import_content}.py` exist; `--check` has
  the value-level half.
- §8 step 3 prod round-trip ran on `HOME_CURRENCY_LABEL`, both legs published by Cesar,
  export byte-identical, prod restored (`texts` v12 → v14).

## Spec deviations — all accepted

- `CharacterManager.cs:103` (ScriptableObject fallback seed) left on `GetAllCharacters()`:
  the legacy `CharacterData` has no CSV sprite name to derive `renderable` from, and a second
  differently-derived rule is the two-rails problem §4 exists to close. Unreachable on a
  production boot. Correct call.
- `BallManager.cs:58` switched though unnamed — same shape as `ItemManager`, feeds the bag. Correct.
- Editor/lab diagnostics (`LabInventoryStub`, `MatchmakingCaptureRunner`) left on `GetAll…`. Correct.

## Architectural / cross-cutting

- Asmdef boundaries: no new references; `renderable` lives on Assembly-CSharp runtime rows,
  `ContentSpriteGuard` unchanged. Clean.
- Intent: the invariant ("never show a row this build cannot draw") now holds game-wide for the
  three catalogs without Placeholder policy, while owned rows survive save + `InventoryCodec`.
  That is the intent, not just the letter.

## Carried forward (not defects here)

- `content_art_urls` (already IMPLEMENTER_WORKING) adds "cached remote URL" as the first rung of
  the resolution ladder in front of this rail; two review notes for that task are recorded in
  its own folder when it reaches review: (1) replacing a row's art mints a new URL, so the row
  is withheld for exactly one launch until the prefetch lands — acceptable, document it;
  (2) cache-decoded PNGs are uncompressed RGBA in memory (~1.9 MB per 537×900 full-body), so the
  50 MB disk cache implies a comparable RAM cost if many rows go URL-only — fine at tester
  scale, and it is the number to look at before ever retiring bundled art.
