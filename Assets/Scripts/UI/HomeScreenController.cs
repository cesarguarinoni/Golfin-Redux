using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace GolfinRedux.UI
{
    /// <summary>
    /// Home screen controller: top bar, news/announcement, character,
    /// GPS promo banner, next hole panel, and bottom navigation.
    /// Screen switching is handled by ScreenManager on ShellSceneRoot.
    /// 
    /// NEW: Added localization support, auto-cycle timer for news panel,
    /// and reward icon support for Next Hole panel.
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
        [SerializeField] private Sprite[] characterSprites;

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

        // -------- Bottom Navigation --------
        [Header("Bottom Navigation")]
        [SerializeField] private Button navHomeButton;
        [SerializeField] private Button navGachaButton;
        [SerializeField] private Button navTeeButton;
        [SerializeField] private Button navInventoryButton;
        [SerializeField] private Button navCharactersButton;

        [SerializeField] private Image navHomeIcon;
        [SerializeField] private Image navGachaIcon;
        [SerializeField] private Image navTeeIcon;
        [SerializeField] private Image navInventoryIcon;
        [SerializeField] private Image navCharactersIcon;

        [SerializeField] private Color navNormalColor = Color.white;
        [SerializeField] private Color navActiveColor = Color.cyan;

        // -------- Optional: Hole Database --------
        [Header("Optional: Hole Database")]
        [SerializeField] private HoleDatabase holeDatabase;
        [SerializeField] private int currentHoleIndex = 0;

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

            // Bottom nav
            if (navHomeButton != null)       navHomeButton.onClick.AddListener(() => OnNavClicked(ScreenId.Home));
            if (navGachaButton != null)      navGachaButton.onClick.AddListener(() => OnNavClicked(ScreenId.Home));     // TODO: Gacha
            if (navTeeButton != null)        navTeeButton.onClick.AddListener(() => OnNavClicked(ScreenId.Loading));    // TODO: Hole select
            if (navInventoryButton != null)  navInventoryButton.onClick.AddListener(() => OnNavClicked(ScreenId.Home)); // TODO: Inventory
            if (navCharactersButton != null) navCharactersButton.onClick.AddListener(() => OnNavClicked(ScreenId.Home)); // TODO: Characters
        }

        private void OnEnable()
        {
            // Initial UI state when Home screen becomes active

            // Top bar: placeholder values for now
            if (rewardPointsText != null)
                rewardPointsText.text = "0";    // TODO: load real value

            if (usernameText != null)
                usernameText.text = "Player";   // TODO: load real value

            // Character
            RandomizeCharacter();

            // News
            _currentNewsIndex = 0;
            _newsTimer = 0f;
            UpdateNewsDots();
            UpdateNewsContent();

            // Next hole panel
            LoadNextHole();

            // Bottom nav highlight Home
            SetActiveNav(ScreenId.Home);
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
            if (screenManager != null)
                screenManager.ShowScreen(ScreenId.Settings);
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
            // For now, just use localization keys
            if (newsTitleText != null)
                newsTitleText.text = LocalizationManager.Get("HOME_MAINTENANCE_TITLE");
            
            if (newsBodyText != null)
                newsBodyText.text = LocalizationManager.Get("HOME_MAINTENANCE_BODY");
        }

        // ---------- Promo Banner (GPS) ----------

        private void OnPromoBannerClicked()
        {
            // TODO: open GPS info / permissions panel
            Debug.Log("[HomeScreen] Promo (GPS) banner clicked");
        }

        // ---------- Character ----------

        private void RandomizeCharacter()
        {
            if (characterImage == null || characterSprites == null || characterSprites.Length == 0)
                return;

            int idx = Random.Range(0, characterSprites.Length);
            characterImage.sprite = characterSprites[idx];
            characterImage.preserveAspect = true;
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

            bool show = amount > 0;
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
            if (screenManager != null)
                screenManager.ShowScreen(ScreenId.Loading);
        }

        // ---------- Bottom Navigation ----------

        private void OnNavClicked(ScreenId target)
        {
            SetActiveNav(target);

            if (screenManager == null) return;

            switch (target)
            {
                case ScreenId.Home:
                    screenManager.ShowScreen(ScreenId.Home);
                    break;
                case ScreenId.Loading:
                    screenManager.ShowScreen(ScreenId.Loading);
                    break;
                // For now other tabs just keep you on Home or are TODO
                default:
                    screenManager.ShowScreen(ScreenId.Home);
                    break;
            }
        }

        private void SetActiveNav(ScreenId active)
        {
            if (navHomeIcon != null)
                navHomeIcon.color = active == ScreenId.Home ? navActiveColor : navNormalColor;
            if (navGachaIcon != null)
                navGachaIcon.color = navNormalColor; // no separate screen yet
            if (navTeeIcon != null)
                navTeeIcon.color = active == ScreenId.Loading ? navActiveColor : navNormalColor;
            if (navInventoryIcon != null)
                navInventoryIcon.color = navNormalColor;
            if (navCharactersIcon != null)
                navCharactersIcon.color = navNormalColor;
        }
    }
}
