// Assets/Scripts/UI/Gacha/GachaRevealModalController.cs
// gacha_reveal_animation §2 + §4 — the reveal moment between PULL and the Prizes screen.
//
// Sequence (SPEC §2 timeline): A enter → then per prize: B shake, C pop, D land, E hold,
// F exit (every card but the last) → G finish. Auto-play with SKIP; a tap during a hold
// fast-forwards THAT hold only (never a half-drawn card).
//
// Everything is coroutines + Time.unscaledDeltaTime — the project has no tween library
// (see the VersusResultModalController header) and ModalController's own fade is unscaled,
// so a reveal must not stall if something has parked timeScale at 0.
//
// Rarity drives the FX: the six-entry _tiers table (index = (int)CharacterRarity) decides
// shake/hold length, burst density, glow/rays/flash/rain/panel-shake and the stinger.
// Every tint comes from RarityHelper.GetRarityColor — never a hardcoded colour.
#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using Golfin.Audio.Events;
using Golfin.Gameplay.UI.Quality;
using Golfin.Inventory;
using Golfin.Roster;
using Golfin.UI.Modals;
using UnityEngine;
using UnityEngine.UI;

namespace GolfinRedux.UI.Gacha
{
    /// <summary>
    /// Per-rarity FX budget. Six of these live on the modal (index = <see cref="CharacterRarity"/>),
    /// Inspector-tunable; the field initializer seeds the SPEC §4 table so a freshly added
    /// component is already correct.
    /// </summary>
    [Serializable]
    public class RarityFxTier
    {
        public string label = "";

        [Header("Timing (seconds, unscaled)")]
        [Tooltip("Bag shake before the FIRST card of the pull.")]
        public float shakeFirst = 0.6f;
        [Tooltip("Bag shake before every subsequent card — shorter, so an x10 doesn't drag.")]
        public float shakeNext = 0.35f;
        [Tooltip("How long the card rests at the card position before it exits.")]
        public float hold = 0.9f;
        [Tooltip("Doubles the pop duration (Supreme only) so the launch reads as heavier.")]
        public bool slowPop;

        [Header("FX")]
        public bool bagGlow;
        public int burstCount = 12;
        public bool rays;
        public bool flash;
        public float flashDuration = 0.08f;
        public float flashAlpha = 0.6f;
        public bool panelShake;
        public float panelShakeAmplitude = 8f;
        public float panelShakeDuration = 0.3f;
        public bool rain;

        [Header("Audio")]
        [Tooltip("When off, the card lands on GachaCardLand alone (Common).")]
        public bool hasStinger;
        public SfxId stinger = SfxId.GachaRevealUncommon;
    }

    /// <summary>
    /// Scene-instance modal that plays the gacha reveal. Reached through
    /// <see cref="GachaPullFlow.Pull"/> via the static <see cref="Instance"/> — banner cards are
    /// cloned by the carousel at runtime, so a serialized reference to this modal is impossible.
    /// </summary>
    public class GachaRevealModalController : ModalController
    {
        // ── Static access ──────────────────────────────────────────────────────

        public static GachaRevealModalController? Instance { get; private set; }

        // ── Inspector refs ─────────────────────────────────────────────────────

        [Header("Bag")]
        [SerializeField] private RectTransform?  _bagPivot;      // rotates — sits at the bag's BOTTOM
        [SerializeField] private RectTransform?  _bag;           // the Bag.png image rect
        [SerializeField] private Image?          _bagGlow;
        [SerializeField] private Image?          _bagRays;
        [SerializeField] private ParticleSystem? _bagMouthFx;

        [Header("Card")]
        [SerializeField] private RectTransform?  _cardAnchor;
        [SerializeField] private Image?          _cardRays;
        [SerializeField] private ParticleSystem? _cardBurstFx;
        [SerializeField] private ParticleSystem? _cardIdleFx;
        [SerializeField] private ParticleSystem? _cardRainFx;
        [SerializeField] private GameObject?     _cardPrefab;    // Assets/Prefabs/UI/Inventory/BagClubCard.prefab

        [Header("Screen FX")]
        [SerializeField] private Image?          _flash;
        [SerializeField] private RectTransform?  _panelRect;     // shaken on the heavy tiers

