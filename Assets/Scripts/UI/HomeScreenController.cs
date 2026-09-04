using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Golfin.UI;
using Golfin.Roster;
using Golfin.UI.Matchmaking;
using Golfin.UI.Common;
using Golfin.Gps.UI;

namespace GolfinRedux.UI
{
    /// <summary>
    /// Home screen controller: top bar, news/announcement, character,
    /// GPS promo banner, and next hole panel. Bottom navigation is
    /// owned by PersistentUIManager.
    /// Screen switching is handled by ScreenManager on ShellSceneRoot.
    /// </summary>
    public class HomeScreenController : MonoBehaviour
    {
        [Header("Screen Manager")]
        // K13: no longer read from code — its only remaining caller was the PLAY fallback that
        // navigated to the fake boot Loading screen. Kept as a scene-wired reference so the
        // ShellScene serialization is untouched and any future navigation from Home has it ready.
        [SerializeField] private ScreenManager screenManager;

        // -------- Top Bar --------
        [Header("Top Bar")]
        [SerializeField] private TextMeshProUGUI rewardPointsText;
        [SerializeField] private TextMeshProUGUI usernameText;
        [SerializeField] private Button settingsButton;

        // -------- News / Announcement --------
        [Header("News Panel")]
        [SerializeField] private TextMeshProUGUI newsTitleText;
        [SerializeField] private TextMeshProUGUI newsBodyText;
        [SerializeField] private Transform dotsContainer;

        /// <summary>
        /// The notice panel's own root — the object owning <see cref="newsTitleText"/> and
        /// <see cref="newsBodyText"/>. Hidden outright when no notice is live, because there is no
        /// bundled copy to fall back to (home_notices SPEC §4.3): a cold offline launch with no
        /// cache must show NOTHING, not a maintenance date that has already passed.
        /// <para>
        /// The page dots are a SIBLING of this root in ShellScene, not a child, so they are hidden
        /// separately by <see cref="UpdateNewsDots"/> — a page count of 0 takes the <c>n &lt;= 1</c>
        /// branch there and switches the whole container off.
        /// </para>
        /// <para>
        /// Leaving this unassigned must not crash and must not hide anything: every use is
        /// null-checked and falls back to leaving the panel visible, which is the pre-change
        /// behaviour.
        /// </para>
        /// </summary>
        [SerializeField] private GameObject newsPanelRoot;

        /// <summary>
        /// <b>Ignored for the live page count</b> (home_notices SPEC §4.2). Kept only so ShellScene's
        /// serialization is untouched. It is not applied as a cap either: the endpoint already caps
        /// at 5 rows, so capping again here would silently swallow a notice an operator published.
        /// The count comes from <see cref="NewsPageCount"/>.
        /// </summary>
        /// <summary>
        /// The daily-mission pill (daily_mission_home_pill §2). Home owns the reference for one
        /// reason only: the pill's Y is COMPUTED from <see cref="newsPanelRoot"/>, and Home is the
        /// only object that knows when that panel's visibility changed. Everything else the pill
        /// does — fetching, animating, its own tap — it owns itself.
        /// <para>Unassigned is not an error: no pill, and Home behaves exactly as before.</para>
        /// </summary>
        [SerializeField] private Golfin.UI.Home.DailyMissionPillController dailyMissionPill;

        [SerializeField] private int totalNewsPages = 3;
        [SerializeField] private float newsAutoCycleInterval = 5f; // seconds

        private int _currentNewsIndex;
        private float _newsTimer;
        private bool _autoCycleNews = true;

        /// <summary>
        /// Pooled + windowed notice dots. Replaces the old "colour the scene's three authored dot
        /// children" loop, which could not represent more notices than the scene happened to hold —
        /// a 4th or 5th notice cycled with no dot for it and only logged a shortfall warning. The
        /// strip adopts Dot1/Dot2/Dot3 as its pool and creates more on demand, up to its own cap.
        /// </summary>
        private PaginationDotStrip _newsDots;

        // -------- Promo Banner (GPS) --------
        [Header("Promo Banner (GPS)")]
        [SerializeField] private Button promoBannerButton;
        [SerializeField] private TextMeshProUGUI promoBannerText;
        [SerializeField] private Image gpsIcon;

