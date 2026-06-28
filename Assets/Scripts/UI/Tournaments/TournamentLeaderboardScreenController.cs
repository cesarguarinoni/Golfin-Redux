using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using GolfinRedux.UI;
using Golfin.UI.Rankings;
using Golfin.Tournaments;

namespace GolfinRedux.UI.Tournaments
{
    /// <summary>
    /// Stage 2 controller for the Tournament Leaderboard screen.
    /// Navigation + title, plus live population of the podium / rows / sticky from
    /// TournamentService.Instance.Backend.GetLeaderboard(SelectedTournamentId).
    ///
    /// Binding strategy (per SPEC §3): reuse the existing
    /// Top3CardWidget / RankingsCardWidget for art + rarity, resolved via the
    /// same fake-player roster (LeaderboardManager.GetRanking(Daily)) that the
    /// normal Rankings screen uses. Override the score pill with strokes via SetStrokes().
    ///
    /// Live differences from Stage 1:
    ///   - PopulateBots replaced by PopulateLive(), sourced from GetLeaderboard(id)
    ///   - Art resolved by CharacterId match against the fake-player roster
    ///   - Sticky row: Rank shown as "--" while IsProvisional; uses entry.Strokes
    ///   - DNF entries are filtered from ranked rows (SPEC GDD §17.4)
    ///
    /// iter-2 fix (CESAR_REJECTION Defect 6):
    ///   - PopulateLive() now binds Banner/IdentityPillRow sponsor label and
    ///     tournament-name label from SelectedTournamentId → def.NameKey/SponsorKey.
    ///
    /// Deliberately does NOT inherit RankingsScreenController.
    /// </summary>
    public class TournamentLeaderboardScreenController : MonoBehaviour
    {
        [Header("Title")]
        [SerializeField] private TextMeshProUGUI _titleLabel;
        [SerializeField] private string _titleText = "TOURNAMENT LEADERBOARD";

        [Header("Navigation")]
        [Tooltip("Silver CLOSE button → back to Tournament Selection.")]
        [SerializeField] private Button _closeButton;

        [SerializeField] private ScreenId _backScreen = ScreenId.TournamentSelection;

        // Relative paths from this screen root to the reused Rankings hierarchy.
        private const string ModalPath       = "ContentArea/BarsArea/RankingsArea/Modal";

        // Banner pill paths (iter-2 — Defect 6 binding)
        private const string SponsorLabelPath   = "ContentArea/Banner/IdentityPillRow/Pill_SPONSO/Label";
        private const string TournNameLabelPath  = "ContentArea/Banner/IdentityPillRow/Row2/Pill_KASUMI/Label";

        // ── Fake-player roster (iter-3 Cesar): real bot identities ────────────
        // Bots carry CharacterId == BotId == fake_players.csv id (e.g. "fp_028").
        // We resolve that id → real username / characterId / level so the board
        // shows "GALADRIEL · RARE · Lv 80", not the raw "fp_028" placeholder.
        private System.Collections.Generic.Dictionary<string, Golfin.Tournaments.FakePlayerRow> _fakeById;
        private System.Collections.Generic.List<Golfin.Tournaments.FakePlayerRow> _fakeRows;

        private void EnsureFakeRoster()
        {
            if (_fakeById != null) return;
            _fakeById = new System.Collections.Generic.Dictionary<string, Golfin.Tournaments.FakePlayerRow>(System.StringComparer.Ordinal);
            _fakeRows = new System.Collections.Generic.List<Golfin.Tournaments.FakePlayerRow>();
            var ta = Resources.Load<TextAsset>("Data/fake_players");
            if (ta == null) { Debug.LogWarning("[TournamentLeaderboard] fake_players.csv not found at Resources/Data/fake_players."); return; }
            foreach (var row in Golfin.Tournaments.FakePlayerRosterParser.Parse(ta.text))
            {
                _fakeRows.Add(row);
                _fakeById[row.Id] = row;
            }
        }

