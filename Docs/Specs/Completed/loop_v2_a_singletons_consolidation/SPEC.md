# Loop v2 — Stage A — Singletons Consolidation

**Status:** SPEC_READY (architect, 2026-05-19)
**Type:** TELLCODE (multi-file, established patterns, no new asmdef/arch)
**Parent:** `Docs/Specs/Active/loop_v2_scope/SPEC.md`
**Notion:** Loop v2 Order 300 — `loop_v2_a_singletons_consolidation`

---

## Goal

**One bottom-nav controller. One SettingsController.**

Today there are two of each, both wired into production. Loop v2 will add new screens that need to navigate and open settings; doing that with the current source-of-truth split would compound the mess. This stage strips the duplicates before any new code lands.

## Audit references

- `Docs/Architecture/CODE_AUDIT_2026-05-19.md` **P0-1** — Two parallel bottom-nav bars driving `ScreenManager`.
- `Docs/Architecture/CODE_AUDIT_2026-05-19.md` **P0-2** — Two `SettingsController`s in production.

---

## Part 1 — Bottom nav consolidation (P0-1)

### Decision
`PersistentUIManager` owns the bottom nav. `HomeScreenController`'s duplicate nav wiring is stripped.

**Why PersistentUI:** it's already designed to persist across screens (lives in ShellScene, `DontDestroyOnLoad`, shows/hides bars based on screen). The bottom nav bar's job is exactly that — persist across screens. `HomeScreenController` should care about Home-specific content (news panel, character image, next-hole panel, play button), not navigation.

