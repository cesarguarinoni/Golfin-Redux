using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace GolfinRedux.UI
{
    /// <summary>
    /// Home screen controller: top bar, news/announcement, character,
    /// GPS promo banner, next hole panel, and bottom navigation.
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

        private int _currentNewsIndex;

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
            UpdateNewsDots();
            // TODO: set newsTitleText / newsBodyText via localization keys or data

            // Next hole panel stub
            SetNextHole("Tutorial Course", 1, 100, 1, 3);

            // Bottom nav highlight Home
            SetActiveNav(ScreenId.Home);
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
            UpdateNewsDots();
            // TODO: change title/body based on _currentNewsIndex
        }

        public void SetNewsPage(int index)
        {
            if (totalNewsPages <= 0) return;
            _currentNewsIndex = Mathf.Clamp(index, 0, totalNewsPages - 1);
            UpdateNewsDots();
            // TODO: change title/body based on _currentNewsIndex
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

        /// <summary>
        /// Temporary stub to set next hole panel.
        /// Later you’ll drive this from a hole database / save data.
        /// </summary>
        public void SetNextHole(string courseName, int holeNumber,
                                int rewardPoints, int rewardItem1, int rewardItem2)
        {
            if (nextHoleTitleText != null)
                nextHoleTitleText.text = "NEXT HOLE"; // or LocalizedText

            if (courseNameText != null)
                courseNameText.text = $"{courseName} - Hole {holeNumber}";

            SetupRewardRow(rewardRow1, reward1Amount, rewardPoints);
            SetupRewardRow(rewardRow2, reward2Amount, rewardItem1);
            SetupRewardRow(rewardRow3, reward3Amount, rewardItem2);
        }

        private void SetupRewardRow(GameObject rowRoot, TextMeshProUGUI amountLabel, int amount)
        {
            if (rowRoot == null || amountLabel == null) return;

            bool show = amount > 0;
            rowRoot.SetActive(show);
            if (!show) return;

            amountLabel.text = $"x{amount}";
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