        /// <summary>
        /// The strip's own Image. Assigned so the slot is inspectable from here; the sprite itself
        /// is swapped by the <c>BannerSlotBinder</c> on the same GameObject, not by this controller.
        /// <para>
        /// <c>promoBannerText</c> and <c>gpsIcon</c> above stay UNASSIGNED on purpose: the banner
        /// content model is image-only, with all copy baked into the artwork.
        /// </para>
        /// </summary>
        [SerializeField] private Image promoBannerImage;

        // -------- Character --------
        [Header("Character")]
        [SerializeField] private Image characterImage;

        // -------- Next Hole Panel --------
        [Header("Next Hole Panel")]
        [SerializeField] private TextMeshProUGUI nextHoleTitleText;   // "NEXT HOLE"
        [SerializeField] private TextMeshProUGUI courseNameText;

        [SerializeField] private GameObject rewardRow1;
        [SerializeField] private Image reward1Icon;
        [SerializeField] private TextMeshProUGUI reward1Amount;

        [SerializeField] private GameObject rewardRow2;
        [SerializeField] private Image reward2Icon;
        [SerializeField] private TextMeshProUGUI reward2Amount;

        [SerializeField] private GameObject rewardRow3;
        [SerializeField] private Image reward3Icon;
        [SerializeField] private TextMeshProUGUI reward3Amount;

        [Header("Reward Icons")]
        [SerializeField] private Sprite pointsIcon;
        [SerializeField] private Sprite repairKitIcon;
        [SerializeField] private Sprite ballIcon;

        [SerializeField] private Button playButton;

        // -------- Optional: Hole Database --------
        [Header("Optional: Hole Database")]
        [SerializeField] private HoleDatabase holeDatabase;
        [SerializeField] private int currentHoleIndex = 0;

        [Header("Matchmaking")]
        [SerializeField] private MatchmakingModalController matchmakingModal;

        // ── Leaderboard entry ─────────────────────────────────────────────────
        [Header("Leaderboard")]
        [SerializeField] private Button _leaderboardButton;

        // ── GPS entry (gps_pill_entry) ────────────────────────────────────────
        // The pill IS the GPS door, and the thing GpsGate turns on and off. It replaced the
        // cross-promotion banner in that role: the banner is now an ordinary banner again and
        // shows whatever the admin publishes, in both variants.
        [Header("GPS")]
        [SerializeField] private Button gpsPillButton;

        private void Awake()
        {
            // Top bar
            if (settingsButton != null)
                settingsButton.onClick.AddListener(OnSettingsClicked);

            // Promo banner
            if (promoBannerButton != null)
                promoBannerButton.onClick.AddListener(OnPromoBannerClicked);

            // Next hole
            if (playButton != null)
                playButton.onClick.AddListener(OnPlayClicked);

            // Leaderboard header icon
            if (_leaderboardButton != null)
                _leaderboardButton.onClick.AddListener(OnLeaderboardClicked);

            // demo_build_slice §3.4: hide the Leaderboard entry in a GOLFIN_DEMO build
            // (Leaderboard is not on the DemoGate allowlist). No-op in the full game.
            if (_leaderboardButton != null && !GolfinRedux.Demo.DemoGate.IsScreenAllowed(ScreenId.Leaderboard))
                _leaderboardButton.gameObject.SetActive(false);

            // gps_pill_entry: the GPS pill, same shape as the Leaderboard gate above. Hidden
            // outright in a "punch it" build (no GOLFIN_GPS) — not shown-and-dead, which is the
            // failure mode this whole variant split exists to avoid. Always on in the Editor and
            // in "punch it GPS" builds.
            if (gpsPillButton != null)
            {
                gpsPillButton.onClick.AddListener(OnGpsPillClicked);
                gpsPillButton.gameObject.SetActive(Golfin.Gps.UI.GpsGate.IsScreenAllowed(ScreenId.GpsHub));
            }
        }

        /// <summary>The pill's tap — the only GPS entry point on Home now.</summary>
        private void OnGpsPillClicked()
        {
            GolfinRedux.UI.ScreenManager.Instance?.ShowScreen(GolfinRedux.UI.ScreenId.GpsHub);
        }

        private void OnLeaderboardClicked()
        {
            var ctrl = FindObjectOfType<Golfin.UI.Rankings.RankingsScreenController>();
            if (ctrl != null)
                ctrl.OpenFrom(GolfinRedux.UI.ScreenId.Home);
            else
                GolfinRedux.UI.ScreenManager.Instance?.ShowScreen(GolfinRedux.UI.ScreenId.Leaderboard);
        }

