// ─────────────────────────────────────────────────────────────────────────────
// screen_hints §3.5 — the presenter.
//
// Sits on ShellScene `PersistentUI` (always loaded), listens to
// ScreenManager.ScreenChanged, and is poked by the two entry points that are not
// ScreenIds (GameplaySceneLoader step 7, ControlsSubmenu.OnEnable). Same shape as
// TournamentResultPresenter: screen change → eligible? → wait for the modal
// stack to clear → open. Nothing here decides WHICH tips — that is
// ScreenHintResolver, which is pure and tested.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using Golfin.UI.Modals;
using UnityEngine;

namespace GolfinRedux.UI
{
    public class ScreenHintPresenter : MonoBehaviour
    {
        [Header("Catalogs")]
        [Tooltip("Assets/Resources/Data/ScreenHints.csv")]
        [SerializeField] private TextAsset hintsCsv = null!;

        [Tooltip("Assets/Resources/Data/LoadingTips.csv")]
        [SerializeField] private TextAsset tipsCsv = null!;

        private static ScreenHintPresenter? _instance;

        private IReadOnlyList<ScreenHint> _hints = Array.Empty<ScreenHint>();
        private IReadOnlyList<LoadingTip> _tips = Array.Empty<LoadingTip>();
        private ScreenHintState _state;

        private Coroutine? _pending;
        private string? _pendingScreen;
        private bool _showing;
        private Action? _stackEmptied;

        /// <summary>The persisted state as this presenter holds it (probe / test seam).</summary>
        public ScreenHintState State => _state;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            _instance = this;
            _hints = hintsCsv != null ? ScreenHintCatalog.Load(hintsCsv) : ScreenHintCatalog.LoadFromResources();
            _tips  = tipsCsv  != null ? LoadingTipCatalog.Load(tipsCsv)  : LoadingTipCatalog.LoadFromResources();
            _state = ScreenHintStore.Load();
        }

        private void OnEnable()
        {
            ScreenManager.ScreenChanged += OnScreenChanged;
        }

        private void OnDisable()
        {
            ScreenManager.ScreenChanged -= OnScreenChanged;
            CancelWait();
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        // ── Entry points ──────────────────────────────────────────────────────

        private void OnScreenChanged(ScreenId id) => Request(ScreenHintCatalog.IdFor(id));

        /// <summary>
        /// The two non-ScreenId entry points: <c>GameplaySceneLoader.LoadCoroutine</c> step 7
        /// (with <see cref="ScreenHintCatalog.GameplayScreen"/>, right after the loading screen
        /// hands off so the hint opens over the revealed tee) and <c>ControlsSubmenu.OnEnable</c>
        /// (with <see cref="ScreenHintCatalog.SettingsControlsScreen"/>). Null-safe: no presenter
        /// → no-op, so the physics-lab / bot launchers that never run the loader are untouched.
        /// </summary>
        public static void NotifyScreenEntered(string screen)
        {
            if (_instance != null) _instance.Request(screen);
        }

        // ── Core ──────────────────────────────────────────────────────────────

        private void Request(string screen)
        {
            if (string.IsNullOrEmpty(screen)) return;

            List<LoadingTip> hints = ScreenHintResolver.HintsFor(screen, _hints, _tips, _state);

            // Architect default a: a screen is seen when it is ENTERED with hints resolved —
            // marked and saved NOW, even when there is nothing left to show for it.
            if (Array.IndexOf(_state.seenScreens ?? Array.Empty<string>(), screen) < 0)
            {
                _state.seenScreens = ScreenHintStore.With(_state.seenScreens, screen);
                ScreenHintStore.Save(_state);
                Debug.Log($"[ScreenHint] {screen} entered for the first time — {hints.Count} hint(s); state={JsonUtility.ToJson(_state)}");
            }

            if (hints.Count == 0) return;

            if (_pending != null)
            {
                if (_showing || screen == _pendingScreen)
                {
                    // One presentation at a time. The new screen is already marked seen above;
                    // it can only happen if two screens change during one modal (§3.5).
                    Debug.Log($"[ScreenHint] {screen} requested while {_pendingScreen} is presenting — dropped");
                    return;
                }
                // A DIFFERENT screen arrived while the previous one was still WAITING for the
                // modal stack: that screen is gone, so its hints are cancelled (it stays seen).
                Debug.Log($"[ScreenHint] {_pendingScreen} left before its hints opened — cancelled");
                CancelWait();
            }

            _pending = StartCoroutine(PresentWhenClear(screen, hints));
        }

        private IEnumerator PresentWhenClear(string screen, List<LoadingTip> hints)
        {
            _pendingScreen = screen;
            _showing = false;

            // One frame: anything that opens a modal on the same screen change (e.g.
            // TournamentResultPresenter) gets there first and we stack behind it.
            yield return null;

            // Bounded by nothing on purpose — a modal the player is reading is a modal the
            // player is reading.
            while (ModalController.OpenModalCount > 0)
            {
                bool emptied = false;
                _stackEmptied = () => emptied = true;
                ModalController.ModalStackEmptied += _stackEmptied;
                while (!emptied && ModalController.OpenModalCount > 0) yield return null;
                ModalController.ModalStackEmptied -= _stackEmptied;
                _stackEmptied = null;
            }

            ScreenHintModalController? modal = ScreenHintModalController.Instance;
            if (modal == null)
            {
                Debug.LogWarning($"[ScreenHint] no ScreenHintModal in the loaded scenes — {screen}'s {hints.Count} hint(s) skipped.");
                _pending = null;
                _pendingScreen = null;
                yield break;
            }

            _showing = true;
            string shownFor = screen;
            modal.Show(hints,
                onFinished: () =>
                {
                    // The tee-idle countdown restarts when the player can actually see the tee.
                    if (shownFor == ScreenHintCatalog.GameplayScreen)
                        Golfin.Gameplay.UI.ShotUI.TeeIdleGlowController.NotifyOtherInteraction();
                },
                onHintShown: tip =>
                {
                    if (Array.IndexOf(_state.seenKeys ?? Array.Empty<string>(), tip.key) >= 0) return;
                    _state.seenKeys = ScreenHintStore.With(_state.seenKeys, tip.key);
                    ScreenHintStore.Save(_state);
                    Debug.Log($"[ScreenHint] shown {tip.key} on {shownFor}; state={JsonUtility.ToJson(_state)}");
                });

            // Stay alive until the modal is gone — CLOSE, force-disable or scene teardown alike —
            // so a hint hidden by teardown can never leave the presenter stuck on "presenting".
            while (modal != null && modal.IsVisible()) yield return null;

            _showing = false;
            _pending = null;
            _pendingScreen = null;
        }

        private void CancelWait()
        {
            if (_stackEmptied != null)
            {
                ModalController.ModalStackEmptied -= _stackEmptied;
                _stackEmptied = null;
            }
            if (_pending != null)
            {
                StopCoroutine(_pending);
                _pending = null;
            }
            _pendingScreen = null;
            _showing = false;
        }
    }
}
