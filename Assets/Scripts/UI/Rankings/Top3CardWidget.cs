#nullable enable
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Golfin.Roster;

namespace Golfin.UI.Rankings
{
    /// <summary>
    /// Binder for the Top1/Top2/Top3Card prefab instances inside the RankingsScreen Top3 podium.
    ///
    /// Confirmed hierarchy (from prefab inspection):
    ///   Mask/Rarity    — Image (rarity background)
    ///   Mask/Portrait  — Image
    ///   Info/UserLabel — TextMeshProUGUI (display name)
    ///   Info/RarityLabel — TextMeshProUGUI
    ///   Info/LevelLabel  — TextMeshProUGUI
    ///   RewardPoints/Background/NameLabel — TextMeshProUGUI (score)
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class Top3CardWidget : MonoBehaviour
    {
        private Image?           _rarityBg;
        private Image?           _portrait;
        private TextMeshProUGUI? _nameLabel;
        private TextMeshProUGUI? _rarityLabel;
        private TextMeshProUGUI? _levelLabel;
        private TextMeshProUGUI? _scoreLabel;

        private bool _resolved;

        private void Awake() => Resolve();

        private void Resolve()
        {
            if (_resolved) return;
            _resolved = true;

            _rarityBg    = transform.Find("Mask/Rarity")?.GetComponent<Image>();
            _portrait    = transform.Find("Mask/Portrait")?.GetComponent<Image>();
            _nameLabel   = transform.Find("Info/UserLabel")?.GetComponent<TextMeshProUGUI>();
            _rarityLabel = transform.Find("Info/RarityLabel")?.GetComponent<TextMeshProUGUI>();
            _levelLabel  = transform.Find("Info/LevelLabel")?.GetComponent<TextMeshProUGUI>();
            _scoreLabel  = transform.Find("RewardPoints/Background/NameLabel")?.GetComponent<TextMeshProUGUI>();
        }


        // Rank rows are bound imperatively, so — unlike a LocalizedText label — nothing repaints
        // them when the language changes. The toggle lives in the Settings OVERLAY, which leaves
        // the screen underneath enabled, so the row never re-enables and RANK_LEVEL / the rarity
        // name kept the old language until the screen was re-entered. Re-bind in place instead.
        private LeaderboardEntry? _lastEntry;

        private void OnEnable()  => LocalizationManager.OnLanguageChanged += RefreshLocalizedText;
        private void OnDisable() => LocalizationManager.OnLanguageChanged -= RefreshLocalizedText;

        private void RefreshLocalizedText()
        {
            if (_lastEntry.HasValue) Bind(_lastEntry.Value);   // Bind is pure data -> visuals
        }

        public void Bind(LeaderboardEntry entry)
        {
            Resolve();
            _lastEntry = entry;

            if (_nameLabel != null)
                _nameLabel.text = entry.DisplayName;

            if (_levelLabel != null)
                _levelLabel.text = string.Format(LocalizationManager.Get("RANK_LEVEL"), entry.Level);

            if (_scoreLabel != null)
                _scoreLabel.text = FormatScore(entry.Score);

            BindCharacterArt(entry.CharacterId);
        }

        private void BindCharacterArt(string characterId)
        {
            if (string.IsNullOrEmpty(characterId)) return;

            CharacterDataRuntime? template = null;
            if (CharacterDatabaseCSV.Instance != null)
                template = CharacterDatabaseCSV.Instance.GetCharacter(characterId);

            if (template == null) return;

            if (_portrait != null)
            {
                Sprite? portrait = null;
                if (!string.IsNullOrEmpty(template.portraitSpriteName))
                    portrait = Resources.Load<Sprite>($"Portraits/Thumbnails/{template.portraitSpriteName}");
                // Graceful fallback: if Thumbnails sprite is missing, fall back to InGame then template default
                if (portrait == null && !string.IsNullOrEmpty(template.portraitSpriteName))
                    portrait = Resources.Load<Sprite>($"Portraits/InGame/{template.portraitSpriteName}");
                if (portrait == null) portrait = template.portraitSprite;
                if (portrait != null) _portrait.sprite = portrait;
            }

            if (_rarityBg != null)
            {
                Sprite? rarityBg = Resources.Load<Sprite>($"Rarities/{template.rarity}");
                if (rarityBg != null) _rarityBg.sprite = rarityBg;
            }

            if (_rarityLabel != null)
            {
                _rarityLabel.text  = RarityHelper.GetLocalizedRarityName(template.rarity);
                _rarityLabel.color = RarityHelper.GetRarityColor(template.rarity);
            }
        }

        // R2-Fix B: coin + number only — no "RP" suffix. Use thousands separators.
        private static string FormatScore(long score)
        {
            return score.ToString("N0");
        }
    }
}
