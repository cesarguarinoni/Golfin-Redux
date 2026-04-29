# Implementer Report — `8_4_indicators` (Round 3)

> **MANDATORY:** Every checklist item from `SPEC.md` must be marked `PASS` or `FAIL` with a one-sentence justification citing what was measured. A report with unfilled, blank, or hand-wavy checklist items will be auto-rejected by the self-reviewer.

## Implementation summary

Round 3 (SELF_REVIEW_FAIL redo — applying v3 code changes from SPEC.md).

The Round 2 code was confirmed to be the v2 implementation (HoleIndicatorWidget.cs had `_slidingChipRoot`, top-right anchored hierarchy). The v3 changes from SPEC.md were NOT applied in Round 2. This round applies them.

### Changes applied in Round 3

1. **`HoleIndicatorWidget.cs` — replaced with v3** (exact code from SPEC.md §Code changes §1):
   - Removed `_slidingChipRoot` field entirely
   - Root (`_root`) is now the sliding element (top-LEFT anchored)
   - `_dataChip` and `_arrowLine` are static children of `_root`
   - Tail is **always visible** (never SetActive(false))
   - Tail length scales with distance via `_tailMinLength`/`_tailMaxLength`/`_tailDistanceForMaxLength` fields
   - Tail only rotates when flag is off-screen; points straight down when flag is on-screen
   - Widget mutates only `_root.anchoredPosition.x` each LateUpdate

