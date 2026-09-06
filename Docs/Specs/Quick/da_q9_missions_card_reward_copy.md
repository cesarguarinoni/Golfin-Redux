# Quick · `da_q9_missions_card_reward_copy` — the MISSIONS card says "Varies by tournament"

**From:** `design_consistency_audit` § 3.10 (fix group Q9), approved by Cesar 2026-09-06. **Est:** XS.

## What is wrong

`Assets/Resources/Data/modes.csv` row `missions` carries `rewardsTextKey = MODE_REWARDS_VARY`
(copied from the `tournaments` row), so `ModeCardController` (line ~514: `hasTextRwd ?
LocalizationManager.Get(mode.rewardsTextKey) : $"x{mode.rewards}"`) renders **"Varies by
tournament"** on the MISSIONS card — collapsed AND expanded (`…/RewardsRow/RewardSlot2/CoinValueGroup/
Reward2Amount` + its `Exp` twin). Visible on Home's carousel and on Mode Select. The node
(`13026:1924`, MISSIONS card) shows a coin + **`x200 (average)`**.

## Fix (data + one string; no code)

1. New string — `Assets/Localization/LocalizationText.csv`:
   `MODE_REWARDS_MISSIONS_AVG,x200 (average),x200（平均）` — EN and JA in the same commit.
   The number is the node's; if the missions catalog's real average differs, use the real average
   and say so in the report (Cesar decides the copy, not the code).
2. `modes.csv` row `missions`: `rewardsTextKey` → `MODE_REWARDS_MISSIONS_AVG`. The coin icon must
   show the way it does for the node (check how `hasTextRwd` treats the icon — if the text path
   hides the coin, the icon for this key stays ON; a one-line condition is acceptable, flag it).
3. **Importer, both catalogs** (PIPELINE_HARDENING §24):
   `python3 Tools/content/import_content.py --env-file … --catalogs texts,modes` — PLAN read and
   quoted (`texts add 1`, `modes change 1`), `--apply`, publish `texts` + `modes` from the admin,
   `export_content.py --check` clean. CONFLICTS ⇒ stop and report.

## Done when

- Still of the MISSIONS card, collapsed and expanded, EN and JA, next to the node crop from
  `Docs/Specs/Active/design_consistency_audit/screenshots/ModeSelectionScreen_sheet.png`.
- `grep -rn "MODE_REWARDS_VARY" Assets/Resources/Data/modes.csv` → tournaments row only.
- PLAN/APPLY/publish/`--check` lines quoted. `git status` shows only the CSV, the loc CSV, the
  regenerated `LocalizationTextTable.asset`.
