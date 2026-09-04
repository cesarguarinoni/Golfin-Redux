using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Golfin.Audio;
using Golfin.Gameplay.Session;
using Golfin.Gameplay.UI.Controls;
using Golfin.Gameplay.UI.HUD;
using Golfin.Gameplay.UI.ShotUI;
using Golfin.UI.GameplayTransition;
using Golfin.Utilities;
using GolfinRedux.UI;

namespace Golfin.UI.Modals
{
    /// <summary>
    /// In-game settings overlay opened by the gameplay HUD gear
    /// (<c>ShotUI_Canvas/SettingsButton</c> in LabScaffold).
    ///
    /// Two cards:
    ///   • SOUND SETTINGS — SFX + Music sliders bound live to <see cref="AudioManager"/>
    ///     (persistence lives inside AudioManager; nothing is written to PlayerPrefs here).
    ///   • PLAYING        — the live hole's course/hole/par, map, strategy text and rewards,
    ///     plus BACK and (solo only) QUIT.
    ///
    /// QUIT opens a confirm card; CONFIRM discards the round and tears gameplay down through
    /// <see cref="GameplaySceneLoader.UnloadGameplay"/> — the same teardown
    /// <c>VersusResultModalController.NewMatchRoutine()</c> uses. No rewards, RP or stamina are
    /// granted or refunded: the round is simply thrown away.
    ///
    /// This lives in Assembly-CSharp (Assets/Scripts/UI/**) because it needs both the gameplay
    /// assemblies (HoleContext, GameSession) and the shell ones (ScreenManager, HoleDatabaseLoader).
    /// The gear Button is a scene-wired [SerializeField] — Inspector references cross assembly
    /// boundaries fine, so no asmdef reference is added anywhere.
    /// </summary>
    public class InGameSettingsModalController : ModalController
    {
        // ── Entry point ───────────────────────────────────────────────────────
        [Header("Entry point (scene-wired to ShotUI_Canvas/SettingsButton)")]
        [SerializeField] private Button gearButton;

        // ── Sound card ────────────────────────────────────────────────────────
        [Header("Sound card")]
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private Slider musicSlider;

        [Tooltip("Filled Image showing the blue wedge. Driven directly (not through Slider.fillRect) " +
                 "so the wedge always ends exactly under the knob instead of being squashed.")]
        [SerializeField] private Image sfxFillBar;
        [SerializeField] private Image musicFillBar;

        // ── CONTROLS card (control_scheme_seam §3.4, Figma 14090:101896) ──────
        //
        // A 2x2 grid of segment buttons under the sound sliders. Same value as
        // Settings > Controls: both surfaces write ControlSchemeService and repaint from its
        // OnSchemeChanged, so they can never disagree.
        [Header("Controls card")]
        [SerializeField] private GameObject controlsCard;

        [Tooltip("Segment buttons in ControlScheme order: Flick, Pendulum, Tap Timing, Free Swing.")]
        [SerializeField] private Button[] schemeButtons = new Button[4];

        [Tooltip("Each segment's own background Image, same order. Left null the button's own " +
                 "Image is used, which is the normal authoring.")]
        [SerializeField] private Image[] schemeFills = new Image[4];

        [Tooltip("Each segment's label, same order.")]
        [SerializeField] private TextMeshProUGUI[] schemeLabels = new TextMeshProUGUI[4];

        [Header("Controls card — segment look")]
        [Tooltip("Selected segment fill: the RETURN button's gold gradient sprite (Figma 14090:101896).")]
        [SerializeField] private Sprite segmentSelectedSprite;
        [Tooltip("Unselected segment fill: 10% white, 55% white 3px stroke, radius 20.")]
        [SerializeField] private Sprite segmentUnselectedSprite;
        [SerializeField] private Color segmentSelectedTextColor   = new Color32(0x0E, 0x2A, 0x47, 0xFF);
        [SerializeField] private Color segmentUnselectedTextColor = Color.white;

        // ── PLAYING card ──────────────────────────────────────────────────────
        [Header("PLAYING card")]
        [SerializeField] private TextMeshProUGUI courseSubtitleText;
        [SerializeField] private Image holeMapImage;
        [SerializeField] private TextMeshProUGUI strategyText;

