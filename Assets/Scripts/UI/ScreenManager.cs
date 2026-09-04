using System.Collections.Generic;
using UnityEngine;
using Golfin.Audio;
using GolfinRedux.Demo;

namespace GolfinRedux.UI
{
    public enum ScreenId
    {
        Logo,
        Splash,
        Loading,
        Home,
        Roster,
        Inventory,
        HoleSelection,
        ModeSelection,
        // missions_v1 §C1 — the Missions campaign browse screen, cloned from HoleSelection.
        MissionSelection,
        Leaderboard,
        // Tournament screens (Stage 1 scaffolds — separate full screens from the
        // non-tournament HoleSelection / Leaderboard above).
        TournamentHoleSelection,
        TournamentLeaderboard,
        // T7 — Tournament Selection browse screen (Figma 13386:1758, Stage 0–1)
        TournamentSelection,
        // Order 517 — Stamina Boost Shop (Figma 13156:1178 + 13330:1139)
        StaminaShopSelection,
        StaminaShopDetail,
        // Order 610 — General Shop / Rewards Center (Figma 4079:28230)
        GeneralShop,
        // Gacha pillar screen 2 — Gacha History / pull log (Figma 4079:18306)
        GachaHistory,
        // Gacha pillar screen 3 — Gacha Prizes / pool preview (Figma 13622:2222)
        GachaPrizes,
        // gps_hub_entry — GPS / PLAYLIFE hub (Figma 14011:32819), reached from the Home promo banner
        GpsHub,
        // score_upload_flow — Figma 14022:32576…14024:101792. ONE screen, six step roots toggled by
        // ScoreUploadFlowController; reached from the hub's camera centre button and SCREENSHOT tile.
        ScoreUpload,
        // gps_profile_pack — three GPS sub-screens
        GpsProfile,
        GpsAvatar,
        GpsBadges,
        // auth_golf_profile — the post-signup Golf Profile capture (Figma 14029:33628) and the
        // one-page Welcome tutorial (14029:33929). GPS surface, so both are on GpsGate's list:
        // in a "punch it" build neither is reachable and the Home trigger that offers them is a
        // no-op. Offered ONCE per device on the first Home entry after sign-in.
        GpsGolfProfile,
        GpsWelcome,
        // gps_gifts_votes — the last two GPS screens (Figma 14027:101843 / 14028:33534).
        // Gift is reached from the hub's GIFT nav slot and its GIFT action tile; Vote from the
        // hub's VOTE tile and from a vote card's own GIFT button in the other direction.
        GpsGift,
        GpsVote,
        // gps_checkin — the Rounds tab (Figma 14076:33800 / 14077:100447). Reached from the hub
        // nav bar's ROUNDS slot, which was deliberately inert until this task: chips + a real map
        // + nearby spots, CHECK IN -> a live round card -> SCORE UPLOAD or CHECK OUT.
        GpsRounds,
        // Settings removed - it's an overlay, not a screen

        // Order: login_signup_screens — account auth gate (Phase 1 — UI only, no backend)
        // These screens are excluded from showBars (pre-game gates); menu music keeps
        // playing across them so the theme is unbroken from Splash to Home.
        Login,
        CreateUsername,
        SignUp,
        EmailConfirmation,
        // auth_recovery_flow — set-new-password screen, reached only from a type=recovery deep link.
        ResetPassword,
        // starting_character_selection — first-run character picker; shares RosterScreen in starter-mode
        StartingCharacterSelection
    }

    /// <summary>
    /// Central controller for shell UI screens (Logo, Splash, Loading, Home).
    /// Uses FadeController (if present) to fade to/from black when switching screens.
    /// Note: Settings is an overlay managed by SettingsController, not a screen.
    /// </summary>
    public class ScreenManager : MonoBehaviour
    {
        public static ScreenManager? Instance { get; private set; }

        [SerializeField] private ScreenId _initialScreen = ScreenId.Logo;

