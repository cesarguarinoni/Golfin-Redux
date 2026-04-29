using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Golfin.Gameplay.UI.HUD;
// Attempt 2 - Y-flip applied

namespace Golfin.EditorTools
{
    /// <summary>
    /// Synchronous screenshot helper. Replaces ScreenCapture.CaptureScreenshot (async, unreliable).
    /// Use SnapGameView from EditMode or paused playmode. Use SnapAtEndOfFrameAndPause from coroutines.
    ///
    /// BANNED: ScreenCapture.CaptureScreenshot(path) — async, fails silently when paused. Never use it.
    /// </summary>
    public static partial class CaptureHelper
    {
        const string OUT_DIR = "Docs/Diagnostics/_capture";

        // ────────────────────────────────────────────────────────────────────────
        // PRIMARY PATH — call this from EditMode, paused playmode, OR running playmode.
        // Returns the absolute path of the written PNG. Synchronous. Always works.
        // ────────────────────────────────────────────────────────────────────────
        [MenuItem("GOLFIN/Capture/Snap Game View %#&s")] // Ctrl+Shift+Alt+S
        public static string SnapGameView()
        {
            return SnapGameViewWithLabel("snap");
        }

        /// <summary>
        /// Attempts to read the GameView's internal RenderTexture via reflection.
        /// This is the only reliable way to capture Game View content in the Unity Editor —
        /// ScreenCapture.CaptureScreenshotAsTexture() reads the OS display swap chain, NOT
        /// the Game View RT, so it produces black or Editor chrome in the editor.
        /// </summary>
        private static Texture2D GrabGameViewRT()
        {
            var gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            if (gameViewType == null) return null;

            var gv = EditorWindow.GetWindow(gameViewType, false, null, false);
            if (gv == null) return null;

            gv.Focus();
            gv.Repaint();

            // Try field names used across Unity versions
            string[] candidates = { "m_RenderTexture", "m_TargetTexture", "m_RenderTarget" };
            RenderTexture rt = null;
            foreach (var name in candidates)
            {
                var f = gameViewType.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
                rt = f?.GetValue(gv) as RenderTexture;
                if (rt != null && rt.IsCreated()) break;
            }

            if (rt == null) return null;

            // ReadPixels reads in OpenGL coordinate space (Y=0 at bottom).
            // Unity's RenderTexture origin is bottom-left, so the image comes out Y-flipped.
            // We flip it back here so the PNG is right-side-up.
            int w = rt.width, h = rt.height;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;

            // Flip Y
            var pixels = tex.GetPixels();
            var flipped = new UnityEngine.Color[pixels.Length];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    flipped[y * w + x] = pixels[(h - 1 - y) * w + x];
            tex.SetPixels(flipped);
            tex.Apply();

            return tex;
        }

        public static string SnapGameViewWithLabel(string label)
        {
            Directory.CreateDirectory(OUT_DIR);
            string path = $"{OUT_DIR}/{label}_{System.DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png";

            // Primary path: read the GameView's internal RenderTexture via reflection.
            // ScreenCapture.CaptureScreenshotAsTexture() reads the OS display swap chain,
            // NOT the Game View RT — so it produces black or Editor chrome in the editor.
            Texture2D tex = GrabGameViewRT();

            if (tex != null)
            {
                Debug.Log("[CaptureHelper] Using RT reflection path (GameView RenderTexture)");
            }
            else
            {
                // Fallback: reflection failed (e.g., future Unity version renamed the field).
                // This reads the swap chain — may be black or show Editor chrome in editor mode.
                Debug.LogWarning("[CaptureHelper] GameView RT reflection failed — falling back to ScreenCapture.CaptureScreenshotAsTexture(). Result may be black or show editor chrome if not in fullscreen playmode.");
                tex = ScreenCapture.CaptureScreenshotAsTexture();
            }

            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.Refresh();
            Debug.Log($"[CaptureHelper] Wrote {path}");
            return Path.GetFullPath(path);
        }

        // ────────────────────────────────────────────────────────────────────────
        // For coroutines that need to capture mid-animation, then freeze.
        // CRITICAL: this captures FIRST, pauses AFTER. Never the other way around.
        // ────────────────────────────────────────────────────────────────────────
        public static IEnumerator SnapAtEndOfFrameAndPause(string label)
        {
            yield return new WaitForEndOfFrame();
            Directory.CreateDirectory(OUT_DIR);
            string path = $"{OUT_DIR}/{label}_f{Time.frameCount}.png";

            // In coroutine context (end-of-frame), the swap chain IS the game frame —
            // fallback to CaptureScreenshotAsTexture is acceptable here since this runs
            // inside the render loop.
            Texture2D tex = GrabGameViewRT();
            if (tex == null)
                tex = ScreenCapture.CaptureScreenshotAsTexture();

            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            EditorApplication.isPaused = true;
            AssetDatabase.Refresh();
            Debug.Log($"[CaptureHelper] Wrote {path} and paused");
        }

