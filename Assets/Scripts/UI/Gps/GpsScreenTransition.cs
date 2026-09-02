// ─────────────────────────────────────────────────────────────────────────────
// gps_polish §D2 — the layered push between two GPS screens.
//
// THE BOUNDARY IS UNCHANGED. Home → GpsHub, GpsHub → Home, any GPS → Login or
// Loading, still fade to black through FadeController exactly as before. That is
// the game-wide convention for "you have left where you were", and the GPS
// surface is a place you enter and leave. This file is only about moving BETWEEN
// rooms once you are inside — and there, a fade to black reads as leaving the
// whole surface every time you tap a nav slot.
//
// WHAT MOVES, AND WHAT DOES NOT
//   Only the two ContentContainers slide. The Background, the GpsNavBar and the
//   hub's BackPill cross-fade IN PLACE. The nav bar is the same five slots on
//   every GPS screen, so a bar that slid with the content would read as five
//   icons leaving and five identical icons arriving — motion with no meaning.
//
// THE COMPOSITING ORDER IS LOAD-BEARING (deviation D-1, see IMPLEMENTER_REPORT).
//   The spec's literal reading is "target background 0 → 1 while the current's
//   goes 1 → 0". Two full-screen OPAQUE sprites at 0.5 alpha do not composite to
//   an opaque frame: the result is 0.5·target + 0.25·current + 0.25·whatever is
//   behind the canvas, so the midpoint of that cross-fade is a 25 % see-through
//   hole. Instead the OUTGOING background is held at 1 and the incoming one is
//   faded in ON TOP of it (the target is moved to last sibling for the duration
//   and restored after). Every frame is fully opaque, and the SPEC's own seam
//   invariant — never both backgrounds below 0.5 — holds by construction rather
//   than by arithmetic that happens to sum to one.
//
// REST STATE IS THE CONTRACT. Everything this file moves or fades is restored to
// position 0 / alpha 1 / blocksRaycasts true before ApplyScreen's swap settles,
// on EVERY exit path — normal completion, an interrupting second Navigate, and a
// host disable mid-push (UiMotionRunner's OnDisable). A2 pixel-diffs an animated
// arrival against ShowScreen(instant: true) for exactly this reason.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using Golfin.UI.Polish;
using GolfinRedux.UI;
using UnityEngine;

namespace Golfin.Gps.UI
{
    /// <summary>
    /// The GPS-internal screen push. Driven by <see cref="ScreenManager"/>, which owns the
    /// coroutine; this class owns the layering, the direction table and the rest-state restore.
    /// </summary>
    public static class GpsScreenTransition
    {
        private const string Tag = "[GpsPush]";

        public enum Dir
        {
            /// <summary>Target enters from +W; the leaver parallaxes to −0.3·W.</summary>
            Forward,
            /// <summary>Mirrored.</summary>
            Back,
        }

        /// <summary>How far the leaving content drifts, as a fraction of the entering travel.
        /// Small on purpose: it is depth, not a second slide.</summary>
        public const float ParallaxFactor = 0.3f;

        // ═════════════════════════════════════════════════════════════════════
        // Entry motion hand-off
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// One-shot: set by <see cref="Push"/> immediately before the target is activated, and
        /// consumed by that screen's controller in <c>OnEnable</c> (§D3). A screen reached
        /// through a push has ALREADY animated its content in — running the boundary Rise on top
        /// of the slide would be two entrances in 0.25 s.
        /// </summary>
        private static bool _skipEntry;

        /// <summary>
        /// True for every controller enabled by the <c>SetActive</c> inside a push, and false
        /// everywhere else.
        ///
        /// <para>Armed and disarmed AROUND that one call rather than cleared by the first reader:
        /// a screen can carry more than one component whose <c>OnEnable</c> wants to know (the
        /// Gift screen enables its send-modal controller alongside the screen controller), and a
        /// first-caller-wins flag would give the honest answer to whichever happened to run
        /// first.</para>
        /// </summary>
        public static bool EnteringViaPush => _skipEntry;

