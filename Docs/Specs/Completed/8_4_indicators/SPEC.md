# SPEC — `8_4_indicators` (v3 — REDO 2026-04-29 afternoon)

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state.
>
> **This is the second redo.** v1 had 6 FAILs, v2 closed 4. Round 2 playmode shows 3 remaining bugs:
> - Chip stays anchored at right edge of screen, only "slides" between fully-visible and fully-off-screen on the right side; never moves with the flag horizontally.
> - Tail is not under the chip — it floats at canvas bottom-center.
> - (Already fixed by Cesar at the asset level — tail PNG is no longer upside-down. **Do NOT add any code-side flip.**)
>
> v3 closes those, plus a nice-to-have for tail length scaling with distance.
>
> **CRITICAL — the v3 work is REAL CODE CHANGES.** The previous redo was passed off as architect-review-ready without actually applying v3's behavioral changes. Do not repeat that. Do the work, then run the builder, then take a fresh screenshot.

## Status

See `STATUS.md`. Currently `SELF_REVIEW_FAIL`. After implementing, set to `READY_FOR_SELF_REVIEW`.

---

## Root cause of the slide bug (v2 hierarchy)

v2's `IndicatorWidgetBuilder` set up `HoleIndicator` like this:

```
HoleIndicator     anchor (1,1)  pivot (1,1)  anchoredPosition (-48, -362)   ← top-right anchored
└── DataChip      anchor (1,1)  pivot (1,1)  anchoredPosition (0, 0)         ← top-right anchored
└── ArrowLine     anchor (1,1)  pivot (0.5, 1) anchoredPosition (-47, -100)  ← top-right anchored, sibling of DataChip
```

The widget then writes `chip.anchoredPosition.x = targetX` where `targetX` is a positive canvas-space X computed as `pinCanvasX - chipWidth/2`. With a top-right anchor + top-right pivot, **positive anchoredPosition.x moves the chip RIGHT of the canvas's right edge** (i.e. off-screen). Only large values push it off; small values keep it near the corner. That's why the chip never appears to slide.

Plus the tail is a SIBLING of the chip, so even if the chip were sliding correctly, the tail wouldn't follow.

## Fix — restructure the hierarchy so the parent slides

`HoleIndicator` (the parent root) becomes the sliding object, anchored **top-LEFT** of canvas. `DataChip` and `ArrowLine` become static children whose layout is fixed inside the parent. The widget mutates ONLY `_root.anchoredPosition.x`.

```
HoleIndicator   anchor (0,1) pivot (0,1) sizeDelta (100, 473) anchoredPosition (initialX, -362)   ← TOP-LEFT anchored, this slides
├── DataChip    anchor (0,1) pivot (0,1) sizeDelta (100, 100) anchoredPosition (0, 0)
│   ├── Backplate, FlagHalf, DistanceHalf  (unchanged)
└── ArrowLine   anchor (0.5,1) pivot (0.5,1) sizeDelta (6, 370) anchoredPosition (50, -100)
                                                                ↑ X=50 = center of 100-wide root
```

Initial X for `_root.anchoredPosition` doesn't matter (widget overwrites on first frame). Pick `1170 - 100 - 48 = 1022` so the inspector preview shows the chip at top-right where Figma puts it.

---

## Behavior — single mode, persistent tail

The chip **always slides horizontally to track the flag** (clamped to screen edges with 48px padding). The tail is **always visible** below the chip. Only the **tail's rotation** changes between two states:

