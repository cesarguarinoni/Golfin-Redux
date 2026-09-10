#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using Golfin.UI.Polish;
using GolfinRedux.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.UI.Modals
{
    /// <summary>
    /// The screen-hint modal (<c>screen_hints</c> §3.3): the first time the player enters a
    /// screen, that screen's Loading tips open over it one at a time — diagram, text, an
    /// <c>n/X</c> counter, CONTINUE / CLOSE, and BACK from hint 2 onwards.
    ///
    /// <para><b>Prefab (Rule 19 clone provenance).</b>
    /// <c>Assets/Prefabs/UI/Modals/ScreenHintModal.prefab</c> is an
    /// <c>AssetDatabase.CopyAsset</c> of <c>SchemeConfirmModal.prefab</c> (itself a copy of
    /// <c>StartingCharacterConfirmModal.prefab</c>): the scrim, the opaque navy
    /// <c>Background - HoleCard</c> plate, the gold title, the <c>Divider</c> separator and the
    /// silver / gold <c>Main Buttons</c> pair are the shipping objects. The content — <c>TipText</c>
    /// + <c>TipImage</c> under a <c>CanvasGroup</c> — is the ShellScene loading card's OWN
    /// <c>ProTipCard/TipContent</c> subtree, copied by <c>ScreenHintModalBuilder</c>, so the two
    /// surfaces cannot drift. Nothing here is hand-built.</para>
    ///
    /// <para><b>Two instances, one prefab</b> — exactly <see cref="SchemeConfirmModalController"/>'s
    /// arrangement: ShellScene (root Canvas, beside <c>SchemeConfirmModal</c>) and LabScaffold
    /// (<c>ShotUI_Canvas</c>, above <c>InGameSettingsModal</c>). <see cref="Instance"/> resolves
    /// whichever is loaded; the non-shell one wins when both are (mid-round).</para>
    ///
    /// <para><b>Button states (§2.1).</b> <c>n == 1 &amp;&amp; X == 1</c> → [CLOSE];
    /// <c>n == 1 &amp;&amp; X &gt; 1</c> → [CONTINUE]; <c>1 &lt; n &lt; X</c> → [BACK][CONTINUE];
    /// <c>n == X &gt; 1</c> → [BACK][CLOSE]. The gold button is ONE <c>Button</c> whose label swaps
    /// between <c>HINT_CONTINUE</c> and <c>HINT_CLOSE</c>; BACK is <c>SetActive(n &gt; 1)</c> — present or
    /// absent, never disabled (Cesar 2026-09-10). Every rebind, buttons included, happens inside
    /// the cross-fade seam so nothing flips on a fully visible row.</para>
    ///
    /// <para><b>Motion (§3.3a)</b> is <c>ProTipCard</c>'s, copied: <see cref="UiMotion.Fade"/> out →
    /// rebind → in as ONE routine (never a <c>Then</c> tail re-entering <c>Run</c> on the same
    /// handle — that is the double-advance the loading card shipped with), the eased
    /// <c>LayoutElement.preferredHeight</c> plate resize with the pin-before-rebind that stops
    /// the one-frame height flash, and <see cref="UiMotion.Bump"/> on the counter. Pop / Unpop
    /// and the scrim fade come from <c>animateShow</c> on the copy.</para>
    /// </summary>
    public class ScreenHintModalController : ModalController
    {
        [Header("Title / counter")]
        [Tooltip("Gold 66px title from the clone. Key TIP_HEADER via LocalizedText.")]
        [SerializeField] private TextMeshProUGUI titleText = null!;

        [Tooltip("The n/X counter, top-right of the plate. Hidden when the group has one hint.")]
        [SerializeField] private TextMeshProUGUI counterText = null!;

        [Header("Content (the loading card's TipContent subtree, copied)")]
        [SerializeField] private CanvasGroup tipContentGroup = null!;
        [SerializeField] private TextMeshProUGUI tipText = null!;
        [SerializeField] private Image tipImage = null!;

        [Tooltip("One entry per tip, selected by the LoadingTips.csv `sprite` name. Wired on the " +
                 "PREFAB so both scene instances share one table.")]
        [SerializeField] private ProTipCard.TipSprite[] tipSprites = Array.Empty<ProTipCard.TipSprite>();

        [Header("Buttons")]
        [Tooltip("The clone's silver CancelButton. Active from hint 2 onwards only.")]
        [SerializeField] private Button backButton = null!;

        [Tooltip("The clone's gold ConfirmButton. Reads CONTINUE, or CLOSE on the last / single hint.")]
        [SerializeField] private Button nextButton = null!;

        [Tooltip("nextButton's label — its LocalizedText key is swapped at every rebind.")]
        [SerializeField] private TextMeshProUGUI nextLabel = null!;

        // ── Keys (§2.3) ───────────────────────────────────────────────────────
        public const string TitleKey    = "TIP_HEADER";
        public const string ContinueKey = "HINT_CONTINUE";
        public const string CloseKey    = "HINT_CLOSE";
        public const string BackKey     = "HINT_BACK";

        /// <summary>Same order as <see cref="SchemeConfirmModalController.SortingOrder"/>: above
        /// the Settings overlay and the in-game gear modal (both 500), below the hole-complete /
        /// tournament overlays (900) and the toast layer (950).</summary>
        public const int SortingOrder = 600;

        // ── State ─────────────────────────────────────────────────────────────
        private IReadOnlyList<LoadingTip> _hints = Array.Empty<LoadingTip>();
        private int _index;
        private Action? _onFinished;
        private Action<LoadingTip>? _onHintShown;

        private RectTransform _panelRect = null!;
        private LayoutElement _panelLayout = null!;
        private readonly Dictionary<string, Sprite> _spriteByName = new Dictionary<string, Sprite>();

        private Coroutine? _swap;
        private Coroutine? _height;
        private Coroutine? _bump;

        /// <summary>True while a hint → hint swap is running. Both buttons ignore input then —
        /// a guard bool, NOT <c>interactable</c>: toggling interactable for 0.3 s would flash the
        /// disabled look on every tap (§3.3a).</summary>
        private bool _swapping;

        /// <summary>Zero-based index of the hint on screen (probe / test seam).</summary>
        public int Index => _index;

        /// <summary>The group size X (probe / test seam).</summary>
        public int Count => _hints.Count;

        public bool Swapping => _swapping;

        // ── Instance resolution (copy of SchemeConfirmModalController.Instance) ──
        private static ScreenHintModalController? _instance;

        /// <summary>
        /// The modal in the loaded scene, or null when neither scene provides one (an EditMode
        /// test, a scene that has not been re-authored). Callers MUST null-check and carry on —
        /// a scene without the prefab must never strand the player.
        ///
        /// <para>Resolved with <c>FindObjectsInactive.Include</c> because the modal sits in the
        /// scene with its panel deactivated; only the ROOT is active, and that root is what we
        /// need. When ShellScene and LabScaffold are both loaded (mid-round) the in-game one
        /// wins — the one in the non-shell scene.</para>
        /// </summary>
        public static ScreenHintModalController? Instance
        {
            get
            {
                if (_instance != null) return _instance;

                var found = FindObjectsByType<ScreenHintModalController>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);

                if (found.Length == 0) return null;
                if (found.Length > 1)
                {
                    for (int i = 0; i < found.Length; i++)
                        if (found[i].gameObject.scene.name != "ShellScene") { _instance = found[i]; break; }
                }

                if (_instance == null) _instance = found[0];
                return _instance;
            }
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────

        protected override void Awake()
        {
            // Base wires closeButton -> Hide (none here) and deactivates modalPanel + backdrop.
            base.Awake();

            ApplySortingOrder();

            _panelRect = (RectTransform)modalPanel.transform;
            _panelLayout = modalPanel.GetComponent<LayoutElement>();
            if (_panelLayout == null) _panelLayout = modalPanel.AddComponent<LayoutElement>();

            if (backButton != null) backButton.onClick.AddListener(OnBack);
            if (nextButton != null) nextButton.onClick.AddListener(OnNext);

            BuildSpriteIndex();
            _instance = this;
        }

        /// <summary>Own the sorting scope IN CODE rather than as a scene override — see
        /// <see cref="SchemeConfirmModalController"/> for why a prefab cannot author it.</summary>
        private void ApplySortingOrder()
        {
            var canvas = GetComponent<Canvas>();
            if (canvas == null) canvas = gameObject.AddComponent<Canvas>();

            if (!canvas.isRootCanvas) canvas.overrideSorting = true;
            canvas.sortingOrder = SortingOrder;

            if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();
        }

        private void BuildSpriteIndex()
        {
            _spriteByName.Clear();
            if (tipSprites == null) return;
            for (int i = 0; i < tipSprites.Length; i++)
            {
                string n = tipSprites[i].name;
                if (string.IsNullOrEmpty(n) || tipSprites[i].sprite == null) continue;
                _spriteByName[n] = tipSprites[i].sprite;
            }
        }

        protected override void OnDisable()
        {
            if (_instance == this) _instance = null;

            // Base first — the OpenModalCount leak guard — then every motion this modal owns.
            base.OnDisable();
            UiMotion.Stop(this, ref _swap);
            UiMotion.Stop(this, ref _height);
            UiMotion.Stop(this, ref _bump);
            _swapping = false;
            // A force-disable mid-sequence owes nothing: the screen is already marked seen
            // (Architect default a). onFinished is deliberately NOT called.
            _onFinished = null;
            _onHintShown = null;
        }

        // ── Show / advance / close ────────────────────────────────────────────

        /// <summary>
        /// Open on hint 1 of <paramref name="hints"/> (<c>hints.Count &gt;= 1</c>).
        /// </summary>
        /// <param name="onFinished">Called after CLOSE hides the modal — and only then. A modal
        /// hidden any other way (force-disable, scene teardown) does not call it.</param>
        /// <param name="onHintShown">Called with each hint as it becomes the one on screen —
        /// hint 1 on open, then every CONTINUE target. The presenter records these as seen keys.</param>
        public void Show(IReadOnlyList<LoadingTip> hints, Action? onFinished, Action<LoadingTip>? onHintShown = null)
        {
            if (hints == null || hints.Count == 0)
            {
                Debug.LogWarning("[ScreenHint] Show called with no hints — nothing to open.");
                onFinished?.Invoke();
                return;
            }
            if (IsVisible())
            {
                Debug.LogWarning("[ScreenHint] Show called while already visible — ignored.");
                return;
            }

            _hints = hints;
            _onFinished = onFinished;
            _onHintShown = onHintShown;
            _swapping = false;

            // A fresh show: nothing to ease from, and nothing measurable yet (the panel is
            // inactive) — hand the height to the fitter outright, as ProTipCard's instant path does.
            UiMotion.Stop(this, ref _height);
            _panelLayout.preferredHeight = -1f;
            if (tipContentGroup != null) tipContentGroup.alpha = 1f;

            Bind(0);
            base.Show();   // Pop + scrim Fade come from animateShow on the clone
        }

        private void OnNext()
        {
            if (_swapping || !IsVisible()) return;

            if (_index < _hints.Count - 1)
            {
                StartSwap(_index + 1);
                return;
            }

            // Last hint: CLOSE.
            Debug.Log($"[ScreenHint] CLOSE on {_index + 1}/{_hints.Count}");
            Action? done = _onFinished;
            _onFinished = null;
            _onHintShown = null;
            Hide();
            done?.Invoke();
        }

        private void OnBack()
        {
            // Never reachable at n == 1: the button is inactive there. Belt and braces.
            if (_swapping || !IsVisible() || _index == 0) return;
            StartSwap(_index - 1);
        }

        // ── Swap (ProTipCard.NextTip / SwapRoutine / SwapTo, copied) ──────────

        private void StartSwap(int target)
        {
            // The instant path also covers "no coroutine will run": UiMotion.Run settles a
            // routine's REGISTERED final state when motion is off or outside play mode, and
            // SwapRoutine registers none — so the kill switch must rebind directly or the
            // hint would never change at all.
            if (tipContentGroup == null || !UiMotion.Enabled || !Application.isPlaying)
            {
                UiMotion.Stop(this, ref _swap);
                SwapTo(target, instant: true);
                if (tipContentGroup != null)
                    UiMotion.Run(this, ref _swap, UiMotion.Fade(tipContentGroup, 0f, 1f));
                return;
            }

            _swapping = true;
            UiMotion.Run(this, ref _swap, SwapRoutine(target));
        }

        /// <summary>
        /// Out → rebind → in, as ONE routine yielding two <see cref="UiMotion.Fade"/> sweeps.
        /// NOT <c>Then(fadeOut, () =&gt; { Bind(); Run(ref _swap, fadeIn); })</c> — see the
        /// "SwapRoutine" note on <c>ProTipCard</c>: a Then tail that re-enters Run on the same
        /// handle runs twice, and here it would advance two hints per tap.
        /// </summary>
        private IEnumerator SwapRoutine(int target)
        {
            IEnumerator fadeOut = UiMotion.Fade(tipContentGroup, tipContentGroup.alpha, 0f);
            while (fadeOut.MoveNext()) yield return fadeOut.Current;

            SwapTo(target);

            IEnumerator fadeIn = UiMotion.Fade(tipContentGroup, 0f, 1f);
            while (fadeIn.MoveNext()) yield return fadeIn.Current;

            _swapping = false;
        }

        /// <summary>Rebind and ease the plate to its new height (ProTipCard.SwapTo, copied).</summary>
        private void SwapTo(int target, bool instant = false)
        {
            if (instant)
            {
                UiMotion.Stop(this, ref _height);
                _panelLayout.preferredHeight = -1f;   // the ContentSizeFitter owns the height outright
                Bind(target);
                LayoutRebuilder.ForceRebuildLayoutImmediate(_panelRect);
                return;
            }

            float from = _panelLayout.preferredHeight >= 0f
                ? _panelLayout.preferredHeight
                : _panelRect.rect.height;

            // PIN BEFORE THE REBIND. The rebuild would otherwise resize the plate to the new
            // hint's natural height for ONE FRAME before the tween's first value lands (the
            // 220 px flash ProTipCard measured). LayoutElement outranks the VerticalLayoutGroup
            // (priority 1 vs 0), so pinning it holds the fitter at the old height.
            _panelLayout.preferredHeight = from;
            Bind(target);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_panelRect);

            // Measure the new natural height with the LayoutElement out of the way, then put
            // the pin back so the frame ENDS at `from`. Neither rebuild is ever drawn.
            _panelLayout.preferredHeight = -1f;
            LayoutRebuilder.ForceRebuildLayoutImmediate(_panelRect);
            float to = LayoutUtility.GetPreferredHeight(_panelRect);
            _panelLayout.preferredHeight = from;
            LayoutRebuilder.ForceRebuildLayoutImmediate(_panelRect);

            Debug.Log($"[ScreenHint] plate height {from:F1} -> {to:F1}");

            // `to > 0.5f`: a height of zero means the layout could not be measured, never that
            // the plate is genuinely empty. Easing to it is always wrong.
            if (to > 0.5f && Mathf.Abs(to - from) > 0.5f)
            {
                UiMotion.Run(this, ref _height, UiMotion.Then(
                    UiMotion.Tween(from, to, UiMotion.EntryDur, h => _panelLayout.preferredHeight = h),
                    () => _panelLayout.preferredHeight = -1f));
            }
            else
            {
                _panelLayout.preferredHeight = -1f;
            }

            // §3.3a — the counter is the thing that "just changed" in either direction.
            if (counterText != null && counterText.gameObject.activeSelf)
                UiMotion.Run(this, ref _bump, UiMotion.Bump((RectTransform)counterText.transform));
        }

        // ── Binding ───────────────────────────────────────────────────────────

        /// <summary>Point every slot at hint <paramref name="n"/> (zero-based): title, text,
        /// sprite, counter and the two buttons per the §2.1 state table.</summary>
        private void Bind(int n)
        {
            _index = n;
            LoadingTip tip = _hints[n];
            int x = _hints.Count;

            SetKey(titleText, TitleKey);

            // --- TEXT --- (ProTipCard.Show's raw-key fallback, verbatim)
            if (tipText != null)
            {
                string key = tip.IsValid ? tip.key : string.Empty;
                var loc = tipText.GetComponent<LocalizedText>();
                if (loc != null) loc.SetKey(key);
                else             tipText.text = key;
            }

            // --- IMAGE ---
            if (tipImage != null)
            {
                Sprite? sprite = null;
                if (tip.IsValid && !string.IsNullOrEmpty(tip.sprite))
                    _spriteByName.TryGetValue(tip.sprite, out sprite);

                if (sprite != null)
                {
                    tipImage.sprite = sprite;
                    tipImage.preserveAspect = true;
                    tipImage.gameObject.SetActive(true);
                }
                else
                {
                    tipImage.gameObject.SetActive(false);
                }
            }

            // --- COUNTER --- ASCII digits, no key: numbers are not localised anywhere in the game.
            if (counterText != null)
            {
                bool show = x > 1;
                counterText.gameObject.SetActive(show);
                if (show) counterText.text = (n + 1).ToString() + "/" + x.ToString();
            }

            // --- BUTTONS --- present or absent, never disabled.
            bool last = n == x - 1;
            if (backButton != null) backButton.gameObject.SetActive(n > 0);
            SetKey(nextLabel, last ? CloseKey : ContinueKey);

            Debug.Log($"[ScreenHint] bind {n + 1}/{x} {tip.key} back={(n > 0 ? "on" : "off")} gold={(last ? "CLOSE" : "CONTINUE")}");
            _onHintShown?.Invoke(tip);
        }

        /// <summary>Re-key a label through its <see cref="LocalizedText"/>. A label without one is
        /// a wiring bug, not a reason to write <c>.text</c>.</summary>
        private static void SetKey(TextMeshProUGUI label, string key)
        {
            if (label == null) return;

            var loc = label.GetComponent<LocalizedText>();
            if (loc == null)
            {
                Debug.LogError($"[ScreenHint] {label.name} has no LocalizedText — cannot bind {key}.");
                return;
            }

            loc.SetKey(key);
        }
    }
}
