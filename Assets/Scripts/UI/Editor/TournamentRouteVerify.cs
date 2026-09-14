// ─────────────────────────────────────────────────────────────────────────────
// TournamentRouteVerify — finished_tournament_leaderboard_route (2026-09-14)
//
// Cesar: "Entering Leaderboard in a finished Tournament from Tournament select screen goes
// to Play Hole. You should not be able to play in finished tournaments."
//
// Drives the REAL path in play mode and writes a PASS/FAIL verdict JSON:
//   boot → StartButton → Home → bottom-nav TEE → ModeSelection → the Tournaments mode card's
//   PLAY → TournamentSelection → a card's own CTA button.onClick.
//
//   A1  LEADERBOARD on an Ended card (never entered) → TournamentLeaderboard
//   A2  the board's real CloseButton (the controller's own _closeButton) → TournamentSelection.
//       An EMPTY board hides it (finding F1 below), so the route continues through the real
//       bottom-nav TEE instead and A2 is recorded as a finding, not a route failure.
//   — then the reported state is CREATED: Register() on the local backend has no time guard, so
//     an entry is planted on that ended tournament (a player who entered, then the tournament
//     ended with holes left — SPEC §2 Row 4, EnteredFinished), plus one synthetic LIVE
//     tournament in memory for the positive control —
//   A3  LEADERBOARD on the EnteredFinished card → TournamentLeaderboard (the bug as reported)
//   A4  the board's CloseButton → TournamentSelection
//   B1  hole selection opened by a direct ShowScreen with the finished tournament selected (what
//       the stale route did) spawns NO TournamentHoleCard_Next clone — every hole binds Locked
//   B2  BeginTournamentHole (private, via reflection) refuses through the IsPlayable guard:
//       "[TournamentHoleSelection] Refusing" logged, no gameplay load
//   C1  CONTINUE on the live entered tournament → TournamentHoleSelection with ONE Next card
//   Z   both planted entries removed from the save again (the dev profile is left as found)
//
// Findings (recorded in the JSON under "findings", outside the route verdict):
//   F1  the board's TournamentCloseButton lives in Bottom97/ScrollArea/…/CloseSlot and
//       ApplyBoardChrome deactivates the whole ScrollArea when rankedCount == 0 — an empty board
//       (every ENDED tournament with no finishers) has no CLOSE; only the nav bar leaves it.
//   F2  TournamentService.EnsureBackendForSession keeps the FIRST RemoteTournamentBackend
//       (`_remoteBackend ??=`), whose `_local` is readonly, so a schedule applied AFTER sign-in
//       never reaches the wrapper the session plays on (this bot swaps the field to keep going).
//
// Menu: GOLFIN ▸ Tournaments ▸ Verify — finished tournament routes (no video)
// Output: Docs/Specs/Quick/media/finished_tournament_leaderboard_route/route_verify.{json,log}
//         + stills through CaptureCore.SnapPlayModeSafe (existence + md5 logged — the
//         phantom-path lesson).
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Golfin.Diagnostics.Runtime;
using Golfin.Save;
using Golfin.Tournaments;
using GolfinRedux.UI;
using GolfinRedux.UI.Tournaments;

namespace GolfinRedux.UI.Editor
{
    public static class TournamentRouteVerify
    {
        public const string OutDir   = "Docs/Specs/Quick/media/finished_tournament_leaderboard_route";
        const string ArmedKey        = "TournamentRouteVerify.Armed";

