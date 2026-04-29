using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Golfin.Gameplay.UI.HUD;

namespace Golfin.Gameplay.UI.ShotUI
{
    public class SelectorOverlayWidget : MonoBehaviour
    {
        public enum Kind { Club, Ball }

        [SerializeField] private RectTransform _root;
        [SerializeField] private Transform     _cardsContainer;
        [SerializeField] private GameObject    _cardPrefab;
        [SerializeField] private Button        _arrowUp;
        [SerializeField] private Button        _arrowDown;
        [SerializeField] private OutsideClickCatcher _outsideClickCatcher;

        [SerializeField] private Vector2 _anchoredPositionForClub  = new Vector2(-58f, 348f);
        [SerializeField] private Vector2 _anchoredPositionForBall  = new Vector2( 58f, 348f);

        Kind _kind;

        void OnEnable()
        {
            if (_outsideClickCatcher != null) _outsideClickCatcher.OnOutsideClick = Close;
        }

        public void Open(Kind kind)
        {
            _kind = kind;
            gameObject.SetActive(true);
            if (_outsideClickCatcher != null) _outsideClickCatcher.gameObject.SetActive(true);

            if (kind == Kind.Club)
            {
                _root.anchorMin = _root.anchorMax = new Vector2(1f, 0f);
                _root.pivot     = new Vector2(1f, 0f);
                _root.anchoredPosition = _anchoredPositionForClub;
            }
            else
            {
                _root.anchorMin = _root.anchorMax = new Vector2(0f, 0f);
                _root.pivot     = new Vector2(0f, 0f);
                _root.anchoredPosition = _anchoredPositionForBall;
            }
            Populate();
        }

        public void Close()
        {
            gameObject.SetActive(false);
            if (_outsideClickCatcher != null) _outsideClickCatcher.gameObject.SetActive(false);
        }

        void Populate()
        {
            if (_cardsContainer == null || _cardPrefab == null) return;

            for (int i = _cardsContainer.childCount - 1; i >= 0; i--)
                DestroyImmediate(_cardsContainer.GetChild(i).gameObject);

            if (_kind == Kind.Club)
            {
                for (int i = 0; i < ClubContext.EquippedBag.Count; i++)
                {
                    int captured = i;
                    var entry = ClubContext.EquippedBag[i];
                    var go = Instantiate(_cardPrefab, _cardsContainer);
                    go.SetActive(true);
                    var card = go.GetComponent<SelectorCardWidget>();
                    if (card != null)
                        card.SetClub(entry, () =>
                        {
                            ClubContext.RequestSelection(captured);
                            ClubSelectionBroadcast.Raise(entry.LabClubIndex);
                            Close();
                        });
                }
            }
            else
            {
                for (int i = 0; i < BallContext.OwnedBalls.Count; i++)
                {
                    int captured = i;
                    var entry = BallContext.OwnedBalls[i];
                    var go = Instantiate(_cardPrefab, _cardsContainer);
                    go.SetActive(true);
                    var card = go.GetComponent<SelectorCardWidget>();
                    if (card != null)
                        card.SetBall(entry, () =>
                        {
                            BallContext.RequestSelection(captured);
                            Close();
                        });
                }
            }
        }
    }

    /// <summary>
    /// Full-screen transparent Image that catches outside-taps and fires a callback.
    /// Sibling of the overlay, rendered BELOW it in the canvas hierarchy.
    /// Builder makes one of these per overlay.
    /// </summary>
    public class OutsideClickCatcher : MonoBehaviour, IPointerClickHandler
    {
        public System.Action OnOutsideClick;
        public void OnPointerClick(PointerEventData _) => OnOutsideClick?.Invoke();
    }
}
