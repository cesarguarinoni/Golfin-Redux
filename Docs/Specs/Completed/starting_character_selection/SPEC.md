# SPEC — `starting_character_selection`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files.

## Status

See `STATUS.md` for current pipeline state.

## Goal

A brand-new player picks ONE starting character (James or Olivia) before reaching Home for the
first time. Every other character in the game — including the starter candidate they did **not**
pick — is **locked**: visible in the Roster strip, greyed with a `LOCKED` badge, un-selectable,
un-levelable. The choice happens exactly once per save; a player who is interrupted before
confirming (app killed mid-flow) gets the screen again on next boot. All new copy is localized
(EN + JA).

This introduces the game's first real **character ownership model**. Today
`PlayerCharacterData.isOwned` exists but is dead code (default `true`, never read, never persisted)
and `CharacterManager` seeds *every* CSV row as owned — so every character is playable from boot.

## Reference

- **Figma file:** `5gEAHjl6xAtW8iYY7NMvWd` (Golfin Game Redux)
- **Node renders dropped to `reference/`** (pulled at spec time via `get_screenshot`; ground truth for every A/B):
  | File | Node | What it shows |
  |---|---|---|
  | `reference/node_13924-41976_starter_unlocked.png` | `13924:41976` "Starting Character Selection" | Starter screen, an **unlocked candidate** (James) selected. Nav bar replaced by the instruction block. |
  | `reference/node_13924-42412_starter_locked.png` | `13924:42412` | Starter screen, a **locked** character (Elizabeth) selected → dimmed detail panel + acquire overlay. |
  | `reference/node_13924-42328_confirm_modal.png` | `13924:42328` "Starrting Character confirmation" | Confirm modal: title + character name + BACK / CONFIRM. |
  | `reference/node_13922-36488_roster_locked.png` | `13922:36488` | **Normal Roster** (bottom nav bar present) with a locked character selected. Post-starter state. |

- **Placeholder vs canonical content:** every stat number, level and rarity letter in the renders is
  mockup (James detail reads `RARE` + `Lv 10/39`; cards read `Lv 9` / `Lv 5`; bars read `12/30`,
  `30/30`). **CSV is truth, never the render numbers.** The render's `R 50000` top-bar balance is
  also mock. What IS canonical: layout, sizing, dim treatment, which controls are present/absent,
  and all copy strings.
