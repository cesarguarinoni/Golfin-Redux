#nullable enable
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Golfin.UI.Polish;
using Golfin.Roster;
using GolfinRedux.UI.Gacha;

namespace Golfin.UI
{
    /// <summary>
    /// Singleton manager for UI elements that persist across scenes (Top Bar, Bottom Nav)
    /// </summary>
    public class PersistentUIManager : MonoBehaviour
    {
        public static PersistentUIManager Instance { get; private set; }

        // Last screen passed to HighlightScreen — lets a language change re-resolve the centre title.
        private GolfinRedux.UI.ScreenId? _lastHighlightedScreen;

        [Header("Top Bar References")]
        public GameObject topBarPanel;

        [Tooltip("safe_area_top_bar (smoke #2): holds ONLY the CENTER ticket cluster (TicketIcon, TicketCountText, " +
                 "ShopPlusButton + the runtime TicketCountBackground pill) — the elements that sit under the " +
                 "centered Dynamic Island. Its SafeAreaFitter uses a 141px baseline (iPhone 14 top inset) and moves " +
                 "them ONLY by the excess on a larger cutout (0 on a 14, ~36px on a 14 Pro Max). The RP cluster " +
                 "(top-left) + Settings (top-right) + UsernameText stay on topBarPanel — they flank the Island and " +
                 "don't move. Chrome/demo logic keys off this + topBarPanel.")]
        public GameObject topBarContent;
        public Image rewardPointsIcon;
        public TMPro.TextMeshProUGUI rewardPointsText;
        public Button settingsButton;
        public TMPro.TextMeshProUGUI usernameText;

        [Header("Top Bar — Gacha Ticket Counter (Stage 1)")]
        [SerializeField] public TMPro.TextMeshProUGUI? ticketCountText;
        [SerializeField] public Button? shopPlusButton;

        // Cached real username so HighlightScreen can restore it on Home.
        private string _username = string.Empty;

        [Header("Bottom Navigation Bar References")]
        public GameObject bottomNavPanel;
        public Button homeButton;
        public Button gachaButton;
        public Button mainPlayButton;
        public Button inventoryButton;
        public Button charactersButton;

        [Header("Bottom Nav Icon Highlight")]
        [Tooltip("Image component on each nav button. Tinted activeColor when its screen is active, normalColor otherwise.")]
        public Image homeIcon;
        public Image gachaIcon;
        public Image mainPlayIcon;
        public Image inventoryIcon;
        public Image charactersIcon;

        public Color iconNormalColor = Color.white;

        /// <summary>
        /// DEAD, and kept only so existing prefabs deserialize without a warning.
        ///
        /// <para>game_polish_a §D7 replaced the tinted-slot selected state. Each nav slot is ONE
        /// baked sprite carrying navy disc, gold ring and white glyph, so tinting it cyan turned
        /// all three cyan — Cesar, 2026-09-03: it "looks ugly". The selected state is now a gold
        /// halo plus a brighter ring, drawn by <see cref="Golfin.UI.Polish.NavSlotHighlight"/>,
        /// and every slot's <c>Image.color</c> stays <see cref="iconNormalColor"/> (white) on
        /// every screen. Nothing reads this at runtime any more —
        /// <c>grep -rn "iconActiveColor" Assets/Scripts</c> is the A15 evidence.</para>
        /// </summary>
        [System.Obsolete("game_polish_a §D7 — the selected state is NavSlotHighlight (gold halo + " +
                         "brighter ring). This field is unread; it survives only for prefab compatibility.")]
        [Tooltip("UNUSED since game_polish_a §D7 — the selected state is NavSlotHighlight.")]
        public Color iconActiveColor = Color.cyan;

        [Header("Bottom Nav Selected State (game_polish_a §D7)")]
        [Tooltip("Gold halo behind a selected 156 px slot. Baked by Docs/Scripts/make_nav_selected.py.")]
        public Sprite? navSlotGlowSmall;
        [Tooltip("Gold halo behind the selected 238 px TEE / CAMERA slot.")]
        public Sprite? navSlotGlowLarge;
        [Tooltip("Brighter #FCF195 ring over a selected 156 px slot.")]
        public Sprite? navSlotRingSmall;
        [Tooltip("Brighter #FCF195 ring over the selected 238 px TEE / CAMERA slot.")]
        public Sprite? navSlotRingLarge;

        public enum Screen
        {
            Home,
            Gacha,
            MainPlay,
            Inventory,
            Characters,
            Settings
        }

        private Screen currentScreen = Screen.Home;

        private void Awake()
        {
            // Singleton pattern
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Cache the designer-set username text (e.g. "CHOTO") at startup.
            // HighlightScreen will restore it when navigating to Home.
            if (usernameText != null)
                _username = usernameText.text;

            // Hide by default (show when HomeScreen loads)
            HideBars();

            InitializeButtons();
        }

