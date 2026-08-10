using UnityEngine;
using UnityEngine.UI;
using Golfin.Gameplay.Input;
using Golfin.Gameplay.Session;

namespace Golfin.Gameplay.UI.ShotUI
{
    /// <summary>
    /// Part B — Tee-off idle glow.
    /// Pulses a gold halo BEHIND the club handle when the player hasn't started
    /// dragging within idleGlowDelay seconds on the very first stroke of a hole
    /// (TurnCount == 1 && ShotHistory.Count == 0).
    ///
    /// HandleGlow is authored as a SIBLING of ClubHandle (lower sibling index in their
    /// shared parent), not a child — a child cannot render behind its parent's Image in
    /// Unity UI.  HandleGlow gets its own CanvasGroup (ignoreParentGroups=true) so it
    /// is not faded by the cone-root CanvasGroup that ConeAlphaController drives.
    ///
    /// Static NotifyOtherInteraction() is called by ActionButtonWidget to reset the
    /// countdown whenever any HUD action button is tapped.
    /// </summary>
    public class TeeIdleGlowController : MonoBehaviour
    {
        // ── Inspector ──────────────────────────────────────────────────────────
        [SerializeField] private ShotController    _shotController;
        [SerializeField] private MapViewController _mapViewController;

        [Tooltip("Seconds of idle before the glow starts (first tee stroke only).")]
        [SerializeField] private float idleGlowDelay = 5.0f;

        [Tooltip("Period (seconds) of one full pulse cycle.")]
        [SerializeField] private float glowPulsePeriod = 1.2f;

        [Tooltip("Gold tint applied to the soft halo during glow.")]
        [SerializeField] private Color glowColor = new Color(1f, 0.788f, 0.290f, 1f); // #FFC94A

        [Tooltip("Halo size as a multiple of the club handle's rect. >1 so the soft falloff " +
                 "extends past the handle on every side and stays visible at the pulse trough.")]
        [SerializeField] private float haloPadding = 1.6f;

        [Tooltip("Completely disables the glow when true (debug only).")]
        [SerializeField] private bool debugDisableIdleGlow = false;

        [Tooltip("Log a [GlowFrame] line every frame while animating/fading. " +
                 "Keep OFF in shipped/shipped-scene state; only enable manually for testing.")]
        [SerializeField] private bool _debugGlowFrameLog = false;

        // ── Static interaction bus ─────────────────────────────────────────────
        // Any live instance registers itself; null-safe callers never NRE.
        private static TeeIdleGlowController s_instance;

        /// <summary>
        /// Called by ActionButtonWidget (base class) when any HUD action button
        /// is clicked/tapped. Resets the idle timer so the countdown restarts.
        /// No-op if there is no live TeeIdleGlowController instance.
        /// </summary>
        public static void NotifyOtherInteraction()
        {
            if (s_instance != null) s_instance.ResetTimer();
        }

        // ── Runtime state ──────────────────────────────────────────────────────
        private ShotState _currentState = ShotState.Idle;
        private bool      _dragging     = false;
        private float     _idleTimer    = 0f;
        private bool      _glowActive   = false;
        private float     _glowPhase    = 0f;

        // Fade-out tracking
        private bool  _fadingOut  = false;
        private float _fadeAlpha  = 0f;
        private const float FadeOutDuration = 0.15f;
        private float _fadeTimer  = 0f;

        // ClubHandle's own localScale, refreshed every Update by SyncGlowRect. The pulse
        // multiplies this so the glow always matches the handle's rendered size.
        private Vector3 _handleBaseScale = Vector3.one;

        // Shared soft-radial falloff texture (one per domain, not per instance).
        private static Sprite s_radialSprite;

        // Scratch buffer for GetWorldCorners — avoids a per-frame 4-element allocation.
        private static readonly Vector3[] s_corners = new Vector3[4];

        // Glow sibling GO / Image
        private GameObject _glowGo;
        private Image      _glowImage;
        private Image      _handleImage;   // source for sprite mirroring

        // ── Lifecycle ──────────────────────────────────────────────────────────
        void Awake()
        {
            BuildGlowObject();
        }

