#if UNITY_EDITOR
// golfer_3d_test §6 — the acceptance evidence that can only be produced by a running game.
//
// Modelled on QualityTierVerificationRecorder (same boot, same start-gate tap, same CaptureCore
// snap discipline) per the reuse rule for the demo-recorder family. It drives the REAL entry path
// — ShellScene ▸ StartButton ▸ GameplaySceneLoader.BeginGameplayLoad — never a direct LabScaffold
// load, and swings through BotSwing so the shot goes out of whatever control scheme is selected.
//
// It writes Docs/Diagnostics/_capture/golfer_invariants.json: a deterministic per-assertion
// PASS/FAIL dump, which is the gate. The stills are for Cesar.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Golfin.Diagnostics.Runtime;
using Golfin.Gameplay.UI.Quality;

namespace Golfin.EditorTools
{
    public static class GolferTestVerificationRecorder
    {
        const string ShellScenePath = "Assets/Scenes/ShellScene.unity";
        const string ArmedKey = "GolferTestVerification.Armed";
        const string HoleKey  = "GolferTestVerification.Hole";

        [InitializeOnLoadMethod]
        static void RegisterHook()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("GOLFIN/Golfer Test/Verify on Hole 06")]
        public static void VerifyHole06() => Launch(6);

        [MenuItem("GOLFIN/Golfer Test/Verify on Hole 08 (sloped lie)")]
        public static void VerifyHole08() => Launch(8);

