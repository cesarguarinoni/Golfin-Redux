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

            // §2d iter-8: activate DimBackground when showing (disabled by default at build time).
            if (_dimBackground != null) _dimBackground.gameObject.SetActive(true);

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
            // §2d iter-8: deactivate DimBackground when hiding.
            if (_dimBackground != null) _dimBackground.gameObject.SetActive(false);
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
            // §2d iter-9 F1: also suppress CentralBall (the golf-ball widget with the "G" logo).
            // The overlay Canvas is at sortingOrder=32767 (above CameraModeDebugCanvas@32760),
            // which should cover CentralBall. But CentralBallWidget.HandleStateChanged() re-activates
            // the GO from a C# event even while inactive — by-name suppression provides defense-in-depth.
#if UNITY_EDITOR
            HideByName("CameraModeDebugHUD");
            HideByName("CameraModeDebugCanvas");
            HideByName("CentralBall");
            HideByName("CentralBallWidget");
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
            RestoreByName("CentralBall");
            RestoreByName("CentralBallWidget");
#endif
        }

        // Tracks hidden DDOL objects by name.
        readonly List<GameObject> _hiddenDDOL = new List<GameObject>();

        // Tracks CanvasGroups we added (and must remove on restore) for by-name visual suppression.
        // Using CanvasGroup.alpha=0 rather than Image.enabled=false because CentralBallWidget's
        // RefreshSprite() resets Image.enabled on every state change. CanvasGroup.alpha survives
        // the SetActive/OnEnable cycle without being touched by CentralBallWidget internals.
        readonly List<CanvasGroup> _addedCanvasGroups = new List<CanvasGroup>();

        void HideByName(string goName)
        {
#if UNITY_EDITOR
            // Search both active and inactive GOs (CentralBall may have been hidden by sibling loop already).
            var allGOs = Resources.FindObjectsOfTypeAll<GameObject>();
            foreach (var go in allGOs)
            {
                if (go.name != goName) continue;
                // §2d iter-9 F1 v2: Use CanvasGroup.alpha=0 instead of SetActive(false)/Image.enabled=false.
                // CentralBallWidget.HandleStateChanged subscribes in Awake and calls SetActive(true) from
                // the ShotController event, then OnEnable→RefreshSprite re-enables the Image component.
                // Neither SetActive nor Image.enabled=false survives that cycle.
                // CanvasGroup.alpha=0 makes the whole GO visually transparent regardless of Image state.
                var cg = go.GetComponent<CanvasGroup>();
                bool weAddedIt = false;
                if (cg == null)
                {
                    cg = go.AddComponent<CanvasGroup>();
                    weAddedIt = true;
                }
                cg.alpha = 0f;
                if (weAddedIt) _addedCanvasGroups.Add(cg);
                Debug.Log($"[§2d HideByName] Suppressed '{goName}' via CanvasGroup.alpha=0 (addedNew={weAddedIt})");
            }
#endif
        }

        void RestoreByName(string goName)
        {
#if UNITY_EDITOR
            // Restore alpha on any CanvasGroups we zeroed (search all GOs including inactive).
            var allGOs = Resources.FindObjectsOfTypeAll<GameObject>();
            foreach (var go in allGOs)
            {
                if (go.name != goName) continue;
                var cg = go.GetComponent<CanvasGroup>();
                if (cg != null) cg.alpha = 1f;
                Debug.Log($"[§2d RestoreByName] Restored '{goName}' CanvasGroup.alpha=1");
            }
            // Clear tracking list (we don't remove the CanvasGroup component — alpha=1 is harmless).
            _addedCanvasGroups.RemoveAll(cg => cg == null || (cg.gameObject != null && cg.gameObject.name == goName));

            // Also restore sibling-suppressed GOs that were SetActive(false) by the sibling loop.
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
        // iter-9 F1 v2 compiled: CanvasGroup-alpha suppression
    }
}
