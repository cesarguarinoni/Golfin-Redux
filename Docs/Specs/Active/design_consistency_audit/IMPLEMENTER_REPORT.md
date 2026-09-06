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
