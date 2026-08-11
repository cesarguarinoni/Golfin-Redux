# SPEC — `tournaments_mode_card`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files.

## Status

See `STATUS.md`. Filed 2026-08-10 by Architect. `SPEC_READY`.

## Goal

Tournament mode (T7 TournamentSelection browse screen + signup/round/leaderboard/result flow) is fully implemented but has **no production entry point** — the only route is the dev-only "TOURNAMENTS (TEMP)" button on ModeSelection. Add a fifth mode card, **TOURNAMENTS**, to the Main Screen mode carousel (Home) and the full-screen Mode Select list, reusing the existing `ModeHomeCard` prefab + `ModeCardController` pipeline unchanged, routing PLAY to the existing `ScreenId.TournamentSelection`. New card texts are localized (EN + JP). No new UI hierarchy — the card is a data row; both card lists are instantiated at runtime from `ModesDatabaseCSV`.

## Card content (Cesar-approved copy)

| Field | EN | JP |
|---|---|---|
| Title | TOURNAMENTS | トーナメント |
| Subtitle (tagline) | Compete for the top of the leaderboard. | リーダーボードの頂点を競おう。 |
| Description | Enter live tournaments, play the featured holes, and climb the leaderboard before time runs out. Finish high to claim your share of the prizes — every stroke counts. | 開催中のトーナメントに参加して対象ホールをプレイし、期間内にリーダーボードを駆け上がろう。上位でフィニッシュして賞品を手に入れよう。一打一打が勝負を決める。 |
| Entry fee | none → renders "NO ENTRY FEE" (existing `entryFee = 0` path) | 参加費無料 |
| Rewards | Varies by tournament | トーナメントごとに異なります |

## Reference

- No new Figma frame — the card reuses the shipped mode-card visuals (§6.2 metrics already live in `ModeHomeCard` / full-screen card prefabs). Fidelity table N/A; regression is against the existing Practice/Multiplayer cards.
- Tournament target screen: T7 TournamentSelection (Figma 13386:1758), already implemented.

## Architecture context

- **Asmdef boundaries affected:** none new. All edited scripts live in the main UI assembly; `LocalizationManager` is already referenced by `ModeCardController.SetTitleText`.
- **Existing code referenced:**
  - `Assets/Scripts/UI/ModeSelect/ModeData.cs` — DTO for `Resources/Data/modes.csv`
  - `Assets/Scripts/UI/ModeSelect/ModesDatabaseCSV.cs` — CSV loader singleton (`LoadFromCSV`, `ParseCsvLine`, `ApplyDemoLock`)
  - `Assets/Scripts/UI/ModeSelect/ModeCardController.cs` — `Bind`, `SetState`, `SetTitleText`, `UpdateEconomyRows`
  - `Assets/Scripts/UI/ModeSelect/ModeCarouselController.cs` — `HandlePlayClicked` (home carousel routing)
  - `Assets/Scripts/UI/ModeSelect/ModeSelectScreenController.cs` — `HandlePlayClicked` (full-screen routing)
  - `Assets/Scripts/UI/Tournaments/TournamentDevEntryButton.cs` — proves the route: `ScreenManager.Instance?.ShowScreen(ScreenId.TournamentSelection)`
  - `Assets/Scripts/UI/ScreenManager.cs` — `ScreenId.TournamentSelection` already exists
- **Existing assets referenced:**
  - `Assets/Resources/Data/modes.csv`
  - `Assets/Localization/LocalizationText.csv` (+ `Tools/Localization/Import Text CSV` menu → `LocalizationTextTable.asset`)
- **Manager APIs used:** `LocalizationManager.Get(string key)` (returns the key itself when missing — that is the fallback contract used below), `ScreenManager.Instance.ShowScreen(ScreenId)`.

## Implementation

### 1. `Assets/Resources/Data/modes.csv` — new row + one new column

Append a new **optional** header column `rewardsTextKey` (after `reward3Amount`). Existing rows need no edit for it — `LoadFromCSV` guards every column with `idx < cols.Length`.

Add the tournaments row and renumber the two locked "coming soon" modes so Tournaments sits directly after Practice:

```
id,title,tagline,description,entryFee,rewards,locked,target,order,versusStrokeCapOverPar,reward1Type,reward1Amount,reward2Type,reward2Amount,reward3Type,reward3Amount,rewardsTextKey
tournaments,TOURNAMENTS,Compete for the top of the leaderboard.,"Enter live tournaments, play the featured holes, and climb the leaderboard before time runs out. Finish high to claim your share of the prizes — every stroke counts.",0,0,false,tournaments,3,0,,,,,,,MODE_REWARDS_VARY
```

- `entryFee = 0` → existing code renders "NO ENTRY FEE" (see step 4 for localizing that string).
- `rewards = 0` (legacy int) — the REWARDS row is instead driven by `rewardsTextKey` (step 4).
- `driving_range` order `3` → `4`; `missions` order `4` → `5`. No other cell changes.
- Note the row keeps English tagline/description in the CSV as fallback; display strings come from localization (step 5) via the key convention in step 3.

