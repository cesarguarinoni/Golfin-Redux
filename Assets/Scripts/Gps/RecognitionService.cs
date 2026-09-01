// Order: score_upload_flow §1 — POST /recognition/analyze over the EXISTING ApiClient.
using System;
using System.Collections;
using Golfin.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Golfin.Gps
{
    /// <summary>
    /// The scorecard-reading half of the GPS module (SPEC §1) — the same plain-C#-singleton shape as
    /// <see cref="VenueService"/> and <c>Golfin.Economy.PointsService</c>, so this assembly keeps one
    /// service pattern rather than two. Constructible in an EditMode test, no MonoBehaviour, no
    /// offline queue (a photo the player has already moved on from is worthless replayed later).
    ///
    /// <para>
    /// TWO THINGS ARE DIFFERENT FROM EVERY OTHER CALL IN THE APP, and both are the reason this class
    /// exists rather than a one-line <c>ApiClient.Post</c>:
    /// </para>
    /// <list type="number">
    /// <item><b>90-second timeout.</b> <see cref="ApiClient.TimeoutSeconds"/> is 30, which is right
    /// for every CRUD call and wrong for this one: the request runs Claude Vision on a Fly machine
    /// that may be cold, and 20–40 s is a normal warm answer. The timeout is raised on THIS request
    /// only, through <see cref="ApiClient.SendRoutine"/>, never by mutating the shared client — a
    /// global 90 s would turn a dead network into a 90-second freeze on every screen in the game.</item>
    /// <item><b>The image is downscaled before it is encoded.</b> A modern phone photo is 12 MP and
    /// base64 inflates by 4/3; posting one raw is several megabytes over a course's LTE. 1600 px /
    /// q80 lands around 300–600 KB and Vision reads a scorecard at that size perfectly well.</item>
    /// </list>
    /// </summary>
    public sealed class RecognitionService
    {
        private const string Tag = "[RecognitionService]";

        private static RecognitionService _instance;

        public static RecognitionService Instance =>
            _instance ?? (_instance = new RecognitionService(ApiClient.Instance));

        public static void ConfigureForTest(RecognitionService service) => _instance = service;

        public static void ResetForTest() => _instance = null;

        /// <summary>Vision on a cold Fly machine takes 20–40 s. See the class remarks for why this is
        /// per-request and not a client-wide setting.</summary>
        public const int AnalyzeTimeoutSeconds = 90;

        /// <summary>Longest edge of the uploaded JPEG, in pixels.</summary>
        public const int MaxUploadEdgePx = 1600;

        public const int UploadJpegQuality = 80;

        /// <summary>The router sniffs the media type off this prefix (recognition.py:270-290); a bare
        /// base64 string also works, but the data URL is what the Dart client sends and what the
        /// sniffing path was written against.</summary>
        public const string JpegDataUrlPrefix = "data:image/jpeg;base64,";

        private readonly ApiClient _client;

        public RecognitionService(ApiClient client)
        {
            _client = client;
        }

        /// <summary>
        /// <c>POST /recognition/analyze {image_base64, sport_type:"golf"}</c> → a
        /// <see cref="RecognitionResult"/>.
        ///
        /// <para>
        /// <paramref name="jpeg"/> must ALREADY be encoded — use <see cref="EncodeForUpload"/> on the
        /// texture the camera/gallery handed back. Keeping the resize out of this method is what lets
        /// the EditMode tests assert the body shape without a GPU.
        /// </para>
        /// <para>
        /// <c>sport_type</c> is pinned to "golf" rather than left null: the router will classify a
        /// screenshot into any of its six sports, and a driving-range photo silently coming back as
        /// "running" would fill the Edit Score step with nonsense.
        /// </para>
        /// </summary>
        public IEnumerator Analyze(byte[] jpeg, Action<ApiResult<RecognitionResult>> onResult)
        {
            if (jpeg == null || jpeg.Length == 0)
            {
                onResult?.Invoke(ApiResult<RecognitionResult>.Fail(ApiErrorKind.NotConfigured,
                    "No image to analyze.", 0, null, 0));
                return Empty();
            }

            var body = new JObject
            {
                ["image_base64"] = JpegDataUrlPrefix + Convert.ToBase64String(jpeg),
                ["sport_type"] = "golf"
            };

            Debug.Log($"{Tag} uploading {jpeg.Length / 1024} KB JPEG " +
                      $"({body["image_base64"].Value<string>().Length / 1024} KB base64) to /recognition/analyze.");

            var request = new HttpRequest("POST", Endpoints.RecognitionAnalyze, body.ToString(Formatting.None))
            {
                TimeoutSeconds = AnalyzeTimeoutSeconds
            };

            return _client.SendRoutine(request, onResult);
        }

        private static IEnumerator Empty() { yield break; }

        // ── image preparation ─────────────────────────────────────────────────────

        /// <summary>
        /// Resize to <see cref="MaxUploadEdgePx"/> on the longest edge and encode JPEG at
        /// <see cref="UploadJpegQuality"/>. An image already inside the bound is encoded as-is —
        /// a needless Blit would only cost quality.
        ///
        /// <para>
        /// The resize goes through a temporary <see cref="RenderTexture"/> + <see cref="Graphics.Blit"/>
        /// so the GPU does the filtering; <c>Texture2D.Resize</c> would discard the pixels instead of
        /// resampling them. The RT is released on every path including the throwing one.
        /// </para>
        /// </summary>
        public static byte[] EncodeForUpload(Texture2D source,
                                             int maxEdgePx = MaxUploadEdgePx,
                                             int quality = UploadJpegQuality)
        {
            if (source == null) return null;

            int longest = Mathf.Max(source.width, source.height);
            if (longest <= maxEdgePx) return source.EncodeToJPG(quality);

            float scale = (float)maxEdgePx / longest;
            int w = Mathf.Max(1, Mathf.RoundToInt(source.width * scale));
            int h = Mathf.Max(1, Mathf.RoundToInt(source.height * scale));

            RenderTexture rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            Texture2D resized = null;
            try
            {
                Graphics.Blit(source, rt);
                RenderTexture.active = rt;

                resized = new Texture2D(w, h, TextureFormat.RGB24, mipChain: false);
                resized.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                resized.Apply(updateMipmaps: false);

                return resized.EncodeToJPG(quality);
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
                // DestroyImmediate outside play mode: Destroy is deferred to the next frame, and
                // there is no next frame in an Editor tool run, so the texture would leak.
                if (resized != null)
                {
                    if (Application.isPlaying) UnityEngine.Object.Destroy(resized);
                    else UnityEngine.Object.DestroyImmediate(resized);
                }
            }
        }
    }
}
