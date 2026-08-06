using System.Collections;
using TMPro;
using UnityEngine;
using Golfin.Gameplay.Loop;
using Golfin.Gameplay.Session;
using Golfin.Gameplay.UI.HUD;
using Golfin.Gameplay.UI.ShotUI;

namespace Golfin.Physics.Viewer
{
    /// <summary>
    /// landing_surface_banner: shows a localized FAIRWAY / GREEN / FRINGE / SEMI-ROUGH /
    /// ROUGH / BUNKER / WATER / OB banner when the ball settles.
    /// Visuals/animation = runtime clone of the 1v1 TurnBanner (Figma 4094:26052), white text.
    /// Solo: every shot. Versus: human shots only (ActiveIndex==0 at OnShotComplete time).
    /// No banner for InCup, Tee, CartPath (Cesar 2026-08-06).
    /// Lives on the [Session] GameObject in LabScaffold.unity.
    /// </summary>
    public class LandingBannerController : MonoBehaviour
    {
        [Header("Required references")]
        [Tooltip("The existing (inactive) TurnBanner under ShotUI_Canvas — used as template.")]
        [SerializeField] TurnBannerWidget _templateBanner;

        TurnBannerWidget _banner;      // runtime clone
        BallStateMachine _sm;

        /// <summary>True while the landing banner is on screen (clone active).
        /// Read by VersusMatchController to sequence the AnnounceTurn banner.</summary>
        public static bool IsBannerVisible { get; private set; }

        void Update()
        {
            IsBannerVisible = _banner != null && _banner.gameObject.activeInHierarchy;
        }

        IEnumerator Start()
        {
            if (_templateBanner == null)
            {
                Debug.LogError("[LandingBanner] _templateBanner not wired — no landing banners.");
                yield break;
            }

            // Clone the turn banner (template is inactive; clone starts inactive too —
            // TurnBannerWidget.Show() handles activation + off-screen pre-positioning).
            _banner = Instantiate(_templateBanner, _templateBanner.transform.parent);
            _banner.gameObject.name = "LandingBanner";

            // SEMI-ROUGH is the only landing label long enough to wrap inside the banner's
            // 534px text area; with wrapping on, TMP breaks it after the hyphen and renders
            // two lines, which is off-design (Figma 4094:26052 is a single centred line).
            // Clone-only: turn wrapping off so auto-size shrinks long labels to one line
            // instead. TurnBanner itself is untouched — its own strings all fit on one line.
            var label = _banner.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.textWrappingMode = TextWrappingModes.NoWrap;

            // Wait for the BallStateMachine, mirroring VersusMatchController.Start().
            var controller = FindFirstObjectByType<PhysicsLabController>();
            float waited = 0f;
            while ((controller == null || controller.BallSM == null) && waited < 15f)
            {
                if (controller == null) controller = FindFirstObjectByType<PhysicsLabController>();
                yield return null;
                waited += Time.unscaledDeltaTime;
            }
            if (controller == null || controller.BallSM == null)
            {
                Debug.LogWarning("[LandingBanner] BallSM unavailable after 15s — banners disabled this session.");
                yield break;
            }

            _sm = controller.BallSM;
            _sm.OnShotComplete += HandleShotComplete;
        }

        void OnDestroy()
        {
            if (_sm != null) _sm.OnShotComplete -= HandleShotComplete;

            // Clear the static so a torn-down session can't leave VersusMatchController
            // waiting on a banner that no longer exists.
            IsBannerVisible = false;
        }

        void HandleShotComplete(ShotResult result)
        {
            // Versus: only the local player's shots (ActiveIndex still == shooter here).
            if (GameSession.IsVersus && MatchContext.ActiveIndex != 0) return;

            string key = KeyFor(result);
            if (key == null) return;

            string text = LocalizationManager.Get(key);
            Debug.Log($"[LandingBanner] {result.TerminalState}/{result.EndSurface}" +
                      $"{(result.OBReason.HasValue ? "/" + result.OBReason.Value : "")} → {key} → \"{text}\"");
            _banner.Show(text, fromLeft: true);
        }

        static string KeyFor(ShotResult r)
        {
            if (r.TerminalState == BallState.OB)
            {
                return r.OBReason == Golfin.Gameplay.Loop.OBReason.Water
                    ? "LANDING_WATER"
                    : "LANDING_OB";   // OutOfBounds + ExitedWorldBounds
            }

            if (r.TerminalState != BallState.AtRest) return null;   // InCup etc.

            switch (r.EndSurface)
            {
                case Golfin.Physics.SurfaceType.Fairway:     return "LANDING_FAIRWAY";
                case Golfin.Physics.SurfaceType.Green:       return "LANDING_GREEN";
                case Golfin.Physics.SurfaceType.GreenCollar: return "LANDING_FRINGE";
                case Golfin.Physics.SurfaceType.Semirough:   return "LANDING_SEMIROUGH";
                case Golfin.Physics.SurfaceType.Rough:       return "LANDING_ROUGH";
                case Golfin.Physics.SurfaceType.Sand:
                case Golfin.Physics.SurfaceType.BunkerLip:   return "LANDING_BUNKER";
                default:                                     return null; // Tee, CartPath — silent by decision
            }
        }
    }
}