        [Header("PLAYING card — rewards")]
        [SerializeField] private GameObject[] rewardSlots = new GameObject[3];
        [SerializeField] private Image[] rewardIcons = new Image[3];
        [SerializeField] private TextMeshProUGUI[] rewardAmounts = new TextMeshProUGUI[3];
        [SerializeField] private Sprite pointsIcon;
        [SerializeField] private Sprite repairKitIcon;
        [SerializeField] private Sprite ballIcon;

        // ── Buttons ───────────────────────────────────────────────────────────
        [Header("Buttons")]
        [SerializeField] private Button backButton;
        [SerializeField] private GameObject quitButtonRoot;
        [SerializeField] private Button quitButton;

        // ── Quit confirmation ─────────────────────────────────────────────────
        [Header("Quit confirmation")]
        [SerializeField] private GameObject confirmDialog;
        [SerializeField] private Button confirmBackButton;
        [SerializeField] private Button confirmQuitButton;

        private bool _quitting;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        protected override void Awake()
        {
            // Base wires closeButton -> Hide and starts modalPanel + backdrop deactivated.
            base.Awake();

            if (confirmDialog != null) confirmDialog.SetActive(false);
        }

        private void OnEnable()
        {
            if (gearButton != null)         gearButton.onClick.AddListener(Toggle);
            if (backButton != null)         backButton.onClick.AddListener(Hide);
            if (quitButton != null)         quitButton.onClick.AddListener(OpenConfirm);
            if (confirmBackButton != null)  confirmBackButton.onClick.AddListener(CloseConfirm);
            if (confirmQuitButton != null)  confirmQuitButton.onClick.AddListener(ConfirmQuit);

            if (sfxSlider != null)   sfxSlider.onValueChanged.AddListener(OnSfxChanged);
            if (musicSlider != null) musicSlider.onValueChanged.AddListener(OnMusicChanged);

            WireSchemeButtons();
            ControlSchemeService.OnSchemeChanged += OnSchemeChangedExternally;
        }

        protected override void OnDisable()
        {
            if (gearButton != null)         gearButton.onClick.RemoveListener(Toggle);
            if (backButton != null)         backButton.onClick.RemoveListener(Hide);
            if (quitButton != null)         quitButton.onClick.RemoveListener(OpenConfirm);
            if (confirmBackButton != null)  confirmBackButton.onClick.RemoveListener(CloseConfirm);
            if (confirmQuitButton != null)  confirmQuitButton.onClick.RemoveListener(ConfirmQuit);

            if (sfxSlider != null)   sfxSlider.onValueChanged.RemoveListener(OnSfxChanged);
            if (musicSlider != null) musicSlider.onValueChanged.RemoveListener(OnMusicChanged);

            UnwireSchemeButtons();
            ControlSchemeService.OnSchemeChanged -= OnSchemeChangedExternally;

            // Base clears the OpenModalCount leak guard.
            base.OnDisable();
        }

        // ── Show / hide ───────────────────────────────────────────────────────

        /// <summary>Gear tap: open when closed, close when open.</summary>
        public void Toggle()
        {
            if (IsVisible()) Hide();
            else             Show();
        }

        public override void Show()
        {
            // The gear is not part of ShotInProgressUiGate's hide list, so guard here instead:
            // the overlay may only be opened with the ball at rest.
            if (ShotInProgressUiGate.ShotInProgress) return;

            // ModalController.Hide() ends its 0.2s fade-out by deactivating modalPanel/backdrop.
            // Re-opening inside that window leaves the stale coroutine running, and it blanks the
            // modal a few frames after it was re-shown (IsVisible()==true, Panel inactive).
            // The gear is a toggle, so tap-tap-tap hits this every time — cancel the fade first.
            StopAllCoroutines();
            base.Show();
        }

        public override void Hide()
        {
            // Symmetric guard: a fade-in still running would keep raising alpha over the fade-out.
            // Safe with respect to the quit teardown — that coroutine is hosted on
            // GameplaySceneLoader, not on this modal, and is started after Hide() returns.
            if (IsVisible()) StopAllCoroutines();
            base.Hide();
        }

