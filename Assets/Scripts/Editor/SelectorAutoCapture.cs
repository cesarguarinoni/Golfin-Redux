using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Golfin.Gameplay.UI.ShotUI;
using Golfin.Gameplay.UI.HUD;
using Golfin.EditorTools;

/// <summary>
/// AUTO-CAPTURE: Fires once after assembly reload (edit mode only).
/// Multi-phase via EditorApplication.update so Unity's render loop can flush
/// CanvasGroup alpha changes and layout rebuilds into the Game View RT before capture.
/// Delete Docs/Diagnostics/_capture/_auto_selector_done.flag to re-arm.
/// Iteration 3: recompile trigger 2026-04-30 to fire after Unity returned to Edit mode.
/// </summary>
[InitializeOnLoad]
public static class SelectorAutoCapture
{
    const string DONE_FLAG = "Docs/Diagnostics/_capture/_auto_selector_done.flag";

    static SelectorAutoCapture()
    {
        if (System.IO.File.Exists(DONE_FLAG)) return;
        EditorApplication.update += Phase1_Setup;
    }

    // ── PHASE 1: Wait a few ticks, then set up state + open club overlay ────────
    static int _setupTick = 0;
    static void Phase1_Setup()
    {
        _setupTick++;
        if (_setupTick < 5) return;  // let Unity initialize for 5 ticks

        // Cannot run in play mode — builder uses EditorSceneManager which is blocked.
        // Stay registered and retry next tick when play mode ends.
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        EditorApplication.update -= Phase1_Setup;

        var canvasGo = GameObject.Find("ShotUI_Canvas");
        if (canvasGo == null)
        {
            Debug.Log("[SelectorAutoCapture] ShotUI_Canvas not found. Ensure LabScaffold.unity is open.");
            return;
        }

        // Rebuild action buttons cluster with latest code (arrow fix: preserveAspect=false, correct rotation)
        Debug.Log("[SelectorAutoCapture] Rebuilding ActionButtons_Cluster with arrow fix...");
        ActionButtonsBuilder.BuildActionButtonsNoDialog();
        AssetDatabase.Refresh();

        // Re-find canvas after rebuild (GOs may have been destroyed + recreated)
        canvasGo = GameObject.Find("ShotUI_Canvas");
        if (canvasGo == null) { Debug.LogError("[SelectorAutoCapture] ShotUI_Canvas lost after rebuild."); return; }

        // Setup ClubContext
        FakeStateLock.IsLocked = true;

        var driverPortrait = Resources.Load<Sprite>("Clubs/Portraits/S_Menu_Driver_GOLFIN");
        var ironPortrait   = Resources.Load<Sprite>("Clubs/Portraits/S_Menu_Iron_GOLFIN");
        var woodPortrait   = Resources.Load<Sprite>("Clubs/Portraits/S_Menu_Wood_GF");
        var putterPortrait = Resources.Load<Sprite>("Clubs/Portraits/S_Menu_Putter_GOLFIN");

        ClubContext.EquippedBag.Clear();
        ClubContext.EquippedBag.Add(new ClubEntry { ClubId = "wood_golfin",   TypeLabel = "WOOD",   Distance = 230, Portrait = woodPortrait,   LabClubIndex = 1 });
        ClubContext.EquippedBag.Add(new ClubEntry { ClubId = "iron_golfin",   TypeLabel = "IRON",   Distance = 180, Portrait = ironPortrait,   LabClubIndex = 2 });
        ClubContext.EquippedBag.Add(new ClubEntry { ClubId = "putter_golfin", TypeLabel = "PUTTER", Distance = 30,  Portrait = putterPortrait, LabClubIndex = 3 });
        ClubContext.EquippedBag.Add(new ClubEntry { ClubId = "driver_golfin", TypeLabel = "DRIVER", Distance = 250, Portrait = driverPortrait, LabClubIndex = 0 });
        ClubContext.SelectedIndex     = 3;
        ClubContext.SelectedClubId    = "driver_golfin";
        ClubContext.SelectedTypeLabel = "DRIVER";
        ClubContext.SelectedDistance  = 250;
        ClubContext.SelectedPortrait  = driverPortrait;
        ClubContext.RaiseBagChanged();
        ClubContext.RaiseSelectedChanged();

        // Open club overlay
        var overlayT = canvasGo.transform.Find("SelectorOverlay");
        if (overlayT == null) { Debug.LogError("[SelectorAutoCapture] SelectorOverlay not found."); return; }
        var overlay = overlayT.GetComponent<SelectorOverlayWidget>();
        if (overlay == null) { Debug.LogError("[SelectorAutoCapture] SelectorOverlayWidget not found."); return; }
        overlay.Open(SelectorOverlayWidget.Kind.Club);

        // Apply fader: hide Driver button, fade others to 0.5
        var clusterT = canvasGo.transform.Find("ActionButtons_Cluster");
        if (clusterT != null)
        {
            var fader = clusterT.GetComponent<OtherButtonsFader>();
            var driverT = clusterT.Find("DriverButton");
            if (fader != null && driverT != null)
            {
                var driverCg = driverT.GetComponent<CanvasGroup>();
                if (driverCg != null) fader.FadeAllExcept(driverCg);
            }
        }

        // Force layout rebuild
        var overlayRt = overlayT.GetComponent<RectTransform>();
        var allRts = overlayRt.GetComponentsInChildren<RectTransform>(false);
        for (int i = allRts.Length - 1; i >= 0; i--)
            LayoutRebuilder.ForceRebuildLayoutImmediate(allRts[i]);
        LayoutRebuilder.ForceRebuildLayoutImmediate(overlayRt);

        // Request a repaint so the RT gets updated
        var gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
        if (gameViewType != null)
        {
            var gv = EditorWindow.GetWindow(gameViewType, false, null, false);
            gv?.Repaint();
        }

        Debug.Log("[SelectorAutoCapture] Phase1 done. Waiting 10 ticks before capture...");

        // Move to Phase 2 (wait 10 more ticks to let the repaint flush)
        EditorApplication.update += Phase2_CaptureClub;
    }