        [Header("Screen Containers")]
        [SerializeField] private GameObject _logoScreen;
        [SerializeField] private GameObject _splashScreen;
        [SerializeField] private GameObject _loadingScreen;
        [SerializeField] private GameObject _homeScreen;
        [SerializeField] private GameObject _rosterScreen;
        [SerializeField] private GameObject _inventoryScreen;
        [SerializeField] private GameObject _holeSelectionScreen;
        [SerializeField] private GameObject _modeSelectionScreen;
        // missions_v1 §C1 — the Missions campaign browse screen.
        [SerializeField] private GameObject _missionSelectionScreen;
        [SerializeField] private GameObject _leaderboardScreen;
        [SerializeField] private GameObject _tournamentHoleSelectionScreen;
        [SerializeField] private GameObject _tournamentLeaderboardScreen;
        [SerializeField] private GameObject _tournamentSelectionScreen;
        // Order 517 — Stamina Boost Shop screens
        [SerializeField] private GameObject _staminaShopSelectionScreen;
        [SerializeField] private GameObject _staminaShopDetailScreen;
        // Order 610 — General Shop / Rewards Center
        [SerializeField] private GameObject _generalShopScreen;
        // Gacha pillar screen 2 — Gacha History / pull log
        [SerializeField] private GameObject _gachaHistoryScreen;
        // Gacha pillar screen 3 — Gacha Prizes / pool preview
        [SerializeField] private GameObject _gachaPrizesScreen;
        // gps_hub_entry — GPS / PLAYLIFE hub. Draws its OWN bottom nav, so it is deliberately
        // absent from the showBars list below and shown with ShowTopBarOnly() instead.
        [SerializeField] private GameObject _gpsHubScreen;
        // score_upload_flow — the six-step score upload. Same top-bar-only shape as the hub: it
        // carries the hub's own GPS nav bar inside its prefab, so the shared bottom nav stays hidden.
        [SerializeField] private GameObject _scoreUploadScreen;
        [SerializeField] private GameObject _gpsProfileScreen;
        [SerializeField] private GameObject _gpsAvatarScreen;
        [SerializeField] private GameObject _gpsBadgesScreen;
        // auth_golf_profile — post-signup Golf Profile capture + Welcome tutorial. Same
        // top-bar-only chrome as the rest of the GPS surface (via GpsGate.IsGpsScreen), even
        // though neither draws the GPS nav bar: the two frames hide it (SPEC § Reference).
        [SerializeField] private GameObject _gpsGolfProfileScreen;
        [SerializeField] private GameObject _gpsWelcomeScreen;
        // gps_gifts_votes — same top-bar-only chrome as the rest of the GPS surface; both draw
        // the hub's own GPS nav bar inside their prefab, so the shared bottom nav stays hidden.
        [SerializeField] private GameObject _gpsGiftScreen;
        [SerializeField] private GameObject _gpsVoteScreen;
        // gps_checkin — the Rounds tab. Same top-bar-only chrome and the same in-prefab GPS nav
        // bar as Gift/Vote.
        [SerializeField] private GameObject _gpsRoundsScreen;
        // _settingsScreen removed - Settings is an overlay managed by SettingsController, not ScreenManager

        // Order: login_signup_screens — account auth gate screens
        [SerializeField] private GameObject _loginScreen;
        [SerializeField] private GameObject _createUsernameScreen;
        [SerializeField] private GameObject _signUpScreen;
        [SerializeField] private GameObject _emailConfirmationScreen;
        // auth_recovery_flow — set-new-password screen
        [SerializeField] private GameObject _resetPasswordScreen;

        [Header("Audio (Order 350)")]
        [Tooltip("Main Theme music clip — assign Assets/Music/Main Theme.mp3 in the Inspector.")]
        [SerializeField] private AudioClip _mainThemeClip;

        private ScreenId _currentScreen;

        /// <summary>Returns the currently active ScreenId.</summary>
        public ScreenId CurrentScreen => _currentScreen;

        // ── nav_back_memory §2 — same-pillar history + per-pillar "last screen" ───────
        // Session-only, in-memory (SPEC § Out of scope: no cross-launch persistence).

        /// <summary>Newest-last stack of same-pillar screens the player pushed through.</summary>
        private readonly List<ScreenId> _history = new List<ScreenId>();

        /// <summary>Deepest screen the player last stood on inside each pillar.</summary>
        private readonly Dictionary<Golfin.UI.PersistentUIManager.Screen, ScreenId> _lastInPillar =
            new Dictionary<Golfin.UI.PersistentUIManager.Screen, ScreenId>();

        private const int HistoryCap = 16;

        /// <summary>
        /// Fired at the end of every ApplyScreen call (after SetActive calls and _currentScreen update).
        /// Used by TournamentResultPresenter to know when an eligible screen is active.
        /// </summary>
        public static event System.Action<ScreenId>? ScreenChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            // FadeController's GameObject may be left inactive in the Editor.
            // Activate it now so fade transitions work for all screens including Inventory.
            if (FadeController.Instance == null)
            {
                var fc = FindObjectOfType<FadeController>(includeInactive: true);
                if (fc != null) fc.gameObject.SetActive(true);
            }
        }

        private void Start()
        {
            // Show the initial screen immediately
            ApplyScreen(_initialScreen);

            // Then fade in from black if FadeController is present
            if (FadeController.Instance != null)
            {
                FadeController.Instance.FadeIn();
            }

            // auth_recovery_flow — warm-path routing: a recovery deep link while the app is running
            // routes to the set-new-password screen (success) or to Login (failure — the Login screen
            // surfaces the localized error). Subscribed AFTER the initial ApplyScreen so a cold-start
            // event (fired during AfterSceneLoad, before Start) can't be stomped by _initialScreen;
            // the cold path is covered by LoginScreenController.OnEnable reading
            // AuthService.PendingRecovery / ConsumeRecoveryFailure instead.
            Golfin.Auth.AuthService.PasswordRecovery += OnPasswordRecovery;

            // gps_standalone_shell §D6 — a WARM internal deep link (golfin://gps,
            // golfingps://gps) opens the surface it names.
            //
            // WARM ONLY, and deliberately: subscribing after the initial ApplyScreen for the same
            // reason the recovery hook does, and no Application.absoluteURL sweep, because a
            // cold-start sweep would race the boot and could land the player past the title gate
            // with no session. The cold case needs no handling in the shell — its boot
            // destination already IS the hub — and in the game a cold golfin://gps simply boots
            // normally, which is what an unauthenticated launch has to do anyway.
            //
            // The route is resolved by BannerPolicy, the one enumerated allowlist, so a URL a
            // stranger can put in Safari gets exactly the grant a dashboard banner row gets —
            // and Navigate still puts the result through every gate.
            Application.deepLinkActivated += OnDeepLinkActivated;
        }

