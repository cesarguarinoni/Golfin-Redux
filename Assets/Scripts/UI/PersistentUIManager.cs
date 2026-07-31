#nullable enable
using UnityEngine;
using UnityEngine.UI;
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

        [Header("Top Bar References")]
        public GameObject topBarPanel;
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
        public Color iconActiveColor = Color.cyan;

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
        }

        /// <summary>
        /// Ensure an RP-style pill sits behind the top-bar ticket counter. Built at runtime because
        /// the top bar is a plain scene object whose file reserializes wholesale on save — so we
        /// avoid a massive scene diff. Idempotent; mirrors the RewardPointsBackground pill (#122C47).
        /// </summary>
        private void EnsureTicketPill()
        {
            if (ticketCountText == null) return;
            var topbar = ticketCountText.transform.parent;
            if (topbar == null || topbar.Find("TicketCountBackground") != null) return;

            var rp   = topbar.Find("RewardPointsBackground");
            var icon = topbar.Find("TicketIcon");
            if (rp == null || icon == null) return;

            var pill = Instantiate(rp.gameObject, topbar);
            pill.name = "TicketCountBackground";
            var prt = pill.GetComponent<RectTransform>();
            prt.anchorMin = new Vector2(0f, 0.5f);
            prt.anchorMax = new Vector2(0f, 0.5f);
            prt.pivot     = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(138f, 54f);
            prt.anchoredPosition = new Vector2(575f, 0f);   // left end tucks under the ticket, right ~8px from Shop+

            // Render order: pill behind, then ticket icon + count + shop draw on top of it.
            pill.transform.SetAsLastSibling();
            icon.SetAsLastSibling();
            ticketCountText.transform.SetAsLastSibling();
            var shop = topbar.Find("ShopPlusButton");
            if (shop != null) shop.SetAsLastSibling();
        }

        private void OnDisable()
        {
            if (RewardPointsManager.Instance != null)
                RewardPointsManager.Instance.OnPointsChanged -= SetRewardPoints;

            // GachaTicketManager unsubscribe.
            if (GachaTicketManager.Instance != null)
                GachaTicketManager.Instance.OnTicketsChanged -= OnTicketCountChanged;
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
            if (topBarPanel != null)
            {
                var pill = topBarPanel.transform.Find("RewardPointsBackground");
                if (pill != null) pill.gameObject.SetActive(false);
            }
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
            if (topBarPanel == null) return;
            foreach (Transform child in topBarPanel.transform)
            {
                if (child.name == "UsernameText") continue;
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

        private void InitializeButtons()
        {
            // Settings button
            if (settingsButton != null)
            {
                settingsButton.onClick.AddListener(OnSettingsButtonClick);
            }

            // Top-bar ShopPlus stub (Stage 1 — no action yet; Stage 2+ will open gacha top-up flow)
            if (shopPlusButton != null)
                shopPlusButton.onClick.AddListener(() => Debug.Log("[PersistentUI] ShopPlus tapped — stub (Stage 1)"));

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

        public void SetRewardPoints(int points)
        {
            if (rewardPointsText != null)
            {
                rewardPointsText.text = points.ToString("N0");
            }
        }

        /// <summary>
        /// Update the top-bar ticket counter. Subscribed to GachaTicketManager.OnTicketsChanged.
        /// </summary>
        public void SetTickets(int count)
        {
            if (ticketCountText != null)
                ticketCountText.text = count.ToString();
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

            switch (screen)
            {
                case Screen.Home:
                    sm.ShowScreen(GolfinRedux.UI.ScreenId.Home);
                    break;
                case Screen.Inventory:
                    sm.ShowScreen(GolfinRedux.UI.ScreenId.Inventory);
                    break;
                case Screen.Characters:
                    sm.ShowScreen(GolfinRedux.UI.ScreenId.Roster);
                    break;
                case Screen.MainPlay:
                    // Bottom-nav tee button → Mode Select screen (mode_select_system spec)
                    sm.ShowScreen(GolfinRedux.UI.ScreenId.ModeSelection);
                    break;
                case Screen.Gacha:
                    // Order 610 — the (previously no-op) Gacha nav button opens the Rewards Center
                    // hub (GACHA | STORE | GIFTS tabs; STORE live). Forward-compatible with the future
                    // gacha pillar via the hub's GACHA tab. Nav-slot choice — see general_shop_ui fork #6.
                    sm.ShowScreen(GolfinRedux.UI.ScreenId.GeneralShop);
                    break;
                default:
                    Debug.LogWarning($"[PersistentUI] Navigation to {screen} not yet implemented.");
                    break;
            }
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
        public void HighlightScreen(GolfinRedux.UI.ScreenId screenId)
        {
            // ── Drive top-bar center text BEFORE the nav-highlight switch ────────
            // (The switch has a default:return for Leaderboard; text must be set first.)
            if (usernameText != null)
            {
                switch (screenId)
                {
                    case GolfinRedux.UI.ScreenId.Home:
                        usernameText.text = _username;
                        break;
                    case GolfinRedux.UI.ScreenId.Leaderboard:
                        usernameText.text = LocalizationManager.Get("NAV_LEADERBOARD");
                        break;
                    case GolfinRedux.UI.ScreenId.ModeSelection:
                        usernameText.text = LocalizationManager.Get("NAV_MODE_SELECTION");
                        break;
                    case GolfinRedux.UI.ScreenId.TournamentHoleSelection:
                        usernameText.text = LocalizationManager.Get("NAV_SELECT_HOLE");
                        break;
                    case GolfinRedux.UI.ScreenId.TournamentLeaderboard:
                        usernameText.text = LocalizationManager.Get("NAV_TOURNAMENT_LEADERBOARD");
                        break;
                    case GolfinRedux.UI.ScreenId.TournamentSelection:
                        usernameText.text = LocalizationManager.Get("NAV_TOURNAMENTS");
                        break;
                    case GolfinRedux.UI.ScreenId.StaminaShopSelection:
                        usernameText.text = LocalizationManager.Get("NAV_BOOST_STAMINA");
                        break;
                    case GolfinRedux.UI.ScreenId.GeneralShop:
                        usernameText.text = LocalizationManager.Get("NAV_REWARDS_CENTER");
                        break;
                    // StaminaShopDetail center text is set dynamically by StaminaShopDetailScreenController
                    // via SetUsername(shopName) after navigating.
                    default:
                        usernameText.text = string.Empty;
                        break;
                }
            }

            // ── Bottom-nav icon highlight ─────────────────────────────────────────
            switch (screenId)
            {
                case GolfinRedux.UI.ScreenId.Home:          currentScreen = Screen.Home; break;
                case GolfinRedux.UI.ScreenId.Roster:        currentScreen = Screen.Characters; break;
                case GolfinRedux.UI.ScreenId.Inventory:     currentScreen = Screen.Inventory; break;
                case GolfinRedux.UI.ScreenId.HoleSelection:  currentScreen = Screen.MainPlay; break;
                case GolfinRedux.UI.ScreenId.ModeSelection:  currentScreen = Screen.MainPlay; break;
                case GolfinRedux.UI.ScreenId.TournamentHoleSelection: currentScreen = Screen.MainPlay; break;
                case GolfinRedux.UI.ScreenId.TournamentLeaderboard:   currentScreen = Screen.MainPlay; break;
                case GolfinRedux.UI.ScreenId.TournamentSelection:     currentScreen = Screen.MainPlay; break;
                // Order 517 — Shop screens entered from Roster; keep Characters nav tab highlighted
                case GolfinRedux.UI.ScreenId.StaminaShopSelection:  currentScreen = Screen.Characters; break;
                case GolfinRedux.UI.ScreenId.StaminaShopDetail:     currentScreen = Screen.Characters; break;
                // Order 610 — Rewards Center opened from the Gacha nav slot
                case GolfinRedux.UI.ScreenId.GeneralShop:           currentScreen = Screen.Gacha; break;
                default:
                    return; // Logo/Splash/Loading/Leaderboard: bars hidden or no nav highlight.
            }
            UpdateScreenHighlight();
        }

        private void UpdateScreenHighlight()
        {
            if (homeIcon != null)       homeIcon.color       = (currentScreen == Screen.Home)       ? iconActiveColor : iconNormalColor;
            if (gachaIcon != null)      gachaIcon.color      = (currentScreen == Screen.Gacha)      ? iconActiveColor : iconNormalColor;
            if (mainPlayIcon != null)   mainPlayIcon.color   = (currentScreen == Screen.MainPlay)   ? iconActiveColor : iconNormalColor;
            if (inventoryIcon != null)  inventoryIcon.color  = (currentScreen == Screen.Inventory)  ? iconActiveColor : iconNormalColor;
            if (charactersIcon != null) charactersIcon.color = (currentScreen == Screen.Characters) ? iconActiveColor : iconNormalColor;
        }

        public void ShowTopBar(bool show)
        {
            if (topBarPanel != null)
                topBarPanel.SetActive(show);
        }

        public void ShowBottomNav(bool show)
        {
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