        protected override void OnShow()
        {
            if (confirmDialog != null) confirmDialog.SetActive(false);

            BindVolumes();
            BindControlsCard();
            BindPlayingCard();
            ApplyModeGating();
        }

        protected override void OnHide()
        {
            if (confirmDialog != null) confirmDialog.SetActive(false);
        }

        // ── Sound ─────────────────────────────────────────────────────────────

        private void BindVolumes()
        {
            var audio = AudioManager.Instance;
            if (audio == null)
            {
                Debug.LogWarning("[InGameSettings] AudioManager.Instance is null — sliders left at their current values.");
                return;
            }

            // SetValueWithoutNotify so binding the UI never writes back into AudioManager.
            if (sfxSlider != null)   sfxSlider.SetValueWithoutNotify(Mathf.Clamp01(audio.GetSFXVolume() / 100f));
            if (musicSlider != null) musicSlider.SetValueWithoutNotify(Mathf.Clamp01(audio.GetMusicVolume() / 100f));

            SyncBar(sfxFillBar,   sfxSlider);
            SyncBar(musicFillBar, musicSlider);
        }

        // ── Slider wedge ──────────────────────────────────────────────────────
        // The Volume Bar / Volume Background sprites are the 882x180 Figma slider export:
        // the visible track runs from x=96 to x=786 inside them, and the 116px knob therefore
        // travels between centres 154 and 728. Unity's Slider.fillRect would scale the whole
        // sprite into the fill rect (squashing the taper and overshooting the knob), so the
        // wedge is a Filled Image driven here instead: reveal exactly up to the knob centre.
        private const float BarWidth      = 882f;
        private const float KnobTravelMin = 154f;
        private const float KnobTravelMax = 728f;

        private static void SyncBar(Image bar, Slider slider)
        {
            if (bar == null || slider == null) return;
            SetBarFill(bar, slider.normalizedValue);
        }

        private static void SetBarFill(Image bar, float normalized)
        {
            if (bar == null) return;
            float knobCentre = Mathf.Lerp(KnobTravelMin, KnobTravelMax, Mathf.Clamp01(normalized));
            bar.fillAmount = knobCentre / BarWidth;
        }

        // Same 0-1 slider <-> 0-100 AudioManager mapping SoundSettingsSubmenu uses.
        private void OnSfxChanged(float value)
        {
            SetBarFill(sfxFillBar, value);
            if (AudioManager.Instance != null) AudioManager.Instance.SetSFXVolume(value * 100f);
        }

        private void OnMusicChanged(float value)
        {
            SetBarFill(musicFillBar, value);
            if (AudioManager.Instance != null) AudioManager.Instance.SetMusicVolume(value * 100f);
        }

        // ── CONTROLS card ─────────────────────────────────────────────────────
        //
        // The same four options as Settings > Controls, reachable without leaving the hole.
        // Switching mid-swing is safe by construction: ShotSchemeHost defers the root swap to
        // the next Idle, so the shot already in the air keeps the scheme it was hit with.

        private readonly UnityEngine.Events.UnityAction[] _schemeHandlers =
            new UnityEngine.Events.UnityAction[4];

        private void WireSchemeButtons()
        {
            if (schemeButtons == null) return;
            for (int i = 0; i < schemeButtons.Length && i < 4; i++)
            {
                if (schemeButtons[i] == null) continue;
                var scheme = (ControlScheme)i;
                _schemeHandlers[i] = () => OnSchemeSegmentTapped(scheme);
                schemeButtons[i].onClick.AddListener(_schemeHandlers[i]);
            }
        }

        private void UnwireSchemeButtons()
        {
            if (schemeButtons == null) return;
            for (int i = 0; i < schemeButtons.Length && i < 4; i++)
            {
                if (schemeButtons[i] == null || _schemeHandlers[i] == null) continue;
                schemeButtons[i].onClick.RemoveListener(_schemeHandlers[i]);
                _schemeHandlers[i] = null;
            }
        }

        private void OnSchemeSegmentTapped(ControlScheme scheme)
        {
            ControlSchemeService.Set(scheme, "ingame");
            BindControlsCard();   // Set() is silent when the value did not move; repaint regardless.
        }

