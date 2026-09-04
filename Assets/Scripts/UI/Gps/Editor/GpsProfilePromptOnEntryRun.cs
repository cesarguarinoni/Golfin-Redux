// gps_profile_prompt_on_entry — real-navigation acceptance run for the moved trigger.
//
// WHAT IT PROVES, and why each half needs the app actually running: that Home comes up and STAYS
// (an assertion about something NOT happening, which only a live Home entry can make), and that
// the first tap of the REAL GPS pill diverts into the Golf Profile capture (PIPELINE_HARDENING
// rule 2 — the widget's own onClick, never a test-only button). Both are decisions taken inside
// ScreenManager.Navigate against the live session, so neither is reachable from EditMode: the
// EditMode suite pins the pure table, this pins the wiring.
//
// Same shape as GpsAuthExtrasEditorRun (boot → Splash gate → real onClick → CaptureCore), because
// that run is the one that already survived the render-harness lesson.
#nullable enable
using System.Collections;
using System.IO;
using System.Reflection;
using Golfin.Banners;
using Golfin.Diagnostics.Runtime;
using GolfinRedux.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.Gps.UI.Editor
{
    public static class GpsProfilePromptOnEntryRun
    {
        const string OutDir = "Docs/Specs/Active/gps_profile_prompt_on_entry/screenshots";
        const string ArmedKey = "golfin.gpspromptonentry.armed";

        [MenuItem("GOLFIN/Gps/Run Profile Prompt On Entry Acceptance", priority = 214)]
        public static void Run()
        {
            Directory.CreateDirectory(OutDir);
            // A fresh install, in the only sense the Editor can offer one: the flag unset.
            GpsAuthExtrasFlow.ClearPrompted();
            Application.runInBackground = true;   // else every capture returns the splash frame
            EditorPrefs.SetBool(ArmedKey, true);
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        static void Hook()
        {
            if (!EditorPrefs.GetBool(ArmedKey, false)) return;
            EditorApplication.update += Pump;
        }

        static bool _spawned;

        /// <summary>Poll rather than delayCall — a delayCall races Unity's own scene restore.</summary>
        static void Pump()
        {
            if (!Application.isPlaying) return;
            if (_spawned) { EditorApplication.update -= Pump; return; }
            _spawned = true;
            EditorPrefs.SetBool(ArmedKey, false);
            EditorApplication.update -= Pump;
            var host = new GameObject("[GpsPromptOnEntry]");
            Object.DontDestroyOnLoad(host);
            host.AddComponent<Runner>();
        }

        sealed class Runner : MonoBehaviour
        {
            void Start() => StartCoroutine(Sequence());

            static void Log(string s) => Debug.Log("[PromptOnEntry] " + s);
            static void Fail(string s) => Debug.LogError("[PromptOnEntry] FAIL — " + s);

            static ScreenId? Now => ScreenManager.Instance?.CurrentScreen;

            IEnumerator Sequence()
            {
                Application.runInBackground = true;

                // ── 1. Through the Splash gate. DevAutoSignIn usually taps StartButton first;
                //       that is normal, so gate on the next screen and never on this click.
                float t = 0f;
                while (t < 15f)
                {
                    var splash = FindFirstObjectByType<SplashScreenController>();
                    var btn = splash == null ? null : splash.transform.Find("StartButton");
                    if (btn != null && btn.gameObject.activeInHierarchy)
                    {
                        btn.GetComponent<Button>()?.onClick.Invoke();
                        Log("tapped StartButton");
                        break;
                    }
                    t += Time.unscaledDeltaTime;
                    yield return null;
                }

                // ── 2. ACCEPTANCE 1 — Home comes up and STAYS. Reaching Home is not enough: the
                //       old behaviour reached Home too and left one frame later. So watch it for
                //       8 s and fail on the first frame that is not Home.
                t = 0f;
                while (t < 45f && Now != ScreenId.Home) { t += Time.unscaledDeltaTime; yield return null; }
                if (Now != ScreenId.Home) { Fail("never reached Home (currentScreen=" + Now + ")"); yield break; }
                Log("reached Home. ShouldOffer=" + GpsAuthExtrasFlow.ShouldOffer()
                    + " (TRUE means the offer is live and armed — Home must still not take it)");

                t = 0f; bool stayed = true;
                while (t < 8f)
                {
                    if (Now != ScreenId.Home)
                    {
                        Fail("Home did not STAY — drifted to " + Now + " after " + t.ToString("F2") + "s");
                        stayed = false; break;
                    }
                    t += Time.unscaledDeltaTime;
                    yield return null;
                }
                if (!stayed) yield break;
                Log("ACCEPT 1 PASS — Home held for 8.0 s with the offer armed; no GpsGolfProfile.");
                yield return Settle(1f);
                Save("home_stays", "01_home_stays.png");

                // ── 3. ACCEPTANCE 2 — the REAL pill. Reached through HomeScreenController's own
                //       serialized reference, so this is the same Button object the player's
                //       finger lands on, with its own onClick listener list.
                var pill = RealPill();
                if (pill == null) { Fail("could not resolve HomeScreenController.gpsPillButton"); yield break; }
                Log("tapping the REAL pill: " + Path(pill.transform)
                    + " (activeInHierarchy=" + pill.gameObject.activeInHierarchy + ")");
                pill.onClick.Invoke();

                yield return WaitFor(ScreenId.GpsGolfProfile, 20f);
                if (Now != ScreenId.GpsGolfProfile)
                { Fail("pill tap did not divert to GpsGolfProfile (currentScreen=" + Now + ")"); yield break; }
                Log("ACCEPT 2 PASS — pill -> GpsGolfProfile (the intercept fired on the first GPS entry).");
                yield return Settle(4f);
                Save("golf_profile_via_pill", "02_golf_profile_via_pill.png");

                // ── 4. SAVE -> Welcome -> GET STARTED -> hub. SAVE writes the account's OWN
                //       display name back with no other change, so the acceptance path is the
                //       real one (a live PUT /user/update) without mutating the dev row.
                var gp = FindFirstObjectByType<GpsGolfProfileScreenController>(FindObjectsInactive.Include);
                Click(gp, "SaveProfileButtonRow/SaveProfileButton");
                yield return WaitFor(ScreenId.GpsWelcome, 25f);
                if (Now != ScreenId.GpsWelcome) { Fail("SAVE did not reach GpsWelcome (currentScreen=" + Now + ")"); yield break; }
                Log("SAVE -> GpsWelcome OK; prompted flag now = " + GpsAuthExtrasFlow.Prompted
                    + "; PendingHubEntry=" + GpsAuthExtrasFlow.PendingHubEntry + " (expect True, still in the chain)");
                yield return Settle(3f);
                Save("welcome", "03_welcome.png");

                var wc = FindFirstObjectByType<GpsWelcomeScreenController>(FindObjectsInactive.Include);
                Click(wc, "GetStartedButtonRow/GetStartedButton");
                yield return WaitFor(ScreenId.GpsHub, 20f);
                if (Now != ScreenId.GpsHub) { Fail("GET STARTED did not reach GpsHub (currentScreen=" + Now + ")"); yield break; }
                Log("ACCEPT 2 PASS (cont.) — GET STARTED -> GpsHub; PendingHubEntry cleared = "
                    + (!GpsAuthExtrasFlow.PendingHubEntry));
                yield return Settle(4f);
                Save("hub_after_get_started", "04_hub_after_get_started.png");

                // ── 5. SECOND entry goes straight to the hub. Home first, so this is a real
                //       Home -> pill tap again and not a hub-to-hub no-op.
                ScreenManager.Instance?.ShowScreen(ScreenId.Home);
                yield return WaitFor(ScreenId.Home, 20f);
                yield return Settle(2f);
                var pill2 = RealPill();
                pill2?.onClick.Invoke();
                yield return WaitFor(ScreenId.GpsHub, 20f);
                Log("second pill tap -> currentScreen=" + Now + " (expect GpsHub, NOT GpsGolfProfile). "
                    + "ShouldOffer=" + GpsAuthExtrasFlow.ShouldOffer());
                if (Now != ScreenId.GpsHub) { Fail("second pill tap did not land on the hub"); yield break; }
                Log("ACCEPT 3 PASS — the offer is spent; the pill is an ordinary hub entry.");
                yield return Settle(3f);
                Save("hub_direct_second_entry", "05_hub_direct_second_entry.png");

                // ── 6. SKIP path, from a cleared flag: Golf Profile Skip -> Welcome -> Skip -> Home.
                GpsAuthExtrasFlow.ClearPrompted();
                ScreenManager.Instance?.ShowScreen(ScreenId.Home);
                yield return WaitFor(ScreenId.Home, 20f);
                yield return Settle(2f);
                RealPill()?.onClick.Invoke();
                yield return WaitFor(ScreenId.GpsGolfProfile, 20f);
                if (Now != ScreenId.GpsGolfProfile) { Fail("re-armed pill tap did not offer again (currentScreen=" + Now + ")"); yield break; }

                var gp2 = FindFirstObjectByType<GpsGolfProfileScreenController>(FindObjectsInactive.Include);
                Click(gp2, "SkipRow");
                yield return WaitFor(ScreenId.GpsWelcome, 20f);
                if (Now != ScreenId.GpsWelcome) { Fail("Skip did not reach GpsWelcome (currentScreen=" + Now + ")"); yield break; }
                Log("Skip -> GpsWelcome OK; prompted flag now = " + GpsAuthExtrasFlow.Prompted);

                var wc2 = FindFirstObjectByType<GpsWelcomeScreenController>(FindObjectsInactive.Include);
                Click(wc2, "SkipRow");
                yield return WaitFor(ScreenId.Home, 20f);
                if (Now != ScreenId.Home) { Fail("Welcome Skip did not return Home (currentScreen=" + Now + ")"); yield break; }
                Log("ACCEPT 4 PASS — Skip -> Welcome -> Skip -> Home; PendingHubEntry cleared = "
                    + (!GpsAuthExtrasFlow.PendingHubEntry));
                yield return Settle(2f);
                RealPill()?.onClick.Invoke();
                yield return WaitFor(ScreenId.GpsHub, 20f);
                Log("ACCEPT 4 PASS (cont.) — next pill tap after Skip -> " + Now + " (expect GpsHub)");
                if (Now != ScreenId.GpsHub) { Fail("after Skip the pill did not go straight to the hub"); yield break; }

                // ── 7. The home_promo deep link. Two halves, each proven where it lives: the
                //       banner binder's ONLY action for an internal link is
                //       ShowScreen(TryGetInternalRoute(link)), so resolving the route and then
                //       driving that navigation IS the banner path — there is no third step.
                GpsAuthExtrasFlow.ClearPrompted();
                ScreenManager.Instance?.ShowScreen(ScreenId.Home);
                yield return WaitFor(ScreenId.Home, 20f);
                yield return Settle(2f);

                bool routed = BannerPolicy.TryGetInternalRoute("golfin://gps", out ScreenId deep);
                Log("deep link: BannerPolicy.TryGetInternalRoute(\"golfin://gps\") -> " + routed + ", " + deep);
                if (!routed || deep != ScreenId.GpsHub) { Fail("golfin://gps no longer resolves to GpsHub"); yield break; }

                ScreenManager.Instance?.ShowScreen(deep);   // exactly what BannerSlotBinder.OpenLink does
                yield return WaitFor(ScreenId.GpsGolfProfile, 20f);
                Log("ACCEPT 5 — golfin://gps with the flag cleared -> currentScreen=" + Now
                    + " (expect GpsGolfProfile)");
                if (Now != ScreenId.GpsGolfProfile) { Fail("the deep link bypassed the intercept"); yield break; }
                Log("ACCEPT 5 PASS — the banner deep link is covered by the same Navigate intercept.");
                yield return Settle(3f);
                Save("deep_link_offers", "06_deep_link_offers.png");

                // Leave the device in the state a played-through player would have.
                GpsAuthExtrasFlow.MarkPrompted();
                GpsAuthExtrasFlow.PendingHubEntry = false;
                Log("ALL ACCEPTANCE CHECKS PASS. DONE");
                EditorApplication.isPlaying = false;
            }

            // ── helpers ──────────────────────────────────────────────────────────────────────

            /// <summary>
            /// The pill the PLAYER taps, not one found by name: read straight off
            /// HomeScreenController's serialized field, so a renamed GameObject cannot make this
            /// run silently drive some other Button.
            /// </summary>
            static Button? RealPill()
            {
                var home = FindFirstObjectByType<HomeScreenController>(FindObjectsInactive.Include);
                if (home == null) return null;
                var f = typeof(HomeScreenController).GetField("gpsPillButton",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                return f?.GetValue(home) as Button;
            }

            static string Path(Transform t)
            {
                string p = t.name;
                while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
                return p;
            }

            IEnumerator WaitFor(ScreenId target, float seconds)
            {
                float t = 0f;
                while (t < seconds && Now != target) { t += Time.unscaledDeltaTime; yield return null; }
            }

            IEnumerator Settle(float seconds)
            {
                float t = 0f;
                while (t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
                Canvas.ForceUpdateCanvases();
                yield return new WaitForEndOfFrame();
            }

            /// <summary>
            /// CaptureCore.SnapPlayModeSafe is the sanctioned play-mode path (synchronous, no
            /// AssetDatabase.Refresh — which would domain-reload and kill this coroutine). It has
            /// been caught returning a path it never wrote AND a byte-identical stale frame for a
            /// different state, so existence and size are both logged.
            /// </summary>
            static void Save(string label, string fileName)
            {
                string src = CaptureCore.SnapPlayModeSafe(label);
                if (string.IsNullOrEmpty(src) || !File.Exists(src))
                {
                    Fail("capture for " + label + " (path='" + src + "', exists="
                         + File.Exists(src ?? "") + ")");
                    return;
                }
                string dst = System.IO.Path.Combine(OutDir, fileName);
                File.Copy(src, dst, true);
                Log("saved " + dst + " (" + new FileInfo(dst).Length + " bytes) from " + src);
            }

            static void Click(Component? root, string path)
            {
                if (root == null) { Fail("no controller for " + path); return; }
                var t = FindDeep(root.transform, path);
                var b = t == null ? null : t.GetComponent<Button>();
                if (b == null) { Fail("no Button at " + path); return; }
                b.onClick.Invoke();   // the REAL widget's own onClick — PIPELINE_HARDENING rule 2
                Log("clicked " + path);
            }

            static Transform? FindDeep(Transform root, string path)
            {
                var direct = root.Find(path);
                if (direct != null) return direct;
                foreach (Transform child in root)
                {
                    var hit = FindDeep(child, path);
                    if (hit != null) return hit;
                }
                return null;
            }
        }
    }
}