        void OnEnable()
        {
            s_instance = this;
            if (_shotController != null)
                _shotController.OnStateChanged += HandleStateChanged;
        }

        void OnDisable()
        {
            if (s_instance == this) s_instance = null;
            if (_shotController != null)
                _shotController.OnStateChanged -= HandleStateChanged;
            StopGlow(instant: true);
        }

        void OnDestroy()
        {
            // HandleGlow is a sibling GO (not a child), so it won't be auto-destroyed
            // when ClubHandle is destroyed — clean up explicitly.
            if (_glowGo != null)
                Destroy(_glowGo);
        }

        void Update()
        {
            if (debugDisableIdleGlow)
            {
                StopGlow(instant: true);
                return;
            }

            // ── Sync HandleGlow rect to ClubHandle every frame ─────────────
            // BuildGlowObject() runs in Awake() before layout resolves, so the
            // initial anchoredPosition copy may be stale (the handle hasn't been
            // positioned by ShotConeView yet). Sync here to guarantee HandleGlow
            // overlays ClubHandle regardless of when layout settles.
            SyncGlowRect();

            // ── Fade-out tick ───────────────────────────────────────────────
            if (_fadingOut)
            {
                _fadeTimer += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(_fadeTimer / FadeOutDuration);
                SetGlowAlpha(_fadeAlpha * (1f - t));

                if (_debugGlowFrameLog)
                    Debug.Log($"[GlowFrame] t={Time.unscaledTime:F3} timer={_idleTimer:F3}" +
                              $" glowActive=fading alpha={(_glowImage != null ? _glowImage.color.a : 0f):F3}" +
                              $" fadeT={t:F3}");

                if (t >= 1f)
                {
                    _fadingOut = false;
                    if (_glowGo != null) _glowGo.SetActive(false);
                }
                return;
            }

            // ── State machine ────────────────────────────────────────────────
            bool teeGate  = (GameSession.TurnCount == 1) && (GameSession.ShotHistory.Count == 0);
            bool shotIdle = (_currentState == ShotState.Idle);
            bool armed    = teeGate && shotIdle && !_dragging;

            bool modalOpen = OtherButtonsFader.AnyOverlayOpen
                          || (_mapViewController != null && _mapViewController.IsOpen);

            if (!armed || modalOpen)
            {
                if (!armed)
                    _idleTimer = 0f;   // proper disarm: clear timer
                else
                    _idleTimer = 0f;   // modal pause: restart from 0 on close
                StopGlow(instant: false);
                return;
            }

            _idleTimer += Time.unscaledDeltaTime;

            if (_idleTimer >= idleGlowDelay)
                AnimateGlow();
        }

        // ── State handler ──────────────────────────────────────────────────────
        void HandleStateChanged(ShotInputState s)
        {
            _currentState = s.State;
            // SPEC: "state machine re-arms from ShotState (swing reset → back to Idle → timer restarts)"
            // When a no-power drag is cancelled, ShotController returns to Idle — clear _dragging so
            // the idle timer can restart without requiring a separate OnPointerUp hook.
            if (s.State == ShotState.Idle)
                _dragging = false;
        }

        // ── Public API ─────────────────────────────────────────────────────────
        /// <summary>
        /// Called by ClubHandleDragger.OnPointerDown — stops the glow and zeroes the timer
        /// so it doesn't immediately restart when the player releases without power.
        /// </summary>
        public void OnHandleTouched()
        {
            _dragging  = true;
            _idleTimer = 0f;
            StopGlow(instant: false);
        }

        /// <summary>
        /// Called by the BotDriver or any code that ends the drag without a pointer event.
        /// (ClubHandleDragger.OnPointerUp goes through state machine re-arm, so no explicit
        /// hook is required there.)
        /// </summary>
        public void OnHandleReleased()
        {
            _dragging = false;
        }

        // ── Private helpers ────────────────────────────────────────────────────
        void ResetTimer()
        {
            _idleTimer = 0f;
            // Zeroing the timer is not enough: if the glow is already running, nothing in
            // Update() will stop it (the state machine only ever STARTS the glow, once the
            // timer passes the delay). Without this the halo freezes on screen at its last
            // frame after a tap on any button that does not open a modal.
            StopGlow(instant: false);
        }