        private void OnSchemeChangedExternally(ControlScheme scheme) => BindControlsCard();

        private void BindControlsCard()
        {
            if (controlsCard != null && !controlsCard.activeSelf) controlsCard.SetActive(true);

            ControlScheme current = ControlSchemeService.Current;

            for (int i = 0; i < 4; i++)
            {
                bool selected = (int)current == i;

                Image fill = (schemeFills != null && schemeFills.Length > i) ? schemeFills[i] : null;
                if (fill == null && schemeButtons != null && schemeButtons.Length > i && schemeButtons[i] != null)
                    fill = schemeButtons[i].GetComponent<Image>();

                if (fill != null)
                {
                    Sprite wanted = selected ? segmentSelectedSprite : segmentUnselectedSprite;
                    // Only assign when a sprite was authored: a null here would blank the segment
                    // into the flat-fill fabrication the UI-fidelity linter exists to catch.
                    if (wanted != null) fill.sprite = wanted;
                    fill.color = Color.white;
                }

                if (schemeLabels != null && schemeLabels.Length > i && schemeLabels[i] != null)
                {
                    var label = schemeLabels[i];
                    label.text  = LocalizationManager.Get(ControlSchemeService.LabelKey((ControlScheme)i));
                    label.color = selected ? segmentSelectedTextColor : segmentUnselectedTextColor;
                }
            }
        }

        // ── PLAYING card ──────────────────────────────────────────────────────

        private void BindPlayingCard()
        {
            HoleData hole = ResolveCurrentHole();

            // Subtitle — "Lomond Country Club  - Hole 6 - Par 3".
            // courseNameKey already localizes to "<Course>  - Hole N" (EN + JP), so only the
            // par fragment is composed here, exactly as HoleCardController / HoleCompleteCardWidget do.
            if (courseSubtitleText != null)
            {
                int holeNumber = hole != null ? hole.holeNumber : HoleContext.HoleNumber;
                int par        = hole != null ? hole.par        : HoleContext.Par;

                string courseAndHole = (hole != null && !string.IsNullOrEmpty(hole.courseNameKey))
                    ? LocalizationManager.Get(hole.courseNameKey)
                    : $"{HoleContext.CourseName}  - Hole {holeNumber}";

                courseSubtitleText.text = $"{courseAndHole} - Par {par}";
            }

            // Hole map — same Resources path + "Missing" fallback HoleCardController uses.
            if (holeMapImage != null)
            {
                Sprite map = null;
                if (hole != null && !string.IsNullOrEmpty(hole.holeImageName))
                    map = Resources.Load<Sprite>($"HoleImages/{hole.holeImageName}");
                if (map == null)
                    map = Resources.Load<Sprite>("HoleImages/Missing");
                holeMapImage.sprite = map;
            }

            // Strategy text — the localized description already carries the <color=#EEDC9A>
            // emphasis spans, so no emphasis is authored here.
            if (strategyText != null)
            {
                strategyText.text = (hole != null && !string.IsNullOrEmpty(hole.descriptionKey))
                    ? LocalizationManager.Get(hole.descriptionKey)
                    : string.Empty;
            }

            PopulateRewards(hole != null ? hole.rewards : null);
        }

        private void PopulateRewards(List<HoleReward> rewards)
        {
            for (int i = 0; i < 3; i++)
            {
                bool has = rewards != null && i < rewards.Count;

                if (rewardSlots != null && rewardSlots.Length > i && rewardSlots[i] != null)
                    rewardSlots[i].SetActive(has);

                if (!has) continue;

                HoleReward reward = rewards[i];

                if (rewardIcons != null && rewardIcons.Length > i && rewardIcons[i] != null)
                    rewardIcons[i].sprite = GetRewardIcon(reward.type);

                if (rewardAmounts != null && rewardAmounts.Length > i && rewardAmounts[i] != null)
                    rewardAmounts[i].text = $"x{reward.amount}";
            }
        }

        // Same RewardType -> sprite mapping as HoleCardController.GetRewardIcon.
        private Sprite GetRewardIcon(RewardType type)
        {
            switch (type)
            {
                case RewardType.Points:    return pointsIcon;
                case RewardType.RepairKit: return repairKitIcon;
                case RewardType.Ball:      return ballIcon;
                default:                   return null;
            }
        }

