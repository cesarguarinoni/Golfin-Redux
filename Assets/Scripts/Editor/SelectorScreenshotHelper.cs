using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using Golfin.Gameplay.UI.ShotUI;
using Golfin.Gameplay.UI.HUD;
using Golfin.EditorTools;

/// <summary>
/// One-shot editor helper for 8.5.C selector screenshot.
/// Captures both the club (Driver) selector and ball (Golfin) selector.
/// Uses a deferred snap (EditorApplication.delayCall) so Unity gets two repaint cycles
/// between the layout/state change and the actual pixel read — this prevents stale frames.
///
/// Menu: GOLFIN/Capture/Selector - Club (Driver side)
///       GOLFIN/Capture/Selector - Ball (Golfin side)
/// </summary>
public static class SelectorScreenshotHelper
{
    // ── Club (Driver) selector capture ────────────────────────────────────────

    [MenuItem("GOLFIN/Capture/Selector - Club (Driver side)")]
    public static void CaptureClubSelector()
    {
        // 1. Rebuild action buttons cluster
        ActionButtonsBuilder.BuildActionButtonsNoDialog();
        AssetDatabase.Refresh();

        // 2. Inject 4 clubs into ClubContext via FakeState lock
        FakeStateLock.IsLocked = true;

        var driverPortrait = Resources.Load<Sprite>("Clubs/Portraits/S_Menu_Driver_GOLFIN");
        var ironPortrait   = Resources.Load<Sprite>("Clubs/Portraits/S_Menu_Iron_GOLFIN");
        var woodPortrait   = Resources.Load<Sprite>("Clubs/Portraits/S_Menu_Wood_GF");
        var putterPortrait = Resources.Load<Sprite>("Clubs/Portraits/S_Menu_Putter_GOLFIN");

        ClubContext.EquippedBag.Clear();
        // Order: WOOD first (idx 0), IRON (1), PUTTER (2), DRIVER (3 = selected).
        // Populate() puts non-selected first then selected at bottom → DRIVER ends up at bottom card.
        ClubContext.EquippedBag.Add(new ClubEntry { ClubId = "wood_golfin",   TypeLabel = "WOOD",   Distance = 230, Portrait = woodPortrait,   LabClubIndex = 1 });
        ClubContext.EquippedBag.Add(new ClubEntry { ClubId = "iron_golfin",   TypeLabel = "IRON",   Distance = 180, Portrait = ironPortrait,   LabClubIndex = 2 });
        ClubContext.EquippedBag.Add(new ClubEntry { ClubId = "putter_golfin", TypeLabel = "PUTTER", Distance = 30,  Portrait = putterPortrait, LabClubIndex = 3 });
        ClubContext.EquippedBag.Add(new ClubEntry { ClubId = "driver_golfin", TypeLabel = "DRIVER", Distance = 250, Portrait = driverPortrait, LabClubIndex = 0 });
        // DRIVER is at bag index 3
        ClubContext.SelectedClubId    = "driver_golfin";
        ClubContext.SelectedTypeLabel = "DRIVER";
        ClubContext.SelectedDistance  = 250;
        ClubContext.SelectedPortrait  = driverPortrait;
        ClubContext.SelectedIndex     = 3;
        ClubContext.RaiseBagChanged();
        ClubContext.RaiseSelectedChanged();

        var canvasGo = GameObject.Find("ShotUI_Canvas");
        if (canvasGo == null)
        {
            Debug.LogError("[SelectorScreenshotHelper] ShotUI_Canvas not found.");
            return;
        }

        // 3. Find and open the club selector overlay
        var overlayT = canvasGo.transform.Find("SelectorOverlay");
        if (overlayT == null)
        {
            Debug.LogError("[SelectorScreenshotHelper] SelectorOverlay not found. Run builder first.");
            return;
        }
        var overlay = overlayT.GetComponent<SelectorOverlayWidget>();
        if (overlay == null)
        {
            Debug.LogError("[SelectorScreenshotHelper] SelectorOverlayWidget component not found.");
            return;
        }
        overlay.Open(SelectorOverlayWidget.Kind.Club);

        // 4. Apply OtherButtonsFader so other buttons fade and trigger button is hidden.
        //    DRIVER's CanvasGroup gets alpha=0, all others get alpha=0.5.
        var clusterT = canvasGo.transform.Find("ActionButtons_Cluster");
        if (clusterT != null)
        {
            var fader = clusterT.GetComponent<OtherButtonsFader>();
            if (fader != null)
            {
                var driverT = clusterT.Find("DriverButton");
                if (driverT != null)
                {
                    var driverCg = driverT.GetComponent<CanvasGroup>();
                    if (driverCg != null)
                        fader.FadeAllExcept(driverCg);
                    else
                        Debug.LogWarning("[SelectorScreenshotHelper] DriverButton has no CanvasGroup.");
                }
            }
        }

        // 5. Force layout rebuild: inner CSFs first, then overlay root.
        ForceRebuildOverlay(overlayT.GetComponent<RectTransform>());

        // 6. Deferred snap — give Unity two more repaint cycles so all layout/alpha
        //    changes are committed to the RenderTexture before ReadPixels.
        SnapDeferred("selector_club_8_5c");
    }