### 2. `ModeData.cs` — one field

```csharp
// rewardsTextKey — optional localization key; when set, the REWARDS row shows this
// localized TEXT (no coin icon, no amount) instead of "x{rewards}". Used by
// tournaments ("Varies by tournament"). Empty for all other modes.
public string rewardsTextKey = "";
```

Update the class doc-comment column list to include `rewardsTextKey`.

### 3. `ModesDatabaseCSV.cs` — parse the new column

In `LoadFromCSV`, alongside the other header lookups:

```csharp
int iRewardsTextKey = System.Array.IndexOf(headers, "rewardsTextKey");
```

and in the row loop:

```csharp
if (iRewardsTextKey >= 0 && iRewardsTextKey < cols.Length) mode.rewardsTextKey = cols[iRewardsTextKey].Trim();
```

Update the class doc-comment column list. Do NOT add tournaments to `AddFallbackModes()` — the fallback is an editor-safety path only; if the CSV is missing we have bigger problems. (NOTE: acceptable to add it there too if the implementer prefers symmetry; not required.)

`ApplyDemoLock` needs **no change**: in a GOLFIN_DEMO build it already locks every non-practice mode, so the tournaments card auto-shows the Coming-Soon treatment in the demo. This is the desired demo behavior.

### 4. `ModeCardController.cs` — rewards-text row + localized "NO ENTRY FEE"

In `UpdateEconomyRows(ModeData mode)`:

```csharp
bool hasFee      = mode.entryFee > 0;
bool hasTextRwd  = !string.IsNullOrEmpty(mode.rewardsTextKey);
bool hasRewards  = hasTextRwd || mode.rewards > 0;
string feeText   = hasFee ? $"x{mode.entryFee}" : LocalizationManager.Get("MODE_NO_ENTRY_FEE");
string rwdText   = hasTextRwd ? LocalizationManager.Get(mode.rewardsTextKey) : $"x{mode.rewards}";
```