    // ── PHASE 2: Wait 10 ticks, then capture club selector ──────────────────────
    static int _captureTick = 0;
    static void Phase2_CaptureClub()
    {
        _captureTick++;
        if (_captureTick < 10) return;

        EditorApplication.update -= Phase2_CaptureClub;

        // Capture club selector
        string clubPath = CaptureHelper.SnapGameViewWithLabel("selector_club_v6_8_5c");
        Debug.Log($"[SelectorAutoCapture] Club selector captured: {clubPath}");

        // Close and restore
        var canvasGo = GameObject.Find("ShotUI_Canvas");
        if (canvasGo != null)
        {
            var overlayT = canvasGo.transform.Find("SelectorOverlay");
            var overlay = overlayT?.GetComponent<SelectorOverlayWidget>();
            overlay?.Close();

            var clusterT = canvasGo.transform.Find("ActionButtons_Cluster");
            var fader = clusterT?.GetComponent<OtherButtonsFader>();
            fader?.RestoreAll();
        }

        // Move to Phase 3: set up ball overlay
        EditorApplication.update += Phase3_SetupBall;
    }

    // ── PHASE 3: Set up ball overlay ─────────────────────────────────────────────
    static int _ballSetupTick = 0;
    static void Phase3_SetupBall()
    {
        _ballSetupTick++;
        if (_ballSetupTick < 5) return;

        EditorApplication.update -= Phase3_SetupBall;

        var canvasGo = GameObject.Find("ShotUI_Canvas");
        if (canvasGo == null) { FinishCapture(); return; }

        // Set up BallContext
        var golfinThumb = Resources.Load<Sprite>("Balls/Thumbnails/S_Controls_Ball_GOLFIN");
        BallContext.OwnedBalls.Clear();
        BallContext.OwnedBalls.Add(new BallEntry { BallId = "golfin",   NameLabel = "GOLFIN",   QuantityDisplay = "∞", Thumbnail = golfinThumb });
        BallContext.OwnedBalls.Add(new BallEntry { BallId = "putt_ace", NameLabel = "PUTT ACE", QuantityDisplay = "3", Thumbnail = golfinThumb });
        BallContext.SelectedBallId          = "golfin";
        BallContext.SelectedNameLabel       = "GOLFIN";
        BallContext.SelectedQuantityDisplay = "∞";
        BallContext.SelectedIndex           = 0;
        BallContext.RaiseSelectedChanged();

        // Open ball overlay
        var ballOverlayT = canvasGo.transform.Find("SelectorOverlay_Ball");
        if (ballOverlayT == null) { Debug.LogError("[SelectorAutoCapture] SelectorOverlay_Ball not found."); FinishCapture(); return; }
        var ballOverlay = ballOverlayT.GetComponent<SelectorOverlayWidget>();
        if (ballOverlay == null) { FinishCapture(); return; }
        ballOverlay.Open(SelectorOverlayWidget.Kind.Ball);

        // Apply fader: hide Golfin button
        var clusterT = canvasGo.transform.Find("ActionButtons_Cluster");
        if (clusterT != null)
        {
            var fader = clusterT.GetComponent<OtherButtonsFader>();
            var golfinT = clusterT.Find("GolfinButton");
            if (fader != null && golfinT != null)
            {
                var golfinCg = golfinT.GetComponent<CanvasGroup>();
                if (golfinCg != null) fader.FadeAllExcept(golfinCg);
            }
        }

        // Force layout rebuild
        var overlayRt = ballOverlayT.GetComponent<RectTransform>();
        var allRts = overlayRt.GetComponentsInChildren<RectTransform>(false);
        for (int i = allRts.Length - 1; i >= 0; i--)
            LayoutRebuilder.ForceRebuildLayoutImmediate(allRts[i]);
        LayoutRebuilder.ForceRebuildLayoutImmediate(overlayRt);

        // Request repaint
        var gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
        if (gameViewType != null)
        {
            var gv = EditorWindow.GetWindow(gameViewType, false, null, false);
            gv?.Repaint();
        }

        EditorApplication.update += Phase4_CaptureBall;
    }

