# IMPLEMENTER_REPORT — `design_consistency_audit`

**Iteration shape:** `design-audit:instrument-and-measure`

**Canonical screenshot:** `screenshots/ModeSelectionScreen_sheet.png` — live build beside node
`13026:1924`. It carries the most findings of any single surface: every label 28.6 % undersized
(÷1.4), the MISSIONS copy bug, and 5 of the 20 `Outline`-as-border instances.

Deliverable: **`Docs/Reports/DESIGN_CONSISTENCY_AUDIT.md`**.

## Acceptance checklist

| # | Item | Verdict | Measurement |
|---|---|---|---|
| A0 | Reference renders | **PARTIAL** | 29 renders in `reference/`, all ≥1024 long edge except `TournamentSignupModal` (1020 natural; Figma will not render above 1:1 — requesting 2040 returns 1020). Node table re-pulled and **7 rows corrected** (`reference/NODE_RESOLUTION.md`). Modal/Tier-2 stragglers not pulled. |
| A1 | Token sheet complete | **PASS** | `Docs/Design/DESIGN_TOKENS.md`: 9 EN steps, 1 JP, 15 flat colours, `Gold`/`Silver`/card-fill resolved from SVG stops with the `<linearGradient>` excerpt quoted. Unresolved variables **listed, not guessed**. |
| A2 | Tripwire (§20) | **PASS** | `liberationSans 0→1→0`, `outline 15→16→15`, sprite `Home Background→<NONE>→Home Background`. JSONs `TRIPWIRE_01/02/03`. `git status` on `Assets/Prefabs|Scenes|Localization|Fonts` clean either side; `git diff --stat HEAD -- Assets/Scenes` empty. |
| A3 | Dumps | **PASS for screens / PARTIAL overall** | **17 screen surfaces in EN and JA** + 2 prefabs. 10 reached by real `onClick`; 9 by `ShowScreen` because no player path exists from a fresh session — each dump records `reachedVia`. **LiberationSans reconciled: 41 live labels vs the stated 46** — the baseline double-counts (`m_fontAsset` + `m_sharedMaterial` per label). **13 in-scope modals + 6 Tier-2 screens dumped in EN only** (no JA modal pass, no modal crop sheets). |
| A4 | Lint | **PASS** | 74 prefabs (12 FAIL / 919 WARN) + 5 live roots (30 FAIL / 982 WARN), tables in the report § 4. Every FAIL is `9slice-collapse-*` and appears in fix group Q4. |
| A5 | Node-spec layer | **PARTIAL** | `get_metadata` + `get_design_context` pulled for `13026:2366`; the node's per-element truth (45/39/66 px, `#EEDC9A`, r50, 3px white border, `#133453→#091B33`) drove the § 3.8 comparison and **corrected this audit's own ÷1.2 reading to ÷1.4**. `figma_node_to_spec.py` specs not generated for the other rows. |
| A6 | Crop sheets | **PASS for 15 screens** | 15 live captures via real navigation / re-seat, each md5-distinct and phantom-checked, paired with its node render: `screenshots/<Screen>_sheet.png`. Residual differences named as data-vs-defect (§ 3.8's colour note). Modals have no sheets. |
| A7 | Rendered-size population | **PASS** | Every dump carries `fontSize`, `lossyScaleY`, `renderedPx` + formula, `autoSize`/`min`/`max`. Populations quantified against the NODE, not against a guessed divisor: 1387 on-scale · 209 ÷1.4 · 144 ÷1.2 · 47 at 59/66 · 275 unexplained, with a recommendation. No site is called wrong-size from a serialized number. |
| A8 | Shape tables | **PASS for 8 of 9, (ix) partial** | (i) 41 · (ii)+(iii) **209 ÷1.4 + 144 ÷1.2 + 47 at 59/66 + 275 unexplained** · (iv) 20 · (v) **refuted, 0** · (vii) 226 · (viii) 710 visible / 291 panel-sized · 9-slice 42 · (ix) node side captured, live radius not measured. |
| A9 | Linter untouched in behaviour | **PASS** | `GeneralShopCard_lint.json` md5 `78c23b5b237c2842ecf94c24811a48bd` **byte-identical** before and after the extraction. `LintRoot_ProducesTheSameFindingsAsLintPrefab` green. `git diff` on the linter shows only the extraction — no rule added, removed or re-tuned. |
| A10 | Nothing production changed | **PASS (by diff — deviation 1)** | This task's diff touches only `Assets/Editor/UIFidelity/*` and `Docs/**`. The tree's 121 foreign dirty paths pre-date kickoff and are listed in `HEARTBEAT.log`. |
| A11 | Report + fix list | **PASS** | `Docs/Reports/DESIGN_CONSISTENCY_AUDIT.md`, 6 fix groups with ids/files/op/estimate/blast radius; § 6 non-empty (5 items). |
| A12 | EditMode | **PASS** | `RUN FINISHED passed=2706 failed=0 skipped=3`. New suite `DesignAuditToolingTests` **12/12**. |
| A13 | GPS untouched | **PASS** | Prefab sweep excludes `/Gps/` by path filter (74 prefabs, zero Gps). No Gps path in any dump, lint or render. |
| A14 | Deviations | **PASS** | Report § 7 — three, each with the reason. |

## Red-team FAIL — both blockers fixed

1. **The headline JA finding was S1 on no visual evidence.** Derived from `font.name`; nobody looked
   at a Japanese screen. The red-team captured one: **CJK renders correctly through TMP's fallback**
   (`screenshots/REDTEAM_ja_MissionSelection.png`). Downgraded **S1 → S3**, Q1 marked DEFERRED and
   explicitly "not a visible defect". The count was also wrong — "860 of 873" subtracted NotoSansJP
   *bindings* from *CJK labels*, two different populations; the real split is **866 on Latin / 7 on
   NotoSansJP**.
2. **Shape counts contradicted between § 1 and § 3, and reproduced from no corpus.** Root cause: the
   EN and JA passes wrote to the SAME filename, so each overwrote the other and the corpus was a
   locale mix — locale-INVARIANT properties differed by locale, which cannot happen. Fixed at the
   source: the dumper now writes `<Screen>__<locale>.json`; all passes re-run; EN/JA now agree
   exactly (`Image.Type.Filled` 561 = 561). Every number in the report is recomputed from **one
   stated corpus** (17 distinct EN screens, Inventory tabs collapsed).

## What the audit found that the SPEC did not anticipate

1. **JA renders on a Rubik asset (507 labels, 1 exception).** `LocalizedText` swaps the string and
   never the font. The largest finding here, and invisible to every prior gate because they only
   looked at EN.
2. **The SPEC's node table is wrong in 7 rows** — an arrow asset, a canvas, a card, a component and
   two page ids sitting in the screen column. Followed blindly it would have pointed A5 and A6 at
   the wrong subtrees.
3. **The 46-site LiberationSans baseline double-counts.** Each TMP writes the font GUID twice, so a
   YAML grep sees 2× the labels. Real total 41; the headline "×77" is ~38.
4. **35 of HomeScreen's 131 labels are auto-sized**, so `fontSize` is a result, not an authored
   value — it moved 49.05 → 51 in the tripwire from a font swap alone. Any size finding taken from
   the serialized number on those sites would have been fiction.

## Honest state

The instruments are built, tested and proven (A2, A9, A12 on hard evidence). **17 screen surfaces
are measured in EN and JA, 8 of the 9 shapes are enumerated with exhaustive site counts, and the
report carries 10 fix groups.**

**17 screen surfaces dumped in EN and JA. 13 in-scope modals and 6 Tier-2 screens dumped in EN
ONLY.** 15 crop sheets (screens only, none for modals); 8 of 9 shapes enumerated; 10 fix groups.

*(An earlier revision of this line said the modals were dumped "in EN and JA". They were not —
only `modals:en` was ever run, and all 13 `MODAL_*.json` carry `locale:"en"`. Caught by the
self-reviewer against the JSON rather than the prose.)*

Still missing, listed in report § 6: `figma_node_to_spec.py` specs for the rows beyond the one node
pulled (A5), crop sheets for the modals, the live-radius half of shape (ix), and the 7 Tier-2
screens. Nine of the 17 surfaces were re-seated with `ShowScreen` rather than tapped — no player
path to them exists from a fresh session — and each such dump records `reachedVia` rather than
passing itself off as a tap. Five GPS modals were swept in by the first modal pass and **removed**;
the pass now filters on namespace (A13).


---

## Red-team round 2 — the four blockers, closed

All four were report-only. No production file changed in this round; the only dirty code file
remains `DesignAuditDumper.cs` (+8 lines, the locale suffix from round 1).

| # | Blocker | Fix | Verified by |
|---|---|---|---|
| 1 | §3.5 breakdown summed to **447** vs its own **225** header | Table regenerated on the 17-corpus: `Bar 182 · BarContainer 33 · BarPending 8 · GhostBar 2` = **225** | `audit_numbers.py` SUM check |
| 2 | §3.6 header **"442 visible, 26 panel-sized"** vs body **701/291** | Header now **701 visible, 291 panel-sized**; "Each of them" → "Each of the **291**" | `audit_numbers.py` label check |
| 3 | Q5 cited **226** | Corrected to **225** | label check |
| 4 | JA headline **866/873** computed on all 21 dumps | **660 / 7** on the stated 17-screen corpus; the superseded figures kept, flagged, in the correction note | label check + `declares_scope` |

### Two defects I found while closing those four (neither was on the red-team's list)

5. **`Image.Type.Filled` 561 sat unlabelled beside §3.5's 225.** It is legitimate — it is the
   EN-vs-JA parity proof across **all 21 dumps** — but it read as a fifth contradiction. The line
   now names its own scope and points at §3.5 for the shape count.
6. **I asserted "no fill in the corpus sits between α 0.02 and α 0.2".** False: **eight** do, and
   three `VerticalDivider`s sit exactly on the boundary. Replaced with a measured sensitivity table
   (α 0.02→0.30). The actionable figure, panel-sized **291**, is stable across α 0.15–0.30; the
   *visible* figure is soft (690–709) and the report now says so instead of implying precision.

### The checker that was passing over all of this

`audit_numbers.py --check` reported "none" on a report that had a stale header, because it required
the number to be its own bold span (`**442**`) and the header bolds the whole phrase
(`**S2/S3 — 442 visible, 26 panel-sized**`). Same failure shape as the two checkers before it: it
was never proven capable of failing. Rewritten so the rule is *"any line naming a shape and stating
a count must state that shape's canonical value"* — formatting-independent — plus a breakdown-sum
check. Scope exemptions are syntactic (the line declares its own scope), never a list of blessed
numbers.

**Both branches tripwired before I trusted the green:**

| Tripwire | Expected | Result |
|---|---|---|
| Restore the stale `442 visible, 26 panel-sized` header | FAIL | `STALE line 170: panel-sized states [26, 442], corpus = 291` ✅ |
| Corrupt one breakdown row (`Bar` 182 → 404) | FAIL | `STALE line SUM: {...} states 447, corpus = 225` ✅ (reproduces the original 447 exactly) |
| The real report | pass | `contradictions vs this corpus: none` ✅ |

## Acceptance tests

`tests-run {mode: EditMode}` — **2709 total, 2706 passed, 0 failed, 3 skipped** (the 3 skips are
pre-existing `HoleCompleteDriverTests` Stage-C1 skips, untouched by this task).

`Results` only enumerates skipped/failed tests, so a green summary is NOT evidence my 12 tooling
tests ran — the documented `tests-run` vacuous-pass trap. Proven live by tripwire: flipping
`RenderedPx_HalvesUnderAHalfScaleParent`'s expectation 20 → 999 produced
`FailedTests: 1 … Expected: 999.0d … But was: 20.0d`, then restoring returned 0 failed. The suite is
genuinely inside the 2709.

**Needs manual on-device verification:** none. This task ships no runtime code — the deliverable is
a document plus editor-only instruments, and every number in it is regenerable from the committed
JSON dumps by `python3 Docs/Scripts/audit_numbers.py`.


---

## Red-team round 3 — four blockers, all confirmed against my own extraction

I re-derived each before touching it; all four were real.

| # | Blocker | Verified how | Fix |
|---|---|---|---|
| 1 | §3.5 carried a fifth row `` `GhostBar` / `Fill` \| 3 `` and summed to **228** vs its 225 header | Searched all 21 EN dumps: Filled leaf names are `Bar` 402 / `BarContainer` 133 / `BarPending` 24 / `GhostBar` 2. **No leaf named `Fill` exists.** The row matched ZERO data | Row deleted; table sums to 225 |
| 2 | Q7b cited ÷1.2 = **144** | Independent bucket derivation: 139 | → 139 |
| 3 | Q8 cited unexplained = **275** | → 274 | → 274 |
| 4 | §3.8 prose "plus 275 labels" | → 274 | → 274 |

Blocker 1 is the serious one: a fabricated table row is worse than a stale number, because nothing
in the corpus can ever produce it.

### Why the gate reported "none" over all four

Two independent blind spots, both now closed and both tripwired:

1. **The SUM check parsed only 4 of the 5 rows.** Its regex wanted `` | `Word` | digits ``; the
   malformed first column `` `GhostBar` / `Fill` `` didn't match, so it summed the 4 rows it could
   see, got 225, and passed — while a human reading the table gets 228. Replaced with a cell-based
   parse that reads every row, plus a **fabrication check**: every object named in §3.5 must occur
   in the corpus with exactly that count. That check catches B1 directly, with no knowledge of the
   right answer.
2. **The size buckets had no coverage at all** — the gate only ever checked four shape labels, so
   B2/B3/B4 were invisible to it. Buckets are now checked wherever they are named, including inside
   a `§ 3.8 (N)` citation (how Q7b and Q8 quote theirs), with the citation bound to the row's
   *subject* so Q7 (the ÷1.4 row, whose prose says "not to the ÷1.2 target") isn't flagged.
   Matching runs on whitespace-normalised **paragraphs**, because §3.8 states the unexplained bucket
   as "plus 274 labels no conversion explains" — wrapped across a line break, and not using the word
   "unexplained" at all. My first attempt at this rule caught B2 and B3 but still missed B4.

**All six tripwires fire; the real report is clean:**

| Planted defect | Caught |
|---|---|
| B1 fabricated `GhostBar / Fill` row | ✅ `matches NO image in the corpus` + SUM 228≠225 |
| B2 Q7b → 144 | ✅ `bucket ÷1.2 states 144` |
| B3 Q8 → 275 | ✅ `bucket unexplained states 275` |
| B4 prose → 275 labels | ✅ `line 247: bucket unexplained states 275` |
| A real row name with a wrong count (`Bar` 190) | ✅ row + SUM |
| The round-2 header defect (442/26) | ✅ still caught |

## Two things I found that the red-team logged as secondary — both were larger

**The modal evidence did not exist.** The report claimed 13 modals dumped; `design_audit/` held
exactly 42 files = 21 EN + 21 JA screens and **zero `MODAL_*`**. The round-1 corpus rebuild wiped
them and `modals:en` was never re-run, so the report cited evidence that had ceased to exist.
Re-ran the pass: 13 modals regenerated, A13 clean (no GPS modal present — `VoteCreate`,
`RoundComplete`, `VenuePicker`, `CheckInConfirm`, `GiftSend` all absent), all `locale:"en"`.

**Regenerating it silently moved the audit's own numbers.** The modal pass also dumps the six
Tier-2 auth screens, which carry no `MODAL_` prefix — so they entered the corpus, took it from 17
screens to 23, and moved **÷1.2 139 → 194** and **unexplained 274 → 279**, while `Filled` 225 and
the flat fills 701/291 stayed identical. Had I re-run the pass and not re-checked the corpus size, I
would have shipped a report whose buckets disagreed with its own stated rule for the third time.
`audit_numbers.py` now excludes the six by name with the reason at the exclusion site. **Corpus back
to 17; every number back to canonical.**

**The evidence was not shipping at all.** All 61 dumps lived only under
`Docs/Diagnostics/_capture/`, which `.gitignore` excludes — zero tracked. Every JSON citation in the
report pointed at a file no other machine could open, including the Architect's. Copied to the
tracked `Docs/Reports/DesignAudit/` (9.4 MB raw, ~0.3 MB packed) and `audit_numbers.py` now computes
from that copy, so the numbers are reproducible by anyone with the repo. This is under `Docs/**` and
therefore inside A10; `.gitignore` was NOT touched.


---

## Round 4 — red-team PASSED, and the two nits it handed over turned out to hide a real defect

The round-4 red-team could not break the deliverable: it re-derived every headline with its own
extractor (all exact), confirmed the tracked evidence is byte-identical to the tool's output, verified
A9 empirically, and set `ARCHITECT_REVIEW_PASS`. It also handed over two non-blocking nits. Chasing
the second one found a defect neither of us had seen.

**Nit 1 — "nine scale steps" lists 33 twice.** Real: **nine type STYLES over eight distinct SIZES**.
`EN/Caption_2` (Regular 400) and `EN/Caption_2_Medium` (Medium 500) are both 33 px and differ by
weight, not size. The matcher always used the eight distinct sizes, so no count was ever affected —
only the prose was wrong. Corrected in the report (3 places) and in `DESIGN_TOKENS.md`.

**Nit 2 — "the Tier-2 exclusion is by name, harmless today."** It was not harmless, and the name list
was a symptom. Tracing why those six screens needed excluding at all:

> `HomeScreen__en.json` carried `reachedVia:"harness ShowScreen (Tier 2)"`.

The Tier-2 loop calls `Force(id)` and then dumps `CurrentScreenRoot()` under `r.name` **without
checking the re-seat landed**. For an id that resolves nowhere, `Force` returns true, the screen does
not change, and the loop dumps whatever IS active — HomeScreen — under its own filename, overwriting
the Tier-1 dump. The measurements were identical (131 texts / 219 images / 35 auto-sized, matching
its untouched JA sibling), so no count moved and every gate passed. Only the provenance was
corrupted — on the single field this report uses to admit which surfaces were re-seated rather than
tapped.

Fixed structurally, both halves:
1. **Verify before dumping.** If the active root's name doesn't match the requested id, log and skip
   rather than overwrite another screen's dump.
2. **`TIER2_` prefix**, exactly as modals carry `MODAL_`. This is why the corpus filter can now
   exclude by prefix instead of a hand-kept name list — closing nit 2 at the cause rather than the
   symptom.

Re-ran `modals:en` and `dump:en`. **Zero measurement changes across the whole corpus**; the only
diffs are the six Tier-2 files renamed to `TIER2_*` and two `reachedVia` strings. Every headline is
byte-for-byte what it was: 17/17 screens, LiberationSans 36 (+5), Outline 20, Shadow 0, Filled 225
{182/33/8/2}, 701/291, CJK 660/7, buckets 1389/209/139/46/274 over n=2057.

**A third finding, from the same thread.** `DumpCurrent` never passed a `via`, so every TAPPED screen
recorded `reachedVia:"unspecified"` — read literally, 11 of 17 corpus dumps claim no provenance at
all, on a report whose §7 leans on that field. The navigation was always real (`Tap(slot)` drives the
bottom-nav button's own `onClick`); only the record was missing, so the field understates real
navigation rather than overstating a harness as a tap. The runner now records it. **The committed
dumps deliberately predate that fix and are labelled in the report instead of regenerated** — because
re-running a pass to improve a label is precisely how the Tier-2 contamination got in, and no
finding is worth risking the corpus for a better string.

**Tests after the runner change:** EditMode 2709 total / 2706 passed / **0 failed** / 3 pre-existing
skips.