        /// <summary>
        /// Resolve a tournament entry to a Rankings <see cref="LeaderboardEntry"/> with a real
        /// display name, character id (for portrait/rarity art) and level.
        /// Bots → fake_players.csv by BotId; player → roster "YOU" entry (real art/level).
        /// </summary>
        private LeaderboardEntry ResolveEntry(TournamentLeaderboardEntry te, IReadOnlyList<LeaderboardEntry> roster)
        {
            if (te.IsPlayer)
            {
                if (roster != null)
                {
                    foreach (var e in roster)
                        if (e.DisplayName == "YOU")
                            return e;
                }
                return new LeaderboardEntry { DisplayName = "YOU", CharacterId = te.CharacterId, Level = System.Math.Max(1, te.Level) };
            }

            EnsureFakeRoster();
            if (_fakeById != null && !string.IsNullOrEmpty(te.CharacterId)
                && _fakeById.TryGetValue(te.CharacterId, out var fp))
            {
                return new LeaderboardEntry { DisplayName = fp.Username, CharacterId = fp.CharacterId, Level = fp.Level };
            }

            // Fallback: deterministic cycle over the fake roster (keeps name/art/level consistent)
            if (_fakeRows != null && _fakeRows.Count > 0)
            {
                int hash = 0;
                string key = te.CharacterId ?? te.DisplayName ?? "x";
                foreach (char c in key) hash = hash * 31 + c;
                int idx = ((hash % _fakeRows.Count) + _fakeRows.Count) % _fakeRows.Count;
                var f = _fakeRows[idx];
                return new LeaderboardEntry { DisplayName = f.Username, CharacterId = f.CharacterId, Level = f.Level };
            }

            return new LeaderboardEntry { DisplayName = te.DisplayName, CharacterId = te.CharacterId, Level = System.Math.Max(1, te.Level) };
        }

        private void Awake()
        {
            if (_closeButton != null)
                _closeButton.onClick.AddListener(Close);
        }

        private void OnEnable()
        {
            if (_titleLabel != null && !string.IsNullOrEmpty(_titleText))
                _titleLabel.text = _titleText;

            PopulateLive();
        }

        private void Close()
        {
            ScreenManager.Instance?.ShowScreen(_backScreen);
        }

        // ── Live data fill ────────────────────────────────────────────────────

