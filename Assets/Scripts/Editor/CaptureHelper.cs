using System.Collections;
using System.IO;
using UnityEditor;
using UnityEngine;
using Golfin.Gameplay.UI.HUD;
using Golfin.Gameplay.Session;
using Golfin.Diagnostics.Runtime;
// §2b: CaptureHelper is now a thin editor-side wrapper around Golfin.Diagnostics.Runtime.CaptureCore.
// The capture implementation lives in CaptureCore so it can be used from non-editor assemblies.

namespace Golfin.EditorTools
{
    /// <summary>
    /// Editor-side screenshot helper. Wraps CaptureCore from Golfin.Diagnostics.Runtime.
    /// Use SnapGameView from EditMode or paused playmode. Use SnapAtEndOfFrameAndPause from coroutines.
    ///
    /// BANNED: ScreenCapture.CaptureScreenshot(path) — async, fails silently when paused. Never use it.
    /// </summary>
    public static partial class CaptureHelper
    {
        const string OUT_DIR = CaptureCore.OutDir;

        // ────────────────────────────────────────────────────────────────────────
        // PRIMARY PATH — delegates to CaptureCore.
        // ────────────────────────────────────────────────────────────────────────
        [MenuItem("GOLFIN/Capture/Snap Game View %#&s")] // Ctrl+Shift+Alt+S
        public static string SnapGameView()
        {
            return SnapGameViewWithLabel("snap");
        }

        // ────────────────────────────────────────────────────────────────────────
        // Open the capture output folder in OS file browser (Explorer / Finder).
        // ────────────────────────────────────────────────────────────────────────
        [MenuItem("GOLFIN/Capture/Open Capture Folder")]
        public static void OpenCaptureFolder()
        {
            Directory.CreateDirectory(OUT_DIR);
            string absolute = Path.GetFullPath(OUT_DIR);
            EditorUtility.RevealInFinder(absolute);
            Debug.Log($"[CaptureHelper] Opened {absolute}");
        }

        public static string SnapGameViewWithLabel(string label)
            => CaptureCore.SnapGameViewWithLabel(label);

        // ────────────────────────────────────────────────────────────────────────
        // For coroutines that need to capture mid-animation, then freeze.
        // Delegates to CaptureCore.SnapAtEndOfFrameAndPause.
        // ────────────────────────────────────────────────────────────────────────
        public static IEnumerator SnapAtEndOfFrameAndPause(string label)
            => CaptureCore.SnapAtEndOfFrameAndPause(label);

        // ────────────────────────────────────────────────────────────────────────
        // For play-mode coroutines that need a captured frame + path string back,
        // without calling AssetDatabase.Refresh (which would force a domain reload
        // and kill the coroutine) or pausing the editor.
        // Delegates to CaptureCore.SnapPlayModeSafe.
        // ────────────────────────────────────────────────────────────────────────
        public static string SnapPlayModeSafe(string label)
            => CaptureCore.SnapPlayModeSafe(label);

        // ────────────────────────────────────────────────────────────────────────
        // FAKE STATE PRESETS
        // ────────────────────────────────────────────────────────────────────────

        [MenuItem("GOLFIN/Capture/Fake State Lock - ON")]
        public static void FakeStateLockOn()
        {
            FakeStateLock.IsLocked = true;
            Debug.Log("[FakeStateLock] ON — runtime populators will skip Refresh()");
            ReleaseMouseAfterMenu();
        }

        [MenuItem("GOLFIN/Capture/Fake State Lock - OFF")]
        public static void FakeStateLockOff()
        {
            FakeStateLock.IsLocked = false;
            Debug.Log("[FakeStateLock] OFF — runtime populators resume Refresh()");
            ReleaseMouseAfterMenu();
        }

        [MenuItem("GOLFIN/Capture/Fake State - Reset All")]
        public static void FakeReset()
        {
            FakeStateLock.IsLocked = false;

            PlayerContext.Reset();
            HoleContext.Reset();
            WindContext.Reset();
            GameSession.SetTurn(1);
            BallContext.Reset();
            ClubContext.EquippedBag.Clear();
            ClubContext.Reset();
            ShotModeContext.Reset();
            SpinContext.Reset();
            // 1v1 versus context — always reset alongside PlayerContext so fake-state
            // presets never leave stale MatchContext data in the bus.
            MatchContext.Reset();
            GameSession.IsVersus = false;
            Debug.Log("[FakeState:Reset] All contexts reset to defaults (including MatchContext)");
            ReleaseMouseAfterMenu();
        }