        [Header("Buttons")]
        [SerializeField] private Button?         _skipButton;
        [SerializeField] private Button?         _scrimButton;
        [Tooltip("Full-screen transparent catcher INSIDE the panel, above the card and below " +
                 "SKIP, so a tap on the card itself also fast-forwards the hold.")]
        [SerializeField] private Button?         _tapCatcherButton;

        [Header("Timing (seconds, unscaled)")]
        [SerializeField] private float _enterDuration    = 0.35f;
        [SerializeField] private float _popDuration      = 0.45f;
        [SerializeField] private float _exitDuration     = 0.25f;
        [SerializeField] private float _hideFadeDuration = 0.2f;   // mirrors ModalController's fade

        [Header("Geometry")]
        [Tooltip("Where a card is born, relative to CardAnchor — the bag mouth.")]
        [SerializeField] private Vector2 _cardSpawnOffset = new Vector2(0f, -339f);
        [Tooltip("Sideways bulge of the pop arc, in canvas px. Alternates sign per card.")]
        [SerializeField] private float _popArcX = 40f;

        [Header("Rarity FX tiers (index = CharacterRarity)")]
        [SerializeField] private RarityFxTier[] _tiers = BuildDefaultTiers();

        // ── Runtime state ──────────────────────────────────────────────────────

        // UIParticle.scale is 10 on every emitter here (the TapFeedbackFX convention):
        // ParticleSystem values are PS-units, on-screen px = PS-units × scale.
        private const float UiParticleScale = 10f;

        private enum Phase { Idle, Enter, Shake, Pop, Hold, Exit }

        private Phase       _phase = Phase.Idle;
        private bool        _running;
        private bool        _holdSkipped;
        private bool        _finishedInvoked;
        private Coroutine?  _sequence;
        private Coroutine?  _flashRoutine;
        private Coroutine?  _shakeRoutine;
        private Coroutine?  _rayRoutine;
        private GameObject? _liveCard;
        private CanvasGroup? _liveCardGroup;

        /// <summary>True between <see cref="BeginWaiting"/> and <see cref="Continue"/>/<see cref="Abort"/>
        /// — the bag is shaking but no prize is known yet.</summary>
        private bool        _waiting;

        /// <summary>True once StepEnter has dropped the bag in. Continue reads it so the bag is not
        /// dropped a second time when it is already on screen.</summary>
        private bool        _bagHasEntered;

        private IReadOnlyList<PrizeRecord> _prizes = Array.Empty<PrizeRecord>();
        private Action?     _onFinished;
        private QualityTier _quality = QualityTier.High;
        private Vector2     _panelHome;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        protected override void Awake()
        {
            base.Awake();   // hides modalPanel/backdrop, builds the CanvasGroup

            Instance = this;

            if (_panelRect != null) _panelHome = _panelRect.anchoredPosition;

            if (_skipButton != null)
            {
                _skipButton.onClick.RemoveListener(OnSkip);
                _skipButton.onClick.AddListener(OnSkip);
            }
            if (_scrimButton != null)
            {
                _scrimButton.onClick.RemoveListener(OnTapAnywhere);
                _scrimButton.onClick.AddListener(OnTapAnywhere);
            }
            if (_tapCatcherButton != null)
            {
                _tapCatcherButton.onClick.RemoveListener(OnTapAnywhere);
                _tapCatcherButton.onClick.AddListener(OnTapAnywhere);
            }
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(Instance, this)) Instance = null;
        }

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>
        /// Play the reveal for <paramref name="prizes"/> in order, then invoke
        /// <paramref name="onFinished"/> EXACTLY once — at the natural end or on SKIP, in both
        /// cases before the modal has finished fading, so the Prizes screen is already bound and
        /// active underneath when the scrim clears.
        /// </summary>
        public void Play(IReadOnlyList<PrizeRecord> prizes, Action? onFinished)
        {
            if (prizes == null || prizes.Count == 0)
            {
                Debug.LogWarning("[GachaRevealModal] Play called with an empty prize list — " +
                                 "skipping straight to the result.");
                onFinished?.Invoke();
                return;
            }

            BeginWaiting();
            Continue(prizes, onFinished);
        }

