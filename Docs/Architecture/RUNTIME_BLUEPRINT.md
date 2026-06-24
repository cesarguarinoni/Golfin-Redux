# Runtime Architecture Blueprint

> **Purpose:** Living reference of how the runtime is wired today — namespaces, asmdef boundaries, singletons, public APIs, where assets live. Updated whenever a session touches manager APIs, namespaces, asmdef references, or asset paths.
>
> **Why this exists:** Multiple Phase 8 sessions burned tokens re-discovering the same facts ("does CharacterManager have DisplayName? what asmdef are the managers in? where do hole maps live?"). This doc front-loads those answers so future Architect/Code sessions can spec confidently without grep-and-read deep dives.
>
> **Maintenance rule (BOTH Claudes):**
> - **Architect** — when writing a spec that surfaces a new manager API, asmdef ref, or asset path, update the relevant section here BEFORE handoff. If a NEEDS-VERIFICATION marker is consumed, replace it with the verified fact.
> - **Code** — when implementing a task that adds/changes a manager API, asmdef ref, asset path, or namespace, add a "Blueprint updates:" line to the done report listing the diffs to this file. If Architect's spec already encoded the change, just confirm it landed.
> - When in doubt, update. Stale > missing.
>
> **Last updated:** 2026-04-30 (added §10 Editor Tooling — CaptureHelper + FakeStateLock; renumbered §10→§11)

---

## 1 — UI Coordinate System

Canonical reference: **1170×2532** (iPhone 14 Pro / 13 Pro point grid × 3, matches Figma source).
Canonical scaler config: `Scale With Screen Size, Reference 1170×2532, Match Width Or Height, Match 0`.

At 1170-wide screens, **1 Figma px = 1 Unity unit** (scale factor 1.000).
At 1284-wide screens, scale factor ≈ 1.097 (uniform on both axes — pinned to width).
At 1080-wide screens, scale factor ≈ 0.923.

**When writing a UI spec, extract Figma values directly and use them 1:1 in Unity. No conversion factor needed.**

### History

Until 2026-04-29 the in-game canvases were authored at `1080×1920 / Match=0.5`. Combined with iPhone 12 Pro Max test screens (1284×2778), this produced a constant ~1.31× uniform scale that silently distorted every UI spec — biggest manifestation was Phase 8.3 attempt 1 (player/hole cards rendered ~31% oversized). Investigation findings: `Docs/Specs/Queued/FIGMA_UNITY_SIZE_MISMATCH.md`. Fix plan: `Docs/Specs/Queued/CANVAS_SCALER_FIX_PLAN.md`. Migration applied 2026-04-29 across 7 scalers in 5 physics-lab scenes; hypothesis validated via `Assets/Scenes/Tests/CanvasScalerTest.unity` matrix (red 180×180 box measured exactly 180×180 at row 4 = 1170×2532/Match=0).

### Exceptions (canvases NOT migrated — intentional)

- `Prefabs/UI/PersistentUI.prefab` and ShellScene's secondary canvases use **Constant Pixel Size** mode (`uiScaleMode = ConstantPixelSize`). Reference resolution is ignored in that mode, so the bug never affected them. These were authored before the design system standardized on 1170 and stay as-is.
- `Scenes/ShellScene.unity` line 86681 + `Prefabs/Original/Gameplay/Hud/GameplayMonitorCanvas.prefab` are already at `1170×2532, Match=1`. Cesar authored these correctly. Left alone (the migration script confirms `[OK] already at target` and skips).
- Menu / inventory / roster / bags / items screens — none currently authored against the bad config (all on Constant Pixel Size mode, or already at 1170). Audit pre-condition added to TellCode roadmap item C: re-verify when wiring menus to gameplay.

### Standing rule

Any new canvas added to the project should use `Scale With Screen Size, 1170×2532, Match=0` unless there's a specific reason to deviate. Document any deviation in this section.

---

## 2 — Asmdef Map

The repo has TWO asmdef regions and one default `Assembly-CSharp` bucket. This boundary matters for every UI binder that wants to read manager state.

| Folder | Asmdef | Notes |
|---|---|---|
| `Assets/Scripts/Gameplay/UI/ShotUI/` | `Golfin.Gameplay.UI` | Refs: `Golfin.Gameplay.Input`, `Golfin.Gameplay.Config`, `Unity.TextMeshPro`, `Unity.ugui`. **Does NOT reference `Assembly-CSharp`** → cannot see CharacterManager, BagManager, etc. without an additive ref. |
| `Assets/Scripts/Gameplay/Config/` | `Golfin.Gameplay.Config` | Holds `ControlsConfig`, `ControlsConfigLoader`. |
| `Assets/Scripts/Gameplay/Input/` | `Golfin.Gameplay.Input` | (assumed; verify when next touched) **NEEDS VERIFICATION: full ref list.** |
| `Assets/Scripts/Gameplay/Defaults/` | `Golfin.Gameplay.Defaults` | (assumed) **NEEDS VERIFICATION: full ref list.** |
| `Assets/Scripts/Gameplay/Tests/` | `Golfin.Gameplay.Tests` (assumed) | EditMode tests. **NEEDS VERIFICATION.** |
| `Assets/Scripts/Physics/Viewer/` | `Golfin.Physics.Viewer` | Holds `PhysicsLabController`, `LabHoleBinder`, `BallAnimator`, `ChaseCamera`, `ShotPreset*`. References `Golfin.Gameplay.UI` (`PhysicsLabController.cs` imports `Golfin.Gameplay.UI.ShotUI`). |
| `Assets/Scripts/Physics/Core/` | `Golfin.Physics` (assumed) | `BallSimulation`, `ShotInputBuilder`, etc. **NEEDS VERIFICATION.** |
| `Assets/Scripts/` (root, **everything else**) | **`Assembly-CSharp` (default)** | All managers (CharacterManager, BagManager, ClubManager, BallManager, ItemManager, BagDatabaseCSV), data classes, UI screens (`Assets/Scripts/UI/...`), Course code, HoleMetadata. |

