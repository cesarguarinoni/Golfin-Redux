#if UNITY_EDITOR
// ─────────────────────────────────────────────────────────────────────────────
// asset_loans_polish — the invariant probe (SPEC § Acceptance; PIPELINE_HARDENING rule 3).
//
// THE GATE IS THIS JSON, NOT A LOOK AT A FRAME. Every claim in the acceptance
// list is a number sampled off the LIVE object frame by frame — a rise is a y
// trace, a bump is a scale trace, a stagger is a set of start frames, "the tick
// does not re-trigger" is a flat trace. Motion is exactly the class of claim an
// eye gets wrong (memory: feedback_never_eyeball_brightness), and a still cannot
// carry it at all.
//
// REAL NAVIGATION, STUBBED SOCKET — the same posture as LoanUiCaptureBot, whose
// stub classes this reuses: boot → the real bottom-nav button's onClick → the
// real detail panel → the real LEND button's onClick → the real modal → a real
// recipient row's own Button.onClick. Nothing under test is called directly.
//
// GOLFIN ▸ Loans ▸ Probe Loan Polish
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Golfin.Net;
using Golfin.Social;
using Golfin.UI.Loans;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.UI.Loans.EditorTools
{
    public static class LoanPolishProbe
    {
        public const string OutPath =
            "Docs/Specs/Active/asset_loans_polish/asset_loans_polish_invariants.json";
        public const string ShotDir = "Docs/Specs/Active/asset_loans_polish/screenshots";

        private const string ArmedKey = "LoanPolishProbe.Armed";

        [MenuItem("GOLFIN/Loans/Probe Loan Polish")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) { Spawn(); return; }
            Application.runInBackground = true;
            SessionState.SetBool(ArmedKey, true);
            EditorApplication.EnterPlaymode();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void SpawnIfArmed()
        {
            if (!SessionState.GetBool(ArmedKey, false)) return;
            SessionState.SetBool(ArmedKey, false);
            Application.runInBackground = true;
            Spawn();
        }

        private static void Spawn()
        {
            var host = new GameObject("[LoanPolishProbe]");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<LoanPolishRunner>().Begin();
        }
    }

    /// <summary>
    /// The stub, with a DELAY the pending test needs. <c>LoanStubTransport</c> answers in one
    /// frame, which is a wait no affordance could be photographed inside; this one holds the POST
    /// open for a configurable stretch, which is the whole point of the affordance being there.
    /// </summary>
    public sealed class LoanPolishTransport : IHttpTransport
    {
        public string LoansJson     = "{\"out\":[],\"in\":[]}";
        public string FollowingJson = "[]";
        public float  PostDelay     = 1.5f;
        public string PostJson      = "{\"status\":\"not_following\",\"loan\":null}";
        public readonly List<string> Seen = new List<string>();

        public IEnumerator Send(HttpRequest request, Action<HttpResponse> onResponse)
        {
            string url = request.Url ?? "";
            Seen.Add($"{request.Method} {url}");

            bool isPost = string.Equals(request.Method, "POST", StringComparison.OrdinalIgnoreCase);
            if (isPost)
            {
                float until = Time.realtimeSinceStartup + PostDelay;
                while (Time.realtimeSinceStartup < until) yield return null;
                onResponse(HttpResponse.Status(200, "{\"data\":" + PostJson + "}"));
                yield break;
            }

            yield return null;
            if (url.Contains("/following"))   onResponse(HttpResponse.Status(200, "{\"data\":" + FollowingJson + "}"));
            else if (url.Contains("/loans"))  onResponse(HttpResponse.Status(200, "{\"data\":" + LoansJson + "}"));
            else                              onResponse(HttpResponse.Status(404, "{\"detail\":\"stub: " + url + "\"}"));
        }
    }

    public sealed class LoanPolishRunner : MonoBehaviour, ICoroutineRunner
    {
        private LoanPolishTransport _t = null!;
        private readonly List<string> _lines = new List<string>();
        private readonly List<string> _shots = new List<string>();
        private int _fail;

        public void Begin() => StartCoroutine(Sequence());
        void ICoroutineRunner.Run(IEnumerator routine) => StartCoroutine(routine);

        // ── the probe clock ──────────────────────────────────────────────────

        /// <summary>
        /// A best-effort sampling interval. THE ASSERTIONS DO NOT DEPEND ON IT.
        ///
        /// <para>`Time.captureDeltaTime` is set around each motion sampler because on hosts where it
        /// does pin `Time.unscaledDeltaTime` — which is what every `UiMotion` routine integrates —
        /// it buys more points per tween. On THIS host it does not: with the lock set to 1/120 the
        /// ribbon's first alpha sample was 0.347, and EaseOut(dt / 0.25) = 0.347 solves to
        /// dt = 0.033 s, the editor's own frame time. So it is a resolution nicety, never a
        /// guarantee, and `PRE_clock` below measures and records what actually happened.</para>
        ///
        /// <para>AND THE VALUE THRESHOLDS ARE "NOT AT REST", NOT "WELL AWAY FROM REST". A 0.99
        /// alpha floor reads as generous and is not: one 125 ms step of a 0.15 s Fade lands at
        /// EaseOut(0.833) = 0.995, so a dim that IS fading measures as a dim that snapped. The
        /// floors are 0.999 — far enough from 1.000 to be a real distinction in a float that a
        /// routine wrote, close enough that no host can step past it.</para>
        ///
        /// <para>WHICH IS WHY EVERY MOTION ASSERTION HERE IS VALUE-BASED, NOT SAMPLE-COUNT-BASED.
        /// The self-reviewer's editor ran at ~125 ms/frame and saw the whole 0.25 s Rise in two
        /// samples; the motion was demonstrably playing, and A1/A1b/A2 failed anyway because they
        /// asked for five distinct values. A gate that answers differently on two hosts is not a
        /// gate. What IS host-independent is the FIRST sample, taken in the same frame as the call
        /// that arms the tween: `UiMotion.Run` drives the routine's first segment synchronously, so
        /// by the time the arming call returns the rect is already off its rest position and the
        /// CanvasGroup already off its rest alpha. No frame has to elapse for that to be true, so no
        /// host can miss it — and nothing but a real tween can produce it.</para>
        ///
        /// <para>⚠️ INSIDE A LOCK, COUNT FRAMES — NEVER <c>WaitForSecondsRealtime</c>. Wall time is
        /// unaffected, so a "0.4 s settle" can advance far less tween than it reads. And the lock is
        /// released before <see cref="TraceModal"/>'s pending section, whose stubbed 1.5 s
        /// round-trip really is measured against the wall.</para>
        /// </summary>
        private const float ProbeDt = 1f / 120f;

        private static void LockClock()   => Time.captureDeltaTime = ProbeDt;
        private static void UnlockClock() => Time.captureDeltaTime = 0f;

        /// <summary>Advance <paramref name="frames"/> rendered frames — the in-lock settle.</summary>
        private static IEnumerator Frames(int frames)
        {
            for (int i = 0; i < frames; i++) yield return null;
        }

        // ── assertion bookkeeping ────────────────────────────────────────────

        private void Assert(string id, bool pass, string detail)
        {
            if (!pass) _fail++;
            _lines.Add("    {\"id\": " + Q(id) + ", \"pass\": " + (pass ? "true" : "false") +
                       ", \"detail\": " + Q(detail) + "}");
            Debug.Log($"[LoanPolishProbe] {(pass ? "PASS" : "FAIL")} {id} — {detail}");
        }

        private static string Q(string s)
            => "\"" + (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"")
                                .Replace("\n", " ").Replace("\r", "") + "\"";

        private static string F(float v) => v.ToString("0.000", CultureInfo.InvariantCulture);
        private static string F(IEnumerable<float> v) => string.Join(" ", v.Select(F));

        /// <summary>A tween never goes backwards. Sampled values may repeat (two samples inside
        /// one rendered frame), so non-decreasing rather than strictly increasing.</summary>
        private static bool NonDecreasing(IList<float> v)
        {
            for (int i = 1; i < v.Count; i++)
                if (v[i] >= 0f && v[i - 1] >= 0f && v[i] < v[i - 1] - 0.0001f) return false;
            return true;
        }

        /// <summary>A staggered group is never "later row further along than an earlier one".</summary>
        private static bool NonIncreasing(IList<float> v)
        {
            for (int i = 1; i < v.Count; i++)
                if (v[i] > v[i - 1] + 0.0001f) return false;
            return true;
        }

        private static string PathOf(Transform t)
        {
            string p = t.name;
            while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
            return p;
        }

        // ── reflection helpers (editor-only probe: exact beats guessing) ─────

        private static T? Priv<T>(object host, string field) where T : class
        {
            FieldInfo? f = host.GetType().GetField(field,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            return f?.GetValue(host) as T;
        }

        private static T? FindActive<T>() where T : Component
            => Resources.FindObjectsOfTypeAll<T>()
                .FirstOrDefault(c => c != null && !string.IsNullOrEmpty(c.gameObject.scene.name)
                                  && c.gameObject.activeInHierarchy);

        private static T? FindAny<T>() where T : Component
            => Resources.FindObjectsOfTypeAll<T>()
                .FirstOrDefault(c => c != null && !string.IsNullOrEmpty(c.gameObject.scene.name));

        // ── canned payloads (same shapes as LoanUiCaptureBot) ────────────────

        private static string Iso(TimeSpan d)
            => DateTime.UtcNow.Add(d).ToString("yyyy-MM-ddTHH:mm:ss.ffffffzzz");

        private static string Party(string id, string name, int lv)
            => $"{{\"id\":\"{id}\",\"display_name\":\"{name}\",\"avatar_url\":null,\"avatar_level\":{lv}}}";

        private static string Loan(string id, string kind, string refId, bool asLender,
                                   int level, TimeSpan left, int days)
        {
            const string me = "11111111-1111-1111-1111-111111111111";
            const string them = "22222222-2222-2222-2222-222222222222";
            string lender   = asLender ? Party(me, "YOU", 12)     : Party(them, "KENJI", 34);
            string borrower = asLender ? Party(them, "MARTA", 21) : Party(me, "YOU", 12);
            return "{" +
                $"\"id\":\"{id}\",\"kind\":\"{kind}\",\"ref_id\":\"{refId}\"," +
                $"\"lender\":{lender},\"borrower\":{borrower}," +
                $"\"days\":{days},\"starts_at\":\"{Iso(TimeSpan.FromDays(-1))}\"," +
                $"\"ends_at\":\"{Iso(left)}\",\"ended_at\":null,\"status\":\"active\"," +
                $"\"level\":{level},\"level_at_start\":{level},\"level_at_end\":null," +
                "\"lender_share_bp\":2000,\"rp_to_lender\":0,\"rp_to_borrower\":0}";
        }

        private static string Following() =>
            "[" +
            "{\"following_id\":\"22222222-2222-2222-2222-222222222222\",\"created_at\":null,\"profiles\":" + Party("22222222-2222-2222-2222-222222222222","MARTA",21) + "}," +
            "{\"following_id\":\"33333333-3333-3333-3333-333333333333\",\"created_at\":null,\"profiles\":" + Party("33333333-3333-3333-3333-333333333333","LUCAS",8) + "}," +
            "{\"following_id\":\"44444444-4444-4444-4444-444444444444\",\"created_at\":null,\"profiles\":" + Party("44444444-4444-4444-4444-444444444444","AIKO",47) + "}" +
            "]";

        private void SetLoans(string outL, string inL)
            => _t.LoansJson = "{\"out\":[" + outL + "],\"in\":[" + inL + "]}";

        private IEnumerator PushLoans()
        {
            ApiResult<LoanListDto>? r = null;
            IEnumerator call = LoanService.Instance.Refresh(x => r = x);
            while (call.MoveNext()) yield return call.Current;
            yield return null;
        }

        // ═════════════════════════════════════════════════════════════════════

        private IEnumerator Sequence()
        {
            Application.runInBackground = true;

            yield return new WaitForSecondsRealtime(5f);
            yield return WaitForHomeToSettle();

            _t = new LoanPolishTransport { FollowingJson = Following() };
            var service = new LoanService(new ApiClient(_t, new LoanStubAuth(), this));
            LoanService.ConfigureForTest(service);
            UserService.Instance.SetDetailForTest(
                new UserDetailDto { Id = "11111111-1111-1111-1111-111111111111" });

            var sync = UnityEngine.Object.FindFirstObjectByType<Golfin.EconomyRuntime.LoanSyncBehaviour>();
            if (sync != null) service.Reconciler = sync;

            var puim = FindActive<Golfin.UI.PersistentUIManager>();
            yield return GoTo(puim != null ? puim.charactersButton : null,
                              GolfinRedux.UI.ScreenId.Roster);

            var carousel = FindActive<Golfin.Roster.CarouselController>();
            string selectedId = Golfin.Roster.CharacterManager.Instance != null
                ? Golfin.Roster.CharacterManager.Instance.GetSelectedCharacterId() : "";
            var owned = Golfin.Roster.CharacterManager.Instance?.GetAllOwnedCharacters();
            string lendableId = owned != null
                ? owned.Select(c => c.characterId).FirstOrDefault(id => id != selectedId) ?? selectedId
                : selectedId;

            SetLoans("", "");
            yield return PushLoans();
            if (carousel != null) carousel.SelectCharacter(lendableId);
            yield return new WaitForSecondsRealtime(1f);

            var ribbon = FindAny<LoanRibbonView>();
            if (ribbon == null) { Assert("PRE_ribbon_found", false, "no LoanRibbonView in the scene"); yield return Finish(); yield break; }

            GameObject? ribbonRoot = Priv<GameObject>(ribbon, "ribbonRoot");
            GameObject? lentDim    = Priv<GameObject>(ribbon, "lentDim");
            var ribbonRect = ribbonRoot != null ? ribbonRoot.transform as RectTransform : null;
            if (ribbonRect == null || lentDim == null)
            { Assert("PRE_ribbon_wired", false, "ribbonRoot/lentDim unwired"); yield return Finish(); yield break; }

            yield return MeasureClock();

            Assert("A0_canvasgroups_authored",
                   Priv<CanvasGroup>(ribbon, "ribbonGroup") != null && Priv<CanvasGroup>(ribbon, "dimGroup") != null,
                   "ribbonGroup + dimGroup serialized references are non-null on the live view");

            float restY = ribbonRect.anchoredPosition.y;

            // ── A1/A2 — the entrance ─────────────────────────────────────────
            yield return TraceEntrance(ribbon, ribbonRect, lentDim, restY, carousel, lendableId);

            // ── A3 — the tick must not re-trigger ────────────────────────────
            yield return TraceTick(ribbonRect, ribbonRoot!, lentDim, restY);

            // ── A13 — the ROSTER ribbon is flush too (red-team gap, 2026-09-09) ──
            yield return TraceRosterRibbonFlush(ribbonRect, lentDim);

            yield return Snap("polish_ribbon_lender_state");

            // ── A10 — leave mid-motion, come back at rest ────────────────────
            yield return TraceLeaveMidMotion(ribbon, ribbonRect, lentDim, restY, puim, carousel, lendableId);

            // ── A9 — the card badge ──────────────────────────────────────────
            yield return TraceBadge(lendableId);

            // ── A4 — Clear fades out, then deactivates ───────────────────────
            yield return TraceClear(ribbonRect, ribbonRoot!, lentDim, carousel, lendableId);

            // ── A5/A6/A7/A8/A11 — the modal ──────────────────────────────────
            yield return TraceModal(carousel, lendableId);

            // ── A14 — the RETURN modal's pending state (red-team gap) ────────
            yield return TraceReturnPending(carousel, lendableId);

            // ── A12 — the CLUB panel's ribbon alignment (Cesar, 2026-09-09) ───
            yield return TraceClubRibbon(puim);

            yield return Finish();
        }

        /// <summary>Record what the clock ACTUALLY does on this host, so every trace below can be
        /// read rather than guessed at. Informational — it never fails the run.</summary>
        private IEnumerator MeasureClock()
        {
            var free = new List<float>();
            for (int f = 0; f < 12; f++) { free.Add(Time.unscaledDeltaTime); yield return null; }

            LockClock();
            var locked = new List<float>();
            for (int f = 0; f < 12; f++) { locked.Add(Time.unscaledDeltaTime); yield return null; }
            UnlockClock();

            float freeMed = Median(free), lockMed = Median(locked);
            bool lockWorks = Mathf.Abs(lockMed - ProbeDt) < ProbeDt * 0.25f;
            Assert("PRE_clock", true,
                   $"effective unscaledDeltaTime: unlocked median {F(freeMed)}s, " +
                   $"captureDeltaTime={F(ProbeDt)} median {F(lockMed)}s -> the lock " +
                   $"{(lockWorks ? "DOES" : "does NOT")} pin unscaledDeltaTime on this host. " +
                   "Every assertion below is value-based and does not depend on either number.");
        }

        private static float Median(List<float> v)
        {
            if (v == null || v.Count == 0) return -1f;
            var c = new List<float>(v); c.Sort();
            return c[c.Count / 2];
        }

        // ═════════════════════════════════════════════════════════════════════
        // A1 / A2
        // ═════════════════════════════════════════════════════════════════════

        private IEnumerator TraceEntrance(LoanRibbonView ribbon, RectTransform rect, GameObject dim,
                                          float restY, Golfin.Roster.CarouselController? carousel,
                                          string id)
        {
            var ys = new List<float>(); var ra = new List<float>(); var da = new List<float>();

            LockClock();
            SetLoans(Loan("aaaaaaaa-0000-4000-8000-000000000001", "character", id,
                          asLender: true, level: 82, left: TimeSpan.FromHours(52), days: 3), "");
            yield return PushLoans();
            if (carousel != null) carousel.SelectCharacter(id);

            CanvasGroup? rg = null, dg = null;
            for (int f = 0; f < 60; f++)
            {
                if (rg == null) rg = rect.GetComponent<CanvasGroup>();
                if (dg == null) dg = dim.GetComponent<CanvasGroup>();
                ys.Add(rect.anchoredPosition.y);
                ra.Add(rg != null ? rg.alpha : -1f);
                da.Add(dg != null ? dg.alpha : -1f);
                yield return null;
            }
            yield return Frames(30);              // 0.25 s of tween — the Rise is EntryDur = 0.25 s
            UnlockClock();

            float maxY = ys.Max(), endY = rect.anchoredPosition.y;
            float minRa = ra.Where(v => v >= 0f).DefaultIfEmpty(-1f).Min();
            float endRa = rg != null ? rg.alpha : -1f;
            float minDa = da.Where(v => v >= 0f).DefaultIfEmpty(-1f).Min();
            float endDa = dg != null ? dg.alpha : -1f;

            // HOST-INDEPENDENT: ys[0] is read in the same frame as the arming call, so it is the
            // value RiseRoutine's synchronous first segment left behind. A ribbon that snapped into
            // place would have ys[0] == restY exactly and one distinct value; nothing but a real
            // Rise puts it ABOVE rest. The sample COUNT is reported, never gated on.
            Assert("A1_ribbon_rises_from_above",
                   ys.Count > 0 && ys[0] > restY + 0.5f && maxY > restY + 0.5f
                   && Mathf.Abs(endY - restY) < 0.01f && ys.Distinct().Count() >= 2,
                   $"restY={F(restY)} firstSample={F(ys[0])} (ABOVE rest = the Rise started off-rest) " +
                   $"maxY={F(maxY)} endY={F(endY)} distinctYs={ys.Distinct().Count()} trace=[{F(ys.Take(12))}]");

            // THE FIRST OBSERVABLE SAMPLE IS ALREADY ONE FRAME IN, and that is arithmetic, not
            // slack. `UiMotion.Run` drives the routine's first segment SYNCHRONOUSLY, so alpha 0
            // exists only inside the frame that started it; the first value any sampler can read
            // is EaseOut(dt/dur). At the editor's ~33 ms frame that is 0.35 for a 0.25 s Rise and
            // 0.53 for a 0.15 s Fade — which is what run 1 measured, to three decimals. So the
            // assertion is the SHAPE (rises monotonically to exactly 1 over many distinct values),
            // not a first-sample floor: run 5 hit a 47 ms frame and read 0.668 for the dim, and a
            // threshold that flaps with the editor's frame time is measuring the editor. The
            // frame-rate-independent proof that the entrance really STARTS off-rest is A1's y
            // trace (maxY > restY), which no partial sample can fake.
            Assert("A1b_ribbon_alpha_0_to_1",
                   minRa >= 0f && minRa < 0.999f && Mathf.Abs(endRa - 1f) < 0.01f
                   && ra.Where(v => v >= 0f).Distinct().Count() >= 2 && NonDecreasing(ra),
                   $"minAlpha={F(minRa)} endAlpha={F(endRa)} distinct={ra.Where(v => v >= 0f).Distinct().Count()} " +
                   $"monotonic={NonDecreasing(ra)} frameDt={F(Time.unscaledDeltaTime)}s trace=[{F(ra.Take(12))}]");

            Assert("A2_dim_fades_in_for_lender",
                   dim.activeSelf && minDa >= 0f && minDa < 0.999f && Mathf.Abs(endDa - 1f) < 0.01f
                   && da.Where(v => v >= 0f).Distinct().Count() >= 2 && NonDecreasing(da),
                   $"dimActive={dim.activeSelf} minAlpha={F(minDa)} endAlpha={F(endDa)} " +
                   $"distinct={da.Where(v => v >= 0f).Distinct().Count()} monotonic={NonDecreasing(da)} trace=[{F(da.Take(12))}]");
        }

        // ═════════════════════════════════════════════════════════════════════
        // A3
        // ═════════════════════════════════════════════════════════════════════

        private IEnumerator TraceTick(RectTransform rect, GameObject root, GameObject dim, float restY)
        {
            CanvasGroup? rg = rect.GetComponent<CanvasGroup>();
            CanvasGroup? dg = dim.GetComponent<CanvasGroup>();

            // A REAL repaint: the same loan pushed again runs the reconciler, which repaints the
            // panel, which calls Show() on an already-visible ribbon — the tick, exactly.
            yield return PushLoans();

            var ys = new List<float>(); var al = new List<float>();
            for (int f = 0; f < 20; f++)
            {
                ys.Add(rect.anchoredPosition.y);
                al.Add(rg != null ? rg.alpha : -1f);
                yield return null;
            }

            float ySpread = ys.Max() - ys.Min();
            float aSpread = al.Max() - al.Min();
            Assert("A3_tick_repaint_plays_nothing",
                   root.activeSelf && ySpread < 0.001f && aSpread < 0.001f
                   && Mathf.Abs(ys[0] - restY) < 0.01f && Mathf.Abs(al[0] - 1f) < 0.01f,
                   $"visible={root.activeSelf} ySpread={F(ySpread)} alphaSpread={F(aSpread)} y={F(ys[0])} alpha={F(al[0])}");
        }

        // ═════════════════════════════════════════════════════════════════════
        // A13 — the Roster ribbon, measured rather than eyeballed
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// The red-team passed this task noting that only the CLUB ribbon had a numeric flush
        /// assertion and the Roster one "reads flush by eye". The club ribbon was 27.05 px out and
        /// cleared an entire pipeline on exactly that reading, so the Roster panel gets the same
        /// number rather than the same eye (memory: feedback_never_eyeball_brightness /
        /// feedback_exact_not_close_enough).
        /// </summary>
        private IEnumerator TraceRosterRibbonFlush(RectTransform ribbonRect, GameObject dim)
        {
            var detail = FindActive<Golfin.Roster.CharacterDetailPanel>();
            if (detail == null)
            { Assert("A13_roster_ribbon_on_portrait", false, "no active CharacterDetailPanel"); yield break; }

            RectTransform? portrait = Deep(detail.transform, "Character");
            if (portrait == null)
            { Assert("A13_roster_ribbon_on_portrait", false, "the portrait ('Character') was not found"); yield break; }

            var cp = new Vector3[4]; portrait.GetWorldCorners(cp);
            var cr = new Vector3[4]; ribbonRect.GetWorldCorners(cr);
            var cd = new Vector3[4]; ((RectTransform)dim.transform).GetWorldCorners(cd);

            float ribL = cr[0].x - cp[0].x, ribR = cr[2].x - cp[2].x;
            float dimL = cd[0].x - cp[0].x, dimR = cd[2].x - cp[2].x;
            float ribTop = cr[1].y - cp[1].y;      // the ribbon sits ON the portrait's top edge

            Assert("A13_roster_ribbon_on_portrait",
                   Mathf.Abs(ribL) < 0.5f && Mathf.Abs(ribR) < 0.5f
                   && Mathf.Abs(dimL) < 0.5f && Mathf.Abs(dimR) < 0.5f
                   && Mathf.Abs(ribTop) < 0.5f,
                   $"ribbon leftD={F(ribL)} rightD={F(ribR)} topD={F(ribTop)}; dim leftD={F(dimL)} " +
                   $"rightD={F(dimR)} (world, vs the portrait; 0.000 = flush). " +
                   $"portrait w={F(portrait.rect.width)} h={F(portrait.rect.height)}; " +
                   $"ribbon w={F(ribbonRect.rect.width)}; dim w={F(((RectTransform)dim.transform).rect.width)}");
            yield return null;
        }

        // ═════════════════════════════════════════════════════════════════════
        // A14 — the RETURN modal's pending state
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// The red-team also noted A8 measures only the LEND modal, RETURN being "verified code
        /// parity". Parity is an argument; this is a measurement. Driven through the real
        /// RETURN button on a real borrowed character.
        /// </summary>
        private IEnumerator TraceReturnPending(Golfin.Roster.CarouselController? carousel, string avoidId)
        {
            string borrowedId = FirstLockedId(avoidId);
            if (string.IsNullOrEmpty(borrowedId))
            { Assert("A14_return_pending", false, "no catalog character available to borrow"); yield break; }

            SetLoans("", Loan("aaaaaaaa-0000-4000-8000-000000000002", "character", borrowedId,
                              asLender: false, level: 64, left: TimeSpan.FromHours(5.4), days: 1));
            yield return PushLoans();
            if (carousel != null) carousel.SelectCharacter(borrowedId);
            yield return new WaitForSecondsRealtime(1.2f);

            var detail = FindActive<Golfin.Roster.CharacterDetailPanel>();
            Button? ret = detail == null ? null
                : detail.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name == "LendButton");
            if (ret == null || !ret.interactable)
            { Assert("A14_return_pending", false, $"the RETURN button was not tappable (found={ret != null})"); yield break; }

            ret.onClick.Invoke();                                   // the REAL entry point
            yield return new WaitForSecondsRealtime(0.8f);

            var modal = Resources.FindObjectsOfTypeAll<LoanReturnModalController>()
                .FirstOrDefault(m => m != null && !string.IsNullOrEmpty(m.gameObject.scene.name) && m.IsVisible());
            if (modal == null)
            { Assert("A14_return_pending", false, "the RETURN tap opened no visible LoanReturnModalController"); yield break; }

            Button? confirm = Priv<Button>(modal, "returnButton");
            Button? cancel  = Priv<Button>(modal, "cancelButton");
            var label       = Priv<TMPro.TextMeshProUGUI>(modal, "returnButtonText");
            GameObject? spin = Priv<GameObject>(modal, "spinner");
            if (confirm == null || cancel == null || label == null)
            { Assert("A14_return_pending", false, $"unwired: return={confirm != null} cancel={cancel != null} label={label != null}"); yield break; }

            string before = label.text;
            bool wasC = confirm.interactable, wasX = cancel.interactable;

            _t.PostDelay = 1.5f;
            confirm.onClick.Invoke();                               // the REAL RETURN button
            yield return new WaitForSecondsRealtime(0.5f);
            string midText = label.text;
            bool midC = confirm.interactable, midX = cancel.interactable;
            bool midSpin = spin != null && spin.activeSelf;
            yield return Snap("polish_return_pending_midflight");

            yield return new WaitForSecondsRealtime(2.0f);
            Assert("A14_return_pending",
                   midText == Golfin.UI.Polish.PendingSpend.PendingLabel && !midC && !midX && !midSpin
                   && label.text == before && confirm.interactable == wasC && cancel.interactable == wasX,
                   $"label '{before}' -> '{midText}' -> '{label.text}'; return {wasC}->{midC}->{confirm.interactable}; " +
                   $"cancel {wasX}->{midX}->{cancel.interactable}; spinnerActiveMidFlight={midSpin}");

            foreach (var m in Resources.FindObjectsOfTypeAll<LoanReturnModalController>())
                if (!string.IsNullOrEmpty(m.gameObject.scene.name)) m.Hide();
            SetLoans("", "");
            yield return PushLoans();
            yield return new WaitForSecondsRealtime(0.5f);
        }

        /// <summary>A catalog character the player does NOT own — borrowing flips exactly that row.</summary>
        private static string FirstLockedId(params string[] avoid)
        {
            var cm = Golfin.Roster.CharacterManager.Instance;
            if (cm == null) return "";
            foreach (var c in cm.GetAllCatalogCharacters())
                if (!c.isOwned && !avoid.Contains(c.characterId)) return c.characterId;
            return "";
        }

        // ═════════════════════════════════════════════════════════════════════
        // A10
        // ═════════════════════════════════════════════════════════════════════

        private IEnumerator TraceLeaveMidMotion(LoanRibbonView ribbon, RectTransform rect,
                                                GameObject dim, float restY,
                                                Golfin.UI.PersistentUIManager? puim,
                                                Golfin.Roster.CarouselController? carousel, string id)
        {
            // Hide it, then re-arm the entrance and walk out 0.1 s in.
            SetLoans("", "");
            yield return PushLoans();
            if (carousel != null) carousel.SelectCharacter(id);
            yield return new WaitForSecondsRealtime(0.6f);

            LockClock();
            SetLoans(Loan("aaaaaaaa-0000-4000-8000-000000000001", "character", id,
                          asLender: true, level: 82, left: TimeSpan.FromHours(52), days: 3), "");
            yield return PushLoans();
            if (carousel != null) carousel.SelectCharacter(id);

            yield return Frames(12);                            // exactly 0.1 s into a 0.25 s Rise
            UnlockClock();
            yield return GoTo(puim != null ? puim.inventoryButton : null,
                              GolfinRedux.UI.ScreenId.Inventory);

            CanvasGroup? rg = rect.GetComponent<CanvasGroup>();
            CanvasGroup? dg = dim.GetComponent<CanvasGroup>();
            float y = rect.anchoredPosition.y;
            float a = rg != null ? rg.alpha : -1f;
            float d = dg != null ? dg.alpha : -1f;

            Assert("A10_ondisable_settles_at_rest",
                   Mathf.Abs(y - restY) < 0.01f && Mathf.Abs(a - 1f) < 0.01f && Mathf.Abs(d - 1f) < 0.01f,
                   $"left the screen 0.1 s into the entrance; y={F(y)} (rest {F(restY)}) ribbonAlpha={F(a)} dimAlpha={F(d)}");

            yield return GoTo(puim != null ? puim.charactersButton : null,
                              GolfinRedux.UI.ScreenId.Roster);
            if (carousel != null) carousel.SelectCharacter(id);
            yield return new WaitForSecondsRealtime(0.8f);
        }

        // ═════════════════════════════════════════════════════════════════════
        // A9 — the card badge
        // ═════════════════════════════════════════════════════════════════════

        private IEnumerator TraceBadge(string lentId)
        {
            LoanBadgeView? badge = Resources.FindObjectsOfTypeAll<LoanBadgeView>()
                .FirstOrDefault(b => b != null && !string.IsNullOrEmpty(b.gameObject.scene.name)
                                  && b.GetComponentInParent<Golfin.Roster.CharacterThumbnailCard>(true) != null
                                  && string.Equals(
                                        Priv<string>(b.GetComponentInParent<Golfin.Roster.CharacterThumbnailCard>(true)!, "characterId"),
                                        lentId, StringComparison.Ordinal));

            if (badge == null)
            {
                Assert("A9_badge_indicator", false, "no LoanBadgeView on a card bound to the lent character");
                yield break;
            }

            GameObject? root = Priv<GameObject>(badge, "badgeRoot");
            if (root == null) { Assert("A9_badge_indicator", false, "badgeRoot unwired"); yield break; }

            // It is already showing (the loan is live). Turn it OFF through the real card repaint
            // and trace the alpha, then back ON.
            var card = badge.GetComponentInParent<Golfin.Roster.CharacterThumbnailCard>(true);
            CanvasGroup? cg = root.GetComponent<CanvasGroup>();

            Assert("A9a_badge_active_at_alpha_state",
                   root.activeSelf && cg != null,
                   $"Indicator leaves the badge ACTIVE with a CanvasGroup: activeSelf={root.activeSelf} cg={(cg != null ? F(cg.alpha) : "<none>")}");

            // A repaint that says the same thing must be flat.
            var flat = new List<float>();
            if (card != null) card.RefreshIcons();
            for (int f = 0; f < 12; f++) { flat.Add(cg != null ? cg.alpha : -1f); yield return null; }
            Assert("A9b_unchanged_repaint_is_flat",
                   flat.Count > 0 && (flat.Max() - flat.Min()) < 0.001f,
                   $"RefreshIcons with no state change: spread={F(flat.Max() - flat.Min())} alpha={F(flat[0])}");

            // A REAL STATE CHANGE, through the badge's own public entry point.
            //
            // `Apply` is what CharacterThumbnailCard.RefreshIcons calls — line 156, one argument
            // pair — so driving it is driving the shipped path, not poking at the indicator. It is
            // driven directly here rather than through a reconcile because the local
            // `PlayerCharacterData.isLentOut` this editor session holds is whatever the stubbed
            // reconcile left it as; the diagnostic below records what it was, so the reading is
            // not silently standing on it.
            var pd = Golfin.Roster.CharacterManager.Instance?.GetCharacterData(lentId);
            string diag = pd != null ? $"local isLentOut={pd.isLentOut} isBorrowed={pd.isBorrowed}" : "no PlayerCharacterData";

            var fade = new List<float>();
            LockClock();
            badge.Apply(false, false);                     // lent -> free
            for (int f = 0; f < 40; f++) { fade.Add(cg != null ? cg.alpha : -1f); yield return null; }
            float end = cg != null ? cg.alpha : -1f;

            var again = new List<float>();
            badge.Apply(false, false);                     // the same thing said twice
            for (int f = 0; f < 20; f++) { again.Add(cg != null ? cg.alpha : -1f); yield return null; }

            var back = new List<float>();
            badge.Apply(true, false);                      // free -> lent
            for (int f = 0; f < 40; f++) { back.Add(cg != null ? cg.alpha : -1f); yield return null; }
            UnlockClock();
            float endBack = cg != null ? cg.alpha : -1f;

            Assert("A9c_state_change_animates",
                   fade.Count > 0 && fade[0] < 0.999f && fade.Distinct().Count() >= 2 && end < 0.01f
                   && back.Count > 0 && back[0] < 0.999f && back.Distinct().Count() >= 2
                   && Mathf.Abs(endBack - 1f) < 0.01f,
                   $"{diag}; OFF distinct={fade.Distinct().Count()} end={F(end)} trace=[{F(fade.Take(10))}]; " +
                   $"ON distinct={back.Distinct().Count()} end={F(endBack)} trace=[{F(back.Take(10))}]");

            Assert("A9d_repeat_apply_does_not_reanimate",
                   again.Count > 2 && (again.Max() - again.Min()) < 0.0001f,
                   $"Apply(false,false) twice: second call spread={F(again.Max() - again.Min())} alpha={F(again[0])}");
        }

        // ═════════════════════════════════════════════════════════════════════
        // A4 — Clear
        // ═════════════════════════════════════════════════════════════════════

        private IEnumerator TraceClear(RectTransform rect, GameObject root, GameObject dim,
                                       Golfin.Roster.CarouselController? carousel, string id)
        {
            SetLoans(Loan("aaaaaaaa-0000-4000-8000-000000000001", "character", id,
                          asLender: true, level: 82, left: TimeSpan.FromHours(52), days: 3), "");
            yield return PushLoans();
            if (carousel != null) carousel.SelectCharacter(id);
            yield return new WaitForSecondsRealtime(0.8f);

            CanvasGroup? rg = rect.GetComponent<CanvasGroup>();
            var al = new List<float>();
            bool sawActiveDuringFade = false;

            LockClock();
            SetLoans("", "");
            yield return PushLoans();
            for (int f = 0; f < 40; f++)
            {
                al.Add(rg != null ? rg.alpha : -1f);
                // ACTIVE and no longer at full alpha = on its way out rather than snapped off.
                // The first sample is same-frame with the trigger, so this cannot be missed.
                if (root.activeSelf && al[al.Count - 1] < 0.999f) sawActiveDuringFade = true;
                yield return null;
            }
            yield return Frames(20);
            UnlockClock();

            Assert("A4_clear_fades_then_deactivates",
                   sawActiveDuringFade && !root.activeSelf && !dim.activeSelf,
                   $"sawPartialAlphaWhileActive={sawActiveDuringFade} ribbonActive={root.activeSelf} " +
                   $"dimActive={dim.activeSelf} trace=[{F(al.Take(12))}]");
        }

        // ═════════════════════════════════════════════════════════════════════
        // The modal — A5 bump, A6 stagger, A7 placeholders, A8 pending, A11 empty
        // ═════════════════════════════════════════════════════════════════════

        private IEnumerator TraceModal(Golfin.Roster.CarouselController? carousel, string id)
        {
            SetLoans("", "");
            yield return PushLoans();
            if (carousel != null) carousel.SelectCharacter(id);
            yield return new WaitForSecondsRealtime(1f);

            var detail = FindActive<Golfin.Roster.CharacterDetailPanel>();
            Button? lend = detail == null ? null
                : detail.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name == "LendButton");
            if (lend == null || !lend.interactable)
            {
                Assert("PRE_lend_button", false,
                       $"the real LEND button was not tappable (found={lend != null} interactable={lend?.interactable})");
                yield break;
            }

            // ⚠️ THE LEND MODAL IS INSTANTIATED ONCE PER SCREEN (Roster and Inventory), so "the
            // first LoanModalController in the scene" is a coin flip — and the first probe run
            // lost it: it read rows, buttons and labels off the INVENTORY copy while the Roster
            // copy was the one on screen, and reported 0 rows and an untouched LEND button. The
            // modal under test is the VISIBLE one, asked for after the tap.
            LockClock();
            lend.onClick.Invoke();                                  // the REAL entry point
            yield return null;
            var modal = Resources.FindObjectsOfTypeAll<LoanModalController>()
                .FirstOrDefault(m => m != null && !string.IsNullOrEmpty(m.gameObject.scene.name) && m.IsVisible());
            if (modal == null) { UnlockClock(); Assert("PRE_modal", false, "the LEND tap opened no visible LoanModalController"); yield break; }
            Assert("PRE_modal", true, "the visible modal is the one under " + PathOf(modal.transform));

            // ── A6 + A7: ONE sampling loop, from the tap ─────────────────────
            //
            // Run 2 sampled the two phases in sequence and lost both: the placeholder pass burned
            // 10 frames, and by the time the row pass started the 0.09 s stagger was already over
            // (all three rows reported "first visible at frame 0"). It also counted SEVEN
            // placeholder rows, because `Destroy` is deferred to end of frame — the four dying
            // placeholders and the three new real rows are on the parent together for one frame,
            // and the real rows' stagger-zeroed alpha was being read as "a placeholder was
            // animated". Both are fixed by sampling everything from frame 0 and classifying each
            // row by its OWN UserId.
            Transform? parent = Priv<Transform>(modal, "recipientParent");
            var rowsList = Priv<List<LoanRecipientRow>>(modal, "_rows");

            bool placeholderAnimated = false;
            int placeholderRows = 0;
            var firstVisibleFrame = new Dictionary<int, int>();
            var endAlpha = new Dictionary<int, float>();
            // The stagger's signature, read across ROWS within one frame rather than across frames:
            // at any instant inside the run an earlier row is further along than a later one. Rows
            // that rose together would be equal in every frame.
            bool sawStaggeredProfile = false;
            string profileSample = "(none observed)";

            for (int f = 0; f < 90; f++)
            {
                if (parent != null)
                {
                    foreach (var r in parent.GetComponentsInChildren<LoanRecipientRow>(true))
                    {
                        if (r == null || !string.IsNullOrEmpty(r.UserId)) continue;   // real rows are not placeholders
                        placeholderRows = Mathf.Max(placeholderRows, 1);
                        var pcg = r.GetComponent<CanvasGroup>();
                        if (pcg != null && pcg.alpha < 0.99f) placeholderAnimated = true;
                    }
                }

                if (rowsList != null)
                {
                    for (int i = 0; i < rowsList.Count; i++)
                    {
                        var row = rowsList[i];
                        if (row == null || string.IsNullOrEmpty(row.UserId)) continue;
                        var cg = row.GetComponent<CanvasGroup>();
                        if (cg == null) continue;
                        endAlpha[i] = cg.alpha;
                        if (cg.alpha > 0.02f && !firstVisibleFrame.ContainsKey(i)) firstVisibleFrame[i] = f;
                    }

                    if (!sawStaggeredProfile && rowsList.Count >= 2)
                    {
                        var now = new List<float>();
                        for (int i = 0; i < rowsList.Count; i++)
                        {
                            var cg = rowsList[i] != null ? rowsList[i].GetComponent<CanvasGroup>() : null;
                            if (cg != null && !string.IsNullOrEmpty(rowsList[i].UserId)) now.Add(cg.alpha);
                        }
                        if (now.Count >= 2 && now[0] > now[now.Count - 1] + 0.001f && NonIncreasing(now))
                        {
                            sawStaggeredProfile = true;
                            profileSample = $"frame {f}: [{F(now)}]";
                        }
                    }
                }
                yield return null;
            }
            yield return Frames(40);
            UnlockClock();
            for (int i = 0; rowsList != null && i < rowsList.Count; i++)
            {
                var cg = rowsList[i] != null ? rowsList[i].GetComponent<CanvasGroup>() : null;
                if (cg != null) endAlpha[i] = cg.alpha;
            }

            Assert("A7_placeholders_not_animated",
                   placeholderRows > 0 && !placeholderAnimated,
                   $"placeholder rows seen={placeholderRows > 0} anyPlaceholderBelowAlpha1={placeholderAnimated} " +
                   "(rows classified by their own empty UserId, so the frame where the dying " +
                   "placeholders and the new rows overlap cannot be misread)");

            // NON-DECREASING, and the last row strictly later than the first. Strict per-pair
            // would be a frame-rate assertion rather than a stagger assertion: StaggerDelay is
            // 0.03 s, so on a slow editor frame two neighbours can legitimately light up together.
            // What cannot happen without a stagger is a SPREAD.
            bool ordered = firstVisibleFrame.Count >= 2;
            for (int i = 1; i < firstVisibleFrame.Count; i++)
                if (!firstVisibleFrame.ContainsKey(i) || !firstVisibleFrame.ContainsKey(i - 1)
                    || firstVisibleFrame[i] < firstVisibleFrame[i - 1]) ordered = false;
            if (ordered)
            {
                int lo = firstVisibleFrame.OrderBy(k => k.Key).First().Value;
                int hi = firstVisibleFrame.OrderBy(k => k.Key).Last().Value;
                if (hi <= lo) ordered = false;
            }
            bool allUp = endAlpha.Count > 0 && endAlpha.Values.All(a => a > 0.99f);

            // The gate is the per-frame ALPHA PROFILE (host-independent in shape: it holds at any
            // instant inside the run) OR the frame-index spread, plus every row settling at 1.
            // Both are reported. LIMIT, stated rather than hidden: a host slow enough that no frame
            // lands inside the ~0.34 s run can observe neither, and this fails LOUDLY there rather
            // than passing quietly — the detail line says which half was missing.
            Assert("A6_rows_stagger_rise",
                   (sawStaggeredProfile || ordered) && allUp,
                   $"staggered alpha profile across rows in one frame: {sawStaggeredProfile} {profileSample}; " +
                   $"firstVisibleFrame per row = [{string.Join(" ", firstVisibleFrame.OrderBy(k => k.Key).Select(k => k.Key + ":" + k.Value))}] " +
                   $"spreadOrdered={ordered}; endAlphas=[{string.Join(" ", endAlpha.OrderBy(k => k.Key).Select(k => F(k.Value)))}]");

            // A6b — WHAT THE FRAME SHOWS, as numbers. The first capture of this modal had a blank
            // slot where the first recipient should be, so every per-row quantity that could hide
            // a row is dumped: its own alpha, its parent chain's alphas, its rect, and whether it
            // is inside the viewport.
            var dump = new StringBuilder();
            var viewport = Priv<ScrollRect>(modal, "recipientScroll");
            RectTransform? vp = viewport != null ? viewport.viewport : null;
            bool allVisible = true;
            for (int i = 0; rowsList != null && i < rowsList.Count; i++)
            {
                var row = rowsList[i];
                if (row == null) { dump.Append($"[{i} DESTROYED] "); allVisible = false; continue; }
                var rt = (RectTransform)row.transform;
                var cg = row.GetComponent<CanvasGroup>();
                float chain = 1f;
                for (Transform t = row.transform; t != null; t = t.parent)
                {
                    var g = t.GetComponent<CanvasGroup>();
                    if (g != null) chain *= g.alpha;
                }
                bool inVp = true;
                if (vp != null)
                {
                    var rc = new Vector3[4]; rt.GetWorldCorners(rc);
                    var vc = new Vector3[4]; vp.GetWorldCorners(vc);
                    inVp = rc[1].y > vc[0].y && rc[0].y < vc[1].y;
                }
                var img = rt.GetComponent<Image>();
                dump.Append($"[{i} {row.UserId.Substring(0, 4)} y={F(rt.anchoredPosition.y)} h={F(rt.rect.height)} " +
                            $"a={(cg != null ? F(cg.alpha) : "none")} chain={F(chain)} act={row.gameObject.activeSelf} " +
                            $"inVp={inVp} scale={F(rt.localScale.x)}] ");
                if (chain < 0.99f || !row.gameObject.activeSelf || !inVp) allVisible = false;
            }
            Assert("A6b_every_row_is_actually_visible_at_rest", allVisible, dump.ToString());

            yield return Snap("polish_lend_modal_rows");

            // ── A5: the bump ─────────────────────────────────────────────────
            if (rowsList == null || rowsList.Count < 2)
            {
                Assert("A5_selection_bump", false, $"needed 2 recipient rows, got {rowsList?.Count ?? 0}");
            }
            else
            {
                Button? b0 = rowsList[0].GetComponentInChildren<Button>(true);
                Button? b1 = rowsList[1].GetComponentInChildren<Button>(true);
                var s0 = new List<float>(); var s1 = new List<float>();

                // SLOW THE CLOCK, NOT THE TWEEN. `UiMotion.BumpDur` is 0.10 s — three or four
                // rendered frames — and run 2 caught exactly ONE sample above 1 because a single
                // long editor frame stepped straight over the peak. `captureDeltaTime` fixes the
                // frame delta the whole app sees, so the same unchanged 0.10 s bump is sampled at
                // 8 ms instead of 30-90 ms. The instrument changes; the thing under test does not
                // (memory: feedback_point_the_instrument_at_the_subject).
                // …AND SET IT BEFORE THE FRAME THAT MATTERS. `captureDeltaTime` governs from the
                // NEXT frame on, so run 3 set it on the same frame as the tap and the tap's own
                // (long) frame still swallowed the bump — one sample above 1, while the SECOND
                // tap, by then running on the slowed clock, showed all five and peaked at exactly
                // UiMotion.BumpPeak. Same instrument, applied one frame earlier.
                LockClock();
                yield return Frames(4);                              // let the clock take effect
                if (b0 != null) b0.onClick.Invoke();                 // the REAL row widget
                for (int f = 0; f < 40; f++)
                {
                    s0.Add(rowsList[0].transform.localScale.x);
                    s1.Add(rowsList[1].transform.localScale.x);
                    yield return null;
                }
                int over0 = s0.Count(v => v > 1.0001f);
                // HOST-INDEPENDENT: s0[0] is read in the same frame as the tap, so it is the value
                // BumpRoutine's synchronous first segment left. A row that did not bump is at
                // exactly 1.000 in that frame and in every frame after. The peak and the
                // frames-above-1 count are REPORTED, not gated on — on a host slow enough to step
                // over a 0.10 s tween the peak is simply not sampleable, and demanding it would be
                // an assertion about the editor rather than about the bump.
                Assert("A5_selected_row_bumps",
                       s0.Count > 0 && Mathf.Abs(s0[0] - 1f) > 0.0001f
                       && s0.Max() <= Golfin.UI.Polish.UiMotion.BumpPeak + 0.0005f
                       && Mathf.Abs(s0.Last() - 1f) < 0.001f,
                       $"scale left 1.000 in the frame of the tap: firstSample={F(s0[0])}; " +
                       $"framesAbove1={over0} peak={F(s0.Max())} (UiMotion.BumpPeak={F(Golfin.UI.Polish.UiMotion.BumpPeak)}) " +
                       $"settled={F(s0.Last())} trace=[{F(s0.Take(14))}]");
                Assert("A5b_other_rows_do_not_bump",
                       s1.All(v => Mathf.Abs(v - 1f) < 0.0001f),
                       $"row1 scale spread={F(s1.Max() - s1.Min())}");

                // The previous selection must not bump when the next row is picked.
                var prev = new List<float>(); var nxt = new List<float>();
                if (b1 != null) b1.onClick.Invoke();
                for (int f = 0; f < 40; f++)
                {
                    prev.Add(rowsList[0].transform.localScale.x);
                    nxt.Add(rowsList[1].transform.localScale.x);
                    yield return null;
                }
                UnlockClock();                                       // hand the clock back
                Assert("A5c_previous_selection_does_not_bump",
                       prev.All(v => Mathf.Abs(v - 1f) < 0.0001f)
                       && nxt.Count > 0 && Mathf.Abs(nxt[0] - 1f) > 0.0001f,
                       $"row0 (deselected) scale spread={F(prev.Max() - prev.Min())} — it never leaves 1.000; " +
                       $"row1 (newly selected) firstSample={F(nxt[0])} framesAbove1={nxt.Count(v => v > 1.0001f)} peak={F(nxt.Max())}");
            }

            // ── A8: the pending state ────────────────────────────────────────
            Button? confirm = Priv<Button>(modal, "confirmButton");
            Button? cancel  = Priv<Button>(modal, "cancelButton");
            var confirmLabel = Priv<TMPro.TextMeshProUGUI>(modal, "confirmText");
            GameObject? spinner = Priv<GameObject>(modal, "confirmSpinner");

            if (confirm == null || cancel == null || confirmLabel == null)
            {
                Assert("A8_pending_state", false,
                       $"unwired: confirm={confirm != null} cancel={cancel != null} label={confirmLabel != null}");
            }
            else
            {
                string before = confirmLabel.text;
                bool wasConfirm = confirm.interactable, wasCancel = cancel.interactable;
                _t.PostDelay = 1.5f;
                confirm.onClick.Invoke();                            // the REAL LEND button

                yield return new WaitForSecondsRealtime(0.5f);       // mid-flight
                string midText = confirmLabel.text;
                bool midConfirm = confirm.interactable, midCancel = cancel.interactable;
                bool midSpinner = spinner != null && spinner.activeSelf;
                yield return Snap("polish_pending_midflight");

                yield return new WaitForSecondsRealtime(2.0f);       // the answer landed
                string afterText = confirmLabel.text;
                bool afterConfirm = confirm.interactable, afterCancel = cancel.interactable;

                Assert("A8_pending_midflight",
                       midText == Golfin.UI.Polish.PendingSpend.PendingLabel && !midConfirm && !midCancel,
                       $"label='{midText}' (expected '{Golfin.UI.Polish.PendingSpend.PendingLabel}') " +
                       $"confirmInteractable={midConfirm} cancelInteractable={midCancel}");
                Assert("A8b_restores_on_the_answer",
                       afterText == before && afterConfirm == wasConfirm && afterCancel == wasCancel,
                       $"label '{before}' -> '{midText}' -> '{afterText}'; confirm {wasConfirm}->{midConfirm}->{afterConfirm}; " +
                       $"cancel {wasCancel}->{midCancel}->{afterCancel}");
                Assert("A8c_no_spinner",
                       !midSpinner,
                       $"the hand-rolled spinner object stayed inactive through the whole round-trip: midFlightActive={midSpinner}");
            }

            // ── A11: the empty state fades in ────────────────────────────────
            foreach (var m in Resources.FindObjectsOfTypeAll<LoanModalController>())
                if (!string.IsNullOrEmpty(m.gameObject.scene.name)) m.Hide();
            yield return new WaitForSecondsRealtime(0.5f);

            _t.FollowingJson = "[]";
            LockClock();
            lend.onClick.Invoke();
            GameObject? empty = Priv<GameObject>(modal, "emptyStateRoot");
            var ea = new List<float>();
            for (int f = 0; f < 90; f++)
            {
                if (empty != null && empty.activeSelf)
                {
                    var cg = empty.GetComponent<CanvasGroup>();
                    if (cg != null) ea.Add(cg.alpha);
                }
                yield return null;
            }
            yield return Frames(30);
            UnlockClock();
            float endEmpty = empty != null && empty.GetComponent<CanvasGroup>() != null
                ? empty.GetComponent<CanvasGroup>()!.alpha : -1f;
            // SHAPE, not a floor. `min < 0.35` was the last first-sample floor left in this file —
            // the same frame-rate assertion A1b and A2 had already shed, and it survived only
            // because it happened to pass on the implementer's host.
            Assert("A11_empty_state_fades_in",
                   ea.Count > 0 && ea[0] < 0.999f && NonDecreasing(ea)
                   && ea.Distinct().Count() >= 2 && Mathf.Abs(endEmpty - 1f) < 0.01f,
                   $"samples={ea.Count} first={F(ea.DefaultIfEmpty(-1f).First())} distinct={ea.Distinct().Count()} " +
                   $"monotonic={NonDecreasing(ea)} end={F(endEmpty)} trace=[{F(ea.Take(10))}]");

            yield return Snap("polish_lend_modal_empty");
            foreach (var m in Resources.FindObjectsOfTypeAll<LoanModalController>())
                if (!string.IsNullOrEmpty(m.gameObject.scene.name)) m.Hide();
            _t.FollowingJson = Following();
            yield return new WaitForSecondsRealtime(0.4f);
        }

        // ═════════════════════════════════════════════════════════════════════
        // A12 — the club ribbon sits ON the club artwork
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Cesar, 2026-09-09: "On loan overlay is not correctly over the image in clubs (it spills
        /// to the left and does not reach the right border)."
        ///
        /// <para>MEASURED IN PLAY MODE, because the club's LeftPanel is a VerticalLayoutGroup and
        /// its children have no real rect until the screen is up and layout has run — an edit-mode
        /// read puts ClubImage 268 px outside the panel and would send the fix the wrong way.</para>
        /// </summary>
        private IEnumerator TraceClubRibbon(Golfin.UI.PersistentUIManager? puim)
        {
            yield return GoTo(puim != null ? puim.inventoryButton : null,
                              GolfinRedux.UI.ScreenId.Inventory);

            var clubs = global::ClubManager.Instance?.GetAllOwnedClubs();
            string clubId = clubs != null && clubs.Count > 0 ? clubs[0].clubId : "";
            if (string.IsNullOrEmpty(clubId))
            { Assert("A12_club_ribbon_on_artwork", false, "no owned club to lend"); yield break; }

            SetLoans(Loan("aaaaaaaa-0000-4000-8000-000000000003", "club", clubId,
                          asLender: true, level: 11, left: TimeSpan.FromHours(160), days: 7), "");
            yield return PushLoans();
            yield return new WaitForSecondsRealtime(1.2f);

            var panel = FindActive<Golfin.Inventory.ClubDetailPanel>();
            if (panel == null) { Assert("A12_club_ribbon_on_artwork", false, "no active ClubDetailPanel"); yield break; }
            var space = (RectTransform)panel.transform;

            RectTransform? img = Deep(space, "ClubImage");
            RectTransform? rib = Deep(space, "LoanRibbon");
            RectTransform? dim = Deep(space, "LentDim");
            if (img == null || rib == null || dim == null)
            { Assert("A12_club_ribbon_on_artwork", false, $"img={img != null} ribbon={rib != null} dim={dim != null}"); yield break; }

            var report = new StringBuilder();
            foreach (var pair in new (string, RectTransform)[] { ("ClubImage", img), ("LoanRibbon", rib), ("LentDim", dim) })
            {
                var c = new Vector3[4]; pair.Item2.GetWorldCorners(c);
                Vector3 bl = space.InverseTransformPoint(c[0]);
                Vector3 tr = space.InverseTransformPoint(c[2]);
                report.Append($"{pair.Item1} L={F(bl.x)} R={F(tr.x)} T={F(tr.y)} w={F(pair.Item2.rect.width)}; ");
            }

            var ci = new Vector3[4]; img.GetWorldCorners(ci);
            var cr = new Vector3[4]; rib.GetWorldCorners(cr);
            var cd = new Vector3[4]; dim.GetWorldCorners(cd);
            float ribL = cr[0].x - ci[0].x, ribR = cr[2].x - ci[2].x;
            float dimL = cd[0].x - ci[0].x, dimR = cd[2].x - ci[2].x;

            Assert("A12_club_ribbon_on_artwork",
                   Mathf.Abs(ribL) < 0.5f && Mathf.Abs(ribR) < 0.5f
                   && Mathf.Abs(dimL) < 0.5f && Mathf.Abs(dimR) < 0.5f,
                   $"ribbon leftD={F(ribL)} rightD={F(ribR)}; dim leftD={F(dimL)} rightD={F(dimR)} " +
                   $"(world x, vs ClubImage; 0.000 = flush). {report}");

            yield return Snap("polish_club_lent_ribbon");
        }

        private static RectTransform? Deep(Transform root, string name)
        {
            foreach (RectTransform t in root.GetComponentsInChildren<RectTransform>(true))
                if (t.name == name) return t;
            return null;
        }

        // ═════════════════════════════════════════════════════════════════════

        private IEnumerator Snap(string label)
        {
            yield return new WaitForEndOfFrame();
            string path = Golfin.Diagnostics.Runtime.CaptureCore.SnapPlayModeSafe(label);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                Debug.LogError($"[LoanPolishProbe] SnapPlayModeSafe wrote no file for '{label}' (path='{path}')");
                yield break;
            }
            _shots.Add(path);
        }

        private IEnumerator Finish()
        {
            UnlockClock();      // never hand the editor back a pinned clock, on any exit path

            Directory.CreateDirectory(LoanPolishProbe.ShotDir);
            var copied = new List<string>();
            foreach (string src in _shots)
            {
                string dst = Path.Combine(LoanPolishProbe.ShotDir, Path.GetFileName(src));
                try { File.Copy(src, dst, true); copied.Add(dst); }
                catch (Exception e) { Debug.LogWarning($"[LoanPolishProbe] copy failed {src}: {e.Message}"); }
            }

            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"task\": \"asset_loans_polish\",");
            sb.AppendLine("  \"utc\": " + Q(DateTime.UtcNow.ToString("u")) + ",");
            sb.AppendLine("  \"unity\": " + Q(Application.unityVersion) + ",");
            sb.AppendLine("  \"driver\": \"real navigation (bottom-nav onClick, LendButton.onClick, row Button.onClick, confirm onClick); HTTP transport stubbed\",");
            sb.AppendLine("  \"fail\": " + _fail + ",");
            sb.AppendLine("  \"assertions\": [");
            sb.AppendLine(string.Join(",\n", _lines));
            sb.AppendLine("  ],");
            sb.AppendLine("  \"screenshots\": [" + string.Join(", ", copied.Select(Q)) + "]");
            sb.AppendLine("}");

            Directory.CreateDirectory(Path.GetDirectoryName(LoanPolishProbe.OutPath)!);
            File.WriteAllText(LoanPolishProbe.OutPath, sb.ToString());
            Debug.Log($"[LoanPolishProbe] DONE fail={_fail} -> {LoanPolishProbe.OutPath}");
            yield return null;
            EditorApplication.isPlaying = false;
        }

        // ── navigation (same discipline as LoanUiCaptureBot) ─────────────────

        private static IEnumerator WaitForHomeToSettle()
        {
            var mgr = GolfinRedux.UI.ScreenManager.Instance;
            float quiet = Time.realtimeSinceStartup;
            var last = mgr != null ? mgr.CurrentScreen : GolfinRedux.UI.ScreenId.Logo;
            float deadline = Time.realtimeSinceStartup + 25f;
            while (Time.realtimeSinceStartup < deadline)
            {
                mgr = GolfinRedux.UI.ScreenManager.Instance;
                var now = mgr != null ? mgr.CurrentScreen : last;
                if (now != last) { last = now; quiet = Time.realtimeSinceStartup; }
                if (now == GolfinRedux.UI.ScreenId.Home && Time.realtimeSinceStartup - quiet >= 2f) yield break;
                yield return null;
            }
            Debug.LogWarning($"[LoanPolishProbe] Home never settled (stuck on {last}).");
        }

        private IEnumerator GoTo(Button? nav, GolfinRedux.UI.ScreenId target)
        {
            for (int attempt = 0; attempt < 4; attempt++)
            {
                if (attempt == 0 && nav != null) nav.onClick.Invoke();
                else GolfinRedux.UI.ScreenManager.Instance?.ShowScreen(target, instant: true);

                float deadline = Time.realtimeSinceStartup + 3f;
                while (Time.realtimeSinceStartup < deadline)
                {
                    if (OnScreen(target)) { yield return new WaitForSecondsRealtime(1.2f); yield break; }
                    yield return null;
                }
            }
            Debug.LogError($"[LoanPolishProbe] never got {target} on screen.");
        }

        private static bool OnScreen(GolfinRedux.UI.ScreenId target)
        {
            var mgr = GolfinRedux.UI.ScreenManager.Instance;
            if (mgr == null || mgr.CurrentScreen != target) return false;
            switch (target)
            {
                case GolfinRedux.UI.ScreenId.Roster:    return FindActive<Golfin.Roster.CharacterDetailPanel>() != null;
                case GolfinRedux.UI.ScreenId.Inventory: return FindActive<Golfin.Inventory.ClubDetailPanel>() != null;
                default: return true;
            }
        }
    }
}
#endif
