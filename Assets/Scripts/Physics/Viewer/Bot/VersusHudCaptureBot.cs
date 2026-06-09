using System.Collections;
using UnityEngine;
using Golfin.Gameplay.Session;
using Golfin.Gameplay.UI.HUD;
using Golfin.Gameplay.UI.ShotUI;
using Golfin.Diagnostics.Runtime;

namespace Golfin.Physics.Viewer
{
    /// <summary>
    /// Play-mode coroutine bot for the 1v1 versus HUD video deliverables (Phase 1).
    ///
    /// Injected at EnteredPlayMode by VersusHudCaptureMenu (editor-side).
    /// The editor launcher sets Scenario + Armed via static properties before
    /// EditorApplication.EnterPlaymode().  No SessionState needed — the bot is
    /// always created immediately after the launcher sets the values in the same
    /// process (no domain-reload between arm and Start).
    ///
    /// DEFECT-1 FIX (iter-5): MatchContext + PlayerContext are seeded BEFORE
    /// the bot's Start() runs — seeding happens in VersusHudCaptureMenu's
    /// EnteredPlayMode callback right before this component is injected.
    /// That means VersusHudController.OnMatchContextChanged fires with real data
    /// on the FIRST frame, so both cards are full from frame 1 before any banner.
    ///
    /// Scenarios:
    ///   "versus_launch"  — activate 1v1 state, hold 4 s, capture still + exit.
    ///   "turn_swap"      — activate 1v1, wait 2 s, SetActive(1), wait 2 s, SetActive(0), exit.
    ///   "banner_show"    — activate 1v1, show banner, wait for full anim (~2.5 s), exit.
    ///   "solo_regression"— solo (IsVersus=false), hold 3 s, capture still + exit.
    /// </summary>
    public class VersusHudCaptureBot : MonoBehaviour
    {
        // Plain static fields — no SessionState (which is UnityEditor-only).
        // The editor launcher writes these before entering play mode; Start() reads
        // them in the same process so no persistence across domain reloads is needed.
        public static string Scenario { get; set; } = "";
        public static bool   Armed    { get; set; }

        void Start()
        {
            if (!Armed) return;
            Armed = false;   // clear immediately to prevent re-trigger on domain reload
            StartCoroutine(RunScenario(Scenario));
        }

        IEnumerator RunScenario(string scenario)
        {
            // DEFECT-1 FIX: Do NOT wait 2s before seeding — data was already seeded
            // in VersusHudCaptureMenu.OnPlayModeStateChanged before this bot was injected.
            // Just yield one frame to let all Awake/Start finish on scene objects,
            // then give the banner and cards a frame to settle.
            yield return null; // one frame for Awake/Start/OnEnable to run
            yield return null; // second frame: layout groups have updated

            switch (scenario)
            {
                case "versus_launch":
                    yield return StartCoroutine(VersusLaunch());
                    break;
                case "turn_swap":
                    yield return StartCoroutine(TurnSwap());
                    break;
                case "banner_show":
                    yield return StartCoroutine(BannerShow());
                    break;
                case "solo_regression":
                    yield return StartCoroutine(SoloRegression());
                    break;
                default:
                    Debug.LogWarning($"[VersusHudCaptureBot] Unknown scenario: '{scenario}'");
                    break;
            }

#if UNITY_EDITOR
            UnityEditor.EditorApplication.ExitPlaymode();
#endif
        }

        // ── Versus Launch ─────────────────────────────────────────────────────
        // Both cards are already seeded from frame 1 (see DEFECT-1 FIX above).
        // Hold for 4 s (enough to confirm both cards visible at correct opacities),
        // then exit.
        IEnumerator VersusLaunch()
        {
            // Data already seeded by the menu's EnteredPlayMode callback.
            // Trigger VersusHudController to pick up the state reactively.
            EnsureVersusActive();
            yield return new WaitForSeconds(4f);

            // Still capture at end for the screenshots/ folder.
            CaptureCore.SnapPlayModeSafe("versus_launch");
        }

