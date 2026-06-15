using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Golfin.Audio.Events;

namespace Golfin.UI
{
    /// <summary>
    /// Global always-on tap micro-feedback — a soft ring + sparkle burst spawned at every
    /// finger-down point, on every screen, rendered above all UI.
    ///
    /// Strictly OBSERVATIONAL: never consumes or intercepts input.
    /// Never enables EnhancedTouchSupport (per InputSimulationBootstrap.cs comments).
    ///
    /// Bootstrap: [RuntimeInitializeOnLoadMethod(AfterSceneLoad)] creates one DontDestroyOnLoad
    /// GameObject with an overlay Canvas (sortingOrder=5000, no GraphicRaycaster) and this
    /// controller. A fixed pool of TapFeedbackFX prefab instances handles playback.
    ///
    /// TUNING IS INSPECTOR-EDITABLE via a <see cref="TapFeedbackConfig"/> ScriptableObject at
    /// Assets/Resources/UI/TapFeedbackConfig.asset. The controller reads the config every spawn, so
    /// edits to that asset (even during play mode) apply on the next tap — no recompile needed.
    ///
    /// Usage:
    ///   TapFeedbackController.Suppressed = true;  // optional – future shot-drag mute
    /// </summary>
    [DisallowMultipleComponent]
    public class TapFeedbackController : MonoBehaviour
    {
        // ── Static API ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Set to true to silence the effect (e.g., during shot aim-drag).
        /// Wiring to the shot system is out of scope for this task — just expose the flag.
        /// </summary>
        public static bool Suppressed { get; set; } = false;

        private const string ConfigResourcePath = "UI/TapFeedbackConfig";
        private const string PrefabResourcePath = "UI/TapFeedbackFX";

        // ── References ───────────────────────────────────────────────────────────────

        [Header("Tuning")]
        [Tooltip("Inspector-editable effect tuning. Loaded from Resources/UI/TapFeedbackConfig if left empty.")]
        [SerializeField] private TapFeedbackConfig _config;
        [SerializeField] private TapFeedbackFX     _fxPrefab;

        // ── Private state ──────────────────────────────────────────────────────────

        private Canvas              _canvas;
        private RectTransform       _canvasRect;
        private TapFeedbackFX[]     _pool;
        private int                 _poolHead = 0;

        // ── Bootstrap ──────────────────────────────────────────────────────────────

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            // One DontDestroyOnLoad root
            var root = new GameObject("[TapFeedback]");
            DontDestroyOnLoad(root);
            Debug.Log("[TapFeedback] Bootstrap: DontDestroyOnLoad overlay created.");

            // Overlay Canvas — high sortingOrder to be above all UI (Toast=950, LoadingScreen=1000)
            var canvas                    = root.AddComponent<Canvas>();
            canvas.renderMode             = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder           = 5000;
            canvas.pixelPerfect           = false;
            canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1; // needed by UIParticle

            // CanvasScaler matching the project's reference resolution (1170x2532, MatchWidthOrHeight=0)
            var scaler                     = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode             = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution     = new Vector2(1170f, 2532f);
            scaler.screenMatchMode         = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight      = 0f;

            // NO GraphicRaycaster — FX canvas must never intercept input

            // Controller component
            var controller = root.AddComponent<TapFeedbackController>();
            controller._canvas     = canvas;
            controller._canvasRect = canvas.GetComponent<RectTransform>();
        }

        // ── Lifecycle ──────────────────────────────────────────────────────────────

        private void Awake()
        {
            _canvas     = GetComponent<Canvas>();
            _canvasRect = GetComponent<RectTransform>();
            EnsureConfig();
        }

        private void Start()
        {
            BuildPool();
        }

        private void Update()
        {
            if (Suppressed) return;
            if (_config != null && !_config.enabled) return;
            PollInput();
        }

        // ── Config ──────────────────────────────────────────────────────────────────

        private void EnsureConfig()
        {
            if (_config != null) return;

            _config = Resources.Load<TapFeedbackConfig>(ConfigResourcePath);
            if (_config == null)
            {
                Debug.LogWarning($"[TapFeedback] Resources/{ConfigResourcePath} not found — using built-in defaults.");
                _config = TapFeedbackConfig.CreateDefault();
            }
        }

        // ── Input polling (strictly observational) ─────────────────────────────────

        private void PollInput()
        {
            // Primary pointer (mouse in Editor, primary touch on device)
            var pointer = Pointer.current;
            if (pointer != null && pointer.press.wasPressedThisFrame)
            {
                SpawnAt(pointer.position.ReadValue());
            }

            // Additional touches (multi-touch, secondary fingers)
            var ts = Touchscreen.current;
            if (ts != null)
            {
                foreach (var touch in ts.touches)
                {
                    // Skip the primary touch if it was already handled by Pointer.current above
                    if (touch.touchId.ReadValue() == 0) continue;
                    if (touch.press.wasPressedThisFrame)
                    {
                        SpawnAt(touch.position.ReadValue());
                    }
                }
            }
        }

        // ── Effect spawning ────────────────────────────────────────────────────────

        private void SpawnAt(Vector2 screenPos)
        {
            if (_pool == null || _pool.Length == 0) return;
            Debug.Log($"[TapFeedback] Spawning at screenPos={screenPos}");

            // Pick next free slot (round-robin; oldest gets recycled if all busy)
            var fx = _pool[_poolHead];
            _poolHead = (_poolHead + 1) % _pool.Length;

            if (fx == null) return;

            // Convert screen position → local point in the overlay canvas (null camera → ScreenSpaceOverlay)
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvasRect, screenPos, null, out var localPt);

            fx.Play(localPt, _config);

            if (_config.playAudio)
            {
                // Order 350: route through SfxBus so tap audio respects the SFX volume slider.
                // Previously used AudioSource.PlayClipAtPoint which bypassed AudioManager entirely
                // (latent volume-slider-does-nothing bug now fixed). _config.audioClip is preserved
                // for backward-compat inspector data but is no longer used for playback.
                SfxBus.Play(SfxId.UiTap);
            }
        }

        // ── Pool ──────────────────────────────────────────────────────────────────

        private void BuildPool()
        {
            // Load prefab from Resources if not assigned (Bootstrap path — no Inspector wiring)
            if (_fxPrefab == null)
            {
                var prefab = Resources.Load<GameObject>(PrefabResourcePath);
                if (prefab == null)
                {
                    Debug.LogWarning($"[TapFeedback] Resources/{PrefabResourcePath} not found — pool not built.");
                    return;
                }
                _fxPrefab = prefab.GetComponent<TapFeedbackFX>();
                if (_fxPrefab == null)
                {
                    Debug.LogWarning("[TapFeedback] TapFeedbackFX component missing on prefab — pool not built.");
                    return;
                }
            }

            int poolSize = Mathf.Max(1, _config != null ? _config.poolSize : 8);
            _pool = new TapFeedbackFX[poolSize];
            for (int i = 0; i < poolSize; i++)
            {
                var go = Instantiate(_fxPrefab.gameObject, _canvasRect);
                go.name = $"TapFX_{i}";
                go.SetActive(false);
                _pool[i] = go.GetComponent<TapFeedbackFX>();
            }
            Debug.Log($"[TapFeedback] Pool built: {poolSize} instances.");
        }
    }
}
