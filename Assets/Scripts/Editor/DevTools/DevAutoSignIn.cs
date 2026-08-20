// ─────────────────────────────────────────────────────────────────────────────
// DevAutoSignIn — EDITOR ONLY
//
// Signs the Editor into GOLFIN on play-mode entry, and taps past the Splash
// "tap to start" gate, so a play session lands on Home without hand-holding.
// Exists because the auth session lives in PlayerPrefs and expires; every time it
// did, an automated capture silently recorded the login screen instead of the game.
//
// ─── IT NEVER STORES OR PRINTS A CREDENTIAL ─────────────────────────────────
// Credentials are read at run time from ONE of two places the developer controls:
//
//   1. Environment variables:  GOLFIN_DEV_EMAIL  /  GOLFIN_DEV_PASSWORD
//   2. A gitignored JSON file at the PROJECT ROOT (never under Assets/, so Unity
//      does not import it and it cannot end up in a build):
//
//          <project>/.golfin-dev-login.json
//          { "email": "...", "password": "..." }
//
// Nothing is copied into the project, into EditorPrefs, or into any log. The
// password is held in a local for the duration of one SignInWithPassword call.
// Log lines report only WHICH source was used and whether it succeeded.
//
// ─── IT CANNOT SHIP ─────────────────────────────────────────────────────────
// The whole file is inside `#if UNITY_EDITOR` and lives in an Editor-only folder,
// so it is not compiled into a player build. See `reference_editor_only_seams`.
//
// Toggle: GOLFIN > Dev > Auto Sign-In (Editor Only)
// ─────────────────────────────────────────────────────────────────────────────
#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Golfin.Auth;

namespace Golfin.EditorTools
{
    /// <summary>Held only for the duration of one sign-in call; never persisted or logged.</summary>
    internal struct DevCredentials { public string Email; public string Password; }

    [InitializeOnLoad]
    public static class DevAutoSignIn
    {
        private const string EnabledKey  = "Golfin.DevAutoSignIn.Enabled";
        private const string CredFile    = ".golfin-dev-login.json";
        private const string EnvEmail    = "GOLFIN_DEV_EMAIL";
        private const string EnvPassword = "GOLFIN_DEV_PASSWORD";

        public static bool Enabled
        {
            get => EditorPrefs.GetBool(EnabledKey, true);
            set => EditorPrefs.SetBool(EnabledKey, value);
        }

        static DevAutoSignIn()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        // ── Menu ──────────────────────────────────────────────────────────────

        [MenuItem("GOLFIN/Dev/Auto Sign-In (Editor Only)")]
        private static void Toggle() => Enabled = !Enabled;

        [MenuItem("GOLFIN/Dev/Auto Sign-In (Editor Only)", true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked("GOLFIN/Dev/Auto Sign-In (Editor Only)", Enabled);
            return true;
        }

        /// <summary>Reports where credentials would come from, WITHOUT revealing them.</summary>
        [MenuItem("GOLFIN/Dev/Check Auto Sign-In Setup")]
        private static void CheckSetup()
        {
            var (creds, source) = TryLoadCredentials();
            if (creds.HasValue)
                Debug.Log($"[DevAutoSignIn] Ready — credentials found via {source}. " +
                          $"Auto sign-in is {(Enabled ? "ENABLED" : "disabled")}.");
            else
                Debug.LogWarning(
                    "[DevAutoSignIn] No credentials found. Set them up one of two ways:\n" +
                    $"  • env vars {EnvEmail} / {EnvPassword}, or\n" +
                    $"  • a gitignored file at <project>/{CredFile} containing " +
                    "{\"email\":\"...\",\"password\":\"...\"}\n" +
                    "Neither is read by anything except this Editor-only script, and neither is logged.");
        }

        // ── Credential loading ────────────────────────────────────────────────

        /// <summary>
        /// Returns credentials + the NAME of the source (never the values). Env wins over the
        /// file so a machine can override a checked-out working copy without editing anything.
        /// </summary>
        internal static (DevCredentials? creds, string source) TryLoadCredentials()
        {
            string envEmail = Environment.GetEnvironmentVariable(EnvEmail);
            string envPass  = Environment.GetEnvironmentVariable(EnvPassword);
            if (!string.IsNullOrEmpty(envEmail) && !string.IsNullOrEmpty(envPass))
                return (new DevCredentials { Email = envEmail, Password = envPass }, "environment variables");

            // Project root = the folder containing Assets/.
            string root = Path.GetDirectoryName(Application.dataPath);
            string path = Path.Combine(root ?? ".", CredFile);
            if (!File.Exists(path)) return (null, "nothing");

            try
            {
                var parsed = JsonUtility.FromJson<CredFileShape>(File.ReadAllText(path));
                if (parsed == null || string.IsNullOrEmpty(parsed.email) || string.IsNullOrEmpty(parsed.password))
                {
                    Debug.LogWarning($"[DevAutoSignIn] {CredFile} exists but is missing 'email' or 'password'.");
                    return (null, "nothing");
                }
                return (new DevCredentials { Email = parsed.email, Password = parsed.password }, CredFile);
            }
            catch (Exception e)
            {
                // Deliberately does not echo the file body.
                Debug.LogWarning($"[DevAutoSignIn] Could not parse {CredFile}: {e.GetType().Name}");
                return (null, "nothing");
            }
        }

        [Serializable] private class CredFileShape { public string email; public string password; }

        // ── Play-mode hook ────────────────────────────────────────────────────

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !Enabled) return;