        private static HoleData ResolveCurrentHole()
        {
            int holeNumber = HoleContext.HoleNumber > 0 ? HoleContext.HoleNumber : GameSession.CurrentHoleNumber;
            if (holeNumber <= 0) return null;

            // HoleDatabaseLoader is 0-indexed over the ACTIVE course's holes, so index = N-1
            // holds for the shipping single-course database (mirrors HoleCompleteModalController).
            HoleData byIndex = HoleDatabaseLoader.GetHole(holeNumber - 1);
            if (byIndex != null && byIndex.holeNumber == holeNumber) return byIndex;

            // Defensive: fall back to a scan if the index ever stops lining up.
            var db = HoleDatabaseLoader.RuntimeDatabase;
            if (db != null)
            {
                foreach (var h in db.holes)
                    if (h != null && h.holeNumber == holeNumber) return h;
            }

            return byIndex;
        }

        // ── Mode gating ───────────────────────────────────────────────────────

        /// <summary>
        /// QUIT is solo-only. In 1v1 and tournament rounds quitting has forfeit consequences
        /// that are out of scope here, so the button is hidden entirely; the buttons row is a
        /// centered HorizontalLayoutGroup, so BACK re-centers on its own.
        /// </summary>
        private void ApplyModeGating()
        {
            bool isSolo = !GameSession.IsVersus && !TournamentRoundContext.IsActive;
            if (quitButtonRoot != null) quitButtonRoot.SetActive(isSolo);
        }

        // ── Quit flow ─────────────────────────────────────────────────────────

        private void OpenConfirm()
        {
            if (confirmDialog != null) confirmDialog.SetActive(true);
        }

        private void CloseConfirm()
        {
            if (confirmDialog != null) confirmDialog.SetActive(false);
        }

        private void ConfirmQuit()
        {
            if (_quitting) return;
            _quitting = true;

            CloseConfirm();
            Hide();
            // Nothing should linger on screen while the scenes tear down, and the fade-out would
            // outlive the GameObject anyway.
            gameObject.SetActive(false);

            var loader = GameplaySceneLoader.Instance;
            if (loader == null)
            {
                Debug.LogWarning("[InGameSettings] GameplaySceneLoader.Instance is null — cannot unload gameplay.");
                _quitting = false;
                return;
            }

            // CRITICAL: host the teardown on the loader, not on this modal. This modal lives in
            // LabScaffold, so UnloadGameplay destroys it mid-coroutine — everything after the
            // unload (session reset, Home routing) would silently never run. GameplaySceneLoader
            // lives in ShellScene and survives.
            loader.StartCoroutine(QuitRoutine(loader));
        }

        /// <summary>
        /// A frame gap so Hide() lands, then the sanctioned gameplay exit — teardown and the
        /// swap to Home both happen behind GameplaySceneLoader's black curtain, because the
        /// unload takes several frames and the shell scene behind it is empty (bare camera
        /// clear, no UI) the whole time. Static, because `this` is destroyed by the unload
        /// halfway through.
        /// </summary>
        private static IEnumerator QuitRoutine(GameplaySceneLoader loader)
        {
            // Small frame gap so Hide() completes before the curtain drops.
            yield return null;

            yield return loader.ExitToScreen(ScreenId.Home, () =>
            {
                // Full session clear (the Stage D MENU/back-to-Home contract): clears the hole
                // pointer, IsVersus / IsTournament and the tournament round context, so the next
                // hole started from Home begins clean. Runs while the screen is still black, so
                // Home is only ever revealed already-reset.
                GameSession.ResetSession();
                HoleContext.Reset();
            });

            Debug.Log("[InGameSettings] Gameplay unloaded — round discarded, no rewards granted.");
        }

        // ── Editor helper ─────────────────────────────────────────────────────

        /// <summary>
        /// Applies the silver vertex gradient the Figma titles use. Called from the builder so
        /// the gradient is baked into the prefab rather than applied at runtime.
        /// </summary>
        public static void ApplySilverTitle(TextMeshProUGUI text) => TextGradients.ApplySilver(text);
    }
}
