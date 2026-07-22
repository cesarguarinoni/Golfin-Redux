# SPEC — `localize_hole_results`

> **Authoritative spec.** Implementer reads this and ONLY this. STATUS.md tracks pipeline state.

## Status

See `STATUS.md`. Current: `SPEC_READY`.

## Goal

**Batch 3 of the localization sweep** (batch 1 = Persistent/Home pilot; batch 2 = Inventory/Bag; both DONE). Convert the genuinely-static user-facing labels in the **`Hole/Results`** group — the hole-complete widget, matchmaking modal, versus-result screen, hole-select / mode cards — to the localization system, applying the **code-path-first recipe**. Audit group = 114 rows; after triage the real actionable set is ~20 distinct static labels (the rest are dynamic names/ranks/counts, per-hole data, placeholders, and one editor builder).

## Recipe (from batches 1–2 — apply exactly)

1. **Code-path-first.** A label a controller assigns at runtime (`.text = …`) is localized at the **code site** via `LocalizationManager.Get("KEY")`, NOT a binder. A static prefab label nothing overwrites gets a `LocalizedText` binder (via `LocalizationEditorHelper.AddLocalizedText`).
2. **Verify the live surface before binding (finding #1 — CRITICAL for this batch).** Several of these prefabs may be shown as scene GOs or composited (e.g. the versus-result / matchmaking flow composites over ModeSelection per prior notes) rather than as live prefab instances. **For each prefab you bind, confirm the on-screen text is a real instance of that prefab** (cite the controller `Instantiate`/`Show` site). If the live surface is a disconnected scene GO, bind the scene GO in its scene OR convert the controller code site — and document which. Do NOT assume a binder takes effect.
3. **Never bind a runtime-overwritten label.** Player names, ranks, course/hole/par strings, stroke readouts, per-hole tee-shot descriptions, currency counts — all set at runtime. SKIP.
4. **Editor/Archive builders are not shipping code.** SKIP `Assets/Scripts/Editor/VersusResultScreenBuilder.cs` entirely.
5. **Reuse/dedup first.** Many labels repeat across prefabs and across SUCCESS/FAILED panel states — one key, reused everywhere. Reuse existing CSV keys where English matches.
6. **Preserve displayed English exactly.** If a source string has a typo (`DIAMOND LEAGE`), keep the EN value byte-identical to what ships today and FLAG the typo in the report — do NOT silently "fix" displayed copy (that's an unrequested content change).

## Triage

### CONVERT — static labels (bind on prefab, or Get() at code site per recipe rule 1–2). Reuse existing keys:

| Label | Key | Source |
|---|---|---|
| `PLAY` | `BTN_START` (exists) | HoleCompleteWidget, HoleCard, ModeCard, ModeHomeCard, HoleCardController |
| `LOCKED` | `BAG_LOCKED` (exists) | HoleCompleteWidget |
| `USERNAME` | `HOME_USERNAME` (exists) | MatchMakingModal, VersusResultScreen |
| `CANCEL` | `MODAL_CANCEL` (exists) | MatchMakingModal |

### CONVERT — static labels needing NEW keys (verify static first; dedup — one key per distinct English, reused across all occurrences/states). Suggested keys (follow `GROUP_ELEMENT` convention; reuse if an identical-English key already exists):

| English | Suggested key | Appears on |
|---|---|---|
| `SUCCESS` | `RESULT_SUCCESS` | HoleCompleteWidget (both states) |
| `FAILED` | `RESULT_FAILED` | HoleCompleteWidget |
| `NEXT` / `Next` | `RESULT_NEXT` | HoleCompleteWidget, HoleCard, ModeCard (normalize case in the KEY; keep each label's shown casing — if two labels differ only by case and both are static, bind both to the same key ONLY if they should display identically, else keep separate; document the call) |
| `REPLAY` | `RESULT_REPLAY` | HoleCompleteWidget, HoleCardController |
| `RETRY` | `RESULT_RETRY` | HoleCompleteWidget |
| `RESULTS` | `RESULT_RESULTS` | VersusResultScreen |
| `WINNER` | `RESULT_WINNER` | VersusResultScreen |
| `LOSER` | `RESULT_LOSER` | VersusResultScreen |
| `NEW MATCH` | `RESULT_NEW_MATCH` | VersusResultScreen |
| `Vs.` | `MATCH_VS` | MatchMakingModal, VersusResultScreen |
| `HOLE` | `MATCH_HOLE` | MatchMakingModal, VersusResultScreen |
| `FINDING OPPONENT...` | `MATCH_FINDING_OPPONENT` | MatchMakingModal |
| `DIAMOND LEAGE` (typo — keep EN as-is, flag) | `MATCH_DIAMOND_LEAGUE` | MatchMakingModal |
| `ENTRY FEE` | `MODE_ENTRY_FEE` | ModeCard, ModeHomeCard |
| `REWARDS` | `MODE_REWARDS` | ModeCard, ModeHomeCard |
| `PRACTICE` | `MODE_PRACTICE` | ModeHomeCard |

### CONVERT — genuine runtime code strings → `Get()` (NOT editor builders; verify each is the live control)

- `Assets/Scripts/UI/HoleSelection/HoleCardController.cs` — `REPLAY` → `Get("RESULT_REPLAY")`, `PLAY` → `Get("BTN_START")` (verify these are the runtime-set card button labels).
- `Assets/Scripts/UI/Matchmaking/MatchmakingModalController.cs` and `VersusResultScreenController.cs` — `"You"` → `Get("MATCH_YOU")` (new key) IF it's a static "you" label; if it's a default overwritten by the player's name, SKIP and document.

### DO NOT CONVERT — document each in `## Triage findings`, touch nothing:

- **Dynamic / data-driven:** player/opponent names (`SHAE`), ranks (`RANK: #255`, `RANK: <color=…>#142</color>` — the controller composes these; the "RANK:" prefix localization is a deferred code-site follow-up, note it), course/hole/par strings (`Lomond Country Club - Hole N - Par M`, `LOMOND 28/72`, `YAITA - KIKYOU`, `LADIES 18/18`, `FRONT/REGULAR/BACK …/18`), tee-off/strokes readouts (`TEE OFF: REGULAR\nSTROKES: …`), per-hole tee-shot descriptions (`The tee shot is best aimed …`).
- **Placeholders:** `Tagline`, `Description placeholder`.
- **Counts:** `x10`, `x04`, `x02`, `x50`, `x100`, `x200`.
- **Editor builder:** all of `VersusResultScreenBuilder.cs`.
- Note the `HoleSelectionScreenController.cs` course/tee labels (`LOMOND 28/72`, etc.) are compound name+live-progress strings — SKIP as dynamic (the name portion localization is a later structured-string task); document.

Follow the evidence: if a "CONVERT" label is actually runtime-set, or a "skip" is a genuine static label, flip it and document.

## JP policy (unchanged)

Reused keys keep existing JP. New keys: EN = the exact displayed string, JP = EN + ` [JP-TODO]`. No invented Japanese. JP renders via the Noto TMP fallback (`4846d78d3`).

## Deferred (do NOT attempt this batch)

- Any string in explicit-asmdef gameplay code that can't reach `LocalizationManager` (the asmdef-access question — see batch 2's deferral). If a target requires an asmdef change, DEFER it and document, do NOT restructure assemblies.
- Structured/composed dynamic strings (rank prefix, course-name+progress) — a later dedicated pass.

## Acceptance checklist (Implementer fills `IMPLEMENTER_REPORT.md`)

- [ ] **Triage findings:** every audit row (all 114) verdicted — CONVERTED (how) / SKIPPED (bucket) / DEFERRED (reason). Primary deliverable.
- [ ] **Live-surface proof (finding #1):** for each bound prefab, cite the controller Instantiate/Show site proving the live UI uses that prefab instance (or document the scene-GO/code-site alternative you used).
- [ ] **Binders / code edits:** each converted static label carries a `LocalizedText` with correct key (read-back quoted) OR the code site now calls `Get()` (diff shown). No binder on a label the controller also writes.
- [ ] **Dedup:** repeated labels share one key; reused keys confirmed pre-existing; NEW keys are genuinely new (no duplicate minting). Report new key count.
- [ ] **CSV:** new keys added (EN exact + `[JP-TODO]`); importer re-run; key count reported; no duplicate key; typo(s) flagged.
- [ ] **EN unchanged:** capture the hole-complete widget, matchmaking modal, versus-result screen, and a mode/hole card in EN at 1170×2532 via the real flow; labels identical to before. Cite screenshots. (Multi-state: capture SUCCESS and FAILED variants of HoleCompleteWidget if reachable.)
- [ ] **JP smoke:** JP mode — reused labels render real JP, new labels `[JP-TODO]`, NO raw key on screen. Cite screenshots.
- [ ] **Scope:** `git status --porcelain` shows only the touched Hole/Results prefabs + the touched runtime controllers (HoleCardController, MatchmakingModalController/VersusResultScreenController if converted) + CSV + table (+ task folder). NO editor builder edits, NO scene mutation, NO `Assets/Scripts/Physics/`, NO asmdef change. Quote it (pre-existing drift is not this task's).
- [ ] Compiles clean; no task-related console errors.
- [ ] Spec deviations flagged.

## Not a Figma task

No Figma node — Rules 16/17/18/21 N/A. Visual gate: EN labels unchanged; JP renders translated/placeholder, never a raw key; no layout shift.

## Out of scope

Any other group; runtime/dynamic strings; editor builders; placeholders/counts; inventing Japanese; asmdef changes; visual/layout changes; scenes; `Assets/Scripts/Physics/`; `M_Splash*.mat`.

---