    // ── PHASE 4: Wait 10 ticks, then capture ball selector ──────────────────────
    static int _ballCaptureTick = 0;
    static void Phase4_CaptureBall()
    {
        _ballCaptureTick++;
        if (_ballCaptureTick < 10) return;

        EditorApplication.update -= Phase4_CaptureBall;

        string ballPath = CaptureHelper.SnapGameViewWithLabel("selector_ball_v6_8_5c");
        Debug.Log($"[SelectorAutoCapture] Ball selector captured: {ballPath}");

        // Close and restore
        var canvasGo = GameObject.Find("ShotUI_Canvas");
        if (canvasGo != null)
        {
            var ballOverlayT = canvasGo.transform.Find("SelectorOverlay_Ball");
            var ballOverlay = ballOverlayT?.GetComponent<SelectorOverlayWidget>();
            ballOverlay?.Close();

            var clusterT = canvasGo.transform.Find("ActionButtons_Cluster");
            var fader = clusterT?.GetComponent<OtherButtonsFader>();
            fader?.RestoreAll();
        }

        FinishCapture();
    }

    static void FinishCapture()
    {
        FakeStateLock.IsLocked = false;
        System.IO.Directory.CreateDirectory("Docs/Diagnostics/_capture");
        System.IO.File.WriteAllText(DONE_FLAG, System.DateTime.Now.ToString("O"));
        Debug.Log("[SelectorAutoCapture] All captures done. Done flag written.");
    }
}

