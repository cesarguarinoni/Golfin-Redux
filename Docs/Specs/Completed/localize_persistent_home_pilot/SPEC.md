# SPEC — `localize_persistent_home_pilot`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state.

## Status

See `STATUS.md`. Current: `SPEC_READY`.

## Goal

**First batch-conversion task of the game-wide localization sweep, run as a deliberately small pilot on the `Persistent/Home` group.** Its purpose is twofold:

1. **Convert the group's genuinely user-facing copy** to the existing localization system (`LocalizedText` binders for prefab text, `LocalizationManager.Get()` for code-driven text).
2. **Prove and document the batch-conversion workflow** — binder attach via the sanctioned helper, code `Get()` conversion, CSV key add + import, EN-unchanged verification, and a **JP smoke pass** — so the larger groups (Shop/Gacha 251, Other 282, Rankings/Tournaments 125, Hole/Results 114, Inventory/Bag 62) can be specced and executed with a validated recipe.

This task is scoped from the audit report `Docs/Reports/localization_audit_2026-07-22.md` (group `Persistent/Home`). **The audit's per-row `Class` is a heuristic triage, not ground truth** — a primary deliverable of this pilot is to confirm/override each row and record the misclassifications, because that feedback shapes every later batch.

## Existing system (use these — do not reinvent)

- `Assets/Localization/LocalizationManager.cs` — `static Get(string key)`; **global namespace**; fallback returns **the key itself** when missing (so a gap renders as literal `NAV_LEADERBOARD` on screen — this is exactly what the JP smoke pass exploits).
- `Assets/Localization/LocalizedText.cs` — binder MonoBehaviour, `[RequireComponent(typeof(TextMeshProUGUI))]`, refreshes on `OnLanguageChanged`. Binds **UGUI `TextMeshProUGUI` only**.
- `Assets/Scripts/UI/Editor/LocalizationEditorHelper.cs` — **`AddLocalizedText(GameObject textObject, string key)`** is the sanctioned way to attach a binder from an editor script. **Use this — do not hand-add the component.**
- `Assets/Localization/LocalizationText.csv` — 3-column source of truth: `key,English,Japanese`. **Edit the CSV, never the `.asset`.**
- `Assets/Localization/LocalizationTextImporter.cs` — `[MenuItem("Tools/Localization/Import Text CSV")]` imports CSV → `LocalizationTextTable.asset`. Run it after every CSV edit.
- `Assets/Localization/LocalizationDebugWindow.cs` — `[MenuItem("Tools/Localization/Language Debug")]` sets the bootstrap default language — used for the JP smoke capture.

## Triage — confirm each row before touching it

The audit flagged these `Persistent/Home` rows. **Verify each against the live prefab/scene/code before acting; do not blindly convert.** Where the audit was wrong, record it in `## Triage findings` (this feeds the audit-heuristic improvements).

### CONVERT — binder path (prefab `TextMeshProUGUI` → `LocalizedText` via `AddLocalizedText`)

| Asset | GameObject text | Key | Notes |
|---|---|---|---|
| `Assets/Prefabs/UI/HomeScreen.prefab` | `MAINTENANCE NOTICE` | **`HOME_MAINTENANCE_TITLE`** (exists, JP `メンテナンス情報`) | reuse existing key — **no new CSV row**; verify the text GO is static (not runtime-set) before binding |
| `Assets/Prefabs/UI/HomeScreen.prefab` | `NEXT HOLE` | **`HOME_NEXT_HOLE`** (exists, JP `次のホール`) | reuse existing key — **no new CSV row** |

### CONVERT — code path (`.text = "literal"` → `LocalizationManager.Get("KEY")`)

`Assets/Scripts/UI/PersistentUIManager.cs`, the screen-title `switch` (~lines 386–412). Each arm assigns `usernameText.text = "<TITLE>"`. Replace the literal with `Get()` using a **new `NAV_*` key** (or an existing key if one already carries identical English — dedup first):

| Literal | Proposed key |
|---|---|
| `LEADERBOARD` | `NAV_LEADERBOARD` |
| `MODE SELECTION` | `NAV_MODE_SELECTION` |
| `SELECT HOLE` | `NAV_SELECT_HOLE` |
| `TOURNAMENT LEADERBOARD` | `NAV_TOURNAMENT_LEADERBOARD` |
| `TOURNAMENTS` | `NAV_TOURNAMENTS` |
| `BOOST STAMINA` | `NAV_BOOST_STAMINA` |
| `REWARDS CENTER` | `NAV_REWARDS_CENTER` |

The `case … : usernameText.text = _username;` / `string.Empty` arms are **not** literals — leave them. Do not change the switch's control flow, only the 7 literal assignments.

### DO NOT CONVERT — audit misclassifications (document them, touch nothing)

- `HomeScreen.prefab` **"CHOTO"**, `PersistentUI.prefab` **"CHOTO"** — placeholder **player name**, overwritten at runtime. Dynamic, not copy.
- `HomeScreen.prefab` **"Course Name"** — runtime-set from hole data (verify; lean dynamic).
- `HomeScreen.prefab` maintenance **body** (`"Scheduled server maintenance: 2025/12/31…"`) — hardcoded-date **placeholder for server-driven live-ops copy**; not static UI copy. Leave unbound; document.
- `HomeScreen.prefab` **"x10" / "x04" / "x02"** — currency counts (audit `UNKNOWN`). Dynamic.
- `HomeScreenController.cs` `rewardPointsText.text = "0"` and `usernameText.text = "Player"` — both `// TODO: load real value` **placeholder defaults**. Dynamic; do not convert.