        private void PopulateLive()
        {
            // Guard: service + selected id
            if (TournamentService.Instance == null)
            {
                Debug.LogWarning("[TournamentLeaderboard] TournamentService not ready; falling back to empty board.");
                return;
            }

            string id = TournamentService.Instance.SelectedTournamentId;
            if (string.IsNullOrEmpty(id))
            {
                Debug.LogWarning("[TournamentLeaderboard] SelectedTournamentId is null/empty — normal nav always sets it.");
                return;
            }

            // ── iter-2: Bind header pills from SelectedTournamentId ───────────
            BindHeader(id);

            var board = TournamentService.Instance.Backend.GetLeaderboard(id);
            if (board == null || board.Count == 0)
            {
                Debug.Log(string.Format("[TournamentLeaderboard] GetLeaderboard({0}) returned empty board.", id));
                return;
            }

            var modal = transform.Find(ModalPath);
            if (modal == null)
            {
                Debug.LogError(string.Format("[TournamentLeaderboard] Could not find Modal at {0}.", ModalPath));
                return;
            }

            // ── Fake-player roster for art/rarity resolution ─────────────────
            // We reuse the same LeaderboardManager roster the Rankings screen uses.
            // The roster maps CharacterId → portrait + rarity; we just override Rank + strokes.
            IReadOnlyList<LeaderboardEntry> roster =
                LeaderboardManager.Instance != null
                    ? LeaderboardManager.Instance.GetRanking(LeaderboardPeriod.Daily)
                    : null;

            // ── Ranked entries (exclude DNF from display, SPEC GDD §17.4) ────
            var ranked = new List<TournamentLeaderboardEntry>();
            TournamentLeaderboardEntry? playerEntry = null;

            foreach (var e in board)
            {
                if (e.IsPlayer)
                {
                    playerEntry = e;
                    // DNF players still appear in sticky — exclude from main board.
                    if (e.IsDNF) continue;
                }
                if (!e.IsDNF)
                    ranked.Add(e);
            }

            // ── Podium #1 / #2 / #3 ──────────────────────────────────────────
            BindCard(modal.Find("Top3/Top1Card"), ranked, 0, roster, isPodium: true);
            BindCard(modal.Find("Top3/Top2Card"), ranked, 1, roster, isPodium: true);
            BindCard(modal.Find("Top3/Top3Card"), ranked, 2, roster, isPodium: true);

            // ── Ranking rows (#4+) ────────────────────────────────────────────
            var grid = modal.Find("Bottom97/ScrollArea/Viewport/GridContent");
            if (grid != null)
            {
                int entryIdx = 3;
                foreach (Transform row in grid)
                {
                    if (!row.name.StartsWith("TournamentRankingRow")) continue;
                    if (entryIdx < ranked.Count)
                        BindCard(row, ranked, entryIdx, roster, isPodium: false);
                    entryIdx++;
                }
            }

            // ── Sticky "you" row ─────────────────────────────────────────────
            var sticky = modal.Find("TournamentPlayerStickyRow");
            if (sticky != null && playerEntry.HasValue)
            {
                var pe = playerEntry.Value;
                // Resolve identity via fake roster / player roster
                LeaderboardEntry baseEntry = ResolveEntry(pe, roster);
                baseEntry.Rank  = pe.IsProvisional ? 0 : pe.Rank;
                baseEntry.IsTie = pe.IsTie;

                var widget = sticky.GetComponent<RankingsCardWidget>()
                          ?? sticky.gameObject.AddComponent<RankingsCardWidget>();
                widget.Bind(baseEntry);

                // Rank: "--" while provisional/unfinished
                var rankLabel = sticky.Find("RankingsCard/Rank")?.GetComponent<TextMeshProUGUI>();
                if (rankLabel != null)
                    rankLabel.text = pe.IsProvisional ? "--" : pe.Rank.ToString();

                // Cesar (iter-3): rarity/level row needs a dash → " - Lv {N}"
                OverrideRowLevelDash(sticky, baseEntry.Level);
                SetStrokes(sticky, pe.Strokes);
            }
        }

        // ── Header pill binding (iter-2 Defect 6) ────────────────────────────

        /// <summary>
        /// Binds the Banner sponsor pill and tournament-name pill to the
        /// tournament identified by <paramref name="tournId"/>.
        /// Sponsor: "{SPONSOR} PRESENTS" from def.SponsorKey (same format as SelectionCard).
        /// Name: localized via LocalizationManager.Get(def.NameKey).
        /// </summary>
        private void BindHeader(string tournId)
        {
            var defs = TournamentService.Instance?.Backend?.GetTournaments();
            TournamentDefinition def = default;
            bool found = false;
            if (defs != null)
            {
                foreach (var d in defs)
                {
                    if (string.Equals(d.Id, tournId, System.StringComparison.Ordinal))
                    {
                        def   = d;
                        found = true;
                        break;
                    }
                }
            }

            if (!found)
            {
                Debug.LogWarning(string.Format("[TournamentLeaderboard] Def not found for id={0}; header pills unchanged.", tournId));
                return;
            }

            // Sponsor label
            var sponsorLabel = transform.Find(SponsorLabelPath)?.GetComponent<TextMeshProUGUI>();
            if (sponsorLabel != null)
            {
                string sponsor = string.IsNullOrEmpty(def.SponsorKey)
                    ? "GOLFIN"
                    : def.SponsorKey.ToUpperInvariant();
                sponsorLabel.text = "SPONSORED BY " + sponsor;
                Debug.Log(string.Format("[TournamentLeaderboard] Header sponsor → '{0}'", sponsorLabel.text));
            }
            else
            {
                Debug.LogWarning(string.Format("[TournamentLeaderboard] Sponsor label not found at {0}", SponsorLabelPath));
            }

            // Tournament name label
            var nameLabel = transform.Find(TournNameLabelPath)?.GetComponent<TextMeshProUGUI>();
            if (nameLabel != null)
            {
                string localizedName = LocalizationManager.Get(def.NameKey);
                if (string.IsNullOrEmpty(localizedName) || localizedName == def.NameKey)
                    localizedName = def.NameKey; // fallback — key shown as-is
                nameLabel.text = localizedName.ToUpperInvariant();
                Debug.Log(string.Format("[TournamentLeaderboard] Header name → '{0}'", nameLabel.text));
            }
            else
            {
                Debug.LogWarning(string.Format("[TournamentLeaderboard] Name label not found at {0}", TournNameLabelPath));
            }
        }