2. **`IndicatorWidgetBuilder.cs` — HoleIndicator section replaced with v3** (exact code from SPEC.md §Code changes §2):
   - `holeRoot`: anchor (0,1), pivot (0,1), sizeDelta (100, 473), anchoredPosition (1022, -362) — TOP-LEFT anchored
   - `dataChip`: anchor (0,1), pivot (0,1), sizeDelta (100, 100), anchoredPosition (0, 0) — child of holeRoot
   - `arrowLineRt`: anchor (0,1), pivot (0.5, 1), sizeDelta (6, 370), anchoredPosition (50, -100) — child of holeRoot (NOT sibling of dataChip)
   - SerializedObject wires `_root`, `_dataChip`, `_arrowLine`, `_distanceText` — NO `_slidingChipRoot` wire (field doesn't exist in v3)

### Blocking issue — Unity compile + builder not triggered

Unity MCP tools are unavailable in this agent session (`mcp__unity__*` tools returned "No such tool available"). The Unity Editor is running (PID 52960, LabScaffold open) but is not in the foreground and its file watcher has not triggered a compile of the changed .cs files. The Unity Editor log has not been updated by any compile event since the files were written.

**What Cesar must do manually before re-running the implementer:**
1. Tab to Unity Editor (this triggers the file watcher to pick up the .cs changes and compile)
2. Wait for compile to complete (blue progress bar disappears)
3. Run menu: `GOLFIN/Build/Build Indicator Widgets (8.4)` — this rebuilds the HoleIndicator hierarchy with the v3 structure
4. Enter Play mode, wait 5 seconds
5. Run menu: `GOLFIN > Screenshot > Capture Game View`
6. Run: `python .claude/hooks/capture_screenshot.py 8_4_indicators` to copy screenshot to task folder
7. Re-run implementer subagent on `8_4_indicators`

## Files modified or created

| Path | Change |
|---|---|
| `Assets/Scripts/Gameplay/UI/ShotUI/HoleIndicatorWidget.cs` | Replaced with v3 (no `_slidingChipRoot`, top-left sliding root, tail always visible, tail length scaling) |
| `Assets/Scripts/Editor/CanvasScalerMigration/IndicatorWidgetBuilder.cs` | HoleIndicator section replaced with v3 (top-left anchored root, ArrowLine as child not sibling) |

## Screenshot

- **NOTE:** No new screenshot was captured. The MCP tools required to enter play mode and capture a screenshot are unavailable in this agent session.
- **Existing screenshot from Round 2:** `screenshots/screenshot_2026-04-29_11-23-45.png` — this reflects the v2 hierarchy, NOT v3. It should not be used for v3 verification.

## Acceptance checklist

### v3 Behavior (from SPEC.md)

| Item | Result | Justification |
|---|---|---|
| Tail is always visible (never disappears) | FAIL | Cannot verify — no screenshot. Code change confirmed: `_arrowLine.gameObject.SetActive(true)` always called; old `tailVisible=false` branch removed entirely. |
| Tail positioned directly below chip, regardless of chip position | FAIL | Cannot verify — no screenshot. Confirmed in code and builder: ArrowLine is child of _root at anchoredPosition (50, -100), so it always moves with _root. |
| Flag on-screen: chip slides horizontally to flag's screen X | FAIL | Cannot verify — no playmode screenshot. Code confirmed: `targetX = pinCanvasX - chipWidth * 0.5f`. |
| Flag on-screen: chip clamps at edge with 48px padding | FAIL | Cannot verify — no playmode screenshot. Code confirmed: `Mathf.Clamp(targetX, _edgePaddingPx, canvasWidth - chipWidth - _edgePaddingPx)`. |
| Flag on-screen: tail points straight down (no rotation) | FAIL | Cannot verify — no playmode screenshot. Code confirmed: `flagOnScreen` branch sets `_arrowLine.localRotation = Quaternion.identity`. |
| Flag off-screen: chip locks at closer edge with 48px padding | FAIL | Cannot verify — no playmode screenshot. Code confirmed: flagOffLeft → `_edgePaddingPx`, flagOffRight → `canvasWidth - chipWidth - _edgePaddingPx`. |
| Flag off-screen: tail rotates so fade end points toward flag | FAIL | Cannot verify — no playmode screenshot. Code confirmed: Atan2+90f rotation applied in off-screen branch. |
| Tail length grows with ball-to-flag distance | FAIL | Cannot verify — no playmode screenshot. Code confirmed: `Mathf.Lerp(_tailMinLength, _tailMaxLength, Mathf.Clamp01(meters / _tailDistanceForMaxLength))`. |

### Carried from v2 (code-level only — no re-run possible)

| Item | Result | Justification |
|---|---|---|
| [CARRIED] Wind chevron PNG sprite renders | FAIL | Cannot verify — no new screenshot. Round 2 screenshot shows it working but that used v2 hierarchy. |
| [CARRIED] Wind speed text shows CSV value | FAIL | Cannot verify — no new screenshot. Round 2 log confirmed working. |
| [CARRIED] HoleDatabaseLoader in scene with CSV wired | FAIL | Cannot verify — builder not run, scene not rebuilt. Round 2 scene YAML had it, but v3 builder will rebuild scene. |
| [CARRIED] Flag GO found via prefix match (Flag_1) | FAIL | Cannot verify — no playmode. Code unchanged from Round 2 which confirmed working. |
| [CARRIED] Distance text shows real yards from ball to flag | FAIL | Cannot verify — no playmode screenshot. |

### Code integrity (verifiable without Unity)

| Item | Result | Justification |
|---|---|---|
| `_slidingChipRoot` field removed from HoleIndicatorWidget.cs | PASS | Grep confirmed: no matches for `_slidingChipRoot` in HoleIndicatorWidget.cs |
| `_slidingChipRoot` removed from IndicatorWidgetBuilder.cs | PASS | Grep confirmed: no matches for `_slidingChipRoot` in IndicatorWidgetBuilder.cs |
| v3 HoleIndicatorWidget.cs matches spec exactly | PASS | File content matches SPEC.md §Code changes §1 exactly (verified by reading file after write) |
| v3 IndicatorWidgetBuilder.cs HoleIndicator section matches spec | PASS | Replaced section matches SPEC.md §Code changes §2 exactly; SetAnchorTopRight calls removed, direct assignment used per spec |
| No `using` directives missing | PASS | HoleIndicatorWidget.cs uses: TMPro, UnityEngine, UnityEngine.UI, Golfin.Gameplay.UI.HUD — all correct for types used |
| SerializedObject wires correct fields (_root, _dataChip, _arrowLine, _distanceText) | PASS | Builder code confirmed: exactly these 4 fields wired, no `_slidingChipRoot` wire attempted |

### Misc

| Item | Result | Justification |
|---|---|---|
| Unity Console has no errors related to this task | FAIL | Cannot verify — Unity has not compiled the new scripts (log not updated since file changes) |
| Spec deviations (if any) flagged | PASS | No deviations — v3 code applied exactly as specified |

## Open questions for Architect

None — the implementation is correct per spec. The only blocker is that Unity MCP tools are unavailable in this session, preventing the builder from running and a fresh screenshot from being taken.

## Known FAIL items

All FAILs are due to one root cause: **Unity MCP tools (`mcp__unity__*`) are not available in this agent session**, preventing scene rebuild and playmode screenshot capture.

The code changes are complete, correct, and verified against the spec at the file level. All behavioral FAILs will become testable once Cesar:
1. Clicks Unity Editor to trigger compile
2. Runs the builder menu item
3. Enters play mode and takes a screenshot
4. Runs `python .claude/hooks/capture_screenshot.py 8_4_indicators`
5. Re-runs implementer subagent

## Console output

No new console output captured — Unity has not compiled the v3 scripts. Last known good output from Round 2:
```
[PhysicsLab] Flag GO found at (-230.50, 10.18, -72.48)
[PhysicsLab] Wind: 1.5 mph @ 45 deg
```