If verification shows any "CONVERT" row is actually runtime-driven, **move it to triage-skip and say so** — never bind a label whose text is overwritten at runtime (the binder would fight the runtime write).

## JP policy for keys (applies to this pilot and every later batch)

- **Reused keys** (`HOME_MAINTENANCE_TITLE`, `HOME_NEXT_HOLE`) keep their existing real JP — do not touch.
- **New keys** (the 7 `NAV_*`) get `English` = the literal, and `Japanese` = **the English text as a placeholder**, suffixed with the marker ` [JP-TODO]` so the human translator pass can grep them (e.g. `NAV_LEADERBOARD,LEADERBOARD,LEADERBOARD [JP-TODO]`). **Do not invent Japanese translations** — bulk JP is a later human-translator pass (per the audit task's out-of-scope). The `[JP-TODO]` placeholder keeps JP mode rendering English rather than a raw key, and is greppable for the translator.
  - *(If Cesar prefers a different placeholder convention, that overrides this — flag it in the report.)*

## Acceptance checklist (Implementer fills `IMPLEMENTER_REPORT.md`)

- [ ] **Binder path:** `HomeScreen.prefab` "MAINTENANCE NOTICE" and "NEXT HOLE" each carry a `LocalizedText` (added via `LocalizationEditorHelper.AddLocalizedText`) bound to `HOME_MAINTENANCE_TITLE` / `HOME_NEXT_HOLE`. Read back the live component's serialized `key` to prove it — quote it.
- [ ] **Code path:** the 7 `PersistentUIManager.cs` switch literals now call `LocalizationManager.Get("NAV_…")`; control flow otherwise unchanged (show the diff).
- [ ] **CSV:** 7 new `NAV_*` rows added (EN + `[JP-TODO]` JP); reused keys untouched; `Tools/Localization/Import Text CSV` re-run; report the new `LocalizationTextTable.asset` key count (was 227 → expect 234) and confirm no key duplicated.
- [ ] **EN unchanged:** capture `HomeScreen` (and one screen whose persistent-bar title was converted, e.g. Leaderboard) in **English** at iPhone-14 1170×2532 over the real boot→home flow; text reads identically to before. Cite the screenshot.
- [ ] **JP smoke pass:** via `Tools/Localization/Language Debug` set JP, re-capture the same screens; the 2 reused strings render their real JP (`メンテナンス情報` / `次のホール`), the 7 nav titles render the `[JP-TODO]` English placeholder (NOT a raw `NAV_*` key — a raw key on screen = FAIL, means the binder/Get wiring or import is broken). Cite the screenshot.
- [ ] **Triage findings** section present: per audit-flagged row, CONVERTED / SKIPPED-misclassified (with the reason + what the audit heuristic got wrong).
- [ ] **Scope containment:** `git status --porcelain` shows ONLY `HomeScreen.prefab`, `PersistentUIManager.cs`, `LocalizationText.csv`, `LocalizationTextTable.asset` (+ this task folder). No other prefab/scene/script mutated. Quote the output. (If a binding requires a `.meta` for a new nothing — there are no new assets here, so no new `.meta`.)
- [ ] Unity Console has no errors related to this task; project compiles (assets-refresh + console-get-logs clean).
- [ ] Spec deviations (if any) flagged at the bottom of the report.

## Files this task touches

- `Assets/Prefabs/UI/HomeScreen.prefab` — +2 `LocalizedText` binders (no visual change)
- `Assets/Scripts/UI/PersistentUIManager.cs` — 7 literals → `Get()`
- `Assets/Localization/LocalizationText.csv` — +7 `NAV_*` rows
- `Assets/Localization/LocalizationTextTable.asset` — regenerated via importer

## Not a Figma task

No Figma node — this is a text-binding conversion, not a visual redesign. **Rule 18 (Figma fidelity), Rules 16/17 (mesh), Rule 21 (UI-fidelity lint on a built prefab) are N/A.** The visual gate here is narrow and specific: **EN renders byte-identically to before** (no layout/appearance change from attaching a binder) and **JP mode renders translated/placeholder text, never a raw key.** Reviewers verify exactly that.

## Out of scope (do NOT do these)

- Any group other than `Persistent/Home` (Shop/Gacha, Other, etc. are their own later batches).
- The `BLOCKED_IN_FLIGHT` Account texts (owned by `login_signup_screens`).
- Converting the documented misclassifications (player names, currency counts, TODO placeholders, server-driven maintenance body) — report them, don't bind them.
- Inventing Japanese translations beyond the `[JP-TODO]` placeholder.
- Any visual/layout change to `HomeScreen` — attaching a binder must not move or restyle text.
- Touching the archived `Scripts/Editor/Archive/*Builder.cs` (audit flagged some as CODE_DRIVEN — they are editor-time scaffolding, not shipping code).

---
