using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GolfinRedux.UI;
using Golfin.Core.Stamina;
using Golfin.Gameplay.Session;
using Golfin.Roster;
using Golfin.Tournaments;
using Golfin.UI.GameplayTransition;

namespace GolfinRedux.UI.Tournaments
{
    /// <summary>
    /// T6 controller for the Tournament Hole Selection screen.
    /// Binds entry.PerHole sequentially to the three card visual templates
    /// (TournamentHoleCard_Finished / _Next / _Locked) already in the scene.
    /// Uses Instantiate to clone templates for each hole in def.HoleSet.
    /// "Next" card tap → BeginTournamentHole(holeId).
    ///
    /// Deliberately does NOT inherit HoleSelectionScreenController — that controller
    /// destroys the scroll content on OnEnable and rebuilds normal hole cards from the
    /// hole database, which would wipe the tournament cards.
    /// </summary>
    public class TournamentHoleSelectionScreenController : MonoBehaviour
    {
        [Header("Navigation")]
        [Tooltip("Podium icon (top-right) → Tournament Leaderboard.")]
        [SerializeField] private Button _podiumLeaderboardButton;

        [Tooltip("Silver CLOSE button → back to the upstream selection screen.")]
        [SerializeField] private Button _closeButton;

        [Tooltip("Where CLOSE returns to. T7 TournamentSelection screen.")]
        [SerializeField] private ScreenId _backScreen = ScreenId.TournamentSelection;

        [Header("Card templates (scene static GOs — cloned per hole)")]
        [Tooltip("Template for a completed hole (TournamentHoleCard_Finished).")]
        [SerializeField] private GameObject _finishedCardTemplate;

        [Tooltip("Template for the next playable hole (TournamentHoleCard_Next).")]
        [SerializeField] private GameObject _nextCardTemplate;

        [Tooltip("Template for a locked future hole (TournamentHoleCard_Locked).")]
        [SerializeField] private GameObject _lockedCardTemplate;

        [Header("Scroll container")]
        [Tooltip("Parent transform under CardsScrollView/Viewport/Content — cloned cards are placed here.")]
        [SerializeField] private RectTransform _cardsContent;

        [SerializeField] private ScrollRect _cardsScrollRect;

        // ── Runtime state ──────────────────────────────────────────────────────
        private readonly List<GameObject> _spawnedCards = new List<GameObject>();

        // ── Unity lifecycle ────────────────────────────────────────────────────

        private void Awake()
        {
            if (_podiumLeaderboardButton != null)
                _podiumLeaderboardButton.onClick.AddListener(OpenLeaderboard);

            if (_closeButton != null)
                _closeButton.onClick.AddListener(Close);
        }

        private void OnEnable()
        {
            StopAllCoroutines();
            StartCoroutine(RebuildNextFrame());

            // Same authored-placeholder ENDS IN pill as the leaderboard screen — see
            // TournamentCountdown. Started after StopAllCoroutines so it is not immediately killed.
            StartCoroutine(TournamentCountdown.Run(
                transform, TournamentCountdown.HoleSelectionPillPath,
                () => TournamentService.Instance?.SelectedTournamentId));
        }

        private void OnDisable()
        {
            ClearSpawnedCards();
        }

        // ── Card building ──────────────────────────────────────────────────────

        private IEnumerator RebuildNextFrame()
        {
            yield return null;
            RebuildCards();
        }