    // ── Ball (Golfin) selector capture ────────────────────────────────────────

    [MenuItem("GOLFIN/Capture/Selector - Ball (Golfin side)")]
    public static void CaptureBallSelector()
    {
        // 1. Rebuild
        ActionButtonsBuilder.BuildActionButtonsNoDialog();
        AssetDatabase.Refresh();

        // 2. Inject 2 balls into BallContext
        FakeStateLock.IsLocked = true;

        var golfinThumb = Resources.Load<Sprite>("Balls/Thumbnails/S_Controls_Ball_GOLFIN");

        BallContext.OwnedBalls.Clear();
        BallContext.OwnedBalls.Add(new BallEntry { BallId = "golfin",   NameLabel = "GOLFIN",   QuantityDisplay = "∞", Thumbnail = golfinThumb });
        BallContext.OwnedBalls.Add(new BallEntry { BallId = "putt_ace", NameLabel = "PUTT ACE", QuantityDisplay = "3", Thumbnail = golfinThumb });
        BallContext.SelectedBallId          = "golfin";
        BallContext.SelectedNameLabel       = "GOLFIN";
        BallContext.SelectedQuantityDisplay = "∞";
        BallContext.SelectedIndex           = 0;
        BallContext.RaiseSelectedChanged();

        var canvasGo = GameObject.Find("ShotUI_Canvas");
        if (canvasGo == null)
        {
            Debug.LogError("[SelectorScreenshotHelper] ShotUI_Canvas not found.");
            return;
        }

        // 3. Find and open the ball selector overlay
        var overlayT = canvasGo.transform.Find("SelectorOverlay_Ball");
        if (overlayT == null)
        {
            Debug.LogError("[SelectorScreenshotHelper] SelectorOverlay_Ball not found. Run builder first.");
            return;
        }
        var overlay = overlayT.GetComponent<SelectorOverlayWidget>();
        if (overlay == null)
        {
            Debug.LogError("[SelectorScreenshotHelper] SelectorOverlayWidget (ball) not found.");
            return;
        }
        overlay.Open(SelectorOverlayWidget.Kind.Ball);

        // 4. Apply fader: GolfinButton is the trigger → alpha=0; others at 0.5
        var clusterT = canvasGo.transform.Find("ActionButtons_Cluster");
        if (clusterT != null)
        {
            var fader = clusterT.GetComponent<OtherButtonsFader>();
            if (fader != null)
            {
                var golfinT = clusterT.Find("GolfinButton");
                if (golfinT != null)
                {
                    var golfinCg = golfinT.GetComponent<CanvasGroup>();
                    if (golfinCg != null)
                        fader.FadeAllExcept(golfinCg);
                }
            }
        }

        // 5. Force layout rebuild
        ForceRebuildOverlay(overlayT.GetComponent<RectTransform>());

        // 6. Deferred snap
        SnapDeferred("selector_ball_8_5c");
    }

    // ── Layout rebuild helper ─────────────────────────────────────────────────

    /// <summary>
    /// Forces ContentSizeFitter and LayoutGroup rebuilds on the overlay and all children.
    /// Two passes: inner containers first, then overlay root.
    /// </summary>
    static void ForceRebuildOverlay(RectTransform overlayRt)
    {
        if (overlayRt == null) return;

        // Pass 1: rebuild all children bottom-up (innermost first) so CSFs resolve
        var allRts = overlayRt.GetComponentsInChildren<RectTransform>(includeInactive: false);
        for (int i = allRts.Length - 1; i >= 0; i--)
            LayoutRebuilder.ForceRebuildLayoutImmediate(allRts[i]);

        // Pass 2: overlay root picks up final child sizes
        LayoutRebuilder.ForceRebuildLayoutImmediate(overlayRt);
    }

    // ── Deferred snap ─────────────────────────────────────────────────────────

    /// <summary>
    /// Schedules two nested delayCall frames before snapping.
    /// This allows Unity's repaint loop to flush layout changes and alpha values
    /// into the Game View RenderTexture before CaptureHelper.SnapGameViewWithLabel reads pixels.
    /// Without this, the RT may contain a stale frame (alpha not yet applied, layout not rebuilt).
    /// </summary>
    static void SnapDeferred(string label)
    {
        // Frame 1: schedule frame 2
        EditorApplication.delayCall += () =>
        {
            // Frame 2: actually snap
            EditorApplication.delayCall += () =>
            {
                string path = CaptureHelper.SnapGameViewWithLabel(label);
                Debug.Log($"[SelectorScreenshotHelper] Captured: {path}");
            };
        };
    }

    // ── Legacy combined helper (kept for backward compat) ─────────────────────

    [MenuItem("GOLFIN/Capture/Selector - Build + Open + Snap (legacy)")]
    public static void BuildOpenAndSnap()
    {
        CaptureClubSelector();
    }
}
