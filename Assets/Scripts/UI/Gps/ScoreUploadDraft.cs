// ─────────────────────────────────────────────────────────────────────────────
// score_upload_flow §2 — everything the six steps agree on, in one plain object.
//
// The flow is ONE screen with six roots, so "state" cannot live on a step: the
// GPS step needs the hole count the Edit step chose, the Confirm step needs the
// source the Capture step picked, and BACK has to be able to walk backwards
// through all of it without losing anything. This class is that shared memory,
// and it owns every derivation the steps would otherwise each get slightly wrong
// (what the total IS, whether it is postable, what the course is called).
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;

namespace Golfin.Gps.UI
{
    /// <summary>How the score reached the app. Decides the server's points/Trust base
    /// (screenshot 50/50, manual 20/30) and which step BACK from Edit Score returns to.</summary>
    public enum ScoreSource
    {
        Camera,
        Library,
        Manual
    }

    /// <summary>
    /// The in-flight score post. Plain C#, no MonoBehaviour: it is created when the screen opens
    /// and thrown away when it closes, and nothing about it needs a scene.
    /// </summary>
    public sealed class ScoreUploadDraft
    {
        public const int MaxHoles = 18;

        /// <summary>Server bounds, transcribed from score.py:26-27 (<c>SCORE_BOUNDS_18/9</c>). The
        /// client checks them ONLY to gate the button — the server checks them again and its 400 is
        /// the authority. Duplicating the numbers is deliberate: a player should not have to make a
        /// round trip to learn that 12 is not a golf score.</summary>
        public const int Min18 = 50, Max18 = 200, Min9 = 25, Max9 = 100;

        public ScoreSource Source = ScoreSource.Camera;

        /// <summary>The uploaded JPEG, already downscaled by
        /// <see cref="RecognitionService.EncodeForUpload"/>. Null on the manual path.</summary>
        public byte[]? Photo;

        public RecognitionResult? Recognition;

        /// <summary>Per-hole strokes, index 0 = hole 1. Null = the player never touched that hole.</summary>
        public readonly int?[] Holes = new int?[MaxHoles];

        /// <summary>Per-hole PAR, index 0 = hole 1. Drives the score cell's colour and its meta
        /// line (<see cref="HoleRowView.ColourFor"/>). Null throughout in v1 — the recognition
        /// prompt asks for a course par, not a per-hole breakdown — so the cells render white
        /// until a source exists. Filled by <see cref="GolfExtraction.Pars"/> when one does.</summary>
        public readonly int?[] Pars = new int?[MaxHoles];

        /// <summary>18 or 9. Seeded from the AI's <c>holes</c>, then owned by the Edit step's toggle.</summary>
        public int HoleCount = 18;

        /// <summary>Never filled in v1 — the API returns no putts and the Figma cell shows an em
        /// dash. Kept so the field has one home if a future task adds a putts editor.</summary>
        public int? Putts;

        public GpsScoreAttachment? Attachment;

        /// <summary>Set by the manual venue picker. Beats the attachment's auto-registered venue for
        /// DISPLAY — but the attachment still owns what goes up (see
        /// <see cref="ScoreService.Submit"/>), so a hand-picked course changes the name shown, and
        /// the coordinates that prove it stay the ones actually measured.</summary>
        public int? VenueOverrideId;
        public string? VenueOverrideName;

        /// <summary>The server's answer. Null until the post succeeds.</summary>
        public ScoreSubmitResult? Result;

        // ── derivations ───────────────────────────────────────────────────────

        /// <summary>True once the player has typed at least one hole. This is the switch between
        /// "post the AI's total" and "post the sum" — see <see cref="Total"/>.</summary>
        public bool AnyHoleEdited
        {
            get
            {
                for (int i = 0; i < ActiveHoleCount; i++)
                    if (Holes[i].HasValue) return true;
                return false;
            }
        }

        /// <summary>9-hole mode dims and disables holes 10–18; they are not summed and not posted.</summary>
        public int ActiveHoleCount => HoleCount == 9 ? 9 : MaxHoles;

        public int? SumRange(int firstHoleInclusive, int lastHoleInclusive)
        {
            int sum = 0;
            bool any = false;
            for (int h = firstHoleInclusive; h <= lastHoleInclusive && h <= ActiveHoleCount; h++)
            {
                int? v = Holes[h - 1];
                if (!v.HasValue) continue;
                sum += v.Value;
                any = true;
            }
            return any ? sum : (int?)null;
        }

        public int? Out => SumRange(1, 9);

        public int? In => SumRange(10, 18);

        /// <summary>The AI's total, or null on the manual path / an unreadable card.</summary>
        public int? AiScore => Recognition?.Golf().Score;

        /// <summary>
        /// THE number that gets posted.
        ///
        /// <para>
        /// Until the player edits a hole this is the AI's total verbatim, because the API returns a
        /// total and nothing else — reconstructing 18 holes from it would be invention. The moment
        /// any hole is typed the sum takes over and the screen says so
        /// (<c>SU_TOTAL_FROM_HOLES</c>), because two visible numbers that disagree is worse than
        /// either being wrong.
        /// </para>
        /// </summary>
        public int? Total => AnyHoleEdited ? SumRange(1, ActiveHoleCount) : AiScore;

        public int MinScore => HoleCount == 9 ? Min9 : Min18;
        public int MaxScore => HoleCount == 9 ? Max9 : Max18;

        /// <summary>Gate for VERIFY WITH GPS and POST SCORE. A null total is NOT in bounds — an
        /// empty manual card must not be postable.</summary>
        public bool TotalInBounds
        {
            get
            {
                int? t = Total;
                return t.HasValue && t.Value >= MinScore && t.Value <= MaxScore;
            }
        }

