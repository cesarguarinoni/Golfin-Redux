#if UNITY_EDITOR
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Golfin.Physics.Viewer.Bot;

namespace Golfin.Missions.Harness.Editor
{
    /// <summary>
    /// Play today's DAILY through the real player path and let the real claim fire.
    ///
    /// WHY THIS EXISTS. The daily claim wiring had never run against prod: the endpoint
    /// shipped in Phase A with no sender, and the client half (Hole Complete parks the round
    /// on MissionSession.PendingDaily; Mission Selection claims it with the hash it holds)
    /// is only exercised by actually finishing a daily round.
    ///
    /// ⚠️ THIS RUN TOUCHES THE REAL LEDGER, AND THAT IS THE POINT. Every other bot harness
    /// arms Golfin.Dev.BotSessionOverride, which fakes the session and forces the points
    /// backend OFF precisely so a capture run cannot write. This one must NOT: it signs in as
    /// the real dev user through DevAutoSignIn so the claim lands in game_points_ledger. A
    /// daily is once-per-user-per-UTC-date, so a second run on the same date returns
    /// `already_claimed` and pays nothing — that is the server's lock, not a bug in the run.
    ///
    /// Entry is the REAL widget's onClick (PIPELINE_HARDENING rule 2): the mode card's Play,
    /// then the daily card's own action button, reached by reflection because the field is
    /// private — reading a private field is not the same as bypassing the button.
    /// </summary>
    public static class DailyClearHarness
    {
        private const string ArmedKey = "Golfin.DailyClearHarness.Armed";

        [MenuItem("GOLFIN/Missions/Play Today's Daily (REAL claim)")]
        static void Run()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[DailyClear] Exit play mode first.");
                return;
            }
            SessionState.SetBool(ArmedKey, true);
            EditorApplication.EnterPlaymode();
        }

        [MenuItem("GOLFIN/Missions/Play Today's Daily (REAL claim)", isValidateFunction: true)]
        static bool Validate() => !EditorApplication.isPlaying;

        [InitializeOnLoadMethod]
        static void Hook()
        {
            EditorApplication.playModeStateChanged -= OnPlayMode;
            EditorApplication.playModeStateChanged += OnPlayMode;
        }

        static void OnPlayMode(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            if (!SessionState.GetBool(ArmedKey, false)) return;
            SessionState.SetBool(ArmedKey, false);

            Application.runInBackground = true;   // else capture/updates stall when unfocused
            var go = new GameObject("[DailyClearHost]");
            Object.DontDestroyOnLoad(go);         // the gameplay scene load would kill the host
            go.AddComponent<DailyClearHost>();
            Debug.Log("[DailyClear] armed host created; NOT arming BotSessionOverride (real ledger).");
        }
    }

    public class DailyClearHost : MonoBehaviour
    {
        void Start() => StartCoroutine(Run());

        IEnumerator Run()
        {
            var bot = new BotDriver("Docs/Diagnostics/_capture/daily_clear");
            Debug.Log("[DailyClear] === begin ===");

            yield return bot.NavigateToHome(totalTimeoutSeconds: 90f);
            Debug.Log("[DailyClear] home reached");

            yield return bot.ClickModeCardPlay("missions", settleSeconds: 2.0f);
            yield return bot.WaitFor(() => FindSelection() != null, "MissionSelection present", 20f);

            var sel = FindSelection();
            if (sel == null) { Debug.LogError("[DailyClear] FAIL: no MissionSelectionScreenController"); yield break; }

            // Wait for the daily fetch to bind the card before touching its button.
            MissionCardHandle daily = default;
            yield return bot.WaitFor(() =>
            {
                daily = ReadDailyCard(sel);
                return daily.Button != null && daily.Button.interactable && daily.Go != null && daily.Go.activeInHierarchy;
            }, "daily card bound + interactable", 30f);

            daily = ReadDailyCard(sel);
            if (daily.Button == null)
            {
                Debug.LogError("[DailyClear] FAIL: daily card never became interactable (already claimed today?)");
                yield break;
            }

            Debug.Log("[DailyClear] invoking the daily card's REAL action button");
            daily.Button.onClick.Invoke();

            yield return bot.WaitForSceneLoaded("GameplayScene", timeoutSeconds: 60f);
            yield return new WaitForSeconds(3f);
            Debug.Log("[DailyClear] gameplay scene up — playing the hole");

            yield return bot.PlayHoleToCup(par: 4);
            Debug.Log("[DailyClear] PlayHoleToCup returned");

            // The modal evaluates the mission and parks PendingDaily; give it room.
            yield return new WaitForSeconds(4f);
            var pend = Golfin.Gameplay.Missions.MissionSession.PendingDaily;
            Debug.Log(pend == null
                ? "[DailyClear] PendingDaily is NULL (round did not register as a daily)"
                : $"[DailyClear] PendingDaily: id={pend.MissionId} strokes={pend.Strokes} cleared={pend.Cleared}");

            Debug.Log("[DailyClear] === bot log ===\n" + bot.Log);
            Debug.Log("[DailyClear] === end of scripted phase; returning to Missions claims it ===");
        }

        struct MissionCardHandle { public Button Button; public GameObject Go; }

        static MonoBehaviour FindSelection()
        {
            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (mb != null && mb.GetType().Name == "MissionSelectionScreenController" && mb.isActiveAndEnabled)
                    return mb;
            return null;
        }

        static MissionCardHandle ReadDailyCard(MonoBehaviour sel)
        {
            var f = sel.GetType().GetField("dailyCard", BindingFlags.NonPublic | BindingFlags.Instance);
            var card = f?.GetValue(sel) as MonoBehaviour;
            if (card == null) return default;
            var bf = card.GetType().GetField("actionButton", BindingFlags.NonPublic | BindingFlags.Instance);
            return new MissionCardHandle { Button = bf?.GetValue(card) as Button, Go = card.gameObject };
        }
    }
}
#endif
