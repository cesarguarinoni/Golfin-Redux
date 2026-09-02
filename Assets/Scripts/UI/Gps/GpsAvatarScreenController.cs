// gps_profile_pack §5.2 — GPS My Avatar screen (Figma 14026:33187).
#nullable enable
using System;
using Golfin.Economy;
using Golfin.Gps;
using Golfin.Net;
using Golfin.Roster;
using Golfin.Social;
using Golfin.Telemetry;
using GolfinRedux.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.Gps.UI
{
    [DisallowMultipleComponent]
    public sealed class GpsAvatarScreenController : MonoBehaviour
    {
        private const string Tag     = "[GpsAvatar]";
        private const string Unknown = "—";

        // ── Character figure ──────────────────────────────────────────────────
        [Header("Character figure")]
        [SerializeField] private Image? _characterFigure;

        // ── Level row ─────────────────────────────────────────────────────────
        [Header("Level row")]
        [SerializeField] private TextMeshProUGUI? _levelLabel;
        [SerializeField] private TextMeshProUGUI? _rankLabel;

        // ── XP panel ──────────────────────────────────────────────────────────
        [Header("XP panel")]
        [SerializeField] private TextMeshProUGUI? _xpLevelFrom;
        [SerializeField] private TextMeshProUGUI? _xpLevelTo;
        [SerializeField] private TextMeshProUGUI? _xpHint;
        [SerializeField] private Image?           _xpTrackFill;
        [SerializeField] private TextMeshProUGUI? _xpFooter;

        // ── Unlock panel (restored by Cesar 2026-09-02; SPEC had it hidden in v1) ──
        [Header("Unlock panel")]
        [SerializeField] private GameObject?      _unlockPanel;
        [SerializeField] private TextMeshProUGUI? _unlockTitle;

        // ── Evolution panel ───────────────────────────────────────────────────
        [Header("Evolution panel")]
        [Tooltip("Exactly 5 stage views: Beginner(1), Rookie(5), Amateur(12), Single(20), Pro(50).")]
        [SerializeField] private GpsEvolutionStageView[] _evolutionStages = new GpsEvolutionStageView[0];

        // ── Status panel (character stats) ────────────────────────────────────
        [Header("Status panel — character stats")]
        [SerializeField] private TextMeshProUGUI? _statusNote;
        [SerializeField] private Image?           _statStrengthFill;
        [SerializeField] private TextMeshProUGUI? _statStrengthLabel;
        [SerializeField] private Image?           _statClubControlFill;
        [SerializeField] private TextMeshProUGUI? _statClubControlLabel;
        [SerializeField] private Image?           _statRecoveryFill;
        [SerializeField] private TextMeshProUGUI? _statRecoveryLabel;
        [SerializeField] private Image?           _statStaminaFill;
        [SerializeField] private TextMeshProUGUI? _statStaminaLabel;

        // ── Navigation ────────────────────────────────────────────────────────
        [Header("Navigation")]
        [SerializeField] private Button? _backButton;

        private bool _wiredOnce;

        private static readonly (int level, string key)[] RankThresholds = {
            (50, "GPS_AVATAR_RANK_PRO"),
            (20, "GPS_AVATAR_RANK_SINGLE"),
            (12, "GPS_AVATAR_RANK_AMATEUR"),
            ( 5, "GPS_AVATAR_RANK_ROOKIE"),
            ( 1, "GPS_AVATAR_RANK_BEGINNER"),
        };

        // ═══════════════════════════════════════════════════════════════════
        // Lifecycle
        // ═══════════════════════════════════════════════════════════════════

        private void Awake()
        {
            WireOnce();
        }

        private void WireOnce()
        {
            if (_wiredOnce) return;
            _wiredOnce = true;

            if (_backButton != null)
                _backButton.onClick.AddListener(() =>
                    ScreenManager.Instance?.GoBack(ScreenId.GpsProfile));
        }

        private void OnEnable()
        {
            TelemetryService.Instance.RecordSafe("gps_avatar_open", () => null);
            UserService.Instance.OnDetailChanged += OnDetailChanged;

            // Paint from cache immediately
            BindDetail(UserService.Instance.LastDetail);

            // Fire live fetch (copy GpsHubScreenController:128-136 pattern)
            var client = ApiClient.Instance;
            client.Run(UserService.Instance.Detail(r => { if (r.Success) OnDetailChanged(r.Data); }));
        }

        private void OnDisable()
        {
            UserService.Instance.OnDetailChanged -= OnDetailChanged;
        }

        // ═══════════════════════════════════════════════════════════════════
        // Data binding
        // ═══════════════════════════════════════════════════════════════════

        private void OnDetailChanged(UserDetailDto d) => BindDetail(d);

        private void BindDetail(UserDetailDto? d)
        {
            BindCharacterFigure();
            BindCharacterStats();

            if (d == null) { ShowXpPlaceholders(); return; }

            int lv   = d.AvatarLevel ?? 1;
            int xp   = d.AvatarXp   ?? 0;
            int next = 500 * lv;

            SetText(_levelLabel, $"Lv.{lv}");
            SetText(_rankLabel,  LocalizationManager.Get(GetRankKey(lv)));

            // Node 14026:33495 is a single run: "Lv.12 → Lv.13".
            SetText(_xpLevelFrom, $"Lv.{lv} → Lv.{lv + 1}");
            SetText(_xpLevelTo,   string.Empty);

            // Hint: rounds = ceil((next - xp) / 50), estimated
            int roundsNeeded = Mathf.CeilToInt((next - xp) / 50f);
            SetText(_xpHint, $"{roundsNeeded} more rounds");

            if (_xpTrackFill != null)
                GpsUiColor.SetBarFill(_xpTrackFill, next > 0 ? Mathf.Clamp01((float)xp / next) : 0f);

            SetText(_xpFooter, $"{xp} / {next} XP");

            if (_unlockTitle != null)
                _unlockTitle.text = string.Format(
                    LocalizationManager.Get("GPS_AVATAR_UNLOCKS_FMT"), lv + 1);

            BindEvolution(lv);
        }

        private void BindCharacterFigure()
        {
            if (_characterFigure == null) return;
            try
            {
                // Use Home-screen art (cropped into RectMask2D container in the prefab).
                // Source: HomeScreenController.UpdateHomeCharacterImage:232-250
                var selectedId = CharacterManager.Instance?.GetSelectedCharacterId();
                var csvChar    = CharacterDatabaseCSV.Instance?.GetCharacter(selectedId);
                var sprite     = Resources.Load<Sprite>($"Characters/Homescreen/{csvChar?.characterName}")
                              ?? Resources.Load<Sprite>("Characters/Homescreen/Placeholder");
                if (sprite != null)
                {
                    _characterFigure.sprite        = sprite;
                    _characterFigure.preserveAspect = false; // explicit rect set in builder; no fit needed
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{Tag} Could not bind character figure: {e.Message}");
            }
        }

        private void BindCharacterStats()
        {
            try
            {
                var mgr = CharacterManager.Instance;
                if (mgr == null) return;
                string charId   = mgr.GetSelectedCharacterId();
                var playerData  = mgr.GetCharacterData(charId);
                var charData    = CharacterDatabaseCSV.Instance?.GetCharacter(charId);
                if (playerData == null || charData == null) return;

                var rarity = charData.rarity;

                BindStatBar(_statStrengthFill,   _statStrengthLabel,
                    playerData.currentStrength,
                    RarityStatCaps.GetStatCap(rarity, "Strength"));

                BindStatBar(_statClubControlFill, _statClubControlLabel,
                    playerData.currentClubControl,
                    RarityStatCaps.GetStatCap(rarity, "ClubControl"));

                BindStatBar(_statRecoveryFill,   _statRecoveryLabel,
                    playerData.currentRecovery,
                    RarityStatCaps.GetStatCap(rarity, "Recovery"));

                BindStatBar(_statStaminaFill,    _statStaminaLabel,
                    playerData.currentStamina,
                    RarityStatCaps.GetStatCap(rarity, "Stamina"));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{Tag} Could not bind character stats: {e.Message}");
            }
        }

        private static void BindStatBar(Image? fill, TextMeshProUGUI? label, int value, int cap)
        {
            if (fill  != null) GpsUiColor.SetBarFill(fill, cap > 0 ? Mathf.Clamp01((float)value / cap) : 0f);
            if (label != null) label.text      = $"{value}/{cap}";
        }

        private void BindEvolution(int level)
        {
            if (_evolutionStages.Length < 5) return;
            int[] thresholds = { 1, 5, 12, 20, 50 };
            for (int i = 0; i < _evolutionStages.Length; i++)
            {
                bool done    = level > thresholds[i];
                bool current = level >= thresholds[i] && (i + 1 >= thresholds.Length || level < thresholds[i + 1]);
                _evolutionStages[i].SetState(done, current);
            }
        }

        private void ShowXpPlaceholders()
        {
            SetText(_levelLabel, Unknown);
            SetText(_rankLabel,  Unknown);
            SetText(_xpLevelFrom, Unknown);
            SetText(_xpLevelTo,   Unknown);
            SetText(_xpHint,   Unknown);
            SetText(_xpFooter, Unknown);
            if (_xpTrackFill != null) GpsUiColor.SetBarFill(_xpTrackFill, 0f);
        }

        private static string GetRankKey(int level)
        {
            foreach (var (threshold, key) in RankThresholds)
                if (level >= threshold) return key;
            return "GPS_AVATAR_RANK_BEGINNER";
        }

        private static void SetText(TextMeshProUGUI? t, string value)
        { if (t != null) t.text = value; }
    }

    // ── Lightweight evolution-stage view ──────────────────────────────────────

    /// <summary>
    /// One evolution stage on the avatar screen (Done / Current / Locked state).
    /// </summary>
    [Serializable]
    public sealed class GpsEvolutionStageView
    {
        [SerializeField] public Image?           IconRing;
        [SerializeField] public TextMeshProUGUI? LevelLabel;
        [SerializeField] public TextMeshProUGUI? RankLabel;

        private static readonly Color GoldStroke  = GpsUiColor.Gold;
        private static readonly Color GreenFill   = GpsUiColor.Green;

        // Node geometry: non-current stages are ring 68 / icon 32 with labels at 74 and 101 in a
        // container at y=90; the CURRENT stage is ring 88 / icon 44 with labels at 94 and 121 in a
        // container at y=80. The builder lays every stage out at the non-current numbers and this
        // promotes whichever one the player is actually on.
        const float RingBase = 68f, RingCur = 88f;
        const float IcoBase  = 32f, IcoCur  = 44f;
        const float RankBase = 74f, RankCur = 94f;
        const float LvBase   = 101f, LvCur  = 121f;
        const float TopBase  = 90f, TopCur  = 80f;

        public void SetState(bool done, bool current)
        {
            float alpha = (done || current) ? 1f : 0.55f;

            if (IconRing != null)
            {
                // CURRENT wins over DONE. `done` is level > threshold, so the stage you are
                // standing on is BOTH done and current, and testing done first painted it green —
                // the node marks the current stage GOLD, which is the "you are here" cue.
                var c = current ? GoldStroke : done ? GreenFill : GpsUiColor.Muted;
                IconRing.color = new Color(c.r, c.g, c.b, alpha);

                float ring = current ? RingCur : RingBase;
                float ico  = current ? IcoCur  : IcoBase;
                var ringRt = IconRing.rectTransform;
                var stage  = ringRt.parent as RectTransform;
                ringRt.sizeDelta = new Vector2(ring, ring);
                if (stage != null)
                {
                    ringRt.anchoredPosition = new Vector2((stage.rect.width - ring) * 0.5f, 0f);
                    // The whole stage rides 10px higher when it is the current one.
                    var p = stage.anchoredPosition;
                    stage.anchoredPosition = new Vector2(p.x, -(current ? TopCur : TopBase));
                }
                if (ringRt.childCount > 0 && ringRt.GetChild(0) is RectTransform icoRt)
                {
                    icoRt.sizeDelta        = new Vector2(ico, ico);
                    icoRt.anchoredPosition = new Vector2((ring - ico) * 0.5f, -(ring - ico) * 0.5f);
                }
            }

            if (RankLabel != null)
            {
                RankLabel.color = new Color(1, 1, 1, alpha);
                var rt = RankLabel.rectTransform;
                rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -(current ? RankCur : RankBase));
            }
            if (LevelLabel != null)
            {
                var lc = current ? GoldStroke : Color.white;
                LevelLabel.color = new Color(lc.r, lc.g, lc.b, alpha);
                var rt = LevelLabel.rectTransform;
                rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -(current ? LvCur : LvBase));
            }
        }
    }
}
