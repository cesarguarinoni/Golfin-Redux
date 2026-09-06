# ARCHITECT_REVIEW — `design_consistency_audit` (red-team gate)

**Reviewer:** golfin-redteam-reviewer (adversarial). `golfin-reviewer` was skipped — Cesar sent
this straight to red-team, so this is the only adversarial pass before he acts on the fix list.
**Timestamp:** 2026-09-06 10:15 JST
**Verdict:** **ARCHITECT_REVIEW_FAIL** — two material blockers in the deliverable Cesar acts on:
the headline S1 finding is visually REFUTED, and the shape counts are internally contradictory
and non-reproducible from the primary dumps. The instruments themselves (A2/A9/A10/A13) are
sound; the *reported measurements and severities* are not.

The damage model for this task is not "is the build right" (no production code changed) — it is
"the Architect turns a wrong finding into a Quick spec that makes the game worse, or wastes a
large refactor on a defect that does not exist." Both blockers below feed that model directly.

---

## BLOCKER 1 — the headline finding is S1 with ZERO visual evidence, and the render is FINE

**Claim under attack (§1 summary, §3.1, fix list Q1, commit message):** *"JA renders on a Rubik
asset, not NotoSansJP — 860 of 873 labels — S1 — the single largest finding in the audit."* S1 is
defined in SPEC § Phase-2 as **"a player sees it."** The entire finding was derived from `font.name`
in the dumps. **Nobody ever looked at a Japanese screen.** There are no JA crop sheets.

**What I did:** entered play mode, `LocalizationManager.SetLanguage(Japanese)` (confirmed
`CurrentLanguage=Japanese`), navigated to `MissionSelection`, captured the Game View at full
1170×2532. Evidence: `screenshots/REDTEAM_ja_MissionSelection.png`.

**What I saw:** every CJK label renders **correctly** through TMP's fallback chain — ミッション,
ロモンドカントリークラブ 1/40, ビギナー/アマチュア/プロ/レジェンド, デイリーミッション, 次のミッション,
ショートホール, ルール, ダブルボギー以上であがる, スタート/風/クラブ, ロック中, and プレイ (the PLAY
button). **No tofu, no missing-glyph boxes, no clipping, no wrong-baseline metrics, correct
weight, sized within every container.** The player sees clean, correct Japanese.

**Therefore the S1 classification is false.** Per the brief's own instruction: with correct
fallback rendering the finding downgrades to *"the font BINDING is inconsistent/fragile (relies on
the fallback chain; `japaneseFontScale` never applies) but the render is fine"* — an **S2/S3**, not
an S1, and **Q1's priority collapses.** As written, Q1 is an **M-effort, every-screen, both-locale
`LocalizedText` refactor** ("needs a JA visual pass before merge") justified by a phantom
player-facing defect. If Cesar makes it a Quick spec at that severity, that is precisely the
"large risky change for a defect that doesn't visibly exist" this gate exists to stop.

**Secondary count error inside the same finding.** Re-derived from the 21 JA dumps myself:
**866 of 873 CJK-text labels are on a Latin asset; only 7 are on NotoSansJP.** The report's
"860 / 13 exceptions" conflates two different populations — there are 13 *total* NotoSansJP
bindings, but only 7 of them actually hold CJK text (the other 6 are numbers/empty). "860" =
873−13, which is not the count of anything. State it precisely.

**Fix instruction:** (1) add the JA visual evidence and downgrade the finding to S2/S3 with the
render explicitly called correct; (2) re-scope Q1 (propagation is optional hardening, not a
player-facing fix); (3) correct the arithmetic to "866/873 CJK labels on Latin; 7 on NotoSansJP;
13 NotoSansJP bindings total."

---

## BLOCKER 2 — the shape counts contradict themselves and reproduce from no corpus

The report gives **two different numbers for the same shape in the same document**, and the fix
list scopes Q-rows off numbers I cannot reproduce from the dumps.

| Shape | §1 Summary | §3.x table | fix row | my re-derivation (EN dedup / EN / JA / all) |
|---|---|---|---|---|
| (vii) `Image.Type.Filled` | **226** | **447** (287+133+24+3) | Q5 "§3.5 (226)" | 133 / 134 / 1007 / 1141 |
| (viii) flat fill visible | **710** | **442** | — | 560 / 570 / 2003 / 2573 |
| (viii) flat fill panel-sized | **291** | **26** | Q6 "§3.6 (291)" | 317 / 318 / 365 / 683 |

`226 ≠ 447` and `291 ≠ 26` for the SAME shape is an unambiguous internal contradiction, and
**neither number reproduces from any corpus** I computed from the on-disk JSON.

