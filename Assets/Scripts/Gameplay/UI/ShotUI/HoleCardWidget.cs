using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Golfin.Gameplay.UI.HUD;

namespace Golfin.Gameplay.UI.ShotUI
{
    /// <summary>
    /// HUD widget showing course name, hole number, par, and a hole-map thumbnail.
    /// The hole-map image area is a tappable Button whose onClick calls
    /// MapViewController.Open() — the real player entry point for the map view
    /// (PIPELINE_HARDENING Rule 2: no synthetic bypass; this IS the player's button).
    /// </summary>
    public class HoleCardWidget : MonoBehaviour
    {
        [Header("Hole Map")]
        [SerializeField] Image   _holeMap;
        [SerializeField] Sprite  _defaultHoleMap;
        [SerializeField] Sprite[] _holeMaps; // 18-entry array; index = holeNumber - 1

        [Header("Chip rows")]
        [SerializeField] TMP_Text _courseText;
        [SerializeField] TMP_Text _holeText;
        [SerializeField] TMP_Text _parText;

        // ── v2 entry-point wiring ─────────────────────────────────────────────
        // The Button + ButtonPressFeedback components are expected on the same GO
        // as the _holeMap Image (the "HoleMap" child), which is the natural tap
        // target for the player.  We wire onClick at runtime so we never need a
        // persistent-callback YAML entry in the scene.
        Button _mapButton;

        void Awake()
        {
            // Find the Button on the _holeMap child (already in the scene with
            // Button + ButtonPressFeedback).  Fall back to GetComponentInChildren
            // in case the widget is re-parented.
            if (_holeMap != null)
                _mapButton = _holeMap.GetComponent<Button>();

            if (_mapButton == null)
                _mapButton = GetComponentInChildren<Button>(includeInactive: true);

            if (_mapButton != null)
            {
                _mapButton.onClick.AddListener(OpenMapView);
            }
            else
            {
                Debug.LogWarning("[HoleCardWidget] No Button found on HoleMap child — map cannot be opened via tap.");
            }
        }

        void OnDestroy()
        {
            if (_mapButton != null)
                _mapButton.onClick.RemoveListener(OpenMapView);
        }

        // ── Public API — the REAL player entry point ──────────────────────────
        /// <summary>
        /// Opens the map view.  Called by the Button's onClick and also
        /// invokable in tests via widget.GetComponent<Button>().onClick.Invoke()
        /// on the HoleMap child.
        /// </summary>
        public void OpenMapView()
        {
            // The map is not reachable for the duration of a shot. ShotInProgressUiGate flips
            // the Button inert in every mode, and additionally hides HoleMapContainer in Versus
            // only — in Practice/solo the thumbnail stays on screen top-left and is merely
            // non-clickable (Cesar, 2026-08-06; Versus-only hide as of 2026-08-27), so this
            // guard is what actually keeps the map view shut there.
            if (ShotInProgressUiGate.ShotInProgress) return;

            var mvc = FindObjectOfType<MapViewController>(includeInactive: false);
            if (mvc == null)
            {
                Debug.LogError("[HoleCardWidget] MapViewController not found in scene — cannot open map.");
                return;
            }
            mvc.OpenViaWidget();  // sets _entryViaRealWidget=true for §11 assert
        }

        // ── Public accessor for Gate-A verification ───────────────────────────
        /// <summary>Returns the tappable Button this widget wired (the HoleMap child Button).</summary>
        public Button MapButton => _mapButton;

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
            int    holeNum = HoleContext.HoleNumber;
            int    par     = HoleContext.Par;
            string tee     = HoleContext.TeeName;

            if (_courseText != null) _courseText.text = HoleContext.CourseName;
            if (_holeText   != null) _holeText.text   = $"HOLE {holeNum} - {tee}";
            if (_parText    != null) _parText.text     = $"PAR {par}";

            if (_holeMap != null)
            {
                int idx = holeNum - 1;
                Sprite sp = (idx >= 0 && _holeMaps != null && idx < _holeMaps.Length) ? _holeMaps[idx] : null;
                _holeMap.sprite = sp != null ? sp : _defaultHoleMap;
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
