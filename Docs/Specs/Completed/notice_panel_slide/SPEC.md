# SPEC — `notice_panel_slide`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work
> definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files.
>
> Filed 2026-09-09 (Architect via Cowork). Follow-up to `home_notices` (DONE) and the notice
> dot pooling from `pagination_dots`. Cesar's brief, verbatim intent: *"New Notices should
> slide (with the container) from the right. And they are changing too fast at the moment. The
> player should be able to slide them with the finger as well."*

## Status

See `STATUS.md`. `SPEC_READY`.

## Decisions of record (Cesar, 2026-09-09)

| # | Decision | Consequence |
|---|---|---|
| D1 | **The whole notice box moves** — background + title + divider + body. On a page change the current box slides out to the left and the next box slides in from the right. | Two box instances under one static root; no `RectMask2D`, no text-only slide. |
| D2 | **Auto-cycle interval 10 s** (was 5 s). | Change the SCENE's serialized `newsAutoCycleInterval` (ShellScene, currently `5`), not only the C# default. |
| D3 | **A swipe restarts the countdown only.** Cycling continues from the page the player landed on, after a full interval. | No pause-until-re-entry, no grace multiplier. |

**Architect defaults** (Cesar did not decide these — flag in the report if you change them):
snap duration 0.28 s `Ease.OutCubic`; commit a drag at ≥ 60 px **or** release velocity ≥ 800 px/s
(canvas px); page spacing = 1170 (canvas width); single-page rubber-band factor 0.35 capped at
80 px; the auto-cycle timer holds while a finger is down and while a snap is in flight.

## Goal

The Home notice panel pages like the mode carousel below it: the box the player is reading
slides off to the left while the next notice's box slides in from the right (auto-cycle and
"next"), or the reverse for "previous"; the player can drag the box with a finger and it follows,
then snaps to whichever page the release resolves to; and the automatic rotation slows to 10 s.
Everything else about notices — fetch, language resolution, hide-when-empty, the pooled dots,
the daily pill's placement below the panel — is unchanged.

## Reference

No Figma frame — this task changes no layout at rest. The box, its rect and its contents look
exactly as today when nothing is moving. Motion reference: `ModeCarouselController` (drag
follows the finger, snap on release) and `DailyMissionPillController.Enter` (a box sliding on
`anchoredPosition.x` through `UiMotion.Slide`).

## Architecture context

- **Scene, not prefab.** `Assets/Scenes/ShellScene.unity` holds the live Home screen
  (`HomeScreen` → `NoticePanel`, GameObject `446239822`, RectTransform `446239821`:
  anchors (0.5, 1)/(0.5, 1), pivot (0.5, 1), anchoredPosition (0, −361), size 1018×352).
  `Assets/Prefabs/UI/HomeScreen.prefab` is referenced by nothing (GUID grep, 2026-09-09) and
  carries `newsPanelRoot: 0` — **do not edit it**; note it as stale in the report.
- `NoticePanel` today carries: `Image` (raycast target, sprite `71badbcc…`),
  `VerticalLayoutGroup` (padding L1/T42/B42, spacing 10), **`GolfinRedux.UI.SwipeDetector`**
  (`Assets/Scripts/UI/SwipeDetector.cs`, threshold 50, `onSwipeLeft → NextNewsPage`,
  `onSwipeRight → PreviousNewsPage`), and three children `NewsTitleText` (1000×64),
  `Divider` (978×2), `NewsBodyText` (1012×180).
- `PageDots` (`1909492906842863269`) is a **sibling** of `NoticePanel` under `HomeScreen` at
  (607.2, −744.5). It does not move. `HomeScreenController._newsDots` (`PaginationDotStrip`,
  `Assets/Scripts/UI/Common/PaginationDotStrip.cs`) keeps painting it.
- `Assets/Scripts/UI/HomeScreenController.cs` (`GolfinRedux.UI`): `newsTitleText`,
  `newsBodyText`, `newsPanelRoot`, `newsAutoCycleInterval` (5), `_currentNewsIndex`,
  `_newsTimer`, `_autoCycleNews`; `Update()` at ~L287 advances with `NextNewsPage()`;
  `RefreshNewsPanel()` / `UpdateNewsContent()` / `UpdateNewsDots()` / `NextNewsPage()` /
  `PreviousNewsPage()` / `SetNewsPage(int)` / `SetNewsPanelVisible(bool)`.
  `NewsPageCount` reads `Golfin.Notices.NoticeService.Instance.Pages` (0 is normal).