- **Flag on screen** (chip is directly above flag's screen X): tail points **straight down** (rotation = 0).
- **Flag off screen** (chip clamped at left or right edge): tail **rotates** so its fade end points toward the off-screen flag.

Tail length scales with distance to flag (nice-to-have, but the inspector fields are simple to add):
- Short tail when close, long tail when far.
- `Mathf.Lerp(_tailMinLength, _tailMaxLength, Mathf.Clamp01(meters / _tailDistanceForMaxLength))`
- Defaults: 120 / 600 / 500.

---

## Code changes

### 1. `HoleIndicatorWidget.cs` — replace entire file with this

Drop `_slidingChipRoot` (dead — root is what slides now). Add three tail-length fields. Tail always visible, rotates only when flag off-screen.

```csharp
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Golfin.Gameplay.UI.HUD;

namespace Golfin.Gameplay.UI.ShotUI
{
    public class HoleIndicatorWidget : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] RectTransform _root;          // sliding root — top-LEFT anchored to canvas; widget mutates anchoredPosition.x
        [SerializeField] RectTransform _dataChip;      // static child of _root (the 100x100 chip visual; doesn't move independently)
        [SerializeField] RectTransform _arrowLine;     // static child of _root (the tail; pivot top-center; rotates when flag off-screen)
        [SerializeField] TMP_Text      _distanceText;

        [Header("Tracking config")]
        [SerializeField] float _edgePaddingPx = 48f;

        [Header("Tail length scaling")]
        [SerializeField] float _tailMinLength = 120f;
        [SerializeField] float _tailMaxLength = 600f;
        [SerializeField] float _tailDistanceForMaxLength = 500f; // meters

        Camera    _cam;
        Transform _ballTransform;

        public void SetCamera(Camera cam)            => _cam           = cam;
        public void SetBallTransform(Transform ball) => _ballTransform = ball;

        void OnEnable()
        {
            HoleContext.OnChanged += OnHoleChanged;
            OnHoleChanged();
        }
        void OnDisable() { HoleContext.OnChanged -= OnHoleChanged; }
        void OnHoleChanged() { /* values applied next LateUpdate */ }

        void LateUpdate()
        {
            if (_cam == null || _root == null) return;
            if (HoleContext.PinWorld == Vector3.zero) return;

            // Distance text
            Transform ball = _ballTransform;
            float meters = ball != null ? Vector3.Distance(ball.position, HoleContext.PinWorld) : 0f;
            float yards  = meters * 1.0936133f;
            if (_distanceText != null) _distanceText.text = $"{yards:F0} yds";

            // Project pin to canvas coords
            Vector3 pinScreen = _cam.WorldToScreenPoint(HoleContext.PinWorld);
            var canvas     = GetComponentInParent<Canvas>();
            if (canvas == null) return;
            var canvasRect = canvas.GetComponent<RectTransform>();
            float canvasWidth  = canvasRect.rect.width;
            float canvasHeight = canvasRect.rect.height;
            float scaleFactor  = canvas.scaleFactor;
            if (scaleFactor <= 0f) scaleFactor = 1f;

            float pinCanvasX        = pinScreen.x / scaleFactor;
            float pinCanvasYFromTop = canvasHeight - (pinScreen.y / scaleFactor);
            bool  pinBehind         = pinScreen.z < 0f;

            // Determine on-screen / off-screen
            bool flagOffLeft  = pinBehind || pinCanvasX < 0f;
            bool flagOffRight = !pinBehind && pinCanvasX > canvasWidth;
            bool flagOnScreen = !pinBehind && !flagOffLeft && !flagOffRight;

            // Chip width for clamp
            float chipWidth = _dataChip != null ? _dataChip.rect.width : 100f;

            // Compute desired root X (root is top-left anchored, so X is in canvas-space from left edge)
            float targetX;
            if (flagOnScreen)
            {
                targetX = pinCanvasX - chipWidth * 0.5f;
            }
            else if (flagOffLeft)
            {
                targetX = _edgePaddingPx;
            }
            else
            {
                targetX = canvasWidth - chipWidth - _edgePaddingPx;
            }
            targetX = Mathf.Clamp(targetX, _edgePaddingPx, canvasWidth - chipWidth - _edgePaddingPx);

            var rootPos = _root.anchoredPosition;
            rootPos.x = targetX;
            _root.anchoredPosition = rootPos;

            // Tail: always visible, length scales with distance, rotates only off-screen
            if (_arrowLine != null)
            {
                _arrowLine.gameObject.SetActive(true);

                if (_tailMaxLength > _tailMinLength && _tailDistanceForMaxLength > 0f)
                {
                    var sd = _arrowLine.sizeDelta;
                    sd.y = Mathf.Lerp(_tailMinLength, _tailMaxLength, Mathf.Clamp01(meters / _tailDistanceForMaxLength));
                    _arrowLine.sizeDelta = sd;
                }

                if (flagOnScreen)
                {
                    _arrowLine.localRotation = Quaternion.identity;
                }
                else
                {
                    // Chip's bottom-center in canvas-space (the tail's pivot point)
                    float chipCenterX = targetX + chipWidth * 0.5f;
                    float chipBottomY = -_root.anchoredPosition.y + 100f; // root.anchoredPosition.y is negative (top-anchored); chip is 100 tall

                    Vector2 dir = new Vector2(
                        pinCanvasX - chipCenterX,
                        -(pinCanvasYFromTop - chipBottomY)
                    );
                    if (dir.sqrMagnitude > 0.01f)
                    {
                        float angleRad = Mathf.Atan2(dir.y, dir.x);
                        float angleDeg = angleRad * Mathf.Rad2Deg;
                        // Tail at rest points straight down (rotation 0). To swing tail to point along `dir`,
                        // rotate Z by (angleDeg + 90).
                        // If round-3 playmode shows the tail pointing AWAY from the flag instead of toward it,
                        // STOP and print debug values for one frame (dir.x, dir.y, angleDeg, tailRotZ); do NOT chain-tweak.
                        _arrowLine.localRotation = Quaternion.Euler(0f, 0f, angleDeg + 90f);
                    }
                }
            }
        }
    }
}
```

### 2. `IndicatorWidgetBuilder.cs` — rebuild HoleIndicator hierarchy

Replace the HoleIndicator section (lines that build `holeRoot`, `dataChip`, and `arrowLineRt`) with the version below. The WindIndicator section, sprite loading, HoleDatabaseLoader-ensure, and helpers all stay as-is.

```csharp
// ─── HoleIndicator (v3 — top-left anchored, sliding root) ───────────────

// Root: 100x473, anchor TOP-LEFT, pivot top-left.
// Initial X is just an inspector default; the widget overwrites it each LateUpdate.
var holeRoot = CreateRectTransform("HoleIndicator", canvas.transform, new Vector2(100, 473));
holeRoot.anchorMin = new Vector2(0, 1);
holeRoot.anchorMax = new Vector2(0, 1);
holeRoot.pivot     = new Vector2(0, 1);
holeRoot.anchoredPosition = new Vector2(1170f - 100f - 48f, -362f); // initial: top-right corner with 48px padding

// DataChip: 100x100, anchor top-left of root, pivot top-left
var dataChip = CreateRectTransform("DataChip", holeRoot, new Vector2(100, 100));
dataChip.anchorMin = new Vector2(0, 1);
dataChip.anchorMax = new Vector2(0, 1);
dataChip.pivot     = new Vector2(0, 1);
dataChip.anchoredPosition = new Vector2(0, 0);

// DataChip Backplate
var holeBackplate = CreateRectTransform("Backplate", dataChip);
StretchFill(holeBackplate);
var holeBackImg = holeBackplate.gameObject.AddComponent<Image>();
holeBackImg.sprite = backplateSprite;
holeBackImg.type   = Image.Type.Simple;

// FlagHalf: top 50px (anchored top-left of DataChip)
var flagHalf = CreateRectTransform("FlagHalf", dataChip, new Vector2(100, 50));
flagHalf.anchorMin = new Vector2(0, 1);
flagHalf.anchorMax = new Vector2(0, 1);
flagHalf.pivot     = new Vector2(0, 1);
flagHalf.anchoredPosition = new Vector2(0, 0);

// FlagIcon: 83x42, anchor center within FlagHalf
var flagIconRt = CreateRectTransform("FlagIcon", flagHalf, new Vector2(83, 42));
SetAnchorCenter(flagIconRt);
flagIconRt.anchoredPosition = Vector2.zero;
var flagImg = flagIconRt.gameObject.AddComponent<Image>();
flagImg.sprite = flagSprite;
flagImg.preserveAspect = true;

// DistanceHalf: bottom 50px
var distHalf = CreateRectTransform("DistanceHalf", dataChip, new Vector2(100, 50));
distHalf.anchorMin = new Vector2(0, 1);
distHalf.anchorMax = new Vector2(0, 1);
distHalf.pivot     = new Vector2(0, 1);
distHalf.anchoredPosition = new Vector2(0, -50);

// DistanceText: TMP, stretch within DistanceHalf
var distTextGo = new GameObject("DistanceText");
distTextGo.transform.SetParent(distHalf, false);
var distTextRt = distTextGo.AddComponent<RectTransform>();
StretchFill(distTextRt);
distTextRt.offsetMin = new Vector2(9, 10);
distTextRt.offsetMax = new Vector2(-9, -10);
var distTmp = distTextGo.AddComponent<TextMeshProUGUI>();
distTmp.text = "0 yds";
distTmp.fontSize = 23;
distTmp.color = navyColor;
distTmp.alignment = TextAlignmentOptions.Center;
if (rubikFont != null) distTmp.font = rubikFont;

// ArrowLine: 6x370, anchor TOP-CENTER of root, pivot top-center.
// Sits directly under the chip (chip height = 100, so anchoredPosition.y = -100).
// X = 50 = horizontal center of 100-wide root.
// Rotates around its top pivot when flag is off-screen.
// NOTE: Tail PNG asset has been corrected by Cesar (gradient now full-at-top, fade-at-bottom).
// Do NOT apply any localScale flip in code.
var arrowLineRt = CreateRectTransform("ArrowLine", holeRoot, new Vector2(6, 370));
arrowLineRt.anchorMin = new Vector2(0, 1);
arrowLineRt.anchorMax = new Vector2(0, 1);
arrowLineRt.pivot     = new Vector2(0.5f, 1f);
arrowLineRt.anchoredPosition = new Vector2(50f, -100f); // X=50 = center of 100-wide root, Y=-100 = directly below chip
var trailImg = arrowLineRt.gameObject.AddComponent<Image>();
trailImg.sprite = trailSprite;
trailImg.color  = whiteColor;
trailImg.type   = Image.Type.Simple;

// Add HoleIndicatorWidget and wire SerializeFields
var holeWidget = holeRoot.gameObject.AddComponent<Golfin.Gameplay.UI.ShotUI.HoleIndicatorWidget>();
var holeSo = new SerializedObject(holeWidget);
holeSo.FindProperty("_root").objectReferenceValue         = holeRoot;
holeSo.FindProperty("_dataChip").objectReferenceValue     = dataChip;
holeSo.FindProperty("_arrowLine").objectReferenceValue    = arrowLineRt;
holeSo.FindProperty("_distanceText").objectReferenceValue = distTmp;
holeSo.ApplyModifiedProperties();

Debug.Log("[IndicatorWidgetBuilder] HoleIndicator built and wired (v3 hierarchy: top-left anchored sliding root).");
```

**Note:** The widget no longer has `_slidingChipRoot`. Remove the line `holeSo.FindProperty("_slidingChipRoot").objectReferenceValue = dataChip;` from the builder. If left in, it will throw a NullReferenceException at runtime when the FindProperty returns null.

---

## Acceptance checklist (v3)

Run the builder via `GOLFIN/Build/Build Indicator Widgets (8.4)` after applying code changes, then enter playmode and verify:

### Behavior

- [ ] Tail is **always visible** (visible in every frame; never disappears)
- [ ] Tail is positioned **directly below the chip**, regardless of where the chip is
- [ ] When flag is on-screen, chip slides horizontally to align with flag's screen X — verify by panning camera left/right with flag on screen, chip moves
- [ ] When flag is on-screen and would push chip past screen edge, chip clamps at left or right edge with 48px padding
- [ ] When flag is on-screen, tail points straight down (no rotation)
- [ ] When flag is off-screen, chip locks at the closer edge (left or right) with 48px padding
- [ ] When flag is off-screen, tail rotates so its fade end points TOWARD the off-screen flag (NOT away from it)
- [ ] Tail length grows as ball-to-flag distance grows; shrinks as ball gets closer

### Carried from v2 (no re-verification)

- [ ] [CARRIED] Wind chevron PNG sprite renders
- [ ] [CARRIED] Wind speed text shows CSV value
- [ ] [CARRIED] HoleDatabaseLoader in scene with CSV wired
- [ ] [CARRIED] Flag GO found via prefix match (Flag_1)
- [ ] [CARRIED] Distance text shows real yards from ball to flag

### Verification protocol — fresh playmode screenshot required

The Implementer MUST:
1. Apply code changes to `HoleIndicatorWidget.cs` and `IndicatorWidgetBuilder.cs`.
2. Run `GOLFIN/Build/Build Indicator Widgets (8.4)` to rebuild the scene hierarchy.
3. Enter playmode, capture a screenshot, and verify the chip's actual on-screen position relative to the flag's screen position.
4. Save screenshot to `Docs/Specs/Active/8_4_indicators/screenshots/` with a v3-marked filename.
5. Check that the screenshot actually shows the chip in a non-corner position when the flag is on-screen and toward the screen center. If the chip is still at the corner, that means the rebuild didn't take or the widget code didn't compile — investigate before marking PASS.

**The previous round's IMPLEMENTER_REPORT marked behavioral items "Unverifiable in static screenshot" and bounced to architect review without applying v3's actual code changes. Do NOT do that. The code changes ARE verifiable by re-running playmode and checking the chip's position against the flag's position.**

---

## Files this redo touches

**Modified (v3):**
- `Assets/Scripts/Gameplay/UI/ShotUI/HoleIndicatorWidget.cs` — replace entire file (drop `_slidingChipRoot`, add tail length fields, behavioral changes)
- `Assets/Scripts/Editor/CanvasScalerMigration/IndicatorWidgetBuilder.cs` — rebuild HoleIndicator hierarchy (top-left anchored root, top-center anchored ArrowLine as child)

**Carried from v2 (no changes):**
- All other files

---

## Rotation math: STOP rule

If round-3 playmode shows the tail rotating in the wrong direction when the flag is off-screen, **STOP**. Do NOT chain-tweak `+90`/`-90`/`±angleDeg` and re-run. Add a one-frame `Debug.Log($"dir=({dir.x:F1},{dir.y:F1}) angleDeg={angleDeg:F1} rotZ={tailRotZ:F1}")` to the off-screen branch, run playmode with the camera turned so the flag is clearly off the right edge, capture the log, and stop. Architect will read the values and tell you the correct fix.

This is the second time this rotation math has shipped without on-screen verification. We don't have the budget for a third blind tweak.
