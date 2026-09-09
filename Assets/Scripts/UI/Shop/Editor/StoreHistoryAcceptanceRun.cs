// Assets/Scripts/UI/Shop/Editor/StoreHistoryAcceptanceRun.cs
// store_history — the acceptance walk, driven through the REAL widgets.
//
// PIPELINE_HARDENING §2 (real-entry rule) and feedback_no_render_harness_use_real_navigation: every
// step below is a `Button.onClick.Invoke()` on the object the player's finger lands on — the Splash
// StartButton, the bottom-nav Gacha slot, the STORE tab, the Rewards Center's HistoryChip, the
// chip row, CLOSE. No ShowScreen(target) shortcut anywhere: the app boots behind a title gate that
// ScreenManager does not manage, so `CurrentScreen == target` after a bare ShowScreen is a FALSE
// POSITIVE and the frame stays on the title.
//
// §8 is measured, not eyeballed: the y of the first three STORE cards' anchoredPosition after the
// stagger has ended, on all three entry paths, written to a JSON alongside the screenshots.
//
// EDITOR-ONLY and not shipped — it lives under an Editor/ folder so no runtime assembly sees it
// (project_editor_only_seams_break_player_builds).
#nullable enable
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Golfin.Diagnostics.Runtime;
using GolfinRedux.UI;
using GolfinRedux.UI.Gacha;

namespace GolfinRedux.UI.Shop.EditorTools
{
    public static class StoreHistoryAcceptanceRun
    {
        private const string ArmedKey = "GOLFIN.StoreHistory.AcceptanceArmed";
        private const string OutDir     = "Docs/Specs/Active/store_history/screenshots";
        private const string ShellScene = "Assets/Scenes/ShellScene.unity";

        [MenuItem("GOLFIN/Store History/Run acceptance walk", priority = 261)]
        public static void Run()
        {
            Directory.CreateDirectory(OutDir);

            // ShellScene, explicitly. An EditMode sweep leaves whatever scene it opened last —
            // often an untitled empty one — and entering play mode there boots the runtime
            // bootstrappers with no ScreenManager and no Splash, so the walk finds nothing to tap.
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != ShellScene)
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(ShellScene, UnityEditor.SceneManagement.OpenSceneMode.Single);

            Application.runInBackground = true;   // else every capture returns the splash frame
            EditorPrefs.SetBool(ArmedKey, true);
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void Hook()
        {
            if (!EditorPrefs.GetBool(ArmedKey, false)) return;
            EditorApplication.update += Pump;
        }

        private static bool _spawned;

        /// <summary>Poll rather than delayCall — a delayCall races Unity's own scene restore
        /// (reference_initializeonload_delaycall_races_scene_restore).</summary>
        private static void Pump()
        {
            if (!Application.isPlaying) return;
            if (_spawned) { EditorApplication.update -= Pump; return; }
            _spawned = true;
            EditorPrefs.SetBool(ArmedKey, false);
            EditorApplication.update -= Pump;
            var host = new GameObject("[StoreHistoryAcceptance]");
            Object.DontDestroyOnLoad(host);
            host.AddComponent<Runner>();
        }

        private sealed class Runner : MonoBehaviour
        {
            private void Start() => StartCoroutine(Sequence());

            private static readonly StringBuilder Report = new StringBuilder();

            private static void Log(string s)  { Debug.Log("[StoreHistoryRun] " + s);  Report.AppendLine(s); }
            private static void Fail(string s) { Debug.LogError("[StoreHistoryRun] FAIL — " + s); Report.AppendLine("FAIL — " + s); }

            private static ScreenId? Now => ScreenManager.Instance?.CurrentScreen;