**Root cause I found:** the JA dumps captured a structurally different (larger) object set than the
EN dumps, even though image/size counts are locale-invariant:

```
Image.Type.Filled     EN dumps 134   vs   JA dumps 1007
non-autosized TMP     EN dumps 2244  vs   JA dumps 4909
```

Same screens, ~7×/~2× more objects in JA. So any count summed across locales is inflated and
meaningless, and the size-bucket denominator the report quotes (**2062** non-autosized) reproduces
from no corpus (EN 2244, EN-dedup 1950, JA 4909, all 7153). Every divisor bucket built on that
denominator (÷1.4 209 / ÷1.2 144 / 59-66 47 / unexplained 275) inherits the same uncertainty.

**Fix instruction:** pin ONE defined corpus (recommend EN, deduped by screen, active-only), re-run
every shape count against it, make §1 / §3.5 / §3.6 / §3.8 / Q5 / Q6 agree, and either fix the JA
dump so its structural counts equal EN or state that all structural shape counts are EN-scoped.

---

## What I verified as SOUND (credit where due — these are not the reason for the FAIL)

- **÷1.4 divisor (Q7) — CONFIRMED a third time.** Node `13026:2366` = 45/39 (title/footnote), live
  dump = 32.14/27.86, `45÷1.4 = 32.1429`, `39÷1.4 = 27.8571` — exact. The audit correctly caught
  its own earlier ÷1.2 misread; Q7 targets the node value (39/45), not a divisor. Well-aimed.
  Visually corroborated on the canonical sheet (every ModeCard label ~⅓ undersized, PLAY on-scale).
- **LiberationSans = 41 (Q2).** YAML double-count (StatBar 4 hits/2 labels, CharacterThumbnailCard
  6/3) is real; 27+8+1+3+2 = 41. Confirmed.
- **A2 tripwire** — `liberationSans 0→1→0`, `outlineComponents 15→16→15` across TRIPWIRE_01/02/03.
- **A9** — `GeneralShopCard_lint.json` md5 `78c23b5b237c2842ecf94c24811a48bd` matches; `git diff`
  of `UIFidelityLinter.cs` vs HEAD is empty (change committed in `84174e914`); no rule added/tuned.
- **A10** — commit `84174e914` = 48 files, **all** under `Assets/Editor/UIFidelity/` or `Docs/`;
  zero production `Assets/Prefabs|Scenes|Scripts|Localization|Fonts` paths.
- **A13** — no `Assets/Prefabs/UI/Gps` dump/render/screenshot. The only "gps" grep hits are
  `GPS` / `GpsPill` / `GpsPill/Label` — child elements ON HomeScreen (in scope), not Gps prefabs.
- **MISSIONS copy bug (Q9)** — visible on the canonical sheet: MISSIONS card shows
  "REWARDS Varies by tournament" (node says x200); it is a hardcoded/mis-bound string with empty
  locKey, not dev-account data. Real, XS. (Note: the TOURNAMENTS card shows the same string, which
  is correct there — the fix must not blanket-change both.)
- Node-table 7-row correction and the SPEC-input findings (§0) hold.

## Three break-attempts and why the two above stuck

1. **Visual:** switched to JA and looked (Blocker 1) — the render defeated the S1 claim, but the
   binding-inconsistency direction survived (866/873 re-derived). The attack SUCCEEDED against the
   severity, which is the load-bearing part of Q1.
2. **Geometric/arithmetic:** re-derived vii/viii/size buckets from the raw JSON (Blocker 2) — the
   report's numbers do not reproduce and self-contradict. Attack SUCCEEDED.
3. **Spec-intent / integrity:** md5, tripwire, commit scope, GPS containment, ÷1.4, LiberationSans
   — all held under re-derivation. Attack FAILED (these are genuinely correct). No fabricated tool
   output found, so no `review_misses.log` fabrication entry is warranted — this is honest-but-wrong
   measurement, not fabrication.

---

## Verdict

**ARCHITECT_REVIEW_FAIL.** Route back to the implementer. Two fixes, both in the report only (no
production change): (1) add the JA visual capture, downgrade the JA finding to S2/S3 and re-scope
Q1, correct 860→866/7/13; (2) reconcile the vii/viii/size-bucket counts to one reproducible corpus
and fix or scope the JA-dump object inflation. Everything else I attacked held.

STATUS → `ARCHITECT_REVIEW_FAIL`.

---

# RED-TEAM RE-SUBMISSION — `design_consistency_audit`

