using UnityEngine;
using Golfin.Gameplay.Input;

namespace Golfin.Gameplay.UI.ShotUI
{
    public class ActionButtonsRoot : MonoBehaviour
    {
        [SerializeField] private ShotController _shotController;
        [SerializeField] private CanvasGroup    _group;

        void Awake()
        {
            if (_group == null) _group = GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
        }

        void OnEnable()
        {
            if (_shotController != null) _shotController.OnStateChanged += Handle;
        }

        void OnDisable()
        {
            if (_shotController != null) _shotController.OnStateChanged -= Handle;
        }

        void Handle(ShotInputState s)
        {
            bool idle = s.State == ShotState.Idle;
            _group.interactable   = idle;
            _group.blocksRaycasts = idle;
        }
    }
}
