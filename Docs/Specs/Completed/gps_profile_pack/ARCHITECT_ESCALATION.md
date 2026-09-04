# ARCHITECT_ESCALATION — `gps_profile_pack` (iter-3)

**Timestamp:** 2026-09-02 03:30 JST
**Circuit-breaker:** shape `gps_profile_ui:node-elements-absent` — failure 3 of 3 → forced escalate
per PIPELINE_HARDENING §1. iter-4 of this shape may not run under the auto-router.

## Where the diagnosis lives

The full analysis is in `Docs/Specs/Active/gps_profile_pack/ARCHITECT_REVIEW.md` § "Diagnosis for
Cesar". This file is the escalation pointer only. Read the review's diagnosis section — not the
fail list — to make the call.

## Single-line summary

iter-3 built the UI atoms iter-1/iter-2 were missing (background, character sprite, equip slots,
STATUS containment, section counts, EDIT PROFILE) but the runtime data pipeline is not wired to
the built UI. The Profile and Avatar controllers subscribe to `UserService.OnDetailChanged` and
paint from cache, but never fire a fetch of their own; two builder-seeded literals leak on the
two fields the SPEC says must render `—` permanently, because those fields have no controller
SerializeField that would clear them. Badge names render as raw seed ids because the builder
seeds a literal at `.cs:747` and the runtime `BadgeCellView.Bind` either doesn't run or looks up
a case-mismatched key. The task shape shifted between iter-2 and iter-3
(`node-elements-absent` → `data-pipeline-not-wired`).

## Cesar's calls

Named in the review's "What I need from Cesar to unblock" section:

1. Direction on the fetch pattern (add `FetchDetail()` to the controllers vs. centralize in
   `PostAuthBoot`).
2. Scope call: reframe as a new `gps_profile_data_wiring` task, or override the circuit-breaker
   and authorize iter-4 of THIS task on the (shifted) shape.
3. Confirm Item 2 closure (`ButtonCancel.png` = silver Main Button, per
   `ScoreUploadScreenBuilder.cs:38`).
4. Publish status on the 24 `BADGE_*_NAME` CSV rows.

## Genuine iter-3 progress worth naming before deciding

- Home Background sprite bound in all 3 prefabs (was flat navy in iter-2).
- Character figure now renders centered in the Avatar stage (was a left-aligned narrow strip).
- Avatar STATUS panel contains all 4 rows (was ~23px STAMINA overflow).
- Badge section counts / order / cell containment corrected (was 6-per-section overflow into
  adjacent headers).
- Five equip slots with labels (was six unlabeled dots).
- Stale byte-identical PNGs deleted; fresh test run cited today.

The remaining fail set is a different shape of defect, not iter-2's defects unfixed.

---

## Orchestrator addendum (main Claude Code thread, 2026-09-02 ~03:40)

> Written AFTER the circuit-breaker fired. **This does not override the escalation** — iter-4 is
> still blocked pending Cesar. It answers three of the four questions the reviewer put to him, so
> the decision is a read rather than an investigation. Every claim below is a command I ran.

### Q1 "fetch-pattern direction" — not a judgment call; there is an approved precedent in-repo

`GpsHubScreenController.OnEnable` (`Assets/Scripts/UI/Gps/GpsHubScreenController.cs:128-136`) does
**three** things, and the hub shows live data because of the third:

```csharp
ApplyDetail(UserService.Instance.LastDetail);                        // :128  paint from cache
UserService.Instance.OnDetailChanged += ApplyDetail;                 // :131  subscribe
client.Run(UserService.Instance.Detail(OnDetailResult));             // :135  ← FIRE THE FETCH
client.Run(ScoreHistoryService.Instance.History(0, 3, OnHistoryResult)); // :136
```

The three new controllers do :128 and :131 and omit :135-136, so `LastDetail` is null on entry and
every field routes to `ShowPlaceholders()`. This is a defect against an existing pattern, not a
design fork — the fix is one `client.Run(...)` per service per controller, matching the hub.

### Q4 "publish status of the 24 `BADGE_*_NAME` rows" — nothing was published, at all

```
git diff --stat HEAD -- Assets/Localization/LocalizationText.csv
  → 1 file changed, 75 insertions(+)
git show HEAD:…LocalizationText.csv | grep -c '^BADGE_'            → 0    (working tree: 24)
git show HEAD:…LocalizationText.csv | grep -c '^GPS_(PROFILE|AVATAR|BADGES)' → 0  (working tree: 51)
```

All **75** new rows are uncommitted, CSV-only. No importer PLAN, no APPLY, no `texts` publish, no
`export_content.py --check` appears in any of the three `IMPLEMENTER_REPORT.md` versions. SPEC
**Build rule 7** ("every new text key is PUBLISHED, not just in the CSV") has been untouched for
three iterations. The `texts=26→28` bump in the working tree is **other tasks' drift** — the
implementer attributed it correctly (`IMPLEMENTER_REPORT.md:302`, `HEARTBEAT.log:10`).

⚠️ Consequence: fixing the builder's key-case bug alone will **not** make the badge labels render.
Unpublished keys render as the raw key. Both fixes are needed, in that order.

### Q3 "confirm Item 2 closure" — confirmed, independently, twice

`ButtonCancel.png` IS this project's silver Main Button:
`ScoreUploadScreenBuilder.cs:38` `const string SprSilver = "Assets/Art/RosterScreen/ButtonCancel.png";`,
used at `:1251` and `:1286` for every silver `MainButton` on the score-upload screens Cesar approved.
The reviewer reached the same conclusion. Item 2 is not a defect.

### Q2 "scope call" — genuinely Cesar's, but here is the shape of what is left

Four mechanical causes, each with a known fix:

| # | Cause | Fix | Evidence |
|---|---|---|---|
| 1 | Controllers never fire their fetch | one `client.Run(...)` per service, per the hub | `GpsHubScreenController.cs:128-136` |
| 2 | Badge key case mismatch — builder emits `BADGE_FIRST_ROUND_NAME`, CSV has `BADGE_first_round_NAME` | stop uppercasing the id | `GpsProfilePackBuilder.cs:745`; CSV `:859-882` |
| 3 | 75 loc rows never published | importer PLAN → APPLY → publish `texts` → `--check` | `git diff --stat HEAD` above |
| 4 | Translucency defeated by the `Border` child painting `S_PillStadium` opaque across the cell | let the cell fill carry `A()`/`ADark()`; stop over-painting | reviewer's pixel samples `#efdc98` / `#495970` |

`AVG PUTTS 33.2` / `GIFTS SENT 24` need no separate fix: they are seeded literals that survive
because no controller `SerializeField` owns those two fields, so `ShowPlaceholders()` cannot clear
them. Per SPEC they must render `—` permanently.

**Orchestrator's read:** the shape genuinely changed between iter-2 and iter-3 (`node-elements-absent`
→ `data-pipeline-not-wired`), and the four remaining causes are mechanical with in-repo precedents
rather than open design questions. That argues for a scoped continuation. But the breaker fired for a
good reason — three rounds of optimistic PASS rows, one now logged as a Rule 6 fabrication — so the
call to continue vs. re-scope is Cesar's, and I have not started iter-4.