- Replace the hardcoded `"NO ENTRY FEE"` with the `MODE_NO_ENTRY_FEE` lookup (localizes the string for ALL cards; EN value is identical to today's hardcoded text, so non-JP behavior is unchanged).
- Rewards coin icon: `rewardsCoin.gameObject.SetActive(hasRewards && !hasTextRwd);` — the text variant shows no coin. (The expanded container has no serialized rewards coin; only the label/amount fields, which pick up `rwdText` automatically.)
- Everything else in the method (demo hide, colors, expanded rows) unchanged — `rwdText` flows into both `rewardsAmount` and `rewardsAmountExp`.

### 5. `ModeCardController.cs` — localized tagline/description with CSV fallback

Titles are already localized in `SetTitleText` via key `"MODE_" + title` (→ `MODE_TOURNAMENTS`). Extend the same fallback pattern to tagline/description with **id-based** keys:

```csharp
// Localize tagline/description by convention (id-based): MODE_<ID>_TAGLINE / MODE_<ID>_DESC.
// Get() returns the key itself when not found; fall back to the raw CSV string in that
// case, so existing modes without keys are pixel-identical to today.
private static string Localize(string key, string fallback)
{
    string s = LocalizationManager.Get(key);
    return string.Equals(s, key, System.StringComparison.Ordinal) ? fallback : s;
}
private string LocTagline() => _data == null ? "" :
    Localize($"MODE_{_data.id.ToUpperInvariant()}_TAGLINE", _data.tagline);
private string LocDescription() => _data == null ? "" :
    Localize($"MODE_{_data.id.ToUpperInvariant()}_DESC", _data.description);
```

Replace every direct read of `_data.tagline` / `_data.description` (and `mode.tagline` / `mode.description` in `Bind`) with these helpers. Sites (6 total):

- `Bind`: `subtitleTextExpanded.text`, `explanationText.text`, `descriptionTextExpanded.text`
- `SetState`: `explanationText.text` (tagline/description swap), `descriptionTextExpanded.text`, `subtitleTextExpanded.text`

In `Bind`, ensure `_data`/`ModeId` are assigned **before** the text block (they already are — first lines of the method).

### 6. `ModeCarouselController.cs` + `ModeSelectScreenController.cs` — PLAY routing

Add a case to **both** `HandlePlayClicked` switches, mirroring the existing `hole_select` case and the proven `TournamentDevEntryButton` route:

```csharp
case "tournaments":
    if (ScreenManager.Instance != null)          // ModeSelectScreenController: use its `sm` local
        ScreenManager.Instance.ShowScreen(ScreenId.TournamentSelection);
    else
        Debug.LogWarning("[ModeCarousel] Tournaments PLAY — ScreenManager not found.");
    break;
```

No entry-fee spend applies (`entryFee = 0`); tournament-level entry fees are owned by the tournament signup flow (`TournamentSignupModalController`), not the mode card.

### 7. `Assets/Localization/LocalizationText.csv` — five new rows

Place them next to the existing `MODE_*` block (`MODE_ENTRY_FEE` / `MODE_REWARDS` / `MODE_PRACTICE`):

```
MODE_TOURNAMENTS,TOURNAMENTS,トーナメント
MODE_TOURNAMENTS_TAGLINE,Compete for the top of the leaderboard.,リーダーボードの頂点を競おう。
MODE_TOURNAMENTS_DESC,"Enter live tournaments, play the featured holes, and climb the leaderboard before time runs out. Finish high to claim your share of the prizes — every stroke counts.",開催中のトーナメントに参加して対象ホールをプレイし、期間内にリーダーボードを駆け上がろう。上位でフィニッシュして賞品を手に入れよう。一打一打が勝負を決める。
MODE_REWARDS_VARY,Varies by tournament,トーナメントごとに異なります
MODE_NO_ENTRY_FEE,NO ENTRY FEE,参加費無料
```

Then run **Tools → Localization → Import Text CSV** to regenerate `LocalizationTextTable.asset` (commit both files). NOTE: `LocalizationPlaymodeHook` may auto-import on play — run the menu item anyway so the committed asset is current.

## Acceptance checklist (Implementer fills in `IMPLEMENTER_REPORT.md`)

Each item MUST be marked `PASS` or `FAIL` with a one-sentence justification citing what was measured.

- [ ] Home carousel shows 5 cards; TOURNAMENTS sits between PRACTICE and DRIVING RANGE (order 3), unlocked, styled identically to other cards (gold title + white border when centered)
- [ ] Tournaments card collapsed + expanded: fee row shows "NO ENTRY FEE" (no coin icon), REWARDS row shows "Varies by tournament" (no coin icon, white text)
- [ ] Expanded card shows the tagline as subtitle and the full description body
- [ ] PLAY on the Tournaments card (home carousel) opens the TournamentSelection screen
- [ ] PLAY on the Tournaments card (full-screen Mode Select list) opens the TournamentSelection screen
- [ ] JP language: title トーナメント, tagline/description/rewards/fee strings show the JP values from the table above
- [ ] Regression: PRACTICE and Multiplayer cards render byte-identical text to before (fallback path — no `MODE_PRACTICE_TAGLINE`-style keys exist for them), and their PLAY routes still work (hole select / matchmaking modal + fee spend)
- [ ] Regression: locked cards (Driving Range, Missions) still show Coming-Soon treatment at orders 4 and 5
- [ ] `TournamentLoopCaptureHarness` still passes its ModeSelect → "TOURNAMENTS (TEMP)" click path (temp button untouched)
- [ ] Unity Console has no errors related to this task
- [ ] Spec deviations (if any) flagged at the bottom of the report with justification

## Files / hierarchy this task touches

- `Assets/Resources/Data/modes.csv` — tournaments row, `rewardsTextKey` column, order renumber
- `Assets/Scripts/UI/ModeSelect/ModeData.cs` — `rewardsTextKey` field
- `Assets/Scripts/UI/ModeSelect/ModesDatabaseCSV.cs` — parse `rewardsTextKey`
- `Assets/Scripts/UI/ModeSelect/ModeCardController.cs` — rewards-text row, localized NO ENTRY FEE, tagline/desc localization helpers
- `Assets/Scripts/UI/ModeSelect/ModeCarouselController.cs` — `case "tournaments"` route
- `Assets/Scripts/UI/ModeSelect/ModeSelectScreenController.cs` — `case "tournaments"` route
- `Assets/Localization/LocalizationText.csv` + `Assets/Localization/LocalizationTextTable.asset` — 5 keys + reimport
- **No scene or prefab edits** — cards are runtime-instantiated from `cardPrefab`; nothing to wire in the Inspector.

## Smoke evidence

Editor play-mode drive: boot ShellScene → Home → swipe carousel to TOURNAMENTS → verify economy rows → PLAY → TournamentSelection appears → back → bottom-nav Tee (ModeSelection) → tap Tournaments card → PLAY → TournamentSelection. Repeat the card render checks with language switched to JP. Screenshot the collapsed + expanded card (EN and JP) into `screenshots/`. This is a UI/data task — human-in-the-loop play-and-confirm per Lesson O is sufficient; no position-trace needed.

## Out of scope (do NOT do these)

- Do NOT remove or modify `TournamentDevEntryButton` / the "TOURNAMENTS (TEMP)" button on ModeSelection — `TournamentLoopCaptureHarness` clicks it by name. Retiring it is a separate follow-up once Cesar approves the card as the canonical entry.
- No tournament gameplay/backend changes (`Golfin.Tournaments`, `TournamentService`, signup fee logic).
- No localization of OTHER modes' taglines/descriptions (keys can be added later CSV-only, zero code, thanks to step 5's convention).
- No new prefab, no scene edits, no rebuild of card visuals.
- No changes to `versus_1v1` / `practice` routing, fee spend, or the demo gate.
