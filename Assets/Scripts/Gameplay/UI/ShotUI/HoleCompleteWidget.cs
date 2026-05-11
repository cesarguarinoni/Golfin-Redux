using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.Gameplay.UI.ShotUI
{
    /// <summary>
    /// §2d Result Screen. Hidden by default. Shown on hole-complete with
    /// stacked Card 1 (current hole) + Card 2 (next hole).
    ///
    /// Top bar (RP counter, settings, RESULTS title, rankings) and bottom nav
    /// are NOT included in the LabScaffold variant per Q3 — they slot in as
    /// siblings of _root in the full implementation. The widget's RectTransform
    /// is sized to fill the viewport between top bar and nav bar regions.
    ///
    /// HUD suppression: when the modal opens, all other root-level GOs on the
    /// ShotUI_Canvas (PlayerCard, opponent chips, debug panels, camera banner)
    /// are hidden via SetActive(false) and restored on Hide(). This ensures the
    /// result screen has a clean backdrop.
    /// </summary>
    public class HoleCompleteWidget : MonoBehaviour
    {
        [Header("Root (SetActive on Show/Hide)")]
        [SerializeField] GameObject _root;

        [Header("Dim background overlay")]
        [SerializeField] Image _dimBackground;

        [Header("Cards")]
        [SerializeField] HoleCompleteCardWidget _card1; // current-hole card
        [SerializeField] HoleCompleteCardWidget _card2; // next-hole card

        Action _closeCallback;

        // GOs we hid on Show() so we can restore them on Hide().
        readonly List<GameObject> _hiddenHUDObjects = new List<GameObject>();

        void Awake()
        {
            if (_root != null) _root.SetActive(false);
        }

        public bool IsShowing => _root != null && _root.activeSelf;

        public void Show(HoleCompleteData data, Action onClose)
        {
            // Suppress competing HUD GOs so only the result cards are visible.
            SuppressHUD();

            if (_root != null) _root.SetActive(true);

            // Card 1 → current-hole variant
            if (_card1 != null)
                _card1.BindCurrentHole(data, OnAnyButtonTap);

            // Card 2 → next-hole variant. Locked when failed-no-PB.
            bool card2Locked = data.IsFailed && !data.HasPersonalBest;
            if (_card2 != null)
                _card2.BindNextHole(data, card2Locked, OnAnyButtonTap);

            _closeCallback = onClose;

            bool isFailed = data.IsFailed;
            bool hasPB    = data.HasPersonalBest;
            Debug.Log($"[§2d] Widget showing {(isFailed ? "FAILED" : "SUCCESS")} state. IsFailed={isFailed} HasPB={hasPB} -> Card2 {(isFailed && !hasPB ? "locked" : "unlocked")}");
        }

        public void Hide()
        {
            if (_root != null) _root.SetActive(false);
            _closeCallback = null;
            RestoreHUD();
        }

        void OnAnyButtonTap()
        {
            _closeCallback?.Invoke();
        }

        /// <summary>
        /// Hide sibling HUD GameObjects on the parent canvas and the
        /// CameraModeDebugHUD which uses its own high-sortOrder canvas.
        /// </summary>
        void SuppressHUD()
        {
            _hiddenHUDObjects.Clear();

            // Siblings on the same canvas parent that are active (exclude ourselves).
            if (transform.parent != null)
            {
                foreach (Transform sibling in transform.parent)
                {
                    if (sibling == transform) continue;
                    if (sibling.gameObject.activeSelf)
                    {
                        sibling.gameObject.SetActive(false);
                        _hiddenHUDObjects.Add(sibling.gameObject);
                    }
                }
            }

            // CameraModeDebugHUD creates its own DontDestroyOnLoad GO with a
            // canvas at sortingOrder=32760 — must be hidden explicitly.
#if UNITY_EDITOR
            HideByName("CameraModeDebugHUD");
            HideByName("CameraModeDebugCanvas");
#endif
        }

        void RestoreHUD()
        {
            foreach (var go in _hiddenHUDObjects)
            {
                if (go != null) go.SetActive(true);
            }
            _hiddenHUDObjects.Clear();

#if UNITY_EDITOR
            RestoreByName("CameraModeDebugHUD");
            RestoreByName("CameraModeDebugCanvas");
#endif
        }

        // Tracks hidden DDOL objects by name.
        readonly List<GameObject> _hiddenDDOL = new List<GameObject>();

        void HideByName(string goName)
        {
#if UNITY_EDITOR
            var go = GameObject.Find(goName);
            if (go != null && go.activeSelf)
            {
                go.SetActive(false);
                _hiddenDDOL.Add(go);
            }
#endif
        }

        void RestoreByName(string goName)
        {
#if UNITY_EDITOR
            foreach (var go in _hiddenDDOL)
            {
                if (go != null && go.name == goName) go.SetActive(true);
            }
            _hiddenDDOL.RemoveAll(g => g == null || g.name == goName);
#endif
        }

        // Internal accessor for unit tests.
        internal HoleCompleteCardWidget Card1 => _card1;
        internal HoleCompleteCardWidget Card2 => _card2;
    }
}
