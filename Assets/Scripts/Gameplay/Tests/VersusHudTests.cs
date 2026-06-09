using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TestTools;
using Golfin.Gameplay.Session;
using Golfin.Gameplay.UI.HUD;
using Golfin.Gameplay.UI.ShotUI;
using System.Collections;
using System.Reflection;
using TMPro;

namespace Golfin.Gameplay.Tests
{
    /// <summary>
    /// EditMode tests for the 1v1 versus HUD spec (Phase 1), iter-2.
    ///
    /// Coverage:
    ///   1. Solo path: PlayerCardWidget.Refresh() uses PlayerContext when IsVersus=false.
    ///      Verifies alpha stays 1.0 (no CanvasGroup dimming in solo).
    ///   2. Versus path: PlayerCardWidget.Refresh() reads MatchContext when IsVersus=true.
    ///      Verifies _playerIndex=0 reads Players[0], _playerIndex=1 reads Players[1].
    ///   3. Versus path: inactive card gets alpha 0.50, active card gets alpha 1.0.
    ///   4. GameSession.ResetSession() clears IsVersus.
    ///   5. MatchContext.Reset() clears both player slots.
    ///   6. MatchContext.SetActive() updates ActiveIndex and fires OnActiveChanged.
    ///   7. ResetSession → MatchContext.Reset() both invoked so alpha reverts to 1.0.
    ///
    /// NOTE: Tests 1–3 and 7 construct a real PlayerCardWidget GameObject to exercise
    /// the actual code paths (not just data assertions).
    ///
    /// Construction order: go is disabled before AddComponent so Awake/OnEnable are
    /// deferred. After SetActive(true) all Awake/OnEnable fire, but TMP's OnEnable
    /// re-initialises the mesh buffer and can overwrite text set by PlayerCardWidget's
    /// OnEnable. To isolate Refresh() behaviour, the tests explicitly call Refresh()
    /// via reflection after activation — this is the sanctioned EditMode pattern for
    /// testing Unity UI components with deferred Awake/OnEnable ordering.
    /// </summary>
    [TestFixture]
    public class VersusHudTests
    {
        static readonly BindingFlags _flags =
            BindingFlags.NonPublic | BindingFlags.Instance;

        // ── Helper: build a minimal PlayerCardWidget on a new Canvas root ─────────
        // Uses TextMeshProUGUI (concrete) for the text fields; the PlayerCardWidget
        // serialised field type is TMP_Text (abstract base class), which accepts a UGUI.
        //
        // Returns the Canvas root as 'root' so callers can DestroyImmediate(root)
        // in their finally blocks to clean up the entire hierarchy.
        static (GameObject root, GameObject go, PlayerCardWidget widget,
                CanvasGroup cg, TMP_Text nameTxt, TMP_Text lvlTxt, TMP_Text turnTxt)
            MakeCard(int playerIndex)
        {
            // Canvas root (needed so TMP components have a valid Canvas ancestor).
            var root   = new GameObject("TestCanvas_P" + playerIndex);
            root.AddComponent<Canvas>();

            var go = new GameObject("TestCard_P" + playerIndex);
            go.transform.SetParent(root.transform, false);

            var cg = go.AddComponent<CanvasGroup>();

            // Disable BEFORE AddComponent<PlayerCardWidget> so Awake/OnEnable
            // are deferred until we call go.SetActive(true) after wiring.
            go.SetActive(false);

            var widget = go.AddComponent<PlayerCardWidget>();
            var type   = typeof(PlayerCardWidget);

            // Explicitly wire _canvasGroup via reflection.
            // Awake() calls GetComponent<CanvasGroup>() but Unity's Awake-on-inactive
            // behaviour is version-dependent — in some builds it fires immediately on
            // AddComponent, in others only on SetActive. Wiring it here guarantees
            // RefreshAlpha() has a valid _canvasGroup regardless of Unity version.
            type.GetField("_canvasGroup", _flags)?.SetValue(widget, cg);

            // Attach text children to inactive parent — their Awake/OnEnable
            // will also fire when go.SetActive(true) is called.
            var nameTxtGo = new GameObject("Name");
            nameTxtGo.transform.SetParent(go.transform, false);
            TMP_Text nameTxt = nameTxtGo.AddComponent<TextMeshProUGUI>();
            type.GetField("_nameText", _flags)?.SetValue(widget, nameTxt);

            var lvlTxtGo = new GameObject("Level");
            lvlTxtGo.transform.SetParent(go.transform, false);
            TMP_Text lvlTxt = lvlTxtGo.AddComponent<TextMeshProUGUI>();
            type.GetField("_levelText", _flags)?.SetValue(widget, lvlTxt);

            var turnTxtGo = new GameObject("Turn");
            turnTxtGo.transform.SetParent(go.transform, false);
            TMP_Text turnTxt = turnTxtGo.AddComponent<TextMeshProUGUI>();
            type.GetField("_turnText", _flags)?.SetValue(widget, turnTxt);

            type.GetField("_playerIndex", _flags)?.SetValue(widget, playerIndex);

            return (root, go, widget, cg, nameTxt, lvlTxt, turnTxt);
        }