        [InitializeOnLoadMethod]
        static void RegisterHook()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("GOLFIN/Tournaments/Verify — finished tournament routes (no video)")]
        public static void Launch()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[TournamentRouteVerify] Already playing — stop first."); return; }
            Directory.CreateDirectory(OutDir);
            SessionState.SetBool(ArmedKey, true);
            EditorApplication.EnterPlaymode();
            Debug.Log("[TournamentRouteVerify] Armed. Entering play mode...");
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(ArmedKey, false)) return;
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            SessionState.SetBool(ArmedKey, false);
            var host = new GameObject("[TournamentRouteVerifyRunner]");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<TournamentRouteVerifyRunner>().Begin(Time.realtimeSinceStartup);
        }
    }

    public class TournamentRouteVerifyRunner : MonoBehaviour
    {
        const string LiveId = "route_verify_live";
        const BindingFlags Priv = BindingFlags.NonPublic | BindingFlags.Instance;

        readonly StringBuilder _log = new StringBuilder();
        readonly List<(string id, bool pass, string detail)> _checks = new List<(string, bool, string)>();
        readonly List<(string id, string detail)> _findings = new List<(string, string)>();
        readonly List<string> _warnings = new List<string>();
        readonly List<string> _stills = new List<string>();
        readonly List<string> _planted = new List<string>();
        float _t0;
        string _note = "";

        public void Begin(float t0)
        {
            _t0 = t0;
            Application.runInBackground = true;   // an unfocused Editor stops rendering otherwise
            Application.logMessageReceived += OnLog;
            StartCoroutine(Run());
        }

        void OnDestroy() => Application.logMessageReceived -= OnLog;

        void OnLog(string condition, string stack, LogType type)
        {
            if (condition.Contains("[TournamentHoleSelection]") || condition.Contains("[TournamentSelectionScreen]"))
                _warnings.Add(condition);
        }

        float Now => Time.realtimeSinceStartup - _t0;
        void Line(string s) { _log.AppendLine($"[{Now,6:0.0}s] {s}"); Debug.Log("[TournamentRouteVerify] " + s); }
        void Check(string id, bool pass, string detail) { _checks.Add((id, pass, detail)); Line($"  {(pass ? "PASS" : "FAIL")} {id}: {detail}"); }
        void Finding(string id, string detail) { _findings.Add((id, detail)); Line($"  FINDING {id}: {detail}"); }

        IEnumerator Run()
        {
            Line($"=== finished tournament routes {DateTime.UtcNow:u} ===");
            yield return Boot();
            if (ScreenManager.Instance == null) { _note = "no ScreenManager"; yield return Finish(); yield break; }

            // ── Home → TEE (bottom nav) → Tournaments mode card PLAY ─────────────────────
            yield return TapNamed("NavTeeButton", ScreenId.ModeSelection, "bottom-nav TEE");
            if (ScreenManager.Instance.CurrentScreen != ScreenId.ModeSelection) { _note = "ModeSelection not reached"; yield return Finish(); yield break; }
            yield return ModeCardPlayTournaments();
            if (ScreenManager.Instance.CurrentScreen != ScreenId.TournamentSelection) { _note = "TournamentSelection not reached"; yield return Finish(); yield break; }
            yield return SettleSelection();

            // ── the cards, as found ──────────────────────────────────────────────────────
            var cards = LogCards("as found");
            TournamentSelectionCard? ended = null;
            foreach (var c in cards) if (c.State == TournamentSelectionCard.CardState.Ended) { ended = c; break; }
            if (ended == null) { _note = "no Ended card to test"; Check("A0", false, _note); yield return Finish(); yield break; }
            string endedId = ended.TournamentId;

            // A1 — LEADERBOARD on the Ended card (never entered)
            yield return TapCta(ended, "A1", ScreenId.TournamentLeaderboard, "LEADERBOARD on ended (not entered)");
            yield return Still("A1_leaderboard_from_ended_card");
            // A2 — the board's own close button (hidden on an empty board: F1)
            yield return LeaveBoard("A2");

            // ── create the reported state: entered, then the tournament ended ───────────
            var svc = TournamentService.Instance;
            var backend = svc != null ? svc.Backend : null;
            if (svc == null || backend == null) { _note = "no TournamentService"; yield return Finish(); yield break; }
            string charId = Golfin.Roster.CharacterManager.Instance != null
                ? Golfin.Roster.CharacterManager.Instance.GetSelectedCharacterId() : string.Empty;

            if (backend.GetMyEntry(endedId) == null)
            {
                var e = backend.Register(endedId, 0L, charId);
                if (e != null) { _planted.Add(endedId); Line($"planted an InProgress entry on ended '{endedId}' (Row 4 → EnteredFinished)"); }
                else Line($"WARN Register returned null for '{endedId}'");
            }
            else Line($"'{endedId}' already has an entry in this profile — not planting, not removing");

            bool liveOk = InjectLiveTournament(svc, backend, endedId);
            if (liveOk && backend.GetMyEntry(LiveId) == null)
            {
                var e = svc.Backend.Register(LiveId, 0L, charId);
                if (e != null) { _planted.Add(LiveId); Line($"planted an InProgress entry on live '{LiveId}' (Row 2 → EnteredActive)"); }
            }

            // rebuild the selection the way a player would: the mode card again
            if (ScreenManager.Instance.CurrentScreen != ScreenId.ModeSelection)
                yield return TapNamed("NavTeeButton", ScreenId.ModeSelection, "bottom-nav TEE");
            yield return ModeCardPlayTournaments();
            yield return SettleSelection();
            cards = LogCards("after planting");

            TournamentSelectionCard? finishedCard = null;
            bool liveListed = false;      // a bool, not the card: the selection rebuilds (destroys) its cards on every visit
            foreach (var c in cards)
            {
                if (c.TournamentId == endedId) finishedCard = c;
                if (c.TournamentId == LiveId) liveListed = true;
            }
            Check("S1", finishedCard != null && finishedCard.State == TournamentSelectionCard.CardState.EnteredFinished,
                  $"'{endedId}' after planting: state={(finishedCard != null ? finishedCard.State.ToString() : "<no card>")} (wanted EnteredFinished)");

            // A3 — the bug as reported: LEADERBOARD on the entered, finished tournament
            if (finishedCard != null)
            {
                yield return TapCta(finishedCard, "A3", ScreenId.TournamentLeaderboard, "LEADERBOARD on entered+finished");
                yield return Still("A3_leaderboard_from_finished_entered_card");
                yield return ClaimResultModal();          // the presenter's rank modal for the planted entry
                yield return LeaveBoard("A4");
            }

            // ── B — the adversarial half: hole selection with the finished tournament selected ──
            if (ScreenManager.Instance.CurrentScreen != ScreenId.TournamentSelection)
            {
                if (ScreenManager.Instance.CurrentScreen != ScreenId.ModeSelection)
                    yield return TapNamed("NavTeeButton", ScreenId.ModeSelection, "bottom-nav TEE");
                yield return ModeCardPlayTournaments();
                yield return SettleSelection();
            }
            ScreenId cameFrom = ScreenManager.Instance.CurrentScreen;
            svc.SelectedTournamentId = endedId;
            Line($"adversarial: ShowScreen(TournamentHoleSelection) with '{endedId}' selected — what the stale route did (from {cameFrom})");
            ScreenManager.Instance.ShowScreen(ScreenId.TournamentHoleSelection);
            yield return WaitScreen(ScreenId.TournamentHoleSelection, 6f);
            yield return new WaitForSecondsRealtime(1.5f);       // RebuildNextFrame + layout
            yield return DismissScreenHints();
            var holeScreen = ScreenObject("TournamentHoleSelectionScreen");
            var ctrl = holeScreen != null ? holeScreen.GetComponentInChildren<TournamentHoleSelectionScreenController>(true) : null;
            var counts = HoleCardCounts(holeScreen);
            Check("B1", ctrl != null && counts.next == 0 && (counts.locked + counts.finished) > 0,
                  $"hole cards for finished '{endedId}': next={counts.next} locked={counts.locked} finished={counts.finished}");
            yield return Still("B1_hole_selection_finished_tournament");

            // B2 — BeginTournamentHole refuses through the guard (private; reflection is the adversary's shortcut)
            if (ctrl != null)
            {
                var def = backend.GetTournament(endedId);
                string holeId = def != null && def.HoleSet.Count > 0 ? def.HoleSet[0] : "1";
                int warningsBefore = _warnings.Count;
                Golfin.Gameplay.Session.GameSession.IsTournament = false;
                var m = typeof(TournamentHoleSelectionScreenController).GetMethod("BeginTournamentHole", Priv);
                if (m == null) Check("B2", false, "BeginTournamentHole not found by reflection");
                else
                {
                    m.Invoke(ctrl, new object[] { holeId });
                    yield return new WaitForSecondsRealtime(1.5f);
                    bool refused = false;
                    for (int i = warningsBefore; i < _warnings.Count; i++)
                        if (_warnings[i].Contains("Refusing to begin hole")) refused = true;
                    bool stillHere = ScreenManager.Instance != null && ScreenManager.Instance.CurrentScreen == ScreenId.TournamentHoleSelection;
                    Check("B2", refused && stillHere && !Golfin.Gameplay.Session.GameSession.IsTournament,
                          $"BeginTournamentHole('{holeId}') on finished '{endedId}': refusedLog={refused} stillOnHoleSelection={stillHere} GameSession.IsTournament={Golfin.Gameplay.Session.GameSession.IsTournament}");
                }
            }
            yield return CloseScreen<TournamentHoleSelectionScreenController>("TournamentHoleSelectionScreen", "B3", cameFrom);

            // ── C — positive control: the live entered tournament still opens its Next card ────
            if (liveListed)
            {
                if (ScreenManager.Instance.CurrentScreen != ScreenId.TournamentSelection)
                {
                    if (ScreenManager.Instance.CurrentScreen != ScreenId.ModeSelection)
                        yield return TapNamed("NavTeeButton", ScreenId.ModeSelection, "bottom-nav TEE");
                    yield return ModeCardPlayTournaments();
                    yield return SettleSelection();
                }
                cards = SceneCards();
                TournamentSelectionCard? live = null;
                foreach (var c in cards) if (c.TournamentId == LiveId) { live = c; break; }
                if (live == null) Check("C1", false, "live card lost after rebuild");
                else
                {
                    Check("C0", live.State == TournamentSelectionCard.CardState.EnteredActive,
                          $"'{LiveId}': state={live.State} cta='{CtaLabel(live)}' (wanted EnteredActive / CONTINUE)");
                    yield return TapCta(live, "C1", ScreenId.TournamentHoleSelection, "CONTINUE on live entered");
                    yield return new WaitForSecondsRealtime(1.5f);
                    yield return DismissScreenHints();
                    var hs = ScreenObject("TournamentHoleSelectionScreen");
                    var lc = HoleCardCounts(hs);
                    Check("C2", ScreenManager.Instance.CurrentScreen == ScreenId.TournamentHoleSelection && lc.next == 1,
                          $"hole cards for live '{LiveId}': next={lc.next} locked={lc.locked} finished={lc.finished} (wanted next=1)");
                    yield return Still("C1_hole_selection_live_tournament");
                    yield return CloseScreen<TournamentHoleSelectionScreenController>("TournamentHoleSelectionScreen", "C3", ScreenId.TournamentSelection);
                }
            }
            else Line($"no '{LiveId}' card — positive control not run (liveOk={liveOk})");

            yield return Finish();
        }

        // ── the reported state, created ──────────────────────────────────────

        /// <summary>
        /// One synthetic LIVE tournament, in memory only: a copy of the ended one with a window
        /// around now, appended to the current definitions and applied through the service's own
        /// private Apply(schedule, source) — the same path a server fetch takes. Nothing touches
        /// the disk cache; play-mode exit discards it.
        /// </summary>
        bool InjectLiveTournament(TournamentService svc, ITournamentBackend backend, string templateId)
        {
            try
            {
                var tpl = backend.GetTournament(templateId);
                if (tpl == null) { Line("WARN no template definition"); return false; }
                var live = new TournamentDefinition(
                    LiveId, tpl.NameKey, tpl.ClubId, tpl.HoleSet,
                    DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddDays(1),
                    tpl.ResolveDelayMinutes, 0L, tpl.PrizeTableId, tpl.BotFieldId, tpl.SponsorKey, tpl.LeagueKey,
                    title: "ROUTE VERIFY LIVE");
                var defs = new List<TournamentDefinition>(backend.GetTournaments()) { live };
                var prizesField = typeof(TournamentService).GetField("_prizeTables", Priv);
                var prizes = prizesField?.GetValue(svc) as IReadOnlyDictionary<string, PrizeTable>;
                if (prizes == null) { Line("WARN _prizeTables not readable"); return false; }
                var schedule = new TournamentSchedule(defs, prizes, DateTime.UtcNow.ToString("u"));
                var apply = typeof(TournamentService).GetMethod("Apply", Priv, null,
                    new[] { typeof(TournamentSchedule), typeof(ScheduleSource) }, null);
                if (apply == null) { Line("WARN TournamentService.Apply not found"); return false; }
                apply.Invoke(svc, new object[] { schedule, ScheduleSource.DiskCache });
                int seen = svc.Backend.GetTournaments().Count;
                Line($"injected live '{LiveId}' ({live.StartUtc:u} → {live.EndUtc:u}) through TournamentService.Apply; Backend sees {seen} defs (schedule has {defs.Count})");
                if (seen != defs.Count && svc.Backend is RemoteTournamentBackend remote)
                {
                    // F2 — the session's RemoteTournamentBackend still wraps the local backend it
                    // was built with. Swap in the freshly composed one so the control can run.
                    var localField = typeof(TournamentService).GetField("_localBackend", Priv);
                    var wrapped    = typeof(RemoteTournamentBackend).GetField("_local", Priv);
                    var fresh      = localField?.GetValue(svc) as LocalTournamentBackend;
                    if (fresh != null && wrapped != null)
                    {
                        wrapped.SetValue(remote, fresh);
                        Line($"  swapped RemoteTournamentBackend._local to the freshly composed backend; Backend now sees {svc.Backend.GetTournaments().Count} defs");
                    }
                    Finding("F2", $"a schedule applied after sign-in did not reach the session's RemoteTournamentBackend (Backend saw {seen} of {defs.Count} defs) — TournamentService.EnsureBackendForSession keeps the first wrapper (`_remoteBackend ??=`) and RemoteTournamentBackend._local is readonly");
                }
                return svc.Backend.GetTournament(LiveId) != null;
            }
            catch (Exception e) { Line("WARN InjectLiveTournament: " + e.Message); return false; }
        }

        /// <summary>Remove the planted rows from the save so the dev profile is left as found.</summary>
        void RemovePlantedEntries()
        {
            if (_planted.Count == 0) { Line("nothing planted — save untouched"); return; }
            try
            {
                var host = SaveDataHost.Instance;
                var rows = host != null ? host.Data.tournamentEntries : null;
                if (rows == null) { Check("Z", false, "SaveDataHost/tournamentEntries unavailable — planted entries NOT removed: " + string.Join(", ", _planted)); return; }
                int removed = rows.RemoveAll(r => _planted.Contains(r.tournamentId));
                host!.MarkDirty();
                bool gone = true;
                var backend = TournamentService.Instance?.Backend;
                foreach (var id in _planted) if (backend != null && backend.GetMyEntry(id) != null) gone = false;
                Check("Z", removed == _planted.Count && gone, $"removed {removed}/{_planted.Count} planted entries ({string.Join(", ", _planted)}); backend sees none={gone}");
            }
            catch (Exception e) { Check("Z", false, "RemovePlantedEntries: " + e.Message); }
        }

        // ── steps ────────────────────────────────────────────────────────────

        IEnumerator Boot()
        {
            float deadline = Time.realtimeSinceStartup + 60f;
            while (ScreenManager.Instance == null && Time.realtimeSinceStartup < deadline) yield return null;
            deadline = Time.realtimeSinceStartup + 90f;
            while (Time.realtimeSinceStartup < deadline)
            {
                bool tapped = false;
                foreach (Button b in FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                    if (b.name == "StartButton" && b.gameObject.activeInHierarchy)
                    { Line("tapping the real StartButton"); b.onClick.Invoke(); tapped = true; break; }
                if (tapped) break;
                yield return new WaitForSecondsRealtime(0.5f);
            }
            yield return WaitScreen(ScreenId.Home, 20f);
            yield return new WaitForSecondsRealtime(1.5f);
            yield return DismissScreenHints();
        }

        IEnumerator SettleSelection()
        {
            yield return new WaitForSecondsRealtime(2.0f);      // cards + a possible remote repaint
            yield return DismissScreenHints();
            yield return new WaitForSecondsRealtime(1.0f);
        }

        IEnumerator TapNamed(string buttonName, ScreenId target, string what)
        {
            Button? b = FindActiveButton(buttonName);
            if (b == null) { Line($"MISSING {what}: no active button '{buttonName}'"); yield break; }
            Line($"tapping the real {buttonName}.onClick ({what})");
            b.onClick.Invoke();
            yield return WaitScreen(target, 8f);
            yield return new WaitForSecondsRealtime(1.0f);
            yield return DismissScreenHints();
        }

        IEnumerator TapCta(TournamentSelectionCard card, string checkId, ScreenId target, string what)
        {
            Button? cta = ActiveCta(card);
            if (cta == null) { Check(checkId, false, $"{what}: card '{card.TournamentId}' has no active CTA button"); yield break; }
            Line($"tapping the real {cta.name}.onClick ('{CtaLabel(card)}') on '{card.TournamentId}' ({card.State})");
            cta.onClick.Invoke();
            yield return WaitScreen(target, 6f);
            ScreenId landed = ScreenManager.Instance!.CurrentScreen;
            Check(checkId, landed == target,
                  $"{what} '{card.TournamentId}' landed on {landed} (wanted {target}; SelectedTournamentId={TournamentService.Instance?.SelectedTournamentId})");
            yield return new WaitForSecondsRealtime(2.0f);
            yield return DismissScreenHints();
            yield return new WaitForSecondsRealtime(1.0f);
        }

        /// <summary>
        /// Leave the board through its own CLOSE when it is available; an empty board hides it
        /// (F1), so the real bottom-nav TEE — the only widget left — takes the player out.
        /// </summary>
        IEnumerator LeaveBoard(string checkId)
        {
            var screen = ScreenObject("TournamentLeaderboardScreen");
            var ctrl = screen != null ? screen.GetComponentInChildren<TournamentLeaderboardScreenController>(true) : null;
            var field = typeof(TournamentLeaderboardScreenController).GetField("_closeButton", Priv);
            var close = ctrl != null && field != null ? field.GetValue(ctrl) as Button : null;
            bool usable = close != null && close.gameObject.activeInHierarchy && close.interactable;
            if (usable)
            {
                yield return CloseScreen<TournamentLeaderboardScreenController>("TournamentLeaderboardScreen", checkId, ScreenId.TournamentSelection);
                yield break;
            }
            string why = close == null ? "not wired" : (!close.gameObject.activeInHierarchy ? "inactive: " + InactiveAncestor(close.transform) : "not interactable");
            Finding("F1", $"{checkId}: the board's wired CloseButton is {why} — an empty board (\"No finishers yet\") has no CLOSE; leaving through the real bottom-nav TEE instead");
            yield return TapNamed("NavTeeButton", ScreenId.ModeSelection, "bottom-nav TEE (no CLOSE on the empty board)");
        }

        /// <summary>The result presenter's rank modal for the planted entry — CLAIM through its real button.</summary>
        IEnumerator ClaimResultModal()
        {
            var modal = FindObjectOfType<TournamentResultModalController>(false);
            var field = typeof(TournamentResultModalController).GetField("_claimButton", Priv);
            var claim = modal != null && field != null ? field.GetValue(modal) as Button : null;
            if (claim == null || !claim.gameObject.activeInHierarchy) { Line("no result modal on screen"); yield break; }
            Line("result modal is up for the planted entry — tapping its real CLAIM button");
            claim.onClick.Invoke();
            yield return new WaitForSecondsRealtime(2.5f);
        }

        static string InactiveAncestor(Transform t)
        {
            for (Transform? p = t; p != null; p = p.parent) if (!p.gameObject.activeSelf) return p.name + " is inactive";
            return "?";
        }

        /// <summary>The screen's own close button — the controller's wired <c>_closeButton</c>, not a name guess.</summary>
        IEnumerator CloseScreen<T>(string screenName, string checkId, ScreenId expected) where T : Component
        {
            var screen = ScreenObject(screenName);
            var ctrl = screen != null ? screen.GetComponentInChildren<T>(true) : null;
            var field = typeof(T).GetField("_closeButton", Priv);
            var close = ctrl != null && field != null ? field.GetValue(ctrl) as Button : null;
            if (close == null || !close.gameObject.activeInHierarchy || !close.interactable)
            {
                Check(checkId, false, $"{screenName}: no active wired _closeButton (ctrl={(ctrl != null)}, field={(field != null)}, button={(close != null ? close.name : "<null>")})");
                yield break;
            }
            Line($"tapping the real {close.name}.onClick (wired _closeButton of {typeof(T).Name})");
            close.onClick.Invoke();
            yield return WaitScreen(expected, 6f);
            Check(checkId, ScreenManager.Instance!.CurrentScreen == expected, $"{screenName} CLOSE landed on {ScreenManager.Instance.CurrentScreen} (wanted {expected})");
            yield return new WaitForSecondsRealtime(1.0f);
        }

        /// <summary>The Tournaments mode card, chosen by its CSV route (GamePolishProbe's scar), expanded
        /// if needed, then its PLAY.</summary>
        IEnumerator ModeCardPlayTournaments()
        {
            var modeGo = ScreenObject("ModeSelectionScreen");
            var sr = modeGo != null ? modeGo.GetComponentInChildren<ScrollRect>(true) : null;
            Transform? content = sr != null ? sr.content : null;
            if (content == null) { Line("WARN no mode-card content"); yield break; }
            Transform? chosen = null;
            for (int i = 0; i < content.childCount; i++)
            {
                Transform card = content.GetChild(i);
                if (!card.gameObject.activeInHierarchy) continue;
                var cc = card.GetComponent<GolfinRedux.UI.ModeSelect.ModeCardController>();
                if (cc == null || string.IsNullOrEmpty(cc.ModeId)) continue;
                var db = GolfinRedux.UI.ModeSelect.ModesDatabaseCSV.Instance;
                var mode = db != null ? db.GetMode(cc.ModeId) : null;
                if (mode != null && mode.target == GolfinRedux.UI.ModeSelect.ModeSelectScreenController.TargetTournaments) { chosen = card; break; }
            }
            if (chosen == null) { Line("WARN no mode card routes to tournaments"); yield break; }
            var cc2 = chosen.GetComponent<GolfinRedux.UI.ModeSelect.ModeCardController>();
            if (cc2 != null && cc2.State != GolfinRedux.UI.ModeSelect.ModeCardState.Expanded)
            {
                var tap = chosen.Find("CardTapButton")?.GetComponent<Button>() ?? chosen.GetComponent<Button>();
                if (tap != null && tap.interactable) tap.onClick.Invoke();
                yield return new WaitForSecondsRealtime(0.8f);
            }
            var play = chosen.Find("ExpandedContainer/ActionButton")?.GetComponent<Button>();
            if (play == null || !play.gameObject.activeInHierarchy || !play.interactable) { Line("WARN tournaments card has no active ActionButton"); yield break; }
            Line($"tapping the real Tournaments mode card ActionButton.onClick ('{chosen.name}')");
            play.onClick.Invoke();
            yield return WaitScreen(ScreenId.TournamentSelection, 8f);
        }

        IEnumerator DismissScreenHints()
        {
            for (int i = 0; i < 12; i++)
            {
                Button? next = null;
                foreach (Button b in FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                    if (b.name == "NextButton" && b.interactable && HasAncestor(b.transform, "ScreenHintModal")) { next = b; break; }
                if (next == null) { if (i > 0) yield return new WaitForSecondsRealtime(0.6f); yield break; }
                Line($"  hint modal: tapping the real {next.name}.onClick (#{i + 1})");
                next.onClick.Invoke();
                yield return new WaitForSecondsRealtime(0.9f);
            }
        }

        IEnumerator WaitScreen(ScreenId target, float seconds)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (ScreenManager.Instance != null && ScreenManager.Instance.CurrentScreen != target && Time.realtimeSinceStartup < deadline)
                yield return null;
            Line($"  screen = {(ScreenManager.Instance != null ? ScreenManager.Instance.CurrentScreen.ToString() : "<none>")} (wanted {target})");
        }

        IEnumerator Still(string label)
        {
            yield return new WaitForEndOfFrame();
            string path = CaptureCore.SnapPlayModeSafe(label);
            bool exists = !string.IsNullOrEmpty(path) && File.Exists(path);
            string md5 = exists ? Md5(path) : "-";
            Line($"  still '{label}': {(exists ? path : "<phantom path: " + path + ">")} md5={md5}");
            if (exists)
            {
                string dst = Path.Combine(TournamentRouteVerify.OutDir, label + ".png");
                File.Copy(path, dst, true);
                _stills.Add(dst + " md5=" + md5);
            }
        }

        IEnumerator Finish()
        {
            RemovePlantedEntries();
            bool all = true; foreach (var c in _checks) all &= c.pass;
            string status = _checks.Count == 0 ? "FAIL" : (all ? "PASS" : "FAIL");
            Line($"=== {status} {_note} ===");
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"task\": \"finished_tournament_leaderboard_route\",");
            sb.AppendLine($"  \"utc\": \"{DateTime.UtcNow:u}\",");
            sb.AppendLine($"  \"status\": \"{status}\",");
            sb.AppendLine($"  \"note\": \"{Esc(_note)}\",");
            sb.AppendLine("  \"checks\": [");
            for (int i = 0; i < _checks.Count; i++)
                sb.AppendLine($"    {{ \"id\": \"{_checks[i].id}\", \"pass\": {(_checks[i].pass ? "true" : "false")}, \"detail\": \"{Esc(_checks[i].detail)}\" }}{(i < _checks.Count - 1 ? "," : "")}");
            sb.AppendLine("  ],");
            sb.AppendLine("  \"findings\": [");
            for (int i = 0; i < _findings.Count; i++)
                sb.AppendLine($"    {{ \"id\": \"{_findings[i].id}\", \"detail\": \"{Esc(_findings[i].detail)}\" }}{(i < _findings.Count - 1 ? "," : "")}");
            sb.AppendLine("  ],");
            sb.AppendLine("  \"stills\": [" + string.Join(", ", _stills.ConvertAll(s => "\"" + Esc(s) + "\"")) + "]");
            sb.AppendLine("}");
            Directory.CreateDirectory(TournamentRouteVerify.OutDir);
            File.WriteAllText(Path.Combine(TournamentRouteVerify.OutDir, "route_verify.json"), sb.ToString());
            File.WriteAllText(Path.Combine(TournamentRouteVerify.OutDir, "route_verify.log"), _log.ToString());
            yield return new WaitForSecondsRealtime(0.5f);
            EditorApplication.isPlaying = false;
        }

        // ── lookups ──────────────────────────────────────────────────────────

        List<TournamentSelectionCard> LogCards(string when)
        {
            var cards = SceneCards();
            Line($"{cards.Count} cards on TournamentSelection ({when}):");
            foreach (var c in cards)
                Line($"  '{c.TournamentId}' state={c.State} cta='{CtaLabel(c)}' active={c.gameObject.activeInHierarchy}");
            return cards;
        }

        static (int next, int locked, int finished) HoleCardCounts(GameObject? holeScreen)
        {
            int next = 0, locked = 0, finished = 0;
            if (holeScreen != null)
                foreach (var t in holeScreen.GetComponentsInChildren<Transform>(false))
                {
                    if (!t.name.EndsWith("(Clone)")) continue;
                    if (t.name.StartsWith("TournamentHoleCard_Next")) next++;
                    else if (t.name.StartsWith("TournamentHoleCard_Locked")) locked++;
                    else if (t.name.StartsWith("TournamentHoleCard_Finished")) finished++;
                }
            return (next, locked, finished);
        }

        static List<TournamentSelectionCard> SceneCards()
        {
            var list = new List<TournamentSelectionCard>();
            foreach (var c in FindObjectsByType<TournamentSelectionCard>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (c.gameObject.scene.IsValid() && !string.IsNullOrEmpty(c.TournamentId)) list.Add(c);
            list.Sort((a, b) => string.CompareOrdinal(a.TournamentId, b.TournamentId));
            return list;
        }

        static Button? ActiveCta(TournamentSelectionCard card)
        {
            foreach (var b in card.GetComponentsInChildren<Button>(false))
                if (b.gameObject.activeInHierarchy && b.interactable) return b;
            return null;
        }

        static string CtaLabel(TournamentSelectionCard card)
        {
            var b = ActiveCta(card);
            var t = b != null ? b.GetComponentInChildren<TMPro.TMP_Text>(false) : null;
            return t != null ? t.text : "?";
        }

        static GameObject? ScreenObject(string name)
        {
            var root = GameObject.Find("Canvas/ScreensRoot");
            var t = root != null ? root.transform.Find(name) : null;
            return t != null ? t.gameObject : null;
        }

        static Button? FindActiveButton(string name)
        {
            foreach (Button b in FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (b.name == name && b.gameObject.activeInHierarchy && b.interactable) return b;
            return null;
        }

        static bool HasAncestor(Transform t, string name)
        {
            for (Transform? p = t.parent; p != null; p = p.parent) if (p.name == name) return true;
            return false;
        }

        static string Md5(string path)
        {
            using var md5 = System.Security.Cryptography.MD5.Create();
            using var fs = File.OpenRead(path);
            return BitConverter.ToString(md5.ComputeHash(fs)).Replace("-", "").ToLowerInvariant();
        }

        static string Esc(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ");
    }
}
