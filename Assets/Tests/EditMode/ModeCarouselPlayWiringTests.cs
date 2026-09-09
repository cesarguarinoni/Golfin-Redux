// ─────────────────────────────────────────────────────────────────────────────
// The home carousel's PLAY button, and the one invariant that makes it safe to
// press: A CARD SHOWING A LIVE PLAY BUTTON MUST HAVE SOMEBODY LISTENING TO IT.
//
// The carousel loops by instantiating the mode list THREE times and sliding a
// window over the middle copy. For a year it subscribed OnPlayClicked on pass 1
// only, on the reasoning that the centred card is always a pass-1 instance. That
// is true of a SETTLED carousel — NormalizeCenterInstant folds the centre index
// back into the middle third after every snap — and false of a MOVING one:
// tapping a side card centres the tapped instance itself, a pass-0 or pass-2
// clone, and ApplyCardStates lights its PLAY button for the length of the slide.
//
// Press it in that window and the click ran the entire spend path — the entry fee
// left the player's balance, server-side — and then invoked an event with no
// subscribers, so nothing navigated. 10 RP for a button that did nothing, twice
// reproduced on 2026-09-09.
//
// So the assertion here is not "pass 1 is subscribed". It is the invariant, swept
// over EVERY centre index the carousel can hold, including the ones the old code
// only reached mid-animation. Wire a fourth pass, add a fifth entry point, change
// which clone ApplyCardStates lights up — this still asks the only question that
// matters.
//
// Reached by reflection into Assembly-CSharp, the way ModesOverlayTests and
// ModesFallbackCsvTests reach the same namespace: an asmdef-based test assembly
// cannot reference a predefined assembly.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace GolfinRedux.Tests.EditMode
{
    [TestFixture]
    public class ModeCarouselPlayWiringTests
    {
        private const BindingFlags NP = BindingFlags.NonPublic | BindingFlags.Instance;
        private const string HomeCardPrefab = "Assets/Prefabs/UI/ModeSelect/ModeHomeCard.prefab";

        private static readonly Type? _carouselType =
            Type.GetType("GolfinRedux.UI.ModeSelect.ModeCarouselController, Assembly-CSharp");

        private static readonly Type? _cardType =
            Type.GetType("GolfinRedux.UI.ModeSelect.ModeCardController, Assembly-CSharp");

        private static readonly Type? _dbType =
            Type.GetType("GolfinRedux.UI.ModeSelect.ModesDatabaseCSV, Assembly-CSharp");

        private static readonly Type? _stateType =
            Type.GetType("GolfinRedux.UI.ModeSelect.ModeCardState, Assembly-CSharp");

        // A preview scene, not the active one: this fixture instantiates fifteen card prefabs and
        // the point is that ShellScene never notices. (tests-run refuses to start on a dirty scene.)
        private Scene _scene;
        private object? _previousDbInstance;

        [SetUp]
        public void SetUp()
        {
            // Reflection that silently resolves to null is the failure mode where a renamed type
            // makes every assertion below vacuous and the suite goes green on nothing.
            Assert.IsNotNull(_carouselType, "ModeCarouselController not found in Assembly-CSharp");
            Assert.IsNotNull(_cardType,     "ModeCardController not found in Assembly-CSharp");
            Assert.IsNotNull(_dbType,       "ModesDatabaseCSV not found in Assembly-CSharp");
            Assert.IsNotNull(_stateType,    "ModeCardState not found in Assembly-CSharp");

            _scene = EditorSceneManager.NewPreviewScene();
            _previousDbInstance = DbInstanceProperty.GetValue(null);
        }

        [TearDown]
        public void TearDown()
        {
            // The singleton is global state this fixture borrows; hand it back whatever it held.
            DbInstanceProperty.GetSetMethod(nonPublic: true)!.Invoke(null, new[] { _previousDbInstance });
            if (_scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene);
        }

        private static PropertyInfo DbInstanceProperty =>
            _dbType!.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)!;

        // ── Harness ───────────────────────────────────────────────────────────

        /// <summary>
        /// A ModesDatabaseCSV that has parsed the real bundled CSV and claimed Instance.
        ///
        /// Awake does not run in EditMode, so its two jobs are done by hand — and Instance is one
        /// of them here, unlike in ModesOverlayTests, because RebuildCards reads the singleton.
        /// </summary>
        private int LoadDatabase()
        {
            var host = new GameObject("ModesDatabaseCSV (test)");
            SceneManager.MoveGameObjectToScene(host, _scene);
            object db = host.AddComponent(_dbType!);

            DbInstanceProperty.GetSetMethod(nonPublic: true)!.Invoke(null, new[] { db });
            _dbType!.GetMethod("LoadFromCSV", NP)!.Invoke(db, Array.Empty<object>());

            var modes = (IEnumerable)_dbType.GetMethod("GetAllModes", BindingFlags.Public | BindingFlags.Instance)!
                                            .Invoke(db, Array.Empty<object>())!;
            return modes.Cast<object>().Count();
        }

        /// <summary>
        /// A carousel that has run its real RebuildCards against the real home-card prefab —
        /// the production build path, not a re-implementation of it. Returns the built clones.
        /// </summary>
        private (object carousel, IList cards) BuildCarousel()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HomeCardPrefab);
            Assert.IsNotNull(prefab, $"Home card prefab missing at {HomeCardPrefab}");

            var root = new GameObject("ModeCarousel (test)", typeof(RectTransform));
            SceneManager.MoveGameObjectToScene(root, _scene);
            var container = new GameObject("CardsContainer", typeof(RectTransform));
            container.transform.SetParent(root.transform, worldPositionStays: false);

            object carousel = root.AddComponent(_carouselType!);
            _carouselType!.GetField("cardsContainer", NP)!.SetValue(carousel, container.GetComponent<RectTransform>());
            _carouselType.GetField("cardPrefab", NP)!.SetValue(carousel, prefab!.GetComponent(_cardType!));

            _carouselType.GetMethod("RebuildCards", NP)!.Invoke(carousel, Array.Empty<object>());

            var cards = (IList)_carouselType.GetField("_allCards", NP)!.GetValue(carousel)!;
            return (carousel, cards);
        }

        private static bool PlayIsLive(object card)
        {
            var play = (Button?)_cardType!.GetField("playButton", NP)!.GetValue(card);
            return play != null && play.gameObject.activeInHierarchy && play.IsInteractable();
        }

        private static bool HasPlaySubscriber(object card) =>
            (Delegate?)_cardType!.GetField("OnPlayClicked", NP)!.GetValue(card) != null;

        // ── The invariant ─────────────────────────────────────────────────────

        [Test]
        public void EveryCloneShowingPlayHasALivePlayClickedSubscriber()
        {
            int modeCount = LoadDatabase();
            var (carousel, cards) = BuildCarousel();

            // The 3x virtual array. If this stops being 3x the sweep below still covers whatever
            // it became, but the count is worth pinning: it is the reason clones exist at all.
            Assert.AreEqual(modeCount * 3, cards.Count,
                "Carousel should build one clone per mode per pass");

            var centreField = _carouselType!.GetField("_centeredVirtualIndex", NP)!;
            var applyStates = _carouselType.GetMethod("ApplyCardStates", NP)!;
            var modeIdProp  = _cardType!.GetProperty("ModeId")!;

            var violations = new List<string>();

            // Sweep EVERY centre the carousel can hold. Passes 0 and 2 are not hypothetical: a tap
            // on a side card centres that clone directly, and it stays centred for the whole slide.
            for (int centre = 0; centre < cards.Count; centre++)
            {
                centreField.SetValue(carousel, centre);
                applyStates.Invoke(carousel, Array.Empty<object>());

                for (int i = 0; i < cards.Count; i++)
                {
                    object card = cards[i]!;
                    if (!PlayIsLive(card) || HasPlaySubscriber(card)) continue;
                    violations.Add(
                        $"centre={centre} (pass {centre / modeCount}) -> card[{i}] " +
                        $"(pass {i / modeCount}, id='{modeIdProp.GetValue(card)}') " +
                        "shows PLAY with no OnPlayClicked subscriber");
                }
            }

            Assert.IsEmpty(violations,
                "A live PLAY button with no subscriber charges the entry fee and navigates " +
                "nowhere. Offenders:\n  " + string.Join("\n  ", violations));
        }

        /// <summary>
        /// The backstop, from the other side. Even if some future surface shows a card with PLAY
        /// live and forgets to subscribe, the click must not reach the spend. This drives the REAL
        /// button's onClick — not the private handler — so it also proves the prefab's wiring.
        /// </summary>
        [Test]
        public void PlayWithNoSubscriberRefusesToSpend()
        {
            LoadDatabase();
            var (_, cards) = BuildCarousel();

            // A zero-fee mode, so the affordability guard above cannot be what returns: that would
            // make this test pass without ever reaching the subscriber check.
            object card = cards.Cast<object>().First(c =>
                (string)_cardType!.GetProperty("ModeId")!.GetValue(c)! == "missions");

            // Awake is what puts HandlePlayButtonClicked behind the button, and EditMode does not
            // run it on an instantiated clone. Call the real one, so the Invoke below travels the
            // production route — prefab button -> onClick -> handler — rather than reflecting
            // straight into the private handler and proving nothing about the prefab.
            _cardType!.GetMethod("Awake", NP)!.Invoke(card, Array.Empty<object>());
            _cardType.GetField("OnPlayClicked", NP)!.SetValue(card, null);

            var play = (Button?)_cardType.GetField("playButton", NP)!.GetValue(card);
            Assert.IsNotNull(play, "Home card prefab has no playButton wired");

            // If the click never reaches the handler this fails as "expected log not received",
            // so it doubles as the proof that the button is actually armed.
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(
                "no OnPlayClicked subscriber — refusing to spend"));

            play!.onClick.Invoke();
        }

        /// <summary>
        /// Hiding the screen mid-slide kills the snap coroutine without running the line that
        /// clears _isSnapping, and OnBeginDrag / HandleCardTapped both read it as "an animation
        /// owns the carousel" — so the carousel came back from the next Home visit frozen. Tapping
        /// a card and then PLAY is exactly that sequence, and it only became reachable once PLAY
        /// started working on side clones.
        /// </summary>
        [Test]
        public void EnablingTheCarouselClearsStaleAnimationLatches()
        {
            LoadDatabase();
            var (carousel, _) = BuildCarousel();

            var snapping = _carouselType!.GetField("_isSnapping", NP)!;
            var dragging = _carouselType.GetField("_isDragging", NP)!;
            var layout   = _carouselType.GetField("_layoutAnim", NP)!;

            snapping.SetValue(carousel, true);
            dragging.SetValue(carousel, true);

            _carouselType.GetMethod("OnEnable", NP)!.Invoke(carousel, Array.Empty<object>());

            Assert.IsFalse((bool)snapping.GetValue(carousel)!, "_isSnapping survived OnEnable");
            Assert.IsFalse((bool)dragging.GetValue(carousel)!, "_isDragging survived OnEnable");
            Assert.IsNull(layout.GetValue(carousel), "_layoutAnim survived OnEnable");
        }
    }
}