        [MenuItem("GOLFIN/Capture/Fake State - Mid Aim (Camila, Lomond H1, Driver, GOLFIN ball)")]
        public static void FakeMidAim()
        {
            FakeStateLock.IsLocked = true;

            PlayerContext.DisplayName = "CAMILA";
            PlayerContext.Level       = 13;
            PlayerContext.Portrait    = Resources.Load<Sprite>("Portraits/InGame/Camila");
            PlayerContext.Raise();

            // Mirror into MatchContext slot 0 so that if the versus HUD is visible,
            // it shows sensible P1 data (FakeReset has cleared IsVersus, so this is a
            // no-op for P1 card — but keeps the bus consistent).
            MatchContext.Players[0] = new MatchContext.Player
            {
                DisplayName = "CAMILA",
                Level       = 13,
                TurnCount   = 5
            };
            MatchContext.Raise();

            HoleContext.HoleNumber        = 1;
            HoleContext.Par               = 5;
            HoleContext.ChampionshipYards = 425;
            HoleContext.CourseName        = "LOMOND";
            HoleContext.TeeName           = "REGULAR";
            HoleContext.Raise();

            WindContext.SpeedMph         = 8f;
            WindContext.DirectionDegrees = 270f;
            WindContext.Raise();

            GameSession.SetTurn(5);

            BallContext.SelectedBallId          = "golfin";
            BallContext.SelectedNameLabel       = "GOLFIN";
            BallContext.SelectedQuantityDisplay = "∞";
            BallContext.RaiseSelectedChanged();

            var driverPortrait = Resources.Load<Sprite>("Clubs/Portraits/S_Menu_Driver_GOLFIN");
            ClubContext.EquippedBag.Clear();
            ClubContext.EquippedBag.Add(new ClubEntry {
                ClubId       = "driver_golfin",
                TypeLabel    = "DRIVER",
                Distance     = 230,
                Portrait     = driverPortrait,
                LabClubIndex = 0
            });
            ClubContext.SelectedClubId    = "driver_golfin";
            ClubContext.SelectedTypeLabel = "DRIVER";
            ClubContext.SelectedDistance  = 230;
            ClubContext.SelectedPortrait  = driverPortrait;
            ClubContext.SelectedIndex     = 0;
            ClubContext.RaiseBagChanged();
            ClubContext.RaiseSelectedChanged();

            ShotModeContext.Reset();
            SpinContext.SetSpin(Vector2.zero);

            Debug.Log("[FakeState:MidAim] Player=CAMILA Lv13 Hole=Lomond#1 Par5 425y Wind=8mph@270 Turn=5 Ball=GOLFIN Club=DRIVER 230y Mode=Straight Spin=(0,0) MatchContext.Players[0]=CAMILA Lv13");
            ReleaseMouseAfterMenu();
        }