        // ═════════════════════════════════════════════════════════════════════
        // Direction table
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Nav-bar slot order, left to right, for the screens that ARE hub-nav destinations.
        /// −1 means "not a tab" — a sub-screen reached from inside another screen, which always
        /// reads as going deeper.
        ///
        /// <para>GpsVote has no slot of its own (it is reached from the hub's VOTE tile and from
        /// a vote card's GIFT button), but it sits with Gift in the player's mental row, so it is
        /// given the position immediately right of Gift.</para>
        /// </summary>
        public static int NavSlot(ScreenId id)
        {
            switch (id)
            {
                case ScreenId.GpsHub:      return 0;   // NavHome
                case ScreenId.ScoreUpload: return 2;   // NavCamera
                case ScreenId.GpsGift:     return 3;   // NavGift
                case ScreenId.GpsVote:     return 4;   // (no slot — sits right of Gift)
                case ScreenId.GpsProfile:  return 5;   // NavProfile
                default:                   return -1;
            }
        }

        /// <summary>
        /// The pinned direction table (<c>GpsScreenTransitionTests</c> asserts every ordered
        /// pair). Rules, in order:
        /// <list type="number">
        /// <item>Anything whose target is the hub reads as coming BACK to the front door.</item>
        /// <item>A non-push (GoBack, a nav-bar pillar jump) is BACK.</item>
        /// <item>Between two nav-bar destinations, the bar's own left-to-right order decides.</item>
        /// <item>Leaving a DEEP sub-screen (Badges, Avatar — no slot of their own) for a screen
        /// that IS in the nav bar is coming back up, so it is BACK. Tapping the Profile slot from
        /// Badges is the player's only way out of Badges, and it must not read as going deeper.</item>
        /// <item>Everything else is going deeper: FORWARD.</item>
        /// </list>
        /// </summary>
        public static Dir DirectionFor(ScreenId from, ScreenId to, bool push)
        {
            if (to == ScreenId.GpsHub) return Dir.Back;
            if (!push) return Dir.Back;

            int a = NavSlot(from), b = NavSlot(to);
            if (a >= 0 && b >= 0) return b > a ? Dir.Forward : Dir.Back;
            if (a < 0 && b >= 0) return Dir.Back;

            return Dir.Forward;
        }

        // ═════════════════════════════════════════════════════════════════════
        // Eligibility
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Whether this pair may be pushed rather than faded. False sends the caller straight
        /// back to the untouched <see cref="FadeController"/> path, which is why every reason
        /// here is a reason the push would look WORSE, not merely a reason it is hard.
        /// </summary>
        public static bool CanPush(ScreenId from, ScreenId to, GameObject? fromGo, GameObject? toGo)
        {
            if (!UiMotion.Enabled) return false;
            if (from == to) return false;
            if (!GpsGate.IsGpsScreen(from) || !GpsGate.IsGpsScreen(to)) return false;

            // ScoreUpload's six step roots each carry their OWN full-screen background and sit
            // BESIDE ContentContainer (ScoreUploadScreenBuilder § step roots). There is no single
            // content layer to slide and no single background to cross-fade, so the boundary fade
            // is the honest transition for it — in both directions.
            if (from == ScreenId.ScoreUpload || to == ScreenId.ScoreUpload) return false;

            return HasSplit(fromGo) && HasSplit(toGo);
        }

        /// <summary>A screen is pushable only if it really has the Background / ContentContainer
        /// split the whole design depends on.</summary>
        public static bool HasSplit(GameObject? go)
        {
            if (go == null) return false;
            return go.transform.Find("Background") != null
                && go.transform.Find("ContentContainer") != null;
        }

        // ═════════════════════════════════════════════════════════════════════
        // The push
        // ═════════════════════════════════════════════════════════════════════

