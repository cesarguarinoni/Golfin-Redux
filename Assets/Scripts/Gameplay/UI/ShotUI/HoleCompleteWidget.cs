using System;
using System.Collections;
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
        // ── game_polish_b §D1.3 — this widget pops like the other fourteen ────────
        //
        // Every game modal pops now, and this one cannot get there the way the others did.
        // HoleCompleteModalController.Show() is overridden as a no-op — visibility belongs to
        // THIS widget's _root.SetActive — so setting `animateShow` on the modal makes no
        // difference at all. Without the pop below, the one result screen the player sees most
        // would be the only modal in the game that still snaps.
        //
        // WHY THE CURVE IS COPIED RATHER THAN CALLED. Golfin.UI.Polish.UiMotion lives in
        // Assembly-CSharp and this asmdef (Golfin.Gameplay.UI) cannot reference it. That is the
        // same wall SelectorOverlayWidget and SelectorCarouselMath already document and solve the
        // same way, and this follows their precedent rather than inventing a third answer:
        // identical constants (0.9 -> 1 over PopDur 0.20, `1 - (1-t)^3`, FadeDur 0.15), unscaled
        // time, and a settle on the exact final value including on interruption.
        //
        // The CanvasGroups are added at RUNTIME, never authored, so the prefab is untouched and
        // A3's rest parity cannot move: an alpha-1 CanvasGroup draws nothing differently.

        /// <summary>Modal pop-in scale. UiMotion.PopDur.</summary>
        const float PopDur = 0.20f;
        /// <summary>Scrim cross-fade. UiMotion.FadeDur.</summary>
        const float FadeDur = 0.15f;
        /// <summary>UiMotion.Pop's start scale.</summary>
        const float PopFromScale = 0.9f;
        /// <summary>UiMotion.Unpop's end scale.</summary>
        const float UnpopToScale = 0.95f;

        CanvasGroup _rootGroup;
        CanvasGroup _dimGroup;
        Coroutine _popRoutine;
        Coroutine _dimRoutine;

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

            // §D1.3 — pop the card stack, fade the scrim. Independent properties on purpose, the
            // same way ModalController does it: a scrim arriving at full speed under a panel that
            // is still growing reads as "on top of" rather than "instead of".
            StartPop();

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
            // §D1.3 — reverse the pop, THEN deactivate. HideNow is chained off the unpop rather
            // than called here so the panel is never deactivated out from under its own shrink.
            //
            // The HUD is restored immediately rather than on the tween's tail: RestoreHUD is what
            // gives the player their controls back, and holding it for 0.15 s of animation would
            // be the one part of this change that a player could actually feel.
            _closeCallback = null;
            RestoreHUD();

            if (_root == null || !_root.activeSelf || !isActiveAndEnabled)
            {
                HideNow();
                return;
            }
            StartUnpop();
        }

        /// <summary>Deactivate the root and the scrim. The end of every hide path.</summary>
        void HideNow()
        {
            StopMotion();
            if (_root != null) _root.SetActive(false);
            // §2d iter-8: deactivate DimBackground when hiding.
            if (_dimBackground != null) _dimBackground.gameObject.SetActive(false);
        }

        // ── §D1.3 motion ─────────────────────────────────────────────────────────

        void StartPop()
        {
            EnsureGroups();
            StopMotion();
            if (!isActiveAndEnabled)
            {
                // No coroutines off-screen: settle on the final state so a widget shown while
                // disabled is fully visible rather than stranded at 0.9 and alpha 0.
                SettlePop();
                return;
            }
            if (_root != null) _popRoutine = StartCoroutine(PopRoutine());
            if (_dimGroup != null) _dimRoutine = StartCoroutine(FadeRoutine(_dimGroup, 0f, 1f, FadeDur, null));
        }

        void StartUnpop()
        {
            EnsureGroups();
            StopMotion();
            if (!isActiveAndEnabled) { HideNow(); return; }
            _popRoutine = StartCoroutine(UnpopRoutine());
            if (_dimGroup != null)
                _dimRoutine = StartCoroutine(FadeRoutine(_dimGroup, _dimGroup.alpha, 0f, FadeDur, null));
        }

        void StopMotion()
        {
            if (_popRoutine != null) { StopCoroutine(_popRoutine); _popRoutine = null; }
            if (_dimRoutine != null) { StopCoroutine(_dimRoutine); _dimRoutine = null; }
        }

        /// <summary>The state a completed pop leaves behind. Also the interruption settle — a root
        /// stranded at 0.94 is a visibly wrong-sized card stack that survives until the next show.</summary>
        void SettlePop()
        {
            if (_root != null) _root.transform.localScale = Vector3.one;
            if (_rootGroup != null) _rootGroup.alpha = 1f;
            if (_dimGroup != null) _dimGroup.alpha = 1f;
        }

        IEnumerator PopRoutine()
        {
            Transform rt = _root.transform;
            rt.localScale = new Vector3(PopFromScale, PopFromScale, 1f);
            if (_rootGroup != null) _rootGroup.alpha = 0f;

            float elapsed = 0f;
            while (elapsed < PopDur)
            {
                elapsed += Time.unscaledDeltaTime;
                if (_root == null) yield break;
                float e = EaseOut(elapsed / PopDur);
                float s = Mathf.Lerp(PopFromScale, 1f, e);
                rt.localScale = new Vector3(s, s, 1f);
                if (_rootGroup != null) _rootGroup.alpha = e;
                yield return null;
            }
            if (_root != null) _root.transform.localScale = Vector3.one;
            if (_rootGroup != null) _rootGroup.alpha = 1f;
            _popRoutine = null;
        }

        IEnumerator UnpopRoutine()
        {
            Transform rt = _root.transform;
            float elapsed = 0f;
            while (elapsed < FadeDur)
            {
                elapsed += Time.unscaledDeltaTime;
                if (_root == null) break;
                float e = EaseIn(elapsed / FadeDur);
                float s = Mathf.Lerp(1f, UnpopToScale, e);
                rt.localScale = new Vector3(s, s, 1f);
                if (_rootGroup != null) _rootGroup.alpha = 1f - e;
                yield return null;
            }
            // Scale settles at ONE, not at UnpopToScale: the root is about to be deactivated and
            // the next Show must find it at rest.
            if (_root != null) _root.transform.localScale = Vector3.one;
            if (_rootGroup != null) _rootGroup.alpha = 1f;
            _popRoutine = null;
            HideNow();
        }

        IEnumerator FadeRoutine(CanvasGroup g, float from, float to, float dur, Action after)
        {
            g.alpha = from;
            float elapsed = 0f;
            while (elapsed < dur)
            {
                elapsed += Time.unscaledDeltaTime;
                if (g == null) yield break;
                g.alpha = Mathf.Lerp(from, to, EaseOut(elapsed / dur));
                yield return null;
            }
            if (g != null) g.alpha = to;
            _dimRoutine = null;
            after?.Invoke();
        }

        /// <summary>Ease-out cubic — the same curve as <c>Golfin.UI.Polish.UiMotion.EaseOut</c>,
        /// which this asmdef cannot reference.</summary>
        static float EaseOut(float t)
        {
            t = Mathf.Clamp01(t);
            float inv = 1f - t;
            return 1f - inv * inv * inv;
        }

        /// <summary>Cubic ease-in — UiMotion.EaseIn, the leaving half of the pair.</summary>
        static float EaseIn(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * t;
        }

        /// <summary>Both CanvasGroups, created on demand. Never authored — see the class header.</summary>
        void EnsureGroups()
        {
            if (_root != null && _rootGroup == null)
            {
                _rootGroup = _root.GetComponent<CanvasGroup>();
                if (_rootGroup == null) _rootGroup = _root.AddComponent<CanvasGroup>();
            }
            if (_dimBackground != null && _dimGroup == null)
            {
                _dimGroup = _dimBackground.GetComponent<CanvasGroup>();
                if (_dimGroup == null) _dimGroup = _dimBackground.gameObject.AddComponent<CanvasGroup>();
            }
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

        // Accessors — internal for unit tests, public for production controller seams.
        public HoleCompleteCardWidget Card1 => _card1;
        public HoleCompleteCardWidget Card2 => _card2;
        // iter-9 F1 v2 compiled: CanvasGroup-alpha suppression
    }
}