            var host = new GameObject("[DevAutoSignIn]") { hideFlags = HideFlags.HideAndDontSave };
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<DevAutoSignInRunner>().Begin(TryLoadCredentials);
        }
    }

    /// <summary>
    /// Runtime half: waits for AuthService, signs in if needed, then taps the Splash gate.
    /// Lives in the Editor assembly and is only ever spawned from the play-mode hook.
    /// </summary>
    internal class DevAutoSignInRunner : MonoBehaviour
    {
        internal void Begin(Func<(DevCredentials? creds, string source)> loader)
        {
            StartCoroutine(Run(loader));
        }

        private System.Collections.IEnumerator Run(Func<(DevCredentials? creds, string source)> loader)
        {
            // 1. Wait for AuthService to exist and finish its own bootstrap/refresh.
            float waited = 0f;
            while (AuthService.Instance == null && waited < 10f)
            { yield return new WaitForSecondsRealtime(0.2f); waited += 0.2f; }

            var auth = AuthService.Instance;
            if (auth == null)
            {
                Debug.LogWarning("[DevAutoSignIn] AuthService never appeared — skipping.");
                Destroy(gameObject); yield break;
            }

            // Give the normal session-refresh path a moment; if it works we must not interfere.
            yield return new WaitForSecondsRealtime(2.0f);

            if (auth.Session != null && auth.Session.IsAuthenticated)
            {
                Debug.Log("[DevAutoSignIn] Existing session is valid — no sign-in needed.");
            }
            else
            {
                var (creds, source) = loader();

                if (!creds.HasValue)
                {
                    Debug.LogWarning(
                        "[DevAutoSignIn] Not signed in and no credentials configured — " +
                        "run GOLFIN > Dev > Check Auto Sign-In Setup for how to set them.");
                    Destroy(gameObject); yield break;
                }

                string email    = creds.Value.Email;
                string password = creds.Value.Password;

                bool done = false, ok = false; string msg = null;
                Debug.Log($"[DevAutoSignIn] Signing in (credentials from {source}) …");
                auth.SignInWithPassword(email, password, r => { ok = r != null && r.Success; msg = r?.Message; done = true; });
                email = null; password = null;   // drop the references immediately

                waited = 0f;
                while (!done && waited < 20f) { yield return new WaitForSecondsRealtime(0.2f); waited += 0.2f; }

                // msg can echo a server error; it never contains the password.
                Debug.Log(ok
                    ? "[DevAutoSignIn] Signed in."
                    : $"[DevAutoSignIn] Sign-in failed ({(done ? msg : "timed out")}).");
                if (!ok) { Destroy(gameObject); yield break; }
            }

            // 2. Tap past the Splash gate. Even an authenticated session parks here until
            //    StartButton fires — that button IS the refresh-and-route-to-Home entry point.
            waited = 0f;
            while (waited < 15f)
            {
                var splash = FindFirstObjectByType<GolfinRedux.UI.SplashScreenController>();
                if (splash != null && splash.gameObject.activeInHierarchy)
                {
                    var tf = splash.transform.Find("StartButton");
                    var btn = tf != null ? tf.GetComponent<Button>() : null;
                    if (btn != null && btn.gameObject.activeInHierarchy)
                    {
                        Debug.Log("[DevAutoSignIn] Tapping Splash StartButton → Home.");
                        btn.onClick.Invoke();
                        break;
                    }
                }
                yield return new WaitForSecondsRealtime(0.25f);
                waited += 0.25f;
            }

            Destroy(gameObject);
        }
    }
}
#endif