        private void RebuildCards()
        {
            ClearSpawnedCards();
            HideTemplates();

            if (_cardsContent == null)
            {
                Debug.LogError("[TournamentHoleSelection] _cardsContent not wired.");
                return;
            }

            // Prefer static Instance; fall back to scene search to handle
            // init-order edge cases (e.g. domain reload after Awake).
            var service = TournamentService.Instance
                ?? Object.FindFirstObjectByType<TournamentService>();
            if (service == null)
            {
                Debug.LogWarning("[TournamentHoleSelection] TournamentService not ready.");
                return;
            }

            string tournamentId = service.SelectedTournamentId;
            if (string.IsNullOrEmpty(tournamentId))
            {
                Debug.LogWarning("[TournamentHoleSelection] No SelectedTournamentId set.");
                return;
            }

            var backend = service.Backend;
            var def = backend.GetTournament(tournamentId);
            if (def == null)
            {
                Debug.LogError($"[TournamentHoleSelection] Tournament not found: {tournamentId}");
                return;
            }

            var entry = backend.GetMyEntry(tournamentId);
            int finishedCount = entry != null ? entry.PerHole.Count : 0;

            Debug.Log($"[TournamentHoleSelection] Binding {def.HoleSet.Count} holes; {finishedCount} finished.");

            for (int i = 0; i < def.HoleSet.Count; i++)
            {
                string holeId = def.HoleSet[i];

                GameObject template;
                bool isNext = false;
                if (i < finishedCount)
                    template = _finishedCardTemplate;
                else if (i == finishedCount)
                {
                    template = _nextCardTemplate;
                    isNext = true;
                }
                else
                    template = _lockedCardTemplate;

                if (template == null)
                {
                    Debug.LogWarning($"[TournamentHoleSelection] Template is null for hole index {i}; skipping.");
                    continue;
                }

                var card = Object.Instantiate(template, _cardsContent);
                card.SetActive(true);
                _spawnedCards.Add(card);

                // ── Bind hole label (TitleArea / TitleAreaExp) ──────────────
                BindHoleLabel(card, i + 1, holeId);

                // ── Wire the "Next" card tap to BeginTournamentHole ─────────
                if (isNext)
                {
                    string capturedHoleId = holeId; // capture for closure
                    // ActionButton is under ExpandedContainer
                    var actionBtn = card.transform.Find("ExpandedContainer/ActionButton")?.GetComponent<Button>();
                    if (actionBtn != null)
                    {
                        actionBtn.onClick.RemoveAllListeners();
                        actionBtn.onClick.AddListener(() => BeginTournamentHole(capturedHoleId));
                    }
                    // CardTapButton (the full card tap) also fires Begin
                    var tapBtn = card.transform.Find("CardTapButton")?.GetComponent<Button>();
                    if (tapBtn != null)
                    {
                        tapBtn.onClick.RemoveAllListeners();
                        tapBtn.onClick.AddListener(() => BeginTournamentHole(capturedHoleId));
                    }
                }
            }

            // Clones are appended to the end of _cardsContent, which would push the
            // static CloseButton above them. Re-pin CLOSE below all spawned cards.
            if (_closeButton != null)
                _closeButton.transform.SetAsLastSibling();

            if (_cardsScrollRect != null)
                _cardsScrollRect.verticalNormalizedPosition = 1f;
        }

        private void BindHoleLabel(GameObject card, int holeNumber, string holeId)
        {
            // Derive par from HoleDatabaseLoader (fallback to "-")
            string parStr = "-";
            var db = HoleDatabaseLoader.RuntimeDatabase;
            if (db != null)
            {
                var holeData = db.GetHole(holeNumber - 1);  // GetHole uses 0-based index
                if (holeData != null && holeData.par > 0)
                    parStr = holeData.par.ToString();
            }

            // Replace ALL TMP_Text components anywhere in the card whose text
            // contains "Hole" — covers Subtitle (collapsed) and SubtitleExp (expanded)
            // regardless of card template hierarchy depth.
            string newLabel = $"Lomond Country Club - Hole {holeNumber} - Par {parStr}";
            var allTexts = card.GetComponentsInChildren<TMP_Text>(true);
            foreach (var tmp in allTexts)
            {
                if (tmp.text.Contains("Hole"))
                    tmp.text = newLabel;
            }
        }

        private void HideTemplates()
        {
            if (_finishedCardTemplate != null) _finishedCardTemplate.SetActive(false);
            if (_nextCardTemplate != null)     _nextCardTemplate.SetActive(false);
            if (_lockedCardTemplate != null)   _lockedCardTemplate.SetActive(false);
        }

        private void ClearSpawnedCards()
        {
            foreach (var card in _spawnedCards)
            {
                if (card != null) Object.Destroy(card);
            }
            _spawnedCards.Clear();
        }

        // ── Round boot (T6 §6) ─────────────────────────────────────────────────

