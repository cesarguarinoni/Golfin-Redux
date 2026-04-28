using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Golfin.Gameplay.UI.HUD;

namespace Golfin.Gameplay.UI.ShotUI
{
    public class HoleCardWidget : MonoBehaviour
    {
        [Header("Hole Map")]
        [SerializeField] Image _holeMap;
        [SerializeField] Sprite[] _holeMaps; // 18-entry array; index = holeNumber - 1

        [Header("Chip rows")]
        [SerializeField] TMP_Text _courseText;
        [SerializeField] TMP_Text _holeText;
        [SerializeField] TMP_Text _parText;

        void OnEnable()
        {
            HoleContext.OnChanged += Refresh;
            Refresh();
        }

        void OnDisable()
        {
            HoleContext.OnChanged -= Refresh;
        }

        void Refresh()
        {
            int holeNum = HoleContext.HoleNumber;
            int par     = HoleContext.Par;
            string tee  = HoleContext.TeeName;

            if (_courseText != null) _courseText.text = HoleContext.CourseName;
            if (_holeText != null)   _holeText.text   = $"HOLE {holeNum} - {tee}";
            if (_parText != null)    _parText.text     = $"PAR {par}";

            if (_holeMap != null && _holeMaps != null)
            {
                int idx = holeNum - 1;
                if (idx >= 0 && idx < _holeMaps.Length && _holeMaps[idx] != null)
                    _holeMap.sprite = _holeMaps[idx];
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Auto-Assign Hole Maps")]
        void AutoAssignHoleMaps()
        {
            _holeMaps = new Sprite[18];
            for (int i = 1; i <= 18; i++)
            {
                string path = $"Assets/Art/In-Game UI/HoleMaps/Lomond - Hole {i}.png";
                var sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
                _holeMaps[i - 1] = sp;
            }
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
