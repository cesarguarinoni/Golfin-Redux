#nullable enable
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Golfin.Roster;

namespace Golfin.UI.Rankings
{
    /// <summary>
    /// Row binder for a RankingsCards or RankingsCardUser prefab instance.
    ///
    /// Expected hierarchy (confirmed from prefab inspection):
    ///   RankingsCard/Rank                      — TextMeshProUGUI
    ///   RankingsCard/Mask/Portrait             — Image
    ///   RankingsCard/Mask/Background           — Image (rarity bg)
    ///   RankingsCard/Name+Level/NameLabel      — TextMeshProUGUI
    ///   RankingsCard/Name+Level/Rarity+Level/RartityLabel  — TextMeshProUGUI (note: sic typo kept)
    ///   RankingsCard/Name+Level/Rarity+Level/LevelLabel    — TextMeshProUGUI
    ///   RankingsCard/RewardPoints/Background/NameLabel     — TextMeshProUGUI (score)
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class RankingsCardWidget : MonoBehaviour
    {
        // ── References — resolved at runtime from child hierarchy ─────────────
        private TextMeshProUGUI? _rankLabel;
        private Image?           _portrait;
        private Image?           _rarityBg;
        private TextMeshProUGUI? _nameLabel;
        private TextMeshProUGUI? _rarityLabel; // note: "RartityLabel" in prefab (typo kept)
        private TextMeshProUGUI? _levelLabel;
        private TextMeshProUGUI? _scoreLabel;

        private bool _resolved;

        private void Awake() => Resolve();

        private void Resolve()
        {
            if (_resolved) return;
            _resolved = true;

            // The card row is a direct child named "RankingsCard"
            Transform? card = transform.Find("RankingsCard");
            if (card == null)
            {
                // If this IS the RankingsCard (e.g. Top1Card hierarchy), use self
                card = transform;
            }

            _rankLabel  = card.Find("Rank")?.GetComponent<TextMeshProUGUI>();
            _portrait   = card.Find("Mask/Portrait")?.GetComponent<Image>();
            _rarityBg   = card.Find("Mask/Background")?.GetComponent<Image>();
            _nameLabel  = card.Find("Name+Level/NameLabel")?.GetComponent<TextMeshProUGUI>();
            _rarityLabel = card.Find("Name+Level/Rarity+Level/RartityLabel ")?.GetComponent<TextMeshProUGUI>();
            if (_rarityLabel == null) // try without trailing space
                _rarityLabel = card.Find("Name+Level/Rarity+Level/RartityLabel")?.GetComponent<TextMeshProUGUI>();
            _levelLabel = card.Find("Name+Level/Rarity+Level/LevelLabel ")?.GetComponent<TextMeshProUGUI>();
            if (_levelLabel == null)
                _levelLabel = card.Find("Name+Level/Rarity+Level/LevelLabel")?.GetComponent<TextMeshProUGUI>();
            _scoreLabel = card.Find("RewardPoints/Background/NameLabel")?.GetComponent<TextMeshProUGUI>();
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Bind a leaderboard entry to this card widget.</summary>
        public void Bind(LeaderboardEntry entry)
        {
            Resolve();

            // Rank label (with T prefix on ties)
            if (_rankLabel != null)
                _rankLabel.text = entry.IsTie ? $"T{entry.Rank}" : $"{entry.Rank}";

            // Name
            if (_nameLabel != null)
                _nameLabel.text = entry.DisplayName;

            // Level
            if (_levelLabel != null)
                _levelLabel.text = $"LVL {entry.Level}";

            // Score
            if (_scoreLabel != null)
                _scoreLabel.text = FormatScore(entry.Score);

            // Portrait + rarity art
            BindCharacterArt(entry.CharacterId);
        }

        private void BindCharacterArt(string characterId)
        {
            if (string.IsNullOrEmpty(characterId)) return;

            // Resolve character template via CharacterDatabaseCSV (same pattern as MatchmakingModalController)
            CharacterDataRuntime? template = null;
            if (CharacterDatabaseCSV.Instance != null)
                template = CharacterDatabaseCSV.Instance.GetCharacter(characterId);

            if (template == null) return;

            // Portrait: Resources/Portraits/InGame/<portraitSpriteName>, fallback to portraitSprite field
            if (_portrait != null)
            {
                Sprite? portrait = null;
                if (!string.IsNullOrEmpty(template.portraitSpriteName))
                    portrait = Resources.Load<Sprite>($"Portraits/InGame/{template.portraitSpriteName}");
                if (portrait == null) portrait = template.portraitSprite;
                if (portrait != null) _portrait.sprite = portrait;
            }

            // Rarity background: Resources/Rarities/<rarity>
            if (_rarityBg != null)
            {
                Sprite? rarityBg = Resources.Load<Sprite>($"Rarities/{template.rarity}");
                if (rarityBg != null) _rarityBg.sprite = rarityBg;
            }

            // Rarity label text + color — R2-Fix E: spell out full name (RARE, LEGENDARY, etc.)
            if (_rarityLabel != null)
            {
                _rarityLabel.text  = RarityHelper.GetRarityFullName(template.rarity);
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