        // ────────────────────────────────────────────────────────────────────────
        // FAKE STATE PRESETS
        // Each preset populates static-bus contexts in Golfin.Gameplay.UI.HUD so
        // UI widgets can be verified without entering playmode. Every preset ends
        // with a single Debug.Log line summarising what was injected.
        //
        // STANDING RULE: when a new *Context.cs is added to HUD/, extend:
        //   1. FakeReset  — add Reset() call
        //   2. FakeMidAim — add sensible values + update the closing Debug.Log
        //   3. Add a dedicated preset if the context has interesting variation
        // ────────────────────────────────────────────────────────────────────────

        [MenuItem("GOLFIN/Capture/Fake State - Reset All")]
        public static void FakeReset()
        {
            PlayerContext.Reset();
            HoleContext.Reset();
            WindContext.Reset();
            GameSession.SetTurn(1);
            BallContext.Reset();
            ClubContext.EquippedBag.Clear();
            ClubContext.Reset();
            ShotModeContext.Reset();
            SpinContext.Reset();
            Debug.Log("[FakeState:Reset] All contexts reset to defaults");
            ReleaseMouseAfterMenu();
        }

        [MenuItem("GOLFIN/Capture/Fake State - Mid Aim (Camila, Lomond H1, Driver, GOLFIN ball)")]
        public static void FakeMidAim()
        {
            // Fix 3: use InGame portraits (not Thumbnails)
            PlayerContext.DisplayName = "CAMILA";
            PlayerContext.Level       = 13;
            PlayerContext.Portrait    = Resources.Load<Sprite>("Portraits/InGame/Camila");
            PlayerContext.Raise();

            HoleContext.HoleNumber        = 1;
            HoleContext.Par               = 5;
            HoleContext.ChampionshipYards = 425;
            HoleContext.CourseName        = "LOMOND";
            HoleContext.TeeName           = "REGULAR";
            HoleContext.Raise();

            WindContext.SpeedMph         = 8f;
            WindContext.DirectionDegrees = 270f; // wind from West
            WindContext.Raise();

            GameSession.SetTurn(5);

            // Ball: GOLFIN selection. Thumbnail/sprite null is OK — widgets fall back to defaults.
            BallContext.SelectedBallId          = "golfin";
            BallContext.SelectedNameLabel       = "GOLFIN";
            BallContext.SelectedQuantityDisplay = "∞";
            BallContext.RaiseSelectedChanged();

            // Fix 2: inject real club portrait sprite + populate EquippedBag
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
            ClubContext.RaiseBagChanged();       // fires OnBagChanged — widget refreshes bag list
            ClubContext.RaiseSelectedChanged();  // fires OnSelectedChanged — widget refreshes selected display

            // ShotMode: Straight (aim state). Reset() sets Mode=Straight and fires OnChanged.
            ShotModeContext.Reset();

            // Spin: center (no spin). SetSpin fires OnChanged.
            SpinContext.SetSpin(Vector2.zero);

            Debug.Log("[FakeState:MidAim] Player=CAMILA Lv13 Hole=Lomond#1 Par5 425y Wind=8mph@270 Turn=5 Ball=GOLFIN Club=DRIVER 230y Mode=Straight Spin=(0,0)");
            ReleaseMouseAfterMenu();
        }

        [MenuItem("GOLFIN/Capture/Fake State - Putt (Olivia, Lomond H7, Putter)")]
        public static void FakePutt()
        {
            // Fix 3: use InGame portraits (not Thumbnails)
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

            // Fix 2: inject real putter portrait sprite + populate EquippedBag
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
            ClubContext.SelectedDistance  = 0; // putters don't carry a distance label
            ClubContext.SelectedPortrait  = putterPortrait;
            ClubContext.SelectedIndex     = 0;
            ClubContext.RaiseBagChanged();
            ClubContext.RaiseSelectedChanged();

            // ShotMode: Straight (putts don't use fade/draw). Reset() sets Straight + fires.
            ShotModeContext.Reset();

            // Spin: center
            SpinContext.SetSpin(Vector2.zero);

            Debug.Log("[FakeState:Putt] Player=OLIVIA Lv7 Hole=Lomond#7 Par4 Turn=3 Wind=0 Club=PUTTER 0y Mode=Straight Spin=(0,0)");
            ReleaseMouseAfterMenu();
        }

        [MenuItem("GOLFIN/Capture/Fake State - Strong Wind (extreme indicator test)")]
        public static void FakeStrongWind()
        {
            WindContext.SpeedMph         = 25f;
            WindContext.DirectionDegrees = 135f; // SE
            WindContext.Raise();
            Debug.Log("[FakeState:StrongWind] Wind=25mph@135");
            ReleaseMouseAfterMenu();
        }

        // ────────────────────────────────────────────────────────────────────────
        // When a MenuItem executes via mouse click, the Editor never delivers the
        // corresponding MouseUp to the Game View — Unity's input state thinks the
        // button is still held, so moving back to the canvas pans the camera.
        // Resetting hotControl + queuing a deferred Repaint releases the stuck state.
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
