using UnityEngine;
using Golfin.Gameplay.Session;
using Golfin.Gameplay.UI.HUD;

namespace Golfin.Gameplay.UI.ShotUI
{
    /// <summary>
    /// Orchestrator for the 1v1 in-game HUD overlay.
    ///
    /// Placed on the HUD root (same or child of the Canvas that owns PlayerCard).
    /// On Start():
    ///   • If !IsVersus → no-op. Solo HUD is byte-identical to pre-spec.
    ///   • If IsVersus  → activates P2 card, repositions mini-map, shows "YOUR TURN" banner.
    ///
    /// The controller also subscribes to MatchContext.OnChanged so that a bot or
    /// production code that sets GameSession.IsVersus=true and fires MatchContext.Raise()
    /// AFTER Start() has run will still trigger the versus layout activation. This makes
    /// the shipped scene state (_debugForceVersus=false) safe for solo use without
    /// requiring a script-execution-order trick.
    ///
    /// Phase-1 notes (flagged for Phase 2):
    ///   • Turn-swap calls SetActive(0/1) manually via DEBUG controls — real turn-flow
    ///     is out of scope for Phase 1.
    ///   • Opponent Level / TurnCount = Phase-1 placeholder (spec §3.3 note).
    /// </summary>
    public class VersusHudController : MonoBehaviour
    {
        [Header("Mini-map (HoleCard)")]
        [SerializeField] RectTransform _miniMap;
        [Tooltip("anchoredPosition of mini-map in VERSUS layout. " +
                 "Positioned above the FadeDrawButton. " +
                 "R4-1 (iter-12): _miniMapVersusPos.y adjusted to -1718 so map↔FadeDraw visible gap " +
                 "visually equals the Driver↔FadeDraw visible gap (~33px consensus). " +
                 "iter-11 at y=-1728 gave 22px (too small); iter-10 at y=-1716 gave 36px (close). " +
                 "-1718 targets ~34px — matches the 33px driver gap within visual noise. " +
                 "R4-2 (iter-11): x=-61 keeps map right edge flush with buttons right edge. " +
                 "DO NOT change x.")]
        [SerializeField] Vector2 _miniMapVersusPos = new Vector2(-61f, -1718f);

        // Cache of the mini-map's original position/size and ChipStack state.
        Vector2 _miniMapOriginalPos;
        Vector2 _miniMapOriginalSize;
        bool    _miniMapMoved;
        // ChipStack state cache for R2-2 restore.
        GameObject _chipStack;
        bool       _chipStackWasActive;
        bool       _chipStackHidden;

        [Header("P2 Card")]
        [Tooltip("The cloned P2 PlayerCard GameObject — inactive by default.")]
        [SerializeField] GameObject _p2Card;

        [Header("Turn Banner")]
        [SerializeField] TurnBannerWidget _banner;

        [Header("DEBUG Phase-1 controls")]
        [Tooltip("Set to true ONLY for direct editor verification without running matchmaking. " +
                 "Must be FALSE in the shipped scene — solo (Practice) must NOT show the P2 card. " +
                 "NEVER set this to true via code (use DebugForceVersus() which uses a runtime flag).")]
        [SerializeField] bool _debugForceVersus;

        [Tooltip("Text shown by the banner when _debugForceVersus is true.")]
        [SerializeField] string _debugBannerText = "YOUR TURN";

        // Runtime-only versus override — used by DebugForceVersus() so the call
        // path does NOT mutate the serialized _debugForceVersus field (which would
        // get persisted to the scene asset on save). This field is never serialized,
        // so entering/exiting play mode or calling Save Scene can never bake a
        // "forced versus" state into LabScaffold.unity.
        bool _runtimeDebugForceVersus;

        bool _versusActive;

        void Awake()
        {
            // _debugForceVersus (serialized): pre-arm IsVersus so PlayerCardWidget.OnEnable()
            // sees the correct branch. Only do this when the flag is explicitly set — NOT
            // the default shipped value (which is false).
            // _runtimeDebugForceVersus is a non-serialized clone set by DebugForceVersus()
            // so that calling that method never mutates the serialized field.
            if (_debugForceVersus || _runtimeDebugForceVersus)
                GameSession.IsVersus = true;
        }

        void OnEnable()
        {
            // Subscribe to MatchContext.OnChanged so the controller can activate the
            // versus layout REACTIVELY when a capture-bot or production code sets
            // IsVersus=true and fires MatchContext.Raise() after Start() has already run.
            MatchContext.OnChanged += OnMatchContextChanged;
        }

        void OnDisable()
        {
            MatchContext.OnChanged -= OnMatchContextChanged;

            if (_miniMapMoved && _miniMap != null)
            {
                _miniMap.anchoredPosition = _miniMapOriginalPos;
                _miniMap.sizeDelta        = _miniMapOriginalSize;
                _miniMapMoved = false;
            }

            // Restore ChipStack visibility (R2-2).
            if (_chipStackHidden && _chipStack != null)
            {
                _chipStack.SetActive(_chipStackWasActive);
                _chipStackHidden = false;
            }
        }

        void Start()
        {
            _versusActive = GameSession.IsVersus || _debugForceVersus || _runtimeDebugForceVersus;
            if (!_versusActive) return;

            ActivateVersusLayout();
        }