        // Activate the card (fires Awake + OnEnable) then explicitly invoke Refresh()
        // again to work around TMP's OnEnable re-initialising the text buffer after
        // PlayerCardWidget.OnEnable has already written to it.
        static void ActivateAndRefresh(GameObject go, PlayerCardWidget widget)
        {
            go.SetActive(true);   // Awake + OnEnable + TMP.OnEnable
            // TMP.OnEnable re-zeroes the mesh buffer. Calling Refresh() again ensures
            // the test reads the value that the widget would show after full init.
            typeof(PlayerCardWidget)
                .GetMethod("Refresh", _flags)
                ?.Invoke(widget, null);
        }

        [SetUp]
        public void SetUp()
        {
            GameSession.ResetSession();
            MatchContext.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            GameSession.ResetSession();
            MatchContext.Reset();
        }

        // ── Test 1: Solo path — PlayerCardWidget reads PlayerContext, alpha stays 1 ──
        [Test]
        public void SoloPath_PlayerCardWidget_ReadsPlayerContext_AlphaOne()
        {
            GameSession.IsVersus = false;
            PlayerContext.DisplayName = "MAYA";
            PlayerContext.Level       = 7;
            PlayerContext.Raise();

            var (root, go, widget, cg, nameTxt, lvlTxt, _) = MakeCard(0);

            try
            {
                ActivateAndRefresh(go, widget);

                Assert.AreEqual("MAYA", nameTxt.text,
                    "Solo path: _nameText should reflect PlayerContext.DisplayName.");
                Assert.AreEqual("Lv 7", lvlTxt.text,
                    "Solo path: _levelText should reflect PlayerContext.Level.");
                Assert.AreEqual(1f, cg.alpha, 0.01f,
                    "Solo path: CanvasGroup.alpha should be 1.0 (no dimming in solo).");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // ── Test 2: Versus path — P1 widget reads MatchContext.Players[0] ─────────
        [Test]
        public void VersusPath_P1Widget_ReadsMatchContextSlot0()
        {
            GameSession.IsVersus = true;
            MatchContext.Players[0] = new MatchContext.Player
            {
                DisplayName = "CAMILA", Level = 13, TurnCount = 2
            };
            MatchContext.ActiveIndex = 0;
            MatchContext.Raise();

            var (root, go, widget, cg, nameTxt, lvlTxt, turnTxt) = MakeCard(0);

            try
            {
                ActivateAndRefresh(go, widget);

                Assert.AreEqual("CAMILA", nameTxt.text,
                    "Versus P1: _nameText must read MatchContext.Players[0].DisplayName.");
                Assert.AreEqual("Lv 13", lvlTxt.text,
                    "Versus P1: _levelText must read MatchContext.Players[0].Level.");
                Assert.AreEqual("TURN 2", turnTxt.text,
                    "Versus P1: _turnText must read MatchContext.Players[0].TurnCount.");
                Assert.AreEqual(1f, cg.alpha, 0.01f,
                    "Versus P1 (active): CanvasGroup.alpha should be 1.0.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // ── Test 3: Versus path — inactive card has alpha 0.50 ───────────────────
        [Test]
        public void VersusPath_InactiveCard_AlphaFifty()
        {
            GameSession.IsVersus = true;
            MatchContext.Players[1] = new MatchContext.Player
            {
                DisplayName = "TARO", Level = 17, TurnCount = 0
            };
            MatchContext.ActiveIndex = 0;  // P1's turn → P2 inactive.
            MatchContext.Raise();

            var (root, go, widget, cg, nameTxt, _, _) = MakeCard(1);

            try
            {
                ActivateAndRefresh(go, widget);

                Assert.AreEqual("TARO", nameTxt.text,
                    "Versus P2 inactive: _nameText must read MatchContext.Players[1].DisplayName.");
                Assert.AreEqual(0.50f, cg.alpha, 0.01f,
                    "Versus P2 (inactive): CanvasGroup.alpha must be 0.50 per spec.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // ── Test 4: ResetSession clears IsVersus ──────────────────────────────────
        [Test]
        public void ResetSession_ClearsIsVersus()
        {
            GameSession.IsVersus = true;
            GameSession.ResetSession();
            Assert.IsFalse(GameSession.IsVersus,
                "GameSession.ResetSession() must clear IsVersus to false.");
        }

        // ── Test 5: MatchContext.Reset() clears both slots ────────────────────────
        [Test]
        public void MatchContextReset_ClearsBothSlots()
        {
            MatchContext.Players[0] = new MatchContext.Player { DisplayName = "P1", Level = 10 };
            MatchContext.Players[1] = new MatchContext.Player { DisplayName = "P2", Level = 42 };
            MatchContext.ActiveIndex = 1;

            MatchContext.Reset();

            Assert.AreEqual(default(MatchContext.Player), MatchContext.Players[0],
                "MatchContext.Reset() must clear Players[0].");
            Assert.AreEqual(default(MatchContext.Player), MatchContext.Players[1],
                "MatchContext.Reset() must clear Players[1].");
            Assert.AreEqual(0, MatchContext.ActiveIndex,
                "MatchContext.Reset() must reset ActiveIndex to 0.");
        }

        // ── Test 6: SetActive fires OnActiveChanged and updates index ─────────────
        [Test]
        public void SetActive_UpdatesIndexAndFiresEvent()
        {
            bool eventFired = false;
            System.Action handler = () => eventFired = true;
            MatchContext.OnActiveChanged += handler;

            try
            {
                MatchContext.SetActive(1);

                Assert.AreEqual(1, MatchContext.ActiveIndex,
                    "SetActive(1) must set ActiveIndex to 1.");
                Assert.IsTrue(eventFired,
                    "SetActive must fire OnActiveChanged event.");
            }
            finally
            {
                MatchContext.OnActiveChanged -= handler;
            }
        }

        // ── Test 7: After ResetSession + MatchContext.Reset, card alpha reverts ───
        [Test]
        public void AfterReset_VersusCard_AlphaReverts()
        {
            // Start in versus mode with P2 inactive.
            GameSession.IsVersus = true;
            MatchContext.Players[1] = new MatchContext.Player { DisplayName = "TARO", Level = 17 };
            MatchContext.ActiveIndex = 0;
            MatchContext.Raise();

            var (root, go, widget, cg, _, _, _) = MakeCard(1);

            try
            {
                ActivateAndRefresh(go, widget);
                // Confirm it started at 0.50 (P2 inactive).
                Assert.AreEqual(0.50f, cg.alpha, 0.01f, "Should start at 0.50 (inactive).");

                // Reset both session and match context.
                GameSession.ResetSession();
                MatchContext.Reset();

                // Re-enable: triggers OnDisable (cleanup) then OnEnable (re-subscribe solo path).
                go.SetActive(false);
                ActivateAndRefresh(go, widget);

                // After reset, IsVersus=false → solo path → no dimming.
                Assert.AreEqual(1f, cg.alpha, 0.01f,
                    "After reset (IsVersus=false), CanvasGroup.alpha should be 1.0 on re-enable.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
