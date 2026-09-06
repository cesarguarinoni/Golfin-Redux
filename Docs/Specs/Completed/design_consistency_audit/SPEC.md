# SPEC — `design_consistency_audit`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files.
>
> Notion roadmap row **2112** (`design_consistency_audit`). Track: GAME polish (Architect: the game-polish Cowork session). Runs BEFORE `game_polish` (2111) — Cesar's call, 2026-09-03: *"so we don't animate inconsistencies"*.

## Status

See `STATUS.md`. Standard pipeline states (`SPEC_READY → IMPLEMENTER_WORKING → READY_FOR_SELF_REVIEW → … → ARCHITECT_REVIEW_PASS → DONE`).

## Goal

**An audit, not a build.** Measure every player-facing GAME screen against the Figma design tokens and the project's own render-health rules, and hand back (1) a findings report, (2) a per-screen fix list grouped by *shape* (PIPELINE_HARDENING §22), and (3) the machine evidence behind every row. **This task changes no production code, no prefab, no scene, no CSV.** Every fix it proposes becomes its own Quick spec (`Docs/Specs/Quick/`) that Cesar approves individually — the Architect writes those from the fix list; the implementer of THIS task does not fix anything.

The six dimensions Cesar named, and what "consistent" means for each:

| # | Dimension | Consistent means |
|---|---|---|
| F | **Fonts** | family is Rubik (EN) / Noto Sans JP (JA) — never `LiberationSans SDF`; weight matches the node (SemiBold / Medium / Regular); **rendered** size matches the node within ±1.5 px (rendered = serialized `fontSize` × `lossyScale` on the 1170×2532 shell canvas — many cards live under a scaled parent, so the serialized number alone proves nothing) |
| C | **Colours** | text colour and panel/pill fills match the node / the token sheet within Σ\|ΔRGB\| ≤ 0.12 (the linter's own `color` tolerance; ≈ 10 per channel); no sprite tinted to fake a fill the node draws as a gradient (`UI_ELEMENT_PALETTE.md` "bake, never tint") |
| H | **Hierarchy** | the same text ROLE (screen title, card title, body, caption, button label, price) uses the same type-scale step on every screen — the Figma type scale is the ruler, `Docs/Design/DESIGN_TOKENS.md` (Phase 0 below) is where it is written down |
| S | **Sizes** | buttons, pills, cards, gaps and paddings match the node (±2 px, the linter's `width`/`height` tolerance); shared atoms (`Main Buttons`, pills, card frames) are the same size everywhere the node says they are |
| O | **Outlines / borders** | a border the node draws exists, at the node's width and colour, built as a two-layer rim or a 9-sliced rim sprite — never a `UnityEngine.UI.Outline` component (trap C5, linter `outline-border`), never a flat box where the node shows a rim (linter `flat-fill` / `require-sprite`) |
| D | **Drop shadows / effects** | a shadow the node draws exists (baked into the sprite margin, the way `Next Hole Panel.png` carries its own — see the palette's "draws INSIDE its RectTransform" note) and a shadow the node does NOT draw is absent; `UnityEngine.UI.Shadow` components are listed (they blur, they do not match a Figma drop shadow) |

Plus layer R — the **render-health rules** already in `Golfin.EditorTools.UIFidelity.UIFidelityLinter.RenderHealth` (`Assets/Editor/UIFidelity/UIFidelityLinter.cs`): `flat-fill`, `default-sprite`, `9slice-collapse-x/y`, `nonuniform-stretch`, `outline-border`, `tmp-default-sizedelta`, `9slice-cap-kink`, `tiny-text`, and `LocalizationHealth`'s `unlocalized-text`. These run on every screen in scope as-is. **Do not weaken, re-tune or add rules to the linter in this task** — a rule change is a separate spec (`Rule 21` gates every other task on this file).

## What the Architect already measured (baseline — the audit reproduces or corrects it)

Counted from the serialized YAML at HEAD `85b2365fb` (`ShellScene.unity` + `Assets/Prefabs/UI/**`), so these are SERIALIZED numbers, not rendered ones:

- **Font assets in use:** `Rubik-SemiBold SDF` (`39fb7824…`) ×1271 sites, `Rubik-VariableFont_wght SDF` (`0e84913c…`) ×525, **`LiberationSans SDF` (`8f586378…`) ×77** across scenes+prefabs, `NotoSansJP-VariableFont_wght SDF` (`8f62f163…`) ×6 (serialized default; JA is normally swapped at runtime by `LocalizedText` — the audit must say which).
- **LiberationSans sites in scope:** ShellScene `InventoryScreen` 27, `RosterScreen` 8, `SettingsScreen` 1; prefabs `Roster/CharacterThumbnailCard` 3 (×N instances), `Roster/StatBar` 4. That is one SHAPE ("Unity's default font never replaced") — audit it as one row-set, not 46 findings.
- **Off-scale serialized sizes** (Figma type scale is 30 / 33 / 39 / 45 / 51 / 66 … — see Phase 0): `19.85`, `23`, `23.1`, `24`, `25`, `27.8`, `27.86`, `32.14`, `34.6`, `38.1`, `42.5`, `46.2`, `49.05`, `50.8` appear across Inventory, Roster, ModeCard, GeneralShopScreen, TournamentLeaderboard, InGameSettingsModal, TournamentSignupModal, HoleSelection/Mission cards. Two known legitimate sources exist and must be separated from real defects: (a) the **÷1.2 divisor era** (`Docs/Specs/Completed/FIGMA_UNITY_SIZE_MISMATCH.md`, `CANVAS_SCALER_FIX_PLAN.md`) and (b) the **`59/66` SemiBold calibration** (`FIGMA_SCREEN_BUILD_PLAYBOOK.md` §4: a Rubik SemiBold run authored at the node's nominal px renders 10–12 % oversize, so `node_px × 59/66` is CORRECT — e.g. serialized 59 for a 66 node). A serialized value is therefore neither right nor wrong until it is RENDERED and compared to the node render.
- Screens whose root is a scaled/nested prefab (`GeneralShopScreen` at 15.4/23.1/34.6/46.2, `ModeCard` at 27.86/32.14, `TournamentSelectionCard` = `GeneralShopCard` sizes) are exactly where serialized ≠ rendered.

## Reference

- **Figma file:** `5gEAHjl6xAtW8iYY7NMvWd`. Canvas is **1170×2532 at scale 1 — a Figma px IS a Unity px** (playbook §2). `get_variable_defs` on any frame returns the typography + colour variables (Phase 0 uses this).
- **Node table** (Architect-resolved 2026-09-03; where only a PAGE id is given, resolve the frame with `get_metadata` on that page and record the frame id in the report — Rule 9 re-pull applies to every row anyway):

| Screen (Unity object / prefab) | Figma page → frame | Node |
|---|---|---|
| `HomeScreen` (ShellScene + `Assets/Prefabs/UI/HomeScreen.prefab`) | Home Screen → `New - UK Female` (+notice variant) | `13994:1935` (`2098:8490` with notice) |
| `ModeSelectionScreen` + `ModeSelect/ModeCard`, `ModeHomeCard` | (mode_select_system) | `13027:10222` |
| `HoleSelectionScreen` + `HoleSelection/HoleCard` | (hole_selection_screen) | `12885:87551`, cards `12961:1694…1728` |
| `MissionSelectionScreen` + `MissionSelection/MissionCard` | (missions_v1) | `4002:6036`, cards `4003:4412…5297` |
| `TournamentSelectionScreen` + `TournamentSelectionCard` | (tournament_selection_screen) | `13386:1758` |
| `TournamentHoleSelectionScreen` + `TournamentHoleCard_*` | (tournament_screens) | `13414:2936` / `13414:2972` |
| `TournamentLeaderboardScreen` + `TournamentRankingRow`, `TournamentPlayerStickyRow` | (tournament_screens) | `13414:4041` |
| `RankingsScreen` (Leaderboard) + `Rankings/*Card*` | Rankings → `Rankings` | `4079:1726` |
| `RosterScreen` + `Roster/CharacterThumbnailCard`, `StatBar`, `PaginationDot` | Characters Screen → `Roster Screen` | `4065:14998` (Compare `4300:63876`) |
| `InventoryScreen` — Bags / Clubs / Balls / Items tabs + `Inventory/*Card*` | Clubs Screen → `Clubs Screen` `4065:9071`; Bags page `2563:18880` (`12754:40669`); Balls page `2636:1972`; Items page `4063:393` | per tab |
| `GeneralShopScreen` + `GeneralShopCard` | (general_shop_ui) | `4079:28230`, card `13509:2978` |
| `StaminaShopSelectionScreen`, `StaminaShopDetailScreen` + `StaminaMenuRow`, pills, cards | (stamina_boost_shop) | `13156:1178`, `13330:1139` |
| `GachaHistoryScreen` + `GachaHistoryRow*` | (gacha_history) | `13622:21105` |
| `GachaPrizesScreen` | (gacha_prizes) | `13622:2222` |
| `SettingsScreen` overlay + submenus (`SoundSettingsSubmenu`, language, about…) | Settings → `Settings Screen` | `4065:16939` (Sounds `4065:16941`, Language `4065:16942`, User Profile `4065:16940`, About `4065:16946`) |
| `PersistentUI` — Top UI + `Nav Bar Container` | Home Screen → `Top UI` / `Nav Bar Container` | `2098:8493` / `2098:7988` |
| Result modals: `HoleCompleteModal`, `TournamentResultModal`, `VersusResultScreen`/`VersusResultModal`, `GachaRevealModal`, `LevelUpModal` (Roster), `ClubLevelUpModal` (Inventory) | Pop-Ups page `4079:28746`; tournament result `13498:2067`; 1v1 result `13274:877`; Character Level Up page `4059:5509`; Club Level Up page `4056:1542` | per modal |
| Other modals: `TournamentSignupModal` `13480:2479`, `MatchMakingModal` `12813:77056`, `GachaRatesModal`, `InGameSettingsModal` `13873:33610`, `StartingCharacterConfirmModal` `13924:41976`, `Toast` | — | per modal |
| Tier 2 (inventory + lint only, no crop sheet): `LoginScreen`, `SignUpScreen`, `CreateUsernameScreen`, `EmailConfirmationScreen`, `ResetPasswordScreen`, `LoadingScreen`, `SplashScreen` | Splash page `2032:327`, Loading page `4096:1181`, login `4062:4971` | — |

- **NOT in scope** (owned elsewhere — do not lint, do not list): every `Gps*` screen and modal (`Assets/Prefabs/UI/Gps/**`, GPS track), the in-game HUD / shot UI (`Docs/POLISH_BACKLOG.md` row), the admin dashboard.
- **Node renders in `reference/`:** the Architect did NOT pre-pull them — for an audit of ~25 surfaces the pull is the audit's own Phase 0 (A0 below), and each reviewer checks the folder is complete before reading a single finding. One PNG per node in the table, named `<UnityRoot>__<nodeId>.png`, via `mcp__figma__get_screenshot` (long edge ≥ 1024).
- **Build playbook:** `Docs/Architecture/FIGMA_SCREEN_BUILD_PLAYBOOK.md` §0 (capture through REAL navigation; match the backdrop before trusting ΔRGB; sample at 1:1), §4 (weight and rendered size, the 59/66 rule), §7 (crop sheets). **Atom catalogue:** `Docs/Architecture/UI_ELEMENT_PALETTE.md` — the "expected" side of every sprite/rim/fill row cites an atom from it or says "no atom exists".
- **Tooling that exists:** `UIFidelityLinter.LintPrefab(prefabPath, specJson)` → `Docs/Diagnostics/_capture/<prefab>_lint.json`; `Docs/Scripts/figma_node_to_spec.py <metadata.xml> <context.jsx> --name-map map.json -o spec.json` (node-spec layer for the linter); `Docs/Scripts/figma_diff.py built ref out_prefix` (grid diff + side-by-side); `Assets/Scripts/UI/Editor/*DemoRecorder.cs` (real-navigation drivers per screen — reuse their `Press()`/navigation, do not build a render harness); `Tools/unity-mcp-call.py`.

## Design — the method

### Phase 0 · Instruments and tokens (before any measurement)

0.1 **Token sheet.** Write `Docs/Design/DESIGN_TOKENS.md` (the Architect seeded it with the variables read on 2026-09-03 — complete it, do not restart it): every `EN/*` and `JP/*` typography variable (family, style, size, line height, tracking), every colour variable (`Main (Game)/*`, `Text Colors/*`, `Greys/*`, `Rarity Fonts/*`, `Rarity Backgrounds/*`, `Gold`/`Silver`/`Blue`/`Copper` — several come back EMPTY from `get_variable_defs`; for those read the SVG of one instance, `download_assets(nodeId, "svg")`, and record the gradient stops — the palette doc explains why the CSS lies), and the shared atom sizes (`Main Buttons` heights, pill radii, card radii r50/r32, the 3 px white card border). Source column per row = the node id it was read from. This sheet is the "expected" column of every F/C/H/S row below.

0.2 **`DesignAuditDumper`** — new Editor-only tool, `Assets/Editor/UIFidelity/DesignAuditDumper.cs` (namespace `Golfin.EditorTools.UIFidelity`, beside the linter; **reads only**). In PLAY mode, after REAL navigation to a screen (playbook §0 — reuse the DemoRecorders' drivers; `HomeScreenController` → nav-bar `onClick.Invoke()` → …), it walks the active screen root (`ScreenManager`'s current screen object, plus `PersistentUI` once, plus each modal opened through its real trigger) and writes `Docs/Diagnostics/_capture/design_audit/<Screen>.json` with, per `TextMeshProUGUI`: path, `font.name`, `fontStyle`, serialized `fontSize`, **rendered px** = `fontSize × rectTransform.lossyScale.y ÷ canvas.scaleFactor` (state the formula in the JSON header), `color` hex, `text` (first 40 chars) + `LocalizedText` key if bound, `outlineWidth`/`outlineColor` (TMP material), `UnityEngine.UI.Shadow`/`Outline` siblings; per `Image`: path, sprite name + GUID, `type`, `pixelsPerUnitMultiplier`, `color` hex, rendered `rect.size`, sprite border, `preserveAspect`, `Shadow`/`Outline` siblings, `raycastTarget`; per `Button`: path, rendered size, `ButtonPressFeedback` present; plus `counts` (tmp / image / button / liberationSans / outlineComponents / shadowComponents). Inactive children ARE included with `active:false` (tabs, hidden states) — the Inventory tabs and Settings submenus are driven through their real toggles too, so each tab/submenu gets its own JSON.

0.3 **Tripwire (PIPELINE_HARDENING §20).** Before the first real dump, prove the dumper can see a defect: in play mode, on a TEMPORARY runtime instance only (never a prefab/scene save — `git status` must stay clean), swap one label to `LiberationSans SDF`, set one `Image.sprite = null`, add one `Outline` component; dump; show the three rows flagged; stop play mode; dump again; show them gone. Quote both JSON excerpts in the report.

0.4 **Node specs for the linter.** For every row of the node table with a frame id: `get_metadata` + `get_design_context` → `figma_node_to_spec.py … --name-map` → `Docs/Specs/Active/design_consistency_audit/reference/specs/<Root>_spec.json`. The name map maps ONLY to GO names that exist in the built object (the script's own IMPORTANT note) — an unmapped node is listed in the report as "no Unity counterpart", which is itself a finding when the node draws something the screen lacks.

### Phase 1 · Measure (per screen, in the table's order)

1.1 **Lint.** `UIFidelityLinter.LintPrefab(prefab, spec.json)` for every prefab in scope (screens, cards, rows, modals — enumerate the list in the report; `Assets/Prefabs/UI/**` minus `Gps/`). For the eight ShellScene-hosted screens (`HomeScreen` instance, `RosterScreen`, `InventoryScreen`, `HoleSelectionScreen`, `MissionSelectionScreen`, `ModeSelectionScreen`, `TournamentHoleSelectionScreen`, `TournamentLeaderboardScreen`, `ResetPasswordScreen`) the linter has no prefab path: add a **`LintRoot(GameObject root, string reportName, string specJsonPath)`** overload that runs the SAME three layers on a live root and writes the same JSON shape — a pure refactor of `LintPrefab`'s body (instantiate → lint → report) into (lint → report); `LintPrefab`'s behaviour and output are byte-identical before/after (A9 pins it on one existing prefab's JSON).

1.2 **Dump.** `DesignAuditDumper` per screen / tab / submenu / modal, via real navigation, EN locale first, then JA (the JA pass is what shows whether `LocalizedText` swaps the font asset and whether JA sizes follow `JP/*` tokens — record `font.name` per locale).

1.3 **Crop sheets (Rule 10 / playbook §7).** For every Tier-1 row: live capture (≥ 900 px long edge, play mode, real navigation, wait ≥ 3 s / 5 s with data) beside the node render, `figma_diff.py` grid + a per-element crop strip for the elements the fix list will cite. One sheet per screen in `screenshots/<Screen>_sheet.png`. **The residual ΔRGB is named**: data differences (the node mocks a populated state, our build shows the dev account) are not fidelity defects and are said so.

### Phase 2 · Classify (the report)

Every finding gets an id `DA-<Screen>-<n>`, a dimension (F/C/H/S/O/D/R), a **site** (GO path + prefab/scene), **measured** (from the JSON or lint), **expected** (token sheet row or node id + value), a severity, and a **shape** tag:

- **S1 — a player sees it**: wrong family (LiberationSans), rendered size off by > 10 %, colour Σ\|ΔRGB\| > 0.12, missing/extra border or shadow the node draws/omits, a flat box where the node has a sprite/gradient, oval pill (9-slice collapse), distorted corner.
- **S2 — inconsistent across screens**: same role, different step on the type scale; same atom, different size/radius; mixed weights in one list; a rim at 2 px here and 3 px there.
- **S3 — hygiene**: `tmp-default-sizedelta`, `tiny-text` on hidden/debug labels, `unlocalized-text` on a dynamic readout, `Outline` on an invisible object.

**Shape audit is mandatory (§22).** When two findings rhyme, name the shape as a mechanically checkable question ("is this TMP's font asset LiberationSans?", "is this serialized size a ÷1.2 leftover — i.e. `node_px / 1.2` within 0.1?", "is this a `Filled` image on a 9-sliced bar sprite?"), grep/dump EVERY candidate site in scope, and publish the per-site verdict table INCLUDING the sites that are fine. Expected shapes (confirm or refute each with a table): (i) default font never replaced; (ii) ÷1.2-era sizes; (iii) SemiBold authored at nominal instead of 59/66 (or the reverse — Cesar decides which convention the game keeps; the report shows rendered cap-height for both populations side by side, from the crop sheets, and recommends); (iv) `UnityEngine.UI.Outline` used as a border; (v) `UnityEngine.UI.Shadow` used as a drop shadow; (vi) tinted `S_PillStadium`/`Next Hole Panel` where the node has a gradient; (vii) `Image.Type.Filled` on 9-sliced bars (`StatBar`, stamina bars, tournament progress); (viii) null-sprite `Image` panels; (ix) card radius r50 vs r32 mismatched to card size class.

### Phase 3 · The fix list (the deliverable Cesar approves from)

`Docs/Reports/DESIGN_CONSISTENCY_AUDIT.md` — sections: 1 Summary (counts per screen × dimension × severity, one table); 2 Token sheet deltas (what the game uses that the sheet doesn't define, and vice versa); 3 Shape tables (Phase 2, one per shape); 4 Per-screen findings (table per screen, `DA-` ids, one crop reference each); 5 **Fix list**, grouped into candidate Quick specs by shape/screen, each group with: the `DA-` ids it closes, the files it would touch (prefab / scene object / builder script / baker), the one-line fix op ("set font asset to `Rubik-VariableFont_wght SDF` on 27 Inventory sites via `SerializedObject`", "rebuild `S_x` via `make_x.py` with the 3-stop gold stroke"), an estimate (XS/S/M), and a blast-radius note (other screens sharing the prefab). **The Architect turns approved groups into `Docs/Specs/Quick/<slug>.md`; this task ends at the list.** 6 Things the audit could not measure and why (the honest gaps — e.g. an effect only visible on device).

## Localization

No player-facing strings are added or changed. `unlocalized-text` WARNs are reported, not fixed. Importer PLAN is not needed; state `no string change` in the report and quote `git status -- Assets/Localization` empty.

## Architecture context

- **New (Editor-only, `Assets/Editor/UIFidelity/`):** `DesignAuditDumper.cs` (+ `.meta`), the `LintRoot` overload in `UIFidelityLinter.cs`. Optional: a tiny `DesignAuditRunner` play-mode driver (like `GpsPolishProbe --mode …`) that chains navigation → dump → capture per screen so the whole pass is one command and re-runnable; if built, it lives in `Assets/Editor/UIFidelity/` too.
- **New (docs):** `Docs/Design/DESIGN_TOKENS.md` (complete the seed), `Docs/Reports/DESIGN_CONSISTENCY_AUDIT.md`, `Docs/Diagnostics/_capture/design_audit/*.json`, `Docs/Specs/Active/design_consistency_audit/{reference,screenshots}/…`.
- **Read, never written:** `Assets/Prefabs/UI/**` (except `Gps/`, not even read), `Assets/Scenes/ShellScene.unity`, `Assets/Scripts/**`, `Assets/Localization/**`, `Assets/Fonts/**`.
- EditMode: `LintRootParityTests` — `LintPrefab` vs `LintRoot` on the same instantiated prefab produce identical findings (A9); `DesignAuditDumperTests` — rendered-px formula pinned on a nested scaled rect (0.5 parent scale → half), `LiberationSans` detection, `Outline`/`Shadow` sibling detection.
- Runs on the Mac Editor; nothing needs a device. Nothing needs the server. No asmdef change.

## Acceptance checklist (Implementer fills in `IMPLEMENTER_REPORT.md`)

Each item is `PASS`/`FAIL` with the measurement quoted (Rule 6 — unexplained PASS = FAIL). Reviewers re-run the whole list (Rule 5) and check the SHAPE tables for completeness rather than rediscovering instances (§22 rule 5).

- [ ] **A0 · Reference renders.** One node render per Tier-1 row of the node table in `reference/`, named `<UnityRoot>__<nodeId>.png`, long edge ≥ 1024; frame ids resolved for every page-only row and listed. `ls reference/` quoted.
- [ ] **A1 · Token sheet complete.** `Docs/Design/DESIGN_TOKENS.md` has every typography variable (EN + JP) and every colour variable with a source node; EMPTY variables resolved from SVG stops (quote one `<linearGradient>` excerpt). No value invented — a variable that could not be read is listed under "unresolved", not guessed.
- [ ] **A2 · Tripwire.** The three planted defects flagged, then absent, both JSON excerpts quoted (§20). `git status --porcelain` clean before and after (quoted).
- [ ] **A3 · Dumps.** One JSON per screen/tab/submenu/modal in scope, EN and JA, via real navigation (the driver call chain quoted once per screen — `<RealWidget>.onClick.Invoke()`, Rule 2); the `counts` block of each quoted in a summary table. Total TMP sites across Tier 1 stated; LiberationSans total reconciles with the Architect's 46 in-scope sites or the difference is explained per site.
- [ ] **A4 · Lint.** `fail`/`warn` per prefab and per live root, in one table, JSON paths cited; every FAIL row appears in the fix list or is explicitly accepted with a reason ("bars sized at runtime — width 0 in a temp canvas", the `gps_polish` A6 precedent).
- [ ] **A5 · Node-spec layer ran** for every row with a frame id: spec JSON path, mapped/unmapped element counts, and each `missing`/`width`/`height`/`radius`/`color`/`font-size` FAIL classified into the fix list.
- [ ] **A6 · Crop sheets.** One `screenshots/<Screen>_sheet.png` per Tier-1 row (≥ 900 px long edge, Rule 14), residual ΔRGB named as data vs defect. `Canonical screenshot:` line points at the sheet with the most findings.
- [ ] **A7 · Rendered-size population.** For every text site: rendered px vs node px vs type-scale step, in the dump; the report shows the two SemiBold populations (nominal vs 59/66) with cap-height crops side by side and a recommendation — no site is marked "wrong size" from the serialized number alone.
- [ ] **A8 · Shape tables** for at least shapes (i)–(ix), each with the checkable question, the exhaustive site enumeration (grep/dump command quoted), and a verdict per site including the passing ones.
- [ ] **A9 · Linter untouched in behaviour.** `LintPrefab` output on `Assets/Prefabs/UI/Shop/GeneralShopCard.prefab` byte-identical before/after the `LintRoot` refactor (md5 of the JSON quoted); `LintRootParityTests` green; no rule added/removed/re-tuned (`git diff` of `UIFidelityLinter.cs` shows only the extraction).
- [ ] **A10 · Nothing production changed.** `git status --porcelain --untracked-files=all` shows ONLY `Assets/Editor/UIFidelity/*`, `Docs/**` — no `Assets/Prefabs`, `Assets/Scenes`, `Assets/Scripts`, `Assets/Localization`, `Assets/Fonts` paths (quoted); `git diff --stat HEAD -- Assets/Scenes` empty (Rule 14 orchestrator guardrail).
- [ ] **A11 · Report + fix list** exist at the paths above; every fix group cites `DA-` ids, files, op, estimate, blast radius; section 6 (could-not-measure) is non-empty or says why it is empty.
- [ ] **A12 · EditMode** full sweep green; the new suites executed by name (quote the runner line).
- [ ] **A13 · GPS untouched.** No `Assets/Prefabs/UI/Gps`, `Assets/Scripts/UI/Gps` path in any dump, lint list, crop sheet or `git status` (grep quoted).
- [ ] **A14 · Deviations** flagged at the bottom of the report with justification.

## Smoke evidence

The crop sheets (A6), the dump summary table (A3), the lint table (A4), the shape tables (A8), the report (A11). No video is required — nothing moves in this task.

## Out of scope (do NOT do these)

- **Fixing anything.** Not one font asset, size, colour, sprite or component — the fix list is the output; Cesar approves each group as a Quick spec.
- Any change to `UIFidelityLinter` rules or thresholds; any new linter rule (file a `Docs/POLISH_BACKLOG.md` row with the rule you wanted).
- GPS screens and modals; in-game HUD / shot UI; admin dashboard; the Rubik Medium font import (`POLISH_BACKLOG.md`).
- Motion, transitions, haptics — `game_polish` (2111) and `haptics_option` (2130).
- Re-pulling art from Figma, baking sprites, importing fonts.
- Localization fixes (`unlocalized-text` is reported only).

## Files / hierarchy this task touches

- `Assets/Editor/UIFidelity/UIFidelityLinter.cs` — `LintRoot` overload (extraction only)
- `Assets/Editor/UIFidelity/DesignAuditDumper.cs` (+ optional `DesignAuditRunner.cs`) — new
- EditMode tests: the linter has NO tests today (verified 2026-09-03 — `grep -rl UIFidelityLinter Assets --include=*.cs` returns only the linter). Add `Assets/Editor/UIFidelity/Tests/` in whichever Editor test assembly already reaches `Golfin.EditorTools` (`GolfinRedux.Tests.EditMode` is the likely one — confirm the asmdef reference in the report, add one if none exists and say so)
- `Docs/Design/DESIGN_TOKENS.md` — complete
- `Docs/Reports/DESIGN_CONSISTENCY_AUDIT.md` — new
- `Docs/Diagnostics/_capture/design_audit/*.json`, `Docs/Diagnostics/_capture/*_lint.json`
- `Docs/Specs/Active/design_consistency_audit/{reference/,reference/specs/,screenshots/,IMPLEMENTER_REPORT.md,STATUS.md}`
