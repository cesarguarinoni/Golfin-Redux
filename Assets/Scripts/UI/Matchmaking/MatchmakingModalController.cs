#nullable enable
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Golfin.Roster;
using Golfin.UI.Modals;
using GolfinRedux.UI;

namespace Golfin.UI.Matchmaking
{
    /// <summary>
    /// Fake matchmaking modal controller.
    /// Animates a "FINDING OPPONENT…" status with cycling dots,
    /// populates the player portrait, cycles through opponent characters
    /// during search, then freezes on "OPPONENT FOUND" after searchDurationSeconds.
    /// No real networking — purely cosmetic stub for the Mac env test.
    /// </summary>
    public class MatchmakingModalController : ModalController
    {
        // ── Player Side ───────────────────────────────────────────────────────
        [Header("Player Side")]
        [SerializeField] private CharacterThumbnailCard playerCard = null!;
        [SerializeField] private TextMeshProUGUI playerUsernameText = null!;
        [SerializeField] private TextMeshProUGUI playerRankText = null!;

        // ── Opponent Side ─────────────────────────────────────────────────────
        [Header("Opponent Side")]
        [SerializeField] private CharacterThumbnailCard opponentCard = null!;
        [SerializeField] private TextMeshProUGUI opponentUsernameText = null!;
        [SerializeField] private TextMeshProUGUI opponentRankText = null!;

        // ── Status / Hole / Rewards ───────────────────────────────────────────
        [Header("Status / Hole / Rewards")]
        [SerializeField] private TextMeshProUGUI statusText = null!;
        [SerializeField] private TextMeshProUGUI holeTitleText = null!;
        [SerializeField] private TextMeshProUGUI holeInfoText = null!;

        [SerializeField] private GameObject rewardRow1 = null!;
        [SerializeField] private Image reward1Icon = null!;
        [SerializeField] private TextMeshProUGUI reward1Amount = null!;

        [SerializeField] private GameObject rewardRow2 = null!;
        [SerializeField] private Image reward2Icon = null!;
        [SerializeField] private TextMeshProUGUI reward2Amount = null!;

        [SerializeField] private GameObject rewardRow3 = null!;
        [SerializeField] private Image reward3Icon = null!;
        [SerializeField] private TextMeshProUGUI reward3Amount = null!;

        [SerializeField] private Sprite pointsIcon = null!;
        [SerializeField] private Sprite repairKitIcon = null!;
        [SerializeField] private Sprite ballIcon = null!;

        // ── Cancel Button ─────────────────────────────────────────────────────
        [Header("Cancel Button")]
        [SerializeField] private Button cancelButton = null!;

        // ── Home Screen Elements (hidden while modal is open) ─────────────────
        [Header("Home Screen Elements")]
        [SerializeField] private GameObject homeNoticePanel = null!;
        [SerializeField] private GameObject homeNextHolePanel = null!;

        // ── Tunables ──────────────────────────────────────────────────────────
        [Header("Tunables")]
        [SerializeField] private float searchDurationSeconds = 5f;
        [SerializeField] private float opponentCycleIntervalSeconds = 0.3f;
        [SerializeField] private float dotCycleIntervalSeconds = 0.4f;
        [SerializeField] private string statusSearchingText = "FINDING OPPONENT";
        [SerializeField] private string statusFoundText = "OPPONENT FOUND";
        [SerializeField] private string[] fakeOpponentUsernames = new string[]
        {
            "GolfWar", "Birdie", "EagleEye", "ParBust",
            "GreenKng", "SwingMst", "AceShot", "FairPro"
        };
        [SerializeField] private Vector2Int fakeRankRange = new Vector2Int(50, 999);
        [SerializeField] private Vector2Int fakeOpponentLevelRange = new Vector2Int(1, 50);

        // ── Hole Source ───────────────────────────────────────────────────────
        [Header("Hole Source")]
        [SerializeField] private HoleDatabase? holeDatabase;
        [SerializeField] private int defaultHoleIndex = 0;