        private void OnDestroy()
        {
            Golfin.Auth.AuthService.PasswordRecovery -= OnPasswordRecovery;
            Application.deepLinkActivated -= OnDeepLinkActivated;
        }

        // gps_standalone_shell §D6 — see the subscription note in Start().
        private void OnDeepLinkActivated(string url)
        {
            if (!Golfin.Banners.BannerPolicy.TryGetInternalRoute(url, out ScreenId screen)) return;
            Debug.Log($"[ScreenManager] deep link {url} -> {screen}");
            ShowScreen(screen);
        }

        // auth_recovery_flow — see the subscription note in Start().
        private void OnPasswordRecovery(Golfin.Auth.AuthResult r)
        {
            if (r != null && r.Success) ShowScreen(ScreenId.ResetPassword);
            else ShowScreen(ScreenId.Login);
        }

        /// <summary>
        /// Show the given screen. If instant=false and FadeController exists,
        /// performs fade-out -> swap -> fade-in.
        /// Every FORWARD navigation goes through here; it pushes the screen being left
        /// onto the same-pillar history stack (nav_back_memory §2).
        /// </summary>
        public void ShowScreen(ScreenId screenId, bool instant = false)
        {
            Navigate(screenId, instant, push: true);
        }

        /// <summary>
        /// The one real navigation entry point. <paramref name="push"/> = false for BACK
        /// (<see cref="GoBack"/>) and nav-bar jumps (<see cref="NavigateToPillar"/>), which
        /// manage <see cref="_history"/> themselves.
        /// </summary>
        private void Navigate(ScreenId screenId, bool instant, bool push)
        {
            // gps_standalone_shell §D4 — the shell has no Home. REWRITE before the gates, so
            // every gate below judges the screen that is actually going to open, and so the
            // GPS first-entry intercept further down sees GpsHub rather than a screen that
            // does not exist here. No-op in every build without GOLFIN_STANDALONE.
            //
            // A rewrite rather than a refusal because Home is the "sane default" of a dozen
            // call sites — the Welcome tutorial's SKIP, the hub's BackPill fallback, every
            // GoBack whose history ran dry. Refusing those strands the player; rewriting them
            // makes the shell's root mean what the game's root meant.
            ScreenId requested = screenId;
            screenId = StandaloneGate.Rewrite(screenId);
            if (screenId != requested)
                Debug.Log($"[StandaloneGate] rewrote {requested} -> {screenId} (the shell has no Home).");

            // Demo gate (demo_build_slice §3.2): deny-by-default screen allowlist.
            // No-op outside a GOLFIN_DEMO build.
            if (!DemoGate.IsScreenAllowed(screenId))
            {
                Debug.Log($"[DemoGate] blocked {screenId}");
                return;
            }

            // GPS gate (punch_it_gps_variants): the GPS surface is unreachable in a "punch it"
            // build (no GOLFIN_GPS). No-op in the Editor and in "punch it GPS" builds.
            if (!Golfin.Gps.UI.GpsGate.IsScreenAllowed(screenId))
            {
                Debug.Log($"[GpsGate] blocked {screenId}");
                return;
            }

            // Standalone shell gate (gps_standalone_shell §D4): the PLAYLIFE variant carries the
            // pre-auth screens and the GPS surface and nothing else. An ALLOWLIST, so a golf
            // screen added later cannot quietly appear in a golf-free product. No-op in every
            // build without GOLFIN_STANDALONE.
            if (!StandaloneGate.IsScreenAllowed(screenId))
            {
                Debug.Log($"[StandaloneGate] blocked {screenId} — not part of the PLAYLIFE shell.");
                return;
            }

            // Hard sign-in gate (points_cutover_followups item 3): no session, no post-auth screen.
            // Redirect rather than dead-end — a blocked navigation with nowhere to go would strand
            // the player on whatever screen they were already on with no explanation.
            if (!AuthGate.IsScreenAllowed(screenId))
            {
                Debug.LogWarning($"[AuthGate] blocked {screenId} — not signed in. Routing to Login.");
                Navigate(ScreenId.Login, instant, push);
                return;
            }

            // gps_profile_prompt_on_entry §2 — the ONE post-signup intercept. The first entry into
            // the GPS surface, from wherever (the Home pill, the home_promo banner's golfin://gps
            // internal route, later the standalone shell), is diverted once into the Golf Profile
            // capture. It sits here, after the gates, so the decision is only ever taken on a
            // navigation that was actually going to happen — and it re-enters Navigate rather than
            // rewriting screenId in place, so GpsGolfProfile is put through the three gates on its
            // own account instead of inheriting GpsHub's verdict. Same shape as the AuthGate
            // redirect above. No recursion risk: only GpsHub is ever intercepted.
            // gps_profile_prompt_server_flag §3 — the offer is once per ACCOUNT, so on a device
            // that has never answered the decision needs the server's word. Hold this navigation
            // for ONE /user/detail (bounded; see AccountFlagBudgetSeconds) and re-enter, rather
            // than guessing — guessing wrong means asking a player who already answered on their
            // other app, which is the whole defect. False for every other navigation in the game,
            // and false the moment this device has a local flag, so it costs one branch.
            if (Golfin.Gps.UI.GpsAuthExtrasFlow.NeedsAccountCheck(screenId))
            {
                Debug.Log($"[ScreenManager] {screenId} — first entry on this install, resolving the " +
                          $"account's Golf Profile flag before deciding.");
                Golfin.Gps.UI.GpsAuthExtrasFlow.EnsureAccountFlagThen(
                    () => Navigate(screenId, instant, push));
                return;
            }

            ScreenId intercepted = Golfin.Gps.UI.GpsAuthExtrasFlow.InterceptHubEntry(screenId);
            if (intercepted != screenId)
            {
                Debug.Log($"[ScreenManager] gps_profile_prompt_on_entry — first GPS entry, " +
                          $"{screenId} -> {intercepted} (Golf Profile offered once).");
                Golfin.Gps.UI.GpsAuthExtrasFlow.PendingHubEntry = true;
                Navigate(intercepted, instant, push);
                return;
            }

            Debug.Log($"[ScreenManager] ShowScreen called: {screenId} (current: {_currentScreen}, instant: {instant})");

            if (_currentScreen == screenId && !instant)
            {
                Debug.Log($"[ScreenManager] Already on {screenId}, ignoring");
                return;
            }

            // nav_back_memory §2 — history bookkeeping, BEFORE the swap so _currentScreen is
            // still the screen being left. A forward push inside one pillar stacks; anything
            // else (pillar change, or leaving the shell for Loading/Login/gameplay) resets,
            // because a lateral or hard-boundary move has no meaningful "back".
            if (push && _currentScreen != screenId)
            {
                if (IsShell(_currentScreen) && IsShell(screenId) && SamePillar(_currentScreen, screenId))
                {
                    _history.Add(_currentScreen);
                    if (_history.Count > HistoryCap) _history.RemoveAt(0);
                }
                else
                {
                    _history.Clear();
                }
            }

            // gps_polish §D2 — a push already in flight is finished INSTANTLY (rest state written,
            // its deferred ApplyScreen run) before anything else starts. No queue: the player who
            // taps two nav slots in 200 ms gets the second screen, not both animations in order.
            if (Golfin.Gps.UI.GpsScreenTransition.IsPushing)
                Golfin.Gps.UI.GpsScreenTransition.CompleteActiveNow();

            // If no fade system, or caller requests instant, just swap
            if (instant || FadeController.Instance == null)
            {
                Debug.Log($"[ScreenManager] Applying screen immediately: {screenId}");
                ApplyScreen(screenId);
                return;
            }

            // gps_polish §D2 — the ONE branch. Both ends inside the GPS surface, both prefabs
            // carrying the Background / ContentContainer split, and motion on: the screens push
            // laterally instead of going through black. Everything else — Home ↔ GpsHub, any GPS
            // screen to Login or Loading, ScoreUpload in either direction — falls through to the
            // untouched FadeController path below, which is the game-wide boundary convention.
            GameObject? fromGo = GpsScreenObject(_currentScreen);
            GameObject? toGo   = GpsScreenObject(screenId);
            if (Golfin.Gps.UI.GpsScreenTransition.CanPush(_currentScreen, screenId, fromGo, toGo)
                && isActiveAndEnabled && fromGo != null && toGo != null)
            {
                var dir = Golfin.Gps.UI.GpsScreenTransition.DirectionFor(_currentScreen, screenId, push);
                ScreenId target = screenId;
                StartCoroutine(Golfin.Gps.UI.GpsScreenTransition.Push(
                    fromGo, toGo, dir, () => ApplyScreen(target)));
                return;
            }

            Debug.Log($"[ScreenManager] Fading to {screenId}");
            // Fade to black, swap at midpoint, fade back in
            FadeController.Instance.FadeOutThenIn(() => ApplyScreen(screenId));
        }