- `Assets/Scripts/UI/Home/DailyMissionPillController.ComputeTargetY()` reads
  `noticePanelRoot`'s `anchoredPosition.y`, `rect.height` and `activeInHierarchy` — the
  ROOT must keep those values and keep being the object `HomeScreenController` toggles.
- Motion: `Golfin.UI.Polish.UiMotion` — `Run(host, ref handle, routine)`, `Stop`,
  `Tween(from, to, dur, apply, Ease)`, `Slide(rect, fromX, toX, dur, …)`, `Ease.OutCubic`,
  unscaled time, `UiMotion.Enabled` respected by `Register`. No per-frame allocation.
- Tests: `Assets/Tests/EditMode/` (`GolfinRedux.Tests.EditMode`; `InternalsVisibleTo` is
  already declared for it in `Assets/Scripts/UI/Gacha/GachaBannerModel.cs` — do not redeclare).

## Implementation

### 1. Scene hierarchy (ShellScene only)

Restructure `NoticePanel` into a static root with two identical page boxes:

```
NoticePanel                (root — RectTransform ONLY + NoticePageSlider; rect UNCHANGED:
│                           anchors 0.5/1, pivot 0.5/1, pos (0, −361), size 1018×352)
├── PageA                  (anchors 0.5/0.5, pivot 0.5/0.5, pos (0,0), size 1018×352 —
│   │                       Image + VerticalLayoutGroup moved here from the root, values as-is)
│   ├── NewsTitleText      (the existing objects, re-parented — same rects, same TMP settings)
│   ├── Divider
│   └── NewsBodyText
└── PageB                  (duplicate of PageA; pos (1170, 0) at rest)
    ├── NewsTitleText
    ├── Divider
    └── NewsBodyText
```

- Move the `Image` and `VerticalLayoutGroup` components off the root onto `PageA` (same
  values). The root keeps no `Graphic`. Both page `Image`s keep `raycastTarget = 1` — that is
  what delivers the drag to the root's `NoticePageSlider` (events bubble to the parent).
- **Remove the `SwipeDetector` component** from `NoticePanel`. The slider replaces it; leaving
  it would double-fire `NextNewsPage` on top of the slider's own commit. Leave
  `SwipeDetector.cs` in the repo (still a valid utility).
- `HomeScreenController` fields: `newsPanelRoot` stays on the root; `newsTitleText` /
  `newsBodyText` are re-pointed at `PageA`'s texts (they remain the no-slider fallback, §3).
  New field `noticeSlider` → the root's `NoticePageSlider`.
- Nothing else on Home moves. `PageDots`, `CharacterRoot`, `PromoBanner`, the daily pill and
  the mode carousel are untouched.

### 2. `NoticePageSlider` (new — `Assets/Scripts/UI/Home/NoticePageSlider.cs`, `Golfin.UI.Home`)

```csharp
public sealed class NoticePageSlider : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Serializable] public sealed class PageBox
    {
        public RectTransform rect;
        public TextMeshProUGUI title;
        public TextMeshProUGUI body;
    }

    [SerializeField] private PageBox pageA, pageB;
    [SerializeField] private float pageSpacing     = 1170f;  // canvas px between boxes
    [SerializeField] private float snapDuration    = 0.28f;
    [SerializeField] private float commitDistance  = 60f;    // canvas px
    [SerializeField] private float commitVelocity  = 800f;   // canvas px / s
    [SerializeField] private float rubberBand      = 0.35f;  // single-page drag resistance
    [SerializeField] private float rubberBandMax   = 80f;

    /// Raised when a DRAG commits to a neighbour (+1 next / −1 previous). Never raised by
    /// SlideTo/SetPage — the controller already knows about those.
    public event Action<int> OnDragCommitted;

    public bool IsBusy { get; }        // finger down OR snap in flight — the timer holds on this
    public int  Current { get; }       // index of the page in the front box

    /// Replace the page set. Paints `index` into the front box instantly (no motion). Used on
    /// every refresh: notices changed, language changed, Home OnEnable.
    public void SetPages(IReadOnlyList<Golfin.Notices.NoticePage> pages, int index);

    /// Animate to `index`. direction +1: the new box enters from the RIGHT, the current exits
    /// LEFT (auto-cycle and Next). −1: the reverse (Previous). Ignored while IsBusy.
    public void SlideTo(int index, int direction);
}
```