        // ── Private state ─────────────────────────────────────────────────────
        private Coroutine? _dotCycleCoroutine;
        private Coroutine? _opponentScanCoroutine;
        private List<CharacterDataRuntime> _opponentPool = new List<CharacterDataRuntime>();

        // Stage B: captured at Open(); read in OpponentScanRoutine to seed GameSession.
        private int       _resolvedIndex;
        private HoleData? _resolvedHoleData;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        protected override void Awake()
        {
            base.Awake(); // wires closeButton → Hide, hides modalPanel

            // Also wire cancelButton → closeButton for auto-wire pattern
            if (cancelButton != null && closeButton == null)
                closeButton = cancelButton;
        }

        protected override void OnShow()
        {
            // Hide home-screen elements that would show through the backdrop
            if (homeNoticePanel != null)   homeNoticePanel.SetActive(false);
            if (homeNextHolePanel != null) homeNextHolePanel.SetActive(false);

            _dotCycleCoroutine    = StartCoroutine(DotCycleRoutine());
            _opponentScanCoroutine = StartCoroutine(OpponentScanRoutine());
        }

        protected override void OnHide()
        {
            if (_dotCycleCoroutine != null)    { StopCoroutine(_dotCycleCoroutine);    _dotCycleCoroutine    = null; }
            if (_opponentScanCoroutine != null) { StopCoroutine(_opponentScanCoroutine); _opponentScanCoroutine = null; }

            if (statusText != null)
                statusText.text = string.Empty;

            // Restore home-screen elements that were hidden while modal was open
            if (homeNoticePanel != null)   homeNoticePanel.SetActive(true);
            if (homeNextHolePanel != null) homeNextHolePanel.SetActive(true);
        }

