DONE

All three of Cesar's 2026-09-07 decisions are APPLIED. Nothing is open; no FAIL items remain.

(1) MissPowerMul 0.20 -> 0.40 — applied to ControlsConfig + controls.csv. Re-measured: the duff
    is now 30.9 yd of 329 (9.4%), still half the <=25% acceptance bar, launch 3.82 deg.
    PuttMissPowerMul left at 0.30 (one knob moved), so the putt duff is unchanged at 28.6%.
    One test moved with it: the putt-duff case asserted PuttMissPowerMul > MissPowerMul, which
    held only by accident at 0.30 vs 0.20; it now asserts D3's actual rule (an absolute floor,
    "never stationary"). EditMode after the retune: 2765 total, 2762 pass, 0 fail, 3 skip.
    bot_difficulty.csv re-run and STILL zero diff (md5 03ab6300...).

(2) Retired rows DEACTIVATED and published — texts v44, "0 added, 0 changed, 4 deactivated",
    is_active true -> false on SHOT_GRADE_JUST / MISS / PERFECT / SHANK only. Repo re-exported,
    --check clean, content_version.txt texts=44, bundled table re-imported (1100 rows, no column
    leakage). NOTE: first deactivation in this catalog, so the exporter widened
    LocalizationText.csv with an is_active column — 2201 changed lines, 1096 of them ",true"
    appended to untouched rows.

(3) Needle perfect zone STAYS PURE green — veto not exercised, no change needed.

DONE 2026-09-07 on Cesar's approval. Implementation committed as a6462af8e; this folder
moved to Docs/Specs/Completed/ in the close-out commit that follows it.


Architect review 2026-09-07: PASS (ARCHITECT_REVIEW.md). Cesar decisions 2026-09-07: (1) MissPowerMul 0.20 -> 0.40 (Code applies with the closing commit), (2) retired SHOT_GRADE_JUST/PERFECT/SHANK/MISS rows: Code deactivates in the admin on Cesar's order, (3) Needle perfect zone stays PURE green. Then DONE -> Docs/Specs/Completed/.