            private IEnumerator Sequence()
            {
                Application.runInBackground = true;

                // ── 1. Through the Splash gate, by tapping the real StartButton.
                float t = 0f;
                while (t < 20f)
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

                yield return WaitFor(ScreenId.Home, 60f);
                if (Now != ScreenId.Home) { Fail("never reached Home (currentScreen=" + Now + ")"); Finish(); yield break; }
                Log("reached Home");
                yield return Settle(1.5f);

                // ── 2. §8 path A — the top-bar "+" from HOME.
                if (!TapShopPlus()) { Finish(); yield break; }
                yield return WaitFor(ScreenId.GeneralShop, 20f);
                if (Now != ScreenId.GeneralShop) { Fail("+ did not reach GeneralShop (currentScreen=" + Now + ")"); Finish(); yield break; }
                yield return Settle(3f);
                MeasureGrid("A_plus_from_home");
                Save("A_plus_from_home", "01_store_grid_plus_from_home.png");

                // ── 3. The History chip on the STORE tab — the REAL widget's own onClick.
                var tab = FindFirstObjectByType<GachaTabController>(FindObjectsInactive.Include);
                if (tab == null) { Fail("no GachaTabController"); Finish(); yield break; }
                var chip = tab.transform.Find("HistoryChip")?.GetComponent<Button>();
                if (chip == null) { Fail("HistoryChip has no Button at GeneralShopScreen/HistoryChip"); Finish(); yield break; }
                Log("HistoryChip listeners = " + chip.onClick.GetPersistentEventCount() +
                    " persistent, interactable=" + chip.interactable +
                    ", activeInHierarchy=" + chip.gameObject.activeInHierarchy);
                chip.onClick.Invoke();

                yield return WaitFor(ScreenId.StoreHistory, 20f);
                if (Now != ScreenId.StoreHistory) { Fail("History chip on STORE did not open StoreHistory (currentScreen=" + Now + ")"); Finish(); yield break; }
                Log("ACCEPT — STORE tab History chip -> ScreenId.StoreHistory (no toast)");
                yield return Settle(5f);
                DumpScreen("store_history_all");
                Save("store_history_all", "02_store_history_all.png");

                // ── 4. The CLUBS chip — a real filter tap.
                var ctrl = FindFirstObjectByType<StoreHistoryScreenController>(FindObjectsInactive.Include);
                var clubs = Chip(ctrl, "CLUBSChip");
                if (clubs == null) Fail("CLUBSChip not found on StoreHistoryScreen");
                else
                {
                    clubs.onClick.Invoke();
                    Log("tapped CLUBSChip");
                    yield return Settle(3f);
                    DumpScreen("store_history_clubs");
                }

                // TICKETS is the chip with rows behind it on this account — CLUBS is legitimately
                // empty here (nothing was ever bought in that category), so it proves the filter
                // narrows but not that it still RENDERS.
                var tickets = Chip(ctrl, "TICKETSChip");
                if (tickets == null) Fail("TICKETSChip not found on StoreHistoryScreen");
                else
                {
                    tickets.onClick.Invoke();
                    Log("tapped TICKETSChip");
                    yield return Settle(3f);
                    DumpScreen("store_history_tickets");
                    Save("store_history_tickets", "03_store_history_tickets.png");
                }

                var all = Chip(ctrl, "ALLChip");
                if (all != null) { all.onClick.Invoke(); Log("tapped ALLChip (restore)"); yield return Settle(2.5f); }

                // ── 5. The A/B: Gacha History through ITS real chip, same shell.
                var strip = FindFirstObjectByType<GachaHistoryTabStrip>(FindObjectsInactive.Include);
                var gachaTabBtn = ctrl == null ? null
                    : ctrl.transform.Find("GameScreenContent/ContentContainer/FiltersBlock/TabBar/DailyTab")?.GetComponent<Button>();
                if (gachaTabBtn != null)
                {
                    gachaTabBtn.onClick.Invoke();           // GACHA on the strip -> Rewards Center, gacha tab
                    yield return WaitFor(ScreenId.GeneralShop, 20f);
                    yield return Settle(2.5f);
                    var chip2 = tab.transform.Find("HistoryChip")?.GetComponent<Button>();
                    chip2?.onClick.Invoke();
                    yield return WaitFor(ScreenId.GachaHistory, 20f);
                    if (Now == ScreenId.GachaHistory)
                    {
                        Log("ACCEPT — GACHA tab History chip -> ScreenId.GachaHistory (unchanged)");
                        yield return Settle(5f);
                        Save("gacha_history_ab", "04_gacha_history_ab.png");
                    }
                    else Fail("GACHA tab History chip did not open GachaHistory (currentScreen=" + Now + ")");
                }

                // ── 6. §8 path B — the "+" from GachaHistory.
                if (Now == ScreenId.GachaHistory && TapShopPlus())
                {
                    yield return WaitFor(ScreenId.GeneralShop, 20f);
                    yield return Settle(3f);
                    MeasureGrid("B_plus_from_gacha_history");
                    Save("B_plus_from_gacha_history", "05_store_grid_plus_from_gachahistory.png");
                }

                // ── 7. §8 path C — bottom-nav Gacha slot -> GACHA tab -> STORE tab.
                if (TapNavGacha())
                {
                    yield return WaitFor(ScreenId.GeneralShop, 20f);
                    yield return Settle(2.5f);
                    var storeTab = tab.transform.Find("ContentArea/BarsArea/TabBar/WeeklyTab")?.GetComponent<Button>();
                    if (storeTab == null) Fail("STORE tab not found on GeneralShopScreen");
                    else
                    {
                        storeTab.onClick.Invoke();
                        Log("tapped STORE tab from the GACHA tab");
                        yield return Settle(3f);
                        MeasureGrid("C_navgacha_then_store");
                        Save("C_navgacha_then_store", "06_store_grid_navgacha_store.png");
                    }
                }

                Finish();
            }