        /// <summary>
        /// Open the modal and start shaking the bag with NO prizes yet
        /// (gacha_client_real_pull §4.2).
        ///
        /// <para>
        /// This is what covers the server round trip. There is no spinner and no new UI: the
        /// player already spent a second watching the bag shake before the first card, so waiting
        /// inside that shake is indistinguishable from the reveal they know — right up until the
        /// prizes arrive and <see cref="Continue"/> takes over mid-animation.
        /// </para>
        /// <para>
        /// SKIP is hidden while waiting. Skipping a reveal cuts to the result; there is no result
        /// yet, so the button would have nothing to cut to.
        /// </para>
        /// </summary>
        public void BeginWaiting()
        {
            if (_running)
            {
                Debug.Log("[GachaRevealModal] BeginWaiting ignored — a reveal is already running.");
                return;
            }

            _prizes          = Array.Empty<PrizeRecord>();
            _onFinished      = null;
            _finishedInvoked = false;
            _waiting         = true;

            // Read the tier ONCE per pull: a mid-reveal Settings change must not restyle
            // card 7 of 10 differently from card 6.
            _quality = QualityTierService.Current;

            // The previous run's trailing teardown (it waits out the hide fade) would otherwise
            // reset THIS one mid-flight if a second pull arrives inside that window.
            if (_sequence != null) { StopCoroutine(_sequence); _sequence = null; }

            Show();          // base: scrim over the persistent bars (ModalScrim), panel fade-in
            ResetToIdle();   // in case a previous run was force-closed

            if (_skipButton != null) _skipButton.gameObject.SetActive(false);

            _running = true;
            _sequence = StartCoroutine(WaitingSequence());
        }

        /// <summary>
        /// The server answered: reveal <paramref name="prizes"/> in order and invoke
        /// <paramref name="onFinished"/> exactly once, at the natural end or on SKIP.
        ///
        /// <para>
        /// Called while <see cref="BeginWaiting"/>'s idle shake is running. It does NOT restart the
        /// modal — the bag is already on screen mid-shake, and re-entering would drop it in again.
        /// </para>
        /// </summary>
        public void Continue(IReadOnlyList<PrizeRecord> prizes, Action? onFinished)
        {
            if (prizes == null || prizes.Count == 0)
            {
                Debug.LogWarning("[GachaRevealModal] Continue called with an empty prize list — " +
                                 "skipping straight to the result.");
                Abort();
                onFinished?.Invoke();
                return;
            }

            _prizes          = prizes;
            _onFinished      = onFinished;
            _finishedInvoked = false;
            _waiting         = false;

            if (_skipButton != null) _skipButton.gameObject.SetActive(true);

            if (!_running)
            {
                // Nothing was waiting — a caller went straight to Continue, or the wait was
                // aborted. Open from scratch, which is what Play() used to do.
                _quality = QualityTierService.Current;
                if (_sequence != null) { StopCoroutine(_sequence); _sequence = null; }
                Show();
                ResetToIdle();
                _running = true;
            }
            else if (_sequence != null)
            {
                // Hand the bag over from the idle loop to the real sequence. The bag stays where
                // it is; only the coroutine driving it changes.
                StopCoroutine(_sequence);
            }

            _sequence = StartCoroutine(RevealSequence(skipEnter: _bagHasEntered));
        }

        /// <summary>
        /// The server refused, or could not be reached. Close the modal with NO cards and WITHOUT
        /// invoking a finished callback — the Prizes screen must not open on a pull that did not
        /// happen. The caller shows the toast.
        /// </summary>
        public void Abort()
        {
            if (!_running && !_waiting) return;

            _waiting = false;
            StopSequence();
            ResetToIdle();

            // Deliberately NOT InvokeFinishedOnce: BeginWaiting stored no callback, and on the
            // Continue path a refusal means there is nothing to show. Clearing it makes that
            // structural rather than merely true today.
            _onFinished      = null;
            _finishedInvoked = true;

            if (_skipButton != null) _skipButton.gameObject.SetActive(true);

            Hide();
        }

        /// <summary>SKIP — cut straight to the result. Wired to the SKIP button.</summary>
        public void OnSkip()
        {
            if (!_running) return;

            // The button is hidden while waiting, so this is belt-and-braces: there is no result to
            // cut to before the server answers, and skipping to one would open an empty screen.
            if (_waiting) return;

            SfxBus.Play(SfxId.GachaSkip);
            StopSequence();
            ResetToIdle();
            InvokeFinishedOnce();
            Hide();
        }