        private void OnDisable()
        {
            // Safety net: if modal is killed without going through Hide(),
            // ensure home-screen elements are never left stuck hidden.
            if (homeNoticePanel != null)   homeNoticePanel.SetActive(true);
            if (homeNextHolePanel != null) homeNextHolePanel.SetActive(true);
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Open the matchmaking modal using the defaultHoleIndex.
        /// </summary>
        public void Open()
        {
            Open(-1);
        }

        /// <summary>
        /// Open the matchmaking modal for the given hole index.
        /// Pass -1 to use defaultHoleIndex.
        /// </summary>
        public void Open(int holeIndex)
        {
            // 1. Resolve hole index (Stage B: lifted to field so OpponentScanRoutine can read it)
            _resolvedIndex = holeIndex >= 0 ? holeIndex : defaultHoleIndex;
            int resolvedIndex = _resolvedIndex;

            // 2. Resolve player character
            string playerCharId = CharacterManager.Instance?.GetSelectedCharacterId() ?? string.Empty;
            if (string.IsNullOrEmpty(playerCharId))
            {
                Debug.LogWarning("[MatchmakingModalController] No selected character — cannot open modal.");
                return;
            }

            var playerData = CharacterManager.Instance?.GetPlayerCharacter(playerCharId);
            if (playerData == null)
            {
                Debug.LogWarning($"[MatchmakingModalController] PlayerCharacterData for '{playerCharId}' not found — cannot open modal.");
                return;
            }

            // 3. Populate player card
            if (playerCard != null)
                playerCard.Initialize(playerCharId);

            // 4. Player username + rank (placeholder)
            if (playerUsernameText != null)
                playerUsernameText.text = "You";

            if (playerRankText != null)
                playerRankText.text = $"RANK: #{Random.Range(fakeRankRange.x, fakeRankRange.y + 1)}";

            // 5. Build opponent pool (all characters minus player's)
            _opponentPool.Clear();
            var allChars = CharacterDatabaseCSV.Instance?.GetAllCharacters();
            if (allChars != null)
            {
                foreach (var c in allChars)
                {
                    if (c.characterId != playerCharId)
                        _opponentPool.Add(c);
                }
                // Fallback: if only one character exists, include them
                if (_opponentPool.Count == 0)
                    _opponentPool.AddRange(allChars);
            }

            // 6. Resolve hole + populate
            HoleData? hole = null;
            if (holeDatabase != null)
                hole = holeDatabase.GetHole(resolvedIndex);

            if (hole == null && HoleDatabaseLoader.RuntimeDatabase != null)
                hole = HoleDatabaseLoader.GetHole(resolvedIndex);

            if (hole == null)
            {
                // Hardcoded stub
                hole = new HoleData("HOLE_LOMOND_5", 5);
                hole.AddReward(RewardType.Points,    10);
                hole.AddReward(RewardType.RepairKit, 10);
                hole.AddReward(RewardType.Ball,      10);
            }

            ApplyHole(hole);

            // Stage B: remember the resolved hole so OpponentScanRoutine can seed GameSession.
            _resolvedHoleData = hole;

            // Stage B fix (pre-existing bug exposed by Stage B smoke test): the
            // MatchMakingModal root GO is saved inactive in ShellScene (see
            // commit 49d16d36 "Hole Selection Fixes"). ModalController.Show()
            // activates the inner modalPanel but does NOT activate self — so
            // the panel stays masked by the inactive parent AND OnShow's
            // coroutines silently fail to start (StartCoroutine on an inactive
            // MonoBehaviour is a no-op). Activate self before Show().
            if (!gameObject.activeSelf) gameObject.SetActive(true);

            // 7. Show (kicks OnShow → coroutines)
            Show();
        }

        // ── Private Helpers ───────────────────────────────────────────────────

        private void ApplyHole(HoleData hole)
        {
            if (hole == null) return;

            if (holeTitleText != null)
                holeTitleText.text = LocalizationManager.Get("HOME_NEXT_HOLE");

            if (holeInfoText != null)
                holeInfoText.text = LocalizationManager.Get(hole.courseNameKey);

            for (int i = 0; i < 3; i++)
            {
                if (i < hole.rewards.Count)
                    SetupRewardRow(i, hole.rewards[i].type, hole.rewards[i].amount);
                else
                    HideRewardRow(i);
            }
        }

        private void SetupRewardRow(int rowIndex, RewardType rewardType, int amount)
        {
            GameObject? rowRoot = null;
            Image? icon = null;
            TextMeshProUGUI? amountLabel = null;

            switch (rowIndex)
            {
                case 0: rowRoot = rewardRow1; icon = reward1Icon; amountLabel = reward1Amount; break;
                case 1: rowRoot = rewardRow2; icon = reward2Icon; amountLabel = reward2Amount; break;
                case 2: rowRoot = rewardRow3; icon = reward3Icon; amountLabel = reward3Amount; break;
            }

            if (rowRoot == null) return;

            bool show = amount > 0;
            rowRoot.SetActive(show);
            if (!show) return;

            if (icon != null)
            {
                icon.sprite = rewardType switch
                {
                    RewardType.Points    => pointsIcon,
                    RewardType.RepairKit => repairKitIcon,
                    RewardType.Ball      => ballIcon,
                    _                    => null
                };
            }

            if (amountLabel != null)
                amountLabel.text = $"x{amount}";
        }

        private void HideRewardRow(int rowIndex)
        {
            GameObject? rowRoot = rowIndex switch
            {
                0 => rewardRow1,
                1 => rewardRow2,
                2 => rewardRow3,
                _ => null
            };

            if (rowRoot != null)
                rowRoot.SetActive(false);
        }

        // ── Coroutines ────────────────────────────────────────────────────────

        private IEnumerator DotCycleRoutine()
        {
            // Always render exactly 3 dot slots so the base phrase never
            // shifts horizontally. Unused dot positions use <alpha=#00> so
            // TMP still lays out the glyph width but renders it invisible.
            // Dot state 1: ".<alpha=#00>.."
            // Dot state 2: "..<alpha=#00>."
            // Dot state 3: "..."
            // The string width stays constant across all three states.
            const string INV = "<alpha=#00>.";    // invisible dot (TMP tag, no closing needed — next visible char resets alpha)
            int dotCount = 0;
            while (true)
            {
                dotCount = (dotCount % 3) + 1;
                if (statusText != null)
                {
                    string suffix;
                    switch (dotCount)
                    {
                        case 1:  suffix = "."  + INV + INV; break;
                        case 2:  suffix = ".." + INV;       break;
                        default: suffix = "...";            break;
                    }
                    statusText.text = statusSearchingText + suffix;
                }
                yield return new WaitForSeconds(dotCycleIntervalSeconds);
            }
        }

        private IEnumerator OpponentScanRoutine()
        {
            float elapsed = 0f;
            string lastPickId = string.Empty;

            while (elapsed < searchDurationSeconds)
            {
                if (_opponentPool.Count > 0)
                {
                    // Pick a random opponent, avoiding immediate repeat when pool >= 2
                    CharacterDataRuntime pick;
                    if (_opponentPool.Count >= 2)
                    {
                        CharacterDataRuntime candidate;
                        int attempts = 0;
                        do
                        {
                            candidate = _opponentPool[Random.Range(0, _opponentPool.Count)];
                            attempts++;
                        }
                        while (candidate.characterId == lastPickId && attempts < 10);
                        pick = candidate;
                    }
                    else
                    {
                        pick = _opponentPool[0];
                    }

                    lastPickId = pick.characterId;

                    int fakeLevel = Random.Range(fakeOpponentLevelRange.x, fakeOpponentLevelRange.y + 1);
                    if (opponentCard != null)
                        opponentCard.InitializeFromTemplate(pick.characterId, fakeLevel);

                    if (opponentUsernameText != null && fakeOpponentUsernames != null && fakeOpponentUsernames.Length > 0)
                        opponentUsernameText.text = fakeOpponentUsernames[Random.Range(0, fakeOpponentUsernames.Length)];

                    if (opponentRankText != null)
                        opponentRankText.text = $"RANK: #{Random.Range(fakeRankRange.x, fakeRankRange.y + 1)}";
                }

                yield return new WaitForSeconds(opponentCycleIntervalSeconds);
                elapsed += opponentCycleIntervalSeconds;
            }

            // Search complete: stop dot cycle, set final status
            if (_dotCycleCoroutine != null)
            {
                StopCoroutine(_dotCycleCoroutine);
                _dotCycleCoroutine = null;
            }

            if (statusText != null)
                statusText.text = statusFoundText;

            // Stage B: seed GameSession at OPPONENT FOUND so downstream
            // gameplay code (PhysicsLab, ShellScene Result modal, etc.) can
            // read the current hole / character / bag without re-resolving
            // singletons every frame.
            int seededHole = (_resolvedHoleData != null)
                                ? _resolvedHoleData.holeNumber
                                : (_resolvedIndex + 1);
            string charId = CharacterManager.Instance != null
                                ? CharacterManager.Instance.GetSelectedCharacterId()
                                : string.Empty;
            int bagSlot   = BagManager.Instance != null
                                ? BagManager.Instance.EquippedBagSlot
                                : 0;
            Golfin.Gameplay.Session.GameSession.SeedSession(seededHole, charId, bagSlot);
#if UNITY_EDITOR
            Debug.Log($"[Stage B] GameSession seeded — Hole={seededHole}, CharacterId='{charId}', BagSlot={bagSlot}");
#endif

            _opponentScanCoroutine = null;

            // Stage C0: brief beat on "OPPONENT FOUND" so the player registers it,
            // then hand off to GameplaySceneLoader. The loader hides THIS modal at the
            // FadeController midpoint (under the full-black overlay) so the modal and the
            // home backdrop fade out together — no more "background fades then modal pops"
            // staging.
            yield return new WaitForSeconds(0.6f);

            var loader = Golfin.UI.GameplayTransition.GameplaySceneLoader.Instance;
            if (loader != null)
            {
                loader.BeginGameplayLoad(seededHole, modalToHideOnMidpoint: this);
            }
            else
            {
                Debug.LogError("[MatchmakingModalController] GameplaySceneLoader.Instance is null — " +
                               "gameplay scene will not load. Verify GameplaySceneLoader is wired in ShellScene.");
                Hide();  // best-effort cleanup if loader is missing
            }
        }
    }
}