            // ── Helpers ────────────────────────────────────────────────────────

            private static bool TapShopPlus()
            {
                var pum = FindFirstObjectByType<Golfin.UI.PersistentUIManager>(FindObjectsInactive.Include);
                if (pum == null) { Fail("no PersistentUIManager"); return false; }
                Button? btn = pum.shopPlusButton;
                if (btn == null) { Fail("PersistentUIManager.shopPlusButton is not wired"); return false; }
                Log("tapping the REAL top-bar + : " + Path(btn.transform));
                btn.onClick.Invoke();
                return true;
            }

            private static bool TapNavGacha()
            {
                var pum = FindFirstObjectByType<Golfin.UI.PersistentUIManager>(FindObjectsInactive.Include);
                if (pum == null) { Fail("no PersistentUIManager"); return false; }
                Button btn = pum.gachaButton;
                if (btn == null) { Fail("PersistentUIManager.gachaButton is not wired"); return false; }
                Log("tapping the REAL bottom-nav Gacha slot: " + Path(btn.transform));
                btn.onClick.Invoke();
                return true;
            }

            /// <summary>§8 — the y of the first three STORE cards after the stagger has settled.
            /// Equal spacing is the pass; a card-sized hole under card 0 is the defect.</summary>
            private static void MeasureGrid(string label)
            {
                var shop = FindFirstObjectByType<GeneralShopScreenController>(FindObjectsInactive.Include);
                var grid = shop == null ? null : shop.transform.Find(
                    "ContentArea/BarsArea/RankingsArea/Modal/Bottom97/ScrollArea/Viewport/GridContent");
                if (grid == null) { Log(label + ": GridContent not found"); return; }

                var ys = new List<string>();
                int taken = 0;
                foreach (Transform c in grid)
                {
                    if (!c.gameObject.activeSelf) continue;
                    var rt = c as RectTransform;
                    if (rt == null) continue;
                    ys.Add(c.name + " y=" + rt.anchoredPosition.y.ToString("F2") + " h=" + rt.rect.height.ToString("F2"));
                    if (++taken >= 4) break;
                }
                Log(label + " grid children (first 4): " + string.Join(" | ", ys));
            }