        /// <summary>
        /// Launches a tournament hole.
        /// holeId is a string from def.HoleSet (e.g. "lomond_1").
        /// The spec notes HoleSet may carry numeric indices or string IDs;
        /// GameplaySceneLoader.BeginGameplayLoad takes a 1-based int.
        /// For v1, parse the trailing number from holeId; fall back to 1.
        /// </summary>
        private void BeginTournamentHole(string holeId)
        {
            if (TournamentService.Instance == null)
            {
                Debug.LogError("[TournamentHoleSelection] TournamentService.Instance is null; cannot begin hole.");
                return;
            }

            string tournamentId = TournamentService.Instance.SelectedTournamentId;
            var backend = TournamentService.Instance.Backend;
            var def = backend?.GetTournament(tournamentId);
            if (def == null)
            {
                Debug.LogError($"[TournamentHoleSelection] Tournament definition null for id={tournamentId}.");
                return;
            }

            var entry = backend.GetMyEntry(tournamentId);
            if (entry == null)
            {
                Debug.LogError("[TournamentHoleSelection] No entry found; cannot begin tournament hole.");
                return;
            }

            // ── Derive hole number from def.HoleSet index (1-based for GameSession) ──
            // IReadOnlyList<string> has no IndexOf; use LINQ ToList or a manual search.
            int holeIndex = -1;
            for (int i = 0; i < def.HoleSet.Count; i++)
            {
                if (def.HoleSet[i] == holeId) { holeIndex = i; break; }
            }
            int holeNumber = holeIndex >= 0 ? (holeIndex + 1) : 1;

            // ── Gear: character from snapshot (locked), ball/club from live equip ──
            string charId = entry.Snapshot != null
                ? entry.Snapshot.CharacterId
                : (CharacterManager.Instance != null ? CharacterManager.Instance.GetSelectedCharacterId() : string.Empty);

            int bagSlot = BagManager.Instance != null ? BagManager.Instance.EquippedBagSlot : 0;

            Debug.Log($"[TournamentHoleSelection] BeginTournamentHole id={holeId} holeNum={holeNumber} char={charId} bag={bagSlot}");

            // ── Set tournament flags ───────────────────────────────────────────
            GameSession.IsTournament = true;
            GameSession.TournamentId = tournamentId;

            // ── BeginRound: seed pool from the persisted entry (Phase 3) ─────────
            // D3 = NO regen: remaining seeded straight from entry.ConditionRemaining — no
            // time-based refill, no LastHoleUtc/Recovery read (clock-trust-free model).
            // D2: sentinel -1f means "unseeded" → hydrate to full = MaxCondition(snapshot.Stamina).
            if (entry.Snapshot != null)
            {
                float tank      = StaminaModel.IsConfigured
                                  ? StaminaModel.MaxCondition(entry.Snapshot.Stamina)
                                  : TournamentRoundContext.DefaultStaminaMax;
                float remaining = entry.ConditionRemaining < 0f
                                  ? tank                                    // sentinel = full (D2)
                                  : Mathf.Min(tank, entry.ConditionRemaining);
                // D3 = NO: tournament pool does not regen — seed straight from the persisted value.
                TournamentRoundContext.BeginRound(tournamentId, entry.Snapshot, tank, remaining);
            }
            else
                Debug.LogWarning("[TournamentHoleSelection] entry.Snapshot is null; tournament stat seam will fall back to live stats.");

            // ── Seed session (character locked to snapshot; gear live) ────────
            GameSession.SeedSession(holeNumber, charId, bagSlot);

            // ── Launch gameplay ───────────────────────────────────────────────
            var loader = GameplaySceneLoader.Instance;
            if (loader != null)
            {
                loader.BeginGameplayLoad(holeNumber);
            }
            else
            {
                Debug.LogError("[TournamentHoleSelection] GameplaySceneLoader.Instance is null — " +
                               "gameplay scene will not load.");
            }
        }

        // ── Navigation ─────────────────────────────────────────────────────────

        private void OpenLeaderboard()
        {
            ScreenManager.Instance?.ShowScreen(ScreenId.TournamentLeaderboard);
        }

        private void Close()
        {
            ScreenManager.Instance?.ShowScreen(_backScreen);
        }
    }
}
