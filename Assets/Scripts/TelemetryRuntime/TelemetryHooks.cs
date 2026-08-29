// Order: beta_telemetry — the one place every telemetry event is wired to an existing signal.
using System;
using System.Collections.Generic;
using Golfin.Gameplay.Session;
using Golfin.InventorySync;
using Golfin.Roster;
using Golfin.Gameplay.UI;
using Golfin.Gameplay.UI.HUD;
using Golfin.Gameplay.UI.Quality;
using Golfin.Telemetry;
using GolfinRedux.UI;
using GolfinRedux.UI.BuildInfo;
using UnityEngine;

namespace GolfinRedux.TelemetryRuntime
{
    /// <summary>
    /// Subscribes the telemetry service to signals that already existed (beta_telemetry
    /// SPEC §1). This assembly is the glue: it is the only one that can see BOTH
    /// Assembly-CSharp types (<c>ScreenManager</c>, <c>CharacterManager</c>,
    /// <c>RewardPointsManager</c>, <c>AppVersion</c>) and the telemetry core.
    ///
    /// Self-bootstrapping at <c>AfterSceneLoad</c> — the same pattern <c>AuthService</c>,
    /// <c>ServerBalanceSyncBehaviour</c> and <c>BuildStamp</c> use. No scene edits, no
    /// prefab, no execution-order dependency.
    ///
    /// NOTHING HERE MAY THROW INTO GAMEPLAY. Every handler body goes through
    /// <c>RecordSafe</c>, which builds the payload inside its own try/catch, so a null
    /// manager or a bad lookup costs one telemetry row and nothing else.
    /// </summary>
    public static class TelemetryHooks
    {
        private static bool _installed;

        // Late-bound instance events: RewardPointsManager and CharacterManager expose
        // INSTANCE events on singletons that may not exist yet at AfterSceneLoad.
        private static bool _pointsBound;
        private static bool _characterBound;
        private static float _lateBindDeadline;

        private static int _lastPointsBalance = int.MinValue;
        private static ScreenId _lastScreen = ScreenId.Logo;
        private static float _roundStartRealtime;
        private static int _roundHole;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void Install()
        {
            if (_installed || !TelemetryConfig.Enabled) return;

            // Play mode only. This is [RuntimeInitializeOnLoadMethod] so it does not auto-run in
            // edit mode, but an edit-mode call (a test, a tooling script) would otherwise set
            // _installed and SILENTLY BLOCK the real install on the next play — the failure mode
            // where telemetry is wired to nothing and no error says so.
            if (!Application.isPlaying) return;

            _installed = true;

            try
            {
                var svc = TelemetryService.Instance;

                // Envelope fields only this assembly can resolve. AppVersion.BuildNumber reads
                // the same baked Resources/Data/build_stamp.txt the About screen shows, so the
                // telemetry rows and the on-device stamp can never disagree about the binary.
                svc.BuildNumber = int.TryParse(AppVersion.BuildNumber, out int build) ? build : (int?)null;
                svc.CurrentScreenProvider = () =>
                    ScreenManager.Instance != null ? ScreenManager.Instance.CurrentScreen.ToString() : null;

                // The behaviour owns the flush clock and the FPS samples — touch it to create it.
                var _ = TelemetryBehaviour.Instance;

                Application.logMessageReceived += OnLogMessage;

                ScreenManager.ScreenChanged   += OnScreenChanged;
                GameSession.OnRoundStarted    += OnRoundStarted;
                GameSession.OnHistoryChanged  += OnHistoryChanged;
                GameSession.OnHoleComplete    += OnHoleComplete;
                ShotTelemetryRelay.FlickRejected += OnFlickRejected;
                ShotTelemetryRelay.ShotCancelled += OnShotCancelled;
                Golfin.Auth.AuthService.SignedIn += OnSignedIn;

                // The refundable-spend path, counted (CONTENT_PIPELINE_PLAN §6.5 decision 1).
                // ASSIGNED, not +=: Install() is guarded against running twice, but a domain reload
                // between edit and play can leave a stale delegate on the service singleton, and a
                // double-subscribe here would double every count — which is exactly the number this
                // is being read for.
                InventorySyncService.Instance.OnQuantitiesRaised = OnInventoryQuantitiesRaised;

                _lateBindDeadline = Time.realtimeSinceStartup + 60f;
                TryLateBind();

                svc.RecordSafe(TelemetryEventNames.SessionStart, () => new Dictionary<string, object>
                {
                    ["device_model"] = SystemInfo.deviceModel,
                    ["os"]           = SystemInfo.operatingSystem,
                    ["memory_mb"]    = SystemInfo.systemMemorySize,
                    ["screen"]       = $"{Screen.width}x{Screen.height}",

                    // quality_tiers §6: which tier this session actually rendered at, and whether the
                    // player pinned it. Paired with the existing per-hole fps_avg/fps_low this is the
                    // whole evidence base for the deferred thermal-governor question — a Mid session
                    // that still sags means static tiers were not enough.
                    ["tier"]         = QualityTierService.Current.ToString(),
                    ["tier_source"]  = QualityTierService.IsOverride ? "override" : "auto",
                });

                Debug.Log($"[Telemetry] Hooks installed — session={svc.SessionId}, sends={svc.SendsEnabled}.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Telemetry] Install failed and was swallowed: {ex}");
            }
        }

