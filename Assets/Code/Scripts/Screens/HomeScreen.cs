using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;
using System.Collections;

/// <summary>
/// Home screen — main hub after splash.
/// Features: currency display, username banner, announcement cards (paginated),
/// random character display, GPS banner, next hole panel, bottom nav bar.
/// </summary>
public class HomeScreen : ScreenBase
{
    [Header("Top Bar")]
    [SerializeField] private TextMeshProUGUI currencyText;
    [SerializeField] private Image currencyIcon;
    [SerializeField] private TextMeshProUGUI usernameText;

    [Header("Announcement")]
    [SerializeField] private TextMeshProUGUI announcementTitle;
    [SerializeField] private TextMeshProUGUI announcementBody;
    [SerializeField] private Transform dotContainer;
    [SerializeField] private int totalAnnouncements = 3;

    [Header("Character")]
    [SerializeField] private Image characterImage;
    [SerializeField] private Sprite[] characterSprites;

    [Header("GPS Banner")]
    [SerializeField] private TextMeshProUGUI gpsTitleText;
    [SerializeField] private TextMeshProUGUI gpsSubtitleText;
    [SerializeField] private TextMeshProUGUI gpsDescText;

    [Header("Next Hole")]
    [SerializeField] private TextMeshProUGUI nextHoleHeader;
    [SerializeField] private TextMeshProUGUI courseNameText;
    [SerializeField] private TextMeshProUGUI[] rewardTexts;
    [SerializeField] private PressableButton playButton;

    [Header("Bottom Nav")]
    [SerializeField] private PressableButton navHome;
    [SerializeField] private PressableButton navShop;
    [SerializeField] private PressableButton navPlay;
    [SerializeField] private PressableButton navBag;
    [SerializeField] private PressableButton navMore;

    [Header("Settings Button")]
    [SerializeField] private PressableButton settingsButton;  // Drag SettingsButton here in Inspector

    [Header("Settings")]
    [SerializeField] private int startingCurrency = 50000;
    
    [Header("Settings Events")]
    public UnityEvent OnSettingsPressed;  // Configure in Inspector to open Settings

    private int _currentAnnouncement;

    public override void OnScreenEnter()
    {
        // Load saved data
        var save = SaveManager.Data;

        // Character: use saved index or random
        if (save.selectedCharacterIndex >= 0 && characterSprites != null
            && save.selectedCharacterIndex < characterSprites.Length)
        {
            if (characterImage != null)
            {
                characterImage.sprite = characterSprites[save.selectedCharacterIndex];
                characterImage.preserveAspect = true;
            }
        }
        else
        {
            RandomizeCharacter();
        }

        // Currency from save
        if (currencyText != null)
            currencyText.text = save.currency.ToString();

        // Username from save
        if (usernameText != null)
            usernameText.text = save.username;

        // Show first announcement
        _currentAnnouncement = 0;
        UpdateAnnouncementDots();
        
        // Connect Settings button (same pattern as SplashScreen Start button)
        if (settingsButton != null)
            settingsButton.onClick.AddListener(HandleSettings);
    }

    public override void OnScreenExit()
    {
        // Clean up Settings button listener (same pattern as SplashScreen)
        if (settingsButton != null)
            settingsButton.onClick.RemoveListener(HandleSettings);
    }

    private void HandleSettings()
    {
        Debug.Log("[HomeScreen] SETTINGS pressed");
        OnSettingsPressed?.Invoke();  // UnityEvent handles the transition (configure in Inspector)
    }

    public void RandomizeCharacter()
    {
        if (characterImage == null || characterSprites == null || characterSprites.Length == 0)
            return;

        int idx = Random.Range(0, characterSprites.Length);
        characterImage.sprite = characterSprites[idx];
        characterImage.preserveAspect = true;
        characterImage.SetNativeSize();
    }

    public void NextAnnouncement()
    {
        _currentAnnouncement = (_currentAnnouncement + 1) % totalAnnouncements;
        UpdateAnnouncementDots();
        // TODO: Load announcement content from data source
    }

    public void SetAnnouncement(int index)
    {
        _currentAnnouncement = Mathf.Clamp(index, 0, totalAnnouncements - 1);
        UpdateAnnouncementDots();
    }

    private void UpdateAnnouncementDots()
    {
        if (dotContainer == null) return;
        for (int i = 0; i < dotContainer.childCount; i++)
        {
            var img = dotContainer.GetChild(i).GetComponent<Image>();
            if (img != null)
                img.color = i == _currentAnnouncement
                    ? new Color(1f, 1f, 1f, 1f)
                    : new Color(1f, 1f, 1f, 0.4f);
        }
    }

    public void SetCurrency(int amount)
    {
        if (currencyText != null)
            currencyText.text = amount.ToString();
    }

    public void SetUsername(string name)
    {
        if (usernameText != null)
            usernameText.text = name;
    }

    public void SetNextHole(string courseName, int holeNumber)
    {
        if (courseNameText != null)
            courseNameText.text = $"{courseName}  - Hole {holeNumber}";
    }
}