        private sealed class Layer
        {
            public RectTransform? Content;
            public CanvasGroup?   ContentGroup;
            public CanvasGroup?   Chrome;          // Background + NavBar + BackPill, one per rect
            public readonly List<CanvasGroup> ChromeGroups = new List<CanvasGroup>();
            public float RestX;
            public bool  RestBlocksRaycasts = true;
        }

        private sealed class Push_
        {
            public GameObject? FromGo;
            public GameObject? ToGo;
            public Layer From = new Layer();
            public Layer To   = new Layer();
            public int   ToSiblingIndex = -1;
            public Action? Apply;
            public bool  Settled;
        }

        private static Push_? _active;

        /// <summary>True while a push is running. <see cref="ScreenManager"/> checks this so a
        /// second navigation finishes the first instantly instead of queueing.</summary>
        public static bool IsPushing => _active != null;

        // ── Instrumentation for the A1 probe ─────────────────────────────────
        // Published from INSIDE the tween because an observer coroutine cannot measure it
        // honestly: it necessarily starts a frame late, and the first frame after a screen is
        // enabled is the expensive one (OnEnable, the first layout, the fetches). Timing the push
        // from the outside charged that frame to the wrong stopwatch and read 0.10 s for a 0.25 s
        // animation. This is the tween's own accumulated unscaled time.

        /// <summary>Accumulated unscaled seconds of the most recent push.</summary>
        public static float LastPushElapsed { get; private set; }

        /// <summary>Frames the most recent push rendered.</summary>
        public static int LastPushFrames { get; private set; }

        /// <summary>Whether the most recent push ran to completion rather than being snapped by
        /// an interrupting navigation.</summary>
        public static bool LastPushCompleted { get; private set; }

        /// <summary>The offset from rest, in px, the target content was placed at before the
        /// first frame was drawn — the t = 0 assertion, taken where it is actually true.</summary>
        public static float LastPushEnterOffset { get; private set; }

        /// <summary>Rest X of the two content containers, sampled BEFORE either was moved.</summary>
        public static float LastPushTargetRestX { get; private set; }
        public static float LastPushLeaverRestX { get; private set; }

        /// <summary>
        /// Finish the running push NOW — snap everything to rest and run the deferred
        /// <c>ApplyScreen</c>. Called when a second navigation arrives mid-push (SPEC §D2.4:
        /// "no queue") and by <see cref="ScreenManager"/> before it starts anything else.
        /// </summary>
        public static void CompleteActiveNow()
        {
            if (_active == null) return;
            Push_ p = _active;
            _active = null;
            Settle(p);
        }