        // ── Card binding ──────────────────────────────────────────────────────

        private void BindCard(
            Transform card,
            List<TournamentLeaderboardEntry> ranked,
            int index,
            IReadOnlyList<LeaderboardEntry> roster,
            bool isPodium)
        {
            if (card == null) return;
            if (ranked == null || index < 0 || index >= ranked.Count) return;

            var te = ranked[index];

            // Resolve real identity (name + character art + level) — bots via fake_players.csv
            LeaderboardEntry baseEntry = ResolveEntry(te, roster);
            baseEntry.Rank  = te.Rank;
            baseEntry.IsTie = te.IsTie;

            if (isPodium)
            {
                var w = card.GetComponent<Top3CardWidget>()
                     ?? card.gameObject.AddComponent<Top3CardWidget>();
                w.Bind(baseEntry);
                // Podium: stacked rarity/level, no dash, "Lv N" to match Figma reference.
                OverridePodiumLevel(card, baseEntry.Level);
            }
            else
            {
                var w = card.GetComponent<RankingsCardWidget>()
                     ?? card.gameObject.AddComponent<RankingsCardWidget>();
                w.Bind(baseEntry);
                // Rows: rarity + level on one line with a dash → " - Lv N".
                OverrideRowLevelDash(card, baseEntry.Level);
            }

            SetStrokes(card, te.Strokes);
        }

        // ── Level-label overrides (iter-3 Cesar — dash between rarity and level) ──

        /// <summary>Row / sticky level label → " - Lv {N}" (dash form, matches scene + Figma).</summary>
        private static void OverrideRowLevelDash(Transform card, int level)
        {
            var lbl = FindRowLevelLabel(card);
            if (lbl != null) lbl.text = " - Lv " + level;
        }

        /// <summary>Podium level label → "Lv {N}" (stacked, no dash, matches Figma).</summary>
        private static void OverridePodiumLevel(Transform card, int level)
        {
            var lbl = card.Find("Info/LevelLabel")?.GetComponent<TextMeshProUGUI>();
            if (lbl != null) lbl.text = "Lv " + level;
        }

        private static TextMeshProUGUI FindRowLevelLabel(Transform card)
        {
            Transform c = card.Find("RankingsCard") ?? card;
            // Prefab keeps a trailing space on the label name; try both spellings.
            var t = c.Find("Name+Level/Rarity+Level/LevelLabel ")
                 ?? c.Find("Name+Level/Rarity+Level/LevelLabel");
            return t != null ? t.GetComponent<TextMeshProUGUI>() : null;
        }

        // ── Strokes pill override ─────────────────────────────────────────────

        /// <summary>Override the reused RP score pill with the tournament STROKES total.</summary>
        private static void SetStrokes(Transform card, int strokes)
        {
            var label = card.Find("RewardPoints/Background/NameLabel")?.GetComponent<TextMeshProUGUI>()
                     ?? card.Find("RankingsCard/RewardPoints/Background/NameLabel")?.GetComponent<TextMeshProUGUI>();
            if (label != null)
                label.text = string.Format("{0} STROKES", strokes);
        }
    }
}
