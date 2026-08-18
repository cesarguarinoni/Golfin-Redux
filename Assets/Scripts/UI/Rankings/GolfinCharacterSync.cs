// ─────────────────────────────────────────────────────────────────────────────
// UI/Rankings — GolfinCharacterSync (leaderboard_backend SPEC §5)
//
// Tells the backend which character the player is showing, so OTHER players see
// the right portrait and level next to their name on the shared board.
//
// COSMETIC, THEREFORE SILENT. A failed sync costs a stale portrait on someone
// else's leaderboard. It never blocks, never retries beyond ApiClient's own
// transient budget, never surfaces UI, and never touches the character itself —
// this is a one-way push of two fields the player already changed locally.
//
// Lives next to the provider (SPEC §5 offers Net/ or here) because it reads
// CharacterManager, which is an Assembly-CSharp type an asmdef cannot see. The
// same split as ServerBalanceSyncBehaviour: the RULE is a pure static below so a
// test can exercise the throttle without a scene.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System.Collections;
using Golfin.Auth;
using Golfin.Net;
using Golfin.Roster;
using Newtonsoft.Json;
using UnityEngine;

namespace Golfin.UI.Rankings
{
    /// <summary>The throttle rule, extracted so it is testable without a scene or a socket.</summary>
    public static class GolfinCharacterSyncPolicy
    {
        /// <summary>
        /// The PUT body, or null when there is nothing worth sending. An empty character id is the
        /// pre-roster state (and a guaranteed 400 from the server), not something to push.
        /// </summary>
        public static string? BuildPayload(string? characterId, int level)
        {
            if (string.IsNullOrWhiteSpace(characterId)) return null;

            return JsonConvert.SerializeObject(new GolfinCharacterSyncDto
            {
                CharacterId = characterId!.Trim(),
                // Server clamps to 1–999; clamping the floor here keeps an unloaded 0 off the wire.
                Level = level < 1 ? 1 : level
            });
        }

        /// <summary>
        /// Send only when the payload actually differs from the last one this session.
        ///
        /// <c>OnCharacterSelected</c> fires on every carousel commit and <c>OnCharacterLeveledUp</c>
        /// fires once per level — a player spending a stack of RP would otherwise emit one request per
        /// level, all but the last of them already obsolete on arrival.
        /// </summary>
        public static bool ShouldSend(string? payload, string? lastSentPayload)
            => !string.IsNullOrEmpty(payload) && payload != lastSentPayload;
    }

    public sealed class GolfinCharacterSync : MonoBehaviour
    {
        private const string Tag = "[GolfinCharacterSync]";

        private static GolfinCharacterSync? _instance;

        /// <summary>The last body actually put on the wire. Session-scoped on purpose: a fresh launch
        /// re-asserts the character once, which is how a sync lost to a dead network heals.</summary>
        private string? _lastSentPayload;

        private CharacterManager? _subscribedTo;

        /// <summary>
        /// Self-bootstrapping, like <c>NetCoroutineRunner</c> and <c>ServerBalanceSyncBehaviour</c> —
        /// SPEC §4 forbids scene/prefab edits, and there is no natural owner for this on any screen.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;

            var go = new GameObject("[GolfinCharacterSync]");
            _instance = go.AddComponent<GolfinCharacterSync>();
            DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            AuthService.SignedIn += OnSignedIn;
            StartCoroutine(SubscribeToRosterWhenReady());
        }

        private void OnDisable()
        {
            AuthService.SignedIn -= OnSignedIn;
            UnsubscribeFromRoster();
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        // ── Subscriptions ─────────────────────────────────────────────────────

        /// <summary>
        /// <c>CharacterManager</c> is a scene singleton that may not exist yet at
        /// <c>AfterSceneLoad</c>, and is rebuilt across scene loads, so the subscription is polled
        /// into place rather than assumed. One frame per poll, and it stops as soon as it binds.
        /// </summary>
        private IEnumerator SubscribeToRosterWhenReady()
        {
            while (_subscribedTo == null)
            {
                CharacterManager manager = CharacterManager.Instance;
                if (manager != null)
                {
                    manager.OnCharacterSelected  += OnCharacterChanged;
                    manager.OnCharacterLeveledUp += OnCharacterChanged;
                    _subscribedTo = manager;

                    // A returning player is already signed in and already has a selection, so neither
                    // event will ever fire for them — assert the current state once on bind.
                    Push("startup");
                    yield break;
                }
                yield return null;
            }
        }

        private void UnsubscribeFromRoster()
        {
            if (_subscribedTo == null) return;
            _subscribedTo.OnCharacterSelected  -= OnCharacterChanged;
            _subscribedTo.OnCharacterLeveledUp -= OnCharacterChanged;
            _subscribedTo = null;
        }

        // ── Triggers (SPEC §5) ────────────────────────────────────────────────

        private void OnSignedIn(AuthSession session) => Push("sign-in");

        private void OnCharacterChanged(string characterId) => Push("character");

        // ── Push ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Fire-and-forget PUT of {character_id, level}. Every early return here is a normal state,
        /// not an error: no roster yet, nothing changed, or a session that must not reach production.
        /// </summary>
        private void Push(string why)
        {
            // The SAME gate the board itself uses (SPEC §4): a bot run carries a fake token and must
            // never write to the live profile, and a signed-out player has nothing to write with.
            bool botOverride = false;
#if UNITY_EDITOR || GOLFIN_BOT_HARNESS
            botOverride = Golfin.Dev.BotSessionOverride.Active;
#endif
            bool signedIn = Application.isPlaying
                            && AuthService.Instance != null
                            && AuthService.Instance.Session != null
                            && AuthService.Instance.Session.IsAuthenticated;

            if (LeaderboardProviderPolicy.Choose(botOverride, signedIn) != LeaderboardProviderKind.Backend)
                return;

            CharacterManager manager = CharacterManager.Instance;
            if (manager == null) return;

            string characterId = manager.GetSelectedCharacterId();
            int level = 1;
            if (!string.IsNullOrEmpty(characterId))
            {
                PlayerCharacterData? pcd = manager.GetPlayerCharacter(characterId);
                if (pcd != null) level = pcd.currentLevel;
            }

            string? payload = GolfinCharacterSyncPolicy.BuildPayload(characterId, level);
            if (!GolfinCharacterSyncPolicy.ShouldSend(payload, _lastSentPayload)) return;

            _lastSentPayload = payload;

            ApiClient.Instance.Run(ApiClient.Instance.Put<string>(
                Endpoints.UserGolfinCharacter, payload!, result =>
                {
                    if (result.Success) return;

                    // Let the next trigger retry from scratch rather than leaving the throttle holding
                    // a payload the server never received.
                    _lastSentPayload = null;
                    Debug.LogWarning($"{Tag} {why} sync failed ({result.ErrorKind}, HTTP {result.StatusCode}): " +
                                     $"{result.ErrorMessage}. The leaderboard portrait may be stale.");
                }));
        }
    }
}
