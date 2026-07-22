# SPEC — `localization_audit_tooling`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state.

## Status

See `STATUS.md`. Current: `SPEC_READY`.

## Goal

Build the **measurement + guard layer** for the game-wide localization sweep. The localization *system* already works (`LocalizationManager`, `LocalizedText`, CSV→table importer, EN/JP), but it is barely applied: **32 `LocalizedText` binder instances exist across the entire project**, against ~1,500 raw TMP text values and 79 hardcoded `.text = "…"` literals in scripts. Before converting anything, we need to know exactly **what is user-facing copy, what is runtime-driven, and what is dead** — and we need a guard so newly-built screens stop shipping unbound.

This task delivers a **read-only audit tool + triage report + a WARN-only lint rule**. It does **not** convert anything. The report's grouped output becomes the direct input for the follow-up batch-conversion tasks (one per screen group), so those can be specced with real counts instead of guesses.

**Why this shape:** a single "localize the whole game" task would touch ~130 prefabs/scenes in one pass — far beyond what the implementer can hold, and precisely the setup that produced the fabricated-provenance failures in `Docs/Reports/POSTMORTEM_general_shop_ui_fabricated_provenance.md`. Measure first, convert in reviewable batches.

## Baseline measurements (reconcile against these)

Taken on the current working tree. The tool's own output **must reconcile with these numbers**, and any deviation must be explained in `IMPLEMENTER_REPORT.md` (a mismatch means either the baseline or the tool is wrong — find out which; do not silently adopt a different number).

| Surface | Measured |
|---|---|
| Prefabs containing TMP text | **96** of 1444 |
| TMP text values in prefabs (non-empty) | **970** |
| `LocalizedText` instances in prefabs | **2** |
| Scenes containing TMP text | **35** of 111 |
| TMP text values in scenes | **548** (ShellScene alone: 343) |
| `LocalizedText` instances in scenes | **30** |
| Hardcoded `.text = "literal"` in C# | **79** across 631 files |
| CSV keys | **227** (EN complete; **2** missing JP) |
| CSV keys referenced by `LocalizationManager.Get("…")` in code | **67** |

**Known caveats — the tool exists to resolve these, not inherit them:**
- Raw TMP counts include placeholder text overwritten at runtime, numeric readouts, and debug labels. ~1,500 is an **upper bound**, not the localization workload.
- The "67 referenced in code" figure **understates** real key usage, because `LocalizedText` stores its key in serialized YAML, not in code. Any "unused key" list must union code references **and** binder keys harvested from prefab/scene YAML before declaring a key orphaned.

## Existing system (use these — do not reinvent)

- `Assets/Localization/LocalizationManager.cs` — `static Get(string key)`, `SetLanguage`, `OnLanguageChanged`. **Global namespace.** Fallback: `Get()` returns **the key itself** when missing → gaps render as literal `SETTINGS_ABOUT_LICENCES` on screen (exploit this for the JP smoke pass).
- `Assets/Localization/LocalizedText.cs` — binder MonoBehaviour, `[RequireComponent(typeof(TextMeshProUGUI))]`, private `[SerializeField] string key`, `SetKey(string)`, `Refresh()`. **Global namespace.** Note it binds `TextMeshProUGUI` (UGUI) only — a `TextMeshPro` (3D) label cannot take this component; report those separately rather than mis-classifying them.
- `Assets/Localization/LocalizationTextImporter.cs` — `[MenuItem("Tools/Localization/Import Text CSV")]`; **`LocalizationText.csv` is the source of truth**, imported into `LocalizationTextTable.asset`. Never hand-edit the `.asset`.
- `Assets/Localization/LocalizationDebugWindow.cs` — `[MenuItem("Tools/Localization/Language Debug")]`, sets the bootstrap default language. Existing menu convention is **`Tools/Localization/*`** — follow it.
- `Assets/Scripts/UI/Editor/LocalizationEditorHelper.cs` — `AddLocalizedText(GameObject, key)`; the sanctioned way editor scripts attach binders. The batch tasks will use it; this task only *reports*.
- `Assets/Editor/UIFidelity/UIFidelityLinter.cs` — `Finding(sev, path, check, detail)`; `RenderHealth(root)` is the universal layer; `Report(...)` writes `Docs/Diagnostics/_capture/<name>_lint.json` with `fail`/`warn` counts, consumed by **Rule 21**.

