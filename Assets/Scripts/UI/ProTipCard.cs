#nullable enable
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;
using Golfin.UI.Polish;
using Golfin.Gps.UI;
using GolfinRedux.UI;

/// <summary>
/// Pro Tip card for the Loading Screen.
///
/// <para>WHAT CHANGED (loading_tips): the card no longer owns the tip list. It reads
/// <c>LoadingTips.csv</c>, hands the rows to a <see cref="LoadingTipSequencer"/> and shows
/// whatever that returns — so the tutorial order, the "each tip twice" rule and the
/// no-repeat-within-five randomiser live in one testable place instead of in a
/// <c>string[]</c> beside a <c>Sprite[]</c> matched by index.</para>
///
/// <para>Sprites are looked up BY NAME (<see cref="TipSprite"/>), which is the whole point:
/// under the old index matching, adding a key without adding a sprite in the same slot
/// shifted every image one tip down and nothing complained.</para>
///
/// <para>Motion is §3.3a — every moving thing here goes through <see cref="UiMotion"/>.
/// Card auto-resizes via VerticalLayoutGroup + ContentSizeFitter; the resize is EASED
/// rather than snapped, because a 628 px diagram following a 300 px one used to jump the
/// card in a single frame.</para>
/// </summary>
public class ProTipCard : MonoBehaviour, IPointerClickHandler
{
    /// <summary>A sprite and the catalog name that selects it. Replaces the index-matched
    /// <c>Sprite[]</c>; the name is the CSV's `sprite` cell and the PNG's file name.</summary>
    [Serializable]
    public struct TipSprite
    {
        public string name;
        public Sprite sprite;
    }

    [Header("References")]
    [SerializeField] private TextMeshProUGUI headerText;
    [SerializeField] private TextMeshProUGUI tipText;
    [SerializeField] private TextMeshProUGUI tapNextText;
    [SerializeField] private Image dividerImage;

    [Header("Tip Images")]
    [Tooltip("One entry per tip, selected by the CSV's `sprite` name. Missing = text-only tip.")]
    [SerializeField] private TipSprite[] tipSprites = Array.Empty<TipSprite>();
    [SerializeField] private Image tipImageDisplay;

    [Header("Tip Catalog")]
    [Tooltip("Assets/Resources/Data/LoadingTips.csv")]
    [SerializeField] private TextAsset tipsCsv;

    [Tooltip("The text + image wrapper that cross-fades on a tip swap. Falls back to the " +
             "text object alone if unwired.")]
    [SerializeField] private CanvasGroup tipContentGroup;

    [Header("Settings")]
    [SerializeField] private float autoCycleInterval = 8f;

    [Tooltip("One full min→max→min sweep of the TAP FOR NEXT TIP label. Mirrors " +
             "DailyMissionPillController.glowPeriod.")]
    [SerializeField] private float tapPulsePeriod = 1.6f;

    /// <summary>§3.3a — rest alpha of the tap label between sweeps (Architect default).</summary>
    const float TapPulseMin = 0.55f;
    const float TapPulseMax = 1f;

    private bool _initialized;
    private Coroutine _autoCycleCoroutine;
    private LayoutElement _cardLayout;
    private RectTransform _cardRect;
    private CanvasGroup _cardGroup;
    private CanvasGroup _tapGroup;
    private LoadingTipSequencer _seq;
    private readonly Dictionary<string, Sprite> _spriteByName = new Dictionary<string, Sprite>();

    private Coroutine? _swap;
    private Coroutine? _height;
    private Coroutine? _tapPulse;
    private Coroutine? _rise;

    private void Awake()
    {
        _cardRect = (RectTransform)transform;

        _cardLayout = GetComponent<LayoutElement>();
        if (_cardLayout == null) _cardLayout = gameObject.AddComponent<LayoutElement>();

        _cardGroup = EnsureGroup(gameObject);

        // The swap group is authored in the scene (TipContent). Falling back to the text
        // object keeps the card working if the wrapper is ever unwired, at the cost of the
        // image not fading with it.
        if (tipContentGroup == null && tipText != null)
            tipContentGroup = EnsureGroup(tipText.gameObject);

        if (tapNextText != null) _tapGroup = EnsureGroup(tapNextText.gameObject);

        BuildSpriteIndex();
    }

