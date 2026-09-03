// auth_golf_profile — real-navigation capture + funnel smoke for the two auth-extras screens.
//
// REAL NAVIGATION, NOT A RENDER HARNESS (FIGMA_SCREEN_BUILD_PLAYBOOK §0). A preview-scene render
// gave two false readings on gps_profile_pack in twenty minutes: raw localization keys (because
// LocalizationManager is only Initialize()d at boot) and a background swap that silently did
// nothing (because the screen prefab paints its own Background child). Everything here boots the
// app, taps through the Splash gate the way DevAutoSignIn does, and drives the REAL widgets'
// onClick — so what is captured is what a player sees.
#nullable enable
using System.Collections;
using System.IO;
using Golfin.Diagnostics.Runtime;
using GolfinRedux.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.Gps.UI.Editor
{
    public static class GpsAuthExtrasEditorRun
    {
        const string OutDir = "Docs/Specs/Active/auth_golf_profile/screenshots";

        /// <summary>
        /// Armed across the play-mode DOMAIN RELOAD. Entering play mode reloads the domain, which
        /// resets every static and drops every <c>EditorApplication.update</c> subscription — so a
        /// hook registered next to <c>EnterPlaymode()</c> is gone before play mode starts, and the
        /// run silently never happens. EditorPrefs survives the reload; <see cref="Hook"/> re-arms
        /// on the other side of it.
        /// </summary>
        const string ArmedKey = "golfin.authextras.capture.armed";

        /// <summary>The backend round trip mutates the dev account's profiles row (and
        /// restores it). It is proven once; leave it off for pure re-capture passes so a
        /// visual iteration does not churn a live row each time.</summary>
        public static bool RunRoundTrip = false;

        [MenuItem("GOLFIN/Gps/Run Auth Extras Capture", priority = 213)]
        public static void Run()
        {
            Directory.CreateDirectory(OutDir);
            // The whole point is that the trigger fires by itself on the first Home entry, so the
            // flag is cleared here rather than the screen being shown directly.
            GpsAuthExtrasFlow.ClearPrompted();
            Application.runInBackground = true;   // else the capture returns the splash frame
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

        /// <summary>
        /// Poll until play mode is actually running, then spawn the runner exactly once. A
        /// <c>delayCall</c> here would race Unity's own scene restore
        /// (reference_initializeonload_delaycall_races_scene_restore); polling update does not.
        /// </summary>
        static void Pump()
        {
            if (!Application.isPlaying) return;
            if (_spawned) { EditorApplication.update -= Pump; return; }
            _spawned = true;
            EditorPrefs.SetBool(ArmedKey, false);   // one run per arm
            EditorApplication.update -= Pump;
            var host = new GameObject("[AuthExtrasCapture]");
            Object.DontDestroyOnLoad(host);
            host.AddComponent<Runner>();
        }

        sealed class Runner : MonoBehaviour
        {
            void Start() => StartCoroutine(Sequence());

            static void Log(string s) => Debug.Log("[AuthExtrasCapture] " + s);

            IEnumerator Sequence()
            {
                Application.runInBackground = true;

                // ── 1. Through the Splash gate. DevAutoSignIn taps StartButton for us, so this
                //       wait usually expires with the button already gone — that is NORMAL and
                //       must not abort. Gate on the NEXT screen, never on this click.
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

                // ── 2. HARD GATE on the real Golf Profile screen appearing BY ITSELF. If the
                //       Home trigger did not fire, this run must fail loudly rather than
                //       screenshot whatever happens to be on screen.
                t = 0f;
                while (t < 60f && ScreenManager.Instance?.CurrentScreen != ScreenId.GpsGolfProfile)
                {
                    t += Time.unscaledDeltaTime;
                    yield return null;
                }
                if (ScreenManager.Instance?.CurrentScreen != ScreenId.GpsGolfProfile)
                {
                    Debug.LogError("[AuthExtrasCapture] ABORT — the Home trigger never routed to "
                        + "GpsGolfProfile (currentScreen="
                        + ScreenManager.Instance?.CurrentScreen + "). Nothing captured.");
                    EditorApplication.isPlaying = false;
                    yield break;
                }
                Log("Home trigger routed to GpsGolfProfile on its own.");

                yield return Settle(5f);
                Save("golf_profile_default", "01_golf_profile_default.png");

                // ── 3. Selection states: pick a different colour + chip, and type a nickname so
                //       the swatch initial has something to draw.
                var gp = FindFirstObjectByType<GpsGolfProfileScreenController>(FindObjectsInactive.Include);
                var input = Find<TMP_InputField>(gp, "NicknameInput");
                if (input != null) { input.text = "Misaki"; }
                Click(gp, "Colour3");                        // gold
                Click(gp, "Chip2");                          // ADVANCED
                var hc = Find<TMP_InputField>(gp, "HandicapInput");
                if (hc != null) hc.text = "18.4";
                yield return Settle(2f);
                Save("golf_profile_filled", "02_golf_profile_filled.png");

                // ── 4. Error state — the duplicate/invalid treatment the node has no mock for.
                if (hc != null) hc.text = "abc";
                Click(gp, "SaveProfileButtonRow/SaveProfileButton");
                yield return Settle(2f);
                Save("golf_profile_error", "03_golf_profile_error.png");
                if (hc != null) hc.text = "18.4";

                // ── 5. SKIP writes nothing. The absence of a PUT in the log is the evidence.
                Log("=== SKIP: expect NO 'PUT https://.../user/update' between here and the next marker ===");
                Click(gp, "SkipRow");
                yield return Settle(4f);
                Log("=== SKIP END ===");

                if (ScreenManager.Instance?.CurrentScreen != ScreenId.GpsWelcome)
                    Debug.LogError("[AuthExtrasCapture] Skip did not reach GpsWelcome (currentScreen="
                        + ScreenManager.Instance?.CurrentScreen + ")");
                else
                    Log("Skip -> GpsWelcome OK; flag now set = " + GpsAuthExtrasFlow.Prompted);

                Save("welcome", "04_welcome.png");

                // ── 6. GET STARTED lands on the hub.
                var wc = FindFirstObjectByType<GpsWelcomeScreenController>(FindObjectsInactive.Include);
                Click(wc, "GetStartedButtonRow/GetStartedButton");
                yield return Settle(4f);
                Log("GET STARTED -> currentScreen=" + ScreenManager.Instance?.CurrentScreen
                    + " (expect GpsHub)");
                Save("hub_after_get_started", "05_hub_after_get_started.png");

                // ── 7. The Profile hero disc in a NON-DEFAULT colour (SPEC §5), through the REAL
                //       data path: a real PUT, then let the screen fetch it back.
                //
                //       Poking LastDetail.AvatarColor does NOT work and is worth recording: the
                //       screen paints from cache on enable and then fires its own /user/detail,
                //       which overwrites the poked value with the server's within a frame or two.
                //       The first attempt did exactly that and captured the gold fallback while
                //       claiming to show pink. The colour has to be on the ROW.
                var svcP = Golfin.Social.UserService.Instance;
                string nameP = svcP.LastDetail?.DisplayName ?? "Player";
                yield return svcP.Update(nameP, null, null, "pink", golfProfilePrompted: null, onResult: r =>
                    Log("avatar_color='pink' PUT -> HTTP " + r.StatusCode));

                ScreenManager.Instance?.ShowScreen(ScreenId.GpsProfile);
                yield return Settle(6f);
                Log("hero disc now reflects avatar_color=" + (svcP.LastDetail?.AvatarColor ?? "null"));
                Save("profile_avatar_pink", "06_profile_avatar_pink.png");

                // ── 8. Second Home entry must NOT re-offer.
                ScreenManager.Instance?.ShowScreen(ScreenId.Home);
                yield return Settle(4f);
                Log("second Home entry -> currentScreen=" + ScreenManager.Instance?.CurrentScreen
                    + " (expect Home, i.e. NO re-offer). ShouldOffer=" + GpsAuthExtrasFlow.ShouldOffer());

                // ── 9. BACKEND ROUND TRIP against the DEPLOYED playlife-api. This is the
                //       acceptance evidence for the endpoint change: a real PUT over the real
                //       ApiClient with the real session, then a fresh GET that must echo all
                //       three new fields back. Originals are restored at the end so the dev
                //       account is left as it was found.
                if (RunRoundTrip) yield return RoundTrip();

                Log("DONE");
                EditorApplication.isPlaying = false;
            }

            IEnumerator RoundTrip()
            {
                var svc = Golfin.Social.UserService.Instance;
                var api = Golfin.Net.ApiClient.Instance;

                Golfin.Social.UserDetailDto? before = null;
                yield return svc.Detail(r => { if (r.Success) before = r.Data; });
                if (before == null) { Debug.LogError("[AuthExtrasCapture] RT: GET /user/detail failed"); yield break; }
                Log("RT before: name=" + before.DisplayName + " hc=" + before.Handicap
                    + " exp=" + (before.GolfExperience ?? "null") + " colour=" + (before.AvatarColor ?? "null"));

                string name = before.DisplayName;

                // (a) the happy path — all three new fields in one PUT.
                long put = 0; string putErr = "";
                yield return svc.Update(name, 18.4, "advanced", "pink", golfProfilePrompted: null, onResult: r =>
                { put = r.StatusCode; putErr = r.Success ? "" : (r.ErrorKind + ": " + r.ErrorMessage); });
                Log("RT PUT /user/update (hc=18.4, exp=advanced, colour=pink) -> HTTP " + put + " " + putErr);

                // (b) a FRESH GET, not the PUT's own echo — the column has to have landed.
                Golfin.Social.UserDetailDto? after = null;
                yield return svc.Detail(r => { if (r.Success) after = r.Data; });
                Log("RT after GET /user/detail: hc=" + after?.Handicap
                    + " golf_experience=" + (after?.GolfExperience ?? "null")
                    + " avatar_color=" + (after?.AvatarColor ?? "null")
                    + "  => " + ((after != null && after.Handicap == 18.4
                                  && after.GolfExperience == "advanced"
                                  && after.AvatarColor == "pink") ? "ROUND TRIP PASS" : "ROUND TRIP FAIL"));

                // (c) the enum guard — a value outside the CHECK must be a 422 the client can
                //     read, not a 500 from the database.
                long bad = 0;
                yield return svc.Update(name, null, "pro", null, golfProfilePrompted: null, onResult: r => { bad = r.StatusCode; });
                Log("RT PUT golf_experience='pro' -> HTTP " + bad + " (expect 422)");

                long badColour = 0;
                yield return svc.Update(name, null, null, "teal", golfProfilePrompted: null, onResult: r => { badColour = r.StatusCode; });
                Log("RT PUT avatar_color='teal' -> HTTP " + badColour + " (expect 422)");

                // (d) an OMITTED field must not blank a stored one.
                yield return svc.Update(name, null, null, null, golfProfilePrompted: null, onResult: _ => { });
                Golfin.Social.UserDetailDto? kept = null;
                yield return svc.Detail(r => { if (r.Success) kept = r.Data; });
                Log("RT omit-all PUT then GET: exp=" + (kept?.GolfExperience ?? "null")
                    + " colour=" + (kept?.AvatarColor ?? "null") + " hc=" + kept?.Handicap
                    + "  => " + ((kept?.GolfExperience == "advanced" && kept?.AvatarColor == "pink")
                                 ? "OMIT-PRESERVES PASS" : "OMIT-PRESERVES FAIL"));

                // (e) restore — AND THE LIMIT OF WHAT RESTORE CAN MEAN HERE.
                //
                // If the row started with these fields NULL, this endpoint CANNOT put them back:
                // step (d) just proved that an omitted field is preserved, and there is no way to
                // send "make this null" — null and omitted are the same thing on the wire. That is
                // a deliberate property (a partial body must never blank what it does not
                // mention), not a bug, but it means CLEARING a value needs either a direct write
                // or a future explicit-null contract. Worth knowing before the Settings screen
                // that "you can change all of this later" promises gets written.
                yield return svc.Update(name, before.Handicap, before.GolfExperience,
                                        before.AvatarColor, golfProfilePrompted: null, onResult: r =>
                    Log("RT restore attempt -> HTTP " + r.StatusCode
                        + (before.GolfExperience == null
                           ? "  NOTE: the row started NULL, and PUT /user/update cannot write NULL"
                             + " (omitted == preserved, proven at step d) — clear it directly if"
                             + " the account must be left untouched."
                           : "")));
                if (api == null) yield break;
            }

            IEnumerator Settle(float seconds)
            {
                float t = 0f;
                while (t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
                Canvas.ForceUpdateCanvases();
                yield return new WaitForEndOfFrame();
            }

            static void Save(string label, string fileName)
            {
                // CaptureCore.SnapPlayModeSafe is the sanctioned play-mode path: synchronous,
                // no AssetDatabase.Refresh (which would domain-reload and kill this coroutine),
                // no pause. It has been caught returning a path it never wrote AND returning a
                // byte-identical stale frame for a different state, so BOTH are asserted here.
                string src = CaptureCore.SnapPlayModeSafe(label);
                if (string.IsNullOrEmpty(src) || !File.Exists(src))
                {
                    Debug.LogError("[AuthExtrasCapture] capture FAILED for " + label
                        + " (path='" + src + "', exists=" + File.Exists(src ?? "") + ")");
                    return;
                }
                string dst = Path.Combine(OutDir, fileName);
                File.Copy(src, dst, true);
                Log("saved " + dst + " (" + new FileInfo(dst).Length + " bytes) from " + src);
            }

            static T? Find<T>(Component? root, string path) where T : Component
            {
                if (root == null) return null;
                foreach (var c in root.GetComponentsInChildren<T>(true))
                    if (c.gameObject.name == path || c.transform.parent?.name == path) return c;
                return null;
            }

            static void Click(Component? root, string path)
            {
                if (root == null) { Debug.LogError("[AuthExtrasCapture] no controller for " + path); return; }
                var t = FindDeep(root.transform, path);
                var b = t == null ? null : t.GetComponent<Button>();
                if (b == null) { Debug.LogError("[AuthExtrasCapture] no Button at " + path); return; }
                // The REAL widget's own onClick — PIPELINE_HARDENING rule 2. No test-only button.
                b.onClick.Invoke();
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
