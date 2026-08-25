#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using Golfin.Gameplay.Session;
using Golfin.Tournaments;
using Golfin.UI.GameplayTransition;
using GolfinRedux.UI;

namespace GolfinRedux.UI.Tournaments
{
    /// <summary>
    /// ShellScene-resident handler for tournament hole completion (T6).
    ///
    /// Mirrors VersusResultHandler / HoleCompleteModalController pattern:
    ///   HoleCompletionBridge (Golfin.Physics.Viewer) fires
    ///   GameSession.OnTournamentHoleComplete(holeNumber, strokes)
    ///   → this handler submits HoleResult to TournamentService backend
    ///   → routes to HoleSelection (InProgress) or Leaderboard (Finished).
    ///
    /// This handler lives in Assembly-CSharp so it can call TournamentService and
    /// ScreenManager, which are both in Assembly-CSharp/GolfinRedux.UI, without
    /// crossing the asmdef boundary into Golfin.Physics.Viewer.
    ///
    /// Place on a persistent ShellScene GameObject (e.g. TournamentSession or
    /// the same [Session] root as VersusResultHandler).
    /// </summary>
    public class TournamentRoundHandler : MonoBehaviour
    {
        // ── Settings ──────────────────────────────────────────────────────────

        [Tooltip("Delay in seconds before routing after a hole completes. " +
                 "Gives the hole-out animation time to finish.")]
        [SerializeField] private float _postHoleDelaySeconds = 2.0f;

        // ── State ─────────────────────────────────────────────────────────────

        // Capture tee-off time so we can compute TimeSeconds for HoleResult.
        private DateTime _holeStartUtc = DateTime.MinValue;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void OnEnable()
        {
            GameSession.OnTournamentHoleComplete += HandleTournamentHoleComplete;
            GameSession.OnTurnChanged            += OnTurnChanged;
        }

        private void OnDisable()
        {
            GameSession.OnTournamentHoleComplete -= HandleTournamentHoleComplete;
            GameSession.OnTurnChanged            -= OnTurnChanged;
        }

        // ── Hole start tracking ───────────────────────────────────────────────

        private void OnTurnChanged()
        {
            // TurnCount resets to 1 at hole start. Use that as the tee-off moment.
            if (GameSession.TurnCount == 1)
                _holeStartUtc = DateTime.UtcNow;
        }

        // ── Tournament hole complete ───────────────────────────────────────────

        private void HandleTournamentHoleComplete(int holeNumber, int totalStrokes)
        {
            Debug.Log($"[TournamentRoundHandler] Hole complete holeNumber={holeNumber} strokes={totalStrokes}");

            if (TournamentService.Instance == null)
            {
                Debug.LogError("[TournamentRoundHandler] TournamentService.Instance is null; cannot submit result.");
                return;
            }

            string tournamentId = GameSession.TournamentId ?? TournamentRoundContext.TournamentId;
            if (string.IsNullOrEmpty(tournamentId))
            {
                Debug.LogError("[TournamentRoundHandler] TournamentId is empty; cannot submit result.");
                return;
            }

            // Derive holeId from the tournament definition (parallel to BeginTournamentHole logic).
            var backend = TournamentService.Instance.Backend;
            var def = backend.GetTournament(tournamentId);
            string holeId = def != null && holeNumber >= 1 && holeNumber <= def.HoleSet.Count
                ? def.HoleSet[holeNumber - 1]
                : $"hole_{holeNumber}";    // fallback if def unavailable

            // ── Build HoleResult (v1 minimal: rngSeed=0, inputLog=empty) ────
            float timeSeconds = _holeStartUtc != DateTime.MinValue
                ? (float)(DateTime.UtcNow - _holeStartUtc).TotalSeconds
                : 0f;

            var result = new HoleResult(
                holeId:       holeId,
                strokes:      totalStrokes,
                timeSeconds:  timeSeconds,
                completedUtc: DateTime.UtcNow,
                rngSeed:      0,          // TODO server-replay: pull from physics sim seed
                inputLog:     new List<ShotCommand>());  // TODO server-replay: capture shots

            // ── Submit to backend ─────────────────────────────────────────────
            EntryState? updatedEntry;
            try
            {
                updatedEntry = backend.SubmitHoleResult(tournamentId, result);
            }
            catch (Exception e)
            {
                Debug.LogError($"[TournamentRoundHandler] SubmitHoleResult threw: {e.Message}");
                // Don't block the player — treat as InProgress and return to hole selection.
                updatedEntry = null;
            }

            // ── Clear per-hole tournament flag (stays active for context) ─────
            // IsTournament = false between holes so the hole-select screen knows
            // no hole is currently being played. Re-set on next BeginTournamentHole.
            GameSession.IsTournament = false;

            // ── Route ─────────────────────────────────────────────────────────
            bool finished = updatedEntry != null && updatedEntry.Status == EntryStatus.Finished;

            if (finished)
            {
                TournamentRoundContext.EndRound();
                Debug.Log("[TournamentRoundHandler] Tournament FINISHED → routing to Leaderboard.");
                StartCoroutine(RouteAfterDelay(ScreenId.TournamentLeaderboard));
            }
            else
            {
                Debug.Log("[TournamentRoundHandler] Hole done (InProgress) → routing to HoleSelection.");
                StartCoroutine(RouteAfterDelay(ScreenId.TournamentHoleSelection));
            }
        }

        // ── Deferred routing (yields to let hole-out animation finish) ────────

        private System.Collections.IEnumerator RouteAfterDelay(ScreenId target)
        {
            // ExitToScreen, not UnloadGameplay + ShowScreen: the unload takes several frames
            // and the shell scene behind it is empty, so tearing down and swapping in the
            // open showed the bare camera clear between the hole and the target screen.
            var loader = GameplaySceneLoader.Instance;
            if (loader != null)
            {
                yield return loader.ExitToScreen(target);
            }
            else
            {
                Debug.LogWarning("[TournamentRoundHandler] GameplaySceneLoader.Instance is null; " +
                                 "gameplay scenes may not unload.");
                yield return new WaitForSeconds(_postHoleDelaySeconds);
                ScreenManager.Instance?.ShowScreen(target);
            }
        }
    }
}