## Implementation

### 1. `LocalizationAudit` editor tool
New: `Assets/Editor/Localization/LocalizationAudit.cs` (editor-only, `#if UNITY_EDITOR`), `[MenuItem("Tools/Localization/Audit Project")]`.

Scans and classifies **every** TMP text occurrence in prefabs and scenes, plus every string literal assigned to `.text` in C#, and every `Get("…")` key reference.

**Scan scope — hard exclusions (do not report, do not count):**
- `Library/`, `Temp/`, `obj/`, `Build/`
- `Assets/TextMesh Pro/**` (third-party sample scenes)
- `Assets/Scenes/_Recovery/**` (recovery snapshots)
- Any `Assets/Plugins/**`, `Assets/Packages/**`, NuGet output

**Scan scope — flag but DO NOT action:**
- `Assets/Prefabs/Original/**` → classify `CANDIDATE_DEAD`. These include `Original/SplashScene/LoginScreen.prefab` (57 texts) and `Original/SignupScreen.prefab` (22), almost certainly superseded by the new `Prefabs/UI/Account/` screens. **Do not delete, do not localize** — the report flags them for Cesar's dead-asset call. Determine "referenced or not" by searching scenes + prefabs for the asset GUID and report the reference count as evidence.
- `Assets/Prefabs/UI/Account/**` and `Assets/Scripts/Auth/**` → classify `BLOCKED_IN_FLIGHT`. The `login_signup_screens` task owns these files right now; they must be excluded from every actionable batch to avoid merge collisions. Report their counts separately.

**Classification (per text occurrence) — heuristic, and honestly labelled as such:**

| Class | Meaning |
|---|---|
| `BOUND` | already has a `LocalizedText` component with a non-empty key |
| `STATIC_COPY` | user-facing prose; ≥1 alphabetic word, not a known dynamic pattern → **needs a binder** |
| `CODE_DRIVEN` | the label is assigned from C# (its GO/field is referenced by a controller that writes `.text`) → needs `Get()` at the **code site**, not a binder |
| `DYNAMIC_PLACEHOLDER` | design-time filler overwritten at runtime: pure numbers, `0`, `100`, `New Text`, `Text`, `Lorem…`, `Sample`, single glyphs, time/score patterns |
| `NON_UGUI` | `TextMeshPro` (3D) component — binder not applicable |
| `CANDIDATE_DEAD` / `BLOCKED_IN_FLIGHT` | as above |

`CODE_DRIVEN` detection is a **best-effort cross-reference**, not full static analysis: for each script that assigns `.text`, resolve the serialized field name and match it against the prefab/scene YAML field references where feasible; where it cannot be resolved, emit `UNKNOWN` rather than guessing. **The report is a triage document for human review — it must never claim certainty it does not have.** Every heuristic used goes in the summary's "Method + limitations" section.

**Screen-group bucketing.** Every row gets a `Group` (Persistent/Home · Roster · Inventory/Bag · Shop/Gacha · Hole/Results · Settings · Rankings/Tournaments · Account · Other) derived from asset path. This is what makes the follow-up batch tasks speccable.

**Suggested key.** For `STATIC_COPY`, propose a key following the existing convention (`GROUP_SCREEN_ELEMENT`, matching prefixes already in use: `HOME_`, `ROSTER_`, `SETTINGS_`, `BAG_`, `CLUB_`, `MODAL_`, …). If an existing CSV key already has identical English text, **propose that key for reuse instead of a new one** and mark `REUSE_EXISTING` — deduplication is a primary output.

### 2. Outputs
- `Docs/Reports/localization_audit_<YYYY-MM-DD>.csv` — one row per occurrence: `AssetPath, GameObjectPath, TextValue, Class, Group, HasBinder, ExistingKey, SuggestedKey, ReuseOf, Notes`.
- `Docs/Reports/localization_audit_<YYYY-MM-DD>.md` — summary: totals per class, **per-group counts + estimated batch size** (the batch plan), the orphaned-key list (code refs ∪ binder keys), keys used but missing from CSV, the 2 missing-JP rows, `CANDIDATE_DEAD` inventory with reference counts, and a **"Method + limitations"** section stating every heuristic.

### 3. Lint rule — WARN-only, and it must stay that way
Add a `LocalizationHealth(GameObject root)` layer to `UIFidelityLinter` flagging `TextMeshProUGUI` with non-empty literal text and no `LocalizedText`, as **`WARN` severity, check name `unlocalized-text`**.