        // ── Turn Swap ─────────────────────────────────────────────────────────
        // Starts with P1 active, holds 2 s (P1 lit, P2 dim), then swaps to P2
        // active, holds 2 s (P2 lit, P1 dim). Total clip ~5.5 s including lead-in.
        IEnumerator TurnSwap()
        {
            EnsureVersusActive();
            // P1 active (hold to show initial state).
            yield return new WaitForSeconds(2f);
            CaptureCore.SnapPlayModeSafe("turn_swap_p1active");

            // Swap to P2.
            MatchContext.SetActive(1);
            var ctrl = FindVersusController();
            // R2-4 fix (iter-7): OPPONENT'S TURN slides in from RIGHT (fromLeft=false).
            if (ctrl != null) ctrl.DebugShowBanner("OPPONENT'S TURN", fromLeft: false);
            yield return new WaitForSeconds(2.5f);
            CaptureCore.SnapPlayModeSafe("turn_swap_p2active");

            // Swap back to P1.
            MatchContext.SetActive(0);
            ctrl = FindVersusController();
            // R2-4 fix (iter-7): YOUR TURN slides in from LEFT (fromLeft=true, explicit for clarity).
            if (ctrl != null) ctrl.DebugShowBanner("YOUR TURN", fromLeft: true);
            yield return new WaitForSeconds(2.5f);
        }

        // ── Banner Show ───────────────────────────────────────────────────────
        // Shows both "YOUR TURN" and "OPPONENT'S TURN" banners to verify:
        //   - Rubik-SemiBold SDF font (matches Figma Medium stroke weight)
        //   - Top + bottom 3px #818EA1 silver borders
        //   - TMP auto-size: "OPPONENT'S TURN" fits inside the band
        IEnumerator BannerShow()
        {
            EnsureVersusActive();
            yield return new WaitForSeconds(1f);   // let layout settle before banner fires

            var ctrl = FindVersusController();
            if (ctrl == null)
            {
                Debug.LogWarning("[VersusHudCaptureBot:BannerShow] VersusHudController not found.");
                yield break;
            }

            // R2-4 fix (iter-7): YOUR TURN slides from LEFT (fromLeft=true, explicit for clarity).
            ctrl.DebugShowBanner("YOUR TURN", fromLeft: true);
            // Banner animation: 0.25 slide + 1.20 hold + 0.30 fade = 1.75 s.
            yield return new WaitForSeconds(2.5f);
            CaptureCore.SnapPlayModeSafe("banner_your_turn");

            // Wait a beat, then show "OPPONENT'S TURN" (tests auto-size for long string).
            yield return new WaitForSeconds(0.5f);
            // R2-4 fix (iter-7): OPPONENT'S TURN slides from RIGHT (fromLeft=false per spec).
            ctrl.DebugShowBanner("OPPONENT'S TURN", fromLeft: false);
            yield return new WaitForSeconds(2.5f);
            CaptureCore.SnapPlayModeSafe("banner_opponents_turn");

            // Brief trail after second banner fades out.
            yield return new WaitForSeconds(0.5f);
        }

        // ── Solo Regression ───────────────────────────────────────────────────
        // Confirms the HUD in solo/Practice mode (IsVersus=false): P2 card absent,
        // mini-map at top-right, P1 card reads PlayerContext.
        IEnumerator SoloRegression()
        {
            // Solo: do NOT set IsVersus. Data was seeded by menu callback but
            // for solo we override: clear versus flag and set solo player context.
            GameSession.IsVersus = false;
            PlayerContext.DisplayName = "CAMILA";
            PlayerContext.Level       = 13;
            PlayerContext.Raise();

            yield return new WaitForSeconds(3f);
            CaptureCore.SnapPlayModeSafe("solo_regression");
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// Ensures VersusHudController has picked up the state that was pre-seeded
        /// in the EnteredPlayMode callback. Calling Raise() again is harmless and
        /// acts as a defensive guard in case the reactive OnChanged path missed it.
        /// </summary>
        void EnsureVersusActive()
        {
            // State was already seeded — just raise once more to guarantee
            // VersusHudController.OnMatchContextChanged fires if it hadn't yet.
            MatchContext.Raise();
        }

        static VersusHudController FindVersusController()
        {
            return UnityEngine.Object.FindFirstObjectByType<VersusHudController>();
        }
    }
}