        // ── Inventory merge raises (CONTENT_PIPELINE_PLAN §6.5 decision 1) ───────

        /// <summary>
        /// ONE ROW PER RAISED STACK, not one per merge.
        ///
        /// <para>
        /// The decision of record asks for "every merge that raises a quantity, WITH PLAYER AND
        /// ITEM" — so the item has to be the grain. A single row carrying a count would answer "how
        /// often" but not "which consumable", and which consumable is the half that feeds back into
        /// the economy tuning. The player is not in the payload deliberately: <c>/telemetry/events</c>
        /// stamps <c>user_id</c> from the bearer token and IGNORES any id in the body, so putting one
        /// here would be a second, lower-trust copy of a column the server already fills correctly.
        /// </para>
        /// </summary>
        private static void OnInventoryQuantitiesRaised(IReadOnlyList<InventoryRaise> raises)
        {
            if (raises == null) return;

            var svc = TelemetryService.Instance;
            foreach (var raise in raises)
            {
                // Captured per iteration — RecordSafe runs the builder immediately, but a foreach
                // variable closed over lazily is the classic way this stops being true.
                InventoryRaise captured = raise;
                svc.RecordSafe(TelemetryEventNames.InventoryMergeRaise, () => new Dictionary<string, object>
                {
                    ["kind"]  = captured.Kind.ToString().ToLowerInvariant(),
                    ["item"]  = captured.Id,
                    ["from"]  = captured.From,
                    ["to"]    = captured.To,
                    // -1 is the UNLIMITED sentinel on balls, so a delta there is meaningless and is
                    // reported as 0 rather than as a made-up number.
                    ["delta"] = captured.To < 0 || captured.From < 0 ? 0 : captured.To - captured.From,
                });
            }
        }

        // ── Late binding for the two instance-event singletons ────────────────────

        /// <summary>
        /// <c>RewardPointsManager.OnPointsChanged</c> and <c>CharacterManager.OnCharacterLeveledUp</c>
        /// are INSTANCE events on scene singletons, so they cannot be subscribed before those
        /// objects Awake. Retried from <see cref="OnScreenChanged"/> (which fires often and early)
        /// until both are bound or the 60s deadline passes.
        /// </summary>
        private static void TryLateBind()
        {
            if (_pointsBound && _characterBound) return;
            if (Time.realtimeSinceStartup > _lateBindDeadline) return;

            try
            {
                if (!_pointsBound && RewardPointsManager.Instance != null)
                {
                    RewardPointsManager.Instance.OnPointsChanged += OnPointsChanged;
                    _lastPointsBalance = RewardPointsManager.Instance.GetPoints();
                    _pointsBound = true;
                }

                if (!_characterBound && CharacterManager.Instance != null)
                {
                    CharacterManager.Instance.OnCharacterLeveledUp += OnCharacterLeveledUp;
                    _characterBound = true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Telemetry] Late bind failed and was swallowed: {ex.Message}");
            }
        }

        // ── Handler guard ─────────────────────────────────────────────────────────