        private void OnEnable()
        {
            // Initial UI state when Home screen becomes active
            
            // Show PersistentUI (Top Bar + Bottom Nav)
            if (PersistentUIManager.Instance != null)
            {
                PersistentUIManager.Instance.ShowBars();
            }

            // Top bar: placeholder values for now
            if (rewardPointsText != null)
            {
                // demo_build_slice §3.4: hide Home's RP label when points are disabled in the demo.
                if (GolfinRedux.Demo.DemoGate.IsDemo && !GolfinRedux.Demo.DemoConfig.Instance.PointsEnabled)
                    rewardPointsText.gameObject.SetActive(false);
                else
                    rewardPointsText.text = "0";    // TODO: load real value
            }

            if (usernameText != null)
                usernameText.text = Golfin.Auth.PlayerIdentity.DisplayNameOr("Player");

            // Subscribe to selection changes so the image updates immediately
            if (CharacterManager.Instance != null)
                CharacterManager.Instance.OnCharacterSelected += OnCharacterSelectionChanged;

            // Character
            UpdateHomeCharacterImage();

            // News. A fetch that lands later repaints through OnNoticesChanged; a language switch
            // from the Settings overlay repaints through OnLanguageChanged, because that overlay
            // leaves Home enabled and so never re-runs this method.
            Golfin.Notices.NoticeService.OnNoticesChanged += OnNoticesChanged;
            LocalizationManager.OnLanguageChanged += OnNoticeLanguageChanged;

            // Screen-entry refresh, throttled to at most one request per minute by the service.
            Golfin.Notices.NoticeService.Instance?.Refresh();

            _currentNewsIndex = 0;
            _newsTimer = 0f;
            RefreshNewsPanel();

            // Next hole panel
            LoadNextHole();

            // gps_profile_prompt_on_entry — Home offers NOTHING. The post-signup Golf Profile
            // capture used to be handed off from here on the frame after Home came up; a fresh
            // install must land on the game and STAY there (device-pass finding #2), so the offer
            // now lives on the first entry into the GPS surface instead —
            // GpsAuthExtrasFlow.InterceptHubEntry, called from ScreenManager.Navigate. Nothing
            // replaces those two call sites here: the pill tap below goes through that same
            // Navigate, so Home is covered without knowing anything about the flow.
        }

        private void OnDisable()
        {
            if (CharacterManager.Instance != null)
                CharacterManager.Instance.OnCharacterSelected -= OnCharacterSelectionChanged;

            Golfin.Notices.NoticeService.OnNoticesChanged -= OnNoticesChanged;
            LocalizationManager.OnLanguageChanged -= OnNoticeLanguageChanged;
        }

        private void OnCharacterSelectionChanged(string _) => UpdateHomeCharacterImage();

        private void UpdateHomeCharacterImage()
        {
            if (characterImage == null) return;

            var selectedId = CharacterManager.Instance?.GetSelectedCharacterId();
            if (string.IsNullOrEmpty(selectedId)) return;

            var csvChar = CharacterDatabaseCSV.Instance?.GetCharacter(selectedId);
            string charName = csvChar?.characterName ?? "";

            var sprite = Resources.Load<Sprite>($"Characters/Homescreen/{charName}");
            if (sprite == null)
                sprite = Resources.Load<Sprite>("Characters/Homescreen/Placeholder");

            if (sprite != null)
            {
                characterImage.sprite = sprite;
                characterImage.preserveAspect = true;
            }
        }

        private void Update()
        {
            // Auto-cycle news panel. Stops dead at one page or none — a single notice must not
            // "cycle" to itself, and a hidden panel has nothing to cycle.
            if (_autoCycleNews && NewsPageCount > 1 && newsAutoCycleInterval > 0f)
            {
                _newsTimer += Time.deltaTime;
                if (_newsTimer >= newsAutoCycleInterval)
                {
                    _newsTimer = 0f;
                    NextNewsPage();
                }
            }
        }

        // ---------- Top Bar ----------

        private void OnSettingsClicked()
        {
            // Settings is now an overlay managed by SettingsController, not ScreenManager
            if (SettingsController.Instance != null)
            {
                SettingsController.Instance.OpenSettings();
            }
        }

        // ---------- News Panel ----------

