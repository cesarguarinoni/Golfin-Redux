using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Golfin.Gameplay.Input;
using Golfin.Gameplay.UI.HUD;

namespace Golfin.Gameplay.UI.ShotUI
{
    /// <summary>
    /// 2D UI ball sprite at a fixed UI anchor (per Figma layout).
    /// Sprite source: BallContext.SelectedFullSprite. Visible in the same states
    /// as the targeting line (Idle/Aiming/Pulling/Timing/Flicking).
    ///
    /// Decoupled from world ball position — a future game-camera pass may switch
    /// to projecting the world ball's screen position, but for now this is a
    /// fixed-anchor UI element matching the Figma reference.
    ///
    /// Tapping the ball in Idle state toggles the DebugShotPanel (if wired).
    /// </summary>
    public class CentralBallWidget : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private Image          _image;
        [SerializeField] private RectTransform  _rect;
        [SerializeField] private ShotController _shotController;

        [Header("Fallback sprite")]
        // Direct serialized reference to the default ball thumbnail. Unlike Resources.Load,
        // a serialized asset reference is GUARANTEED to be included in the player build and
        // cannot fail to resolve on device. This mirrors BallButtonWidget._defaultThumbnail —
        // the selector survives a null BallContext because it has this direct fallback, while
        // this widget previously relied only on Resources.Load (which can be absent on device).
        [SerializeField] private Sprite _defaultThumbnail;

        [Header("Debug")]
        [SerializeField] private DebugShotPanel _debugPanel;

        [Header("Putter mode")]
        [SerializeField] private float _normalSize   = 150f;
        [SerializeField] private float _puttModeSize = 150f;

        private ShotState _currentState = ShotState.Idle;

        /// <summary>
        /// Read-only access to this widget's rect, so the aim camera can pin the 3D ball to the
        /// same viewport point (aim_camera_ball_centering — the "future game-camera pass" named
        /// in the class doc above, solved by moving the camera under this fixed anchor).
        /// </summary>
        public RectTransform Rect => _rect;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_currentState == ShotState.Idle && _debugPanel != null)
                _debugPanel.Toggle();
        }

        public void SetPuttMode(bool on)
        {
            if (_rect == null) return;
            float s = on ? _puttModeSize : _normalSize;
            _rect.sizeDelta = new Vector2(s, s);
        }

        void Awake()
        {
            if (_rect == null)  _rect  = GetComponent<RectTransform>();
            if (_image == null) _image = GetComponent<Image>();

            // Subscribe in Awake (not OnEnable) so the widget keeps receiving state updates
            // even after it self-deactivates via gameObject.SetActive(false) during a shot.
            // OnDisable would unsubscribe and the widget would never re-show on the return
            // to Idle (the second-shot regression). C# event delegates fire regardless of
            // GameObject active state, so HandleStateChanged still runs and reactivates us.
            BallContext.OnSelectedChanged += RefreshSprite;
            if (_shotController != null) _shotController.OnStateChanged += HandleStateChanged;
        }

        void OnDestroy()
        {
            BallContext.OnSelectedChanged -= RefreshSprite;
            if (_shotController != null) _shotController.OnStateChanged -= HandleStateChanged;
        }

        void OnEnable() => RefreshSprite();

        void RefreshSprite()
        {
            if (_image == null) return;
            // Prefer thumbnail (small round ball icon) over the full portrait card.
            // SelectedFullSprite is the large 537x900 portrait — at 100x100 it renders
            // as a blurry cropped snippet. SelectedThumbnail is the ball icon sprite from
            // Assets/Resources/Balls/Thumbnails/ and is the correct asset to display here.
            //
            // When BallContext has no thumbnail (e.g. BallManager not present in lab scenes),
            // fall back to the default GOLFIN thumbnail from Resources — same fallback logic
            // as BallButtonWidget.
            // Prefer BallContext data; fall back to a directly-serialized default sprite, then
            // (only if that is unwired) Resources. The serialized _defaultThumbnail is guaranteed
            // in the player build and can't fail to resolve on device — same pattern as the
            // BallButtonWidget selector.
            Sprite sprite = BallContext.SelectedThumbnail
                ?? BallContext.SelectedFullSprite
                ?? _defaultThumbnail
                ?? Resources.Load<Sprite>("Balls/Thumbnails/S_Controls_Ball_GOLFIN");
            _image.sprite  = sprite;
            _image.enabled = sprite != null;
        }

        void HandleStateChanged(ShotInputState state)
        {
            _currentState = state.State;
            bool show = state.State is ShotState.Idle
                                    or ShotState.Aiming
                                    or ShotState.Pulling
                                    or ShotState.Timing
                                    or ShotState.Flicking;
            gameObject.SetActive(show);
        }
    }
}
