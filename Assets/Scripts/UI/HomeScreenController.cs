using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Golfin.UI;
using Golfin.Roster;
using Golfin.UI.Matchmaking;

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
        [SerializeField] private int totalNewsPages = 3;
        [SerializeField] private float newsAutoCycleInterval = 5f; // seconds

        private int _currentNewsIndex;
        private float _newsTimer;
        private bool _autoCycleNews = true;

        // -------- Promo Banner (GPS) --------
        [Header("Promo Banner (GPS)")]
        [SerializeField] private Button promoBannerButton;
        [SerializeField] private TextMeshProUGUI promoBannerText;
        [SerializeField] private Image gpsIcon;

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
                usernameText.text = "Player";   // TODO: load real value

            // Subscribe to selection changes so the image updates immediately
            if (CharacterManager.Instance != null)
                CharacterManager.Instance.OnCharacterSelected += OnCharacterSelectionChanged;

            // Character
            UpdateHomeCharacterImage();

            // News
            _currentNewsIndex = 0;
            _newsTimer = 0f;
            UpdateNewsDots();
            UpdateNewsContent();

            // Next hole panel
            LoadNextHole();
        }

        private void OnDisable()
        {
            if (CharacterManager.Instance != null)
                CharacterManager.Instance.OnCharacterSelected -= OnCharacterSelectionChanged;
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
            // Auto-cycle news panel
            if (_autoCycleNews && totalNewsPages > 1 && newsAutoCycleInterval > 0f)
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

        public void NextNewsPage()
        {
            if (totalNewsPages <= 0) return;
            _currentNewsIndex = (_currentNewsIndex + 1) % totalNewsPages;
            _newsTimer = 0f; // Reset timer when manually changed
            UpdateNewsDots();
            UpdateNewsContent();
        }

        public void PreviousNewsPage()
        {
            if (totalNewsPages <= 0) return;
            _currentNewsIndex = (_currentNewsIndex - 1 + totalNewsPages) % totalNewsPages;
            _newsTimer = 0f; // Reset timer when manually changed
            UpdateNewsDots();
            UpdateNewsContent();
        }

        public void SetNewsPage(int index)
        {
            if (totalNewsPages <= 0) return;
            _currentNewsIndex = Mathf.Clamp(index, 0, totalNewsPages - 1);
            _newsTimer = 0f; // Reset timer when manually changed
            UpdateNewsDots();
            UpdateNewsContent();
        }

        private void UpdateNewsDots()
        {
            if (dotsContainer == null) return;

            for (int i = 0; i < dotsContainer.childCount; i++)
            {
                var img = dotsContainer.GetChild(i).GetComponent<Image>();
                if (img == null) continue;

                img.color = (i == _currentNewsIndex)
                    ? new Color(1f, 1f, 1f, 1f)
                    : new Color(1f, 1f, 1f, 0.4f);
            }
        }

        private void UpdateNewsContent()
        {
            // TODO: Load news from data/CSV based on _currentNewsIndex
            // demo_build_slice §3.4: the demo build shows a welcome message instead of the
            // maintenance notice. No-op in the full game.
            bool demo = GolfinRedux.Demo.DemoGate.IsDemo;
            string titleKey = demo ? "HOME_DEMO_WELCOME_TITLE" : "HOME_MAINTENANCE_TITLE";
            string bodyKey  = demo ? "HOME_DEMO_WELCOME_BODY"  : "HOME_MAINTENANCE_BODY";
            if (newsTitleText != null)
                newsTitleText.text = LocalizationManager.Get(titleKey);

            if (newsBodyText != null)
                newsBodyText.text = LocalizationManager.Get(bodyKey);
        }

        // ---------- Promo Banner (GPS) ----------

        private void OnPromoBannerClicked()
        {
            // TODO: open GPS info / permissions panel
            Debug.Log("[HomeScreen] Promo (GPS) banner clicked");
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
            // Legacy fallback if matchmaking isn't wired in this scene
            if (screenManager != null)
                screenManager.ShowScreen(ScreenId.Loading);
        }

    }
}