        /// <summary>
        /// How many notice pages exist right now.
        /// <para>
        /// The demo build is always exactly one page: it has no server, so it keeps its bundled
        /// welcome message verbatim (home_notices SPEC §4.2). The full game asks
        /// <c>NoticeService</c>, and <b>0 is a normal answer</b> meaning "nothing is published" —
        /// it hides the panel rather than falling back to any bundled string.
        /// </para>
        /// </summary>
        private int NewsPageCount
        {
            get
            {
                if (GolfinRedux.Demo.DemoGate.IsDemo) return 1;
                var service = Golfin.Notices.NoticeService.Instance;
                return service != null ? service.Pages.Count : 0;
            }
        }

        /// <summary>
        /// Re-clamp the current page into range, then repaint dots and content. Entry point for
        /// every path that can change the page COUNT underneath the player — screen entry, a fetch
        /// that replaced the set, and a language switch that can drop a page whose only copy was
        /// Japanese.
        /// </summary>
        private void RefreshNewsPanel()
        {
            int count = NewsPageCount;

            // A refresh that removed pages while the player was looking at page 3 must not leave
            // the index out of range (SPEC §4.5).
            if (count <= 0) _currentNewsIndex = 0;
            else if (_currentNewsIndex >= count) _currentNewsIndex = count - 1;
            else if (_currentNewsIndex < 0) _currentNewsIndex = 0;

            UpdateNewsDots();
            UpdateNewsContent();
        }

        private void OnNoticesChanged() => RefreshNewsPanel();

        /// <summary>
        /// A language switch happens in the Settings OVERLAY, which leaves Home enabled — so
        /// nothing re-runs <c>OnEnable</c> and the repaint has to come from the event.
        /// </summary>
        private void OnNoticeLanguageChanged() => RefreshNewsPanel();

        public void NextNewsPage()
        {
            int count = NewsPageCount;
            if (count <= 0) return;
            _currentNewsIndex = (_currentNewsIndex + 1) % count;
            _newsTimer = 0f; // Reset timer when manually changed
            UpdateNewsDots();
            UpdateNewsContent();
        }

        public void PreviousNewsPage()
        {
            int count = NewsPageCount;
            if (count <= 0) return;
            _currentNewsIndex = (_currentNewsIndex - 1 + count) % count;
            _newsTimer = 0f; // Reset timer when manually changed
            UpdateNewsDots();
            UpdateNewsContent();
        }

        public void SetNewsPage(int index)
        {
            int count = NewsPageCount;
            if (count <= 0) return;
            _currentNewsIndex = Mathf.Clamp(index, 0, count - 1);
            _newsTimer = 0f; // Reset timer when manually changed
            UpdateNewsDots();
            UpdateNewsContent();
        }

        /// <summary>
        /// Show one dot per page, highlight the current one (home_notices SPEC §4.4).
        /// <list type="bullet">
        ///   <item><c>n &lt;= 1</c> — the container goes away entirely. One page needs no dots, and
        ///   zero pages means the whole notice area is gone.</item>
        ///   <item>otherwise — the first <c>min(n, childCount)</c> dots are on, the rest off.</item>
        /// </list>
        /// </summary>
        private void UpdateNewsDots()
        {
            if (dotsContainer == null) return;

            int count = NewsPageCount;
            var containerGo = dotsContainer.gameObject;

            if (count <= 1)
            {
                if (containerGo.activeSelf) containerGo.SetActive(false);
                return;
            }

            if (!containerGo.activeSelf) containerGo.SetActive(true);

            // One dot per notice, however many the endpoint returns. The scene's authored dots are
            // adopted as the pool, so the look is unchanged for the usual 2-3 notices.
            _newsDots ??= new PaginationDotStrip(
                dotsContainer, dotPrefab: null, adoptExisting: true,
                inactiveColor: new Color(1f, 1f, 1f, 0.4f));
            _newsDots.Rebuild(count, _currentNewsIndex);
        }