        /// <summary>
        /// The screen GameObject for a GPS <see cref="ScreenId"/>, or null for anything else.
        ///
        /// <para>Deliberately NOT a general id → GameObject map. <see cref="ApplyScreen"/> is a
        /// flat wall of <c>SetActive</c> calls, several of which do more than toggle (Roster
        /// switches starter mode from the same branch), and a general accessor would invite
        /// callers to reach past that logic. This one answers the single question the GPS push
        /// asks — "which two objects am I sliding?" — and returns null the moment the id leaves
        /// the surface <see cref="Golfin.Gps.UI.GpsGate.IsGpsScreen"/> defines.</para>
        /// </summary>
        private GameObject? GpsScreenObject(ScreenId id)
        {
            switch (id)
            {
                case ScreenId.GpsHub:         return _gpsHubScreen;
                case ScreenId.ScoreUpload:    return _scoreUploadScreen;
                case ScreenId.GpsProfile:     return _gpsProfileScreen;
                case ScreenId.GpsAvatar:      return _gpsAvatarScreen;
                case ScreenId.GpsBadges:      return _gpsBadgesScreen;
                case ScreenId.GpsGolfProfile: return _gpsGolfProfileScreen;
                case ScreenId.GpsWelcome:     return _gpsWelcomeScreen;
                case ScreenId.GpsGift:        return _gpsGiftScreen;
                case ScreenId.GpsVote:        return _gpsVoteScreen;
                case ScreenId.GpsRounds:      return _gpsRoundsScreen;
                default:                      return null;
            }
        }