Behaviour:

- **Front/back boxes.** The front box is whichever of A/B is at x = 0. The back box is parked
  at `±pageSpacing` (off-screen: 1170 − 509 = 661 > 585 half-canvas) and is painted with the
  neighbour's text only when it is about to be seen (drag start, `SlideTo`).
- **`SlideTo(index, dir)`**: paint `pages[index]` into the back box, place it at
  `dir * pageSpacing`, then one `UiMotion.Tween(0, 1, snapDuration, t => …)` moving front to
  `−dir * pageSpacing * t` and back to `dir * pageSpacing * (1 − t)`. On completion swap the
  front/back roles, park the old front at `+pageSpacing`. `UiMotion.Run(this, ref _motion, …)`;
  `OnDisable` → `UiMotion.Stop` + snap both boxes to rest (front 0, back parked) so a
  re-entered Home never finds a box mid-flight.
- **Drag** (`ScreenPointToLocalPointInRectangle` on the root, so the delta is in canvas px, as
  `ModeCarouselController` does): on begin, paint the neighbour for the drag's sign into the
  back box **once the sign is known** (first `OnDrag` with |delta| > 0), park it on that side.
  If the sign flips mid-drag, repaint for the new side. Front x = delta, back x = delta ±
  pageSpacing. Page count 1 (or 0): no neighbour; front x = clamp(delta * rubberBand,
  ±rubberBandMax); release always snaps home.
- **Release**: `ResolveRelease(delta, velocity, pageCount)` (pure, `internal static`, tested):
  returns +1 when `delta ≤ −commitDistance || velocity ≤ −commitVelocity`, −1 for the mirror,
  0 otherwise; always 0 when `pageCount <= 1`. Velocity = `eventData.delta.x /
  Time.unscaledDeltaTime` in canvas px. Then a snap tween from the current offsets to the
  resolved rest (same `snapDuration`, `OutCubic`); on a commit, `Current` wraps
  (`(Current + result + count) % count`) and `OnDragCommitted(result)` fires **after** the
  snap settles.
- **Wrap-around** is cyclic in both directions, matching `NextNewsPage`'s `% count`. With two
  pages, dragging either way shows the other page.
- Boxes are positioned only through `anchoredPosition.x`; y stays 0. Parking the back box
  off-canvas is enough — no mask, nothing to clip (the boxes overlap nothing in their band
  except each other).
- Tap: a press that never moves past Unity's drag threshold is not a drag — no `OnBeginDrag`
  — so the panel stays inert to taps exactly as today.

### 3. `HomeScreenController` changes (minimal)

