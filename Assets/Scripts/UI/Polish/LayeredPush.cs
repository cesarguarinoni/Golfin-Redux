// ─────────────────────────────────────────────────────────────────────────────
// game_polish_a §D1 — the layered push between two GAME shell screens.
//
// THIS IS GpsScreenTransition'S SHAPE, NOT ITS CODE. The GPS surface is eight
// prefabs built by one builder to one layout contract: every screen has exactly
// `Background` + `ContentContainer`, so that file can name them as string
// literals and be done. The game shell is fourteen screens accreted over a year
// by different tasks — the chrome child is called `Background` on five of them,
// `BG` on six, and `RosterScreen` / the two StaminaShop screens have no chrome
// child at all — so the layer names have to be a TABLE (LayerMap) rather than
// two constants. Everything else is deliberately the same, and where it is, the
// GPS design point is named in a comment so the review can diff the two.
//
// KEPT FROM GpsScreenTransition (§D1's "cite which design points you kept"):
//   · The COMPOSITING ORDER (its deviation D-1). Two opaque full-screen sprites
//     cross-faded at 0.5 alpha do not composite to an opaque frame. The outgoing
//     chrome is held at 1 and the incoming one is faded in ON TOP of it, with the
//     target moved to last sibling for the duration. Here it matters even less
//     often, because on the shipped path the two chrome layers are THE SAME
//     SPRITE and neither is animated at all — but the flag path (§D4) needs it.
//   · TravelWidth from the CANVAS, not from the content rect (its deviation D-2).
//     Game content layers are inset too (1074 of 1170), so a 1074 px offset would
//     leave a 96 px strip of the arriving screen visible at t = 0.
//   · Offsets measured FROM each layer's own rest X, because the rest X is not
//     the same on every screen.
//   · apply() deferred to the END — running ApplyScreen at the midpoint, as the
//     boundary fade does, would deactivate the screen that is still sliding out.
//   · Rest state written on EVERY exit path, and the tween's own instrumentation
//     (LastPush*) published from INSIDE the loop, because an observer coroutine
//     starts a frame late and charges the expensive first frame to the wrong
//     stopwatch.
//   · One push at a time, no queue: CompleteActiveNow() snaps and runs the
//     deferred apply.
//
// NOT KEPT: the nav-slot ordering in DirectionFor. The GPS surface has an
// in-screen nav bar whose left-to-right order is a real spatial fact about where
// the player is going. The game's pillars have no such bar inside them — the
// shared PersistentUI bar is chrome that does not move — so direction here is
// simply forward-on-ShowScreen, back-on-GoBack (§D1.4).
//
// THE BACKGROUND RULE IS CESAR'S, AND IT IS THE POINT. The push runs ONLY when
// the two screens draw the SAME background sprite asset. A push whose backdrop
// changes reads as the room being swapped out from under the furniture; that
// move keeps the fade to black, which is the game-wide "you have left where you
// were" convention. SameBackground compares the Sprite REFERENCE, never the
// name: the Play screens' background and the Inventory screen's background are
// both assets called "Background" and they are not the same picture
// (2e5476ee… vs 44d64d73…).
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using GolfinRedux.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.UI.Polish
{
    /// <summary>
    /// The game-shell screen push. Driven by <see cref="ScreenManager"/>, which owns the
    /// coroutine; this class owns the layer table, the direction rule and the rest-state restore.
    /// </summary>
    public static class LayeredPush
    {
        private const string Tag = "[GamePush]";

        public enum Dir
        {
            /// <summary>Target enters from +W; the leaver parallaxes to −0.3·W.</summary>
            Forward,
            /// <summary>Mirrored.</summary>
            Back,
        }

        /// <summary>How far the leaving content drifts, as a fraction of the entering travel.
        /// Small on purpose: it is depth, not a second slide. GpsScreenTransition's number.</summary>
        public const float ParallaxFactor = 0.3f;

        // ═════════════════════════════════════════════════════════════════════
        // §D1.1 — the layer table
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>The chrome and content children of one shell screen, by NAME at depth 1.</summary>
        public readonly struct Layers
        {
            public readonly string[] Chrome;
            public readonly string[] Content;
            public Layers(string[] chrome, string[] content) { Chrome = chrome; Content = content; }
        }

        private static readonly string[] None = new string[0];

        /// <summary>
        /// ScreenId → its depth-1 chrome and content children. Measured live off ShellScene, not
        /// copied from the spec's table.
        ///
        /// <para>Four screens are deliberately absent and that absence IS the decision:
        /// <c>Roster</c> has no chrome child at all (the character stage renders behind it), and
        /// the two <c>StaminaShop</c> screens have no background child either — their backdrop
        /// lives inside nested prefabs. A screen with no chrome layer has nothing to hold still
        /// while the content slides, so the honest transition for it is the boundary fade.
        /// <c>Home</c> is absent because Cesar's rule puts every Home move on the fade.</para>
        ///
        /// <para>Note which children count as CONTENT rather than chrome: HoleSelection's
        /// <c>LeaderboardButton</c>, GeneralShop's <c>HistoryChip</c>, GachaHistory's
        /// <c>FiltersIconRow</c> and ModeSelection's <c>TournamentTempEntry</c> are all
        /// screen-local affordances that sit OUTSIDE the main content rect. They travel with the
        /// content; left behind, they would hang in mid-air over the arriving screen.</para>
        /// </summary>
        public static Layers? LayerMap(ScreenId id)
        {
            switch (id)
            {
                // ── the Play group (background 2e5476ee…) ────────────────────
                case ScreenId.ModeSelection:
                    return new Layers(new[] { "Background" }, new[] { "CardsContainer", "TournamentTempEntry" });
                case ScreenId.HoleSelection:
                    return new Layers(new[] { "Background" }, new[] { "Content", "LeaderboardButton" });
                case ScreenId.MissionSelection:
                    return new Layers(new[] { "Background" }, new[] { "Content", "RankingsButton" });
                case ScreenId.TournamentHoleSelection:
                    return new Layers(new[] { "Background" }, new[] { "Content", "LeaderboardButton" });

                // ── the Rankings group (background 0d425c0a…) ────────────────
                case ScreenId.TournamentSelection:
                    return new Layers(new[] { "BG" }, new[] { "ContentArea" });
                case ScreenId.TournamentLeaderboard:
                    return new Layers(new[] { "BG" }, new[] { "ContentArea", "BackButton" });
                case ScreenId.Leaderboard:
                    return new Layers(new[] { "BG" }, new[] { "ContentArea", "BackButton" });

                // ── the Gacha group (background 5ec22d10…) ───────────────────
                case ScreenId.GeneralShop:
                    return new Layers(new[] { "BG" }, new[] { "ContentArea", "HistoryChip" });
                case ScreenId.GachaHistory:
                    return new Layers(new[] { "Background" }, new[] { "GameScreenContent", "FiltersIconRow" });
                case ScreenId.GachaPrizes:
                    return new Layers(new[] { "Background" }, new[] { "GameScreenContent" });

                // ── Inventory: one pillar, one screen — no pair to push to.
                //    Listed anyway so HasSplit is honest about it and a future
                //    second Inventory screen inherits the right layers.
                //    TabBar and Rim are CHROME: they are the same control on every
                //    tab and sliding them would be motion with no meaning (the GPS
                //    nav-bar argument, GpsScreenTransition's header).
                case ScreenId.Inventory:
                    return new Layers(new[] { "BG", "Rim", "TabBar" }, new[] { "ContentArea" });

                default:
                    return null;
            }
        }

        /// <summary>Screens that share a backdrop but not a pillar. <c>Leaderboard</c> has no
        /// pillar of its own — it rides the history stack — yet it is the same room as the two
        /// tournament screens, so the three are treated as one push group (§D1.2).</summary>
        private static bool InRankingsGroup(ScreenId id)
            => id == ScreenId.TournamentSelection
            || id == ScreenId.TournamentLeaderboard
            || id == ScreenId.Leaderboard;

        /// <summary>True iff this screen really has the chrome / content split the design needs.</summary>
        public static bool HasSplit(ScreenId id, GameObject? go)
        {
            if (go == null) return false;
            Layers? l = LayerMap(id);
            if (l == null) return false;
            return FirstFound(go, l.Value.Chrome) != null && FirstFound(go, l.Value.Content) != null;
        }

        private static Transform? FirstFound(GameObject go, string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                Transform? t = go.transform.Find(names[i]);
                if (t != null) return t;
            }
            return null;
        }

        // ═════════════════════════════════════════════════════════════════════
        // §D1.2 — eligibility
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Whether this pair may be pushed rather than faded. False sends the caller straight back
        /// to the untouched <see cref="FadeController"/> path — so every reason here is a reason
        /// the push would look WORSE, not merely a reason it is hard.
        /// </summary>
        public static bool CanPush(ScreenId from, ScreenId to, GameObject? fromGo, GameObject? toGo)
        {
            if (!UiMotion.Enabled) return false;
            if (from == to) return false;

            // Cesar's rule, first and unconditional: every Home move goes through black.
            if (from == ScreenId.Home || to == ScreenId.Home) return false;

            // The GPS surface has its own push and its own branch, which runs before this one.
            if (Golfin.Gps.UI.GpsGate.IsGpsScreen(from) || Golfin.Gps.UI.GpsGate.IsGpsScreen(to)) return false;

            if (!ScreenManager.IsShell(from) || !ScreenManager.IsShell(to)) return false;

            // Same pillar, or the three-screen Rankings group that spans one.
            bool sameGroup = InRankingsGroup(from) && InRankingsGroup(to);
            if (!sameGroup)
            {
                var pa = ScreenManager.PillarOf(from);
                var pb = ScreenManager.PillarOf(to);
                if (pa == null || pb == null || pa.Value != pb.Value) return false;
            }

            if (!HasSplit(from, fromGo) || !HasSplit(to, toGo)) return false;

            // NO BACKGROUND GATE. Option (b) shipped (Cesar, 2026-09-04): two screens of the same
            // pillar push even when their backdrops differ, and the backdrops cross-fade through
            // each other. SameBackground is still consulted — inside Push, to decide whether the
            // chrome is animated at all — but it is no longer a reason to refuse.
            return true;
        }

        /// <summary>
        /// Do the two screens draw the same backdrop?
        ///
        /// <para>Compares the <see cref="Sprite"/> REFERENCE on each screen's first chrome Image,
        /// never the name. Two different assets in this project are both called "Background" —
        /// <c>Art/HoleSelectScreen/Background.png</c> (the Play screens) and
        /// <c>Art/ClubsInventory/Background.png</c> (Inventory) — so a name comparison would
        /// happily push between two visibly different rooms.</para>
        /// </summary>
        public static bool SameBackground(ScreenId from, ScreenId to, GameObject? fromGo, GameObject? toGo)
        {
            Sprite? a = ChromeSprite(from, fromGo);
            Sprite? b = ChromeSprite(to, toGo);
            if (a == null || b == null) return false;
            return ReferenceEquals(a, b);
        }

        private static Sprite? ChromeSprite(ScreenId id, GameObject? go)
        {
            if (go == null) return null;
            Layers? l = LayerMap(id);
            if (l == null) return null;
            Transform? t = FirstFound(go, l.Value.Chrome);
            var img = t != null ? t.GetComponent<Image>() : null;
            return img != null ? img.sprite : null;
        }

        // ═════════════════════════════════════════════════════════════════════
        // §D1.4 — direction
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Forward on a ShowScreen, Back on a GoBack. That is the whole rule, and it is simpler
        /// than the GPS one on purpose (see the header): the game's pillars have no in-screen nav
        /// bar whose left-to-right order could mean anything, so the only spatial fact available
        /// is whether the player is going deeper or coming back — which is exactly what
        /// <paramref name="push"/> already says. Pinned for every ordered pair by
        /// <c>LayeredPushTests</c>.
        /// </summary>
        public static Dir DirectionFor(ScreenId from, ScreenId to, bool push)
            => push ? Dir.Forward : Dir.Back;

        // ═════════════════════════════════════════════════════════════════════
        // §D2 — entry-motion hand-off
        // ═════════════════════════════════════════════════════════════════════

        private static bool _skipEntry;

        /// <summary>
        /// True for every component enabled by the <c>SetActive</c> inside a push, false
        /// everywhere else. A screen reached through a push has ALREADY animated its content in;
        /// running the 16 px Rise on top of the slide would be two entrances in 0.25 s.
        ///
        /// <para>Armed and disarmed AROUND that one call rather than cleared by the first reader —
        /// a screen can carry more than one component whose <c>OnEnable</c> wants to know, and a
        /// first-caller-wins flag would give the honest answer to whichever happened to run
        /// first. GpsScreenTransition's <c>EnteringViaPush</c>, same reasoning.</para>
        /// </summary>
        public static bool EnteringViaPush => _skipEntry;

        /// <summary>
        /// Test seam for <c>ScreenEntryMotionTests</c>: reads the flag and clears it, so a test can
        /// assert "consumed exactly once" without a play session. Production reads
        /// <see cref="EnteringViaPush"/>.
        /// </summary>
        internal static bool ConsumeSkipEntry()
        {
            bool v = _skipEntry;
            _skipEntry = false;
            return v;
        }

        /// <summary>Test seam: arm the flag as <see cref="Push"/> does.</summary>
        internal static void ArmSkipEntry(bool v) => _skipEntry = v;

        // ═════════════════════════════════════════════════════════════════════
        // §D1.3 — the push
        // ═════════════════════════════════════════════════════════════════════

        private sealed class Layer
        {
            public readonly List<RectTransform> Content = new List<RectTransform>();
            public readonly List<CanvasGroup>   ContentGroups = new List<CanvasGroup>();
            public readonly List<float>         RestX = new List<float>();
            public readonly List<CanvasGroup>   ChromeGroups = new List<CanvasGroup>();
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
        // Published from INSIDE the tween. An observer coroutine cannot measure this honestly: it
        // starts a frame late, and the first frame after a screen is enabled is the expensive one
        // (OnEnable, the first layout, the fetches). gps_polish timed a 0.25 s animation at 0.10 s
        // that way.

        /// <summary>Accumulated unscaled seconds of the most recent push.</summary>
        public static float LastPushElapsed { get; private set; }
        /// <summary>Frames the most recent push rendered.</summary>
        public static int LastPushFrames { get; private set; }
        /// <summary>Whether it ran to completion rather than being snapped by an interrupt.</summary>
        public static bool LastPushCompleted { get; private set; }
        /// <summary>The offset from rest the target content was placed at before the first frame
        /// was drawn — the t = 0 assertion, taken where it is actually true.</summary>
        public static float LastPushEnterOffset { get; private set; }
        /// <summary>Rest X of the two primary content rects, sampled BEFORE either was moved.</summary>
        public static float LastPushTargetRestX { get; private set; }
        public static float LastPushLeaverRestX { get; private set; }
        /// <summary>Travel width actually used.</summary>
        public static float LastPushWidth { get; private set; }
        /// <summary>Lowest chrome alpha seen on ANY frame of the most recent push. On the shipped
        /// (same-background) path this must stay exactly 1 — A5's assertion, taken from inside the
        /// loop rather than off a video.</summary>
        public static float LastPushChromeAlphaMin { get; private set; }
        /// <summary>Option (b) only: the worst per-frame max(fromChrome, toChrome). The seam test
        /// is "never both chrome layers below 0.5", i.e. this stays ≥ 0.5.</summary>
        public static float LastPushSeamWorstCover { get; private set; }

        /// <summary>
        /// Finish the running push NOW — snap everything to rest and run the deferred
        /// <c>ApplyScreen</c>. Called when a second navigation arrives mid-push (§D1.3: "no
        /// queue") and by <see cref="ScreenManager"/> before it starts anything else.
        /// </summary>
        public static void CompleteActiveNow()
        {
            if (_active == null) return;
            Push_ p = _active;
            _active = null;
            Settle(p);
        }

        /// <summary>
        /// The coroutine. <paramref name="apply"/> is <c>ScreenManager.ApplyScreen</c>, deferred to
        /// the END — running it at the midpoint would deactivate the screen that is still visibly
        /// sliding out.
        /// </summary>
        public static IEnumerator Push(ScreenId from, ScreenId to,
                                       GameObject fromGo, GameObject toGo, Dir dir, Action apply)
        {
            CompleteActiveNow();

            var p = new Push_ { FromGo = fromGo, ToGo = toGo, Apply = apply };
            Collect(from, fromGo, p.From);
            Collect(to,   toGo,   p.To);

            // Chrome only animates when the two backdrops actually differ — which, on the shipped
            // path, they never do. Same sprite ⇒ nothing to cross-fade and the seam invariant is
            // true by construction rather than by arithmetic that happens to sum to one.
            bool crossFadeChrome = !SameBackground(from, to, fromGo, toGo);

            float w = TravelWidth(toGo, p.To);
            float enterOffset = dir == Dir.Forward ?  w : -w;
            float leaveOffset = dir == Dir.Forward ? -w * ParallaxFactor : w * ParallaxFactor;

            Debug.Log($"{Tag} {from} -> {to} dir={dir} W={w:0.#} enterOffset={enterOffset:0.#} " +
                      $"leaveOffset={leaveOffset:0.#} chromeCrossFade={crossFadeChrome} dur={UiMotion.PushDur}");

            // ── Stage the target under the push's rules, THEN activate it ────
            // Order matters: SkipEntry must be armed before OnEnable runs, and the chrome must
            // already be at alpha 0 before the first frame the target is drawn.
            p.ToSiblingIndex = toGo.transform.GetSiblingIndex();
            if (crossFadeChrome) toGo.transform.SetAsLastSibling();

            if (crossFadeChrome)
                for (int i = 0; i < p.To.ChromeGroups.Count; i++) p.To.ChromeGroups[i].alpha = 0f;

            for (int i = 0; i < p.To.Content.Count; i++)
                p.To.Content[i].anchoredPosition =
                    new Vector2(p.To.RestX[i] + enterOffset, p.To.Content[i].anchoredPosition.y);

            SetBlocks(p.To, false);
            SetBlocks(p.From, false);

            _skipEntry = true;
            if (!toGo.activeSelf) toGo.SetActive(true);
            _skipEntry = false;   // consumed by the target's OnEnable; never leaks to the next screen

            _active = p;

            LastPushElapsed        = 0f;
            LastPushFrames         = 0;
            LastPushCompleted      = false;
            LastPushEnterOffset    = enterOffset;
            LastPushWidth          = w;
            LastPushTargetRestX    = p.To.RestX.Count   > 0 ? p.To.RestX[0]   : 0f;
            LastPushLeaverRestX    = p.From.RestX.Count > 0 ? p.From.RestX[0] : 0f;
            LastPushChromeAlphaMin = 1f;
            LastPushSeamWorstCover = 1f;

            // ── The tween ───────────────────────────────────────────────────
            float elapsed = 0f;
            while (elapsed < UiMotion.PushDur)
            {
                elapsed += Time.unscaledDeltaTime;
                LastPushElapsed = elapsed;
                LastPushFrames++;
                if (_active != p) yield break;   // an interrupting Navigate already settled us

                float e  = UiMotion.EaseOut(Mathf.Clamp01(elapsed / UiMotion.PushDur));
                float fe = UiMotion.EaseOut(Mathf.Clamp01(elapsed / UiMotion.FadeDur));

                for (int i = 0; i < p.To.Content.Count; i++)
                {
                    RectTransform r = p.To.Content[i];
                    r.anchoredPosition = new Vector2(
                        Mathf.Lerp(p.To.RestX[i] + enterOffset, p.To.RestX[i], e), r.anchoredPosition.y);
                }
                for (int i = 0; i < p.From.Content.Count; i++)
                {
                    RectTransform r = p.From.Content[i];
                    r.anchoredPosition = new Vector2(
                        Mathf.Lerp(p.From.RestX[i], p.From.RestX[i] + leaveOffset, e), r.anchoredPosition.y);
                }

                if (crossFadeChrome)
                {
                    // Compositing order (GpsScreenTransition's D-1): the leaver's chrome is held
                    // at 1 and the incoming one dissolves in ON TOP of it, so every frame is fully
                    // opaque. The leaver's CONTENT fades with it so the parallax reads as a
                    // departure rather than a cut once it is occluded.
                    for (int i = 0; i < p.To.ChromeGroups.Count; i++) p.To.ChromeGroups[i].alpha = fe;
                    for (int i = 0; i < p.From.ContentGroups.Count; i++) p.From.ContentGroups[i].alpha = 1f - fe;

                    // The seam invariant, MEASURED rather than restated from the code above.
                    // "Never both chrome layers below 0.5" means the max of the two must stay
                    // >= 0.5, so that is what is sampled, from the live CanvasGroups, every frame.
                    // (The first version wrote Mathf.Max(fe, 1f) — 1 by construction, measuring
                    // nothing. It mattered little while this path was behind an off-by-default
                    // flag; it is the shipped path now.)
                    float cover = Mathf.Max(MinAlpha(p.To.ChromeGroups), MinAlpha(p.From.ChromeGroups));
                    if (cover < LastPushSeamWorstCover) LastPushSeamWorstCover = cover;
                }

                // Only meaningful on the SAME-background path, where nothing should touch the
                // chrome at all. On the cross-fade path the incoming chrome is SUPPOSED to start at
                // 0, so a minimum of 0 there is the feature working — the seam cover above is the
                // assertion that applies to that path.
                if (!crossFadeChrome)
                {
                    float minAlpha = Mathf.Min(MinAlpha(p.To.ChromeGroups), MinAlpha(p.From.ChromeGroups));
                    if (minAlpha < LastPushChromeAlphaMin) LastPushChromeAlphaMin = minAlpha;
                }

                yield return null;
            }

            if (_active != p) yield break;
            LastPushCompleted = true;
            _active = null;
            Settle(p);
        }

        private static float MinAlpha(List<CanvasGroup> gs)
        {
            float m = 1f;
            for (int i = 0; i < gs.Count; i++) if (gs[i] != null && gs[i].alpha < m) m = gs[i].alpha;
            return m;
        }

        // ═════════════════════════════════════════════════════════════════════
        // Rest state
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Put BOTH screens back exactly as an <c>instant: true</c> navigation would leave them,
        /// then run the deferred ApplyScreen. Idempotent — an interrupting Navigate and the normal
        /// completion can both reach it. A2 pixel-diffs an animated arrival against
        /// <c>ShowScreen(instant: true)</c> for exactly this reason.
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
            for (int i = 0; i < l.Content.Count; i++)
                if (l.Content[i] != null)
                    l.Content[i].anchoredPosition = new Vector2(l.RestX[i], l.Content[i].anchoredPosition.y);
            for (int i = 0; i < l.ContentGroups.Count; i++)
                if (l.ContentGroups[i] != null) l.ContentGroups[i].alpha = 1f;
            for (int i = 0; i < l.ChromeGroups.Count; i++)
                if (l.ChromeGroups[i] != null) l.ChromeGroups[i].alpha = 1f;
            SetBlocks(l, true);
        }

        private static void SetBlocks(Layer l, bool blocks)
        {
            for (int i = 0; i < l.ContentGroups.Count; i++)
                if (l.ContentGroups[i] != null) l.ContentGroups[i].blocksRaycasts = blocks;
        }

        // ═════════════════════════════════════════════════════════════════════
        // Wiring
        // ═════════════════════════════════════════════════════════════════════

        private static void Collect(ScreenId id, GameObject go, Layer l)
        {
            Layers? map = LayerMap(id);
            if (map == null) return;

            foreach (string name in map.Value.Content)
            {
                Transform? t = go.transform.Find(name);
                if (t == null) continue;
                var rt = t as RectTransform;
                if (rt == null) continue;
                l.Content.Add(rt);
                l.RestX.Add(rt.anchoredPosition.x);
                l.ContentGroups.Add(EnsureGroup(t.gameObject));
            }

            foreach (string name in map.Value.Chrome)
            {
                Transform? t = go.transform.Find(name);
                if (t == null) continue;
                l.ChromeGroups.Add(EnsureGroup(t.gameObject));
            }
        }

        /// <summary>
        /// The CanvasGroups are authored by <c>GamePolishBuilder</c>. This is the safety net for a
        /// screen the builder has not been run over and for any screen added later. A CanvasGroup
        /// at alpha 1 / blocksRaycasts true is visually and behaviourally a no-op, so adding one at
        /// runtime cannot move a rest pixel. GpsScreenTransition's <c>EnsureGroup</c>, verbatim
        /// reasoning.
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
        /// <para>GpsScreenTransition's deviation D-2, and it applies here for the same reason: the
        /// game's content layers are 1074 px inside an 1170 px canvas, so an offset of the CONTENT
        /// width would leave a 96 px strip of the arriving screen visible at t = 0 — a sliver of
        /// the next screen parked at the edge before the push starts. The canvas width is the
        /// smallest offset that is actually off screen; the content width is the fallback when no
        /// canvas can be resolved.</para>
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
            return l.Content.Count > 0 && l.Content[0].rect.width > 1f ? l.Content[0].rect.width : 1170f;
        }
    }
}