        // ── nav_back_memory §1 — pillar model ────────────────────────────────────────

        /// <summary>
        /// Which bottom-nav pillar a shell screen belongs to, or null when the screen is not a
        /// pillar screen (Logo/Splash/Loading, the account gate, the starter picker) or has no
        /// nav slot at all (Leaderboard — see <see cref="IsShell"/>).
        /// Single source of truth: PersistentUIManager.HighlightScreen calls this.
        /// </summary>
        public static Golfin.UI.PersistentUIManager.Screen? PillarOf(ScreenId id)
        {
            switch (id)
            {
                case ScreenId.Home:
                    return Golfin.UI.PersistentUIManager.Screen.Home;

                case ScreenId.Roster:
                // Order 517 — Shop screens entered from Roster; keep Characters nav tab highlighted
                case ScreenId.StaminaShopSelection:
                case ScreenId.StaminaShopDetail:
                    return Golfin.UI.PersistentUIManager.Screen.Characters;

                case ScreenId.Inventory:
                    return Golfin.UI.PersistentUIManager.Screen.Inventory;

                case ScreenId.HoleSelection:
                case ScreenId.ModeSelection:
                // missions_v1 §C2 — Missions is entered from the PLAY pillar, so the
                // same nav slot stays lit as it does for Practice and Tournaments.
                case ScreenId.MissionSelection:
                case ScreenId.TournamentSelection:
                case ScreenId.TournamentHoleSelection:
                case ScreenId.TournamentLeaderboard:
                    return Golfin.UI.PersistentUIManager.Screen.MainPlay;

                // Order 610 — Rewards Center opened from the Gacha nav slot; History and
                // Prizes are reached only from it, so the Gacha slot stays lit on all three.
                case ScreenId.GeneralShop:
                case ScreenId.GachaHistory:
                case ScreenId.GachaPrizes:
                    return Golfin.UI.PersistentUIManager.Screen.Gacha;

                default:
                    // Logo/Splash/Loading, auth screens, StartingCharacterSelection, and
                    // Leaderboard (no nav slot — it rides the history stack instead).
                    return null;
            }
        }

        /// <summary>The screen a pillar's nav slot opens when there is nothing remembered.</summary>
        public static ScreenId RootOf(Golfin.UI.PersistentUIManager.Screen pillar)
        {
            switch (pillar)
            {
                case Golfin.UI.PersistentUIManager.Screen.Characters: return ScreenId.Roster;
                case Golfin.UI.PersistentUIManager.Screen.Inventory:  return ScreenId.Inventory;
                // Bottom-nav tee button → Mode Select screen (mode_select_system spec)
                case Golfin.UI.PersistentUIManager.Screen.MainPlay:   return ScreenId.ModeSelection;
                // Order 610 — the Gacha nav slot opens the Rewards Center hub.
                case Golfin.UI.PersistentUIManager.Screen.Gacha:      return ScreenId.GeneralShop;
                default:                                              return ScreenId.Home;
            }
        }

        /// <summary>A shell screen — one that shows the persistent bars and can enter history.</summary>
        public static bool IsShell(ScreenId id) => PillarOf(id) != null || id == ScreenId.Leaderboard;

        /// <summary>
        /// Leaderboard has no pillar but is reachable from three of them, so it counts as
        /// "same pillar" as whatever opened it — that is what lets BACK return there.
        /// </summary>
        private static bool SamePillar(ScreenId a, ScreenId b)
        {
            if (a == ScreenId.Leaderboard || b == ScreenId.Leaderboard) return true;
            return PillarOf(a) == PillarOf(b);
        }

        // ── nav_back_memory §2 — BACK and nav-bar jumps ──────────────────────────────

