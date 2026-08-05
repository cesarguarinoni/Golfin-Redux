# SPEC — safe_area_top_bar (smoke #2)

**Source:** K7 in `Docs/TellCode.md`, AMENDED 2026-08-04 (Architect ruling: Option A approved).
**Bug:** Tickets counter (and top-bar chrome) is eaten by the Dynamic Island on iPhone 14 Pro Max.
**Sequencing:** Runs AFTER K4 `nav_bar_edge_gaps` (commits `49825e867` + `26ceeb051`) — DONE, prerequisite satisfied.

## Approved scope (Option A)
ONE isolated commit: `Assets/Scenes/ShellScene.unity` + `Assets/Scripts/UI/PersistentUIManager.cs`.
Two new serialized refs approved: `topBarContent`, `bottomNavContent`.

## The trap
Inset the CONTENT, not the bar BACKGROUNDS.
- Backgrounds (Image on the `TopBar` / `BottomNavBar` roots) stay FULL-BLEED on the existing roots, under the notch / into the home-indicator zone.
- Content moves into a canvas-level `SafeArea` node (SafeAreaFitter) → `TopBarContent` + `BottomNavContent`.

## Before-state (measured, ShellScene)
Canvas `PersistentUI` (ScreenSpaceOverlay, ConstantPixelSize). Children: `TopBar`(0), `BottomNavBar`(1). Both roots `active=False` at rest — PersistentUIManager toggles them at runtime.
- `TopBar` [Image on root] top strip aMin(0,1) aMax(1,1) offMin(0,-321) offMax(0,0). Content children: RewardPointsBackground, RewardPointsIcon, RewardPointsText, SettingsButton, UsernameText, TicketIcon, TicketCountText, ShopPlusButton(>PlusLabel).
- `BottomNavBar` [Image on root] bottom strip aMin(0,0) aMax(1,0) offMin(0,0) offMax(0,196). Content: NavHome/NavGacha/NavTee/NavInventory/NavCharacters.

## Target-state
```
PersistentUI (Canvas)
├─ TopBar (Image, full-bleed)          ← topBarPanel ref, BG only, roots unchanged
├─ BottomNavBar (Image, full-bleed)    ← bottomNavPanel ref, BG only
└─ SafeArea (stretch 0,0-1,1, offsets 0, SafeAreaFitter)   ← NEW, index 2 (draws above BGs)
   ├─ TopBarContent (top strip: aMin(0,1) aMax(1,1) offMin(0,-321) offMax(0,0))
   │   └─ (all 8 top-bar content objects, moved in as SIBLINGS — cluster kept intact)
   └─ BottomNavContent (bottom strip: aMin(0,0) aMax(1,0) offMin(0,0) offMax(0,196))
       └─ (5 nav buttons, moved in)
```
Content local anchors/offsets preserved → identical layout, shifted down/up by the safe-area inset at runtime. In editor (safe area == full) layout is unchanged.

## Code touchpoints (PersistentUIManager.cs) — FOUR
1. `ShowTopBar(bool)` / `ShowBottomNav(bool)` — toggle BOTH the root panel AND the matching content ref (null-guarded).
2. `SetTopBarChromeVisible(bool)` — retarget child loop from `topBarPanel` → `topBarContent` (fallback topBarPanel if null). `UsernameText` moves into `topBarContent`; skip-by-name carries over.
3. `ApplyDemoTopBarTrim()` — retarget `Find("RewardPointsBackground")` from `topBarPanel` → `topBarContent` (fallback). Else demo build regresses demo_build_slice §3.4 (silent no-op).
4. `EnsureTicketPill()` — NO code change; resolves via `ticketCountText.transform.parent` (== TopBarContent post-move). Survives IFF RewardPointsBackground + TicketIcon + ShopPlusButton + TicketCountText move together as siblings.

Untouched: HideIfScreenBlocked + every serialized Button/Text/Image ref (Unity object refs, not paths).

## Scene rules
Minimal diff, diff YAML before commit, revert unrelated default-override drift. No merge driver (Order 429). Re-parent CHILDREN only; never rename/move the panel roots (topBarPanel/bottomNavPanel refs must survive).

## Verify (Simulator VALID — safe-area class)
Show/hide matrix (every row):
| Screen | Expected |
|---|---|
| Logo / Splash / Loading | NO bar backgrounds AND no chrome |
| Account / login | banner + centered title ONLY (chrome stripped, title visible, inside safe area) |
| Home | full bars, chrome restored |
| In-hole | shell bars fully hidden |
| GOLFIN_DEMO (PointsEnabled=false) | RP chrome hidden (touchpoint 3) |

- Sim iPhone 14: tickets pill fully below notch; NO blank strip between notch and top-bar background; bottom nav icons clear of home-indicator band; backgrounds reach all edges.
- Editor Game view 16:9: layout unchanged (safe area zero → any diff is a regression).
- SCOPE-CHECK only (report, don't fix): in-game HUD (player card / hole info) crowding the notch; build stamp handles its own inset (leave alone).
- Final: Cesar's iPhone 14 Pro Max, one launch.