        private void OnEnable()
        {
            // RewardPointsManager may not exist yet if we're very early in startup —
            // Start() below covers that case.
            if (RewardPointsManager.Instance != null)
            {
                RewardPointsManager.Instance.OnPointsChanged += SetRewardPoints;
                SetRewardPoints(RewardPointsManager.Instance.GetPoints());
            }

            // GachaTicketManager — same early-boot guard as RP.
            if (GachaTicketManager.Instance != null)
            {
                GachaTicketManager.Instance.OnTicketsChanged += OnTicketCountChanged;
                SetTickets(GachaTicketManager.Instance.GetTickets(TicketType.Standard));
            }

            // The top-bar centre title ("MODE SELECTION", "LEADERBOARD", …) is resolved once, at
            // navigation time, inside HighlightScreen. The language toggle lives in the Settings
            // OVERLAY — no navigation happens when it is used — so the title kept the old language
            // until the player left and re-entered the screen. Re-resolve it in place instead.
            LocalizationManager.OnLanguageChanged += RefreshTopBarCenterText;
        }

        /// <summary>Re-apply the centre title for whatever screen is currently highlighted.</summary>
        private void RefreshTopBarCenterText()
        {
            if (_lastHighlightedScreen.HasValue && NavTitleKeyFor(_lastHighlightedScreen.Value) != null)
                ApplyTopBarCenterText(_lastHighlightedScreen.Value);
        }

        private void Start()
        {
            // By Start() all Awake() calls are done, so Instance is guaranteed available.
            // Subscribe only if OnEnable() missed it (Instance was null at that point).
            if (RewardPointsManager.Instance != null)
            {
                // Re-subscribing a delegate that's already subscribed would double-fire,
                // so remove first to ensure exactly one subscription.
                RewardPointsManager.Instance.OnPointsChanged -= SetRewardPoints;
                RewardPointsManager.Instance.OnPointsChanged += SetRewardPoints;
                SetRewardPoints(RewardPointsManager.Instance.GetPoints());
            }
            else
            {
                Debug.LogWarning("[PersistentUI] RewardPointsManager not found — RP display will not update.");
            }

            // GachaTicketManager — same double-subscribe guard.
            if (GachaTicketManager.Instance != null)
            {
                GachaTicketManager.Instance.OnTicketsChanged -= OnTicketCountChanged;
                GachaTicketManager.Instance.OnTicketsChanged += OnTicketCountChanged;
                SetTickets(GachaTicketManager.Instance.GetTickets(TicketType.Standard));
            }
            else
            {
                Debug.LogWarning("[PersistentUI] GachaTicketManager not found — ticket display will not update.");
            }

            EnsureTicketPill();
            EnsureRewardPointsAlignment();
            EnsureTicketCountAlignment();

            // gps_standalone_shell §D5 — EnsureTicketPill just CREATED an element of the cluster
            // the shell hides, so trim once here as well as on every ShowBars. No-op in the game.
            ApplyStandaloneChrome();
        }

        /// <summary>Padding kept between a counter's right edge and its pill's inner edge.</summary>
        private const float CounterPillPadding = 12f;

        /// <summary>
        /// Right-align the top-bar RP label inside its pill (see <see cref="AlignCounterInPill"/>).
        /// </summary>
        private void EnsureRewardPointsAlignment()
        {
            if (rewardPointsText == null || topBarPanel == null) return;
            AlignCounterInPill(rewardPointsText,
                               topBarPanel.transform.Find("RewardPointsBackground") as RectTransform);
        }

        /// <summary>
        /// Same treatment for the ticket counter, so both pills read with one right-hand gap.
        /// Runs after <see cref="EnsureTicketPill"/>, which is what creates the pill it measures.
        /// </summary>
        private void EnsureTicketCountAlignment()
        {
            if (ticketCountText == null) return;
            var host = ticketCountText.transform.parent;   // TopBarContent
            if (host == null) return;
            AlignCounterInPill(ticketCountText, host.Find("TicketCountBackground") as RectTransform);
        }

        /// <summary>
        /// Right-align a top-bar counter and pull its rect's right edge
        /// <see cref="CounterPillPadding"/> inside the pill behind it. Done at runtime, like
        /// <see cref="EnsureTicketPill"/>: both labels are authored LEFT-aligned with a rect that
        /// overhangs its pill (RP by 17px, tickets by 23px), so the digits drifted toward — and
        /// with a wide enough value, past — the pill's right edge, and the gap moved with the
        /// digit count. Right-aligning alone would not have been enough: the overhanging rect
        /// would simply have parked the digits outside. Aligning AND insetting fixes the gap at
        /// one value for both counters, whatever the balance. The left edge stays where it is
        /// (clear of each cluster's icon), so a long value grows leftward inside the pill.
        /// Idempotent: a second pass finds no overhang and changes nothing.
        /// </summary>
        private static void AlignCounterInPill(TMPro.TextMeshProUGUI label, RectTransform? pill)
        {
            if (label == null) return;
            label.alignment = TMPro.TextAlignmentOptions.MidlineRight;

            var rt     = label.rectTransform;
            var parent = rt.parent as RectTransform;
            if (parent == null || pill == null || pill.parent != parent) return;

            var corners = new Vector3[4];
            pill.GetWorldCorners(corners);
            float pillRight = parent.InverseTransformPoint(corners[2]).x;   // top-right
            rt.GetWorldCorners(corners);
            float textRight = parent.InverseTransformPoint(corners[2]).x;

            float overhang = textRight - (pillRight - CounterPillPadding);
            if (overhang <= 0.5f) return;

            // Shrink from the right only: narrowing by `overhang` moves the right edge in by
            // (1 - pivot.x) * overhang and the left edge out by pivot.x * overhang, so the
            // anchoredPosition shift cancels the left-edge drift for any pivot.
            rt.sizeDelta        = new Vector2(rt.sizeDelta.x - overhang, rt.sizeDelta.y);
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x - overhang * rt.pivot.x,
                                              rt.anchoredPosition.y);
        }