        /// <summary>
        /// Tap anywhere on the scrim (or on the card). Ends the current hold early; during the
        /// shake or the pop it is deliberately a no-op so no card is ever cut off half-drawn.
        /// </summary>
        public void OnTapAnywhere()
        {
            if (_phase == Phase.Hold) _holdSkipped = true;
        }

        // ── Modal hooks ────────────────────────────────────────────────────────

        protected override void OnHide()
        {
            // Covers a force-close from outside (a screen change while the reveal runs).
            // The natural end clears _running BEFORE calling Hide(), so this does not double up.
            if (!_running) return;

            // A modal closed while it was still WAITING never had a result to hand over, so
            // InvokeFinishedOnce would fire a callback that does not exist — clearing the flag
            // first is what stops a force-close during the round trip from opening an empty
            // Prizes screen.
            _waiting = false;

            StopSequence();
            ResetToIdle();
            InvokeFinishedOnce();
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            _waiting = false;

            if (_running)
            {
                StopSequence();
                InvokeFinishedOnce();
            }
            ResetToIdle();
        }

        // ── The sequence ───────────────────────────────────────────────────────

        /// <summary>
        /// The idle loop that covers the server round trip: the bag drops in once, then rocks in
        /// place until <see cref="Continue"/> or <see cref="Abort"/> takes over. It never spawns a
        /// card and never finishes on its own — a pull that never answers leaves the player looking
        /// at a shaking bag, which is why every branch of GachaPullFlow.OnPullAnswered ends in
        /// Continue or Abort.
        /// </summary>
        private IEnumerator WaitingSequence()
        {
            yield return StepEnter();                                       // A

            // The FIRST-card shake, looped. Reusing it (rather than a new animation) is what makes
            // the wait invisible: this is exactly what the player sees before card 1 either way.
            var tier = TierFor(CharacterRarity.Common);
            while (_waiting)
                yield return StepShake(tier, tier.shakeFirst, Color.white);
        }

        private IEnumerator RevealSequence(bool skipEnter = false)
        {
            if (!skipEnter) yield return StepEnter();                       // A

            for (int i = 0; i < _prizes.Count; i++)
            {
                PrizeRecord record   = _prizes[i];
                // The SERVER's rarity, carried on the record — never a database lookup. A prize
                // whose row was published after this build shipped still gets the FX tier it was
                // actually rolled at, and the reveal can never disagree with the pull log.
                CharacterRarity rar  = record.Rarity;
                RarityFxTier tier    = TierFor(rar);
                Color tint           = RarityHelper.GetRarityColor(rar);
                bool isFirst         = i == 0;
                bool isLast          = i == _prizes.Count - 1;

                yield return StepShake(tier, isFirst ? tier.shakeFirst : tier.shakeNext, tint);  // B
                yield return StepPop(record, tier, i);                                           // C
                StepLand(tier, tint);                                                            // D
                yield return StepHold(tier);                                                     // E

                if (!isLast) yield return StepExit();                                            // F
            }

            // G — fanfare, hand the result over UNDER the scrim, then fade to it.
            SfxBus.Play(SfxId.GachaRevealComplete);
            _phase   = Phase.Idle;
            _running = false;

            InvokeFinishedOnce();
            Hide();

            // Hold the last card on screen THROUGH the fade — tearing it down at Hide() time
            // would blink it out a fifth of a second before the scrim clears.
            yield return new WaitForSecondsRealtime(_hideFadeDuration + 0.05f);
            ResetToIdle();
            _sequence = null;
        }

        // A — bag drops in.
        private IEnumerator StepEnter()
        {
            _phase = Phase.Enter;
            _bagHasEntered = true;
            SfxBus.Play(SfxId.GachaBagDrop);

            float t = 0f;
            while (t < _enterDuration)
            {
                t += Time.unscaledDeltaTime;
                float s = Mathf.LerpUnclamped(0.6f, 1f, EaseOutBack(Mathf.Clamp01(t / _enterDuration)));
                SetBagScale(s, s);
                yield return null;
            }
            SetBagScale(1f, 1f);
        }