        /// <summary>
        /// Paint the current notice page (home_notices SPEC §4.2).
        /// <list type="number">
        ///   <item><b>Demo build</b> — the bundled welcome message, UNCHANGED. A demo build has no
        ///   server to ask.</item>
        ///   <item><b>At least one live page</b> — the operator's copy for
        ///   <c>_currentNewsIndex</c>, already language-resolved by <c>NoticeService</c>.</item>
        ///   <item><b>Nothing live</b> — the panel is hidden. Deliberately NOT the bundled
        ///   <c>HOME_MAINTENANCE_*</c> strings: those name a date in the past, and showing them
        ///   offline is the exact bug this feature removes (SPEC §4.3).</item>
        /// </list>
        /// </summary>
        private void UpdateNewsContent()
        {
            // demo_build_slice §3.4: the demo build shows a welcome message instead of a notice.
            if (GolfinRedux.Demo.DemoGate.IsDemo)
            {
                SetNewsPanelVisible(true);
                if (newsTitleText != null)
                    newsTitleText.text = LocalizationManager.Get("HOME_DEMO_WELCOME_TITLE");
                if (newsBodyText != null)
                    newsBodyText.text = LocalizationManager.Get("HOME_DEMO_WELCOME_BODY");
                return;
            }

            var service = Golfin.Notices.NoticeService.Instance;
            var pages = service != null ? service.Pages : null;

            if (pages == null || pages.Count == 0)
            {
                SetNewsPanelVisible(false);
                return;
            }

            int index = Mathf.Clamp(_currentNewsIndex, 0, pages.Count - 1);
            var page = pages[index];

            SetNewsPanelVisible(true);
            if (newsTitleText != null) newsTitleText.text = page.Title;
            if (newsBodyText != null)  newsBodyText.text  = page.Body;
        }

        /// <summary>
        /// Show or hide the notice panel. An unassigned <see cref="newsPanelRoot"/> is not an error:
        /// the panel stays exactly as authored, which is the pre-change behaviour (SPEC §4.1).
        /// </summary>
        private void SetNewsPanelVisible(bool visible)
        {
            if (newsPanelRoot != null && newsPanelRoot.activeSelf != visible)
                newsPanelRoot.SetActive(visible);

            // The daily pill sits UNDER this panel and Home has no vertical layout group to
            // reflow it, so the move has to be told, not inferred (daily_mission_home_pill §2).
            // Every path that paints the notice reaches here, which is why the hook is here and
            // not repeated at each RefreshNewsPanel/NextNewsPage call site. The panel's height is
            // fixed (no ContentSizeFitter), so it is already final at this point.
            if (dailyMissionPill != null) dailyMissionPill.RefreshPlacement();
        }

        // ---------- Promo Banner (GPS) ----------

        /// <summary>
        /// The strip's tap. Kept as the <c>onClick</c> target (the listener is added in
        /// <c>Awake</c>) and delegated to the <c>BannerSlotBinder</c> on the same GameObject, which
        /// owns the link and re-checks it against the host allowlist before opening anything.
        /// <para>
        /// The binder deliberately does NOT add its own listener here — one tap must not open the
        /// browser twice.
        /// </para>
        /// </summary>
        private void OnPromoBannerClicked()
        {
            var binder = promoBannerButton != null
                ? promoBannerButton.GetComponent<Golfin.Banners.BannerSlotBinder>()
                : null;

            if (binder == null)
            {
                Debug.LogWarning("[HomeScreen] Promo banner tapped but no BannerSlotBinder is attached.");
                return;
            }
            binder.OpenLink();
        }


        // ---------- Next Hole Panel ----------

        private void LoadNextHole()
        {
            // If HoleDatabase is assigned, load from there
            if (holeDatabase != null)
            {
                HoleData hole = holeDatabase.GetHole(currentHoleIndex);
                if (hole != null)
                {
                    SetNextHoleFromData(hole);
                    return;
                }
            }

            // Try runtime database (auto-loaded from CSV)
            if (HoleDatabaseLoader.RuntimeDatabase != null)
            {
                HoleData hole = HoleDatabaseLoader.GetHole(currentHoleIndex);
                if (hole != null)
                {
                    SetNextHoleFromData(hole);
                    return;
                }
            }

            // Fallback: Use hardcoded stub data
            SetNextHole("HOLE_LOMOND_5", 100, RewardType.RepairKit, 1, RewardType.Ball, 3);
        }

        /// <summary>
        /// Set next hole panel from HoleData (uses localization + reward structure).
        /// </summary>
        public void SetNextHoleFromData(HoleData holeData)
        {
            if (holeData == null) return;

            // Title
            if (nextHoleTitleText != null)
                nextHoleTitleText.text = LocalizationManager.Get("HOME_NEXT_HOLE");

            // Course name (localized)
            if (courseNameText != null)
                courseNameText.text = LocalizationManager.Get(holeData.courseNameKey);

            // Rewards
            for (int i = 0; i < 3; i++)
            {
                if (i < holeData.rewards.Count)
                {
                    HoleReward reward = holeData.rewards[i];
                    SetupRewardRow(i, reward.type, reward.amount);
                }
                else
                {
                    HideRewardRow(i);
                }
            }
        }