        /// <summary>
        /// Ensure an RP-style pill sits behind the top-bar ticket counter. Built at runtime because
        /// the top bar is a plain scene object whose file reserializes wholesale on save — so we
        /// avoid a massive scene diff. Idempotent; mirrors the RewardPointsBackground pill (#122C47).
        /// </summary>
        private void EnsureTicketPill()
        {
            if (ticketCountText == null) return;
            // safe_area_top_bar: the ticket cluster (icon/count/shop + this pill) lives in the nudged
            // TopBarContent; the RewardPointsBackground template stays on topBarPanel with the RP cluster.
            var host = ticketCountText.transform.parent;   // TopBarContent (nudged center cluster)
            if (host == null || host.Find("TicketCountBackground") != null) return;

            var rp   = topBarPanel != null ? topBarPanel.transform.Find("RewardPointsBackground") : null;
            var icon = host.Find("TicketIcon");
            if (rp == null || icon == null) return;

            var pill = Instantiate(rp.gameObject, host);
            pill.name = "TicketCountBackground";
            var prt = pill.GetComponent<RectTransform>();
            // Center-anchor (0.5) so the ticket cluster stays centered as the top bar
            // stretches full-width on wider devices. Offset = designX(575) - barCenter(589).
            prt.anchorMin = new Vector2(0.5f, 0.5f);
            prt.anchorMax = new Vector2(0.5f, 0.5f);
            prt.pivot     = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(138f, 54f);
            prt.anchoredPosition = new Vector2(575f - 589f, 0f);   // -14: left end tucks under the ticket, right ~8px from Shop+

            // Render order: pill behind, then ticket icon + count + shop draw on top of it.
            pill.transform.SetAsLastSibling();
            icon.SetAsLastSibling();
            ticketCountText.transform.SetAsLastSibling();
            var shop = host.Find("ShopPlusButton");
            if (shop != null) shop.SetAsLastSibling();
        }

        private void OnDisable()
        {
            if (RewardPointsManager.Instance != null)
                RewardPointsManager.Instance.OnPointsChanged -= SetRewardPoints;

            // GachaTicketManager unsubscribe.
            if (GachaTicketManager.Instance != null)
                GachaTicketManager.Instance.OnTicketsChanged -= OnTicketCountChanged;

            LocalizationManager.OnLanguageChanged -= RefreshTopBarCenterText;
        }

        /// <summary>
        /// Show Top Bar and Bottom Nav (call from HomeScreen onwards)
        /// </summary>
        public void ShowBars()
        {
            ShowTopBar(true);
            SetTopBarChromeVisible(true);
            ShowBottomNav(true);
            ApplyDemoTopBarTrim();
            ApplyStandaloneChrome();
        }