        // B — bag rocks around its bottom pivot, amplitude and frequency both ramping up.
        private IEnumerator StepShake(RarityFxTier tier, float duration, Color tint)
        {
            _phase = Phase.Shake;
            SfxBus.Play(SfxId.GachaBagShake);

            // Each card re-arms the aura from scratch, so a Common following a Legendary does
            // not inherit the previous card's glow.
            bool glow = tier.bagGlow;
            bool rays = tier.rays && _quality != QualityTier.Low;
            if (!glow && _bagGlow != null) SetGraphicColor(_bagGlow, tint, 0f);
            if (!rays && _bagRays != null) SetGraphicColor(_bagRays, tint, 0f);

            float t = 0f;
            float phase = 0f;   // integrate the phase — ramping f directly would jump the angle

            while (t < duration)
            {
                float dt = Time.unscaledDeltaTime;
                t += dt;
                float k = duration > 0f ? Mathf.Clamp01(t / duration) : 1f;

                float amp  = Mathf.Lerp(2f, 7f, k);
                float freq = Mathf.Lerp(6f, 14f, k);
                phase += 2f * Mathf.PI * freq * dt;

                if (_bagPivot != null)
                    _bagPivot.localRotation = Quaternion.Euler(0f, 0f, amp * Mathf.Sin(phase));

                if (glow && _bagGlow != null)
                    SetGraphicColor(_bagGlow, tint, k * 0.85f);

                if (rays && _bagRays != null)
                {
                    SetGraphicColor(_bagRays, tint, k * 0.35f);
                    _bagRays.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -14f * t);
                }

                yield return null;
            }