    private static CanvasGroup EnsureGroup(GameObject go)
    {
        CanvasGroup g = go.GetComponent<CanvasGroup>();
        return g != null ? g : go.AddComponent<CanvasGroup>();
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

    private void Start()
    {
        if (!_initialized)
            Initialize();
    }

    /// <summary>
    /// Build the sequencer from the catalog + the persisted position and show a tip.
    ///
    /// <para>THE DOUBLE-ADVANCE GUARD (§3.3). Every enable after this one advances, so the
    /// player never sees a loading screen open on the tip the previous one opened on. This
    /// first show advances TOO — unless the store is empty, i.e. a fresh install, where
    /// advancing would skip TIP_SWING and open the game on tip 2.</para>
    /// </summary>
    public void Initialize()
    {
        _initialized = true;

        _seq = new LoadingTipSequencer(
            tipsCsv != null ? LoadingTipCatalog.Load(tipsCsv) : LoadingTipCatalog.LoadFromResources(),
            LoadingTipStore.Load(),
            null);

        Show(_seq.HasHistory ? _seq.Advance() : _seq.Current);
        LoadingTipStore.Save(_seq.State);

        RestartAutoCycle();
        ArmTapPulse();
    }

    private void OnEnable()
    {
        // Start has not run on the very first enable; it will Initialize and show tip 1.
        if (!_initialized) return;

        NextTip(instant: true);
        RestartAutoCycle();

        // §3.3a — the card arrives, it does not appear. Gated like every other arrival, in
        // case LoadingScreenController ever becomes a push target.
        if (!GpsPaintMotion.SuppressedByPush)
            UiMotion.Run(this, ref _rise, UiMotion.Rise(_cardRect, _cardGroup));
    }

    /// <summary>Bind one row: text, sprite, and the layout rebuild that follows both.</summary>
    public void Show(LoadingTip tip)
    {
        // --- TEXT ---
        if (tipText != null)
        {
            string key = tip.IsValid ? tip.key : string.Empty;
            var loc = tipText.GetComponent<LocalizedText>();
            if (loc != null) loc.SetKey(key);
            else             tipText.text = key;
        }

        // --- IMAGE ---
        if (tipImageDisplay != null)
        {
            Sprite sprite = null;
            if (tip.IsValid && !string.IsNullOrEmpty(tip.sprite))
                _spriteByName.TryGetValue(tip.sprite, out sprite);

            if (sprite != null)
            {
                tipImageDisplay.sprite = sprite;
                tipImageDisplay.preserveAspect = true;
                tipImageDisplay.gameObject.SetActive(true);
            }
            else
            {
                tipImageDisplay.gameObject.SetActive(false);
            }
        }

        // Force layout so the card measures its new content.
        LayoutRebuilder.ForceRebuildLayoutImmediate(_cardRect);
    }

    /// <summary>Advance the sequencer and cross-fade to the new tip.</summary>
    /// <param name="instant">Skip the fade-out (the screen was just enabled — there is
    /// nothing on it to fade away from).</param>
    public void NextTip(bool instant = false)
    {
        if (!_initialized) { Initialize(); return; }

        // The instant path also covers "no coroutine will run": UiMotion.Run settles a routine's
        // REGISTERED final state when motion is off or we are not in play mode, and SwapRoutine
        // registers none (that is the point — see its note). Without this, the motion kill switch
        // would not make the swap instant, it would stop the tip changing at all.
        if (instant || tipContentGroup == null || !UiMotion.Enabled || !Application.isPlaying)
        {
            UiMotion.Stop(this, ref _swap);
            SwapTo(_seq.Advance());
            if (tipContentGroup != null)
                UiMotion.Run(this, ref _swap, UiMotion.Fade(tipContentGroup, 0f, 1f));
            return;
        }

        UiMotion.Run(this, ref _swap, SwapRoutine());
    }

    /// <summary>
    /// Out → rebind → in, as ONE routine yielding two <see cref="UiMotion.Fade"/> sweeps.
    ///
    /// <para>NOT <c>Then(fadeOut, () =&gt; { Advance(); Run(ref _swap, fadeIn); })</c>, which is
    /// what this was and which ADVANCED THE SEQUENCER TWICE PER TAP. <c>Then</c> runs its tail in
    /// two places — at the routine's end, and again from the finalizer it registers, because an
    /// interrupted sequence still owes its tail. Re-entering <see cref="UiMotion.Run"/> on the
    /// SAME handle from inside that tail settles the routine that is running it, the settle fires
    /// the finalizer, and the finalizer runs the tail a second time. The player saw tips 1, 3, 5, 7
    /// and never 2, 4, 6, 8 — invisible in an alpha or height trace, because both advances land in
    /// the same frame and only the last one is drawn. It is the same shape as the re-arming
    /// <c>Then</c> tail that took the Editor down in <c>DailyMissionPillController.StartGlow</c>.</para>
    ///
    /// <para>An interrupted swap is safe without a registered finalizer: the next swap's fade-out
    /// starts from whatever alpha it finds, and <c>OnEnable</c>'s instant path fades from 0 to 1,
    /// so the card cannot be stranded translucent.</para>
    /// </summary>
    private IEnumerator SwapRoutine()
    {
        IEnumerator fadeOut = UiMotion.Fade(tipContentGroup, tipContentGroup.alpha, 0f);
        while (fadeOut.MoveNext()) yield return fadeOut.Current;

        SwapTo(_seq.Advance());

        IEnumerator fadeIn = UiMotion.Fade(tipContentGroup, 0f, 1f);
        while (fadeIn.MoveNext()) yield return fadeIn.Current;
    }

    /// <summary>Rebind, ease the card to its new height, persist, and re-arm the tap pulse.</summary>
    private void SwapTo(LoadingTip tip)
    {
        float from = _cardLayout.preferredHeight >= 0f
            ? _cardLayout.preferredHeight
            : _cardRect.rect.height;

        // PIN BEFORE THE REBIND. Show()'s ForceRebuildLayoutImmediate would otherwise resize the
        // card to the new tip's natural height for ONE FRAME before the tween's first value
        // lands — measured at 877.5 -> 1097.5 -> 942.3 -> ease, i.e. a 220 px flash up and back
        // on every swap between a short and a tall diagram. LayoutElement outranks the
        // VerticalLayoutGroup (priority 1 vs 0), so pinning it holds the fitter at the old
        // height through every rebuild in this frame.
        _cardLayout.preferredHeight = from;
        Show(tip);

        // Measure the new natural height with the LayoutElement out of the way, then put the pin
        // back so the frame ENDS at `from`. Both rebuilds happen inside this one frame, so
        // neither is ever drawn.
        _cardLayout.preferredHeight = -1f;
        LayoutRebuilder.ForceRebuildLayoutImmediate(_cardRect);
        float to = LayoutUtility.GetPreferredHeight(_cardRect);
        _cardLayout.preferredHeight = from;
        LayoutRebuilder.ForceRebuildLayoutImmediate(_cardRect);

        if (Mathf.Abs(to - from) > 0.5f)
        {
            UiMotion.Run(this, ref _height, UiMotion.Then(
                UiMotion.Tween(from, to, UiMotion.EntryDur, h => _cardLayout.preferredHeight = h),
                () => _cardLayout.preferredHeight = -1f));
        }
        else
        {
            // Same height either way — hand the card straight back to the fitter.
            _cardLayout.preferredHeight = -1f;
        }

        LoadingTipStore.Save(_seq.State);
        ArmTapPulse();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        NextTip();
        RestartAutoCycle();
    }

    /// <summary>§3.3a — the label answers the press. One sweep after another, forever, in the
    /// shape <c>DailyMissionPillController.GlowLoop</c> uses; a self-re-arming
    /// <c>UiMotion.Then</c> tail is unbounded by construction (see that file's note).</summary>
    private void ArmTapPulse()
    {
        if (_tapGroup == null || tapPulsePeriod <= 0f) return;
        UiMotion.Run(this, ref _tapPulse, TapPulseLoop());
    }

    private IEnumerator TapPulseLoop()
    {
        while (true)
        {
            IEnumerator sweep = UiMotion.Pulse(_tapGroup, TapPulseMin, TapPulseMax, 1, tapPulsePeriod);
            while (sweep.MoveNext()) yield return sweep.Current;
        }
    }

    private void RestartAutoCycle()
    {
        if (_autoCycleCoroutine != null)
            StopCoroutine(_autoCycleCoroutine);

        _autoCycleCoroutine = StartCoroutine(AutoCycleRoutine());
    }

    private IEnumerator AutoCycleRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(autoCycleInterval);
            NextTip();
        }
    }

    private void OnDisable()
    {
        if (_autoCycleCoroutine != null)
        {
            StopCoroutine(_autoCycleCoroutine);
            _autoCycleCoroutine = null;
        }

        UiMotion.Stop(this, ref _swap);
        UiMotion.Stop(this, ref _height);
        UiMotion.Stop(this, ref _tapPulse);
        UiMotion.Stop(this, ref _rise);
    }
}