        // ────────────────────────────────────────────────────────────────────────
        // FAKE STATE — 1v1 Versus mid-aim (Camila vs Taro, Lomond H1)
        // Sets IsVersus=true, populates MatchContext for both P1 and P2.
        // Designed for capturing the VersusHudController layout without matchmaking.
        // ────────────────────────────────────────────────────────────────────────
        [MenuItem("GOLFIN/Capture/Fake State - 1v1 Mid Aim (Camila vs Taro, Lomond H1)")]
        public static void Fake1v1MidAim()
        {
            FakeStateLock.IsLocked = true;
            GameSession.IsVersus   = true;

            // P1 (human — slot 0): Camila, active player.
            PlayerContext.DisplayName = "CAMILA";
            PlayerContext.Level       = 13;
            PlayerContext.Portrait    = Resources.Load<Sprite>("Portraits/InGame/Camila");
            PlayerContext.Raise();

            MatchContext.Players[0] = new MatchContext.Player
            {
                DisplayName = "CAMILA",
                Level       = 13,
                TurnCount   = 1
            };

            // P2 (opponent — slot 1): Taro, inactive player.
            MatchContext.Players[1] = new MatchContext.Player
            {
                DisplayName = "TARO",
                Level       = 17,
                TurnCount   = 0
            };

            MatchContext.ActiveIndex = 0;  // P1's turn.
            MatchContext.Raise();

            HoleContext.HoleNumber        = 1;
            HoleContext.Par               = 5;
            HoleContext.ChampionshipYards = 425;
            HoleContext.CourseName        = "LOMOND";
            HoleContext.TeeName           = "REGULAR";
            HoleContext.Raise();

            GameSession.SetTurn(1);

            Debug.Log("[FakeState:1v1MidAim] IsVersus=true P1=CAMILA Lv13 (active) P2=TARO Lv17 (inactive) Hole=Lomond#1 Turn=1");
            ReleaseMouseAfterMenu();
        }

        [MenuItem("GOLFIN/Capture/Fake State - Putt (Olivia, Lomond H7, Putter)")]
        public static void FakePutt()
        {
            FakeStateLock.IsLocked = true;

            PlayerContext.DisplayName = "OLIVIA";
            PlayerContext.Level       = 7;
            PlayerContext.Portrait    = Resources.Load<Sprite>("Portraits/InGame/Olivia");
            PlayerContext.Raise();

            HoleContext.HoleNumber        = 7;
            HoleContext.Par               = 4;
            HoleContext.ChampionshipYards = 380;
            HoleContext.CourseName        = "LOMOND";
            HoleContext.TeeName           = "REGULAR";
            HoleContext.Raise();

            WindContext.SpeedMph         = 0f;
            WindContext.DirectionDegrees = 0f;
            WindContext.Raise();

            GameSession.SetTurn(3);

            BallContext.SelectedBallId          = "golfin";
            BallContext.SelectedNameLabel       = "GOLFIN";
            BallContext.SelectedQuantityDisplay = "∞";
            BallContext.RaiseSelectedChanged();

            var putterPortrait = Resources.Load<Sprite>("Clubs/Portraits/S_Menu_Putter_GOLFIN");
            ClubContext.EquippedBag.Clear();
            ClubContext.EquippedBag.Add(new ClubEntry {
                ClubId       = "putter_golfin",
                TypeLabel    = "PUTTER",
                Distance     = 0,
                Portrait     = putterPortrait,
                LabClubIndex = 0
            });
            ClubContext.SelectedClubId    = "putter_golfin";
            ClubContext.SelectedTypeLabel = "PUTTER";
            ClubContext.SelectedDistance  = 0;
            ClubContext.SelectedPortrait  = putterPortrait;
            ClubContext.SelectedIndex     = 0;
            ClubContext.RaiseBagChanged();
            ClubContext.RaiseSelectedChanged();

            ShotModeContext.Reset();
            SpinContext.SetSpin(Vector2.zero);

            Debug.Log("[FakeState:Putt] Player=OLIVIA Lv7 Hole=Lomond#7 Par4 Turn=3 Wind=0 Club=PUTTER 0y Mode=Straight Spin=(0,0)");
            ReleaseMouseAfterMenu();
        }

        [MenuItem("GOLFIN/Capture/Fake State - Strong Wind (extreme indicator test)")]
        public static void FakeStrongWind()
        {
            FakeStateLock.IsLocked = true;

            WindContext.SpeedMph         = 25f;
            WindContext.DirectionDegrees = 135f;
            WindContext.Raise();
            Debug.Log("[FakeState:StrongWind] Wind=25mph@135");
            ReleaseMouseAfterMenu();
        }

        // ────────────────────────────────────────────────────────────────────────
        private static void ReleaseMouseAfterMenu()
        {
            GUIUtility.hotControl = 0;
            EditorApplication.delayCall += () =>
            {
                GUIUtility.hotControl = 0;
                var gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
                var gv = gameViewType != null
                    ? EditorWindow.GetWindow(gameViewType, false, null, false)
                    : null;
                gv?.Repaint();
            };
        }
    }
}
