// ─────────────────────────────────────────────────────────────────────────────
// gps_gifts_votes § Smoke evidence — reach BOTH new screens the way a player
// does, against the live PLAYLIFE API, and capture what they actually render.
//
// REAL NAVIGATION, NOT A RENDER HARNESS (PIPELINE_HARDENING rule 2, and the
// gps_profile_pack scar: a preview-scene renderer gave two false readings before
// anybody drove the real path). Boot → tap the real StartButton → Home → the
// hub's own BackPill/banner path → the hub's GIFT nav slot's onClick → the hub's
// VOTE tile's onClick. Nothing here calls ShowScreen to get somewhere a player
// reaches by tapping; `CurrentScreen == target` behind an untapped title gate is
// a FALSE POSITIVE and this harness exists partly to make that impossible.
//
// It also opens both modals, because a modal is a state the still of a screen
// cannot show and Cesar's standing rule is that every state gets captured.
// ─────────────────────────────────────────────────────────────────────────────
using System;
using System.Collections;
using System.IO;
using System.Text;
using Golfin.Diagnostics.Runtime;
using Golfin.Gps.UI;
using Golfin.Social;
using GolfinRedux.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.Gps.EditorTools
{
    public static class GpsGiftVoteEditorRun
    {
        const string ArmedKey = "gps_gifts_votes.editor_run.armed";
        const string ShotDir  = "Docs/Specs/Active/gps_gifts_votes/screenshots";
        const string LogPath  = "Docs/Diagnostics/_capture/gps_gifts_votes_run.log";

        [MenuItem("GOLFIN/Diagnostics/Gift + Vote — Editor Run", priority = 224)]
        public static void Arm()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
            File.WriteAllText(LogPath, "");
            EditorPrefs.SetBool(ArmedKey, true);
            if (!EditorApplication.isPlaying) EditorApplication.EnterPlaymode();
            else Spawn();
        }

        [InitializeOnLoadMethod]
        static void Hook() => EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && EditorPrefs.GetBool(ArmedKey, false)) Spawn();
        };

        static void Spawn()
        {
            EditorPrefs.SetBool(ArmedKey, false);
            var go = new GameObject("__GpsGiftVoteEditorRun");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<Driver>();
        }

        // ═════════════════════════════════════════════════════════════════════

        public sealed class Driver : MonoBehaviour
        {
            readonly StringBuilder _log = new StringBuilder();
            int _shot;

            void Start()
            {
                // Without this the Editor stops rendering the moment it loses focus and every
                // capture comes back as whatever it drew last — the splash, usually.
                Application.runInBackground = true;
                StartCoroutine(Run());
            }

            IEnumerator Run()
            {
                Line("=== gps_gifts_votes editor run " + DateTime.UtcNow.ToString("u") + " ===");
                yield return Until(() => ScreenManager.Instance != null, 30f, "ScreenManager");

                // The app boots to a title gate ScreenManager does not manage. Tapping it is not
                // optional even when the session is already authenticated (CAPTURE RULE 0).
                yield return TapStart();
                yield return Until(() => ScreenManager.Instance.CurrentScreen == ScreenId.Home, 90f, "Home");
                yield return new WaitForSecondsRealtime(2f);
                Line("signed in as '" + Golfin.Auth.PlayerIdentity.DisplayNameOr("<none>") +
                     "' id=" + Golfin.Auth.PlayerIdentity.UserId);

                // ── into the hub through the Home entry point ────────────────
                yield return TapNamed("GpsPill", "the Home GPS pill");
                yield return Until(() => ScreenManager.Instance.CurrentScreen == ScreenId.GpsHub, 30f, "GpsHub");
                yield return new WaitForSecondsRealtime(2.5f);
                yield return Shot("hub");

                GameObject hub = GameObject.Find("Canvas/ScreensRoot/GpsHubScreen");
                if (hub == null) { Line("FATAL: no hub in the scene"); yield break; }

                // ── GIFT, through the hub's own nav slot ─────────────────────
                Button navGift = hub.transform.Find("GpsNavBar/NavGiftButton").GetComponent<Button>();
                Line("nav GIFT interactable=" + navGift.interactable);
                navGift.onClick.Invoke();
                yield return Until(() => ScreenManager.Instance.CurrentScreen == ScreenId.GpsGift, 30f, "GpsGift");
                // 5 s, not 3: four requests have to land before the panels stop reading "—".
                yield return new WaitForSecondsRealtime(6f);
                yield return Shot("gift");

                GameObject gift = GameObject.Find("Canvas/ScreensRoot/GpsGiftScreen");
                DumpTexts(gift, "gift");

                // The SEND GIFT modal — a state no still of the screen can show.
                yield return TapIn(gift, "ContentContainer/Golfers/Golfer0/SendGiftButton", "SEND GIFT row 1");
                yield return new WaitForSecondsRealtime(1.5f);
                yield return Shot("gift_send_modal");
                yield return TapIn(gift, "GiftSendModal/ModalPanel/CancelButtonRow/CancelButton", "modal CANCEL");
                yield return new WaitForSecondsRealtime(1f);

                // The BUY confirm, which is the same modal in its other mode.
                yield return TapIn(gift, "ContentContainer/BuyGifts/GiftItems/Item0", "BUY item 1");
                yield return new WaitForSecondsRealtime(1.5f);
                yield return Shot("gift_buy_modal");
                yield return TapIn(gift, "GiftSendModal/ModalPanel/CancelButtonRow/CancelButton", "modal CANCEL");
                yield return new WaitForSecondsRealtime(1f);

                // ── back to the hub, then VOTE through the hub's own tile ────
                ScreenManager.Instance.GoBack(ScreenId.GpsHub);
                yield return Until(() => ScreenManager.Instance.CurrentScreen == ScreenId.GpsHub, 30f, "GpsHub (back)");
                yield return new WaitForSecondsRealtime(1.5f);

                Button tileVote = hub.transform.Find("ContentContainer/ActionTiles/Tile_VOTE").GetComponent<Button>();
                Line("tile VOTE interactable=" + tileVote.interactable);
                tileVote.onClick.Invoke();
                yield return Until(() => ScreenManager.Instance.CurrentScreen == ScreenId.GpsVote, 30f, "GpsVote");
                yield return new WaitForSecondsRealtime(6f);
                yield return Shot("vote");

                GameObject vote = GameObject.Find("Canvas/ScreensRoot/GpsVoteScreen");
                DumpVote(vote);

                // MINE — the one filter that is client-side, and the one that can be wrong
                // silently (it matches creator_id against the session id).
                yield return TapIn(vote, "ContentContainer/ChipsRow/Chip3", "chip MINE");
                yield return new WaitForSecondsRealtime(1.5f);
                yield return Shot("vote_mine");

                yield return TapIn(vote, "ContentContainer/ChipsRow/Chip2", "chip PUBLIC");
                yield return new WaitForSecondsRealtime(1.5f);

                // The CREATE modal.
                yield return TapIn(vote, "ContentContainer/ChipsRow/CreateButton", "+ CREATE");
                yield return new WaitForSecondsRealtime(1.5f);
                yield return Shot("vote_create_modal");
                yield return TapIn(vote, "VoteCreateModal/ModalPanel/CancelButtonRow/CancelButton", "modal CANCEL");
                yield return new WaitForSecondsRealtime(1f);

                Line("=== done ===");
                Flush();
                EditorApplication.isPlaying = false;
            }

            // ── evidence ──────────────────────────────────────────────────────

            /// <summary>
            /// What the two screens actually SAY, logged next to the frame. A still proves the
            /// layout; this proves the BINDING — that "4,820 pts" is the account's own gift_pts
            /// and not the mockup number the node draws.
            /// </summary>
            void DumpTexts(GameObject root, string tag)
            {
                if (root == null) { Line(tag + ": NOT IN SCENE"); return; }
                foreach (string p in new[]
                {
                    "ContentContainer/GiftHero/HeroValue",
                    "ContentContainer/GiftHero/HeroSub",
                    "ContentContainer/Supporters/Supporter0/Name",
                    "ContentContainer/Supporters/Supporter0/Pts",
                    "ContentContainer/Golfers/Golfer0/Name",
                    "ContentContainer/Golfers/Golfer0/Followers",
                    "ContentContainer/BuyGifts/GiftItems/Item0/ItemName",
                    "ContentContainer/BuyGifts/GiftItems/Item0/ItemPrice",
                    "ContentContainer/BuyGifts/GiftItems/Item1/ItemName",
                    "ContentContainer/BuyGifts/GiftItems/Item1/ItemPrice",
                    "ContentContainer/BuyGifts/GiftItems/Item2/ItemName",
                    "ContentContainer/BuyGifts/GiftItems/Item2/ItemPrice",
                })
                {
                    Transform t = root.transform.Find(p);
                    var tmp = t != null ? t.GetComponent<TMPro.TextMeshProUGUI>() : null;
                    Line("  " + p + " = " +
                         (t == null ? "<no such object>"
                                    : !t.gameObject.activeInHierarchy ? "<hidden>"
                                    : tmp == null ? "<no TMP>" : "\"" + tmp.text + "\""));
                }
            }

            void DumpVote(GameObject root)
            {
                if (root == null) { Line("vote: NOT IN SCENE"); return; }
                Transform content = root.transform.Find("ContentContainer/VoteList/Content");
                int cards = 0;
                foreach (Transform c in content)
                {
                    if (!c.gameObject.activeSelf) continue;
                    cards++;
                    Transform q = c.Find("VoteBody/VoteTitleRow/Question");
                    Transform m = c.Find("VoteBody/VoteFooter/Meta");
                    Transform yes = c.Find("VoteBody/BarYes/Pct");
                    Line("  card " + c.name + " (" + ((RectTransform)c).rect.height.ToString("F0") + "px) q=\"" +
                         (q != null ? q.GetComponent<TMPro.TextMeshProUGUI>().text : "-") + "\" meta=\"" +
                         (m != null ? m.GetComponent<TMPro.TextMeshProUGUI>().text : "-") + "\" yes=" +
                         (yes != null ? yes.GetComponent<TMPro.TextMeshProUGUI>().text : "-"));
                }
                Line("  cards rendered = " + cards +
                     " (service saw " + (VoteService.Instance.LastVotes != null
                                         ? VoteService.Instance.LastVotes.Count : 0) + ")");
                for (int i = 0; i < 6; i++)
                {
                    Transform s = root.transform.Find("ContentContainer/StoriesRow/Story" + i + "/Label");
                    if (s != null)
                        Line("  story " + i + " = " + (s.gameObject.activeInHierarchy
                            ? "\"" + s.GetComponent<TMPro.TextMeshProUGUI>().text + "\"" : "<hidden>"));
                }
            }

            IEnumerator Shot(string label)
            {
                _shot++;
                string name = string.Format("gv_{0:00}_{1}", _shot, label);
                string path = Path.Combine(ShotDir, name + ".png");
                Directory.CreateDirectory(ShotDir);

                IEnumerator snap = CaptureCore.SnapAtEndOfFrameAndPause(name, path, skipPause: true);
                while (snap.MoveNext()) yield return snap.Current;

                // Assert the FILE, never the return value — SnapPlayModeSafe has logged a path for
                // a file it never wrote (memory: reference_snapplaymodesafe_phantom_path).
                bool exists = File.Exists(path);
                Line("SHOT " + label + " -> " + (exists
                    ? path + " (" + new FileInfo(path).Length / 1024 + " KB)"
                    : "MISSING (" + path + ")"));
            }

            // ── input ─────────────────────────────────────────────────────────

            IEnumerator TapStart()
            {
                float deadline = Time.realtimeSinceStartup + 90f;
                while (Time.realtimeSinceStartup < deadline)
                {
                    foreach (Button b in FindObjectsByType<Button>(FindObjectsInactive.Exclude,
                                                                   FindObjectsSortMode.None))
                    {
                        if (b.name != "StartButton" || !b.gameObject.activeInHierarchy) continue;
                        Line("tapping the real " + b.name);
                        b.onClick.Invoke();
                        yield return new WaitForSecondsRealtime(2f);
                        yield break;
                    }
                    yield return new WaitForSecondsRealtime(0.5f);
                }
                Line("WARN: no StartButton appeared in 90 s");
            }

            /// <summary>Tap the first ACTIVE button with this name, anywhere in the scene.</summary>
            IEnumerator TapNamed(string name, string what)
            {
                float deadline = Time.realtimeSinceStartup + 30f;
                while (Time.realtimeSinceStartup < deadline)
                {
                    foreach (Button b in FindObjectsByType<Button>(FindObjectsInactive.Exclude,
                                                                   FindObjectsSortMode.None))
                    {
                        if (b.name != name || !b.gameObject.activeInHierarchy) continue;
                        Line("tapping " + what + " (" + b.name + ", interactable=" + b.interactable + ")");
                        b.onClick.Invoke();
                        yield return new WaitForSecondsRealtime(1f);
                        yield break;
                    }
                    yield return new WaitForSecondsRealtime(0.5f);
                }
                Line("WARN: " + what + " ('" + name + "') never appeared — falling back to ShowScreen");
                ScreenManager.Instance?.ShowScreen(ScreenId.GpsHub);
            }

            IEnumerator TapIn(GameObject root, string path, string what)
            {
                Transform t = root != null ? root.transform.Find(path) : null;
                var b = t != null ? t.GetComponent<Button>() : null;
                if (b == null) { Line("WARN: no button at " + path); yield break; }
                Line("tapping " + what + " (interactable=" + b.interactable + ")");
                b.onClick.Invoke();
                yield return null;
            }

            IEnumerator Until(Func<bool> done, float seconds, string what)
            {
                float deadline = Time.realtimeSinceStartup + seconds;
                while (!done() && Time.realtimeSinceStartup < deadline) yield return null;
                Line((done() ? "ok   " : "TIMEOUT ") + what);
            }

            void Line(string s)
            {
                _log.AppendLine(s);
                Debug.Log("[GV-RUN] " + s);
                Flush();
            }

            void Flush() => File.WriteAllText(LogPath, _log.ToString());
        }
    }
}
