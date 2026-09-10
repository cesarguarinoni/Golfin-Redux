// ─────────────────────────────────────────────────────────────────────────────
// asset_loans_offers §3.4 — the ONE acceptance line a stub cannot prove.
//
// "The Settings toggle persists across relaunch." A stubbed transport that echoes a fixed
// row proves nothing about persistence: it would report the same value whatever the server
// held. So this runs against the LIVE API with the real DevAutoSignIn session, and the
// "relaunch" is the play-mode cycle itself — entering play wipes UserService.LastDetail
// (a plain field on a non-persistent singleton), so whatever the toggle paints on the way
// in came from a cold GET /user/detail and nowhere else.
//
// NO STUB IS INSTALLED. That is the entire point, and it is why this is a separate runner
// rather than a mode on LoanUiCaptureBot: that bot's first act is to replace the transport.
//
// GOLFIN ▸ Loans ▸ Verify Settings Toggle (LIVE)
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System.Collections;
using System.Linq;
using Golfin.Social;
using Golfin.UI.Loans;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.UI.Loans.EditorTools
{
    public static class LoanSettingsLiveCheck
    {
        private const string ArmedKey = "LoanSettingsLiveCheck.Armed";

        [MenuItem("GOLFIN/Loans/Verify Settings Toggle (LIVE)")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) { Spawn(); return; }

            // Without this an unfocused Editor stops rendering and the screens never settle.
            Application.runInBackground = true;
            SessionState.SetBool(ArmedKey, true);
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void Hook() => EditorApplication.delayCall += SpawnIfArmed;

        private static void SpawnIfArmed()
        {
            if (!EditorApplication.isPlaying) return;
            if (!SessionState.GetBool(ArmedKey, false)) return;
            SessionState.SetBool(ArmedKey, false);
            Spawn();
        }

        private static void Spawn()
        {
            var go = new GameObject("[LoanSettingsLiveCheck]");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<LoanSettingsLiveRunner>();
        }
    }

    public sealed class LoanSettingsLiveRunner : MonoBehaviour
    {
        private IEnumerator Start()
        {
            Application.runInBackground = true;
            Debug.Log("[SettingsLive] waiting for boot + DevAutoSignIn…");
            yield return new WaitForSecondsRealtime(8f);

            // What does the SERVER say right now? Read it through the same service the UI uses,
            // so a difference between this and the toggle is a CLIENT bug, not a transport one.
            UserDetailDto? detail = null;
            bool done = false;
            UserService.Instance.EnsureDetail(_ => { detail = UserService.Instance.LastDetail; done = true; });
            float w = 0f;
            while (!done && w < 15f) { w += Time.unscaledDeltaTime; yield return null; }

            if (detail == null)
            {
                Debug.LogError("[SettingsLive] no profile row — not signed in? Cannot verify.");
                EditorApplication.isPlaying = false;
                yield break;
            }
            Debug.Log($"[SettingsLive] SERVER says golfin_loan_offers={detail.GolfinLoanOffers} " +
                      $"(AcceptsLoanOffers={detail.AcceptsLoanOffers}) for '{detail.DisplayName}'");

            // Open Settings the way a player does — the gear, then the accordion header.
            Button? gear = Resources.FindObjectsOfTypeAll<Button>()
                .FirstOrDefault(b => b != null && !string.IsNullOrEmpty(b.gameObject.scene.name)
                                  && b.gameObject.activeInHierarchy
                                  && (b.name.Contains("Settings") || b.name.Contains("Gear")));
            if (gear == null) { Debug.LogError("[SettingsLive] no gear button."); EditorApplication.isPlaying = false; yield break; }
            gear.onClick.Invoke();
            yield return new WaitForSecondsRealtime(1.2f);

            var row = Resources.FindObjectsOfTypeAll<Golfin.UI.SettingsMenuItem>()
                .FirstOrDefault(m => m != null && m.gameObject.name == "UserProfileRow");
            if (row == null) { Debug.LogError("[SettingsLive] no UserProfileRow."); EditorApplication.isPlaying = false; yield break; }
            row.ToggleExpansion();
            yield return new WaitForSecondsRealtime(1.6f);

            var toggle = Resources.FindObjectsOfTypeAll<LoanOffersToggle>()
                .FirstOrDefault(t => t != null && !string.IsNullOrEmpty(t.gameObject.scene.name));
            if (toggle == null) { Debug.LogError("[SettingsLive] no LoanOffersToggle."); EditorApplication.isPlaying = false; yield break; }

            bool serverSays = detail.AcceptsLoanOffers;
            bool uiSays = toggle.IsOn;
            Debug.Log($"[SettingsLive] RESULT  server={serverSays}  toggleUI={uiSays}  " +
                      $"MATCH={(serverSays == uiSays ? "YES ✅" : "NO ❌")}");

            // The knob is the visible half — assert its position agrees with the flag, so a
            // toggle whose bool is right and whose knob never moved still fails.
            var knob = toggle.transform.Find("Toggle/Knob") as RectTransform;
            var pill = toggle.transform.Find("Toggle")?.GetComponent<Image>();
            if (knob != null && pill != null)
                Debug.Log($"[SettingsLive] KNOB x={knob.anchoredPosition.x:F0} " +
                          $"(ON=58, OFF=6)   PILL=#{ColorUtility.ToHtmlStringRGB(pill.color)} " +
                          $"(ON=#2775DD, OFF=#38597F)");

            // ── PHASE 2 — the revert path, the one acceptance line a happy server cannot show ──
            //
            // "Reverts + toasts on a failed PUT." The knob moves OPTIMISTICALLY on tap, so a
            // write that fails must put it back — otherwise the player is looking at a setting
            // that says OFF while every other client can still offer to them, and nothing on
            // screen ever says so. Forced by pointing the API host at an address that cannot
            // answer, which exercises the real transport-failure branch rather than a mock.
            bool before = toggle.IsOn;
            string realHost = Golfin.Net.Endpoints.RootUrl;
            Golfin.Net.Endpoints.RootUrl = "https://127.0.0.1:9";   // discard port — connection refused
            Debug.Log($"[SettingsLive] PHASE2 host -> unreachable; toggle was {before}, tapping…");

            var togBtn = toggle.transform.Find("Toggle")?.GetComponent<Button>();
            if (togBtn == null) Debug.LogError("[SettingsLive] PHASE2: no toggle button.");
            else
            {
                togBtn.onClick.Invoke();
                // Long enough for the optimistic flip, the doomed request, and the revert.
                yield return new WaitForSecondsRealtime(6f);

                bool after = toggle.IsOn;
                var k2 = toggle.transform.Find("Toggle/Knob") as RectTransform;
                Debug.Log($"[SettingsLive] PHASE2 RESULT  before={before} after={after}  " +
                          $"REVERTED={(before == after ? "YES ✅" : "NO ❌ — the knob kept a lie")}" +
                          $"  knobX={(k2 != null ? k2.anchoredPosition.x.ToString("F0") : "?")}");

                // And the server must be untouched by a write that never landed.
                Golfin.Net.Endpoints.RootUrl = realHost;
                UserDetailDto? fresh = null; bool got = false;
                UserService.Instance.Detail(r => { fresh = r?.Data; got = true; });
                float w2 = 0f;
                while (!got && w2 < 15f) { w2 += Time.unscaledDeltaTime; yield return null; }
                Debug.Log($"[SettingsLive] PHASE2 server still says " +
                          $"{(fresh != null ? fresh.GolfinLoanOffers.ToString() : "<unread>")}  " +
                          $"(expect unchanged: {serverSays})");
            }
            Golfin.Net.Endpoints.RootUrl = realHost;

            yield return new WaitForSecondsRealtime(0.5f);
            EditorApplication.isPlaying = false;
        }
    }
}