**Practical implication for `Golfin.Gameplay.UI` widgets that need manager state:** add `Assembly-CSharp` to the asmdef references array. This is a one-time, additive change. Any new HUD widget that reads `CharacterManager.Instance` etc. depends on this — Code must add it before the first widget compiles. No reflection workarounds needed.

**Update 2026-04-28 (from Phase 8.3 redo):** the simple "add Assembly-CSharp ref" path described above only works if `autoReferenced: false` (which is the default — in that case Assembly-CSharp doesn't auto-ref this asmdef back, so no cycle). If the asmdef is `autoReferenced: true` (so Assembly-CSharp auto-references it), then adding Assembly-CSharp to the asmdef's references creates a cycle and the project will not compile.

The project currently has `Golfin.Gameplay.UI.autoReferenced = true` (set during 8.3 attempt 1 to let other Assembly-CSharp scripts use widget types). Switching back to `autoReferenced: false` would require auditing every Assembly-CSharp script that currently uses `Golfin.Gameplay.UI.*` types to add explicit asmdef refs — out of scope.

**Workaround pattern:** when a widget in `Golfin.Gameplay.UI` needs Assembly-CSharp manager state, use a two-piece static-bus + populator pattern:

1. **Static context class** in `Golfin.Gameplay.UI.HUD` namespace (the asmdef side). Holds the data + an `OnChanged` event. Example: `Golfin.Gameplay.UI.HUD.HoleContext` (8.3), `Golfin.Gameplay.UI.HUD.PlayerContext` (8.3 redo).
2. **Populator MonoBehaviour** in `Assets/Scripts/UI/HUD/` or any other Assembly-CSharp folder. Subscribes to manager events, pulls state, writes to the static, raises `OnChanged`. Example: `Golfin.UI.HUD.PlayerContextPopulator` reads `CharacterManager.Instance` + `CharacterDatabaseCSV.Instance` and writes to `PlayerContext`.
3. **Widget** in `Golfin.Gameplay.UI` reads from the static context class only — never references Assembly-CSharp types directly. Subscribes to `OnChanged`.

The populator MonoBehaviour must be added to a scene GameObject that runs alongside the Assembly-CSharp manager (e.g. the same scene root that holds `CharacterManager`). If the manager isn't in the scene, the populator's `OnEnable` no-ops and the widget shows static-class defaults — acceptable for editor-only contexts like LabScaffold.

Use this pattern for any future widget that needs to reach across the asmdef boundary into Assembly-CSharp. *(See §10 — Editor Tooling for the `FakeStateLock` + `CaptureHelper` fake-state injection pattern that builds on top of these contexts.)*

---

## 3 — Singletons & Public APIs

### PlayerContext + PlayerContextPopulator pattern (asmdef workaround)

When a widget in `Golfin.Gameplay.UI` needs CharacterManager state but cannot reference `Assembly-CSharp` directly (because `autoReferenced: true` would create a cycle), use this two-piece pattern:

- **PlayerContext** (`Golfin.Gameplay.UI.HUD` namespace, file: `Assets/Scripts/Gameplay/UI/ShotUI/HUD/PlayerContext.cs`) — static class that holds `DisplayName`, `Level`, `Portrait` + `OnChanged` event + `Raise()` + `Reset()`. Lives in the asmdef side.
- **PlayerContextPopulator** (`Golfin.UI.HUD` namespace, file: `Assets/Scripts/UI/HUD/PlayerContextPopulator.cs`) — MonoBehaviour in the `Assembly-CSharp` bucket. Subscribes to `CharacterManager.OnCharacterSelected`, pulls `CharacterDatabaseCSV.GetCharacter(id).characterName` / `.portraitSprite` + `GetPlayerCharacter(id).currentLevel`, writes to `PlayerContext`, raises `OnChanged`.
- **Widget** (`PlayerCardWidget`) subscribes to `PlayerContext.OnChanged` only — never touches Assembly-CSharp types.

The same pattern applies to any future widget in `Golfin.Gameplay.UI` that needs Assembly-CSharp manager state. `HoleContext` (for hole metadata) uses a simpler variant where `PhysicsLabController` (in the Viewer asmdef) populates the static directly via reflection. *(For editor-time fake population — driving these contexts without entering playmode and without the populator overwriting the fakes — see §10 — Editor Tooling.)*

**Asmdef note:** `Golfin.Gameplay.UI.asmdef` is `autoReferenced: true`. Adding `Assembly-CSharp` to its references array creates a build-order cycle (Unity's Bee system compiles named asmdefs before Assembly-CSharp). Switching back to `autoReferenced: false` would require auditing every Assembly-CSharp file that uses Gameplay.UI types — out of scope. Stay with `autoReferenced: true` + the static-bus pattern.

All managers expose a `static Instance` and a `DontDestroyOnLoad` lifecycle. They live on a "Managers" GameObject in the boot scene (typically `ShellScene.unity`).

### CharacterManager — `Golfin.Roster`
File: `Assets/Scripts/CharacterManager.cs`

```csharp
public class CharacterManager : MonoBehaviour
{
    public static CharacterManager Instance { get; private set; }

    public string GetSelectedCharacterId();           // "" if none
    public PlayerCharacterData? GetPlayerCharacter(string id);  // alias of GetCharacterData
    public CharacterData?       GetCharacter(string id);        // alias of GetCharacterTemplate (SO-based)
    public List<PlayerCharacterData> GetAllOwnedCharacters();
    public int GetMaxLevel(string id);
    public int GetLevelUpCost(string id);
    public int LevelUp(string id);                     // returns SP earned, 0 on fail
    public void SelectCharacter(string id);
    public void RefreshStatValues(string id);

    public event Action<string>? OnCharacterLeveledUp;
    public event Action<string>? OnCharacterSelected;
    public event Action?         OnRosterChanged;
}
```

**`PlayerCharacterData`** (`Golfin.Roster`, `Assets/Scripts/UI/Roster/Data/PlayerCharacterData.cs`) — INSTANCE data, per owned character:
- `string characterId`
- `int currentLevel` (default 10 for Common at start; rarity-driven)
- `int totalSPEarned`, `int spentStrength/ClubControl/Recovery/Stamina`
- `int currentStrength/ClubControl/Recovery/Stamina` (base + spent, capped)
- `bool isSelected`, `bool isOwned`, `DateTime acquiredDate`
- `float currentStaminaEnergy / maxStaminaEnergy` (NonSerialized; runtime energy bar)
- **Does NOT have:** name, lastName, portrait, rarity. Those live on the template.

### CharacterDatabaseCSV — `Golfin.Roster`
File: `Assets/Scripts/UI/Roster/Managers/CharacterDatabaseCSV.cs`

```csharp
public class CharacterDatabaseCSV : MonoBehaviour
{
    public static CharacterDatabaseCSV Instance { get; private set; }

    public CharacterDataRuntime?       GetCharacter(string id);
    public List<CharacterDataRuntime>  GetAllCharacters();
}
```

**`CharacterDataRuntime`** — TEMPLATE data, lightweight CSV-backed alternative to the SO `CharacterData`:
- `string characterId`, `characterName`, `characterLastName`, `bio`
- `CharacterRarity rarity`
- `int baseStrength / baseClubControl / baseRecovery / baseStamina`
- `int maxLevel` (default 199)
- `string portraitSpriteName`, `Sprite? portraitSprite` ← **already loaded from `Resources/Portraits/Thumbnails/{name}`** at CSV parse time
- `string portraitFullSpriteName`, `Sprite? portraitFullSprite` ← from `Resources/Portraits/FullBody/`
- `Color GetRarityColor()`, `string GetRarityLabel()` (delegates to `RarityHelper`)
- `string GetDisplayName()` → `"FIRSTNAME\nLASTNAME"` uppercase

**Canonical lookup pattern for "current player's display info":**
```csharp
var id = CharacterManager.Instance.GetSelectedCharacterId();
var rt = CharacterDatabaseCSV.Instance?.GetCharacter(id);
var pc = CharacterManager.Instance.GetPlayerCharacter(id);
// rt.characterName, rt.rarity, rt.portraitSprite, pc.currentLevel
```

### BagManager — *no namespace* (global)
File: `Assets/Scripts/BagManager.cs`

```csharp
public class BagManager : MonoBehaviour
{
    public static BagManager Instance { get; private set; }
    public static int MAX_BAGS;                          // CSV-driven, fallback 10
    public const int  MAX_CLUBS_PER_BAG = 8;             // ← 8, not 14
    public int        EquippedBagSlot { get; }           // 1-based, 0=none

    public bool IsBagUnlocked(int bagSlot);
    public bool IsBagFull(int bagSlot);
    public int  GetClubCountInBag(int bagSlot);
    public List<PlayerClubData> GetClubsInBag(int bagSlot);
    public int  GetUnlockedBagCount();

    public bool AssignClubToBag(string clubId, int bagSlot);
    public void RemoveClubFromBag(string clubId);
    public void EquipBag(int bagSlot);
    public void UnlockNextBag();

    public event Action<int>? OnBagChanged;          // arg = bagSlot that changed
    public event Action<int>? OnEquippedBagChanged;  // arg = new equippedBagSlot
}
```

Equipped bag's clubs: `BagManager.Instance.GetClubsInBag(BagManager.Instance.EquippedBagSlot)`.

### Other singletons (NEEDS VERIFICATION — read on first use)
- **ClubManager** — `Assets/Scripts/ClubManager.cs`. `Instance`. Owns `PlayerClubData` list (source of truth for `equippedBagSlot`). API: `GetAllOwnedClubs()`, `GetClubData(clubId)`, `EquipClub(clubId, bagSlot)`. **Full API: NEEDS VERIFICATION when 8.5 spec is written.**
- **BallManager** — `Assets/Scripts/BallManager.cs`. **Full API: NEEDS VERIFICATION when 8.5/8.6 spec is written.**
- **ItemManager** — `Assets/Scripts/ItemManager.cs`. **Full API: NEEDS VERIFICATION.**
- **RewardPointsManager** — used by `CharacterManager.LevelUp` via `Instance.CanAfford(cost)` / `SpendPoints(cost)`. Location + namespace: **NEEDS VERIFICATION.**
- **AudioManager** — `Assets/Scripts/Audio/AudioManager.cs`. **NEEDS VERIFICATION.**
- **ScreenManager** — `Assets/Scripts/UI/ScreenManager.cs`. Drives Logo→Splash→Loading→Home→Roster screen flow. **API: NEEDS VERIFICATION when needed.**

---

## 4 — Hole-Loading Flow

### Editor-time picker
1. User picks a hole in `PhysicsLabHolePicker` (`Assets/Scripts/Editor/Physics/`) EditorWindow.
2. Editor additively loads `Hole_XX_Geo.unity` from `Assets/Golf/Courses/lomond-country-club/Generated/`.
3. `LabHoleBinder` (`Golfin.Physics.Viewer`, **editor-only via `#if UNITY_EDITOR`**) hears `EditorSceneManager.sceneOpened` and calls `_controller.OnHoleLoaded(scene.name)`.

### Runtime hole load
- **Does not exist yet.** Phase C (menu→gameplay integration) will add a `GameplayScaffold` scene + runtime hole picker. Until then: hole loading is editor-only.

### `PhysicsLabController.OnHoleLoaded(string sceneName)` does today
File: `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs`

- Scans the loaded scene's roots for `SurfaceMarker` MonoBehaviours (uses reflection — `SurfaceMarker` lives in `Assembly-CSharp` originally, now in `Golfin.Physics.Runtime`).
- Builds tee/green/bunker/fairway/water lists.
- Computes green centroid → `_loadedHoleGreenCentroid` (private).
- Finds `TeeMarker_regular_*` GOs by name → averages position → `_runtimeTeeAnchor` → `_ballSpawnPoint`.
- Builds `PlacementEntries` for the lab UI placement dropdown.
- Copies hole scene's lighting (skybox, ambient, fog, reflection) into `LabScaffold`.
- Calls `SetupAtTee()`.

**What it does NOT do (gaps for HUD widgets):**
- Does NOT read `HoleMetadata` MonoBehaviour (par, holeNumber, championshipYards).
- Does NOT publish a public event when hole changes. `OnPlacementEntriesChanged` exists but doesn't carry hole identity.
- Does NOT expose pin world position. (No `Pin_*` GO confirmed in hole scenes — green centroid is the working proxy. **NEEDS VERIFICATION**: scan a hole scene for any pin/cup GO naming convention.)

**Required additions for HUD widgets** (Phase 8.3 / 8.4):
- Inside `OnHoleLoaded`, after the surface scan: find `HoleMetadata` MonoBehaviour on a scene root, populate a new `HoleContext` static, and fire a new `event Action<HoleMetadata> OnHoleMetadataChanged`. Spec'd in Phase 8.3.

### `HoleMetadata` MonoBehaviour
File: `Assets/Scripts/HoleMetadata.cs`. Namespace: `Golfin.CourseImport`. Sits on the root of every `Hole_XX_Geo.unity` scene.

```csharp
public class HoleMetadata : MonoBehaviour
{
    public string courseId;
    public int    holeNumber;
    public int    par;
    public int    strokeIndex;
    public int    championshipYards;
    public string reviewStatus;
    public string importType;         // "Lite" | "LiteFlat" | "Geo" | "GeoFlat"
}
```

**No tee name field.** Tee selection is not currently part of the hole metadata. Hardcode `"REGULAR"` in HUD until tee picker lands (Phase C+).

### `LabHoleBinder`
- Editor-only (entire body wrapped in `#if UNITY_EDITOR`).
- Pure plumbing — no public API for runtime consumers.
- Note this in any spec that says "wire X via LabHoleBinder" — runtime consumers must subscribe to `PhysicsLabController` events instead.

---

## 5 — Asset Locations

### Resources (loadable via `Resources.Load<Sprite>(path)` — NO file extension)
| Path | Contents | Used by |
|---|---|---|
| `Resources/Portraits/Thumbnails/{Name}.png` | 12 character thumbnails (Camila, Ean, Elizabeth, Freda, Guillermo, James, Johan, Mike, Olivia, Richard, Roshana, Shae). | CSV pipeline auto-loads into `CharacterDataRuntime.portraitSprite`. |
| `Resources/Portraits/Mini/{Name}.png` | 12 mini portraits (subtly different roster — has Sean, no Ean). | Manual `Resources.Load` if needed. |
| `Resources/Portraits/FullBody/{Name}.png` | Full-body portraits. | CSV pipeline → `CharacterDataRuntime.portraitFullSprite`. |
| `Resources/Rarities/{Common,Uncommon,Rare,Mythic,Legendary,Supreme,Mask}.png` | Rarity tiles. | Behind portraits / chip backgrounds. |
| `Resources/Clubs/Controls/S_Controls_{Type}_{Brand}.png` | Club handle sprites for shot UI. | `ClubHandleSpriteBinder` (Phase 8.2.5). |
| `Resources/Clubs/Portraits/S_Menu_{Type}_{Brand}.png` | Club menu portraits. | Phase 8.5 Club button. |
| `Resources/Balls/Thumbnails/S_Controls_Ball_{n}.png` (also `Golfin.png`, `PuttAce.png` short variants) | Ball thumbnails. | Phase 8.5/8.6/8.7 ball widgets. |
| `Resources/Bags/...` | Bag art. | Bags screen. |
| `Resources/Items/...` | Item art. | Items screen. |
| `Resources/Characters/...` | (legacy SO databases?) | **NEEDS VERIFICATION.** |
| `Resources/Gameplay/...` | (config CSVs?) | **NEEDS VERIFICATION — likely controls.csv etc.** |
| `Resources/HoleData/Hole_XX/zones.json` + `heightmap.bytes` | Baked physics data per hole (post-Phase F pivot). | `BallSimulation`. |
| `Resources/Physics/...` | Physics configs (`AeroConfig`, `WindConfig`, `SurfaceConfig`, `PuttConfig` CSVs). | `PhysicsLabController.EnsureConfigsLoaded`. |

### Art (NOT in Resources — must be inspector-assigned)
| Path | Contents | How to consume |
|---|---|---|
| `Assets/Art/In-Game UI/*.png` | `Aiming Cone.png`, `Button - All.png`, `Icon - DrawFade/Flag/Settings/Spin/Straight.png`, `Indicator - Info/Power/Trail/Wind-Hole.png`. | Inspector-assigned `Sprite` field on the relevant widget MonoBehaviour (same pattern as `PowerGaugeWidget._backgroundSprite`). **Note (2026-04-28):** `Indicator - Wind-Hole.png` ships with `spriteBorder: {0,0,0,0}` — it is NOT 9-slice ready out of the box. Any widget using it as `Image Type: Sliced` requires Cesar to set borders manually via Sprite Editor first (suggested L=12, R=12, T=8, B=8). Otherwise sliced rendering distorts the sprite. |
| `Assets/Art/In-Game UI/HoleMaps/Lomond - Hole {1..18}.png` | 18 hole map thumbnails. | Inspector-assigned `Sprite[18]` array on `HoleCardWidget`, OR move folder into `Resources/HoleMaps/` for path-based load. **Decision in Phase 8.3 spec: inspector-assigned array** (avoids invalidating other folder consumers + matches existing widget pattern). |
| `Assets/Art/In-Game UI/HoleLayouts/...` | (sibling to HoleMaps) | **NEEDS VERIFICATION — purpose unclear.** |
| `Assets/Art/3D/Props/TeeMarkers/...` | FBX tee markers. | TreePlacer / hole importer. |

### Fonts
- TMP font asset: `Assets/Fonts/Rubik-VariableFont_wght SDF.asset` ← canonical TMP font for shot UI. NOT `Rubik-SemiBold SDF` (that older asset path was used by accident in 8.2 power gauge; Cesar fixed manually).
- Figma → Unity TMP scaling: **Figma font size ÷ 1.4 = Unity size.**

---

## 6 — Rarity System

Enum: `CharacterRarity { Common, Uncommon, Rare, Mythic, Legendary, Supreme }` (6 tiers).

Helpers:
- `RarityHelper.GetRarityColor(rarity) → Color`
- `RarityHelper.GetRarityLabel(rarity) → string`
- `RarityStatCaps.GetStatCaps(rarity) → (strengthCap, clubControlCap, recoveryCap, staminaCap)`

Starting level by rarity (for newly owned chars): Common 10 / Uncommon 40 / Rare 80 / Mythic 120 / Legendary 160 / Supreme 200.
Max level by rarity: Common 39 / Uncommon 79 / Rare 119 / Mythic 159 / Legendary 199 / Supreme 239.

Sprites: `Resources/Rarities/{Rarity}.png` + `Mask.png`.

---

## 7 — Phase 8 ShotUI Hierarchy (Existing)

`LabScaffold.unity` → `ShotUI_Canvas` (CanvasScaler **1170×2532 reference, Match=0** as of 2026-04-29) → children:
- `ConeMesh` (RectTransform anchored at canvas center base; `ConeMeshGraphic` + `TimingSlabGraphic`)
- `ClubHandle` (child of ConeMesh; `Image` + `ClubHandleDragger` + `ClubHandleSpriteBinder`)
- `PowerGaugeWidget` (top-right, anchored `(-180, -460)`; size 200×200; children: `Background` Image + `GaugeArc` PowerGaugeGraphic + `PctText` + `YardsText` TMP)

**To be added in Phase 8.3:**
- `PlayerCard` (top-left)
- `HoleCard` (top-right of `SettingsButton`)
- `SettingsButton` (top-right corner)

**Anchor convention:** all widgets anchored to corners of a 1170×2532 reference canvas (Figma 1:1). Position offsets in canvas units = Figma px directly.

---

## 8 — Open NEEDS-VERIFICATION List

These are markers Architect/Code should fill in next time the relevant area is touched. Don't dive on them just-in-case; fill them when their answer is needed.

- [ ] Asmdef ref lists for `Golfin.Gameplay.Input`, `Golfin.Gameplay.Defaults`, `Golfin.Gameplay.Tests`, `Golfin.Physics` (core).
- [ ] Whether the SO-based `CharacterData` template is still loaded anywhere, or if `CharacterDatabaseCSV` is the only source of truth in production. (CharacterManager has both code paths; reading whichever the boot scene wires.)
- [ ] `ClubManager` full public API + namespace.
- [ ] `BallManager` full public API + namespace.
- [ ] `ItemManager` full public API + namespace.
- [ ] `RewardPointsManager` location + namespace + full public API.
- [ ] `AudioManager` full public API (which sounds it owns; how to play one-shot).
- [ ] `ScreenManager` API + screen flow contract.
- [ ] Pin / cup GO naming convention in `Hole_XX_Geo.unity` scenes — does any GO carry pin world position, or is "green centroid as proxy" the only path?
- [ ] `Resources/Characters/` and `Resources/Gameplay/` contents.
- [ ] `Assets/Art/In-Game UI/HoleLayouts/` purpose (sibling of HoleMaps).
- [ ] Whether `WindContext` / `HoleContext` / `GameSession` static holders exist anywhere. (Spec'd as new in Phase 8.3 — confirm none collide.)

---

## 9 — Working with Figma references

### Standing rule: Multi-Agent Pipeline (set 2026-04-28)

**All UI tasks go through the subagent pipeline at `.claude/agents/`.** Architect (Opus) writes specs → Implementer (Sonnet) builds → Self-Reviewer (Opus) catches false PASSes → Architect (Opus) does final review → Cesar approves. Hooks at `.claude/hooks/` route the chain automatically and notify Cesar via desktop toast when his attention is needed. Full details in `CLAUDE.md` § Multi-Agent Workflow.

**Per-task folder convention:** `Docs/Specs/Active/<task_slug>/` containing `SPEC.md`, `STATUS.md`, `IMPLEMENTER_REPORT.md`, `SELF_REVIEW.md`, `ARCHITECT_REVIEW.md`, `screenshots/` (Code's verification captures), `reference/` (Figma source renders shipped with the spec). Template at `Docs/Specs/Active/_TEMPLATE/`.

The legacy `Docs/TellCode.md` workflow is deprecated for new active tasks. Don't write new active work there.

### Standing rule: extract from STRUCTURAL DATA, not screenshots (set 2026-06-04)

**Before writing OR reviewing any UI fidelity spec, pull the component's structural data and build the per-element token table from it — never from rendered screenshots or recall.** Screenshots show symptoms; they do not show what is encoded in structure.

Two calls, in order, on the target component (frame or instance):
1. **`figma:get_metadata`** — node tree, names, x/y/w/h, and **`hidden="true"` flags**. This reveals: nested containers, per-state node presence, hidden elements (e.g. `Arrow Container hidden=true`), and exact dimensions.
2. **`figma:get_design_context`** — the token values: font family/**weight**/size/lineHeight, fills, **gradients**, **border color + width**, corner radius, separators, padding/gap.

Then write each value as a LITERAL into the spec. Code transcribes; it does not re-query or interpret.

**Lesson — mode_select_system iter-5 review (2026-06-04):** reviewing from the rendered screenshots + memory missed ~10 real issues, ALL of which were sitting in the metadata: active-vs-inactive encoded as **border color** (white vs `#3E7CA8`) + **title color** (gold `#EEDC9A` vs silver gradient); the "back panel" being the **`Cards Container` frame's own fill** (not a separate rect); **per-surface card widths** (home 764/677 vs full-screen 978); the carousel arrows being **`hidden=true`** deliberately; **separator counts** per state (3 on expanded, 1 on collapsed); the fee/reward **centered-cluster** layout vs corner-spread. None of this is legible from a screenshot. Pulling `get_metadata` + `get_design_context` surfaced every one. Do this FIRST, every time.

### Standing rule: ship the Figma RENDERS with the spec (set 2026-06-24)

Tokens tell Code the numbers; they don't show the composition. **Every UI spec must also ship the actual Figma render of each referenced screen/frame/state as a PNG in the task folder, linked inline in SPEC.md next to the matching token table.** The more the Implementer can SEE, the fewer layout misreads — Code should never have to imagine the screen from numbers alone.

- Export each canonical frame + every distinct state (e.g. hole-card Finished/Next/Locked, Top-3 podium, sticky "you" row) via `figma:get_screenshot` on the node (or `figma:download_assets` for a full render), then save into `Docs/Specs/Active/<task>/reference/<screen_or_state>.png`.
- Link them inline in SPEC.md beside the relevant section: `![Tournament Leaderboard](reference/leaderboard.png)`.
- These are the **design source render** — distinct from `screenshots/` (Code's verification output). Keep both.
- This is an ADDITION, never a replacement: still extract literal tokens per the rule above. Image + numbers, not image instead of numbers.

### Standing rule: Figma source-of-truth (set 2026-04-28)

**Figma is the UI source-of-truth.** Reference PNGs in `Docs/Reference/` are companions for visual comparison only — Code uses them for side-by-side diffs during impl, but the canonical numbers (dimensions, fonts, colors, positions) come from Figma via the MCP, not from eyeballing the PNG.

**Architect MUST confirm with Cesar BEFORE writing any UI spec:**
1. Which Figma **page** is the source-of-truth for this task?
2. Which **frame** within that page?
3. Which parts of the visible content are **placeholder vs canonical** (e.g. "LADY'S" tee was placeholder for 8.3 — real default is REGULAR)?

The Figma file is not curated yet (Cesar was working solo and didn't need it tidy). Don't guess which frame is current — ask. After Cesar confirms the page+frame+placeholder list, Architect extracts numbers via Figma MCP and writes the spec with those numbers baked in.

**Don't do this:**
- Pick a frame because the name looks right ("In-Game - Shot Tests 9" — there are 9+ versions, only one is current).
- Assume text values in the frame are real (`"Lv 13"`, `"TURN 5"`, `"LADY'S"` were all mockup-only in 8.3).
- Assume the frame's structure is final — Cesar may have a newer iteration on a WIP page.

**Do this:**
- Ask: "Which page/frame should I use as source-of-truth for [task]? And what's placeholder vs canonical in it?"
- Wait for confirmation.
- Then extract.

### How to consume Figma

Two ways:

1. **Live Figma MCP (preferred).** Authoritative file as of 2026-04-28: **Cesar's personal file**, key `5gEAHjl6xAtW8iYY7NMvWd`, file name `Golfin Game Redux`. 42 pages including `In-game`, `Components`, `Master`, plus newer pages like `WIP- Hole Selection Screen`, `Export in-game`, `Export - Matchmaking`, `Export - Rankings`, `Export - Balls`, `Export - Bags`, `Export - Items`. Use `figma:use_figma` with `figma.getNodeByIdAsync(...)` walks. Now on a paid plan — rate limits should not be an issue.
   - **Older file `hXFadl4O6HGKWakiEKgZbW`** is the previous shared-team file. Still readable but missing newer pages. Treat as a freeze point, not the source-of-truth.
   - **Canonical 8.3 reference frame:** page `In-game`, frame `In-Game - Shot Tests 9`, id `4065:15675`. Maps to `Docs/Reference/In-game UI/Initial State.png`.
2. **Local `.fig` file fallback** — `Docs/Reference/In-game UI/In-game GUI.fig`. Parseable in principle (ZIP archive containing Figma's Kiwi-encoded binary), but parsing it requires extra tooling (unzip + Kiwi schema decoder, or OSS extractors). Use as a frozen historical snapshot only.

**Lesson learned 2026-04-28:** Architect dismissed the `.fig` file as "opaque" without inspecting it. It's actually a ZIP archive with `PK` magic bytes — readable in principle. **Future rule: try to open unfamiliar files before assuming they're inaccessible.** Worst case, one wasted tool call.

**Cheap insurance against future unavailability:** when uploading a new reference PNG, also drop a 5-line text dump alongside it with frame name, canvas dimensions, and key node sizes/positions/fonts. Architect can always read text regardless of MCP availability or .fig parsing tooling.

**Figma → Unity TMP scaling:** Unity TMP size = Figma size ÷ 1.4. Always read the Figma value directly when speccing fonts — do not guess.

### Calibrated reference numbers (8.3 ground truth, from `In-Game - Shot Tests 9`)

- **Canvas:** 1170×2532 (NOT 1080×1920 — the old spec was wrong).
- **Top bar `Frame 2`:** at (48, 24), 1074×110.
- **Settings button:** abs position (978, 24), size 86×86. White circle 86×86 (`#FDFFFE`, render as pure white) + navy gear glyph 63×65 at offset (12, 11) inside the circle. Gear color: navy `#001E39`. Right margin from screen edge: **106px** (NOT 48 — different from cards).
- **Cards row `Content Container`:** starts at (48, 158), height 1396 (full vertical), but `First Row` is the top 180px section.
- **Player card:** abs (48, 158), 478×180. Inside: portrait 180×180 cornerRadius 8 at (0, 0); chip stack 298×160 at (180, 10).
- **Hole card:** abs right edge 1122 (= 1170 - 48), 478×180. Inside: chip stack 298×160 at (0, 10); hole map 180×180 cornerRadius 8 at (298, 0). Mirror layout of player card.
- **Both cards 48px from screen edge.** Symmetric.
- **Chip:** 298×48, navy fill `#001E39`, NO corner radius, NO sprite (flat rectangle). Three chips at y=0, 56, 112 within chip stack (56px row pitch = 48px chip + 8px gap).
- **Chip text:** Rubik Medium, fontSize 33 (Unity TMP size 23), white, **right-aligned** on BOTH cards. Text frame inset 10px from chip top.
- **Rarity background EXISTS** in the Figma `In-game Portrait` instance (subtle layer behind character art). Keep v1 simplification (omit); flag as polish follow-up.
- **`Indicator - Wind-Hole.png` is NOT used** for chip backgrounds in the Figma design — the chips are flat navy rects. The 9-slice border concern doesn't apply to chips. (May still apply to the Wind/Hole indicators in 8.4 — verify when speccing.)

### Color tokens (extracted 2026-04-28)

- **Navy (chip fill, gear, dark accents):** `#001E39` (`r:0, g:0.118, b:0.224`).
- **Near-white (settings circle):** `r:0.992, g:1, b:0.996` — use pure white `#FFFFFF` in Unity.
- **White (chip text):** `#FFFFFF`.

---

## 10 — Editor Tooling

Editor-only utilities that exist purely to support development workflow. Not part of the runtime, not shipped to device.

### CaptureHelper — synchronous Game View screenshots + fake-state presets

**File:** `Assets/Scripts/Editor/CaptureHelper.cs` (`Golfin.EditorTools` namespace, `Assembly-CSharp-Editor`).
**Lock flag:** `Assets/Scripts/Gameplay/UI/ShotUI/HUD/FakeStateLock.cs` (`Golfin.Gameplay.UI.HUD.FakeStateLock`, runtime asmdef so populators can reference it).

**Why this exists.** Multiple Phase 8 tasks failed verification because of screenshot-timing issues: `ScreenCapture.CaptureScreenshot(path)` is async and silently no-ops while the editor is paused (the render loop stops emitting `WaitForEndOfFrame` during pause); pausing-then-capturing yields nothing; capturing-then-pausing risks losing the state of interest. CaptureHelper replaces that path with a synchronous reflection-based grab of the GameView's internal `RenderTexture`, plus fake-state preset menu items so UI verification doesn't require playmode at all.

**The two problems it solves:**

1. **Capture timing.** `CaptureHelper.SnapGameView()` (menu: `GOLFIN > Capture > Snap Game View`, shortcut Ctrl+Shift+Alt+S) reads the GameView RenderTexture directly via reflection (`m_RenderTexture` / `m_TargetTexture` / `m_RenderTarget`) and writes a Y-flipped PNG to `Docs/Diagnostics/_capture/`. Synchronous, works in EditMode, works while paused, works during running playmode. Falls back to `ScreenCapture.CaptureScreenshotAsTexture()` with a warning if reflection fails (future Unity field renames). For mid-animation captures from a coroutine, use `CaptureHelper.SnapAtEndOfFrameAndPause(label)` — captures FIRST, pauses AFTER, never the other way.

2. **Fake state injection.** Menu items `GOLFIN > Capture > Fake State - <preset>` populate the static-bus contexts (PlayerContext, HoleContext, WindContext, BallContext, ClubContext, ShotModeContext, SpinContext, GameSession) with sensible scenario data so widgets render without any game loop running. Current presets:
   - `Reset All` — clears all contexts to defaults; clears `FakeStateLock`.
   - `Mid Aim (Camila, Lomond H1, Driver, GOLFIN ball)` — full populated tee shot.
   - `Putt (Olivia, Lomond H7, Putter)` — putt scenario, alternate character.
   - `Strong Wind (extreme indicator test)` — Wind context only.
   - `Fake State Lock - ON` / `Fake State Lock - OFF` — manual lock toggle.

### FakeStateLock + populator cooperation

Runtime `*Populator` MonoBehaviours (PlayerContextPopulator, BallContextPopulator, ClubContextPopulator) subscribe to manager events and call `Refresh()` to rewrite the contexts from authoritative sources. In a fake-state session, this would stomp the injected values within milliseconds.

The `FakeStateLock.IsLocked` flag (default `false`) gates this. Each preset sets `FakeStateLock.IsLocked = true` before populating. Each populator's `Refresh()` early-returns if the lock is set:

```csharp
void Refresh()
{
    if (FakeStateLock.IsLocked) return;
    // ...normal sync logic...
}
```

Production playmode is unaffected — the lock is only ever set by editor menu actions. Restart playmode (or hit `Reset All`) to clear it.

### Maintenance protocol — STANDING RULE

When any future task introduces a new static-bus context under `Assets/Scripts/Gameplay/UI/ShotUI/HUD/` (or any equivalent), that same task MUST:

1. Extend `CaptureHelper.FakeReset` to call the new context's `Reset()`.
2. Extend `CaptureHelper.FakeMidAim` to set sensible non-default values for the new context.
3. Update `FakeMidAim`'s closing `Debug.Log` line to include the new context's values.
4. Add a dedicated preset if the new context has interesting variation worth isolating (e.g. `Strong Wind` for `WindContext`).
5. **If the new context has a runtime `*Populator` companion**, add `if (FakeStateLock.IsLocked) return;` as the first line of its `Refresh()` so the lock works.

The Architect MUST flag missing fake-state extensions during architect-review when reviewing tasks that add new contexts. Self-reviewer also has a checkpoint for this (see `.claude/agents/golfin-self-reviewer.md` Step 5).

### Output convention

All captures land in `Docs/Diagnostics/_capture/` with timestamped filenames. After capture, copy/rename the relevant ones into the task's `Docs/Specs/Active/<task>/screenshots/` folder. Don't litter `_capture/` with task-specific names — keep it as a scratchpad.

### Banned

- `ScreenCapture.CaptureScreenshot(path)` — async, fails silently when paused, banned project-wide. Only `ScreenCapture.CaptureScreenshotAsTexture()` is acceptable, and only as the internal fallback inside `CaptureHelper`. Code session rules in `CLAUDE.md` § Screenshots enforce this.

### Lesson — `CaptureScreenshotAsTexture` reads OS swap chain, not GameView

In the Unity Editor, `ScreenCapture.CaptureScreenshotAsTexture()` returns the OS display's swap chain frame, NOT the GameView's render target. In editor mode this means it captures Editor chrome or returns black (depending on whether GameView is the active focused window). The reliable path is reflection into `UnityEditor.GameView`'s internal `RenderTexture` field, then `ReadPixels` into a `Texture2D` (with Y-flip — Unity's RT origin is bottom-left). This is documented in `tasks/lessons.md` for future reference.

---

## 11 — Update Log

- **2026-06-24** — Added §9 standing rule "ship the Figma RENDERS with the spec." Every UI spec now also exports each referenced frame/state as a PNG into the task's `reference/` folder (via `get_screenshot`/`download_assets`), linked inline in SPEC.md, in ADDITION to literal tokens. Added `reference/` to the per-task folder convention. Per Cesar: the more the Implementer can see, the better.
- **2026-06-04** — Added §9 standing rule "extract from STRUCTURAL DATA, not screenshots." Before writing/reviewing any UI fidelity spec, pull `get_metadata` + `get_design_context` on the component and build the token table from that. From mode_select_system iter-5 review, where screenshot-eyeballing missed ~10 issues all present in metadata (active/inactive border+title color, container-as-back-panel, per-surface widths, hidden arrow containers, separator counts, centered-cluster layout).

- **2026-04-30** — Added §10 Editor Tooling. Documents `CaptureHelper.cs` (synchronous GameView RT reflection capture, replacing async `ScreenCapture.CaptureScreenshot`), `FakeStateLock` runtime flag, fake-state preset menu items, populator cooperation pattern, and the standing maintenance protocol when adding new static-bus contexts. Cross-referenced from §2 and §3. Renumbered Update Log §10 → §11. Built on the `capture_helper` task (Cesar approved 2026-04-29) plus the populator-lock follow-up (2026-04-30).

- **2026-04-28** — Initial creation during Phase 8.3 handoff prep. Verified CharacterManager / CharacterDatabaseCSV / PlayerCharacterData / CharacterDataRuntime / BagManager / HoleMetadata / LabHoleBinder / PhysicsLabController.OnHoleLoaded APIs and asset locations. Marked the rest NEEDS VERIFICATION.
- **2026-04-28** — Phase 8.3 attempt 1 rejected. Added §1 asmdef workaround pattern (static context + Assembly-CSharp populator) after discovering `autoReferenced: true` blocks the simple Assembly-CSharp ref path. Added §4 9-slice border caveat for `Indicator - Wind-Hole.png` (sprite borders not set in importer; Image Type Sliced will distort without manual fix).
- **2026-04-28** — Added §8 "Working with Figma references" with `.fig` lesson. Architect almost guessed at fonts/dimensions; should always pull live from Figma MCP, fall back to text dump, and only as last resort attempt to parse the `.fig` archive.
- **2026-04-28** — Switched to Cesar's personal Figma file (`5gEAHjl6xAtW8iYY7NMvWd`) on paid plan. Re-extracted full layout numbers for 8.3 cards/settings; updated §8 with calibrated reference. Deprecated the chip-sprite (`Indicator - Wind-Hole.png`) approach for chips — they're flat navy rects in the design. Confirmed font is Rubik Medium 33 (Unity TMP 23), right-aligned on both cards, navy fill `#001E39`.
- **2026-04-28** — Added §8 standing rule: Architect MUST confirm page/frame/placeholder-vs-canonical with Cesar before writing any UI spec. The Figma file is not yet curated; multiple frame versions exist with placeholder content. Don't guess which is current.
- **2026-04-28** — Added Multi-Agent Pipeline standing rule to §8. UI tasks now use the `.claude/agents/` subagent chain with `.claude/hooks/` routing. Per-task folders under `Docs/Specs/Active/<slug>/`. TellCode.md deprecated for new active work. See `CLAUDE.md` § Multi-Agent Workflow for full pipeline.
- **2026-04-29** — Added new §1 (UI Coordinate System) and renumbered downstream sections. Migrated 7 in-scope CanvasScalers from `1080×1920 / Match=0.5` to `1170×2532 / Match=0` across 5 physics-lab scenes. Hypothesis validated via test scene matrix. Updated §7 ShotUI Hierarchy reference resolution. Standing rule: 1 Figma px = 1 Unity unit at design ref.
