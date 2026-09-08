// ─────────────────────────────────────────────────────────────────────────────
// game_polish_b §D2 / A2 — the frame-by-frame gate for the three retrofits.
//
// THE PROBLEM THIS SOLVES. §D2 swaps three hand-rolled tween loops for UiMotion
// primitives and promises the result is FRAME-IDENTICAL. "Looks the same" is not
// a measurement of a twelve-frame event, and the gacha reveal in particular is,
// in the MAP's own words, the most-loved motion in the game — so the gate is a
// per-frame value log of the OLD routine against a per-frame value log of the
// NEW one, compared numerically.
//
// WHY IT NEEDS PLAY MODE, AND A FIXED CLOCK. Every one of these routines
// integrates Time.unscaledDeltaTime. In EDIT mode that reads ~1.1 s (measured on
// this machine, 2026-09-08), so a 0.20 s tween completes in ONE step and there
// are no frames to compare. Play mode with Time.captureDeltaTime = 1/60 makes
// unscaledDeltaTime exactly 1/60 every frame, which is what makes two runs taken
// minutes apart — one before the retrofit, one after — line up frame for frame
// instead of merely curve for curve.
//
// WHY IT DRIVES THE ROUTINES IN ISOLATION rather than through the real modals.
// The quantity under test is the CURVE, and a curve is the same curve wherever it
// is mounted. Driving the real VersusResultModal here would mean standing up a
// finished 1v1 match to sample a scale, and the OLD code's pop is reachable only
// from ShowResult() while the NEW one is reachable only from ModalController.Show()
// — two different call paths, which is exactly the confound a parity gate must
// not have. That the modal actually RUNS the new curve is a separate claim, and
// it is A1's: the modal table plus a mid-pop frame per modal.
//
// The old routines are reached by REFLECTION on the real components, not
// transcribed. A transcription is a claim about the old code; a reflected call is
// the old code. On the OLD run the reflected members exist; on the NEW run they
// are gone, and their absence is recorded as `source: "uimotion"` rather than
// silently skipped.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.UI.Polish.EditorTools
{
    /// <summary>
    /// Records one JSON of per-frame values for each of the three §D2 motions. Run it once at
    /// HEAD (writes <c>…_old.json</c>) and once after the swap (writes <c>…_new.json</c>);
    /// <c>Docs/Scripts/compare_retrofit.py</c> diffs the pair.
    /// </summary>
    public static class RetrofitParityRecorder
    {
        const string ArmedKey = "game_polish_b.retrofit.armed";
        const string SideKey  = "game_polish_b.retrofit.side";
        const string OutDir   = "Docs/Diagnostics/_capture";

        /// <summary>The clock every sample is taken on. 1/60 exactly — see the header.</summary>
        public const float Dt = 1f / 60f;

        [MenuItem("GOLFIN/Game Polish/Retrofit parity — record OLD (pre-change)", priority = 270)]
        public static void ArmOld() => Arm("old");

        [MenuItem("GOLFIN/Game Polish/Retrofit parity — record NEW (post-change)", priority = 271)]
        public static void ArmNew() => Arm("new");

        static void Arm(string side)
        {
            EditorPrefs.SetBool(ArmedKey, true);
            EditorPrefs.SetString(SideKey, side);
            Debug.Log($"[RetrofitParity] armed ({side}) — entering play mode.");
            EditorApplication.delayCall += WhenIdle;
        }

        static void WhenIdle()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += WhenIdle;
                return;
            }
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        static void Hook() => EditorApplication.playModeStateChanged += s =>
        {
            if (s != PlayModeStateChange.EnteredPlayMode) return;
            if (!EditorPrefs.GetBool(ArmedKey, false)) return;
            EditorPrefs.SetBool(ArmedKey, false);
            var go = new GameObject("__RetrofitParityRecorder");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<Driver>();
        };

        // ═════════════════════════════════════════════════════════════════════

        /// <summary>One motion's per-frame trace.</summary>
        public sealed class Trace
        {
            public string Name = "";
            /// <summary>"legacy" — the routine reflected off the shipping component; "uimotion" —
            /// the primitive. Written into the JSON so a comparison can never silently line up two
            /// runs of the same side.</summary>
            public string Source = "";
            /// <summary>The quantity sampled, e.g. "localScale.x", "anchoredPosition.x", "glow.alpha".</summary>
            public string Quantity = "";
            public readonly List<float> Values = new List<float>();
        }

        public sealed class Driver : MonoBehaviour
        {
            readonly List<Trace> _traces = new List<Trace>();
            string _side = "old";

            void Start()
            {
                Application.runInBackground = true;
                _side = EditorPrefs.GetString(SideKey, "old");
                Time.captureDeltaTime = Dt;
                StartCoroutine(Run());
            }

            IEnumerator Run()
            {
                Debug.Log($"[RetrofitParity] side={_side} dt={Dt:F6} (captureDeltaTime)");
                // One frame so captureDeltaTime is what unscaledDeltaTime reports before the
                // first sample — the setter takes effect on the NEXT frame boundary.
                yield return null;
                Debug.Log($"[RetrofitParity] unscaledDeltaTime now {Time.unscaledDeltaTime:F6}");

                yield return Versus();
                yield return PillSlide();
                yield return PillGlow();
                yield return GachaEnter();
                yield return GachaPop();
                yield return GachaShake();

                Write();
                Time.captureDeltaTime = 0f;
                Debug.Log("[RetrofitParity] done — leaving play mode.");
                EditorApplication.isPlaying = false;
            }

            // ── sampling helper ──────────────────────────────────────────────
            //
            // Steps `routine` one MoveNext per rendered frame — exactly what StartCoroutine does
            // for a `yield return null` routine — and samples AFTER each step, so sample k is the
            // value the player would see on frame k.
            IEnumerator Sample(Trace t, IEnumerator routine, Func<float> read, int cap = 600)
            {
                _traces.Add(t);
                int n = 0;
                while (routine.MoveNext())
                {
                    t.Values.Add(read());
                    if (++n > cap) { Debug.LogError($"[RetrofitParity] {t.Name} did not terminate"); break; }
                    yield return null;
                }
                t.Values.Add(read());   // the settle
                Debug.Log($"[RetrofitParity] {t.Name} ({t.Source}) frames={t.Values.Count} " +
                          $"first={t.Values[0]:F5} last={t.Values[t.Values.Count - 1]:F5}");
            }

            static GameObject Temp(string name, out RectTransform rt)
            {
                var go = new GameObject(name, typeof(RectTransform));
                rt = go.GetComponent<RectTransform>();
                return go;
            }

            /// <summary>The old routine, if this build still has it. Null on the NEW side.</summary>
            static IEnumerator? Legacy(Type t, object instance, string method, params object[] args)
            {
                MethodInfo? m = t.GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
                return m == null ? null : (IEnumerator?)m.Invoke(instance, args);
            }

            // ── 1 · Versus pop-in — scale 0.9 → 1.0 over 0.20 s ──────────────
            IEnumerator Versus()
            {
                var t = new Trace { Name = "versus.popin", Quantity = "localScale.x" };
                GameObject panel = Temp("VersusPanelStandIn", out RectTransform rect);

                var hostGo = new GameObject("VersusStandIn");
                var host   = hostGo.AddComponent<Golfin.UI.Matchmaking.VersusResultModalController>();
                host.modalPanel = panel;

                IEnumerator? legacy = Legacy(host.GetType(), host, "PopInScaleRoutine");
                if (legacy != null)
                {
                    t.Source = "legacy:VersusResultModalController.PopInScaleRoutine";
                    yield return Sample(t, legacy, () => panel.transform.localScale.x);
                }
                else
                {
                    t.Source = "uimotion:Pop(rect,null,PopDur)";
                    yield return Sample(t, UiMotion.Pop(rect, null), () => rect.localScale.x);
                }
                Destroy(hostGo); Destroy(panel);
            }

            // ── 2 · Daily-mission pill slide — enter, ease-out, 0.45 s ───────
            IEnumerator PillSlide()
            {
                var t = new Trace { Name = "pill.slide.enter", Quantity = "anchoredPosition.x" };
                GameObject pill = Temp("PillStandIn", out RectTransform rect);
                rect.sizeDelta = new Vector2(549f, 76f);

                var hostGo = new GameObject("PillStandIn.Host");
                var host   = hostGo.AddComponent<Golfin.UI.Home.DailyMissionPillController>();
                Type ht = host.GetType();
                ht.GetField("pillRect", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(host, rect);

                float restX = (float)ht.GetField("restX", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(host)!;
                float dur   = (float)ht.GetField("enterDuration", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(host)!;
                float from  = -(rect.rect.width) - restX;

                IEnumerator? legacy = Legacy(ht, host, "SlideRoutine", from, restX, dur, true,
                                             Enum.ToObject(ht.GetNestedType("PillState", BindingFlags.NonPublic)!, 2));
                if (legacy != null)
                {
                    t.Source = "legacy:DailyMissionPillController.SlideRoutine";
                    yield return Sample(t, legacy, () => rect.anchoredPosition.x);
                }
                else
                {
                    t.Source = "uimotion:Slide(rect,from,restX,enterDuration,easeOut:true)";
                    yield return Sample(t, UiMotion.Slide(rect, from, restX, dur, true), () => rect.anchoredPosition.x);
                }
                Destroy(hostGo); Destroy(pill);
            }

            // ── 3 · Pill glow — the Update() sine, one full period ───────────
            IEnumerator PillGlow()
            {
                var t = new Trace { Name = "pill.glow", Quantity = "glow.alpha" };
                var go = new GameObject("GlowStandIn", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                var img = go.GetComponent<Image>();

                var hostGo = new GameObject("GlowStandIn.Host");
                var host   = hostGo.AddComponent<Golfin.UI.Home.DailyMissionPillController>();
                Type ht = host.GetType();
                FieldInfo? glowF = ht.GetField("glowImage", BindingFlags.Instance | BindingFlags.NonPublic);
                FieldInfo? stateF = ht.GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic);
                MethodInfo? update = ht.GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
                float period = (float)ht.GetField("glowPeriod", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(host)!;
                float min    = (float)ht.GetField("glowMin",    BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(host)!;
                float max    = (float)ht.GetField("glowMax",    BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(host)!;
                int   frames = Mathf.RoundToInt(period / Dt);

                bool legacyGlow = glowF != null && stateF != null && update != null
                                  && ht.GetField("_glowPhase", BindingFlags.Instance | BindingFlags.NonPublic) != null;
                if (legacyGlow)
                {
                    glowF!.SetValue(host, img);
                    stateF!.SetValue(host, Enum.ToObject(ht.GetNestedType("PillState", BindingFlags.NonPublic)!, 2));
                    t.Source = "legacy:DailyMissionPillController.Update() sine";
                    _traces.Add(t);
                    for (int i = 0; i < frames; i++)
                    {
                        update!.Invoke(host, null);
                        t.Values.Add(img.color.a);
                        yield return null;
                    }
                    Debug.Log($"[RetrofitParity] {t.Name} ({t.Source}) frames={t.Values.Count}");
                }
                else
                {
                    var cg = go.AddComponent<CanvasGroup>();
                    t.Source = "uimotion:Pulse(glowGroup,glowMin,glowMax,1,glowPeriod)";
                    yield return Sample(t, UiMotion.Pulse(cg, min, max, 1, period), () => cg.alpha, frames + 10);
                }
                Destroy(hostGo); Destroy(go);
            }

            // ── 4 · Gacha bag enter — scale 0.6 → 1 ease-out-back, 0.35 s ────
            IEnumerator GachaEnter()
            {
                var t = new Trace { Name = "gacha.enter", Quantity = "bagScale.x" };
                var hostGo = new GameObject("GachaStandIn");
                var host   = hostGo.AddComponent<GolfinRedux.UI.Gacha.GachaRevealModalController>();
                Type ht = host.GetType();

                GameObject bag = Temp("BagStandIn", out RectTransform bagRect);
                ht.GetField("_bag", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(host, bagRect);
                float dur = (float)ht.GetField("_enterDuration", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(host)!;

                IEnumerator? legacy = Legacy(ht, host, "StepEnter");
                if (legacy != null)
                {
                    t.Source = "legacy:GachaRevealModalController.StepEnter";
                    yield return Sample(t, legacy, () => bagRect.localScale.x);
                }
                else
                {
                    t.Source = "uimotion:Tween(0.6,1,_enterDuration,SetBagScale,Ease.OutBack)";
                    yield return Sample(t, UiMotion.Tween(0.6f, 1f, dur,
                                        s => bagRect.localScale = new Vector3(s, s, 1f), Ease.OutBack),
                                        () => bagRect.localScale.x);
                }
                Destroy(bag); Destroy(hostGo);
            }

            // ── 5 · Gacha card pop — the reference curve, sampled directly ───
            //
            // StepPop moves four quantities off one clock and needs a spawned card, a bag mouth
            // and a particle burst to run at all. What the retrofit must not change is the CURVE,
            // so that is what is sampled: ease-out-back over _popDuration, the exact expression
            // each side computes, read through whichever implementation this build has.
            IEnumerator GachaPop()
            {
                var t = new Trace { Name = "gacha.pop.curve", Quantity = "easeOutBack(k)" };
                var hostGo = new GameObject("GachaCurveStandIn");
                var host   = hostGo.AddComponent<GolfinRedux.UI.Gacha.GachaRevealModalController>();
                Type ht = host.GetType();
                float dur = (float)ht.GetField("_popDuration", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(host)!;

                MethodInfo? legacyEase = ht.GetMethod("EaseOutBack", BindingFlags.Static | BindingFlags.NonPublic);
                Func<float, float> ease;
                if (legacyEase != null) { t.Source = "legacy:GachaRevealModalController.EaseOutBack"; ease = k => (float)legacyEase.Invoke(null, new object[] { k })!; }
                else                    { t.Source = "uimotion:Curve(Ease.OutBack)";                  ease = k => UiMotion.Curve(Ease.OutBack, k); }

                _traces.Add(t);
                float el = 0f;
                while (el < dur)
                {
                    el += Time.unscaledDeltaTime;
                    t.Values.Add(ease(Mathf.Clamp01(el / dur)));
                    yield return null;
                }
                t.Values.Add(ease(1f));
                Debug.Log($"[RetrofitParity] {t.Name} ({t.Source}) frames={t.Values.Count}");
                Destroy(hostGo);
            }

            // ── 6 · Gacha bag shake — rotation, ramping amplitude + frequency ─
            IEnumerator GachaShake()
            {
                var t = new Trace { Name = "gacha.shake", Quantity = "bagPivot.localEulerAngles.z" };
                var hostGo = new GameObject("GachaShakeStandIn");
                var host   = hostGo.AddComponent<GolfinRedux.UI.Gacha.GachaRevealModalController>();
                Type ht = host.GetType();

                GameObject pivot = Temp("PivotStandIn", out RectTransform pivotRect);
                ht.GetField("_bagPivot", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(host, pivotRect);

                const float duration = 0.6f;
                Func<float> read = () =>
                {
                    float z = pivotRect.localEulerAngles.z;
                    return z > 180f ? z - 360f : z;      // signed, so the sine reads as a sine
                };

                // RarityFxTier is a top-level class in GolfinRedux.UI.Gacha, not a nested type.
                object tier = Activator.CreateInstance(typeof(GolfinRedux.UI.Gacha.RarityFxTier))!;

                IEnumerator? legacy = Legacy(ht, host, "StepShake", tier, duration, Color.white);
                if (legacy != null)
                {
                    t.Source = "legacy:GachaRevealModalController.StepShake";
                    yield return Sample(t, legacy, read);
                }
                else
                {
                    t.Source = "uimotion-driven:GachaRevealModalController.StepShake";
                    MethodInfo m = ht.GetMethod("StepShakeFor", BindingFlags.Instance | BindingFlags.NonPublic)
                                   ?? ht.GetMethod("StepShake", BindingFlags.Instance | BindingFlags.NonPublic)!;
                    yield return Sample(t, (IEnumerator)m.Invoke(host, new object[] { tier, duration, Color.white })!, read);
                }
                Destroy(pivot); Destroy(hostGo);
            }

            // ── output ───────────────────────────────────────────────────────
            void Write()
            {
                var sb = new StringBuilder();
                sb.Append("{\n");
                sb.Append("  \"task\": \"game_polish_b\",\n");
                sb.Append("  \"gate\": \"D2 retrofit frame-by-frame parity\",\n");
                sb.Append($"  \"side\": \"{_side}\",\n");
                sb.Append($"  \"recordedUtc\": \"{DateTime.UtcNow:u}\",\n");
                sb.Append($"  \"headSha\": \"{Head()}\",\n");
                sb.Append($"  \"dt\": {Dt.ToString("R", CultureInfo.InvariantCulture)},\n");
                sb.Append("  \"traces\": [\n");
                for (int i = 0; i < _traces.Count; i++)
                {
                    Trace t = _traces[i];
                    sb.Append("    { \"name\": \"").Append(t.Name).Append("\", \"source\": \"").Append(t.Source)
                      .Append("\", \"quantity\": \"").Append(t.Quantity).Append("\", \"frames\": ").Append(t.Values.Count)
                      .Append(", \"values\": [");
                    for (int k = 0; k < t.Values.Count; k++)
                    {
                        if (k > 0) sb.Append(", ");
                        sb.Append(t.Values[k].ToString("F6", CultureInfo.InvariantCulture));
                    }
                    sb.Append("] }").Append(i < _traces.Count - 1 ? ",\n" : "\n");
                }
                sb.Append("  ]\n}\n");

                Directory.CreateDirectory(OutDir);
                string path = Path.Combine(OutDir, $"game_polish_b_retrofit_{_side}.json");
                File.WriteAllText(path, sb.ToString());
                Debug.Log($"[RetrofitParity] wrote {path} ({_traces.Count} traces)");
            }

            static string Head()
            {
                try
                {
                    var p = new System.Diagnostics.Process
                    {
                        StartInfo = new System.Diagnostics.ProcessStartInfo("git", "rev-parse HEAD")
                        {
                            WorkingDirectory = Directory.GetCurrentDirectory(),
                            RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true,
                        }
                    };
                    p.Start(); string s = p.StandardOutput.ReadToEnd().Trim(); p.WaitForExit();
                    return s;
                }
                catch { return "unknown"; }
            }
        }
    }
}