**Reviewer:** golfin-redteam-reviewer (adversarial). Second adversarial pass; `golfin-reviewer`
skipped again. **Timestamp:** 2026-09-06 10:58 JST
**Verdict:** **ARCHITECT_REVIEW_FAIL.** Blocker 1 is genuinely fixed. **Blocker 2 is NOT** — the
summary/headline numbers were corrected but the breakdown tables, the fix-row scope references and
one section header were left with the stale contaminated-corpus values, and the JA headline is
computed on a *different* corpus than the report claims. The implementer's central claim
("no shape count appears with two different values … every count from ONE stated corpus") is
false in four places I re-derived myself.

## Production-code / scope gate — PASS
- `git diff HEAD` task source = **only** `Assets/Editor/UIFidelity/DesignAuditDumper.cs`, +8 lines,
  exactly the locale-suffix change (`<Screen>__<locale>.json`). Nothing under `Assets/Prefabs|Scenes|
  Scripts|Localization` from this task. The `MapPinIndicator/MapViewController/control_scheme_seam/
  map_view_v2` working-tree edits are unrelated in-flight tasks (last committed by a GPS build sweep).
- Corpus physically de-contaminated: 21 `*__en.json` + 21 `*__ja.json`, all 10:26–10:30 today,
  **zero** unsuffixed `<Screen>.json` leftovers. The fix took at the source.
- **A9** `GeneralShopCard_lint.json` md5 `78c23b5b237c2842ecf94c24811a48bd` — matches.

## What I re-derived and CONFIRMED sound (17-screen corpus, Inventory tabs collapsed)
| Shape | report | my re-derivation | verdict |
|---|---|---|---|
| LiberationSans in-screen | 36 (Inv 27 / Roster 8 / Settings 1) | 36 (27/8/1) | ✅ exact |
| Outline | 20 (Home 15 / Mode 5) | 20 (15/5) | ✅ exact |
| Shadow | 0 | 0 | ✅ exact |
| `Image.Type.Filled` **total** | 225 (Inv 84 / Roster 26) | 225 (Gacha 60/55, Inv 84, Roster 26) | ✅ exact |
| non-autosized denominator | 2057 (1389+209+139+46+274) | 2057 | ✅ exact — the old non-reproducing 2062 is fixed |
| JA render (Blocker 1) | S3, correct through fallback | my own `REDTEAM_ja_MissionSelection.png` | ✅ downgrade honest; S3+DEFERRED is the right disposition, not a rationalisation |

Blocker 1 is fully closed: severity S3, JA screenshot cited, Q1 DEFERRED. The robustness argument
(fragile fallback binding, `japaneseFontScale` never applies) is a legitimate S3, not a dead finding
kept alive.

## BLOCKER 2 — STILL OPEN. Four surviving contradictions I re-derived
**Shape (vii) `Image.Type.Filled` — the number appears THREE ways in the document:**
- §1 summary (L52) = **225**; §3.5 header (L154) = **225** — both correct (17-corpus).
- §3.5 breakdown table (L158–161): `Bar 287 · BarContainer 133 · BarPending 24 · GhostBar/Fill 3` =
  **447**. This table was **never recomputed** — 133/24 are the *all-21 contaminated* counts, 287
  matches nothing. The correct 17-corpus breakdown is **Bar 182 · BarContainer 33 · BarPending 8 ·
  GhostBar 2 = 225**.
- Q5 (L271) references **"§ 3.5 (226)"** — off-by-one vs 225.

**Shape (viii) flat fill — the number appears TWO ways:**
- §1 (L53) & §3.6 body (L170–171) = **701 visible / 291 panel-sized**.
- §3.6 **header** (L167) = **442 visible / 26 panel-sized** (stale), and the body still says
  "Each of the **26** needs a node check" (L172). Q6 says 291. A reader scoping Q6 cannot tell if
  the triage is **26 sites or 291 sites** — a 10× scope ambiguity, exactly the mis-scoping this
  gate exists to stop.

**Corpus-rule violation — the JA headline is on a different corpus than the report claims:**
- §1 states verbatim: *"All counts below come from ONE corpus: the 17 distinct screens … Inventory's
  four tab states collapsed to one screen."*
- The F-row "**866** CJK labels (7 on NotoSansJP)" / §3.1 "873" is computed across **all 21 JA
  dumps** (Inventory tabs NOT collapsed). On the stated 17-screen corpus it is **660 on Latin / 667
  total / 7 on Noto** — I re-derived both. The Inventory stat labels are counted 4× extra. The
  headline JA number silently uses the un-collapsed corpus while every other row uses the collapsed
  one, with no exception flagged.