### Files to touch
| File | Change |
|---|---|
| `Assets/Scripts/UI/HomeScreenController.cs` | Strip 5 nav button SerializeFields (`navHomeButton`, `navGachaButton`, `navTeeButton`, `navInventoryButton`, `navCharactersButton`), their 5 icon SerializeFields (`navHomeIcon` through `navCharactersIcon`), the `navNormalColor` / `navActiveColor` fields, the `OnNavClicked` method, the `SetActiveNav` method, and the corresponding `AddListener` block in `Awake` (lines 114–119) and the `SetActiveNav(ScreenId.Home)` call in `OnEnable` (line 153). |
| Scene `ShellScene.unity` (and any scene that has Home's bottom nav wired) | Cesar visually verifies the Home GameObject's HomeScreenController component shows no broken Inspector refs; if there were nav references on the scene-level HomeScreen, they become dangling. Removing those Inspector fields stops Unity rendering them. **Cesar action:** save the scene after Code's diff lands. |

### Verification (testable)
- `grep -n "navHome\|navGacha\|navTee\|navInventory\|navCharacters\|OnNavClicked\|SetActiveNav" Assets/Scripts/UI/HomeScreenController.cs` returns zero matches.
- `PersistentUIManager` is unchanged — it remains the single nav-wiring authority.
- Compile clean. EditMode test gate green.
- **Visual (Cesar):** launch → Home → bottom nav highlights Home. Tap Roster (Characters button) → Roster screen, bottom nav highlights Characters. Tap Inventory → Inventory screen, bottom nav highlights Inventory. Tap Tee (Hole Selection) → Hole Selection, bottom nav highlights Tee. Tap Home → back to Home, bottom nav highlights Home. **Each highlight should flip exactly once per tap, not twice.**

---

## Part 2 — Settings consolidation (P0-2)

### Decision
Delete `SettingsController.cs` (Phase 1). Rename `SettingsControllerPhase2.cs` → `SettingsController.cs` and the class `SettingsControllerPhase2` → `SettingsController`. Update both call sites.

**Why Phase 2:** it has the accordion behavior, submenu wiring, and the modal hook for Phase 3 work. Phase 1's click handlers are all `Debug.Log` stubs — there is no functionality to preserve there.

### Files to touch
| File | Change |
|---|---|
| `Assets/Scripts/UI/SettingsController.cs` | **Delete** (and its `.meta`). |
| `Assets/Scripts/UI/SettingsControllerPhase2.cs` | **Rename to** `SettingsController.cs` (use `git mv` so the `.meta` GUID follows the file — preserves scene component references). Class rename: `SettingsControllerPhase2` → `SettingsController`. `public static SettingsControllerPhase2 Instance` → `public static SettingsController Instance`. |
| `Assets/Scripts/UI/PersistentUIManager.cs` | Line ~200 (`OnSettingsButtonClick`): drop the Phase 2 fallback to Phase 1 branch. Single call: `if (SettingsController.Instance != null) SettingsController.Instance.OpenSettings();`. |
| `Assets/Scripts/UI/HomeScreenController.cs` | Line ~244 (`OnSettingsClicked`): no change to the call (it was already calling `SettingsController.Instance`), but verify the `using` directives resolve to the new (renamed) class. |
| Scene `ShellScene.unity` (and any scene with Settings) | The Settings GameObject likely has a `SettingsControllerPhase2` component AND/OR a `SettingsController` (Phase 1) component. After the rename: the Phase 2 component will appear as `SettingsController` automatically because the .meta GUID is preserved (Unity remaps the script reference). **Cesar action:** if a Phase 1 `SettingsController` component is also attached (separate to Phase 2), remove it manually in the Inspector — it'll show as "Missing (Mono Script)" after the .cs delete. Save the scene. |

### Verification (testable)
- `grep -rn "SettingsControllerPhase2" Assets/Scripts/` returns zero matches.
- `find Assets/Scripts/UI -name 'SettingsControllerPhase2*'` returns no files.
- `find Assets/Scripts/UI -name 'SettingsController.cs'` returns exactly one file.
- Compile clean. EditMode test gate green.
- **Visual (Cesar):** tap settings button from Home → settings panel opens with accordion items (User Profile, Sound Settings, Language, About) — NOT the Phase 1 flat list with Debug.Log handlers. Tap an accordion item → expands. Tap another → previous collapses (accordion-exclusive). Tap close → panel closes. Tap settings from Roster screen → same behavior. **No "Phase 1 / Phase 2" toggle in code paths**.

---

## Order of operations (for Code)

1. **Part 1 first.** Strip nav from `HomeScreenController`. Compile clean. Confirm bottom nav still functions via PersistentUI in Play mode (Cesar can verify or trust).
2. **Part 2 second.** Delete Phase 1, rename Phase 2. Compile clean. Cesar visually verifies Settings panel still opens.
3. Run EditMode test gate. Expect green (no test changes needed — neither nav nor settings has dedicated EditMode coverage today; that's audit P3).
4. Append to TellCode.md completion log per house style.

---

## Out of scope (deferred)

- **Migrating modals to `ModalController` base class** (P1-4 in audit) — happens during Stage C when the Result modal lands. Settings stays as-is for now; it doesn't extend `ModalController` and won't until a P2 cleanup pass.
- **Namespace migration** `Golfin.UI` ↔ `GolfinRedux.UI` (P1-1) — out of scope. SettingsController stays in `Golfin.UI`. HomeScreenController stays in `GolfinRedux.UI`.
- **Debug.Log cleanup** (P1-5) — out of scope.
- **`.bak` files in `Assets/Scripts/UI/Editor/`** (P2) — out of scope. They can be `git rm`'d in a future housekeeping pass.

---

## Risks / things to watch

1. **Hidden scene-level nav wiring.** If a scene has nav buttons wired directly to `HomeScreenController` (not via PersistentUI), stripping the methods will break those wirings silently (Unity logs a missing-method warning at runtime but doesn't fail compile). Mitigation: Cesar plays through the full nav loop after Part 1.
2. **Phase 1 / Phase 2 dual-component-in-scene.** If both controllers are attached to the same GameObject in ShellScene, the rename will make Phase 2 appear as `SettingsController` (correct), and Phase 1 will appear as a Missing Script (orphan). Cesar removes the orphan manually.
3. **`SettingsControllerPhase2.Instance` typed callers.** I've identified two (`PersistentUIManager`, `HomeScreenController`). If grep surfaces a third, update it too.

---

## Definition of Done (full DoD per scoping spec)

- `HomeScreenController` has zero nav button SerializeFields and zero `OnNavClicked` references. `PersistentUIManager` is the only writer to `ScreenManager.ShowScreen` from a nav-bar context.
- `SettingsController.cs` (Phase 1) is deleted. `SettingsControllerPhase2.cs` is renamed to `SettingsController.cs`, class renamed to `SettingsController`, `Instance` type updated. Both call sites (`PersistentUIManager:OnSettingsButtonClick`, `HomeScreenController:OnSettingsClicked`) point at the single controller.
- Visual: tap settings from Home → opens. Tap settings from Roster → opens. Bottom nav highlights correctly when switching screens. No double-fire / double-highlight.
- Compile clean, test gate still green.