- **Re-pull mandate (Rule 9):** implementer AND both reviewers run `get_design_context` on
  `13924:41976`, `13924:42412` and `13924:42328` at step 0 and diff live px/font/gap/sprite against
  the node. The tables below are a reconcile-against-node convenience, never source of truth.
  Note `13924:42412` was edited by Cesar on 2026-08-24 (Olivia's lock removed) — do not trust any
  older copy of that render.

## Decisions of record (Cesar, 2026-08-24)

1. Ownership model is **in scope** here, not a follow-up task.
2. Persistence is **local save only** for now. A reinstall / new device loses the starter choice and
   the player picks again. Server-side ownership is a known future migration — do not build it here.
3. Starter candidates come from a **CSV column**, not hardcoded ids.
4. Trigger = `SaveData.starterCharacterId == ""`, checked after account creation **and** on every
   boot before Home (this is what covers the interrupted case).
5. CONFIRM → `ScreenId.Home`.
6. Top bar (R balance + settings gear) stays visible on the starter screen; every other exit is blocked.
7. Both James and Olivia read as **selectable** on the starter screen.
8. On a **locked** character: LEVEL UP and BOOST are **disabled** (present, non-interactive);
   COMPARE and SELECT are **gone entirely** (not disabled — removed from layout).
9. Locked characters appear **only in the Roster strip**. Every other character picker is owned-only.
10. No acquisition path (store / prize) is built here — locked stays locked. Out of scope.
11. New copy: Claude writes both EN and JA.
12. **James and Olivia are both `Common`, same total base stat points, distributed differently.**

### Architect assumptions (flag in the report if you disagree; Cesar can override in one line)

- **A1 — the two stat splits.** Both total 25 base points (Str/Ctrl/Rec/Stam), distributed to give
  each starter a distinct archetype (Cesar, 2026-08-24: *"Olivia more control, James more power"*):
  - **James `7/6/5/7`** — power + stamina, weaker recovery.
  - **Olivia `6/7/6/6`** — club-control leaning, balanced elsewhere.

  Both sit within the Common caps (`Str 25 / Ctrl 25 / Rec 18 / Stam 22`). Note this **changes
  James's existing CSV row** (he was `6/7/6/6`) as well as Olivia's — the two splits are swapped
  relative to what shipped.
- **A2 — starter-screen buttons on an *unlocked candidate*.** Node `13924:41976` draws LEVEL UP,
  BOOST, COMPARE and SELECT. LEVEL UP and BOOST are **disabled** in starter mode (you cannot level a
  character you do not own yet, and RP spend is server-authoritative — it would 403). COMPARE and
  SELECT stay **enabled**. Visually all four remain present, matching the render.
- **A3 — existing saves.** The v9→v10 migration sets `starterCharacterId` to the save's current
  `selectedCharacterId`, marks that one character owned, and locks the rest. Existing testers are
  therefore NOT sent through the starter screen, but they DO see the new lock rule everywhere.
  To exercise the starter flow, delete the local save (`LocalJsonPersister` path) or use the dev
  reset in § Smoke evidence.

## Figma Fidelity (enumerate EVERY element — Rule 18)

Frame is 1170×2532 @ scale 1. Per `feedback_shell_canvas_font_conversion`: geometry maps 1:1,
TMP font sizes are node px ÷ 1.2. **Rendered cap-height must be A/B'd against the `reference/`
render at matched scale — the divisor arithmetic is not the gate** (`feedback_review_always_check_font_weight_and_rendered_size`).

### Starter screen — instruction block (replaces the bottom nav bar)

| Element | Figma node | Property → value |
|---|---|---|
| Instruction container | `13924:42124` "Nav Bar Container" | 1170×263, anchored bottom, x=0 y=2269 (i.e. flush to canvas bottom). Occupies the exact band the bottom nav bar uses in `13922:36488`. |
| Instruction text | `13924:42125` | 1003×143 at x=98 y=57 inside the container; centre-aligned, 3 lines; ALL CAPS; white; **Rubik SemiBold**; node 40px → TMP 33.33. Content = `ROSTER_STARTER_INSTRUCTION`. |
| Bottom nav bar | (from `13922:36488`) | **HIDDEN** in starter mode. Not merely covered — `PersistentUIManager` must not show it. |
| Top bar | `13924:41979` "Top UI" | 1170×313, **VISIBLE and unchanged** (R balance + gear). |

### Locked card (Roster strip — both screens)

| Element | Figma node | Property → value |
|---|---|---|
| Card root | `13924:42220` (Elizabeth, locked+selected) | 180×353 when selected / 170×343 otherwise — same geometry as an unlocked card. Locking must NOT resize the card. |
| Portrait dim | `13924:42233` "Portraits" → `13924:42234` | Full-card rounded-rect overlay above the character art, below the texts. Read the exact fill+alpha off the node — do NOT eyeball it (`feedback_never_eyeball_brightness`). |
| `LOCKED` label | `13924:42242` | 170×36, vertically centred in the card (`y=147.5` within the 343 card = mid), centre-aligned, ALL CAPS, white. Content = `UI_LOCKED` (existing key). |
| Rarity letter + level | `13924:42236` (`hidden="true"` in node) | On a locked card the Top row (rarity letter + `Lv N`) is **hidden** inside the Portraits overlay group — but the underlying card's own Top row (e.g. `13924:42226`) is still drawn. Net effect in the render: rarity letter + level **remain visible**, dimmed with the card. Match the render, not the prose. |
| Name label | `13924:42232` | Remains visible, dimmed. |
| Selected outline | `13924:42224` | A locked card still shows the gold selected outline when it is the carousel's current card. |

### Locked detail panel (Roster strip — both screens)

| Element | Figma node | Property → value |
|---|---|---|
| Character art (Left) | `13924:42018`-equivalent in `13924:42412` | Dimmed. Sample the alpha/fill from the node render; do not guess. |
| Right panel (name, rarity, level, 4 stat rows, BIO) | `13924:42021`-equivalent | All still rendered, dimmed, non-interactive. Stat bars still drawn at their real fill. |
| Acquire overlay text | in `13924:42412` | Centred across the FULL panel width (spans both Left and Right columns), 2 lines, ALL CAPS, white, **Rubik SemiBold**, drawn ABOVE the dim layer at full opacity. Vertically ~centred on the panel. Content = `ROSTER_LOCKED_ACQUIRE`. |
| LEVEL UP button | `13924:42112`-equivalent | **Present, disabled** (`Button.interactable = false` + the disabled visual). |
| BOOST button | `13924:42113`-equivalent | **Present, disabled.** |
| COMPARE button | `13924:42121`-equivalent | **Absent** — `SetActive(false)`, removed from the layout so nothing leaves a gap. |
| SELECT button | `13924:42123`-equivalent | **Absent** — same treatment. |

### Confirm modal (`13924:42328`)

| Element | Figma node | Property → value |
|---|---|---|
| Pop-Up panel | `13924:42329` | 978×379, rounded, navy panel + light rim. **Clone the panel sprite from `TournamentSignupModal.prefab`** (see § Clone provenance) — do not author a flat fill. |
| Top separator | `13924:42330` | 978-wide 1px line at y=0. |
| Content container | `13924:42331` | 978×227. |
| Title text | `13924:42334` | 882×47 at x=48 y=32 (inside `13924:42332` "Upper"); centred; ALL CAPS; **Rubik Medium**; node 40px → TMP 33.33. Content = `ROSTER_STARTER_CONFIRM_TITLE`. |
| Character name text | `13924:42354` | 882×76 at y=63 within Upper; centred; ALL CAPS; **Rubik Bold**; node 64px → TMP 53.33. Content = the chosen character's `"{name} {lastName}"` from CSV — **not** a localization key. |
| Mid separator | `13924:42335` | 882-wide 1px line at x=48 y=195. **Gap: 24px above (to the character-name text) and 24px below (to the buttons row) — Cesar, 2026-08-25, verbatim: *"It should be 24px on top and the same on the bottom"*. This number is authoritative and overrides whatever the node spacing computes to.** Measure the gaps numerically (world corners) and report them as exact values, not "about 24". |
| Buttons row | `13924:42336` | 798×120 at x=90 y=227. Two `Main Buttons` instances. |
| BACK button | `13924:42338` | 359×120, **silver** variant, left. Label `ROSTER_STARTER_BACK`. |
| CONFIRM button | `13924:42340` | 391×120, **gold** variant, right, starts at x=407 within the row (i.e. a 48px gap). Label `MODAL_CONFIRM` (existing key). |
| Backdrop | — | Standard `ModalController` backdrop. Note `reference_linear_space_alpha_and_canvas_sorting`: 50% black only dims white to ~187 — match the family's existing backdrop value rather than inventing one. |

## Cover art supplied by Cesar (2026-08-24) — replaces ALL hand-rolled flat fills

Cesar dropped real art for the three fabricated flat fills that iter-1/iter-2 hand-rolled. These are
now mandated clone-provenance sources. **Authoring a flat-colour fill for any of these three is a
hard FAIL.**

| Element | Asset | Size | Matches |
|---|---|---|---|
| Instruction block background (starter screen) | `Assets/Art/RosterScreen/Nav Bar Cover.png` | 1170×263 | Figma node `13924:42124` "Nav Bar Container" — **exact** match. Carries the gradient; Cesar: *"yours was flat and not using the gradient that one has"* |
| Locked portrait cover (carousel card) | `Assets/Art/RosterScreen/Locked Portraits.png` | 178×351 | Card geometry 170×343 / 180×353 selected. Replaces the hand-rolled `new Color(0.05f, 0.13f, 0.20f, 0.75f)` in `CharacterThumbnailCard.SetLocked` (fail F8) |
| Locked detail-panel cover | `Assets/Art/RosterScreen/Roster Cover.png` | 1082×1491 | Figma "Outline" node `13924:42016` is 1074×1483; +4px rim per side. Cesar: *"the cover you are using does not have rounded corners"* — this sprite has them |

**Note:** `Assets/Art/RosterScreen/Button - Retry.png` is a byte-identical duplicate of
`Roster Cover.png` (both md5 `d118cd92a17ee6a3be2666027de444dd`) — an earlier copy under a wrong
filename. Use `Roster Cover.png`. Do not delete the duplicate without asking Cesar; another task may
reference that name.

**ALL THREE ARE IMPORTED AS `textureType: 0` (Default), `spriteMode: 0` — they are NOT sprites yet.**
No `Image.sprite` can reference them until each is re-imported as **Sprite (2D and UI)**. Do that
first, via the importer API (never by hand-editing `.meta`).

**Scaling discipline** (Rule 21 render-health; both of these are defects Cesar has caught by eye
before): a 9-sliced sprite without `pixelsPerUnitMultiplier` collapses its corners into an oval; a
non-9-sliced sprite stretched non-uniformly distorts its corner radius. Prefer native-size
`Image.Type.Simple` where the sprite already matches the target rect — `Nav Bar Cover.png` does
(1170×263). Otherwise 9-slice with correct `spriteBorder` AND set the multiplier.

Never sample a colour off these PNGs and reproduce it as a fill — the entire point is that they
carry gradients and rounded corners.

## Clone provenance (Rule 19 — REUSE MANDATE)

**Author ZERO new panels, buttons, or badges from scratch.** Every element below is cloned from a
real, existing source. If a mandated source cannot be located: set `IMPLEMENTER_BLOCKED` and
surface it — **do not hand-roll it and do not fill the row with prose**
(`feedback_reuse_map_clone_provenance_gate`). Before building, map each node element to an atom in
`Docs/Architecture/UI_ELEMENT_PALETTE.md` (Rule 22).

| Element to build | Clone from (concrete source) |
|---|---|
| Confirm-modal panel + backdrop + rim | `Assets/Prefabs/UI/Modals/TournamentSignupModal.prefab` → `Panel` |
| Confirm-modal buttons row | `TournamentSignupModal.prefab` → `ButtonsRow` (already the Figma "Main Buttons" family: silver + gold) |
| Modal controller base | `ModalController` (existing base class — extend, do not reimplement fade/backdrop/show/hide) |
| Locked-card overlay + `LOCKED` label | Existing roster card prefab used by `CharacterThumbnailCard` — add the overlay child, do not rebuild the card |
| Instruction block background | The bottom band already used by the nav bar container in `ShellScene` — or a plain solid matching the node; cite whichever you use |
| Starter screen itself | `ScreensRoot/RosterScreen` **in-place, mode-flagged.** Do NOT duplicate the roster screen. |

Reviewers verify this table by reading back the **live** `Image.sprite` GUID on each mandated
element. A flat-colour fill where a sprite is required = FAIL (Rule 11 / Rule 21 `requireSprite`).

## Architecture context

- **Asmdefs affected:** `Golfin.Save`, `Golfin.Localization` (add a reference if a new script needs
  `LocalizationManager.Get` — see `reference_localization_asmdef`), plus the roster UI assembly.
- **Existing code referenced:**
  - `Assets/Scripts/CharacterManager.cs` — `ownedCharacters` dict, `GetAllOwnedCharacters()`, `SelectCharacter()`, `SyncToSave()`
  - `Assets/Scripts/UI/Roster/Data/PlayerCharacterData.cs` — dead `isOwned` field (line 54)
  - `Assets/Scripts/UI/Roster/UI/CharacterThumbnailCard.cs` — `// Phase 2b` locked-state stub (line 19)
  - `Assets/Scripts/UI/Roster/UI/RosterScreenController.cs`, `CarouselController.cs`, `CharacterDetailPanel.cs`
  - `Assets/Scripts/UI/ScreenManager.cs` — `ScreenId`, `ShowScreen`, `isMenuScreen`/`showBars`
  - `Assets/Scripts/UI/PersistentUIManager.cs` — bar visibility
  - `Assets/Scripts/UI/Account/CreateUsernameScreenController.cs:82` — currently `ShowScreen(ScreenId.Home)`
  - `Assets/Scripts/Save/SaveData.cs`, `SaveSchemaMigrator.cs` (`CurrentSchemaVersion = 9`)
  - `Assets/Scripts/ClubManager.cs` — the `clubOwnershipSeeded` / `SeedStarter` pattern is the model to copy
- **Existing assets:** `Assets/Data/Characters.csv`, `Assets/Localization/LocalizationText.csv`,
  `Assets/Prefabs/UI/Modals/TournamentSignupModal.prefab`

## Implementation

### 1. Data — `Assets/Data/Characters.csv`

Add a `starterCandidate` column (`0`/`1`) and rebalance Olivia to Common:

- `char_james` → `rarity=Common` (already), `baseStrength=7, baseClubControl=6, baseRecovery=5,
  baseStamina=7`, `startLevel=10`, `maxLevel=39` (already), `starterCandidate=1`  *(assumption A1 —
  his base stats DO change, from `6,7,6,6`)*
- `char_olivia` → `rarity=Common` (was Uncommon), `baseStrength=6, baseClubControl=7,
  baseRecovery=6, baseStamina=6`, `startLevel=10` (was 40), `maxLevel=39` (was 79),
  `starterCandidate=1`  *(assumption A1)*
- every other row → `starterCandidate=0`

Extend the `CharacterDatabaseCSV` parser and `CharacterData` with the new field. The parser must not
break on the added column — verify against the live CSV, and keep it tolerant of a missing column
(default `0`) so a stale CSV cannot NRE.

### 2. Data — `Assets/Scripts/Save/SaveData.cs`

- `SaveData.starterCharacterId` — `string`, default `""`. **This is the single source of truth for
  "has the player chosen a starter".**
- `PersistedCharacter.isOwned` — `bool`.
- Bump `CurrentSchemaVersion` 9 → 10 and add the v9→v10 migration (assumption A3):
  set `starterCharacterId = selectedCharacterId`, mark that character `isOwned = true`, everything
  else `false`. Log the migration line like the existing ones.

### 3. `CharacterManager`

The dict already holds **every catalog character** — keep that. Make ownership explicit:

- Hydrate `PlayerCharacterData.isOwned` from `PersistedCharacter.isOwned`; persist it in `SyncToSave`.
- Fresh save (`starterCharacterId == ""`): nothing is owned.
- `bool IsOwned(string characterId)`
- `List<PlayerCharacterData> GetOwnedCharacters()` — owned only
- `List<PlayerCharacterData> GetAllCatalogCharacters()` — everything, ordered as today
- `IReadOnlyList<CharacterData> GetStarterCandidates()` — CSV `starterCandidate == 1`
- `void GrantStarter(string characterId)` — sets `isOwned`, `starterCharacterId`, selects it, persists.
  Must be idempotent and must refuse an id that is not a starter candidate.
- `SelectCharacter(id)` must **refuse** an unowned id (log + no-op), not silently select it.
- **Rename the old `GetAllOwnedCharacters()` out of existence** so no call site silently keeps the
  old semantics. Retarget every one of the 5 call sites deliberately:
  `RosterScreenController.cs:37` and `CarouselController.cs:120` → `GetAllCatalogCharacters()`
  (the strip shows locked cards); `RosterDebugTools.cs:25` and
  `StaminaLiveMeterDemoRecorder.cs:220,275` → whichever is correct for that tool, stated in the report.
  Any other picker (Compare, tournament sign-up, Home) must use `GetOwnedCharacters()` — enumerate
  every call site you touched in the report (decision 9).

### 4. Screen — `ScreenId.StartingCharacterSelection`

- Add to the `ScreenId` enum. It is **post-auth**, so `AuthGate` needs no change (`HasSession` covers it).
- `ScreenManager.SetScreenActive`: activate `_rosterScreen` when
  `screenId == ScreenId.Roster || screenId == ScreenId.StartingCharacterSelection`.
- **Exclude** it from `isMenuScreen` / `showBars` → no bottom nav bar, no back path.
- `RosterScreenController.SetStarterMode(bool)`:
  - starter mode ON → instruction block active, bottom bar hidden, SELECT re-routed to the confirm
    modal, LEVEL UP + BOOST disabled (assumption A2), the two starter candidates rendered unlocked
    even though `isOwned == false`.
  - starter mode OFF → today's behaviour exactly.
- **Trap C2:** the instruction block must be a child that toggles — do not toggle a modal root.
  **Trap C7:** verify in play mode; the edit-mode Game View does not repaint.

### 5. Routing

Single helper, called from both places (decision 4):

```
bool NeedsStarter => string.IsNullOrEmpty(save.starterCharacterId);
```

- `CreateUsernameScreenController` line 82: `ShowScreen(NeedsStarter ? StartingCharacterSelection : Home)`.
- Boot path into Home (the post-auth landing, wherever `ShowScreen(ScreenId.Home)` fires after the
  splash/login gate): same branch. This is what makes the interrupted case work — **prove it by
  killing play mode mid-flow and re-entering**, not by reasoning about it.
- After CONFIRM → `GrantStarter(id)` → `ShowScreen(ScreenId.Home)`.

### 6. Locked presentation

- `CharacterThumbnailCard` — replace the `// Phase 2b` stub with the real locked state per the
  fidelity table. Starter candidates on the starter screen render as **unlocked**.
- `CharacterDetailPanel` — locked state per the fidelity table: dim, acquire overlay, LEVEL UP +
  BOOST disabled, COMPARE + SELECT `SetActive(false)`.

### 7. Confirm modal

New `Assets/Prefabs/UI/Modals/StartingCharacterConfirmModal.prefab` + a controller extending
`ModalController`, cloned per § Clone provenance. BACK → dismiss, **no state change at all**.
CONFIRM → grant + Home. Every new `Button` gets `Golfin.UI.Polish.ButtonPressFeedback` (hard rule 11).

### 8. Localization — `Assets/Localization/LocalizationText.csv`

Reuse `UI_LOCKED` (LOCKED/ロック) and `MODAL_CONFIRM` (CONFIRM/確認). Add:

```
ROSTER_STARTER_INSTRUCTION,"CHOOSE YOUR STARTING CHARACTER. YOU WILL BE ABLE TO ACQUIRE NEW CHARACTERS THROUGHOUT THE GAME.",スタートキャラクターを選んでください。新しいキャラクターはゲームを進めながら獲得できます。
ROSTER_STARTER_CONFIRM_TITLE,YOU ARE STARTING THE GAME WITH:,このキャラクターでゲームを開始します:
ROSTER_STARTER_BACK,BACK,戻る
ROSTER_LOCKED_ACQUIRE,"ACQUIRE THIS CHARACTER IN THE STORE OR AS A PRIZE TO UNLOCK IT.",このキャラクターはショップまたは報酬で獲得するとアンロックされます。
```

All new text goes through `LocalizationManager.Get(...)`, never a literal. **Repaint on language
change** — every one of these strings must update live when the language is switched from the
Settings overlay; the overlay never disables the screen underneath, so imperative `Get()` text goes
stale (`reference_stale_localization_settings_overlay`). Do not preview JA with a static font asset
(`reference_japanese_preview_bakes_font_atlas`) and do not refresh assets during play mode
(`reference_no_recompile_during_play`).

## Acceptance checklist (Implementer fills in `IMPLEMENTER_REPORT.md`)

Every line PASS/FAIL with a one-sentence justification citing what was measured.

- [ ] Fresh save (no `starterCharacterId`) boots → Starting Character Selection, NOT Home
- [ ] Account creation (CreateUsername → success) lands on Starting Character Selection
- [ ] Interrupted case: exit play mode after reaching the starter screen but before CONFIRM; re-enter → starter screen again, choice not persisted
- [ ] James and Olivia both render **unlocked and selectable** on the starter screen
- [ ] Every other character on the starter screen renders LOCKED with the acquire overlay
- [ ] Locked detail: LEVEL UP + BOOST present-but-disabled; COMPARE + SELECT absent from the layout with no leftover gap
- [ ] Bottom nav bar is hidden on the starter screen and the instruction block occupies that band; top bar (R + gear) still visible
- [ ] No exit from the starter screen except SELECT → CONFIRM (no back button, no nav bar, no gesture)
- [ ] SELECT opens the confirm modal showing the chosen character's real CSV name
- [ ] BACK dismisses with zero state change (character still unowned, `starterCharacterId` still `""`)
- [ ] CONFIRM grants the character, persists `starterCharacterId` + `isOwned`, lands on Home
- [ ] After confirming, Roster shows the chosen character owned/selected and the **other starter candidate LOCKED**
- [ ] Second boot after confirming goes straight to Home — the starter screen never reappears
- [ ] `SelectCharacter()` refuses an unowned id (prove with a direct call, logged)
- [ ] Every non-Roster character picker lists owned characters only — call sites enumerated in the report
- [ ] James and Olivia are both `Common`, `Lv 10/39`, equal base-stat totals (25), with James power/stamina-leaning (`7/6/5/7`) and Olivia control-leaning (`6/7/6/6`) — read the values back off the live `PlayerCharacterData`, not the CSV text
- [ ] v9→v10 migration runs on an existing save without data loss (test with a real pre-change save file)
- [ ] All 4 new keys resolve in EN **and** JA; switching language from the Settings overlay repaints all of them live (no raw keys, no stale EN under JA)
- [ ] **Clone provenance** table filled — every row cites a real prefab path / asset path / GUID; live `Image.sprite` reads back non-null on each mandated element
- [ ] **Figma fidelity** table filled — per-element PASS/FAIL vs the `reference/` renders, including font WEIGHT and rendered cap-height for every text element
- [ ] **UI fidelity lint** (Rule 21) — `UIFidelityLinter.LintPrefab` run on the confirm-modal prefab, JSON cited, `fail == 0`
- [ ] No white-box placeholders visible in any screenshot
- [ ] All `[SerializeField]` references wired (via `SerializedObject`, never by asking Cesar)
- [ ] Unity Console has no errors related to this task
- [ ] Spec deviations flagged at the bottom of the report with justification

## Files / hierarchy this task touches

- `Assets/Data/Characters.csv` — `starterCandidate` column; Olivia → Common + rebalanced
- `Assets/Scripts/UI/Roster/Managers/CharacterDatabaseCSV.cs` + `Data/CharacterData` — parse the new column
- `Assets/Scripts/Save/SaveData.cs` — `starterCharacterId`, `PersistedCharacter.isOwned`
- `Assets/Scripts/Save/SaveSchemaMigrator.cs` — v9→v10
- `Assets/Scripts/CharacterManager.cs` — ownership API, `GrantStarter`, guarded `SelectCharacter`
- `Assets/Scripts/UI/ScreenManager.cs` — new `ScreenId`, activation, bar exclusion
- `Assets/Scripts/UI/PersistentUIManager.cs` — bar visibility for the new screen
- `Assets/Scripts/UI/Account/CreateUsernameScreenController.cs` — routing branch
- `Assets/Scripts/UI/Roster/UI/RosterScreenController.cs` — `SetStarterMode`
- `Assets/Scripts/UI/Roster/UI/CarouselController.cs` — catalog vs owned list
- `Assets/Scripts/UI/Roster/UI/CharacterThumbnailCard.cs` — locked card state
- `Assets/Scripts/UI/Roster/UI/CharacterDetailPanel.cs` — locked detail state
- `Assets/Prefabs/UI/Modals/StartingCharacterConfirmModal.prefab` + controller — NEW (cloned)
- `Assets/Scenes/ShellScene.unity` — instruction block child under `RosterScreen`; modal wiring
- `Assets/Localization/LocalizationText.csv` — 4 new keys

**Standing bans apply:** zero edits to `Assets/Scripts/Physics/`; no `*Gate` scenarios in
`Scenarios.cs`; nothing baked into `LabScaffold.unity`; `M_Splash*.mat` untouched.
**Scene-save discipline:** diff GameObject active-state vs HEAD before any `scene-save`
(`project_scene_save_bakes_layout_churn`, PIPELINE_HARDENING §14) — this task touches `ScreensRoot`.

## Smoke evidence

**Real-entry rule (PIPELINE_HARDENING §2).** Every transition must be driven through the REAL
widget's `onClick` — `SelectButton.onClick.Invoke()`, `ConfirmButton.onClick.Invoke()`. A synthetic
test-only button is an automatic FAIL at all three gates. Boot through the real path:
ShellScene → title/PLAY gate → login/account → starter screen
(`reference_editor_login_devautosignin`, `feedback_real_world_game_testing`). `ShowScreen(target)`
alone is a **false positive** — the frame stays on the title screen.

**Fresh save is the PREFERRED test condition (Cesar, 2026-08-25):** *"I don't care about my save.
In fact, better to start from 0 to check the flow."* Delete
`~/Library/Application Support/NEXT INNOVATION PTE_ LTD_/Golfin/save.json` outright and boot from
nothing — that exercises the genuine first-run path, which is what this feature is. There is NO
requirement to preserve or restore any existing save. Wiping `save.json` does not clear the auth
session (PlayerPrefs); `PlayerPrefs.DeleteAll()` remains banned because it logs the Editor out.

**Captures — 1170×2532, via `mcp__ai-game-developer__screenshot-game-view`** (Capture Rule 0 —
never a hand-rolled `script-execute`; the hook hard-blocks it). Set `runInBackground` and **look at
every PNG before citing it** (`reference_playmode_capture_runinbackground`,
`reference_snapplaymodesafe_phantom_path` — assert the file exists). Every UI state, named
(`feedback_review_all_ui_states`):

1. Starter screen, James selected (unlocked)
2. Starter screen, Olivia selected (unlocked)
3. Starter screen, a locked character selected (acquire overlay)
4. Confirm modal open
5. Roster after confirming — chosen character owned, other candidate LOCKED
6. Locked detail panel in the normal Roster (nav bar present) — the `13922:36488` state
7. Starter screen in **JA**
8. Confirm modal in **JA**

Declare exactly one **canonical screenshot** (long edge ≥ 900px, Rule 14).

**Video (Cesar standing rule — `feedback_video_confirmation_always`).** One clip of the whole flow:
account creation → starter screen → browse a locked character → SELECT → BACK → SELECT → CONFIRM →
Home → Roster showing the other candidate locked. Reuse the DemoRecorder family
(`reference_ui_demo_recorder_family`) — never hand-stitch stills. Record **full 1170×2532**
(`feedback_record_bot_video_full_size`), caption with `Docs/Scripts/build_bot_video.py`'s
`textfile=` idiom (`reference_video_caption_tool`), write it to `videos/`, stills to `screenshots/`
(`convention_videos_vs_screenshots`), and give Cesar the path as a clickable link.

**Leave the editor clean** (`feedback_leave_editor_clean`): exit play mode, no dirty scene, no
leftover mutations. The save file does NOT need restoring — see the fresh-save note above.

## Out of scope (do NOT do these)

- Any acquisition path — store purchase, gacha grant, prize unlock. Locked stays locked (decision 10).
- Server-side ownership / syncing the starter to Supabase (decision 2).
- The sort/filter button visible top-right of the tab bar in `13924:42412` and `13922:36488` — it
  does not exist in code today and is not part of this task.
- Re-theming or re-laying-out the Roster screen beyond the locked state and the instruction block.
- Changing any character other than James and Olivia in `Characters.csv`.
- Adding a back button, gesture, or any other exit from the starter screen.