        // Fired when MatchContext.Raise() or MatchContext.Reset() is called.
        void OnMatchContextChanged()
        {
            // If we haven't activated yet and IsVersus just became true, activate now.
            if (!_versusActive && (GameSession.IsVersus || _debugForceVersus || _runtimeDebugForceVersus))
            {
                _versusActive = true;
                ActivateVersusLayout();
            }
        }

        /// <summary>
        /// Activates P2 card, repositions mini-map, shows opening banner.
        /// Called from Start() when IsVersus is true.
        ///
        /// ORDERING GUARANTEE (Defect 1 fix):
        /// Cards must be FULLY populated before the banner plays, so the player
        /// sees a complete HUD from frame 1. We therefore call MatchContext.Raise()
        /// (which triggers Refresh() on all subscribed PlayerCardWidgets) BEFORE
        /// calling _banner.Show(). This ensures:
        ///   • P1 card shows real name/level/portrait (PlayerContextPopulator already
        ///     called Raise() from its OnEnable, but we force it again defensively).
        ///   • P2 card shows real opponent data (set at OPPONENT FOUND in
        ///     MatchmakingModalController before gameplay scene loaded).
        ///   • Banner only starts AFTER both cards have rendered their data.
        /// </summary>
        void ActivateVersusLayout()
        {
            // Activate P2 card.
            if (_p2Card != null)
                _p2Card.SetActive(true);
            else
                Debug.LogWarning("[VersusHudController] _p2Card is not wired — P2 card will not appear.");

            // Reposition mini-map and show ONLY the map image (R2-1 + R2-2).
            if (_miniMap != null)
            {
                _miniMapOriginalPos  = _miniMap.anchoredPosition;
                _miniMapOriginalSize = _miniMap.sizeDelta;
                _miniMap.anchoredPosition = _miniMapVersusPos;

                // R2-2: shrink HoleCard to the map image only (180×180),
                // hiding the ChipStack data card so the versus layout shows
                // only the circular map thumbnail.
                _chipStack = _miniMap.Find("ChipStack")?.gameObject;
                if (_chipStack != null)
                {
                    _chipStackWasActive = _chipStack.activeSelf;
                    _chipStack.SetActive(false);
                    _chipStackHidden = true;
                }
                // Resize the HoleCard rect to match the map image only.
                _miniMap.sizeDelta = new Vector2(180f, 180f);

                _miniMapMoved = true;
            }
            else
            {
                Debug.LogWarning("[VersusHudController] _miniMap is not wired — mini-map will not be repositioned.");
            }

            // ── DEFECT-1 FIX: Force both cards to refresh BEFORE banner plays ──
            // Raise MatchContext so all subscribed PlayerCardWidgets call Refresh()
            // immediately — guaranteeing both cards show real data from frame 1.
            // In production, PlayerContextPopulator has already fired Raise() during
            // its OnEnable, and MatchmakingModalController set Players[1] before
            // gameplay scene loaded. This extra Raise() is a defensive guarantee
            // for any code path (including debug / capture-bot) that reaches
            // ActivateVersusLayout() without having fired Raise() first.
            MatchContext.Raise();

            // Phase 2a: VersusMatchController's first AnnounceTurn fires the opening "YOUR TURN"
            // banner. Do NOT fire it here as well, or the banner plays twice at match start.
            // The banner call is retained only in editor-debug mode (no VersusMatchController).
#if UNITY_EDITOR
            if (_banner != null && !_suppressOpeningBanner)
                _banner.Show(_debugBannerText, fromLeft: true);
#endif
        }

        /// <summary>
        /// Phase 2a: Called by VersusMatchController.Start() to tell VersusHudController that
        /// the production turn-flow is active and will fire the opening banner via AnnounceTurn.
        /// Prevents the double-banner at match start.
        /// Set before ActivateVersusLayout() runs (both scripts Start() in the same frame;
        /// VersusMatchController calls this in its Awake() to ensure ordering).
        /// </summary>
        public static bool _suppressOpeningBanner;

        // ─────────────────────────────────────────────────────────────────────
        // DEBUG Phase-1 API — editor-only. Production turn-flow is driven by
        // VersusMatchController (Phase 2a). These methods remain available for
        // inspector/screenshot use in-editor but MUST NOT ship in builds.
        // ─────────────────────────────────────────────────────────────────────
#if UNITY_EDITOR
        /// <summary>
        /// DEBUG (editor-only): Swap active turn (0 → 1 → 0).
        /// Phase-2 production turn-flow is driven by VersusMatchController, not this method.
        /// </summary>
        [ContextMenu("DEBUG — Swap Turn")]
        public void DebugSwapTurn()
        {
            int next = MatchContext.ActiveIndex == 0 ? 1 : 0;
            MatchContext.SetActive(next);
            if (_banner != null)
                _banner.Show(next == 0 ? "YOUR TURN" : "OPPONENT'S TURN", fromLeft: next == 0);
        }

        /// <summary>
        /// DEBUG (editor-only): Force versus layout on-demand.
        /// SCENE-MUTATION HAZARD FIX: uses _runtimeDebugForceVersus (non-serialized).
        /// </summary>
        [ContextMenu("DEBUG — Force Versus Layout")]
        public void DebugForceVersus()
        {
            _runtimeDebugForceVersus = true;
            ActivateVersusLayout();
        }

        /// <summary>
        /// DEBUG (editor-only): Show the banner with a given message.
        /// </summary>
        public void DebugShowBanner(string text, bool fromLeft = true)
        {
            if (_banner != null) _banner.Show(text, fromLeft);
        }
#endif
    }
}