        void AnimateGlow()
        {
            if (!_glowActive)
            {
                // The halo is a club-agnostic radial falloff, so there is no per-club sprite
                // to re-sync here — a club switch changes the handle underneath it, not the glow.
                if (_glowGo != null) _glowGo.SetActive(true);
                _glowActive = true;
                _fadingOut  = false;
            }

            _glowPhase += Time.unscaledDeltaTime / glowPulsePeriod * (2f * Mathf.PI);
            float t = (Mathf.Sin(_glowPhase) + 1f) * 0.5f;   // 0..1 ping-pong

            float alpha  = Mathf.Lerp(0.35f, 0.8f, t);
            float scale  = Mathf.Lerp(1.00f, 1.12f, t);

            SetGlowAlpha(alpha);
            // Pulse MULTIPLIES the handle's own scale. ClubHandle runs at localScale 2.0, so
            // writing Vector3.one * scale here drew the glow at ~55% of the handle's size —
            // entirely inside the opaque handle rect, and therefore never visible on screen.
            if (_glowGo != null) _glowGo.transform.localScale = _handleBaseScale * scale;

            if (_debugGlowFrameLog)
                Debug.Log($"[GlowFrame] t={Time.unscaledTime:F3} timer={_idleTimer:F3}" +
                          $" glowActive=true alpha={alpha:F3} scale={scale:F3}");
        }

        void StopGlow(bool instant)
        {
            if (!_glowActive && !_fadingOut) return;

            _glowActive = false;
            _glowPhase  = 0f;

            if (instant || _glowGo == null)
            {
                _fadingOut = false;
                if (_glowGo != null) _glowGo.SetActive(false);
            }
            else
            {
                // Graceful fade-out ≤ FadeOutDuration (0.15 s) — logged in Update's fade block
                _fadingOut  = true;
                _fadeTimer  = 0f;
                if (_glowImage != null)
                    _fadeAlpha = _glowImage.color.a;
                else
                    _fadeAlpha = 0.8f;
            }
        }

        void SetGlowAlpha(float a)
        {
            if (_glowImage == null) return;
            var c = glowColor;
            _glowImage.color = new Color(c.r, c.g, c.b, a);
        }

        /// <summary>
        /// Keeps HandleGlow anchors/position/size in sync with ClubHandle.
        /// Called every Update() because ShotConeView repositions ClubHandle at runtime.
        /// </summary>
        void SyncGlowRect()
        {
            if (_glowGo == null) return;
            var glowRt   = _glowGo.transform as RectTransform;
            var handleRt = transform         as RectTransform;
            if (glowRt == null || handleRt == null) return;

            // The halo pivots at its CENTRE regardless of how ClubHandle is anchored. The
            // handle's own pivot sits on its bottom edge, so copying it made the glow grow
            // upward only; pivoting at the centre makes the pulse expand evenly on all sides.
            glowRt.anchorMin = glowRt.anchorMax = new Vector2(0.5f, 0.5f);
            glowRt.pivot     = new Vector2(0.5f, 0.5f);
            glowRt.sizeDelta = handleRt.rect.size * Mathf.Max(1f, haloPadding);

            // Position by world centre — GetWorldCorners already accounts for the handle's
            // anchoring, pivot and localScale, so this stays correct however ShotConeView
            // repositions or rescales the handle at runtime.
            var hc = s_corners;
            handleRt.GetWorldCorners(hc);
            glowRt.position = (hc[0] + hc[2]) * 0.5f;

            // ClubHandle is scaled at runtime (currently 2.0). Record it as the glow's base so
            // AnimateGlow's 1.00→1.12 pulse rides ON TOP of it instead of replacing it.
            _handleBaseScale = handleRt.localScale;
        }