**Lower-confidence note (not the blocker, but pin it):** the flat-fill 701/291 does not reproduce
exactly for me — null-sprite & alpha>0.02 gives **710 visible / 296 panel-sized** (all objects) or
**377 / 264** (active-only). The exact visible/panel rule (alpha threshold, active filter, ≥200×60)
is not pinned in the report, so the number is ~1 % unverifiable. State the rule.

## Fix instructions (report-only; no production change)
1. Recompute the §3.5 breakdown table on the 17-corpus: **Bar 182 · BarContainer 33 · BarPending 8
   · GhostBar 2 = 225**; change Q5's "§ 3.5 (226)" → **(225)**.
2. §3.6 header → **"701 visible, 291 panel-sized"**; "Each of the 26" → "Each of the **291**".
3. JA F-row: either report **660/667/7** on the stated corpus, OR add an explicit, flagged exception
   that the JA font finding is measured across all 21 dumps (tabs not collapsed) and why. Do not
   leave a different-corpus number inside a table that claims one corpus.
4. Pin the flat-fill visible/panel rule (threshold + active filter + size) so 701/291 reproduces,
   or restate to the number that does.

## Three break-attempts
1. **Arithmetic re-derivation (Blocker 2):** re-derived every headline from `*__en.json` on the
   stated corpus. LiberationSans/Outline/Shadow/Filled-total/non-autosized-denominator all
   reproduce exactly — but the §3.5 breakdown table, Q5, the §3.6 header and the JA F-row do not.
   Attack **SUCCEEDED**: the summaries were fixed, the sub-tables and cross-references were not.
2. **Corpus-rule honesty (item 4):** applied the report's own "17 screens, Inventory collapsed"
   rule literally; the JA 866 collapses to 660. Attack **SUCCEEDED**: the rule is stated but not
   applied to the F-row.
3. **Instrument integrity (A9/A10, code diff, corpus de-dup):** md5 matches, only the dumper
   changed (+8 lines), no unsuffixed leftovers, production untouched. Honest-but-incomplete edits,
   not fabrication — **no `review_misses.log` fabrication entry warranted.** Attack **FAILED**
   (these are genuinely sound).

## Verdict
**ARCHITECT_REVIEW_FAIL.** Route back to the implementer for four report-only edits above. Blocker
1 is closed; Blocker 2's top-lines are correct but its breakdown tables / Q-refs / §3.6 header /
JA-corpus are not — the "one corpus, no contradictions" claim does not hold. STATUS →
`ARCHITECT_REVIEW_FAIL`.

---

# RED-TEAM ROUND 3 — 2026-09-06 11:20 JST (commit 93ad252a1)

**Verdict: `ARCHITECT_REVIEW_FAIL`.** The same failure shape survives a third time — top-lines were
regenerated, but a sub-table and three fix-list rows still carry older-corpus numbers. Every
headline I re-derived independently (my own extraction, NOT `audit_numbers.py`) matched the report
EXCEPT the four items below. The `--check` gate reporting "none" is not evidence — I show below why
it is blind to all four.

## What I re-derived and CONFIRMED correct (independent extraction from `*__{en,ja}.json`, 17-corpus)
- Image.Type.Filled **total = 225** (Bar 182 / BarContainer 33 / BarPending 8 / GhostBar 2). ✅
- Flat fill **701 visible / 291 panel-sized** at α>0.2; sensitivity table reproduces EXACTLY
  (709/295, 708/295, 704/291, 701/291, 690/291); **8 fills** in 0.02<α≤0.20; **3** `VerticalDivider`
  fills at exactly α=0.20 (`#FFFFFF33`, InventoryScreen) excluded by strict `>`. ✅ (item d passes)
- CJK bound to Latin **660** / NotoSansJP **7** / Noto-holding-no-CJK **6**. ✅
- LiberationSans **36 in-screen** (Inv 27 / Roster 8 / Settings 1) +5 prefab = 41. ✅
- Outline **20** (Home 15 / ModeSelection 5); Shadow **0**. ✅
- Size buckets: on-scale 1389 / ÷1.4 **209** / ÷1.2 **139** / 59/66 **46** / unexplained **274**, total 2057. ✅
- Commit 93ad252a1 touches only `Assets/Editor/UIFidelity/*` + `Docs/**`. ✅ (item f)
- Every out-of-surface dirty CODE path (`MapPinIndicator.cs`, `MapViewController.cs`, bot csvs, etc.)
  IS in the iter-1 kickoff baseline in `HEARTBEAT.log`. No path dirtied by this task hides behind
  "pre-existing". ✅ (item f)
