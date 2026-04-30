using UnityEngine;
using UnityEngine.UI;
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
    /// </summary>
    public class CentralBallWidget : MonoBehaviour
    {
        [SerializeField] private Image          _image;
        [SerializeField] private RectTransform  _rect;
        [SerializeField] private ShotController _shotController;

        void Awake()
        {
            if (_rect == null)  _rect  = GetComponent<RectTransform>();
            if (_image == null) _image = GetComponent<Image>();
        }

        void OnEnable()
        {
            BallContext.OnSelectedChanged += RefreshSprite;
            if (_shotController != null) _shotController.OnStateChanged += HandleStateChanged;
            RefreshSprite();
        }

        void OnDisable()
        {
            BallContext.OnSelectedChanged -= RefreshSprite;
            if (_shotController != null) _shotController.OnStateChanged -= HandleStateChanged;
        }

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
            Sprite sprite = BallContext.SelectedThumbnail
                ?? BallContext.SelectedFullSprite
                ?? Resources.Load<Sprite>("Balls/Thumbnails/S_Controls_Ball_GOLFIN");
            _image.sprite  = sprite;
            _image.enabled = sprite != null;
        }

        void HandleStateChanged(ShotInputState state)
        {
            bool show = state.State is ShotState.Idle
                                    or ShotState.Aiming
                                    or ShotState.Pulling
                                    or ShotState.Timing
                                    or ShotState.Flicking;
            gameObject.SetActive(show);
        }
    }
}