        /// <summary>
        /// The coroutine. <paramref name="apply"/> is <c>ScreenManager.ApplyScreen</c>, deferred
        /// to the END — running it at the midpoint (as the boundary fade does) would deactivate
        /// the screen that is still visibly sliding out.
        /// </summary>
        public static IEnumerator Push(GameObject fromGo, GameObject toGo, Dir dir, Action apply)
        {
            // A push already in flight is finished instantly rather than layered on top.
            CompleteActiveNow();

            var p = new Push_ { FromGo = fromGo, ToGo = toGo, Apply = apply };
            Collect(fromGo, p.From);
            Collect(toGo,   p.To);

            float w = TravelWidth(toGo, p.To);
            // Offsets FROM each layer's own rest X — the containers are not all at the same x
            // (the hub's sits at 0, every other GPS screen's at 96), so an absolute ±W would
            // land the hub 96 px away from where the others do.
            float enterOffset = dir == Dir.Forward ?  w : -w;
            float leaveOffset = dir == Dir.Forward ? -w * ParallaxFactor : w * ParallaxFactor;
            float enterFrom   = p.To.RestX + enterOffset;

            Debug.Log($"{Tag} {fromGo.name} -> {toGo.name} dir={dir} W={w:0.#} " +
                      $"enterOffset={enterOffset:0.#} leaveOffset={leaveOffset:0.#} " +
                      $"dur={UiMotion.PushDur}");

            // ── Stage the target UNDER the push's own rules, then activate it ──
            // Order matters: SkipEntry must be armed before OnEnable runs, and the chrome must
            // already be at alpha 0 before the first frame the target is drawn.
            p.ToSiblingIndex = toGo.transform.GetSiblingIndex();
            toGo.transform.SetAsLastSibling();

            foreach (var g in p.To.ChromeGroups) g.alpha = 0f;
            if (p.To.Content != null)
                p.To.Content.anchoredPosition = new Vector2(enterFrom, p.To.Content.anchoredPosition.y);
            SetBlocks(p.To, false);
            SetBlocks(p.From, false);

            _skipEntry = true;
            if (!toGo.activeSelf) toGo.SetActive(true);
            _skipEntry = false;   // consumed by the target's OnEnable; never leaks to the next screen

            _active = p;

            LastPushElapsed     = 0f;
            LastPushFrames      = 0;
            LastPushCompleted   = false;
            LastPushEnterOffset = enterOffset;
            LastPushTargetRestX = p.To.RestX;
            LastPushLeaverRestX = p.From.RestX;

            // ── The tween ───────────────────────────────────────────────────
            float elapsed = 0f;
            while (elapsed < UiMotion.PushDur)
            {
                elapsed += Time.unscaledDeltaTime;
                LastPushElapsed = elapsed;
                LastPushFrames++;
                if (_active != p) yield break;   // an interrupting Navigate already settled us

                float t  = Mathf.Clamp01(elapsed / UiMotion.PushDur);
                float e  = UiMotion.EaseOut(t);
                float ef = Mathf.Clamp01(elapsed / UiMotion.FadeDur);
                float fe = UiMotion.EaseOut(ef);

                if (p.To.Content != null)
                    p.To.Content.anchoredPosition =
                        new Vector2(Mathf.Lerp(enterFrom, p.To.RestX, e), p.To.Content.anchoredPosition.y);

                if (p.From.Content != null)
                    p.From.Content.anchoredPosition =
                        new Vector2(Mathf.Lerp(p.From.RestX, p.From.RestX + leaveOffset, e),
                                    p.From.Content.anchoredPosition.y);

                // Incoming chrome dissolves in over the outgoing one, which stays fully opaque
                // (header, "compositing order"). The leaver's CONTENT fades with it so the
                // parallax reads as a departure rather than a cut once it is occluded.
                for (int i = 0; i < p.To.ChromeGroups.Count; i++) p.To.ChromeGroups[i].alpha = fe;
                if (p.From.ContentGroup != null) p.From.ContentGroup.alpha = 1f - fe;

                yield return null;
            }

            if (_active != p) yield break;
            LastPushCompleted = true;
            _active = null;
            Settle(p);
        }

        // ═════════════════════════════════════════════════════════════════════
        // Rest state
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Put BOTH screens back exactly as an <c>instant: true</c> navigation would leave them,
        /// then run the deferred ApplyScreen. Idempotent — an interrupting Navigate and the
        /// normal completion can both reach it.
        /// </summary>
        private static void Settle(Push_ p)
        {
            if (p.Settled) return;
            p.Settled = true;

            Rest(p.To);
            Rest(p.From);

            if (p.ToGo != null && p.ToSiblingIndex >= 0)
                p.ToGo.transform.SetSiblingIndex(p.ToSiblingIndex);

            // LAST. ApplyScreen deactivates the leaver, and a leaver deactivated before its rest
            // state is written would come back at −0.3·W with its content at alpha 0.
            p.Apply?.Invoke();
        }