        /// <summary>
        /// demo_build_slice §3.4 soft-gating: hide the top-bar Reward Points chrome when RP is
        /// disabled in the demo. Called AFTER SetTopBarChromeVisible(true), which re-shows every
        /// top-bar child, so this must re-hide it. Idempotent. No-op in the full game.
        /// </summary>
        private void ApplyDemoTopBarTrim()
        {
            if (!GolfinRedux.Demo.DemoGate.IsDemo) return;
            if (GolfinRedux.Demo.DemoConfig.Instance.PointsEnabled) return;
            if (rewardPointsText != null) rewardPointsText.gameObject.SetActive(false);
            if (rewardPointsIcon != null) rewardPointsIcon.gameObject.SetActive(false);
            // safe_area_top_bar: the RP cluster (incl. RewardPointsBackground) stays on topBarPanel —
            // only the center ticket cluster moved into topBarContent. Find the RP pill on topBarPanel.
            if (topBarPanel != null)
            {
                var pill = topBarPanel.transform.Find("RewardPointsBackground");
                if (pill != null) pill.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// gps_standalone_shell §D5 — the chrome the PLAYLIFE shell does not have.
        ///
        /// <para>KEPT: the RP pill (points are a PLAYLIFE currency — check-ins and score uploads
        /// earn them), the username nameplate, and the Settings gear. HIDDEN: the whole centre
        /// ticket cluster (TicketIcon / TicketCountText / ShopPlusButton / the runtime
        /// TicketCountBackground pill), because gacha tickets and the shop they lead to are golf
        /// content that does not exist in this product. The bottom nav is hidden by
        /// <see cref="ShowBottomNav"/> itself rather than here — see the note there.</para>
        ///
        /// <para>Called AFTER <see cref="SetTopBarChromeVisible"/>(true), which re-shows every
        /// top-bar child, so like <see cref="ApplyDemoTopBarTrim"/> this must re-hide. Idempotent.
        /// Hides every child of <c>topBarContent</c> rather than the three named references,
        /// because the pill is created at runtime by <see cref="EnsureTicketPill"/> and a fourth
        /// element added to the cluster later would otherwise reappear alone. No-op in the game.</para>
        /// </summary>
        private void ApplyStandaloneChrome()
        {
            if (!GolfinRedux.UI.StandaloneGate.Enabled) return;

            if (topBarContent != null)
            {
                foreach (Transform child in topBarContent.transform)
                    child.gameObject.SetActive(false);
            }

            ShowBottomNav(false);
        }

        /// <summary>
        /// Account / auth screens (Login, Create Username, Sign Up, Email Confirmation):
        /// show ONLY the shared top banner + centered title. No bottom nav and no
        /// reward-points / shop / ticket / settings chrome — the user is not logged in yet.
        /// </summary>
        public void ShowAccountTitleBar(string title)
        {
            ShowTopBar(true);
            SetTopBarChromeVisible(false);
            if (usernameText != null)
            {
                usernameText.gameObject.SetActive(true);
                usernameText.text = title;
            }
            ShowBottomNav(false);
        }

        /// <summary>
        /// Toggle every Top Bar child EXCEPT the centered title (UsernameText).
        /// Strips the reward-points / shop / ticket / settings chrome for pre-login
        /// account screens, and restores it for normal menu screens via ShowBars().
        /// The banner background lives on topBarPanel itself, so it is unaffected.
        /// </summary>
        private void SetTopBarChromeVisible(bool visible)
        {
            // safe_area_top_bar: chrome is split — the RP cluster + Settings stay on topBarPanel (with the
            // UsernameText/nameplate, which must be skipped), and the center ticket cluster lives in the
            // nudged topBarContent. Toggle both groups so account screens strip ALL chrome but keep the title.
            if (topBarPanel != null)
            {
                foreach (Transform child in topBarPanel.transform)
                {
                    if (child.name == "UsernameText") continue;
                    child.gameObject.SetActive(visible);
                }
            }
            if (topBarContent != null)
            {
                foreach (Transform child in topBarContent.transform)
                    child.gameObject.SetActive(visible);
            }
        }

        /// <summary>
        /// Hide Top Bar and Bottom Nav (for Logo, Splash, Loading screens)
        /// </summary>
        public void HideBars()
        {
            ShowTopBar(false);
            ShowBottomNav(false);
        }

        /// <summary>
        /// Show only the Top Bar (with full chrome) but hide the Bottom Nav.
        /// Used by StartingCharacterSelection: SPEC decision 6 — top bar (RP + gear) visible,
        /// bottom nav replaced by the instruction block.
        /// </summary>
        public void ShowTopBarOnly()
        {
            ShowTopBar(true);
            SetTopBarChromeVisible(true);
            ShowBottomNav(false);
            ApplyDemoTopBarTrim();
            ApplyStandaloneChrome();
        }

        private void InitializeButtons()
        {
            // Settings button
            if (settingsButton != null)
            {
                settingsButton.onClick.AddListener(OnSettingsButtonClick);
            }

            // Top-bar ShopPlus — opens the Rewards Center on its STORE tab.
            // (Was a Stage-1 Debug.Log stub; gacha_screen §8 deferred "ticket purchasing via Shop+",
            //  but the button still has to take you to the shop rather than doing nothing.)
            if (shopPlusButton != null)
                shopPlusButton.onClick.AddListener(OnShopPlusButtonClick);

            // Bottom nav buttons
            if (homeButton != null)
                homeButton.onClick.AddListener(() => NavigateTo(Screen.Home));

            if (gachaButton != null)
                gachaButton.onClick.AddListener(() => NavigateTo(Screen.Gacha));

            if (mainPlayButton != null)
                mainPlayButton.onClick.AddListener(() => NavigateTo(Screen.MainPlay));

            if (inventoryButton != null)
                inventoryButton.onClick.AddListener(() => NavigateTo(Screen.Inventory));

            if (charactersButton != null)
                charactersButton.onClick.AddListener(() => NavigateTo(Screen.Characters));

            ApplyDemoNavTrim();
        }

        /// <summary>
        /// demo_build_slice §3.4: in a GOLFIN_DEMO build, hide every bottom-nav button
        /// whose target screen is blocked by DemoGate — a dead-end locked button reads
        /// as an unfinished build under guideline 2.1. No-op in the full game.
        /// Home stays (its target is allowlisted).
        /// </summary>
        private void ApplyDemoNavTrim()
        {
            if (!GolfinRedux.Demo.DemoGate.IsDemo) return;
            HideIfScreenBlocked(gachaButton,      GolfinRedux.UI.ScreenId.GeneralShop);
            HideIfScreenBlocked(mainPlayButton,   GolfinRedux.UI.ScreenId.ModeSelection);
            HideIfScreenBlocked(inventoryButton,  GolfinRedux.UI.ScreenId.Inventory);
            HideIfScreenBlocked(charactersButton, GolfinRedux.UI.ScreenId.Roster);
        }

        private static void HideIfScreenBlocked(Button button, GolfinRedux.UI.ScreenId target)
        {
            if (button != null && !GolfinRedux.Demo.DemoGate.IsScreenAllowed(target))
                button.gameObject.SetActive(false);
        }

        // ── gps_polish §D7 — GPS-originated RP deltas count up ────────────────

        /// <summary>
        /// How long an armed count-up stays armed. The GPS earn is a round trip
        /// (/points/earn, then a balance refresh), so the arm cannot be consumed on the same
        /// frame — but an arm that never fired must not sit waiting to animate the NEXT RP change,
        /// which would be a level-up or a shop refund and belongs to `game_polish`.
        /// </summary>
        private const float RpCountUpArmSeconds = 5f;

        private float _rpCountUpArmedUntil = -1f;
        private Coroutine _rpCountUp;

        /// <summary>
        /// gps_polish §D7 — make the NEXT upward RP change count up rather than snap.
        ///
        /// <para>The top bar is shared with the whole game, and the SPEC is explicit that only a
        /// delta a GPS action caused may animate: the game's own RP updates are `game_polish`.
        /// So this is a one-shot ARM, set by the GPS call site immediately before it spends or
        /// earns, consumed by the first <see cref="SetRewardPoints"/> that follows and expiring on
        /// its own if none does.</para>
        /// </summary>
        public void ArmRewardPointsCountUp()
        {
            _rpCountUpArmedUntil = Time.unscaledTime + RpCountUpArmSeconds;
        }

        /// <summary>
        /// Top-bar counter format: invariant "N0" grouping with a "." thousands separator, so
        /// 9000 reads "9.000". Both counters share it — the RP pill used the invariant comma
        /// while the ticket pill printed a bare int, which is two different numbers on one bar.
        /// </summary>
        private static readonly System.Globalization.NumberFormatInfo TopBarNumberFormat = BuildTopBarNumberFormat();

        private static System.Globalization.NumberFormatInfo BuildTopBarNumberFormat()
        {
            var nfi = (System.Globalization.NumberFormatInfo)
                      System.Globalization.CultureInfo.InvariantCulture.NumberFormat.Clone();
            nfi.NumberGroupSeparator = ".";
            return nfi;
        }

        /// <summary>
        /// Read back a counter this class itself rendered. Digits only — "1.240" is NOT parseable
        /// as an int by any culture-aware parse (the group separator reads as a decimal point and
        /// int.TryParse rejects the fraction), and the count-up needs the previous value.
        /// </summary>
        private static bool TryParseTopBarNumber(string? text, out int value)
        {
            value = 0;
            if (string.IsNullOrEmpty(text)) return false;
            long acc = 0;
            bool any = false;
            foreach (char c in text!)
            {
                if (c >= '0' && c <= '9') { acc = acc * 10 + (c - '0'); any = true; if (acc > int.MaxValue) return false; }
                else if (c != '.' && c != ',' && c != ' ') return false;
            }
            if (!any) return false;
            value = (int)acc;
            return true;
        }

        public void SetRewardPoints(int points)
        {
            if (rewardPointsText == null) return;

            bool armed = Time.unscaledTime <= _rpCountUpArmedUntil;
            if (armed &&
                TryParseTopBarNumber(rewardPointsText.text, out int from) &&
                points > from)
            {
                _rpCountUpArmedUntil = -1f;
                Golfin.UI.Polish.UiMotion.Run(this, ref _rpCountUp,
                    Golfin.UI.Polish.UiMotion.CountUp(rewardPointsText, from, points,
                                                     culture: TopBarNumberFormat));
                return;
            }

            rewardPointsText.text = points.ToString("N0", TopBarNumberFormat);
        }

        /// <summary>
        /// Update the top-bar ticket counter. Subscribed to GachaTicketManager.OnTicketsChanged.
        /// </summary>
        public void SetTickets(int count)
        {
            if (ticketCountText != null)
                ticketCountText.text = count.ToString("N0", TopBarNumberFormat);
        }

        /// <summary>
        /// Adapter: receives the new per-kind (TicketType, int) event and forwards
        /// the Standard balance to SetTickets (top bar shows Standard only).
        /// </summary>
        private void OnTicketCountChanged(TicketType kind, int newBalance)
        {
            if (kind == TicketType.Standard)
                SetTickets(newBalance);
        }

        /// <summary>
        /// Sets the visible center-text WITHOUT touching the cached _username.
        /// This is a transient display override (e.g. screen titles).
        /// To update the persisted username use UpdateUsername instead.
        /// </summary>
        public void SetUsername(string username)
        {
            // NOTE: deliberately does NOT write _username.
            // _username is authoritative and is only written by Awake() and UpdateUsername().
            // ModeSelectScreenController previously poked SetUsername("MODE SELECTION") as a
            // transient title; that was removed in iter-10 (Option A). SetUsername now only
            // updates the live text. If any future caller truly needs to change the real
            // username they must call UpdateUsername instead.
            if (usernameText != null)
            {
                usernameText.text = username;
            }
        }

        /// <summary>
        /// Update the username display (alias for SetUsername for Phase 2 compatibility)
        /// </summary>
        public void UpdateUsername(string newUsername)
        {
            _username = newUsername;
            if (usernameText != null)
            {
                usernameText.text = newUsername;
            }

            Debug.Log($"[PersistentUI] Username updated: {newUsername}");
        }

        private void OnShopPlusButtonClick()
        {
            var sm = GolfinRedux.UI.ScreenManager.Instance;
            if (sm == null)
            {
                Debug.LogWarning("[PersistentUI] ScreenManager.Instance is null — cannot open the shop.");
                return;
            }

            // The "+" sits next to the ticket counter, so it opens the Rewards Center STORE tab
            // rather than the GACHA tab the bottom-nav slot defaults to. It is a jump to the
            // Gacha pillar with the tab forced, not a forward push (nav_back_memory §4), so it
            // lands on the Rewards Center root even from GachaHistory / GachaPrizes.
            GolfinRedux.UI.Gacha.GachaTabController.RequestStoreTab();
            sm.NavigateToPillar(Screen.Gacha);
        }

        private void OnSettingsButtonClick()
        {
            if (SettingsController.Instance != null)
            {
                SettingsController.Instance.OpenSettings();
            }
            else
            {
                Debug.LogWarning("[PersistentUI] No SettingsController instance found - cannot open settings");
            }
        }

        /// <summary>
        /// Bottom-nav slot tap. Routing lives in ScreenManager.NavigateToPillar (nav_back_memory
        /// §4 / D1): the pillar you are already in reopens at its root, a different pillar reopens
        /// at the screen you were last on inside it.
        /// </summary>
        public void NavigateTo(Screen screen)
        {
            currentScreen = screen;
            UpdateScreenHighlight();

            var sm = GolfinRedux.UI.ScreenManager.Instance;
            if (sm == null)
            {
                Debug.LogWarning("[PersistentUI] ScreenManager.Instance is null — cannot navigate.");
                return;
            }

            if (screen == Screen.Settings)
            {
                Debug.LogWarning($"[PersistentUI] Navigation to {screen} not yet implemented.");
                return;
            }

            // HighlightScreen re-runs from ApplyScreen, so the slot lights from the real
            // destination rather than the optimistic value set above.
            sm.NavigateToPillar(screen);
        }

        /// <summary>
        /// Called by ScreenManager whenever the active shell screen changes,
        /// so the bottom-nav highlight tracks navigation that bypasses the nav buttons
        /// (e.g. initial load, programmatic transitions).
        /// Also drives the top-bar center text per screen:
        ///   Home        → real username (e.g. "CHOTO")
        ///   Leaderboard → "LEADERBOARD"
        ///   all others  → "" (blank center)
        /// </summary>

        /// <summary>
        /// Set the top-bar centre title for a screen. Split out of HighlightScreen so a language
        /// change can re-resolve it without re-running the nav-highlight pass.
        /// </summary>
        private void ApplyTopBarCenterText(GolfinRedux.UI.ScreenId screenId)
        {
            if (usernameText == null) return;

            // game_polish_a §D3: this is the AUTHORITATIVE paint, and it is also the recovery path.
            // A push that was interrupted mid-dissolve would otherwise leave the label parked at a
            // fractional alpha forever, so the routine is stopped and the group forced back to 1
            // here rather than trusted to have finished.
            if (_centerTextRoutine != null) { StopCoroutine(_centerTextRoutine); _centerTextRoutine = null; }
            if (_centerTextGroup != null) _centerTextGroup.alpha = 1f;

            usernameText.text = CenterTextFor(screenId);
        }

        /// <summary>
        /// The centre title a screen should show — the ONE resolver, shared by the instant paint
        /// (<see cref="ApplyTopBarCenterText"/>) and the dissolve (<see cref="CrossFadeCenterTextTo"/>)
        /// so the two can never disagree about what the title is.
        /// </summary>
        private string CenterTextFor(GolfinRedux.UI.ScreenId screenId)
        {
            if (screenId == GolfinRedux.UI.ScreenId.Home)
            {
                // Prefer the signed-in player's real name over whatever was cached.
                // Awake() seeds _username from the designer placeholder ("CHOTO") and the
                // real value only arrives via AccountUiBridge.SyncUsername(); if that push
                // happened before this manager existed — or never fired, as on a boot that
                // restores a session without re-routing through login — the top bar kept
                // showing the placeholder. Re-reading here makes Home self-correcting.
                if (Golfin.Auth.PlayerIdentity.HasName)
                    _username = Golfin.Auth.PlayerIdentity.DisplayName;
                return _username;
            }

            string key = NavTitleKeyFor(screenId);
            return key != null ? LocalizationManager.Get(key) : string.Empty;
        }

        // ── game_polish_a §D3 — the centre title dissolves; it does not snap ──────────────
        private Coroutine? _centerTextRoutine;
        private CanvasGroup? _centerTextGroup;

        /// <summary>
        /// Dissolve the top-bar centre title over to <paramref name="screenId"/>'s title.
        ///
        /// <para>WHY THIS EXISTS. The centre title is SHARED chrome — one label on the persistent
        /// top bar — so it cannot travel with a pushing screen the way that screen's own content
        /// does. It was therefore repainted by <c>ApplyScreen</c>, which <see cref="LayeredPush"/>
        /// defers to the end of the push on purpose; the visible result was that the whole 0.25 s
        /// push played with the LEAVER's name over the ARRIVER's content and then the text
        /// hard-cut in a single frame. Under the old fade-to-black that repaint happened behind
        /// the black and nobody could see it — the push is what exposed it.</para>
        ///
        /// <para>Called at push START, so the dissolve (<c>FadeDur</c> = 0.15 s) has settled the new
        /// name well before the content finishes travelling (<c>PushDur</c> = 0.25 s) — the title
        /// leads the arrival instead of trailing it.</para>
        ///
        /// <para>No motion when the text does not change: Rewards Center → Gacha History keeps one
        /// title, and dissolving a label to the same string would be a flicker with no meaning.</para>
        /// </summary>
        public void CrossFadeCenterTextTo(GolfinRedux.UI.ScreenId screenId)
        {
            if (usernameText == null) return;
            if (!UiMotion.Enabled) return;   // motion off ⇒ ApplyScreen's instant repaint stands

            string next = CenterTextFor(screenId);
            if (next == usernameText.text) return;

            UiMotion.Run(this, ref _centerTextRoutine, DissolveCenterText(next));
        }

        private IEnumerator DissolveCenterText(string next)
        {
            CanvasGroup g = EnsureCenterTextGroup();
            float half = UiMotion.FadeDur * 0.5f;
            yield return UiMotion.Fade(g, g.alpha, 0f, half);
            if (usernameText != null) usernameText.text = next;
            yield return UiMotion.Fade(g, 0f, 1f, half);
            _centerTextRoutine = null;
        }

        /// <summary>
        /// The CanvasGroup the dissolve drives, added AT RUNTIME rather than authored.
        /// Authoring one would be a scene edit on the persistent top bar, and A2's parity gate
        /// measures the shell's rest pixels — a runtime group that rests at alpha 1 renders
        /// identically to no group at all. Left at the default <c>blocksRaycasts = true</c>,
        /// which is exactly how the label behaved before it had a group.
        /// </summary>
        private CanvasGroup EnsureCenterTextGroup()
        {
            if (_centerTextGroup == null)
            {
                // `== null`, never `??`: a Unity component compares equal to null through an
                // overloaded operator that `??` does not consult, so `??` would hand back a
                // fake-null component instead of adding a real one (CLAUDE.md, Basic Rules 4).
                var existing = usernameText!.GetComponent<CanvasGroup>();
                _centerTextGroup = existing == null
                    ? usernameText!.gameObject.AddComponent<CanvasGroup>()
                    : existing;
            }
            return _centerTextGroup;
        }

        /// <summary>
        /// The localization key backing a screen's top-bar centre title, or null when the screen
        /// has no localized title. Home (player username) and StaminaShopDetail (set dynamically
        /// by StaminaShopDetailScreenController via SetUsername(shopName) after navigating)
        /// deliberately return null so a language refresh never clobbers their non-localized text.
        /// </summary>
        private static string NavTitleKeyFor(GolfinRedux.UI.ScreenId screenId)
        {
            switch (screenId)
            {
                case GolfinRedux.UI.ScreenId.Roster:                   return "NAV_ROSTER";
                case GolfinRedux.UI.ScreenId.Inventory:                return "NAV_INVENTORY";
                case GolfinRedux.UI.ScreenId.Leaderboard:              return "NAV_LEADERBOARD";
                case GolfinRedux.UI.ScreenId.ModeSelection:            return "NAV_MODE_SELECTION";
                case GolfinRedux.UI.ScreenId.MissionSelection:         return "MISSIONS_TITLE";
                case GolfinRedux.UI.ScreenId.TournamentHoleSelection:  return "NAV_SELECT_HOLE";
                case GolfinRedux.UI.ScreenId.TournamentLeaderboard:    return "NAV_TOURNAMENT_LEADERBOARD";
                case GolfinRedux.UI.ScreenId.TournamentSelection:      return "NAV_TOURNAMENTS";
                case GolfinRedux.UI.ScreenId.StaminaShopSelection:     return "NAV_BOOST_STAMINA";
                case GolfinRedux.UI.ScreenId.GeneralShop:              return "NAV_REWARDS_CENTER";
                // Gacha pillar sub-screens: the history node keeps the Rewards Center title
                // (4079:18306), the prizes node overrides it with "PRIZES" (13622:2222).
                case GolfinRedux.UI.ScreenId.GachaHistory:             return "NAV_REWARDS_CENTER";
                case GolfinRedux.UI.ScreenId.GachaPrizes:              return "GACHA_PRIZES_TITLE";
                // gps_hub_entry §4 — the GPS / PLAYLIFE hub. It has no bottom-nav pillar, so
                // HighlightScreen returns right after ApplyTopBarCenterText; this case is the
                // whole reason the shared top bar can carry the hub's title without a new API.
                case GolfinRedux.UI.ScreenId.GpsHub:                   return "GPS_HUB_TITLE";
                // score_upload_flow — the Posted step (6/6) overrides this to SCORE_POSTED_TITLE
                // through the existing transient SetUsername path, and restores it on the way out.
                case GolfinRedux.UI.ScreenId.ScoreUpload:              return "SCORE_UPLOAD_TITLE";
                // gps_profile_pack
                case GolfinRedux.UI.ScreenId.GpsProfile:               return "GPS_PROFILE_TITLE";
                case GolfinRedux.UI.ScreenId.GpsAvatar:                return "GPS_AVATAR_TITLE";
                case GolfinRedux.UI.ScreenId.GpsBadges:                return "GPS_BADGES_TITLE";
                // auth_golf_profile — post-signup capture + welcome tutorial
                case GolfinRedux.UI.ScreenId.GpsGolfProfile:           return "GPS_GOLFPROF_TITLE";
                case GolfinRedux.UI.ScreenId.GpsWelcome:               return "GPS_WELCOME_TITLE";
                // gps_gifts_votes
                case GolfinRedux.UI.ScreenId.GpsGift:                  return "GPS_GIFT_TITLE";
                case GolfinRedux.UI.ScreenId.GpsVote:                  return "GPS_VOTE_TITLE";
                // gps_checkin — the Rounds tab
                case GolfinRedux.UI.ScreenId.GpsRounds:                return "GPS_ROUNDS_TITLE";
                default:                                               return null;
            }
        }

        public void HighlightScreen(GolfinRedux.UI.ScreenId screenId)
        {
            _lastHighlightedScreen = screenId;

            // ── Drive top-bar center text BEFORE the nav-highlight switch ────────
            // (The switch has a default:return for Leaderboard; text must be set first.)
            ApplyTopBarCenterText(screenId);

            // ── Bottom-nav icon highlight ─────────────────────────────────────────
            // nav_back_memory §1 — the ScreenId→pillar mapping lives in ScreenManager.PillarOf
            // (one mapping, shared with the history stack), not in a second switch here.
            var pillar = GolfinRedux.UI.ScreenManager.PillarOf(screenId);
            if (!pillar.HasValue)
                return; // Logo/Splash/Loading/Leaderboard/auth/starter: bars hidden or no nav highlight.

            currentScreen = pillar.Value;
            UpdateScreenHighlight();
        }

        /// <summary>
        /// True until the first highlight has been painted. The very first paint after boot is
        /// NOT animated — a cold screen should not cross-fade its chrome into place before the
        /// player has touched anything (§D7.2, "animate:false on the first paint").
        /// </summary>
        private bool _firstHighlight = true;

        /// <summary>
        /// game_polish_a §D7 — the selected slot, said with LIGHT rather than with a tint.
        ///
        /// <para>Every slot's Image.color is now <see cref="iconNormalColor"/> unconditionally;
        /// what marks the selection is <see cref="Golfin.UI.Polish.NavSlotHighlight"/>'s gold halo
        /// and brighter ring, cross-faded in over <c>UiMotion.FadeDur</c> with one pulse on the
        /// slot that just became current. The SAME component and the SAME Attach() call drive the
        /// GPS bar from <c>GpsNavBarHighlight</c>, so the two bars cannot drift.</para>
        /// </summary>
        private void UpdateScreenHighlight()
        {
            bool animate = !_firstHighlight;
            _firstHighlight = false;

            Paint(homeIcon,       Screen.Home,       animate);
            Paint(gachaIcon,      Screen.Gacha,      animate);
            Paint(mainPlayIcon,   Screen.MainPlay,   animate);
            Paint(inventoryIcon,  Screen.Inventory,  animate);
            Paint(charactersIcon, Screen.Characters, animate);
        }

        private void Paint(Image icon, Screen slot, bool animate)
        {
            if (icon == null) return;
            // The glyph is white on every screen now — never tinted, selected or not.
            icon.color = iconNormalColor;
            Golfin.UI.Polish.NavSlotHighlight.Attach(icon)?.SetSelected(currentScreen == slot, animate);
        }

        public void ShowTopBar(bool show)
        {
            // safe_area_top_bar: background + nameplate live on topBarPanel; the notch-clearing chrome
            // lives in topBarContent (under a capped SafeAreaFitter sibling). Toggle both.
            if (topBarPanel != null)
                topBarPanel.SetActive(show);
            if (topBarContent != null)
                topBarContent.SetActive(show);
        }

        public void ShowBottomNav(bool show)
        {
            // gps_standalone_shell §D5 — the game's bottom nav does not exist in the PLAYLIFE
            // shell: four of its five slots (Gacha, Play, Inventory, Characters) open screens
            // StandaloneGate refuses, and the GPS screens draw their OWN nav bar inside their
            // prefabs. Forced off HERE rather than at each call site because "show the bars" is
            // said from a dozen places (ShowBars, the gameplay loader, screen controllers), and
            // one that forgot would put a dead golf nav bar under a PLAYLIFE screen.
            if (show && GolfinRedux.UI.StandaloneGate.Enabled) show = false;

            if (bottomNavPanel != null)
                bottomNavPanel.SetActive(show);
        }

        /// <summary>
        /// Stage C0: explicit bottom-nav visibility toggle.
        /// Alias for ShowBottomNav(visible) — exposed so GameplaySceneLoader
        /// has a self-documenting call site for the gameplay-transition flow.
        /// </summary>
        public void SetBottomNavVisible(bool visible) => ShowBottomNav(visible);
    }
}