        /// <summary>
        /// BACK. Pops the most recent same-pillar screen; when the stack is empty falls back to
        /// <paramref name="fallback"/> (the screen's serialized target), then the pillar root,
        /// then Home. Entries that are no longer reachable (DemoGate / AuthGate) are skipped.
        /// Returns false when there was nowhere to go (already on the Home root).
        /// </summary>
        public bool GoBack(ScreenId? fallback = null, bool instant = false)
        {
            while (_history.Count > 0)
            {
                ScreenId candidate = _history[_history.Count - 1];
                _history.RemoveAt(_history.Count - 1);

                if (candidate == _currentScreen) continue;
                if (!DemoGate.IsScreenAllowed(candidate)) continue;
                if (!Golfin.Gps.UI.GpsGate.IsScreenAllowed(candidate)) continue;
                // gps_standalone_shell §D4 — a golf screen left on the stack by a shared code
                // path is not somewhere BACK may land in the shell.
                if (!StandaloneGate.IsScreenAllowed(candidate)) continue;
                if (!AuthGate.IsScreenAllowed(candidate)) continue;

                Navigate(candidate, instant, push: false);
                return true;
            }

            ScreenId target;
            if (fallback.HasValue && fallback.Value != _currentScreen)
            {
                target = fallback.Value;
            }
            else
            {
                var pillar = PillarOf(_currentScreen);
                ScreenId root = pillar.HasValue ? RootOf(pillar.Value) : ScreenId.Home;
                // On a pillar root there is nothing above to fall back to except Home.
                target = (root == _currentScreen) ? ScreenId.Home : root;
            }

            // gps_standalone_shell §D4 — resolve the shell's rewrite HERE as well as in Navigate.
            // Every fallback above can name Home, and the no-op test below compares against
            // _currentScreen: on the hub, an unrewritten Home would compare unequal, reach
            // Navigate, be rewritten to the screen we are already on, and report a BACK that
            // never happened. Rewriting first makes "the hub root has nowhere to go" true.
            target = StandaloneGate.Rewrite(target);

            if (target == _currentScreen) return false;   // Home root: BACK is a no-op, never a quit.

            Navigate(target, instant, push: false);
            return true;
        }

        /// <summary>
        /// Bottom-nav slot tap (nav_back_memory D1). The pillar you are already in → its root
        /// (iOS tab-bar convention). A different pillar → the screen you were last on inside it,
        /// or its root the first time. Never a forward push; the history stack resets.
        /// </summary>
        public void NavigateToPillar(Golfin.UI.PersistentUIManager.Screen pillar)
        {
            var current = PillarOf(_currentScreen);

            ScreenId target;
            if (current.HasValue && current.Value == pillar) target = RootOf(pillar);
            else if (_lastInPillar.TryGetValue(pillar, out ScreenId remembered)) target = remembered;
            else target = RootOf(pillar);

            _history.Clear();
            Navigate(target, instant: false, push: false);
        }

        // ── nav_back_memory §7 — Android hardware / gesture back ─────────────────────

        private void Update()
        {
            if (!BackPressedThisFrame()) return;

            // Modals own their own dismissal (backdrop tap / CANCEL).
            if (Golfin.UI.Modals.ModalController.OpenModalCount > 0) return;

            // Settings is an overlay that leaves the screen enabled underneath it.
            var settings = Golfin.UI.SettingsController.Instance;
            if (settings != null && settings.IsOpen)
            {
                settings.CloseSettings();
                return;
            }

            // Gameplay, the auth gate and Loading are not ours — in-game back is handled by the
            // in-game settings modal (SPEC D2 / § Out of scope).
            if (!IsShell(_currentScreen)) return;

            // Never Application.Quit(): on the Home root GoBack returns false and nothing happens.
            GoBack();
        }