        private static void Rest(Layer l)
        {
            if (l.Content != null)
                l.Content.anchoredPosition = new Vector2(l.RestX, l.Content.anchoredPosition.y);
            if (l.ContentGroup != null) l.ContentGroup.alpha = 1f;
            for (int i = 0; i < l.ChromeGroups.Count; i++) l.ChromeGroups[i].alpha = 1f;
            SetBlocks(l, true);
        }

        private static void SetBlocks(Layer l, bool blocks)
        {
            if (l.ContentGroup != null) l.ContentGroup.blocksRaycasts = blocks;
        }

        // ═════════════════════════════════════════════════════════════════════
        // Wiring
        // ═════════════════════════════════════════════════════════════════════

        private static void Collect(GameObject go, Layer l)
        {
            Transform content = go.transform.Find("ContentContainer");
            if (content != null)
            {
                l.Content      = content as RectTransform;
                l.ContentGroup = EnsureGroup(content.gameObject);
                if (l.Content != null) l.RestX = l.Content.anchoredPosition.x;
            }

            AddChrome(go, "Background", l);
            AddChrome(go, "GpsNavBar",  l);
            AddChrome(go, "BackPill",   l);
        }

        private static void AddChrome(GameObject go, string child, Layer l)
        {
            Transform t = FindLayer(go, child);
            if (t == null) return;                    // Golf Profile / Welcome have no nav bar
            l.ChromeGroups.Add(EnsureGroup(t.gameObject));
        }

        /// <summary>
        /// A top-level screen layer by name, wherever it currently sits.
        ///
        /// <para>THE ONE lookup for these four names, in the runtime AND in the editor builders.
        /// gps_polish §D9 puts <c>GpsNavBar</c> inside a stretched <c>NavSafeArea</c> wrapper so
        /// it can be inset above the home indicator, which moves it one level down. Every caller
        /// that had hard-coded <c>Find("GpsNavBar")</c> would then find nothing and log a warning
        /// rather than fail — the worst kind of break, so there is exactly one place that knows
        /// about the wrapper and this is it.</para>
        /// </summary>
        public static Transform? FindLayer(GameObject? go, string name)
        {
            if (go == null) return null;
            return go.transform.Find(name) ?? go.transform.Find("NavSafeArea/" + name);
        }

        /// <summary>
        /// The prefabs are authored with these CanvasGroups by <c>GpsPolishBuilder</c>. This is
        /// the safety net for the one GPS screen that has no builder at all — the hub was
        /// hand-built over MCP in <c>gps_hub_entry</c> — and for any prefab that misses a future
        /// builder pass. A CanvasGroup at alpha 1 / blocksRaycasts true is visually and
        /// behaviourally a no-op, so adding one at runtime cannot move a rest pixel.
        /// </summary>
        private static CanvasGroup EnsureGroup(GameObject go)
        {
            var cg = go.GetComponent<CanvasGroup>();
            if (cg == null) cg = go.AddComponent<CanvasGroup>();
            return cg;
        }

        /// <summary>
        /// How far the entering content starts off screen.
        ///
        /// <para>Deviation D-2: the spec says <c>W = ContentContainer.rect.width</c> (978). The
        /// containers are inset 96 px from the left of an 1170-wide canvas, so a 978 px offset
        /// leaves the last 96 px of the arriving screen ON SCREEN at t = 0 — a visible strip of
        /// the next screen sitting at the right edge before the push starts. The canvas width is
        /// used instead, which is the smallest offset that is actually off screen; the content
        /// width is the fallback when no canvas can be resolved.</para>
        /// </summary>
        private static float TravelWidth(GameObject go, Layer l)
        {
            var canvas = go.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                var crt = canvas.rootCanvas != null
                    ? canvas.rootCanvas.transform as RectTransform
                    : canvas.transform as RectTransform;
                if (crt != null && crt.rect.width > 1f) return crt.rect.width;
            }
            return l.Content != null && l.Content.rect.width > 1f ? l.Content.rect.width : 1170f;
        }
    }
}