        public static void Launch(int hole)
        {
            // §9.9(4): THROW, do not warn. This used to be a LogWarning + silent return, so a
            // launch fired while a previous run was still playing looked like it had succeeded —
            // and the caller then waited minutes for frames from a run that never started. Twice.
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException(
                    "[GolferVerify] refused to launch: the Editor is already in play mode. Stop it " +
                    "first (set isPlaying:false ALONE, then poll until IsPlaying==false && " +
                    "!IsPlayingOrWillChangePlaymode) — see HANDOFF_9_8_ARCHITECT.md §10.");

            // §9.9(4): Console "Error Pause" pauses play mode on the first logged exception, which
            // silently strands an unattended harness run partway through. It is what paused every
            // Mixamo take before the grip block learned to skip.
            DisableConsoleErrorPause();
            // NOT SaveCurrentModifiedScenesIfUserWantsTo(): it opens a modal, and a run driven
            // over MCP or from batchmode has nobody to click it — the Editor sits wedged until a
            // human does. Dirty scenes are DISCARDED here instead, which is the right default for
            // a verification take: it must start from what is committed, not from whatever a
            // previous script left in memory.
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                var sc = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (sc.isDirty) Debug.LogWarning("[GolferVerify] discarding unsaved changes in " + sc.name);
            }
            EditorSceneManager.OpenScene(ShellScenePath, OpenSceneMode.Single);
            // Without this the Game View stops emitting frames while the Editor is unfocused and
            // every capture comes back as the splash frame.
            PlayerSettings.runInBackground = true;
            SessionState.SetBool(ArmedKey, true);
            SessionState.SetInt(HoleKey, hole);
            EditorApplication.EnterPlaymode();
        }

        // ── Video take ───────────────────────────────────────────────────────────────
        //
        // Same run, same assertions, with the Unity Recorder rolling over it. Uses
        // BotVideoRecorder — the sanctioned recorder (com.unity.recorder), the one
        // HoleFlyoverRecorder and the smoke bot already use — rather than a per-task capture
        // path, per CAPTURE RULE 0 and the "CaptureCore/BotVideoRecorder is the only sanctioned
        // path" lesson. Reached by REFLECTION: BotVideoRecorder lives in
        // Golfin.Physics.Viewer.Editor and this file may not add an assembly reference to reach
        // across, exactly as it already reflects to reach ShotController.
        //
        // GameView input, NOT UseCameraInput/TaggedCamera. Rule 4 mandates TaggedCamera for
        // world→screen proof because GameViewInputSettings can bake a systematic Y-flip out of
        // the METAL backbuffer; this is Windows/DX, and TaggedCamera drops URP Overlay cameras,
        // which here would silently throw away the whole HUD. A swing clip is a character
        // animation artefact for Cesar, not a projected-geometry gate, and the gate for §9.2 is
        // the shot.launchDeferredToImpact number either way.
        const string VideoKey = "GolferTestVerification.Video";

        // ── SPEC §9.8 — the Mixamo-native variant ────────────────────────────────────
        //
        // Same hole, same harness, same sequence; only the spawned prefab differs. The variant
        // goes in through GolferTestBootstrap.ResourcePathOverride so BOTH golfers arrive down
        // the identical path (GameSession.OnRoundStarted -> bootstrap -> PlaceAtBall). Spawning
        // the Mixamo one some other way would make the two frames differ by more than the thing
        // under test, which is the whole point of the comparison.
        const string VariantKey = "GolferTestVerification.Variant";

        [MenuItem("GOLFIN/Golfer Test/Verify Mixamo-native on Hole 06 (§9.8)")]
        public static void VerifyMixamoNative()
        {
            SessionState.SetString(VariantKey, "GolferTest/PfGolfer_MixamoNative");
            Launch(6);
        }

        [MenuItem("GOLFIN/Golfer Test/Record video on Hole 06")]
        public static void RecordHole06()
        {
            SessionState.SetBool(VideoKey, true);
            Launch(6);
        }

        /// <summary>
        /// §9.9(4). Clears Console "Error Pause" for the run. Internal API reached by reflection —
        /// there is no public setter — and deliberately silent if it moves in a future Unity: the
        /// harness must still run, it just loses this protection.
        /// </summary>
        static void DisableConsoleErrorPause()
        {
            try
            {
                var t = typeof(EditorApplication).Assembly.GetTypes().FirstOrDefault(x => x.Name == "ConsoleWindow");
                var m = t?.GetMethod("SetConsoleErrorPause",
                                     BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                if (m != null) { m.Invoke(null, new object[] { false }); Debug.Log("[GolferVerify] Console Error Pause cleared for this run."); }
                else Debug.LogWarning("[GolferVerify] could not find SetConsoleErrorPause — if the run pauses partway, that is why.");
            }
            catch (Exception e) { Debug.LogWarning("[GolferVerify] Error Pause not cleared: " + e.Message); }
        }

        static Type BotVideoRecorderType => AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => { try { return a.GetTypes().FirstOrDefault(t => t.Name == "BotVideoRecorder"); } catch { return null; } })
            .FirstOrDefault(t => t != null);

        /// <summary>
        /// ARMS a DEFERRED recording; the runner starts it right before the swing.
        ///
        /// <para>NOT <c>Begin()</c> at play-mode entry — that was tried and produced 6.8 s of the
        /// GOLFIN splash and the NOW LOADING screen. The recorder's 30 s runaway watchdog
        /// force-stops the clip, and this harness spends its first ~40 s passing the start gate,
        /// loading ShellScene, seeding the round and holding for the hole geometry. The interesting
        /// two seconds are three quarters of a minute in, so the recording has to be started from
        /// inside the sequence. <c>ArmDeferred</c>/<c>BeginDeferred</c> exists for exactly this.</para>
        /// </summary>
        static void VideoArm()
        {
            var t = BotVideoRecorderType;
            if (t == null) { Debug.LogWarning("[GolferVerify] BotVideoRecorder not found — no video."); return; }

            string outDir = "Docs/Specs/Active/golfer_3d_test/videos";
            Directory.CreateDirectory(outDir);
            // Path WITHOUT extension — the recorder appends .mp4.
            t.GetProperty("CustomOutputPath")?.SetValue(null,
                outDir + "/golfer_swing_h06_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));
            t.GetMethod("ArmDeferred")?.Invoke(null, null);
            Debug.Log("[GolferVerify] deferred video armed -> " + outDir);
        }

        internal static void VideoBeginDeferred()
        {
            BotVideoRecorderType?.GetMethod("BeginDeferred")?.Invoke(null, null);
            Debug.Log("[GolferVerify] deferred video START");
        }

        internal static void VideoEnd()
        {
            BotVideoRecorderType?.GetMethod("End")?.Invoke(null, null);
        }

        internal static bool VideoArmed => SessionState.GetBool(VideoKey, false);

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                // Belt and braces: the runner ends the clip itself, but if it threw or the
                // watchdog beat it, this is the last chance to mux the MP4.
                if (SessionState.GetBool(VideoKey, false)) { SessionState.SetBool(VideoKey, false); VideoEnd(); }
                return;
            }

            if (state != PlayModeStateChange.EnteredPlayMode) return;
            if (!SessionState.GetBool(ArmedKey, false)) return;
            SessionState.SetBool(ArmedKey, false);

            if (SessionState.GetBool(VideoKey, false)) VideoArm();

            // §9.8: point the bootstrap at the variant BEFORE the round starts. Set by
            // reflection — Golfin.Gameplay.Golfer lives in Golfin.Physics.Viewer via the asmref
            // and this editor file may not name it. Cleared to "" for a normal run so the
            // override can never leak into the next take.
            string variant = SessionState.GetString(VariantKey, "");
            SessionState.SetString(VariantKey, "");
            var bootType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => { try { return a.GetType("Golfin.Gameplay.Golfer.GolferTestBootstrap"); } catch { return null; } })
                .FirstOrDefault(t => t != null);
            bootType?.GetField("ResourcePathOverride", BindingFlags.Public | BindingFlags.Static)
                    ?.SetValue(null, variant);
            Debug.Log("[GolferVerify] spawning " + (string.IsNullOrEmpty(variant) ? "PfGolfer_Test (default)" : variant));

            var host = new GameObject("[GolferTestVerificationBot]");
            UnityEngine.Object.DontDestroyOnLoad(host);
            var runner = host.AddComponent<GolferTestVerificationRunner>();
            runner._variant = variant;   // §9.9(5): drives the golfer_invariants_mixamo.json filename
            runner.Begin(SessionState.GetInt(HoleKey, 6));
        }
    }

    public class GolferTestVerificationRunner : MonoBehaviour
    {
        int _hole;
        readonly StringBuilder _log = new StringBuilder();
        readonly List<string>  _json = new List<string>();
        int _pass, _fail;

        internal string _variant = "";
        float _slideL, _slideR;

        public void Begin(int hole) { _hole = hole; StartCoroutine(Sequence()); }

        void Mark(string m) { _log.AppendLine(m); Debug.Log("[GolferVerify] " + m); }

        void Assert(string id, bool ok, string detail)
        {
            if (ok) _pass++; else _fail++;
            _json.Add("    {\"id\": \"" + id + "\", \"verdict\": \"" + (ok ? "PASS" : "FAIL") +
                      "\", \"detail\": \"" + detail.Replace("\\", "/").Replace("\"", "'") + "\"}");
            Mark((ok ? "PASS " : "FAIL ") + id + " — " + detail);
        }

        /// <summary>
        /// §9.9(4). An assertion that does not apply to THIS rig. Counted as neither pass nor fail
        /// — a SKIP must never read as a green tick (it would inflate the count and hide that the
        /// grip was never measured) and never as a failure (nothing is broken).
        /// </summary>
        int _skip;
        void Skip(string id, string why)
        {
            _skip++;
            _json.Add("    {\"id\": \"" + id + "\", \"verdict\": \"SKIP\", \"detail\": \"" +
                      why.Replace("\\", "/").Replace("\"", "'") + "\"}");
            Mark("SKIP  " + id + " — " + why);
        }

        static string F(float v) => v.ToString("F4", CultureInfo.InvariantCulture);
        static string V(Vector3 v) => "(" + F(v.x) + ", " + F(v.y) + ", " + F(v.z) + ")";
        static IEnumerator Hold(float s) { yield return new WaitForSecondsRealtime(s); }

        static Type FindType(string n) => AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => { try { return a.GetType(n); } catch { return null; } }).FirstOrDefault(t => t != null);

        IEnumerator PassTheStartGate()
        {
            yield return Hold(6f);
            for (int i = 0; i < 20; i++)
            {
                foreach (var b in UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    if (b == null || !b.gameObject.activeInHierarchy) continue;
                    if (b.name != "StartButton" && b.name != "PlayButton") continue;
                    Mark("start-gate: tapping " + b.name);
                    b.onClick.Invoke();
                    yield return Hold(2f);
                    yield break;
                }
                yield return Hold(0.5f);
            }
            Mark("start-gate: no StartButton found (already past it?)");
        }

        IEnumerator Sequence()
        {
            yield return PassTheStartGate();

            if (!SeedAndLoad(_hole)) { Assert("boot.load", false, "SeedAndLoad(" + _hole + ") failed"); yield return Finish(); yield break; }
            yield return WaitForScene("LabScaffold", 60f);
            yield return WaitForScene("Hole_" + _hole.ToString("00") + "_Geo", 60f);
            yield return Hold(8f);

            // ── 1. the golfer exists at all ────────────────────────────────────────────
            var golfer = GameObject.Find("GolferTest");
            Assert("spawn.exists", golfer != null,
                   golfer != null ? "GolferTest instantiated from Resources on GameSession.OnRoundStarted"
                                  : "no GameObject named GolferTest — bootstrap did not fire, or the define is off");
            if (golfer == null) { yield return Finish(); yield break; }

            var anim = golfer.GetComponent<Animator>();
            Assert("spawn.animator", anim != null && anim.runtimeAnimatorController != null && anim.avatar != null && anim.avatar.isHuman,
                   "animator=" + (anim != null) + " controller=" + (anim?.runtimeAnimatorController?.name ?? "<null>") +
                   " avatar=" + (anim?.avatar?.name ?? "<null>") + " isHuman=" + (anim?.avatar?.isHuman));

            var pres = golfer.GetComponent(FindType("Golfin.Gameplay.Golfer.GolferPresenter"));
            Assert("spawn.presenter", pres != null, "GolferPresenter present on the spawned root");

            // ── 2. stance geometry vs the live ball + aim heading ──────────────────────
            var ballT = BallTransform();
            var shot  = FindShotController();
            Assert("bind.shotController", shot != null, shot != null ? "ShotController found in the loaded hole" : "no ShotController");
            yield return Hold(1.5f);
            yield return Snap("golfer_h" + _hole.ToString("00") + "_address");
            LogStance("address", golfer, ballT, shot);

            // THE ASSERTION THAT MATCHES THE PICTURE. Everything below measures bone positions,
            // which are only meaningful if the golfer is actually in an Address state — and for
            // most of this task's life he was not. He stood upright with the club dangling for
            // the whole of a real round, while `shot.addressBeforeSwing` (further down) reported
            // PASS, because that one samples states seen WHILE the harness drives a synthetic
            // drag and the presenter used to address only during input. Nothing asserted the
            // state that is live at the moment the canonical frame is drawn, so a render that
            // was visibly wrong cleared the gate. This does exactly that, on the same frame the
            // PNG above was captured, with no shot in progress — the resting state a player
            // spends nearly all of a hole looking at.
            string liveAtAddress = CurrentState(anim);
            Assert("shot.addressAtRest", liveAtAddress.StartsWith("Address"),
                   "animator state live on the captured address frame (no shot in progress) = '" +
                   liveAtAddress + "' — must be Address_Drive/Address_Putt; 'Idle' means the " +
                   "golfer is standing upright with the club dangling, which is the bug");

            // The grip is asserted HERE, at address, and nowhere else. It used to run after the
            // club-swap section, by which point the shot has gone back to Idle and the lead arm
            // is hanging at the golfer's side — so "is the lead hand on the club" was measuring
            // an arm 0.57 m from the shaft and calling the grip broken. Idle is not a grip: only
            // the trail hand holds the club there, which is correct and is what the animation
            // does. Address is the state the player actually looks at while aiming.
            // ── 4b. the grip: both fists ON the shaft, not balled up beside it ─────────
            {
                var all = golfer.GetComponentsInChildren<Transform>(true);
                Transform Fb(string n) => all.FirstOrDefault(x => x.name == n);
                Vector3 Fist(string side)
                {
                    Vector3 a = Vector3.zero; int k = 0;
                    foreach (var nm in new[] { "middle_02_", "index_02_", "ring_02_", "thumb_03_", "middle_01_" })
                    { var tr = Fb(nm + side); if (tr != null) { a += tr.position; k++; } }
                    return k > 0 ? a / k : Vector3.zero;
                }
                var slot = Fb("ClubSlot");

                // §9.9(4): the whole grip block is Quaternius-rig-specific — it addresses bones by
                // their Quaternius names (middle_02_r, index_04_leaf_l …). On a Mixamo rig those
                // resolve to null, Fist() returns Vector3.zero, and every measurement below became
                // the distance from the shaft to the WORLD ORIGIN — 70–78 m — while the null
                // dereferences threw. Those exceptions are what tripped Console Error Pause and
                // silently paused every Mixamo run partway. Skip honestly instead: the grip is not
                // under test on a rig this block cannot address, and §9.8 forbids grip work anyway.
                bool hasQuaterniusFingers = Fb("middle_02_r") != null;
                if (!hasQuaterniusFingers)
                {
                    foreach (var id in new[] { "grip.rightFistOnShaft", "grip.fingersClosed",
                                               "grip.leadHandOnGrip", "grip.handsJoined",
                                               "grip.wrapped_r", "grip.wrapped_l",
                                               "grip.thumbDownShaft_r", "grip.thumbDownShaft_l" })
                        Skip(id, "N/A — rig has no Quaternius finger bones");
                }

                if (slot != null && hasQuaterniusFingers)
                {
                    float gapR = Vector3.Cross(slot.up, Fist("r") - slot.position).magnitude;
                    float gapL = Vector3.Cross(slot.up, Fist("l") - slot.position).magnitude;
                    Assert("grip.rightFistOnShaft", gapR < 0.03f,
                           "right fist centre is " + F(gapR) + " m from the shaft line");
                    Assert("grip.fingersClosed",
                           Vector3.Distance(Fb("middle_04_leaf_r").position, Fb("middle_01_r").position) < 0.055f,
                           "right middle finger tip-to-knuckle " +
                           F(Vector3.Distance(Fb("middle_04_leaf_r").position, Fb("middle_01_r").position)) +
                           " m (straight ~0.09, fist ~0.04)");
                    // A real grip is JOINED: lead hand at the top of the grip, trail hand
                    // immediately below it and touching (right pinky over the left index, right
                    // palm over the left thumb). Measured as stations along the shaft from the
                    // butt — before this, the lead fist sat 0.0057 m ABOVE the butt, i.e. holding
                    // air past the grip cap, which is what "not held correctly" looked like.
                    float handWidth = Vector3.Distance(Fb("hand_r").position, Fb("middle_01_r").position);
                    float alongL = Vector3.Dot(Fist("l") - slot.position, slot.up);
                    float alongR = Vector3.Dot(Fist("r") - slot.position, slot.up);
                    Assert("grip.leadHandOnGrip", alongL > 0.005f && alongL < 0.09f,
                           "lead fist sits " + F(alongL) + " m below the butt cap (want ~0.03, " +
                           "negative means it is off the end of the club)");
                    Assert("grip.handsJoined", Mathf.Abs((alongR - alongL) - handWidth) < 0.06f,
                           "fists are " + F(alongR - alongL) + " m apart along the shaft; one hand " +
                           "width is " + F(handWidth) + " m (a joined grip is about one hand width)");
                    Mark("grip: left fist " + F(gapL) + " m off the shaft line laterally");

        
            // ── 3. the golfer turns with the aim heading ───────────────────────────────
            if (shot != null && ballT != null)
            {
                float h0 = Heading(shot);
                Vector3 f0 = golfer.transform.forward;
                SetHeading(shot, h0 + 0.6f);                    // ~34 degrees
                yield return Hold(1.0f);
                Vector3 f1 = golfer.transform.forward;
                float turned = Vector3.Angle(f0, f1);
                Assert("stance.followsHeading", turned > 25f && turned < 45f,
                       "heading +34.4 deg -> golfer forward turned " + F(turned) + " deg (expected ~34)");
                yield return Snap("golfer_h" + _hole.ToString("00") + "_heading_turned");
                SetHeading(shot, h0);
                yield return Hold(1.0f);
            }

            // ── 4. putter mode swaps the club mesh ─────────────────────────────────────
            var drv = FindChild(golfer, "GOLFIN_Driver");
            var ptr = FindChild(golfer, "GOLFIN_Putter");
            Assert("club.bothPresent", drv != null && ptr != null,
                   "driver=" + (drv != null) + " putter=" + (ptr != null));
            if (drv != null && ptr != null)
            {
                Assert("club.driverDefault", drv.activeSelf && !ptr.activeSelf,
                       "driver active=" + drv.activeSelf + " putter active=" + ptr.activeSelf);
                SetIsPutt(shot, true);
                Golfin.Gameplay.UI.ShotUI.ClubSelectionBroadcast.SetPutterMode(true, 0);
                yield return Hold(0.8f);
                Assert("club.putterSwap", !drv.activeSelf && ptr.activeSelf,
                       "after OnPutterModeChanged(true): driver=" + drv.activeSelf + " putter=" + ptr.activeSelf +
                       " animator IsPutt=" + (anim != null && anim.GetBool("IsPutt")));
                yield return Snap("golfer_h" + _hole.ToString("00") + "_putter");
                SetIsPutt(shot, false);
                Golfin.Gameplay.UI.ShotUI.ClubSelectionBroadcast.SetPutterMode(false, 0);
                yield return Hold(0.8f);
                Assert("club.driverSwapBack", drv.activeSelf && !ptr.activeSelf,
                       "after OnPutterModeChanged(false): driver=" + drv.activeSelf + " putter=" + ptr.activeSelf);

                // ── SPEC §9.4 — the putter fingertip, measured ONCE, on a green ────────
                //
                // Recorded as a measurement, NOT a pass/fail gate. §9.3 declares the grip
                // numbers final for the stand-in, so promoting this to an assertion would
                // hard-wire a red board for a defect nobody intends to fix. The number is
                // what §9.4 asked for; the decision to gate on it is Cesar's.
                //
                // ON A GREEN, and that is the point. Grip geometry is bone-space and does not
                // care where the golfer stands — but the PUTT ADDRESS POSE does. Address_Putt
                // is a different clip at a different cycleOffset from Address_Drive, so the
                // hands sit differently on a shorter shaft. Measuring it at the tee with the
                // driver pose blended in is what produced the ambiguous 0.0429 m.
                yield return PuttGripOnGreen(golfer, shot, anim);
            }

            // ── does the hand actually WRAP the shaft, or just sit beside it? ──
                    //
                    // The assertions above all pass on a hand that is in the right PLACE with
                    // its fingers open — which is exactly what shipped and got rejected five
                    // times. A finger is wrapped when its far end lies about one finger-radius
                    // off the grip's surface; a 24 mm grip plus a 9 mm finger puts contact at
                    // 0.021 m from the shaft's centre line. Measured here from the bones rather
                    // than read back off the presenter, so a solve that silently no-ops fails
                    // instead of confirming itself.
                    const float contact = 0.012f + 0.009f;
                    foreach (var side in new[] { "r", "l" })
                    {
                        float worst = 0f; string worstFinger = "?";
                        foreach (var fng in new[] { "index", "middle", "ring", "pinky" })
                        {
                            var tip = Fb(fng + "_04_leaf_" + side) ?? Fb(fng + "_03_" + side);
                            if (tip == null) continue;
                            float d = Vector3.Cross(slot.up, tip.position - slot.position).magnitude;
                            if (d > worst) { worst = d; worstFinger = fng; }
                        }
                        Assert("grip.wrapped_" + side, worst < contact * 2f,
                               side + "-hand worst fingertip (" + worstFinger + ") is " + F(worst) +
                               " m from the shaft centre line; contact is " + F(contact) +
                               " m, so anything past " + F(contact * 2f) + " m is not touching the club");
                    }

                    // Thumbs run DOWN the shaft on a golf grip, not curled into the fist.
                    foreach (var side in new[] { "r", "l" })
                    {
                        var root = Fb("thumb_01_" + side);
                        var tip  = Fb("thumb_04_leaf_" + side) ?? Fb("thumb_03_" + side);
                        if (root == null || tip == null) continue;
                        float deg = Vector3.Angle(tip.position - root.position, slot.up);
                        Assert("grip.thumbDownShaft_" + side, deg < 45f,
                               side + "-hand thumb is " + deg.ToString("F1") +
                               " deg off the shaft axis (down the shaft is 0, curled into the fist is ~90)");
                    }
                }
            }

            // ── 5. frame-time delta with vs without the golfer ─────────────────────────
            float withGolfer = 0f, withoutGolfer = 0f;
            yield return MeasureFrameMs(120, r => withGolfer = r);
            golfer.SetActive(false);
            yield return Hold(1f);
            yield return MeasureFrameMs(120, r => withoutGolfer = r);
            golfer.SetActive(true);
            yield return Hold(1f);
            Assert("perf.frameDelta", (withGolfer - withoutGolfer) <= 1.0f,
                   "median frame ms with=" + F(withGolfer) + " without=" + F(withoutGolfer) +
                   " delta=" + F(withGolfer - withoutGolfer) + " (Editor, not device)");

            // ── 6. tri count of the rendered golfer + club ─────────────────────────────
            int tris = 0; var parts = new StringBuilder();
            foreach (var r in golfer.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.gameObject.activeInHierarchy || !r.enabled) continue;
                Mesh m = (r as SkinnedMeshRenderer)?.sharedMesh ?? r.GetComponent<MeshFilter>()?.sharedMesh;
                if (m == null) continue;
                int t = 0; for (int s = 0; s < m.subMeshCount; s++) t += (int)(m.GetIndexCount(s) / 3);
                tris += t; parts.Append(r.name).Append('=').Append(t).Append(' ');
            }
            Assert("budget.tris", tris <= 15000, "rendered tris = " + tris + " (limit 15000) | " + parts.ToString().Trim());

            // ── 7. quality tier: Low = Bone2, no shadows ───────────────────────────────
            QualityTierService.SetOverride((int)QualityTier.Low);
            yield return Hold(1.5f);
            var smrs = golfer.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            bool lowOk = smrs.Length > 0 && smrs.All(s => s.quality == SkinQuality.Bone2 &&
                                                          s.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.Off);
            Assert("tier.low", lowOk, "Low: " + string.Join(", ", smrs.Select(s => s.name + " q=" + s.quality + " shadow=" + s.shadowCastingMode)) +
                   " animatorCulling=" + (anim != null ? anim.cullingMode.ToString() : "?"));
            yield return Snap("golfer_h" + _hole.ToString("00") + "_low_tier");

            QualityTierService.SetOverride((int)QualityTier.High);
            yield return Hold(1.5f);
            bool highOk = smrs.All(s => s.quality == SkinQuality.Bone4 &&
                                        s.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.On);
            Assert("tier.high", highOk, "High: " + string.Join(", ", smrs.Select(s => s.name + " q=" + s.quality + " shadow=" + s.shadowCastingMode)));
            QualityTierService.SetOverride(QualityTierService.AutoPref);
            PlayerPrefs.DeleteKey(QualityTierService.PrefKey); PlayerPrefs.Save();
            yield return Hold(1f);

            // ── 8. a REAL shot: commit -> swing, ball rest -> re-placed ────────────────
            Vector3 ballBefore = ballT != null ? ballT.position : Vector3.zero;
            Vector3 golferBefore = golfer.transform.position;
            string stateBefore = CurrentState(anim);

            // Sample the animator WHILE the shot is being set up: the golfer must be in an
            // Address state, not standing in Idle with a club stretched out to the ball.
            // Roll the camera HERE, not at play-mode entry: the first ~40 s of this sequence is
            // the start gate, the scene loads and the settle holds, and the recorder's 30 s
            // runaway watchdog would spend the whole clip on the NOW LOADING screen (it did).
            // A few seconds of address, then the swing, then the ball leaving at impact.
            if (GolferTestVerificationRecorder.VideoArmed)
            {
                GolferTestVerificationRecorder.VideoBeginDeferred();
                yield return Hold(2.0f);        // a beat at address before he moves
            }

            var addrSeen = new List<string>();
            var addrProbe = StartCoroutine(SampleStates(anim, addrSeen, 20, 0.1f));
            // §9.2 evidence + gate, running alongside the swing: the gameplay camera at
            // t = 0.6 s after commit, plus the assertion that the ball has NOT left yet.
            var deferProbe = StartCoroutine(ProveLaunchDeferred(shot, anim, 0.6f));
            var slideProbe = StartCoroutine(MeasureFootSlide(golfer, anim));
            yield return DriveARealShot(shot);
            if (addrProbe != null) StopCoroutine(addrProbe);
            yield return deferProbe;
            yield return slideProbe;
            bool addressed = addrSeen.Any(x => x.StartsWith("Address"));
            // NOT a render check, and it must never be read as one: it samples states seen ACROSS
            // the drag, so a single Address frame anywhere in that window passes it. The gate for
            // "is he actually at address" is shot.addressAtRest, up at the canonical frame.
            Assert("shot.addressBeforeSwing", addressed,
                   "animator states seen at any point while the shot was being set up (sampled " +
                   "across the drag, NOT the state on any one rendered frame): " +
                   string.Join(", ", addrSeen.Distinct().Take(6)));
            yield return Hold(0.35f);
            string stateAtSwing = CurrentState(anim);
            Assert("shot.swingPlays", stateAtSwing.StartsWith("Swing"),
                   "animator state right after OnShotResolved = '" + stateAtSwing + "' (was '" + stateBefore + "')");
            yield return Snap("golfer_h" + _hole.ToString("00") + "_swing");

            // Close the clip once the ball is clearly away. Ending here rather than at
            // ball-at-rest keeps it inside the recorder's 30 s watchdog — a 247 m drive can
            // outlast it — and the follow-through plus the launch is the whole point.
            if (GolferTestVerificationRecorder.VideoArmed)
            {
                yield return Hold(4.0f);
                GolferTestVerificationRecorder.VideoEnd();
                Mark("video: clip closed after the swing + 4 s of ball flight");
            }

            // Wait for the BALL to settle, not for a stopwatch: a 247 m drive on Hole 08 takes
            // longer than any fixed hold, and measuring early reads the golfer at the tee and
            // calls a working re-placement a failure (it did, on the first take).
            yield return WaitForBallAtRest(45f);
            var ballAfterT = BallTransform();
            Vector3 ballAfter = ballAfterT != null ? ballAfterT.position : ballBefore;
            Vector3 golferAfter = golfer.transform.position;
            float ballMoved = Vector3.Distance(new Vector3(ballBefore.x,0,ballBefore.z), new Vector3(ballAfter.x,0,ballAfter.z));
            float golferMoved = Vector3.Distance(new Vector3(golferBefore.x,0,golferBefore.z), new Vector3(golferAfter.x,0,golferAfter.z));
            Assert("shot.ballMoved", ballMoved > 5f, "ball travelled " + F(ballMoved) + " m in plan");
            Assert("shot.golferFollowed", golferMoved > 5f && Mathf.Abs(golferMoved - ballMoved) < ballMoved * 0.25f + 2f,
                   "golfer moved " + F(golferMoved) + " m (ball " + F(ballMoved) + " m) — re-placed at the new lie on OnShotComplete");
            // "Idle OR Address" is exactly the looseness that let the bug live here for a whole
            // task. The ball has re-armed by now (the owner calls ReArm on the AtRest branch, the
            // same beat the camera returns to aiming framing), so the player is looking at the
            // next shot and the golfer must be OVER it — Idle at this point is him standing bolt
            // upright with the club dangling, which is the reported defect one shot later. The
            // old assertion accepted precisely that and reported PASS on it.
            //
            // Bounded wait rather than an instant read: re-arm and the animator transition land
            // within a frame or two of ball-at-rest, and "returns to address promptly" is the
            // real requirement.
            float addrDeadline = Time.realtimeSinceStartup + 3f;
            while (!CurrentState(anim).StartsWith("Address") && Time.realtimeSinceStartup < addrDeadline)
                yield return null;
            LogStance("atRest", golfer, ballAfterT, shot);
            yield return Snap("golfer_h" + _hole.ToString("00") + "_atrest");
            string atRestState = CurrentState(anim);
            Assert("shot.addressAfterShot", atRestState.StartsWith("Address"),
                   "animator state once the ball is at rest and re-armed = '" + atRestState +
                   "' — must be an Address state; 'Idle' means he stands upright with the club " +
                   "dangling while the player lines up the next shot");

            yield return Finish();
        }

        /// <summary>
        /// Stance invariants, as numbers: distance from the ball, whether the golfer faces the
        /// ball, whether the target line is on his LEFT, and how far his soles are off the ground
        /// under him. All four are what "stands beside the ball, facing perpendicular to the aim,
        /// feet on the ground" means (SPEC §6), and none of them is a judgement about a picture.
        /// </summary>
        void LogStance(string tag, GameObject golfer, Transform ball, Component shot)
        {
            if (ball == null) { Assert("stance." + tag + ".ball", false, "no ball transform"); return; }
            Vector3 g = golfer.transform.position, b = ball.position;
            float dist = Vector3.Distance(new Vector3(g.x,0,g.z), new Vector3(b.x,0,b.z));
            Assert("stance." + tag + ".distance", dist > 0.4f && dist < 1.4f,
                   "golfer->ball plan distance = " + F(dist) + " m (stanceDistance 0.75)");

            // THE assertion. Angles were the wrong question: a stance can be 90 deg off and
            // still satisfy "faces the ball" and "perpendicular to the aim" — the first version
            // passed both while the club head sat 1.05 m from the ball. What matters is whether
            // the club reaches what he is supposed to hit.
            var presType = FindType("Golfin.Gameplay.Golfer.GolferPresenter");
            var headProp = presType?.GetProperty("AddressClubHeadWorld");
            var pres2 = presType != null ? golfer.GetComponent(presType) : null;
            if (headProp != null && pres2 != null)
            {
                Vector3 head = (Vector3)headProp.GetValue(pres2);
                float gap = Vector3.Distance(new Vector3(head.x, 0f, head.z), new Vector3(b.x, 0f, b.z));
                Assert("stance." + tag + ".clubReachesBall", gap < 0.20f,
                       "club head at address is " + F(gap) + " m from the ball in plan " +
                       "(head=" + V(head) + " ball=" + V(b) + ")");
            }
            else Assert("stance." + tag + ".clubReachesBall", false, "AddressClubHeadWorld not reachable");

            if (shot != null)
            {
                // The swing must travel down the aim line, not across it.
                float h = Heading(shot);
                Vector3 aim = new Vector3(Mathf.Cos(h), 0f, Mathf.Sin(h));
                // Local +Z is the ball-flight direction, measured as the club-head velocity AT
                // impact: 26.75 m/s along local (0.05, 0, 0.999). NOT the address-to-impact
                // difference, which is near-zero, noise-dominated, and points the other way —
                // believing it is what put the golfer on the wrong side of the ball.
                Vector3 swing = golfer.transform.forward;
                float off = Vector3.Angle(swing, aim);
                Assert("stance." + tag + ".swingsDownTheAim", off < 15f,
                       "angle(swing direction, aim) = " + F(off) + " deg");
            }

            var smr = golfer.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(s => s.sharedMesh != null && s.sharedMesh.vertexCount > 5000);
            float soleGap = float.NaN;
            if (UnityEngine.Physics.Raycast(g + Vector3.up * 2f, Vector3.down, out var hit, 8f, ~0, QueryTriggerInteraction.Ignore))
                soleGap = g.y - hit.point.y;
            Assert("stance." + tag + ".onGround", !float.IsNaN(soleGap) && Mathf.Abs(soleGap) < 0.20f,
                   "golfer root Y minus ground hit Y = " + F(soleGap) + " m (ray hit " + (float.IsNaN(soleGap) ? "NONE" : hit.collider.name) + ")");
            Mark("stance." + tag + " golfer=" + V(g) + " ball=" + V(b) + " fwd=" + V(golfer.transform.forward));
        }

        /// <summary>
        /// Bots swing through BotSwing, never BeginExternalDrag/CommitFlick — so this take exercises
        /// whatever control scheme happens to be selected (bot_scheme_parity §3.5).
        /// </summary>
        IEnumerator DriveARealShot(Component shot)
        {
            if (shot == null) { Assert("shot.driver", false, "no ShotController to swing"); yield break; }

            // bot_scheme_parity §3.5: bots swing through BotSwing, never BeginExternalDrag /
            // CommitFlick, so this take goes out through whatever control scheme is selected.
            // Golfin.Gameplay.UI is autoReferenced, so these are named directly — the earlier
            // reflection lookup used the wrong namespace and silently fired no shot at all.
            var ctx = Golfin.Gameplay.UI.Controls.Bot.BotExecutionContext.Resolve();
            var executor = Golfin.Gameplay.UI.Controls.Bot.BotSwing.ResolveExecutor();
            Assert("shot.driver", executor != null,
                   "swinging through BotSwing.PlayPerfect; active executor = " +
                   (executor?.GetType().Name ?? "<null>"));
            yield return Golfin.Gameplay.UI.Controls.Bot.BotSwing.PlayPerfect(
                power01: 0.85f, aimYawRad: Heading(shot), isPutt: false, ctx: ctx);
        }

        /// <summary>
        /// SPEC §9.2 — proves the ball launch is held back to the swing's impact frame.
        ///
        /// <para>THE PICTURE AND THE NUMBER, from the same moment. §9.2 asks for a gameplay-camera
        /// frame at t = 0.6 s after commit; a frame alone only shows that something looked right,
        /// so the same instant is also asserted: the golfer must be mid-SWING and the ball must
        /// still be sitting where it was at commit. Drive impact is 1.167 s, so at 0.6 s the ball
        /// has not been struck. Before §9.2 the ball left on the commit frame, which is exactly
        /// what put the cut-to-ball on a golfer who had not moved.</para>
        ///
        /// <para>Capture is the Game View through <c>CaptureCore.SnapPlayModeSafe</c> (see
        /// <see cref="Snap"/>) — the gameplay camera, no harness camera, per §9.2 and CAPTURE
        /// RULE 0.</para>
        /// </summary>
        IEnumerator ProveLaunchDeferred(Component shot, Animator anim, float atSeconds)
        {
            var stateProp = shot?.GetType().GetProperty("State");
            if (stateProp == null) { Mark("§9.2 probe: no State property — skipped"); yield break; }

            // Commit == the frame State becomes Resolving.
            float deadline = Time.realtimeSinceStartup + 25f;
            while (Time.realtimeSinceStartup < deadline &&
                   stateProp.GetValue(shot)?.ToString() != "Resolving")
                yield return null;

            if (stateProp.GetValue(shot)?.ToString() != "Resolving")
            { Mark("§9.2 probe: never reached Resolving within 25 s — skipped"); yield break; }

            var b0 = BallTransform();
            Vector3 ballAtCommit = b0 != null ? b0.position : Vector3.zero;

            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < atSeconds) yield return null;

            var b1 = BallTransform();
            Vector3 ballNow = b1 != null ? b1.position : ballAtCommit;
            float moved = Vector3.Distance(ballAtCommit, ballNow);
            string st = CurrentState(anim);

            yield return Snap("golfer_h" + _hole.ToString("00") + "_t0_6_after_commit");

            // Read the impact constant off the live type: Golfin.Gameplay.Input is
            // autoReferenced:false, so no editor assembly may NAME ShotController.
            object impact = shot.GetType()
                .GetField("GolferImpactDelayDriveSeconds", BindingFlags.Public | BindingFlags.Static)
                ?.GetValue(null);

            Assert("shot.launchDeferredToImpact", moved < 0.05f && st.StartsWith("Swing"),
                   "at t=" + F(Time.realtimeSinceStartup - t0) + " s after commit the ball has moved " +
                   F(moved) + " m (want < 0.05 — impact is at " +
                   (impact == null ? "<no GolferImpactDelayDriveSeconds — define off?>" : F((float)impact)) +
                   " s) and the animator is '" + st + "' (want a Swing state). Before §9.2 the ball " +
                   "left on the commit frame, so this measured metres and the cut landed on a " +
                   "golfer who had not moved.");
        }

        /// <summary>
        /// SPEC §9.8 — foot slide during the swing, the number that separates "the clips are
        /// wrong" from "the retarget is wrong".
        ///
        /// <para>A planted foot should not travel. Unity Humanoid retargeting has no foot pinning,
        /// so mocap replayed on a body of different proportions drags the feet along the ground —
        /// that is the sliding-legs artefact §9 blames on retargeting. A character animated by
        /// clips authored on ITS OWN skeleton has nothing to retarget and should slide far less.
        /// Measured in the golfer's own space so his re-placement at the ball cannot be mistaken
        /// for slide, and reported for BOTH prefabs with no tuning either way.</para>
        ///
        /// <para>Peak-to-peak of each foot's planar position across the swing, taking the worse
        /// foot. Peak-to-peak rather than start-to-end because a foot that slides out and comes
        /// back has still slid.</para>
        /// </summary>
        IEnumerator MeasureFootSlide(GameObject golfer, Animator anim)
        {
            Transform Foot(HumanBodyBones b)
            {
                if (anim != null && anim.avatar != null && anim.avatar.isHuman)
                { var t = anim.GetBoneTransform(b); if (t != null) return t; }
                string want = b == HumanBodyBones.LeftFoot ? "foot_l" : "foot_r";
                return golfer.GetComponentsInChildren<Transform>(true)
                             .FirstOrDefault(x => x.name == want ||
                                                  x.name.EndsWith(b == HumanBodyBones.LeftFoot ? "LeftFoot" : "RightFoot"));
            }
            var fl = Foot(HumanBodyBones.LeftFoot);
            var fr = Foot(HumanBodyBones.RightFoot);
            if (fl == null || fr == null)
            { Mark("§9.8 foot-slide SKIPPED: feet not resolvable (l=" + (fl != null) + " r=" + (fr != null) + ")"); yield break; }

            var lo = new Vector2(float.MaxValue, float.MaxValue); var hi = new Vector2(float.MinValue, float.MinValue);
            var lo2 = lo; var hi2 = hi;
            void Acc(Transform f, ref Vector2 mn, ref Vector2 mx)
            {
                // Golfer-local, so PlaceAtBall moving him down the fairway is not counted as slide.
                Vector3 p = golfer.transform.InverseTransformPoint(f.position);
                mn = Vector2.Min(mn, new Vector2(p.x, p.z));
                mx = Vector2.Max(mx, new Vector2(p.x, p.z));
            }

            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 2.5f)
            { Acc(fl, ref lo, ref hi); Acc(fr, ref lo2, ref hi2); yield return null; }

            float slideL = (hi - lo).magnitude;
            _slideL = slideL;
            float slideR = (hi2 - lo2).magnitude;
            _slideR = slideR;
            float worst  = Mathf.Max(slideL, slideR);
            Mark("§9.8 foot-slide during the swing: left=" + F(slideL) + " m  right=" + F(slideR) +
                 " m  WORST=" + F(worst) + " m (planted feet should not travel; golfer-local, " +
                 "peak-to-peak over 2.5 s from commit)");
        }

        /// <summary>Blocks until the ball has not moved for 1.5 s, or the timeout expires.</summary>
        IEnumerator WaitForBallAtRest(float timeout)
        {
            float t = 0f, still = 0f;
            Vector3 last = BallTransform() != null ? BallTransform().position : Vector3.zero;
            while (t < timeout)
            {
                yield return new WaitForSecondsRealtime(0.25f);
                t += 0.25f;
                var b = BallTransform();
                Vector3 now = b != null ? b.position : last;
                still = Vector3.Distance(now, last) < 0.01f ? still + 0.25f : 0f;
                last = now;
                if (still >= 1.5f && t > 3f) { Mark("ball at rest after " + F(t) + " s"); yield break; }
            }
            Mark("ball did NOT settle within " + F(timeout) + " s");
        }

        IEnumerator SampleStates(Animator a, List<string> into, int n, float dt)
        {
            for (int i = 0; i < n; i++) { into.Add(CurrentState(a)); yield return new WaitForSecondsRealtime(dt); }
        }

        IEnumerator MeasureFrameMs(int frames, Action<float> result)
        {
            var samples = new List<float>(frames);
            for (int i = 0; i < frames; i++) { yield return null; samples.Add(Time.unscaledDeltaTime * 1000f); }
            samples.Sort();
            result(samples[samples.Count / 2]);
        }

        /// <summary>
        /// SPEC §9.4. Puts the ball on the hole's green, switches to the putter, waits for the
        /// golfer to settle into Address_Putt, and measures the worst fingertip against the
        /// PUTTER's shaft axis. Logged as a measurement (see the call site for why it is not an
        /// assertion) and restored to driver-at-the-original-lie afterwards so nothing downstream
        /// sees a mutated world.
        /// </summary>
        IEnumerator PuttGripOnGreen(GameObject golfer, Component shot, Animator anim)
        {
            var labType = FindType("Golfin.Physics.Viewer.PhysicsLabController");
            var lab = labType == null ? null : UnityEngine.Object.FindFirstObjectByType(labType) as Component;
            var placeBallAt = labType?.GetMethod("PlaceBallAt");
            var ballT0 = BallTransform();
            Vector3 lie0 = ballT0 != null ? ballT0.position : Vector3.zero;

            Vector3 target = Golfin.Gameplay.UI.HUD.HoleContext.PinWorld;
            if (target == Vector3.zero) target = Golfin.Gameplay.UI.HUD.HoleContext.GreenCentroidWorld;
            if (lab == null || placeBallAt == null || target == Vector3.zero)
            {
                Mark("putt-grip §9.4 SKIPPED: lab=" + (lab != null) + " PlaceBallAt=" + (placeBallAt != null) +
                     " pin/green=" + V(target) + " — cannot reach a green, so no number is reported " +
                     "(a guessed one would be worse than none)");
                yield break;
            }

            // 1 == Golfin.Course.SurfaceType.Green, per PlaceBallAt's own doc comment.
            placeBallAt.Invoke(lab, new object[] { target + new Vector3(1.5f, 0f, 0f), (int?)1 });
            SetIsPutt(shot, true);
            Golfin.Gameplay.UI.ShotUI.ClubSelectionBroadcast.SetPutterMode(true, 0);
            yield return Hold(1.5f);

            // RESTAGE THROUGH IDLE, and note WHY this is needed — it is a finding in its own
            // right. Setting putter mode while the golfer is already at address leaves him in
            // Address_Drive holding a putter: Address_Drive's only transitions are Swing and
            // Cancel, so there is no Address_Drive -> Address_Putt edge and the IsPutt bool has
            // nothing to act on. The first run of this measurement read animator=Address_Drive
            // and produced numbers against the wrong pose AND a shorter shaft.
            //
            // In a real round this does not bite: the ball comes to rest on the green, auto club
            // selection sets IsPutt, and only THEN does re-arm fire the Address trigger — so the
            // Idle -> Address_Putt edge is picked correctly. It bites only when the club changes
            // while he is already standing over the ball, which is a real thing a player can do
            // from the club widget. Reported, not fixed here (§9.3 froze the grip work).
            //
            // The nudge below is measurement-only scaffolding and is disclosed as such: it drives
            // the animator through Idle so the Address trigger re-evaluates with IsPutt true.
            if (anim != null && CurrentState(anim) == "Address_Drive")
            {
                Mark("putt-grip §9.4 NOTE: club swapped at address left him in Address_Drive with a " +
                     "putter (no Address_Drive->Address_Putt edge). Restaging through Idle to measure " +
                     "the real putt pose. See comment at GolferTestVerificationRecorder.PuttGripOnGreen.");
                anim.SetTrigger("Cancel");
                yield return Hold(0.8f);
                anim.SetTrigger("Address");
                yield return Hold(1.2f);
            }

            string st = CurrentState(anim);
            var all = golfer.GetComponentsInChildren<Transform>(true);
            Transform Fb(string n) => all.FirstOrDefault(x => x.name == n);
            var slot = Fb("ClubSlot");

            if (slot == null || st != "Address_Putt")
            {
                Mark("putt-grip §9.4 SKIPPED: animator='" + st + "' (wanted Address_Putt) slot=" +
                     (slot != null) + " — a fingertip measured against the driver pose or a " +
                     "non-address pose is not the number §9.4 asked for, so none is reported");
            }
            else
            {
                const float contact = 0.012f + 0.009f;   // 24 mm grip + 9 mm finger, as § grip
                var sb = new StringBuilder("putt-grip §9.4 on green (animator=" + st + "): ");
                foreach (var side in new[] { "r", "l" })
                {
                    float worst = 0f; string worstFinger = "?";
                    foreach (var fng in new[] { "index", "middle", "ring", "pinky" })
                    {
                        var tip = Fb(fng + "_04_leaf_" + side) ?? Fb(fng + "_03_" + side);
                        if (tip == null) continue;
                        float d = Vector3.Cross(slot.up, tip.position - slot.position).magnitude;
                        if (d > worst) { worst = d; worstFinger = fng; }
                    }
                    sb.Append(side).Append("-hand worst=").Append(worstFinger).Append(' ').Append(F(worst))
                      .Append(" m (contact ").Append(F(contact)).Append(", gate ").Append(F(contact * 2f)).Append("); ");
                }
                Mark(sb.ToString());
                yield return Snap("golfer_h" + _hole.ToString("00") + "_putt_green");
            }

            // Restore: driver, original lie. A measurement must not leave the world changed.
            Golfin.Gameplay.UI.ShotUI.ClubSelectionBroadcast.SetPutterMode(false, 0);
            SetIsPutt(shot, false);
            if (lie0 != Vector3.zero) placeBallAt.Invoke(lab, new object[] { lie0, (int?)null });
            yield return Hold(1.5f);
        }

        static string CurrentState(Animator a)
        {
            if (a == null) return "<no animator>";
            var i = a.GetCurrentAnimatorStateInfo(0);
            foreach (var n in new[] { "Idle", "Address_Drive", "Address_Putt", "Swing_Drive", "Swing_Putt" })
                if (i.IsName(n)) return n;
            return "<unknown " + i.shortNameHash + ">";
        }

        // Golfin.Gameplay.Input is autoReferenced:false, so no editor assembly may NAME
        // ShotController. Reached as a Component, with the heading read/written by reflection.
        static Component FindShotController()
        {
            var t = FindType("Golfin.Gameplay.Input.ShotController");
            if (t == null) return null;
            return UnityEngine.Object.FindFirstObjectByType(t) as Component;
        }

        static float Heading(Component shot)
        {
            var p = shot?.GetType().GetProperty("CameraHeadingRadians");
            return p == null ? 0f : (float)p.GetValue(shot);
        }

        static void SetIsPutt(Component shot, bool v)
            => shot?.GetType().GetProperty("IsPutt")?.SetValue(shot, v);

        static void SetHeading(Component shot, float v)
            => shot?.GetType().GetProperty("CameraHeadingRadians")?.SetValue(shot, v);

        static GameObject FindChild(GameObject root, string name)
            => root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name)?.gameObject;

        static Transform BallTransform()
        {
            var t = FindType("Golfin.Physics.Viewer.BallAnimator");
            var inst = t?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            return inst == null ? null : (Transform)t.GetProperty("CurrentBall").GetValue(inst);
        }

        IEnumerator Finish()
        {
            // §9.9(5) asks for the variant's numbers as golfer_invariants_mixamo.json — a separate
            // file, so the Quaternius baseline is not overwritten by the run it is compared against.
            string file = string.IsNullOrEmpty(_variant)
                ? "golfer_invariants.json"
                : "golfer_invariants_mixamo.json";

            string json = "{\n  \"task\": \"golfer_3d_test\",\n  \"hole\": " + _hole +
                          ",\n  \"prefab\": \"" + (string.IsNullOrEmpty(_variant) ? "PfGolfer_Test" : _variant) + "\"" +
                          ",\n  \"pass\": " + _pass + ",\n  \"fail\": " + _fail +
                          ",\n  \"skip\": " + _skip +
                          ",\n  \"footSlideLeftM\": " + F(_slideL) +
                          ",\n  \"footSlideRightM\": " + F(_slideR) +
                          ",\n  \"assertions\": [\n" + string.Join(",\n", _json) + "\n  ]\n}\n";
            Directory.CreateDirectory("Docs/Diagnostics/_capture");
            File.WriteAllText("Docs/Diagnostics/_capture/" + file, json);
            Debug.Log("[GolferVerify] ===== SUMMARY  pass=" + _pass + " fail=" + _fail + " skip=" + _skip +
                      "  footSlide L=" + F(_slideL) + " R=" + F(_slideR) + " =====\n" + _log +
                      "\nwrote Docs/Diagnostics/_capture/" + file);
            yield return Hold(0.5f);
            EditorApplication.isPlaying = false;
        }

        static IEnumerator Snap(string label)
        {
            yield return new WaitForEndOfFrame();
            string path = CaptureCore.SnapPlayModeSafe(label);
            bool ok = !string.IsNullOrEmpty(path) && File.Exists(path);
            Debug.Log("[GolferVerify] SNAP " + label + " -> " + path + " exists=" + ok +
                      (ok ? " bytes=" + new FileInfo(path).Length : ""));
        }

        static bool SeedAndLoad(int hole)
        {
            try
            {
                var gsType = FindType("Golfin.Gameplay.Session.GameSession");
                if (gsType == null) return false;
                gsType.GetProperty("IsVersus", BindingFlags.Public | BindingFlags.Static)?.SetValue(null, false);
                string charId = "";
                var cmType = FindType("Golfin.Roster.CharacterManager") ?? FindType("CharacterManager");
                if (cmType != null)
                {
                    var inst = cmType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                    if (inst != null) charId = (string)(cmType.GetMethod("GetSelectedCharacterId")?.Invoke(inst, null) ?? "");
                }
                gsType.GetMethod("SeedSession", new[] { typeof(int), typeof(string), typeof(int) })
                      ?.Invoke(null, new object[] { hole, charId, 0 });
                var loaderType = FindType("Golfin.UI.GameplayTransition.GameplaySceneLoader");
                var loaderInst = loaderType?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                if (loaderInst == null) return false;
                var begin = loaderType.GetMethods().FirstOrDefault(m => m.Name == "BeginGameplayLoad");
                if (begin == null) return false;
                var pars = begin.GetParameters();
                begin.Invoke(loaderInst, pars.Length == 1 ? new object[] { hole } : new object[] { hole, null });
                return true;
            }
            catch (Exception e) { Debug.LogWarning("[GolferVerify] seed/load failed: " + e.Message); return false; }
        }

        static IEnumerator WaitForScene(string name, float timeout)
        {
            float t = 0f;
            while (t < timeout)
            {
                for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                {
                    var s = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                    if (s.name == name && s.isLoaded) yield break;
                }
                yield return new WaitForSecondsRealtime(0.5f);
                t += 0.5f;
            }
            Debug.LogWarning("[GolferVerify] timed out waiting for scene " + name);
        }
    }
}
#endif
