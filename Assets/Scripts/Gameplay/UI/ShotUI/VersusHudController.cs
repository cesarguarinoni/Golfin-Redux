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

            // Show opening banner AFTER cards have been forced to refresh.
            // "YOUR TURN" slides in from the left (fromLeft=true).
            if (_banner != null)
                _banner.Show(_debugBannerText, fromLeft: true);
            else
                Debug.LogWarning("[VersusHudController] _banner is not wired — turn banner will not appear.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // DEBUG Phase-1 API — called from Inspector buttons or external scripts
        // for screenshot / verification purposes.
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// DEBUG: Swap active turn (0 → 1 → 0).
        /// Fires MatchContext.SetActive to drive alpha on both cards.
        /// Phase-2 will replace with real turn-flow logic.
        /// </summary>
        [ContextMenu("DEBUG — Swap Turn")]
        public void DebugSwapTurn()
        {
            int next = MatchContext.ActiveIndex == 0 ? 1 : 0;
            MatchContext.SetActive(next);
            // R2-4: YOUR TURN slides from left; OPPONENT'S TURN slides from right.
            if (_banner != null)
                _banner.Show(next == 0 ? "YOUR TURN" : "OPPONENT'S TURN", fromLeft: next == 0);
        }

        /// <summary>
        /// DEBUG: Force versus layout on-demand (e.g., from inspector button or test harness).
        ///
        /// SCENE-MUTATION HAZARD FIX (iter-11, FIX 3):
        /// The old implementation set _debugForceVersus=true — a SerializeField — which Unity
        /// would then bake into the scene asset on the next EditorSceneManager.SaveScene() or
        /// editor auto-save, shipping _debugForceVersus:1 and breaking Practice/solo mode.
        ///
        /// This version sets _runtimeDebugForceVersus (a plain non-serialized bool) instead.
        /// Non-serialized fields are NEVER written to .unity files, so this call can never
        /// mutate LabScaffold.unity regardless of when/how the scene is saved.
        ///
        /// The serialized _debugForceVersus field is INTENTIONALLY never set to true by code;
        /// it exists only as an Inspector checkbox that devs can toggle manually in EditMode for
        /// one-shot inspection — and must always be FALSE in the committed scene asset.
        /// </summary>
        [ContextMenu("DEBUG — Force Versus Layout")]
        public void DebugForceVersus()
        {
            // Use runtime-only flag — NEVER mutate the serialized _debugForceVersus field here.
            _runtimeDebugForceVersus = true;
            ActivateVersusLayout();
        }

        /// <summary>
        /// DEBUG: Show the banner with a given message (for screenshot capture).
        /// fromLeft=true for "YOUR TURN", false for "OPPONENT'S TURN".
        /// </summary>
        public void DebugShowBanner(string text, bool fromLeft = true)
        {
            if (_banner != null) _banner.Show(text, fromLeft);
        }
    }
}