        public string ScoreType => HoleCount == 9 ? "9" : "18";

        /// <summary>"screenshot" for both photo paths, "manual" for typed. This string is what the
        /// server prices the post on.</summary>
        public string InputMethod => Source == ScoreSource.Manual ? "manual" : "screenshot";

        /// <summary>Hand-picked name first, then the venue GPS resolved, then whatever the AI read
        /// off the card. Empty string rather than null — <c>course_name</c> is required by the
        /// server and an unnamed round is legal.</summary>
        public string CourseName
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(VenueOverrideName)) return VenueOverrideName!;
                if (!string.IsNullOrWhiteSpace(Attachment?.VenueName)) return Attachment!.VenueName;
                string? fromCard = Recognition?.Golf().Course;
                return string.IsNullOrWhiteSpace(fromCard) ? string.Empty : fromCard!;
            }
        }

        /// <summary>The venue the UI should talk about. What actually goes up is the attachment's —
        /// see <see cref="VenueOverrideId"/>.</summary>
        public int? DisplayVenueId => VenueOverrideId ?? Attachment?.VenueId;

        public bool HasVenue => DisplayVenueId.HasValue;

        /// <summary>Par as the AI read it, or null. Null hides "(+N)" rather than assuming 72.</summary>
        public int? Par => Recognition?.Golf().Par;

        /// <summary>Strokes over par, or null when either half is unknown.</summary>
        public int? VsPar
        {
            get
            {
                int? t = Total;
                int? p = Par;
                return (t.HasValue && p.HasValue) ? t.Value - p.Value : (int?)null;
            }
        }

        /// <summary>The round's date: what the AI read off the card, else today. <c>yyyy.MM.dd</c>.</summary>
        public string DisplayDate
        {
            get
            {
                string? raw = Recognition?.Golf().Date;
                if (!string.IsNullOrWhiteSpace(raw) &&
                    DateTime.TryParse(raw, System.Globalization.CultureInfo.InvariantCulture,
                                      System.Globalization.DateTimeStyles.None, out DateTime parsed))
                    return parsed.ToString("yyyy.MM.dd", System.Globalization.CultureInfo.InvariantCulture);

                return DateTime.Now.ToString("yyyy.MM.dd", System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        // ── client-side estimates (Confirm step only — NEVER sent) ────────────

        /// <summary>
        /// What the Confirm step promises, using the server's own constants (score.py:18-20):
        /// 50 screenshot / 20 manual, +30 when a venue is set.
        ///
        /// <para>AN ESTIMATE, NOT A PROMISE. The Posted step renders
        /// <see cref="ScoreSubmitResult.PointsEarned"/> instead, because only the server knows about
        /// the rate-limit penalty, the mock penalty and badges.</para>
        /// </summary>
        public int PointsEstimate
        {
            get
            {
                int pts = Source == ScoreSource.Manual ? 20 : 50;
                if (HasVenue && Attachment?.Position != null) pts += 30;
                return pts;
            }
        }

        /// <summary>
        /// The Trust bar on the Confirm step (SPEC § Client Trust estimate):
        /// 50 screenshot | 30 manual, +30 with a venue, +20 at <c>gps_check_count ≥ 3</c> (K4),
        /// −40 on a mock fix; clamped 0–100.
        /// </summary>
        public int TrustEstimate
        {
            get
            {
                int trust = Source == ScoreSource.Manual ? 30 : 50;
                if (HasVenue && Attachment?.Position != null) trust += 30;
                if (Attachment?.Session != null && Attachment.Session.CheckCount >= 3) trust += 20;
                if (Attachment?.Signals != null && Attachment.Signals.IsMock) trust -= 40;
                return trust < 0 ? 0 : (trust > 100 ? 100 : trust);
            }
        }

        /// <summary>The per-hole list for the request, or null when nothing was typed — which is
        /// what keeps eighteen zeroes out of the database.</summary>
        public System.Collections.Generic.List<HoleScore>? HoleScores()
        {
            if (!AnyHoleEdited) return null;

            var list = new System.Collections.Generic.List<HoleScore>();
            for (int i = 0; i < ActiveHoleCount; i++)
                if (Holes[i].HasValue) list.Add(new HoleScore(i + 1, Holes[i]));
            return list;
        }

        /// <summary>The AI payload archived with the post, so a disputed score can be re-read
        /// against what the model actually said. Null on the manual path.</summary>
        public Newtonsoft.Json.Linq.JObject? ScreenshotData()
        {
            if (Recognition == null) return null;

            var o = Recognition.ExtractedData != null
                ? (Newtonsoft.Json.Linq.JObject)Recognition.ExtractedData.DeepClone()
                : new Newtonsoft.Json.Linq.JObject();

            if (!string.IsNullOrEmpty(Recognition.Id)) o["recognition_id"] = Recognition.Id;
            o["confidence"] = Recognition.Confidence;
            return o;
        }

        public ScoreSubmitRequest BuildRequest() => new ScoreSubmitRequest
        {
            Score = Total ?? 0,
            ScoreType = ScoreType,
            CourseName = CourseName,
            InputMethod = InputMethod,
            Holes = HoleScores(),
            ScreenshotData = ScreenshotData()
        };

        /// <summary>Wipe everything the player can redo. Called by RETAKE and by re-entering the
        /// screen, so a second upload never inherits the first one's photo or venue.</summary>
        public void ResetForNewCapture()
        {
            Photo = null;
            Recognition = null;
            for (int i = 0; i < MaxHoles; i++) { Holes[i] = null; Pars[i] = null; }
            HoleCount = 18;
            Putts = null;
            Attachment = null;
            VenueOverrideId = null;
            VenueOverrideName = null;
            Result = null;
        }
    }
}