- New `[SerializeField] private NoticePageSlider noticeSlider;` (Header "News Panel").
- `newsAutoCycleInterval` default `5f → 10f` **and** the ShellScene serialized value
  (`newsAutoCycleInterval: 5` on the HomeScreenController component) → `10`. Both, or the
  scene keeps 5 (WORKFLOW_NOTES: scene values win over C# defaults).
- `Update()`: the auto-cycle block additionally requires `noticeSlider == null ||
  !noticeSlider.IsBusy` before it accumulates `_newsTimer` (D3 / Architect default: the
  countdown holds while a finger is down or a snap runs; it does not reset just because the
  player touched the box without committing).
- `UpdateNewsContent()`: non-demo path with ≥1 page → `noticeSlider.SetPages(pages,
  _currentNewsIndex)` when the slider is assigned, else the existing direct `.text` writes
  (keep them: the fallback and the demo path). Demo path: unchanged texts (`PageA`'s).
- `NextNewsPage()` / `PreviousNewsPage()` / `SetNewsPage(int)`: compute the new index as
  today, reset `_newsTimer`, `UpdateNewsDots()`, then `noticeSlider.SlideTo(newIndex, +1 / −1 /
  sign(new − old))` instead of `UpdateNewsContent()` when the slider is assigned. `SetNewsPage`
  to the same index is a no-op.
- Subscribe `noticeSlider.OnDragCommitted += OnNoticeDragCommitted` in `OnEnable`, unsubscribe
  in `OnDisable` (event-driven UI convention). Handler: `_currentNewsIndex =
  noticeSlider.Current; _newsTimer = 0f; UpdateNewsDots();` — no `SlideTo` (the box is
  already there).
- `RefreshNewsPanel()` (notices/language changed, OnEnable) keeps calling
  `UpdateNewsContent()` → `SetPages` → instant repaint in place, no motion. A refresh that
  arrives mid-snap: `SetPages` cancels the motion and paints at rest.

### 4. No new strings, no data changes

No player-facing text is added. `LocalizationText.csv` untouched; nothing goes through the
importer. No server or admin change.

## Acceptance checklist (Implementer fills in `IMPLEMENTER_REPORT.md`)

- [ ] At rest, Home with one notice is pixel-identical to before (screenshot diff of the notice
      band, EN + JA): same box, same rect (0, −361, 1018×352), same text placement.
- [ ] Two+ notices: auto-cycle advances every **10 s** (log timestamps quoted), the old box
      slides out LEFT and the new box slides in from the RIGHT (video in `videos/`).
- [ ] Finger drag: the box follows the finger; release past 60 px commits, under 60 px snaps
      back; a fast flick under 60 px still commits (velocity path). Drag right shows the
      previous notice entering from the left. Wrap-around both ways with 2 and with 3 notices.
- [ ] After a committed swipe, the countdown restarts: the next auto-advance is a full 10 s
      later (D3). Timer holds while a finger is down and during a snap.
- [ ] One notice: drag rubber-bands (≤ 80 px) and snaps home; no auto-cycle; no neighbour box
      ever visible. Zero notices: panel hidden, as before.
- [ ] Dots track the page across auto-cycle AND drag commits (`PaginationDotStrip` current dot).
- [ ] Notices refreshed or language switched mid-motion → repaint in place at rest; no box
      left off-centre; no exception.
- [ ] Leaving Home mid-snap and returning → box at rest at x = 0; back box parked.
- [ ] Daily pill: `ComputeTargetY()` unchanged — screenshot with the pill below a live notice,
      before/after, same y.
- [ ] `SwipeDetector` removed from `NoticePanel` in ShellScene (YAML grep quoted: zero
      `SwipeDetector` on `446239822`); `HomeScreen.prefab` untouched.
- [ ] EditMode tests green: `ResolveRelease` table (distance commit, velocity commit, both
      signs, dead zone, `pageCount <= 1` → 0) + wrap index math; full sweep, no new failures.
- [ ] Profiler: no per-frame GC alloc during a drag or a snap (one screenshot or a
      `GC.Alloc` assertion).
- [ ] `newsAutoCycleInterval` = 10 in BOTH the scene YAML and the C# default (both quoted).
- [ ] All `[SerializeField]` references wired (`noticeSlider`, `pageA`/`pageB` ×3 each).
- [ ] Unity Console has no errors related to this task.
- [ ] Spec deviations (if any) flagged at the bottom of the report with justification.

## Files / hierarchy this task touches

- `Assets/Scenes/ShellScene.unity` — `NoticePanel` restructured per §1; `SwipeDetector`
  removed; `HomeScreenController.noticeSlider` + `newsTitleText`/`newsBodyText` re-wired;
  `newsAutoCycleInterval` 5 → 10. **Nothing else in the scene.**
- `Assets/Scripts/UI/Home/NoticePageSlider.cs` (new).
- `Assets/Scripts/UI/HomeScreenController.cs` — §3 only.
- `Assets/Tests/EditMode/NoticePageSliderTests.cs` (new).
- `Docs/AI_CONTEXT.md` — session note.

## Smoke evidence

Editor play mode with ≥ 2 live notices (activate a second row in the dashboard Notices panel
if prod has one): video of auto-cycle (two transitions, timestamps visible), a drag-commit, a
drag-cancel, a flick, a wrap-around, plus the JA screenshot at rest. Human play-and-confirm note
(Lesson O) on the motion feel — the Architect defaults above are the starting point; Cesar
signs off on device.

## Out of scope (do NOT do these)

- The box sliding in from the right when Home opens (first paint stays instant). Cesar chose
  "whole box moves on page change" — not an entry animation.
- Tappable dots, arrows, or a tap-to-open notice detail.
- Any change to `NoticeService`, the endpoint, the dashboard, or the notice text styling.
- Editing `HomeScreen.prefab` (stale, unreferenced).
- A reduced-motion setting — `UiMotion.Enabled` is the seam; nothing wires it yet.
