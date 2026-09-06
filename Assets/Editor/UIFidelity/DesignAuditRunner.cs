// ─────────────────────────────────────────────────────────────────────────────
// DesignAuditRunner — the play-mode driver for `design_consistency_audit`.
//
// One command per pass, re-runnable, so a dump can be regenerated without a human
// clicking through the game. Navigation is REAL (PIPELINE_HARDENING rule 2): the app
// boots behind a Title/PLAY gate that ScreenManager does NOT manage, so ShowScreen()
// swaps screens BEHIND the gate and `CurrentScreen == target` is a FALSE POSITIVE on
// a frame still showing the splash. Every hop here is a real widget's own onClick.
//
// ARMED THROUGH SessionState + [InitializeOnLoad]: entering play mode domain-reloads,
// which wipes any delegate subscribed before the transition. An EditorApplication.update
// hook armed pre-transition simply ceases to exist and the run silently never starts.
//
// READS ONLY, with ONE deliberate exception: the §20 tripwire plants three defects on
// RUNTIME-ONLY instances to prove the dumper can see them. Play-mode edits to scene
// objects are discarded when play mode exits — nothing is saved, and A10 quotes
// `git status` clean either side.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System.Collections;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.EditorTools.UIFidelity
{
    [InitializeOnLoad]
    public static class DesignAuditRunner
    {
        const string ArmedKey = "DesignAuditRunner.Armed";
        const string ModeKey  = "DesignAuditRunner.Mode";

        /// <summary>TMP's default font asset — the one the audit's shape (i) hunts for.</summary>
        const string LiberationGuid = "8f586378b4e144a9851e7b34d9b748ee";

        static DesignAuditRunner() => EditorApplication.playModeStateChanged += OnPlayModeChanged;

        [MenuItem("GOLFIN/Design Audit/Tripwire (prove the dumper sees defects)", priority = 400)]
        public static void LaunchTripwire() => Launch("tripwire");

        [MenuItem("GOLFIN/Design Audit/Dump the nav-reachable screens (EN)", priority = 401)]
        public static void LaunchDumpEn() => Launch("dump:en");

        [MenuItem("GOLFIN/Design Audit/Dump the nav-reachable screens (JA)", priority = 402)]
        public static void LaunchDumpJa() => Launch("dump:ja");

        [MenuItem("GOLFIN/Design Audit/Lint the live ShellScene roots", priority = 403)]
        public static void LaunchLintRoots() => Launch("lint");

        [MenuItem("GOLFIN/Design Audit/Dump the DEEP screens (EN)", priority = 404)]
        public static void LaunchDeepEn() => Launch("deep:en");

        [MenuItem("GOLFIN/Design Audit/Dump the DEEP screens (JA)", priority = 405)]
        public static void LaunchDeepJa() => Launch("deep:ja");

        [MenuItem("GOLFIN/Design Audit/Dump the MODALS + Tier-2 (EN)", priority = 406)]
        public static void LaunchModalsEn() => Launch("modals:en");

        [MenuItem("GOLFIN/Design Audit/Capture live screens for the crop sheets", priority = 407)]
        public static void LaunchCapture() => Launch("capture");

        public static void Launch(string mode)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[DesignAudit] Already playing — stop first.");
                return;
            }
            SessionState.SetString(ModeKey, mode);
            SessionState.SetBool(ArmedKey, true);
            EditorApplication.EnterPlaymode();
            Debug.Log($"[DesignAudit] Armed mode='{mode}'. Entering play mode…");
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            if (!SessionState.GetBool(ArmedKey, false)) return;
            SessionState.SetBool(ArmedKey, false);

            var host = new GameObject("~DesignAuditRunner");
            Object.DontDestroyOnLoad(host);
            host.AddComponent<Runner>().Mode = SessionState.GetString(ModeKey, "tripwire");
        }

        class Runner : MonoBehaviour
        {
            public string Mode = "tripwire";

            void Start()
            {
                Application.runInBackground = true;
                StartCoroutine(Go());
            }

            static void Line(string s) => Debug.Log("[DesignAudit] " + s);

            IEnumerator Go()
            {
                yield return Tap("StartButton", 90f);
                yield return new WaitForSecondsRealtime(4f);

                var root = CurrentScreenRoot();
                if (root == null) { Line("FAIL: no current screen root after boot"); yield break; }
                Line($"current screen root = {root.name}");

                if (Mode == "tripwire") yield return Tripwire(root);
                else if (Mode.StartsWith("dump:")) yield return DumpPass(root, Mode.Substring(5));
                else if (Mode == "lint") yield return LintPass();
                else if (Mode.StartsWith("deep:")) yield return DeepPass(Mode.Substring(5));
                else if (Mode.StartsWith("modals:")) yield return ModalPass(Mode.Substring(7));
                else if (Mode == "capture") yield return CapturePass();

                Line("done");
                EditorApplication.isPlaying = false;
            }

            // ── §20 tripwire ────────────────────────────────────────────────
            IEnumerator Tripwire(GameObject root)
            {
                DesignAuditDumper.Dump(root, "TRIPWIRE_01_clean", "en");

                // (1) a label forced onto Unity's default font
                var label = root.GetComponentsInChildren<TextMeshProUGUI>(true).FirstOrDefault();
                // (2) an Image whose sprite is nulled — the linter's `flat-fill` shape
                var img = root.GetComponentsInChildren<Image>(true).FirstOrDefault(i => i.sprite != null);
                if (label == null || img == null)
                {
                    Line("FAIL: screen has no TMP and/or no sprite-bearing Image to plant on");
                    yield break;
                }

                string fontPath = AssetDatabase.GUIDToAssetPath(LiberationGuid);
                var lib = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath);
                if (lib == null) { Line("FAIL: LiberationSans SDF not found at " + fontPath); yield break; }

                string labelPath = label.name, imgPath = img.name;
                var originalFont = label.font;
                var originalSprite = img.sprite;

                label.font = lib;                       // defect 1 — wrong family
                img.sprite = null;                      // defect 2 — flat fill
                label.gameObject.AddComponent<Outline>(); // defect 3 — Outline-as-border

                Canvas.ForceUpdateCanvases();
                yield return new WaitForEndOfFrame();

                DesignAuditDumper.Dump(root, "TRIPWIRE_02_planted", "en");
                Line($"planted: font->LiberationSans on '{labelPath}', sprite->null on '{imgPath}', " +
                     $"Outline added to '{labelPath}'");

                // Put it back in-session too, so the dump after the revert is meaningful even
                // before play mode exits and discards everything anyway.
                label.font = originalFont;
                img.sprite = originalSprite;
                Object.DestroyImmediate(label.GetComponent<Outline>());
                Canvas.ForceUpdateCanvases();
                yield return new WaitForEndOfFrame();

                DesignAuditDumper.Dump(root, "TRIPWIRE_03_reverted", "en");
                Line("reverted in-session; play-mode exit discards any residue");
            }

            // ── Phase 1.2 · the dump pass ───────────────────────────────────
            //
            // Every hop is a REAL widget onClick. The screen NAME comes from ScreenManager's own
            // CurrentScreen after the hop, never from what this list intended to reach — a nav tap
            // that silently fails would otherwise dump the previous screen under the next screen's
            // name, and every finding on it would be attributed to the wrong surface.
            IEnumerator DumpPass(GameObject home, string locale)
            {
                if (locale == "ja")
                {
                    if (!SetLanguage("Japanese"))
                    { Line("FAIL: could not switch to Japanese — aborting the JA pass"); yield break; }
                    // The swap is imperative on many labels, so give every OnLanguageChanged
                    // subscriber a chance to repaint before anything is measured.
                    yield return new WaitForSecondsRealtime(3f);
                    Line("locale switched to Japanese");
                }

                yield return DumpCurrent("Home", locale);

                // PersistentUI is dumped ONCE, separately: it is not part of any screen root and
                // would otherwise be counted on every screen that happens to show it.
                var persistent = GameObject.Find("PersistentUI") ?? GameObject.Find("PersistentUICanvas");
                if (persistent != null) DesignAuditDumper.Dump(persistent, "PersistentUI", locale);
                else Line("WARN: PersistentUI root not found");

                foreach (var (slot, label) in new[]
                {
                    ("NavGachaButton",      "GeneralShop"),
                    ("NavTeeButton",        "ModeSelection"),
                    ("NavInventoryButton",  "Inventory"),
                    ("NavCharactersButton", "Roster"),
                    ("NavHomeButton",       "HomeReturn"),
                })
                {
                    yield return Tap(slot, 20f);
                    yield return new WaitForSecondsRealtime(3.5f);
                    yield return DumpCurrent(label, locale);
                }

                // The Inventory tabs are hidden states reachable only through their own toggles.
                yield return Tap("NavInventoryButton", 20f);
                yield return new WaitForSecondsRealtime(3f);
                var inv = CurrentScreenRoot();
                if (inv != null)
                {
                    // Ask the CONTROLLER which buttons are the tabs. A name heuristic
                    // ("starts with Tab") found ZERO on the first run — the tabs are not named that
                    // way — and a dump pass that silently covers no tabs looks identical to one
                    // where the tabs are clean. InventoryScreenController.tabButtons IS the list the
                    // game itself drives, so it cannot disagree with what the player can reach.
                    var tabs = new System.Collections.Generic.List<Button>();
                    var ctrl = inv.GetComponentsInChildren<MonoBehaviour>(true)
                                  .FirstOrDefault(m => m != null && m.GetType().Name == "InventoryScreenController");
                    if (ctrl != null)
                    {
                        var fi = ctrl.GetType().GetField("tabButtons",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (fi?.GetValue(ctrl) is Button[] arr) tabs.AddRange(arr.Where(b => b != null));
                    }
                    if (tabs.Count == 0)
                        Line("WARN: InventoryScreenController.tabButtons empty/unreadable — tabs NOT covered");
                    Line($"Inventory tab buttons found: {tabs.Count} ({string.Join(", ", tabs.Select(t => t.name))})");
                    for (int i = 0; i < tabs.Count; i++)
                    {
                        tabs[i].onClick.Invoke();
                        yield return new WaitForSecondsRealtime(2.5f);
                        DesignAuditDumper.Dump(inv, $"Inventory_tab{i}_{tabs[i].name}", locale);
                    }
                }

                // Settings is an OVERLAY: it never becomes CurrentScreen, so it is dumped from its
                // own root rather than through the screen resolver.
                yield return Tap("SettingsButton", 20f);
                yield return new WaitForSecondsRealtime(3f);
                var settings = GameObject.Find("SettingsScreen");
                if (settings != null && settings.activeInHierarchy)
                    DesignAuditDumper.Dump(settings, "SettingsOverlay", locale);
                else Line("WARN: SettingsScreen overlay not open after tapping SettingsButton");
            }

            /// <summary>
            /// A4's second half: the ShellScene-hosted screens have no prefab path, so LintPrefab
            /// cannot reach them. LintRoot runs the SAME three layers on the live object — pinned
            /// byte-identical to LintPrefab by `LintRoot_ProducesTheSameFindingsAsLintPrefab`.
            /// </summary>
            /// <summary>
            /// The screens the nav bar cannot reach. Several have NO player path from a fresh
            /// session — a tournament needs an entered tournament, GachaPrizes needs a completed
            /// pull that spends currency — so those are re-seated with ShowScreen and the dump
            /// records `reachedVia:"harness ShowScreen"`. The ones that DO have a path are tapped.
            /// </summary>
            IEnumerator DeepPass(string locale)
            {
                if (locale == "ja")
                {
                    if (!SetLanguage("Japanese")) { Line("FAIL: JA switch failed"); yield break; }
                    yield return new WaitForSecondsRealtime(3f);
                }

                // --- reachable by a real tap ---
                yield return Tap("NavGachaButton", 20f);
                yield return new WaitForSecondsRealtime(3f);
                yield return TapNamedDeep("HistoryChip", "GachaHistoryScreen", locale);

                yield return Tap("NavTeeButton", 20f);
                yield return new WaitForSecondsRealtime(3f);
                yield return DumpNow(locale, "real nav: bottom-nav TEE");

                // --- no player path: re-seat, and SAY SO in the artifact ---
                foreach (var id in new[]
                {
                    "HoleSelection", "MissionSelection", "TournamentSelection",
                    "TournamentHoleSelection", "TournamentLeaderboard", "Leaderboard",
                    "GachaPrizes", "StaminaShopSelection", "StaminaShopDetail",
                })
                {
                    if (!Force(id)) { Line($"WARN: could not re-seat {id}"); continue; }
                    yield return new WaitForSecondsRealtime(3.5f);
                    var r = CurrentScreenRoot();
                    if (r == null) { Line($"WARN: no root after forcing {id}"); continue; }
                    DesignAuditDumper.Dump(r, r.name, locale, "harness ShowScreen (no player path)");
                }
            }

            /// <summary>
            /// The modals and the Tier-2 boot/auth screens.
            ///
            /// <para>Most modal triggers cannot be driven from a fresh session without side effects
            /// a read-only audit must not cause — a gacha reveal needs a PULL that spends currency,
            /// HoleComplete needs a finished hole. So each modal is opened through its OWN
            /// controller's `Show()` — the same entry the game uses — and the dump records
            /// `reachedVia:"controller.Show()"`. That is weaker than a tap and is labelled as such,
            /// but it is the real show path, not a SetActive(true) that would skip the controller's
            /// own binding and measure an unbound modal.</para>
            /// </summary>
            /// <summary>
            /// A6 — one live capture per Tier-1 screen, to sit beside its node render in the crop
            /// sheet. Snapped through CaptureCore.SnapPlayModeSafe at END of frame: in play mode it
            /// uses ScreenCapture.CaptureScreenshotAsTexture, which returns null unless the
            /// backbuffer is readable — and on null SnapPlayModeSafe warns, skips the write and
            /// STILL RETURNS THE PATH. Every frame is therefore checked for existence and for an
            /// md5 differing from the previous one.
            /// </summary>
            IEnumerator CapturePass()
            {
                string outDir = "Docs/Specs/Active/design_consistency_audit/screenshots";
                System.IO.Directory.CreateDirectory(outDir);
                string last = "";

                foreach (var (slot, force) in new (string, string)[]
                {
                    ("NavHomeButton", null), ("NavGachaButton", null), ("NavTeeButton", null),
                    ("NavInventoryButton", null), ("NavCharactersButton", null),
                    (null, "HoleSelection"), (null, "MissionSelection"),
                    (null, "TournamentSelection"), (null, "TournamentHoleSelection"),
                    (null, "TournamentLeaderboard"), (null, "Leaderboard"),
                    (null, "GachaHistory"), (null, "GachaPrizes"),
                    (null, "StaminaShopSelection"), (null, "StaminaShopDetail"),
                })
                {
                    if (slot != null) { yield return Tap(slot, 20f); }
                    else if (!Force(force)) { Line($"WARN: could not reach {force}"); continue; }
                    yield return new WaitForSecondsRealtime(3.5f);

                    var r = CurrentScreenRoot();
                    string name = r != null ? r.name : (force ?? slot);

                    yield return new WaitForEndOfFrame();
                    string path = Golfin.Diagnostics.Runtime.CaptureCore.SnapPlayModeSafe("audit_" + name);
                    if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
                    { Line($"FAIL {name}: phantom capture path ({path})"); continue; }

                    string md5 = Md5(path);
                    if (md5 == last) { Line($"FAIL {name}: STALE frame (md5 == previous)"); continue; }
                    last = md5;

                    string dest = System.IO.Path.Combine(outDir, "live_" + name + ".png");
                    System.IO.File.Copy(path, dest, true);
                    Line($"captured {dest} md5={md5.Substring(0,8)}");
                }
            }

            static string Md5(string path)
            {
                using (var md5 = System.Security.Cryptography.MD5.Create())
                using (var fs = System.IO.File.OpenRead(path))
                    return System.BitConverter.ToString(md5.ComputeHash(fs)).Replace("-", "");
            }

            IEnumerator ModalPass(string locale)
            {
                if (locale == "ja")
                {
                    if (!SetLanguage("Japanese")) { Line("FAIL: JA switch failed"); yield break; }
                    yield return new WaitForSecondsRealtime(3f);
                }

                // Tier 2 — real ScreenIds, so the same re-seat the deep pass uses.
                foreach (var id in new[] { "Login", "SignUp", "CreateUsername",
                                           "EmailConfirmation", "ResetPassword", "Loading", "Splash" })
                {
                    if (!Force(id)) { Line($"note: no ScreenId '{id}' — skipped"); continue; }
                    yield return new WaitForSecondsRealtime(2.5f);
                    var r = CurrentScreenRoot();
                    if (r != null) DesignAuditDumper.Dump(r, r.name, locale, "harness ShowScreen (Tier 2)");
                }

                // Modals — every ModalController in the scene, opened through its own Show().
                var modals = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include,
                                                                    FindObjectsSortMode.None)
                    // A13: the GPS surface is owned elsewhere and must not appear in ANY dump.
                    // The first modal run swept in five GPS modals (VoteCreate, RoundComplete,
                    // VenuePicker, CheckInConfirm, GiftSend) purely because they derive from the
                    // same base class — "every ModalController in the scene" is not the same set as
                    // "every in-scope modal". Filtered by the type's own namespace, which is what
                    // actually says who owns it.
                    .Where(m => m != null && IsModalController(m.GetType()) && !IsGps(m.GetType()))
                    .GroupBy(m => m.GetType().Name).Select(g => g.First()).ToList();
                Line($"modal controllers found: {modals.Count} " +
                     $"({string.Join(", ", modals.Select(m => m.GetType().Name))})");

                foreach (var m in modals)
                {
                    var show = m.GetType().GetMethod("Show", System.Type.EmptyTypes);
                    if (show == null) { Line($"note: {m.GetType().Name} has no parameterless Show() — skipped"); continue; }
                    bool ok = true;
                    try { show.Invoke(m, null); }
                    catch (System.Exception e) { ok = false; Line($"note: {m.GetType().Name}.Show() threw {e.GetBaseException().GetType().Name} — skipped"); }
                    if (!ok) continue;
                    yield return new WaitForSecondsRealtime(2.5f);
                    DesignAuditDumper.Dump(m.gameObject, "MODAL_" + m.GetType().Name, locale,
                                           "controller.Show() (no side-effect-free player trigger)");
                }
            }

            /// <summary>A13 guard — anything under the Gps namespace is out of scope.</summary>
            static bool IsGps(System.Type t) =>
                (t.Namespace ?? "").Contains(".Gps") || t.Name.StartsWith("Gps");

            static bool IsModalController(System.Type t)
            {
                for (var b = t; b != null; b = b.BaseType)
                    if (b.Name == "ModalController") return true;
                return false;
            }

            IEnumerator TapNamedDeep(string widget, string expect, string locale)
            {
                yield return Tap(widget, 20f);
                yield return new WaitForSecondsRealtime(3.5f);
                yield return DumpNow(locale, "real nav: " + widget + ".onClick");
            }

            IEnumerator DumpNow(string locale, string via)
            {
                yield return null;
                var r = CurrentScreenRoot();
                if (r == null) { Line("WARN: no root for " + via); yield break; }
                DesignAuditDumper.Dump(r, r.name, locale, via);
            }

            /// <summary>ScreenManager.ShowScreen(ScreenId.X) by reflection.</summary>
            static bool Force(string screenIdName)
            {
                var smType = System.AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => { try { return a.GetTypes(); } catch { return new System.Type[0]; } })
                    .FirstOrDefault(t => t.Name == "ScreenManager" && typeof(MonoBehaviour).IsAssignableFrom(t));
                var idType = System.AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => { try { return a.GetTypes(); } catch { return new System.Type[0]; } })
                    .FirstOrDefault(t => t.Name == "ScreenId" && t.IsEnum);
                if (smType == null || idType == null) return false;
                var inst = Object.FindFirstObjectByType(smType) as MonoBehaviour;
                // ShowScreen(ScreenId, bool instant = false) — TWO parameters. Asking for the
                // one-arg overload returns null and every re-seat silently no-ops, which is what
                // the first run did: nine "could not re-seat" warnings and nine missing screens.
                var show = smType.GetMethod("ShowScreen", new[] { idType, typeof(bool) })
                        ?? smType.GetMethod("ShowScreen", new[] { idType });
                if (inst == null || show == null) return false;
                object id;
                try { id = System.Enum.Parse(idType, screenIdName); } catch { return false; }
                var args = show.GetParameters().Length == 2 ? new object[] { id, true } : new object[] { id };
                show.Invoke(inst, args);
                return true;
            }

            IEnumerator LintPass()
            {
                var linter = System.AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => { try { return a.GetTypes(); } catch { return new System.Type[0]; } })
                    .FirstOrDefault(t => t.Name == "UIFidelityLinter");
                var lintRoot = linter?.GetMethod("LintRoot",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (lintRoot == null) { Line("FAIL: LintRoot not found"); yield break; }

                foreach (var (slot, _) in new[]
                {
                    ("NavHomeButton", "Home"), ("NavGachaButton", "GeneralShop"),
                    ("NavTeeButton", "ModeSelection"), ("NavInventoryButton", "Inventory"),
                    ("NavCharactersButton", "Roster"),
                })
                {
                    yield return Tap(slot, 20f);
                    yield return new WaitForSecondsRealtime(3f);
                    var r = CurrentScreenRoot();
                    if (r == null) { Line("WARN: no root after " + slot); continue; }
                    var res = lintRoot.Invoke(null, new object[] { r, "LIVE_" + r.name, null }) as string;
                    Line($"LintRoot {r.name}: {(res ?? "").Split('\n').LastOrDefault(x => x.Length > 0)}");
                }
            }

            IEnumerator DumpCurrent(string intended, string locale)
            {
                yield return null;
                var root = CurrentScreenRoot();
                if (root == null) { Line($"WARN: no screen root for intended '{intended}'"); yield break; }
                if (!root.name.Contains(intended.Replace("Return", "")) && intended != "HomeReturn")
                    Line($"ROUTE NOTE: intended '{intended}' but ScreenManager says '{root.name}' — " +
                         "dumping under the REAL name");
                DesignAuditDumper.Dump(root, root.name, locale);
            }

            // ── real navigation ─────────────────────────────────────────────
            IEnumerator Tap(string name, float seconds)
            {
                float deadline = Time.realtimeSinceStartup + seconds;
                while (Time.realtimeSinceStartup < deadline)
                {
                    var b = Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                                  .FirstOrDefault(x => x.name == name && x.gameObject.activeInHierarchy);
                    if (b != null) { Line("tapping the real " + name + ".onClick"); b.onClick.Invoke(); yield break; }
                    yield return new WaitForSecondsRealtime(0.5f);
                }
                Line("WARN: " + name + " never appeared in " + seconds + " s");
            }

            /// <summary>
            /// Flip `LocalizationManager.CurrentLanguage`. Returns false rather than silently
            /// dumping an English screen under a "ja" label — a JA pass that never switched would
            /// "prove" that every JA size matches EN.
            /// </summary>
            static bool SetLanguage(string languageName)
            {
                var lm = System.AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => { try { return a.GetTypes(); } catch { return new System.Type[0]; } })
                    .FirstOrDefault(t => t.Name == "LocalizationManager");
                if (lm == null) { Line("WARN: LocalizationManager type not found"); return false; }

                var set = lm.GetMethod("SetLanguage",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                var cur = lm.GetProperty("CurrentLanguage",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (set == null || cur == null) { Line("WARN: SetLanguage/CurrentLanguage missing"); return false; }

                var enumType = set.GetParameters()[0].ParameterType;
                object target;
                try { target = System.Enum.Parse(enumType, languageName); }
                catch { Line($"WARN: '{languageName}' is not a {enumType.Name} value"); return false; }

                set.Invoke(null, new[] { target });
                var now = cur.GetValue(null);
                Line($"LocalizationManager.CurrentLanguage = {now}");
                return now != null && now.ToString() == languageName;
            }

            /// <summary>The screen ScreenManager says is current — resolved by reflection so this
            /// Editor assembly needs no reference to Assembly-CSharp.</summary>
            static GameObject? CurrentScreenRoot()
            {
                var smType = System.AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => { try { return a.GetTypes(); } catch { return new System.Type[0]; } })
                    .FirstOrDefault(t => t.Name == "ScreenManager" && typeof(MonoBehaviour).IsAssignableFrom(t));
                if (smType == null) return null;

                var inst = Object.FindFirstObjectByType(smType) as MonoBehaviour;
                if (inst == null) return null;

                var m = smType.GetMethod("ShellScreenObject",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static);
                var cur = smType.GetProperty("CurrentScreen");
                if (m != null && cur != null)
                {
                    var id = cur.GetValue(inst);
                    var go = m.IsStatic ? m.Invoke(null, new[] { id }) : m.Invoke(inst, new[] { id });
                    if (go is GameObject g && g != null) return g;
                }

                // Fallback: the one active child of ScreensRoot.
                var screensRoot = GameObject.Find("ScreensRoot");
                if (screensRoot != null)
                    foreach (Transform c in screensRoot.transform)
                        if (c.gameObject.activeInHierarchy) return c.gameObject;
                return null;
            }
        }
    }
}