        /// <summary>
        /// Builds the soft radial falloff used as the halo. Generated in code rather than
        /// shipped as an art asset: it is a pure gradient with no design content, and
        /// generating it keeps the effect independent of the equipped club's sprite.
        /// Alpha falls off as (1-r)^2 from the centre, so the rim fades out smoothly
        /// instead of ending on a hard edge.
        /// </summary>
        static Sprite BuildRadialGlowSprite()
        {
            if (s_radialSprite != null) return s_radialSprite;

            const int   size   = 128;
            const float centre = (size - 1) * 0.5f;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false)
            {
                name       = "TeeIdleGlow_Radial",
                filterMode = FilterMode.Bilinear,
                wrapMode   = TextureWrapMode.Clamp,
                hideFlags  = HideFlags.HideAndDontSave,
            };

            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - centre) / centre;
                    float dy = (y - centre) / centre;
                    float r  = Mathf.Sqrt(dx * dx + dy * dy);
                    float a  = Mathf.Clamp01(1f - r);
                    a *= a;                       // quadratic falloff — soft outer rim
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels(px);
            tex.Apply(updateMipmaps: false);

            s_radialSprite = Sprite.Create(
                tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect);
            s_radialSprite.name      = "TeeIdleGlow_Radial";
            s_radialSprite.hideFlags = HideFlags.HideAndDontSave;
            return s_radialSprite;
        }

        void BuildGlowObject()
        {
            // Find the handle Image to copy its sprite
            _handleImage = GetComponent<Image>();

            // ── Create sibling "HandleGlow" ──────────────────────────────────
            // HandleGlow must be a SIBLING of ClubHandle (this GO) at a LOWER sibling
            // index so Unity UI renders it first (behind ClubHandle's own Image).
            // A child cannot render behind its parent's Image — that is a Unity UI
            // rendering constraint.
            _glowGo = new GameObject("HandleGlow");

            Transform parentTransform = transform.parent;
            if (parentTransform == null)
            {
                // No parent — fall back to child (won't render behind, but won't crash)
                Debug.LogWarning("[TeeIdleGlowController] ClubHandle has no parent; " +
                                 "HandleGlow will be a child and may render on top instead of behind.");
                parentTransform = transform;
            }

            // SetParent before AddComponent so RectTransform is correctly initialised
            _glowGo.transform.SetParent(parentTransform, worldPositionStays: false);

            // ── Centre-pivoted rect over ClubHandle ───────────────────────────
            // SyncGlowRect() re-applies this every Update; this is just the initial state.
            var rt       = _glowGo.AddComponent<RectTransform>();
            var handleRt = transform as RectTransform;
            if (handleRt != null && parentTransform != transform)
            {
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot     = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = handleRt.rect.size * Mathf.Max(1f, haloPadding);
                handleRt.GetWorldCorners(s_corners);
                rt.position  = (s_corners[0] + s_corners[2]) * 0.5f;
            }
            else
            {
                // Fallback: fill the parent
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }

            // ── Own CanvasGroup (ignoreParentGroups=true) ──────────────────────
            // Without this, HandleGlow inherits the cone-root CanvasGroup alpha that
            // ConeAlphaController drives (0.25 at Idle), making the glow nearly invisible
            // at exactly the moment it is supposed to be brightest.
            var cg = _glowGo.AddComponent<CanvasGroup>();
            cg.ignoreParentGroups = true;
            cg.alpha              = 1f;
            cg.interactable       = false;
            cg.blocksRaycasts     = false;

            // ── Place HandleGlow BEFORE ClubHandle in sibling order ────────────
            // Lower sibling index = rendered first = appears behind.
            if (parentTransform != transform)
            {
                int handleIdx = transform.GetSiblingIndex();
                // SetSiblingIndex(handleIdx) inserts before ClubHandle, pushing ClubHandle to handleIdx+1
                _glowGo.transform.SetSiblingIndex(handleIdx);
            }

            // ── Image ──────────────────────────────────────────────────────────
            // Soft radial falloff, stretched to the halo rect (so it reads as an ellipse
            // matching the club head's proportions). preserveAspect stays OFF — forcing a
            // square would leave the halo far too tall for a wide, flat driver head.
            _glowImage = _glowGo.AddComponent<Image>();
            _glowImage.raycastTarget  = false;
            _glowImage.preserveAspect = false;
            _glowImage.type           = Image.Type.Simple;
            _glowImage.sprite         = BuildRadialGlowSprite();
            _glowImage.color = new Color(glowColor.r, glowColor.g, glowColor.b, 0f);

            _glowGo.SetActive(false);
        }
    }
}