            private static void DumpScreen(string label)
            {
                var ctrl = FindFirstObjectByType<StoreHistoryScreenController>(FindObjectsInactive.Include);
                if (ctrl == null) { Log(label + ": no StoreHistoryScreenController"); return; }

                var main    = ctrl.transform.Find("GameScreenContent/ContentContainer/MainPanel") as RectTransform;
                var bar     = ctrl.transform.Find("GameScreenContent/ContentContainer/MainPanel/CardsContainer/Scrollbar") as RectTransform;
                var content = ctrl.transform.Find("GameScreenContent/ContentContainer/MainPanel/CardsContainer/Viewport/Content");
                var title   = ctrl.transform.Find("GameScreenContent/ContentContainer/MainPanel/Header/Title")?.GetComponent<TMPro.TMP_Text>();

                if (main != null && bar != null)
                {
                    var m = new Vector3[4]; main.GetWorldCorners(m);
                    var b = new Vector3[4]; bar.GetWorldCorners(b);
                    bool inside = b[0].x >= m[0].x - 0.01f && b[2].x <= m[2].x + 0.01f
                               && b[0].y >= m[0].y - 0.01f && b[2].y <= m[2].y + 0.01f;
                    Log(label + " MainPanel world = [" + m[0] + " .. " + m[2] + "]");
                    Log(label + " Scrollbar world = [" + b[0] + " .. " + b[2] + "]   inside=" + inside);
                }
                if (title != null) Log(label + " Title = \"" + title.text + "\"");
                if (content != null)
                {
                    int rows = 0;
                    var lines = new List<string>();
                    foreach (Transform c in content)
                    {
                        var row = c.GetComponent<StoreHistoryRow>();
                        if (row == null) continue;
                        rows++;
                        if (rows > 4) continue;
                        var texts = c.GetComponentsInChildren<TMPro.TMP_Text>(false);
                        var got = new List<string>();
                        foreach (var tx in texts)
                            if (!string.IsNullOrWhiteSpace(tx.text) && tx.transform.parent.name == "MetaLines")
                                got.Add(tx.text.Replace("\n", " / "));
                        lines.Add("row" + rows + ": " + string.Join(" ¦ ", got));
                    }
                    Log(label + " rows rendered = " + rows + " (of " + StoreHistoryStore.All.Count + " records)");
                    foreach (var l in lines) Log("   " + l);

                    // What the CARD actually holds — the portrait sprite and the description
                    // label — read off the live objects rather than inferred from the binder.
                    int n = 0;
                    foreach (Transform c in content)
                    {
                        if (c.GetComponent<StoreHistoryRow>() == null) continue;
                        if (++n > 4) break;
                        var card = c.GetComponentInChildren<Golfin.Inventory.BagClubCard>(true);
                        if (card == null) { Log("   card" + n + ": <no BagClubCard>"); continue; }
                        var portrait = card.transform.Find("Mask/Background/CardTop/Portrait")?.GetComponent<Image>();
                        var desc     = card.transform.Find("Mask/Background/PrizeDescription")?.GetComponent<TMPro.TMP_Text>();
                        var distance = card.transform.Find("Mask/Background/StatsPanel/DistanceRow/DistanceValue")?.GetComponent<TMPro.TMP_Text>();
                        Log("   card" + n + ": portrait sprite=" + (portrait == null ? "<no Image>" : (portrait.sprite == null ? "<NULL>" : portrait.sprite.name))
                            + " enabled=" + (portrait == null ? "?" : portrait.enabled.ToString())
                            + " | detail=\"" + (distance == null ? "<none>" : (distance.gameObject.activeInHierarchy ? distance.text : "<row hidden>")) + "\""
                            + " | description=" + (desc == null ? "<ABSENT>"
                                : "active=" + desc.gameObject.activeInHierarchy
                                  + " fontSize=" + desc.fontSize.ToString("F1")
                                  + " band=[" + desc.fontSizeMin.ToString("F0") + ".." + desc.fontSizeMax.ToString("F0") + "]"
                                  + " chars=" + desc.text.Length
                                  + " \"" + (desc.text.Length > 40 ? desc.text.Substring(0, 40) + "…" : desc.text) + "\""));
                    }
                }
            }

            private static Button? Chip(StoreHistoryScreenController? ctrl, string name) =>
                ctrl == null ? null
                : ctrl.transform.Find("GameScreenContent/ContentContainer/FiltersBlock/CategoryRow/" + name)?.GetComponent<Button>();

            private static IEnumerator WaitFor(ScreenId id, float seconds)
            {
                float t = 0f;
                while (t < seconds && Now != id) { t += Time.unscaledDeltaTime; yield return null; }
            }

            private static IEnumerator Settle(float seconds)
            {
                float t = 0f;
                while (t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
            }

            private static void Save(string label, string fileName)
            {
                string dst = System.IO.Path.Combine(OutDir, fileName);

                // SnapPlayModeSafe first — the sanctioned path. It can return a path for a file it
                // NEVER WROTE (reference_snapplaymodesafe_phantom_path): ScreenCapture's backbuffer
                // read yields nothing when the Editor is not the frontmost app, which is exactly
                // the case when this walk is driven over MCP. So the return value is VERIFIED on
                // disk rather than trusted, and the Game View's own render texture — the same
                // surface the sanctioned `screenshot-game-view` tool reads — is the fallback.
                string src = CaptureCore.SnapPlayModeSafe(label);
                if (!string.IsNullOrEmpty(src) && File.Exists(src))
                {
                    File.Copy(src, dst, true);
                    Log("saved " + dst + " (" + new FileInfo(dst).Length + " bytes) from SnapPlayModeSafe");
                    return;
                }

                var tex = CaptureCore.GrabGameViewRT();
                if (tex == null) { Fail("capture for " + label + " — SnapPlayModeSafe wrote nothing and the Game View RT was null"); return; }
                File.WriteAllBytes(dst, tex.EncodeToPNG());
                Log("saved " + dst + " (" + new FileInfo(dst).Length + " bytes, " + tex.width + "x" + tex.height + ") from GrabGameViewRT");
                Object.Destroy(tex);
            }

            private static string Path(Transform t)
            {
                var s = t.name;
                while (t.parent != null) { t = t.parent; s = t.name + "/" + s; }
                return s;
            }

            private static void Finish()
            {
                File.WriteAllText("/tmp/store_history_run.txt", Report.ToString());
                Debug.Log("[StoreHistoryRun] report -> /tmp/store_history_run.txt");
                EditorApplication.isPlaying = false;
            }
        }
    }
}
