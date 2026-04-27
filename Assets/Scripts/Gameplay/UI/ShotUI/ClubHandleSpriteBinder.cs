using UnityEngine;
using UnityEngine.UI;

namespace Golfin.Gameplay.UI.ShotUI
{
    [RequireComponent(typeof(Image))]
    public class ClubHandleSpriteBinder : MonoBehaviour
    {
        private Image    _image;
        private Sprite[] _cachedByIndex;

        private static readonly string[] ResourceKeys =
        {
            "Clubs/Controls/S_Controls_Driver_GOLFIN",
            "Clubs/Controls/S_Controls_Iron_GOLFIN",
            "Clubs/Controls/S_Controls_Wedge_GOLFIN",
            "Clubs/Controls/S_Controls_Putter_GOLFIN",
        };

        private void Awake()
        {
            _image = GetComponent<Image>();
            _cachedByIndex = new Sprite[ResourceKeys.Length];
            for (int i = 0; i < ResourceKeys.Length; i++)
            {
                _cachedByIndex[i] = Resources.Load<Sprite>(ResourceKeys[i]);
                if (_cachedByIndex[i] == null)
                    Debug.LogWarning($"[ClubHandleSpriteBinder] Missing sprite: Resources/{ResourceKeys[i]}");
            }
        }

        private void OnEnable()
        {
            ClubSelectionBroadcast.OnClubChanged += HandleClubChanged;
            HandleClubChanged(ClubSelectionBroadcast.CurrentIndex);
        }

        private void OnDisable()
        {
            ClubSelectionBroadcast.OnClubChanged -= HandleClubChanged;
        }

        private void HandleClubChanged(int index)
        {
            if (_image == null) return;
            if (index < 0 || index >= _cachedByIndex.Length) index = 0;
            var s = _cachedByIndex[index];
            if (s != null) _image.sprite = s;
        }
    }
}