- `IsGps` namespace filter is present in `DesignAuditRunner.cs` (excludes ns `.Gps` / name `Gps*`). ✅ (item g, code)

## BLOCKERS (report-only edits; all four are the "stale number in a sub-location" shape)

**B1 — §3.5 Filled breakdown table sums to 228, not 225 (fabricated row).**
Lines 158-166 list a FIFTH row `` `GhostBar` / `Fill` | 3 ``. I searched all 21 EN dumps: there is
NO Filled image with a `GhostBar/Fill` path or a leaf `Fill` of type Filled — zero. The real
breakdown is Bar 182 / BarContainer 33 / BarPending 8 / GhostBar 2 = **225**, matching the header,
§1, Q5 and STATUS. **Fix:** delete the `` `GhostBar` / `Fill` | 3 `` row. The table must sum to 225.

**B2 — Q7b (line 305) cites ÷1.2 = "144"; canonical is 139.**
The ÷1.2 population is **139** (§1 line 49, §3.8 table line 228, and my re-derivation). Q7b's 144 is
stale. **Fix:** `§ 3.8 (144)` → `§ 3.8 (139)`.

**B3 — Q8 (line 306) cites unexplained = "275"; canonical is 274.**
Unexplained-by-any-divisor is **274** (§1 line 49, §3.8 table line 230, my re-derivation). **Fix:**
`§ 3.8 (275)` → `§ 3.8 (274)`.

**B4 — §3.8 prose (line 250) says "plus 275 labels no conversion explains"; canonical is 274.**
Same stale figure as B3, in prose. **Fix:** `275` → `274`.

## Why `audit_numbers.py --check` reported "none" while all four stand (item c — the checker is NOT trustworthy)
- **B1 dodges the SUM regex.** The sum check parses breakdown rows with
  `^\|\s*` + backtick-word + backtick + `\s*\|\s*` + digits. The fabricated row's first column is
  `` `GhostBar` / `Fill` `` — the ` / ` + second backtick group breaks the pattern, so the row is
  NOT parsed. The checker therefore sums only the 4 clean rows = 225 and passes, while a human reads
  5 rows = 228. A stale number CAN hide behind row formatting.
- **B2/B3/B4 are outside the checker's coverage entirely.** `--check` only validates the labels
  `Image.Type.Filled`, `panel-sized`, `visible flat fill`, `CJK label`. It has NO rule for the size
  buckets (÷1.2, ÷1.4, unexplained), so 144 and 275 are never examined. The `declares_scope`
  exemptions currently firing (134/1007, 561, 866) are each genuinely a different-scope/superseded
  figure — those are fine — but the gate's silence is not proof of consistency; it is blind here.

## Secondary (not blocking the FAIL, flag for implementer)
- §6.4 and the top-of-report evidence line cite `MODAL_*.json` (13 modals) and `PREFAB_*` dumps as
  evidence, but NONE exist on disk in `design_audit/` — only the 21 EN + 21 JA screen dumps remain
  (all re-dumped Sep 6 10:26-10:30; the modal pass was not re-run into the folder). No shape count
  depends on modals, so this is not a count defect, but the report references evidence that a reader
  cannot open. GPS exclusion is therefore verifiable only in code (`IsGps`), not in an artifact.
- Not independently re-run this round (Unity-required, secondary; do not change the verdict since the
  deliverable already fails): A9 `LintRoot` byte-identical to `LintPrefab`, and the EditMode suite /
  tripwire. Re-run these after the four edits.

## Numbered fix list for the implementer
1. Delete the `` `GhostBar` / `Fill` | 3 `` row from the §3.5 table so it sums to 225.
2. Q7b: `§ 3.8 (144)` → `§ 3.8 (139)`.
3. Q8: `§ 3.8 (275)` → `§ 3.8 (274)`.
4. §3.8 prose line 250: "plus 275 labels" → "plus 274 labels".
5. Then extend `audit_numbers.py --check` so it would have caught all four: (a) parse breakdown
   rows regardless of first-column formatting (or sum ALL numeric table rows under a header), and
   (b) add the size-bucket counts (÷1.2=139, ÷1.4=209, 59/66=46, unexplained=274) to the checked
   labels including their Q-row references. Tripwire both new branches.
6. Optional: either re-dump `MODAL_*`/`PREFAB_*` into `design_audit/` so §6.4 evidence resolves, or
   soften the report's evidence citation to what is on disk.

Re-derivation scripts: `scratchpad/redderive.py` (mine, independent of `audit_numbers.py`).