            if (_bagPivot != null) _bagPivot.localRotation = Quaternion.identity;
        }

        // C — a card launches out of the bag mouth up to the card position.
        private IEnumerator StepPop(PrizeRecord record, RarityFxTier tier, int index)
        {
            _phase = Phase.Pop;

            SpawnCard(record);
            EmitBurst(_bagMouthFx, Mathf.RoundToInt(tier.burstCount * BurstMultiplier()), Color.white, 90f);
            SfxBus.Play(SfxId.GachaCardPop);

            float duration = tier.slowPop ? _popDuration * 2f : _popDuration;
            float arcX     = (index % 2 == 0) ? _popArcX : -_popArcX;
            var   cardRt   = _liveCard != null ? _liveCard.transform as RectTransform : null;

            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / duration);

                if (cardRt != null)
                {
                    float eased = EaseOutBack(k);
                    Vector2 pos = Vector2.LerpUnclamped(_cardSpawnOffset, Vector2.zero, Mathf.Clamp01(eased));
                    pos.x += Mathf.Sin(k * Mathf.PI) * arcX;
                    cardRt.anchoredPosition = pos;

                    float s = Mathf.LerpUnclamped(0.25f, 1f, eased);   // ease-out-back overshoots past 1
                    cardRt.localScale = new Vector3(s, s, 1f);
                }

                if (_liveCardGroup != null)
                    _liveCardGroup.alpha = Mathf.Clamp01(k / 0.3f);

                // Bag recoil — squashes on the launch, back to rest over the first 0.15 s.
                float recoil = t < 0.15f ? Mathf.Lerp(0.94f, 1f, t / 0.15f) : 1f;
                SetBagScale(1f, recoil);

                yield return null;
            }

            if (cardRt != null)
            {
                cardRt.anchoredPosition = Vector2.zero;
                cardRt.localScale = Vector3.one;
            }
            if (_liveCardGroup != null) _liveCardGroup.alpha = 1f;
            SetBagScale(1f, 1f);
        }

        // D — the card has arrived: everything the rarity earns fires here.
        private void StepLand(RarityFxTier tier, Color tint)
        {
            bool extras = _quality != QualityTier.Low;   // Low drops rays / flash / rain
            int burst   = Mathf.RoundToInt(tier.burstCount * BurstMultiplier());

            EmitBurst(_cardBurstFx, burst, tint, 220f);

            if (tier.rays && extras && _cardRays != null)
                _rayRoutine = StartCoroutine(FadeGraphic(_cardRays, tint, 0f, 0.75f, 0.2f));

            if (tier.flash && extras && _flash != null)
                _flashRoutine = StartCoroutine(FlashRoutine(tier));

            if (tier.panelShake && _panelRect != null)
                _shakeRoutine = StartCoroutine(PanelShakeRoutine(tier));

            PlayEmitter(_cardIdleFx, tint, Mathf.Max(4, burst / 4));
            if (tier.rain && extras) PlayEmitter(_cardRainFx, tint, Mathf.Max(8, burst / 2));

            SfxBus.Play(SfxId.GachaCardLand);
            if (tier.hasStinger) SfxBus.Play(tier.stinger);
        }

        // E — the card rests. A tap ends it early.
        private IEnumerator StepHold(RarityFxTier tier)
        {
            _phase       = Phase.Hold;
            _holdSkipped = false;

            float t = 0f;
            while (t < tier.hold && !_holdSkipped)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        // F — the card leaves so the next one can take its place.
        private IEnumerator StepExit()
        {
            _phase = Phase.Exit;
            SfxBus.Play(SfxId.GachaCardExit);

            StopEmitter(_cardIdleFx);
            StopEmitter(_cardRainFx);
            if (_bagGlow != null) StartCoroutine(FadeGraphic(_bagGlow, _bagGlow.color, _bagGlow.color.a, 0f, _exitDuration));
            if (_bagRays != null) StartCoroutine(FadeGraphic(_bagRays, _bagRays.color, _bagRays.color.a, 0f, _exitDuration));
            if (_rayRoutine != null) { StopCoroutine(_rayRoutine); _rayRoutine = null; }
            if (_cardRays != null) _rayRoutine = StartCoroutine(FadeGraphic(_cardRays, _cardRays.color, _cardRays.color.a, 0f, _exitDuration));

            var cardRt = _liveCard != null ? _liveCard.transform as RectTransform : null;

            float t = 0f;
            while (t < _exitDuration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / _exitDuration);

                if (cardRt != null)
                {
                    float s = Mathf.Lerp(1f, 0.6f, k);
                    cardRt.localScale = new Vector3(s, s, 1f);
                    cardRt.anchoredPosition = new Vector2(0f, Mathf.Lerp(0f, 60f, k));
                }
                if (_liveCardGroup != null) _liveCardGroup.alpha = 1f - k;

                yield return null;
            }

            DestroyCard();
        }

        // ── Card instance ──────────────────────────────────────────────────────

        private void SpawnCard(PrizeRecord record)
        {
            DestroyCard();

            if (_cardPrefab == null || _cardAnchor == null)
            {
                Debug.LogWarning("[GachaRevealModal] Card prefab or CardAnchor not wired — no card to show.");
                return;
            }

            _liveCard = Instantiate(_cardPrefab, _cardAnchor);
            _liveCard.name = "RevealCard";

            var rt = _liveCard.transform as RectTransform;
            if (rt != null)
            {
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = _cardSpawnOffset;
                rt.localScale = new Vector3(0.25f, 0.25f, 1f);
            }

            _liveCardGroup = _liveCard.GetComponent<CanvasGroup>();
            if (_liveCardGroup == null) _liveCardGroup = _liveCard.AddComponent<CanvasGroup>();
            _liveCardGroup.alpha = 0f;

            GachaPrizeCardBinder.Bind(_liveCard, record);   // the shared binder
        }

        private void DestroyCard()
        {
            if (_liveCard != null) Destroy(_liveCard);
            _liveCard = null;
            _liveCardGroup = null;
        }

        // ── Sub-effects ────────────────────────────────────────────────────────

        private IEnumerator FlashRoutine(RarityFxTier tier)
        {
            if (_flash == null) yield break;

            float half = Mathf.Max(0.01f, tier.flashDuration * 0.5f);

            float t = 0f;
            while (t < half) { t += Time.unscaledDeltaTime; SetGraphicColor(_flash, Color.white, Mathf.Lerp(0f, tier.flashAlpha, t / half)); yield return null; }
            t = 0f;
            while (t < half) { t += Time.unscaledDeltaTime; SetGraphicColor(_flash, Color.white, Mathf.Lerp(tier.flashAlpha, 0f, t / half)); yield return null; }

            SetGraphicColor(_flash, Color.white, 0f);
            _flashRoutine = null;
        }

        private IEnumerator PanelShakeRoutine(RarityFxTier tier)
        {
            if (_panelRect == null) yield break;

            float t = 0f;
            while (t < tier.panelShakeDuration)
            {
                t += Time.unscaledDeltaTime;
                float decay = 1f - Mathf.Clamp01(t / tier.panelShakeDuration);
                float amp   = tier.panelShakeAmplitude * decay;
                _panelRect.anchoredPosition = _panelHome + new Vector2(
                    Mathf.Sin(t * 90f) * amp,
                    Mathf.Cos(t * 73f) * amp * 0.6f);
                yield return null;
            }

            _panelRect.anchoredPosition = _panelHome;
            _shakeRoutine = null;
        }

        private IEnumerator FadeGraphic(Graphic g, Color rgb, float from, float to, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                SetGraphicColor(g, rgb, Mathf.Lerp(from, to, Mathf.Clamp01(t / duration)));
                yield return null;
            }
            SetGraphicColor(g, rgb, to);
        }

        // ── Particles (TapFeedbackFX conventions) ──────────────────────────────

        private float BurstMultiplier() => _quality switch
        {
            QualityTier.Low  => 0.5f,
            QualityTier.Mid  => 0.75f,
            _                => 1f,
        };

        private static void EmitBurst(ParticleSystem? ps, int count, Color tint, float speedPx)
        {
            if (ps == null || count <= 0) return;

            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.playOnAwake     = false;
            main.loop            = false;
            // Speed and size are authored in canvas px; UIParticle scales PS-units by 10.
            // RANDOMISED, not constant: a one-shot burst where every particle leaves at the
            // same speed expands as a perfect ring — at the bag mouth that read as a dashed
            // circle, indistinguishable from a loading spinner. A speed spread makes it a puff.
            main.startSpeed      = new ParticleSystem.MinMaxCurve(speedPx * 0.3f / UiParticleScale,
                                                                  speedPx / UiParticleScale);
            main.startSize       = new ParticleSystem.MinMaxCurve(14f / UiParticleScale,
                                                                  30f / UiParticleScale);
            main.maxParticles    = Mathf.Max(main.maxParticles, count + 8);
            main.startColor      = new ParticleSystem.MinMaxGradient(Color.white);

            // White core → rarity tint → fade out, so the burst reads as light first.
            ApplyLifetimeGradient(ps, tint);

            var shape = ps.shape;
            shape.enabled        = true;
            shape.shapeType      = ParticleSystemShapeType.Circle;   // radial in the canvas plane
            shape.radius         = 14f / UiParticleScale;
            shape.radiusThickness= 1f;                               // fill the disc, not its edge
            shape.arcMode        = ParticleSystemShapeMultiModeValue.Random;

            ps.Emit(count);
        }

        private static void PlayEmitter(ParticleSystem? ps, Color tint, int rate)
        {
            if (ps == null) return;

            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.playOnAwake     = false;
            main.loop            = true;
            main.startColor      = new ParticleSystem.MinMaxGradient(tint);
            main.maxParticles    = Mathf.Max(16, rate * 4);
            var sz = main.startSize;
            if (sz.mode == ParticleSystemCurveMode.Constant)
                main.startSize = new ParticleSystem.MinMaxCurve(sz.constant * 0.55f, sz.constant);

            var emission = ps.emission;
            emission.rateOverTime = rate;

            ps.Play();
        }

        private static void ApplyLifetimeGradient(ParticleSystem ps, Color tint)
        {
            var grad = new Gradient();
            grad.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(tint,        0.35f),
                    new GradientColorKey(tint,        1f),
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 0.6f),
                    new GradientAlphaKey(0f, 1f),
                });

            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color   = new ParticleSystem.MinMaxGradient(grad);
        }

        private static void StopEmitter(ParticleSystem? ps)
        {
            if (ps == null) return;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        private static void ClearEmitter(ParticleSystem? ps)
        {
            if (ps == null) return;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            ps.Clear(true);
        }

        // ── Teardown / idle ────────────────────────────────────────────────────

        private void StopSequence()
        {
            if (_sequence != null) { StopCoroutine(_sequence); _sequence = null; }
            _running = false;
            _phase   = Phase.Idle;
        }

        /// <summary>
        /// Back to the state a fresh open expects: no card, no running emitter, bag upright and
        /// unscaled, glow / rays / flash invisible, panel at home. Idempotent — it is the single
        /// cleanup path for the natural end, SKIP, OnHide and OnDisable alike, which is what keeps
        /// "SKIP then pull again" from inheriting a rotated bag or a live emitter.
        /// </summary>
        private void ResetToIdle()
        {
            if (_flashRoutine != null) { StopCoroutine(_flashRoutine); _flashRoutine = null; }
            if (_shakeRoutine != null) { StopCoroutine(_shakeRoutine); _shakeRoutine = null; }
            if (_rayRoutine   != null) { StopCoroutine(_rayRoutine);   _rayRoutine   = null; }

            DestroyCard();

            ClearEmitter(_bagMouthFx);
            ClearEmitter(_cardBurstFx);
            ClearEmitter(_cardIdleFx);
            ClearEmitter(_cardRainFx);

            if (_bagPivot != null) _bagPivot.localRotation = Quaternion.identity;
            if (_bagRays != null) _bagRays.rectTransform.localRotation = Quaternion.identity;
            SetBagScale(1f, 1f);

            if (_bagGlow  != null) SetGraphicColor(_bagGlow,  _bagGlow.color,  0f);
            if (_bagRays  != null) SetGraphicColor(_bagRays,  _bagRays.color,  0f);
            if (_cardRays != null) SetGraphicColor(_cardRays, _cardRays.color, 0f);
            if (_flash    != null) SetGraphicColor(_flash,    Color.white,     0f);

            if (_panelRect != null) _panelRect.anchoredPosition = _panelHome;

            _phase         = Phase.Idle;
            _holdSkipped   = false;

            // The bag is back at rest, so the next sequence has to drop it in again. Missing this
            // would make a SECOND pull in the same session start with the bag already on screen.
            _bagHasEntered = false;
        }

        private void InvokeFinishedOnce()
        {
            if (_finishedInvoked) return;
            _finishedInvoked = true;

            var cb = _onFinished;
            _onFinished = null;
            cb?.Invoke();
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private void SetBagScale(float x, float y)
        {
            if (_bag != null) _bag.localScale = new Vector3(x, y, 1f);
        }

        private static void SetGraphicColor(Graphic g, Color rgb, float alpha)
        {
            if (g == null) return;
            g.color = new Color(rgb.r, rgb.g, rgb.b, alpha);
        }

        private RarityFxTier TierFor(CharacterRarity rarity)
        {
            int i = (int)rarity;
            if (_tiers != null && i >= 0 && i < _tiers.Length && _tiers[i] != null) return _tiers[i];
            return new RarityFxTier();
        }

        /// <summary>Ease-out-back — overshoots past 1 and settles. SPEC §2 steps A and C.</summary>
        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float p = t - 1f;
            return 1f + c3 * p * p * p + c1 * p * p;
        }

        // The SPEC §4 table, as the field initializer so a component added via script/Inspector
        // starts correct instead of six Common-shaped blanks.
        private static RarityFxTier[] BuildDefaultTiers() => new[]
        {
            new RarityFxTier { label = "Common",    shakeFirst = 0.6f, shakeNext = 0.35f, hold = 0.9f, burstCount = 12 },
            new RarityFxTier { label = "Uncommon",  shakeFirst = 0.6f, shakeNext = 0.35f, hold = 1.0f, burstCount = 20,
                               hasStinger = true, stinger = SfxId.GachaRevealUncommon },
            new RarityFxTier { label = "Rare",      shakeFirst = 0.7f, shakeNext = 0.40f, hold = 1.2f, burstCount = 32,
                               bagGlow = true, rays = true,
                               hasStinger = true, stinger = SfxId.GachaRevealRare },
            new RarityFxTier { label = "Mythic",    shakeFirst = 0.9f, shakeNext = 0.50f, hold = 1.6f, burstCount = 48,
                               bagGlow = true, rays = true, flash = true, flashDuration = 0.08f, flashAlpha = 0.6f,
                               hasStinger = true, stinger = SfxId.GachaRevealMythic },
            new RarityFxTier { label = "Legendary", shakeFirst = 1.1f, shakeNext = 0.60f, hold = 2.0f, burstCount = 72,
                               bagGlow = true, rays = true, flash = true, flashDuration = 0.12f, flashAlpha = 0.8f,
                               panelShake = true, panelShakeAmplitude = 8f, panelShakeDuration = 0.3f, rain = true,
                               hasStinger = true, stinger = SfxId.GachaRevealLegendary },
            new RarityFxTier { label = "Supreme",   shakeFirst = 1.3f, shakeNext = 0.70f, hold = 2.4f, burstCount = 96,
                               bagGlow = true, rays = true, flash = true, flashDuration = 0.15f, flashAlpha = 1.0f,
                               panelShake = true, panelShakeAmplitude = 12f, panelShakeDuration = 0.4f, rain = true,
                               slowPop = true,
                               hasStinger = true, stinger = SfxId.GachaRevealSupreme },
        };
    }
}