        /// <summary>
        /// ProjectSettings.activeInputHandler == 1 (Input System package only), so the legacy
        /// UnityEngine.Input path would throw at runtime. Unity surfaces the Android hardware /
        /// gesture back button as Keyboard.escapeKey. Fully qualified so no new using is needed.
        /// </summary>
        private static bool BackPressedThisFrame()
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            return keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
        }

        /// <summary>
        /// Actually activates/deactivates screen GameObjects.
        /// </summary>
        private void ApplyScreen(ScreenId screenId)
        {
            Debug.Log($"[ScreenManager] ApplyScreen: {screenId}");
            _currentScreen = screenId;

            // nav_back_memory §2 — per-pillar memory. Non-pillar screens (Leaderboard, the
            // auth gate, Loading) are deliberately never remembered as a nav-slot destination.
            var pillarOfScreen = PillarOf(screenId);
            if (pillarOfScreen.HasValue) _lastInPillar[pillarOfScreen.Value] = screenId;

            if (_logoScreen != null) _logoScreen.SetActive(screenId == ScreenId.Logo);
            if (_splashScreen != null) _splashScreen.SetActive(screenId == ScreenId.Splash);
            if (_loadingScreen != null) _loadingScreen.SetActive(screenId == ScreenId.Loading);
            if (_homeScreen != null) _homeScreen.SetActive(screenId == ScreenId.Home);
            
            if (_rosterScreen != null)
            {
                bool shouldBeActive = (screenId == ScreenId.Roster || screenId == ScreenId.StartingCharacterSelection);
                Debug.Log($"[ScreenManager] RosterScreen: {(shouldBeActive ? "ACTIVATING" : "deactivating")}");
                _rosterScreen.SetActive(shouldBeActive);
                // Notify RosterScreenController about which mode to use
                var rosterCtrl = _rosterScreen.GetComponentInChildren<Golfin.Roster.RosterScreenController>(includeInactive: true);
                if (rosterCtrl != null)
                    rosterCtrl.SetStarterMode(screenId == ScreenId.StartingCharacterSelection);
            }
            else
            {
                Debug.LogWarning("[ScreenManager] _rosterScreen is NULL!");
            }

            if (_inventoryScreen != null)
                _inventoryScreen.SetActive(screenId == ScreenId.Inventory);

            if (_holeSelectionScreen != null)
                _holeSelectionScreen.SetActive(screenId == ScreenId.HoleSelection);

            if (_modeSelectionScreen != null)
                _modeSelectionScreen.SetActive(screenId == ScreenId.ModeSelection);

            if (_missionSelectionScreen != null)
                _missionSelectionScreen.SetActive(screenId == ScreenId.MissionSelection);

            if (_leaderboardScreen != null)
                _leaderboardScreen.SetActive(screenId == ScreenId.Leaderboard);

            if (_tournamentHoleSelectionScreen != null)
                _tournamentHoleSelectionScreen.SetActive(screenId == ScreenId.TournamentHoleSelection);

            if (_tournamentLeaderboardScreen != null)
                _tournamentLeaderboardScreen.SetActive(screenId == ScreenId.TournamentLeaderboard);

            if (_tournamentSelectionScreen != null)
                _tournamentSelectionScreen.SetActive(screenId == ScreenId.TournamentSelection);

            // Order 517 — Stamina Boost Shop screens
            if (_staminaShopSelectionScreen != null)
                _staminaShopSelectionScreen.SetActive(screenId == ScreenId.StaminaShopSelection);
            if (_staminaShopDetailScreen != null)
                _staminaShopDetailScreen.SetActive(screenId == ScreenId.StaminaShopDetail);

            // Order 610 — General Shop / Rewards Center
            if (_generalShopScreen != null)
                _generalShopScreen.SetActive(screenId == ScreenId.GeneralShop);

            // Gacha pillar screen 2 — Gacha History
            if (_gachaHistoryScreen != null)
                _gachaHistoryScreen.SetActive(screenId == ScreenId.GachaHistory);

            // Gacha pillar screen 3 — Gacha Prizes
            if (_gachaPrizesScreen != null)
                _gachaPrizesScreen.SetActive(screenId == ScreenId.GachaPrizes);

            // gps_hub_entry — GPS / PLAYLIFE hub
            if (_gpsHubScreen != null)
                _gpsHubScreen.SetActive(screenId == ScreenId.GpsHub);

            // score_upload_flow — the six-step score upload
            if (_scoreUploadScreen != null)
                _scoreUploadScreen.SetActive(screenId == ScreenId.ScoreUpload);
            // gps_profile_pack — three GPS sub-screens
            if (_gpsProfileScreen != null)
                _gpsProfileScreen.SetActive(screenId == ScreenId.GpsProfile);
            if (_gpsAvatarScreen != null)
                _gpsAvatarScreen.SetActive(screenId == ScreenId.GpsAvatar);
            if (_gpsBadgesScreen != null)
                _gpsBadgesScreen.SetActive(screenId == ScreenId.GpsBadges);
            // auth_golf_profile — post-signup capture + welcome tutorial
            if (_gpsGolfProfileScreen != null)
                _gpsGolfProfileScreen.SetActive(screenId == ScreenId.GpsGolfProfile);
            if (_gpsWelcomeScreen != null)
                _gpsWelcomeScreen.SetActive(screenId == ScreenId.GpsWelcome);
            // gps_gifts_votes — Gift + Vote
            if (_gpsGiftScreen != null)
                _gpsGiftScreen.SetActive(screenId == ScreenId.GpsGift);
            if (_gpsVoteScreen != null)
                _gpsVoteScreen.SetActive(screenId == ScreenId.GpsVote);
            if (_gpsRoundsScreen != null)
                _gpsRoundsScreen.SetActive(screenId == ScreenId.GpsRounds);


            // Order: login_signup_screens — account auth gate (excluded from showBars)
            if (_loginScreen != null)
                _loginScreen.SetActive(screenId == ScreenId.Login);
            if (_createUsernameScreen != null)
                _createUsernameScreen.SetActive(screenId == ScreenId.CreateUsername);
            if (_signUpScreen != null)
                _signUpScreen.SetActive(screenId == ScreenId.SignUp);
            if (_emailConfirmationScreen != null)
                _emailConfirmationScreen.SetActive(screenId == ScreenId.EmailConfirmation);
            if (_resetPasswordScreen != null)
                _resetPasswordScreen.SetActive(screenId == ScreenId.ResetPassword);

            // Settings is an overlay (SettingsController), not managed here

            // Order 350: menu music — starts on the Splash gate and runs unbroken through the
            // auth/starter screens into Home and every other shell screen. Deny-list, not an
            // allow-list: the old allow-list started the theme on Home only, so the title screen
            // was silent and every new shell screen had to remember to opt in. Silent on Logo (the
            // pre-title brand card) and Loading (the gameplay hand-off, which is where the theme
            // must stop — GameplaySceneLoader shows Loading on its way into a hole).
            // AudioManager is DDOL and lives in Assembly-CSharp, same as ScreenManager.
            bool isMenuScreen = screenId != ScreenId.Logo
                             && screenId != ScreenId.Loading;
            if (AudioManager.Instance != null)
            {
                if (isMenuScreen && _mainThemeClip != null && !AudioManager.Instance.IsMusicPlaying())
                    AudioManager.Instance.PlayMusic(_mainThemeClip, loop: true);
                else if (!isMenuScreen && AudioManager.Instance.IsMusicPlaying())
                    AudioManager.Instance.StopMusic();
            }

            // Show persistent bars on Home, Roster, Inventory, HoleSelection, ModeSelection, Leaderboard; hide on Logo/Splash/Loading
            bool showBars = screenId == ScreenId.Home
                         || screenId == ScreenId.Roster
                         || screenId == ScreenId.Inventory
                         || screenId == ScreenId.HoleSelection
                         || screenId == ScreenId.ModeSelection
                         || screenId == ScreenId.MissionSelection
                         || screenId == ScreenId.Leaderboard
                         || screenId == ScreenId.TournamentHoleSelection
                         || screenId == ScreenId.TournamentLeaderboard
                         || screenId == ScreenId.TournamentSelection
                         || screenId == ScreenId.StaminaShopSelection
                         || screenId == ScreenId.StaminaShopDetail
                         || screenId == ScreenId.GeneralShop
                         // Gacha pillar screens 2 and 3. Both REUSE the shared bars per their
                         // specs (gacha_history §L2.1/L2.3, gacha_prizes §L2/L4) — their own
                         // TopUI / NavBarContainer children are empty placeholders — so leaving
                         // them out of this list rendered both screens with no top bar and no
                         // navbar, which is what the player saw from the History chip.
                         || screenId == ScreenId.GachaHistory
                         || screenId == ScreenId.GachaPrizes;
            // Account / auth screens reuse the shared top bar for their title only
            // (banner + centered title, no bottom nav or logged-in chrome).
            bool isAccountScreen = screenId == ScreenId.Login
                                || screenId == ScreenId.CreateUsername
                                || screenId == ScreenId.SignUp
                                || screenId == ScreenId.EmailConfirmation
                                || screenId == ScreenId.ResetPassword;
            // SPEC decision 6: starter selection shows top bar (RP + gear) but hides bottom nav.
            bool isStarterScreen = screenId == ScreenId.StartingCharacterSelection;
            // gps_hub_entry §4 — the GPS hub takes the same shape for a different reason: the top
            // bar is shared (RP + gear + the "GOLFIN GPS" title), and the SHARED bottom nav is
            // hidden because the hub draws its own GPS nav bar inside its prefab. Showing both
            // would stack two nav bars at the bottom of one screen.
            // score_upload_flow joins it for the same reason — one group, not two rules.
            // punch_it_gps_variants — the five-way OR moved into GpsGate so the chrome rule and
            // the reachability deny-list are the same list; a GPS screen added to one is added to
            // both, and they cannot drift.
            bool isGpsScreen = Golfin.Gps.UI.GpsGate.IsGpsScreen(screenId);

            if (Golfin.UI.PersistentUIManager.Instance != null)
            {
                if (isAccountScreen)
                {
                    // Localized: these were the last hardcoded English strings on the account
                    // screens, so a Japanese player saw a fully translated card under an
                    // English banner.
                    string accountTitle = (screenId == ScreenId.SignUp || screenId == ScreenId.EmailConfirmation)
                        ? LocalizationManager.Get("NAV_SIGN_UP")
                        : LocalizationManager.Get("NAV_GOLFIN_ACCOUNT");
                    Golfin.UI.PersistentUIManager.Instance.ShowAccountTitleBar(accountTitle);
                }
                else if (isStarterScreen)
                {
                    // Top bar visible (RP balance + gear); bottom nav hidden (replaced by instruction block).
                    Golfin.UI.PersistentUIManager.Instance.ShowTopBarOnly();
                }
                else if (isGpsScreen)
                {
                    // HighlightScreen is what resolves the centre title through NavTitleKeyFor;
                    // it returns early on a screen with no bottom-nav pillar, which both of these
                    // are, AFTER the title has already been applied. Passing screenId (not a
                    // hardcoded GpsHub) is what gives ScoreUpload its own title.
                    Golfin.UI.PersistentUIManager.Instance.ShowTopBarOnly();
                    Golfin.UI.PersistentUIManager.Instance.HighlightScreen(screenId);
                }
                else if (showBars)
                {
                    Golfin.UI.PersistentUIManager.Instance.ShowBars();
                    Golfin.UI.PersistentUIManager.Instance.HighlightScreen(screenId);
                }
                else
                {
                    Golfin.UI.PersistentUIManager.Instance.HideBars();
                }
            }

            // S1 — notify TournamentResultPresenter (and any other listeners) that the
            // active screen has changed. Fired AFTER all SetActive calls and _currentScreen update.
            ScreenChanged?.Invoke(screenId);
        }
    }
}