        /// <summary>
        /// Legacy method for setting next hole (with localization key support).
        /// </summary>
        public void SetNextHole(string courseNameKey, 
                                int reward1Amount, RewardType reward1Type,
                                int reward2Amount, RewardType reward2Type,
                                int reward3Amount, RewardType reward3Type)
        {
            if (nextHoleTitleText != null)
                nextHoleTitleText.text = LocalizationManager.Get("HOME_NEXT_HOLE");

            if (courseNameText != null)
                courseNameText.text = LocalizationManager.Get(courseNameKey);

            SetupRewardRow(0, reward1Type, reward1Amount);
            SetupRewardRow(1, reward2Type, reward2Amount);
            SetupRewardRow(2, reward3Type, reward3Amount);
        }

        /// <summary>
        /// Simpler overload for quick testing.
        /// </summary>
        public void SetNextHole(string courseNameKey, 
                                int pointsReward, 
                                RewardType item1Type, int item1Amount,
                                RewardType item2Type, int item2Amount)
        {
            SetNextHole(courseNameKey, 
                       pointsReward, RewardType.Points,
                       item1Amount, item1Type,
                       item2Amount, item2Type);
        }

        private void SetupRewardRow(int rowIndex, RewardType rewardType, int amount)
        {
            GameObject rowRoot = null;
            Image icon = null;
            TextMeshProUGUI amountLabel = null;

            switch (rowIndex)
            {
                case 0:
                    rowRoot = rewardRow1;
                    icon = reward1Icon;
                    amountLabel = reward1Amount;
                    break;
                case 1:
                    rowRoot = rewardRow2;
                    icon = reward2Icon;
                    amountLabel = reward2Amount;
                    break;
                case 2:
                    rowRoot = rewardRow3;
                    icon = reward3Icon;
                    amountLabel = reward3Amount;
                    break;
            }

            if (rowRoot == null) return;

            // demo_build_slice §3.4: suppress reward rows whose type is disabled in the demo
            // (all three are off, so the next-hole reward area is empty). No-op in the full game.
            bool typeEnabled = !GolfinRedux.Demo.DemoGate.IsDemo || rewardType switch
            {
                RewardType.Points    => GolfinRedux.Demo.DemoConfig.Instance.PointsEnabled,
                RewardType.RepairKit => GolfinRedux.Demo.DemoConfig.Instance.RepairKitsEnabled,
                RewardType.Ball      => GolfinRedux.Demo.DemoConfig.Instance.BallsEnabled,
                _                    => true
            };
            bool show = amount > 0 && typeEnabled;
            rowRoot.SetActive(show);
            if (!show) return;

            // Set icon sprite
            if (icon != null)
            {
                icon.sprite = rewardType switch
                {
                    RewardType.Points => pointsIcon,
                    RewardType.RepairKit => repairKitIcon,
                    RewardType.Ball => ballIcon,
                    _ => null
                };
            }

            // Set amount text
            if (amountLabel != null)
                amountLabel.text = $"x{amount}";
        }

        private void HideRewardRow(int rowIndex)
        {
            GameObject rowRoot = rowIndex switch
            {
                0 => rewardRow1,
                1 => rewardRow2,
                2 => rewardRow3,
                _ => null
            };

            if (rowRoot != null)
                rowRoot.SetActive(false);
        }

        private void OnPlayClicked()
        {
            Debug.Log("[HomeScreen] PLAY clicked");
            if (matchmakingModal != null)
            {
                matchmakingModal.Open(currentHoleIndex);
                return;
            }
            // K13 (boot_loading_screen_removal): this used to fall back to ScreenId.Loading,
            // which in LegacyBootHome mode is a fake 2s timer that just bounces straight back
            // to Home — it never loaded a hole, so it helped nobody. This branch only fires on
            // a wiring bug (matchmakingModal unassigned in the scene), so say so loudly instead.
            Debug.LogError("[HomeScreen] PLAY: matchmakingModal is not wired on this " +
                           "HomeScreenController — cannot start a match. Assign it in the Inspector.");
        }

    }
}
