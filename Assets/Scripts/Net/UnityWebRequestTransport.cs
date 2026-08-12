// Order: reward_points_backend Slice 1 — the real HTTP transport (UnityWebRequest).
using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Golfin.Net
{
    /// <summary>
    /// Shipping <see cref="IHttpTransport"/>. Mirrors the transport discipline already proven in
    /// <c>SupabaseAuthClient</c>: explicit verb, raw upload handler, buffered download handler, and a
    /// per-request timeout, with connection/data-processing failures reported as such rather than as a
    /// status code (UnityWebRequest leaves <c>responseCode</c> at 0 for those).
    ///
    /// This class does NOT retry, unwrap, or attach auth — <see cref="ApiClient"/> owns all of that, so
    /// the retry/401 logic is covered by tests against a fake transport instead of the network.
    /// </summary>
    public sealed class UnityWebRequestTransport : IHttpTransport
    {
        public IEnumerator Send(HttpRequest request, Action<HttpResponse> onResponse)
        {
            if (request == null || string.IsNullOrEmpty(request.Url))
            {
                onResponse?.Invoke(HttpResponse.ConnectionFailure("No URL supplied."));
                yield break;
            }

            using (var req = new UnityWebRequest(request.Url, request.Method ?? UnityWebRequest.kHttpVerbGET))
            {
                if (!string.IsNullOrEmpty(request.Body))
                    req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(request.Body));

                req.downloadHandler = new DownloadHandlerBuffer();
                req.timeout = Mathf.Max(5, request.TimeoutSeconds);

                if (!string.IsNullOrEmpty(request.ContentType))
                    req.SetRequestHeader("Content-Type", request.ContentType);

                foreach (var kv in request.Headers)
                {
                    if (string.IsNullOrEmpty(kv.Key) || kv.Value == null) continue;
                    req.SetRequestHeader(kv.Key, kv.Value);
                }

                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.ConnectionError ||
                    req.result == UnityWebRequest.Result.DataProcessingError)
                {
                    onResponse?.Invoke(HttpResponse.ConnectionFailure(req.error));
                    yield break;
                }

                onResponse?.Invoke(new HttpResponse
                {
                    StatusCode = req.responseCode,
                    Body = req.downloadHandler != null ? req.downloadHandler.text : null
                });
            }
        }
    }
}