        /// <summary>
        /// Runs a handler body so it can NEVER reach the gameplay code that raised the event.
        ///
        /// `TelemetryService.RecordSafe` only wraps the payload BUILDER; the statements around
        /// it (reading a manager, touching the behaviour, flipping RoundActive) were unguarded,
        /// so a throw there propagated out through e.g. `GameSession.MarkHoleComplete`, whose
        /// `OnHoleComplete?.Invoke(data)` has no try/catch of its own. That is precisely the
        /// failure SPEC §3 rule 1 forbids, and it was live: see the isPlaying note below.
        ///
        /// The `isPlaying` gate exists because these are STATIC event subscriptions. They
        /// outlive play mode until the next domain reload, so an EditMode test calling
        /// `GameSession.MarkHoleComplete` after any play-mode session lands here. Telemetry has
        /// nothing to say about an edit-mode call, and trying to service one used to throw.
        /// </summary>
        private static void Guard(string what, Action body)
        {
            if (!TelemetryConfig.Enabled || !Application.isPlaying) return;
            try { body(); }
            catch (Exception ex) { Debug.LogWarning($"[Telemetry] {what} threw and was swallowed: {ex.Message}"); }
        }

        // ── Handlers ──────────────────────────────────────────────────────────────

        private static void OnSignedIn(Golfin.Auth.AuthSession session)
        {
            Guard("OnSignedIn", () =>
            {
                // The queue holds while unauthenticated; this is the moment it can drain.
                TelemetryService.Instance.Flush();
            });
        }

        private static void OnScreenChanged(ScreenId screen)
        {
            Guard("OnScreenChanged", () =>
            {
                TryLateBind();

                var svc = TelemetryService.Instance;

                svc.RecordSafe(TelemetryEventNames.ScreenView, () => new Dictionary<string, object>
                {
                    ["screen"]       = screen.ToString(),
                    // The first Home view's since_boot_s IS the boot→Home load-time metric
                    // (SPEC §1 #3) — there is no separate load_time event.
                    ["since_boot_s"] = Math.Round(Time.realtimeSinceStartup, 2),
                });

                // round_abandoned: a menu screen came up while a round was still active and no
                // hole_complete had cleared it. ResetSession() would have been the tidier choke
                // point but it has ZERO production call sites (tests only), so it never fires.
                if (svc.RoundActive && IsMenuScreen(screen))
                {
                    int hole = _roundHole;
                    int shots = GameSession.ShotHistory.Count;
                    ScreenId from = _lastScreen;

                    svc.RoundActive = false;
                    svc.RecordSafe(TelemetryEventNames.RoundAbandoned, () => new Dictionary<string, object>
                    {
                        ["hole"]        = hole,
                        ["shots_taken"] = shots,
                        ["last_screen"] = from.ToString(),
                    });
                }

                _lastScreen = screen;
            });
        }

        private static bool IsMenuScreen(ScreenId screen)
            => screen == ScreenId.Home
            || screen == ScreenId.HoleSelection
            || screen == ScreenId.ModeSelection
            || screen == ScreenId.TournamentHoleSelection
            || screen == ScreenId.TournamentSelection;

        private static void OnRoundStarted()
        {
            Guard("OnRoundStarted", () =>
            {
                var svc = TelemetryService.Instance;
                svc.RoundActive = true;
                _roundStartRealtime = Time.realtimeSinceStartup;
                _roundHole = GameSession.CurrentHoleNumber;

                var behaviour = TelemetryBehaviour.Instance;
                if (behaviour != null) behaviour.ResetFpsSampling();

                svc.RecordSafe(TelemetryEventNames.RoundStart, () => new Dictionary<string, object>
                {
                    ["hole"]          = GameSession.CurrentHoleNumber,
                    ["character_id"]  = GameSession.SelectedCharacterId,
                    ["bag_slot"]      = GameSession.EquippedBagSlot,
                    ["is_tournament"] = GameSession.IsTournament,
                    ["tournament_id"] = GameSession.TournamentId,
                });
            });
        }