> **Hard constraint:** this rule must **not** change the `fail` count of any existing prefab. 968 prefab texts are currently unbound; a `FAIL` here would instantly red-gate Rule 21 for every in-flight task including `login_signup_screens`. `WARN` only. Do not add it to any hard gate in this task.

Verify explicitly: run `LintPrefab` on **two** existing prefabs before and after the change and show `fail` is **identical**, with `warn` increased.

### 4. Free fix
Fill the two missing Japanese values in `LocalizationText.csv` — `SETTINGS_ABOUT_APP_VERSION` (`APP VERSION`) and `SETTINGS_ABOUT_LICENCES` (`LICENCES`) — then run `Tools/Localization/Import Text CSV` so the `.asset` matches. These are the only two EN-complete/JP-missing rows.

## Acceptance checklist (Implementer fills `IMPLEMENTER_REPORT.md`)

- [ ] `Tools/Localization/Audit Project` runs clean and writes both report files
- [ ] Tool output **reconciles with the baseline table** (970 / 2 / 548 / 30 / 79 / 227 / 67); every deviation explained, not silently adopted
- [ ] Exclusions honoured — zero rows from `TextMesh Pro/**`, `_Recovery/**`, `Library/**`
- [ ] `Prefabs/Original/**` rows classified `CANDIDATE_DEAD` **with GUID reference counts as evidence**; nothing deleted
- [ ] `Prefabs/UI/Account/**` + `Scripts/Auth/**` classified `BLOCKED_IN_FLIGHT` and excluded from the batch plan
- [ ] Orphaned-key list computed from code refs **∪ binder keys harvested from YAML** (not code alone)
- [ ] Per-group batch plan present in the `.md`, with counts — usable as direct input to the follow-up conversion specs
- [ ] "Method + limitations" section present, naming every heuristic and every `UNKNOWN` bucket
- [ ] `unlocalized-text` lint rule is **WARN**; before/after `LintPrefab` on 2 existing prefabs shows **identical `fail`**, higher `warn` — both `_lint.json` files cited
- [ ] 2 missing JP values filled; CSV re-imported; `LocalizationTextTable.asset` row count still 227
- [ ] **Zero mutations to prefabs/scenes:** `git status --porcelain` shows **no modified `.prefab` or `.unity` files**. This is an audit — quote the command output in the report
- [ ] Unity Console has no errors related to this task
- [ ] Spec deviations (if any) flagged at the bottom of the report

## Files / hierarchy this task touches

- `Assets/Editor/Localization/LocalizationAudit.cs` — **new** editor audit tool
- `Assets/Editor/UIFidelity/UIFidelityLinter.cs` — **+** `LocalizationHealth` layer (WARN-only)
- `Assets/Localization/LocalizationText.csv` — 2 JP values
- `Assets/Localization/LocalizationTextTable.asset` — regenerated via importer
- `Docs/Reports/localization_audit_<date>.{csv,md}` — **new** report artifacts

## Smoke evidence

Run the audit from the menu; paste the console summary and the head of both reports into `IMPLEMENTER_REPORT.md`. Show the before/after `LintPrefab` output for the two probe prefabs. Show `git status --porcelain` proving no prefab/scene mutations. Confirm the CSV re-import logs 227 rows.

Optional but valuable: launch in Japanese via `Tools/Localization/Language Debug` and screenshot one already-bound screen, confirming JP renders and that any raw-key leakage is visible — this validates the smoke method the batch tasks will use.

## Out of scope (do NOT do these)

- **Attaching binders / converting anything.** No prefab or scene edits. That is the follow-up batch tasks.
- Replacing the 79 hardcoded `.text = "…"` literals with `Get()` — report them, don't fix them
- Deleting or modifying anything under `Prefabs/Original/**` — report only; the dead-asset call is Cesar's
- Touching `Prefabs/UI/Account/**` or `Scripts/Auth/**` — owned by `login_signup_screens`
- Promoting the lint rule to `FAIL`, or wiring it into Rule 21 / any hard gate
- Adding new languages beyond EN/JP, or reworking `LocalizationManager` / `LocalizedText` APIs
- Translating anything into Japanese beyond the 2 named rows — bulk JP copy is a later pass with a human translator

---