        private static void OnHistoryChanged()
        {
            Guard("OnHistoryChanged", () =>
            {
                // OnHistoryChanged also fires on ResetForNewHole (a CLEAR, not a shot) — an empty
                // history means there is nothing to report.
                if (GameSession.ShotHistory.Count == 0) return;

                TelemetryService.Instance.RecordSafe(TelemetryEventNames.ShotTaken, () =>
                {
                    var shot = GameSession.ShotHistory[GameSession.ShotHistory.Count - 1];
                    var payload = new Dictionary<string, object>
                    {
                        ["shot_number"] = shot.ShotNumber,
                        ["club"]        = shot.ClubLabel,
                        ["distance_m"]  = Math.Round(shot.DistanceXZMeters, 1),
                        ["terminal"]    = shot.TerminalState,
                        ["ob_reason"]   = shot.OBReason,
                        ["surface"]     = shot.FinalSurface,
                        ["penalty"]     = shot.PenaltyStrokes,
                        ["hole"]        = GameSession.CurrentHoleNumber,
                    };
                    // shot_timing_telemetry: timing01 / timing_mul / timing_band. Written by
                    // GameSession (which can see ControlsConfig's band edges — this assembly
                    // cannot, Golfin.Gameplay.Config is not auto-referenced). timing01 and
                    // timing_band are null, never 0/"red", for a sampleless swing — same
                    // nullable path ob_reason already uses.
                    GameSession.AppendShotTimingKeys(payload, shot);
                    return payload;
                });
            });
        }

        private static void OnHoleComplete(HoleCompletionData data)
        {
            Guard("OnHoleComplete", () =>
            {
                var svc = TelemetryService.Instance;
                float duration = Time.realtimeSinceStartup - _roundStartRealtime;
                var behaviour = TelemetryBehaviour.Instance;
                float fpsAvg = behaviour != null ? behaviour.AverageFps : 0f;
                float fpsLow = behaviour != null ? behaviour.LowFps : 0f;

                svc.RoundActive = false;

                svc.RecordSafe(TelemetryEventNames.HoleComplete, () => new Dictionary<string, object>
                {
                    ["hole"]            = data.HoleNumber,
                    ["strokes"]         = data.Strokes,
                    ["penalty_strokes"] = data.PenaltyStrokes,
                    ["result"]          = data.TerminalState.ToString(),
                    ["duration_s"]      = Math.Round(duration, 1),
                    ["fps_avg"]         = Math.Round(fpsAvg, 1),
                    ["fps_low"]         = Math.Round(fpsLow, 1),
                    // HoleContext.Par is the same value the result modal and the hole card read.
                    ["par"]             = HoleContext.Par,
                });
            });
        }

        private static void OnFlickRejected(float speed)
        {
            Guard("OnFlickRejected", () =>
            {
                TelemetryService.Instance.RecordSafe(TelemetryEventNames.FlickRejected, () =>
                    new Dictionary<string, object>
                    {
                        ["speed"]       = Math.Round(speed, 3),
                        ["hole"]        = GameSession.CurrentHoleNumber,
                        ["shot_number"] = GameSession.ShotHistory.Count + 1,
                    });
            });
        }

        private static void OnShotCancelled()
        {
            Guard("OnShotCancelled", () =>
            {
                TelemetryService.Instance.RecordSafe(TelemetryEventNames.ShotCancelled, () =>
                    new Dictionary<string, object>
                    {
                        ["hole"]        = GameSession.CurrentHoleNumber,
                        ["shot_number"] = GameSession.ShotHistory.Count + 1,
                    });
            });
        }

        private static void OnPointsChanged(int balance)
        {
            Guard("OnPointsChanged", () =>
            {
                int previous = _lastPointsBalance;
                _lastPointsBalance = balance;

                TelemetryService.Instance.RecordSafe(TelemetryEventNames.PointsChanged, () =>
                    new Dictionary<string, object>
                    {
                        ["balance"] = balance,
                        // Null on the very first callback: there is no previous value to diff against,
                        // and reporting `delta == balance` would read as a huge phantom grant.
                        ["delta"]   = previous == int.MinValue ? (int?)null : balance - previous,
                    });
            });
        }

        private static void OnCharacterLeveledUp(string characterId)
        {
            Guard("OnCharacterLeveledUp", () =>
            {
                TelemetryService.Instance.RecordSafe(TelemetryEventNames.LevelUp, () =>
                    new Dictionary<string, object> { ["character_id"] = characterId });
            });
        }

        private static void OnLogMessage(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Exception) return;
            TelemetryService.Instance.RecordException(condition, stackTrace);
        }
    }
}
