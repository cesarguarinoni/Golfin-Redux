#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;
using UnityEditor.SceneManagement;
using UnityEngine;
using Golfin.CourseImport;

namespace Golfin.CourseImport.Recording
{
    [InitializeOnLoad]
    public static class HoleFlyoverRecorder
    {
        // --- Tunable constants ---
        private const float FlyoverDurationSeconds = 20f;
        private const float DroneStartHeight  = 25f;  // m above terrain
        private const float DroneStartBackset = 15f;  // m behind tee
        private const float DroneTeeHeight    = 6f;   // at zoom-in apex
        private const float DroneCruiseHeight = 12f;  // m above terrain while cruising
        private const float DronePinHeight    = 4f;
        private const float CameraFov         = 55f;
        private const int   OutputWidth       = 1284;  // iPhone 12 Pro Max portrait
        private const int   OutputHeight      = 2778;
        private const int   OutputFrameRate   = 60;
        private const string FlyoverCamTag    = "FlyoverCam";

        // --- State machine ---
        private enum RecState { Idle, WaitingForPlayMode, Recording, WaitingForEditMode }

        private static RecState          _state = RecState.Idle;
        private static readonly Queue<int> _holeQueue = new Queue<int>();
        private static bool              _cancelRequested;
        private static int               _currentHole;
        private static double            _recordStartTime;

        // Per-recording (non-persistent — reset each hole)
        private static GameObject        _flyoverCamGO;
        private static RecorderController _recorderController;
        private static FlyoverKeyframe[] _keyframes;
        private static Terrain           _terrain;
        private static float             _terrainBaseY;

        // SessionState keys (survive domain reloads)
        private const string SK_QUEUE    = "HoleFlyoverRec.Queue";
        private const string SK_CURHOLE  = "HoleFlyoverRec.CurrentHole";
        private const string SK_BATCHING = "HoleFlyoverRec.Batching";
        private const string SK_ACTIVE   = "HoleFlyoverRec.Active";
        private const string SK_STATE    = "HoleFlyoverRec.State"; // persists _state across domain reload

        // ---------------------------------------------------------------
        // Boot / domain-reload recovery
        // ---------------------------------------------------------------

        static HoleFlyoverRecorder()
        {
            EditorApplication.update -= OnUpdate;
            EditorApplication.update += OnUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;

            TryResumeAfterReload();
        }

        private static void TryResumeAfterReload()
        {
            if (!SessionState.GetBool(SK_ACTIVE, false)) return;

            // Restore current hole and state so OnPlayModeChanged(EnteredPlayMode) fires correctly.
            _currentHole = SessionState.GetInt(SK_CURHOLE, 0);
            var savedState = (RecState)SessionState.GetInt(SK_STATE, (int)RecState.Idle);

            if (savedState == RecState.WaitingForPlayMode)
            {
                // Domain reload happened mid-play-mode-entry. Restore so EnteredPlayMode picks it up.
                _state = RecState.WaitingForPlayMode;
                Debug.Log($"[HoleFlyoverRecorder] Restored WaitingForPlayMode for hole {_currentHole} after domain reload.");
                return;
            }

            // Recording was interrupted mid-recording (rare). Skip hole and continue batch.
            bool batching = SessionState.GetBool(SK_BATCHING, false);
            if (!batching)
            {
                SessionState.SetBool(SK_ACTIVE, false);
                return;
            }

            string queueJson = SessionState.GetString(SK_QUEUE, "");
            if (!string.IsNullOrEmpty(queueJson))
            {
                int[] remaining = JsonHelper.FromJsonWrapped<int>(queueJson);
                if (remaining != null)
                    foreach (int h in remaining) _holeQueue.Enqueue(h);
            }

            if (_holeQueue.Count == 0) { FinishBatch(); return; }

            Debug.Log($"[HoleFlyoverRecorder] Resuming batch after domain reload — {_holeQueue.Count} holes remaining.");
            EditorApplication.delayCall += StartNextHole;
        }

        // ---------------------------------------------------------------
        // Menu items
        // ---------------------------------------------------------------

        [MenuItem("Golfin/Recording/Record Current Hole Flyover")]
        private static void MenuRecordCurrent()
        {
            if (_state != RecState.Idle)
            {
                EditorUtility.DisplayDialog("Recording", "A recording is already in progress.", "OK");
                return;
            }
            var meta = FindHoleMetadata();
            if (meta == null)
            {
                EditorUtility.DisplayDialog("Recording Error",
                    "No HoleMetadata found in current scene.\nOpen a Hole_XX_Geo scene first.", "OK");
                return;
            }
            _cancelRequested = false;
            _holeQueue.Clear();
            _holeQueue.Enqueue(meta.holeNumber);
            SessionState.SetBool(SK_BATCHING, false);
            StartNextHole();
        }

        [MenuItem("Golfin/Recording/Record All 18 Holes")]
        private static void MenuRecordAll()
        {
            if (_state != RecState.Idle)
            {
                EditorUtility.DisplayDialog("Recording", "A recording is already in progress.", "OK");
                return;
            }
            _cancelRequested = false;
            _holeQueue.Clear();
            for (int h = 1; h <= 18; h++) _holeQueue.Enqueue(h);
            SessionState.SetBool(SK_BATCHING, true);
            PersistQueue();
            StartNextHole();
        }

        [MenuItem("Golfin/Recording/Cancel Recording Queue")]
        private static void MenuCancel()
        {
            _cancelRequested = true;
            SessionState.SetBool(SK_ACTIVE, false);
            Debug.Log("[HoleFlyoverRecorder] Cancel requested — will stop after current hole finishes.");
        }

        // ---------------------------------------------------------------
        // State machine
        // ---------------------------------------------------------------

        private static void StartNextHole()
        {
            if (_cancelRequested || _holeQueue.Count == 0)
            {
                FinishBatch();
                return;
            }

            _currentHole = _holeQueue.Dequeue();
            PersistQueue();
            SessionState.SetInt(SK_CURHOLE, _currentHole);
            SessionState.SetBool(SK_ACTIVE, true);

            if (SessionState.GetBool(SK_BATCHING, false))
            {
                string scenePath = GetVideoScenePath(_currentHole);
                string fullPath  = Path.Combine(Application.dataPath, "..", scenePath).Replace('\\', '/');
                if (!File.Exists(fullPath))
                {
                    Debug.LogWarning($"[HoleFlyoverRecorder] Scene not found: {scenePath} — skipping hole {_currentHole}");
                    StartNextHole();
                    return;
                }
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                // Wait one editor tick for the scene to settle before entering Play Mode
                EditorApplication.delayCall += EnterPlayMode;
            }
            else
            {
                EnterPlayMode();
            }
        }

        private static void EnterPlayMode()
        {
            _state = RecState.WaitingForPlayMode;
            // Persist state + hole so domain reload (which clears static fields) doesn't lose them.
            SessionState.SetInt(SK_STATE, (int)RecState.WaitingForPlayMode);
            SessionState.SetInt(SK_CURHOLE, _currentHole);
            EditorApplication.isPlaying = true;
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode && _state == RecState.WaitingForPlayMode)
            {
                _state = RecState.Recording;
                SessionState.SetInt(SK_STATE, (int)RecState.Recording);
                BeginRecording();
            }
            else if (change == PlayModeStateChange.EnteredEditMode && _state == RecState.WaitingForEditMode)
            {
                if (!_cancelRequested && _holeQueue.Count > 0)
                    EditorApplication.delayCall += StartNextHole;
                else
                    FinishBatch();
            }
        }

        private static void OnUpdate()
        {
            if (_state != RecState.Recording) return;

            float elapsed = (float)(EditorApplication.timeSinceStartup - _recordStartTime);
            float t       = Mathf.Clamp01(elapsed / FlyoverDurationSeconds);

            // Always drive the camera — independent of whether the recorder is running.
            UpdateCameraFromPath(t);

            if (_recorderController != null && _recorderController.IsRecording())
                EditorUtility.DisplayProgressBar("Recording Flyover",
                    $"Hole {_currentHole:D2} — {Mathf.RoundToInt(t * 100)}%", t);

            if (elapsed >= FlyoverDurationSeconds)
            {
                StopAndCleanup();
                _state = RecState.WaitingForEditMode;
                SessionState.SetInt(SK_STATE, (int)RecState.WaitingForEditMode);
                EditorApplication.isPlaying = false;
            }
        }

        // ---------------------------------------------------------------
        // Recording setup / teardown
        // ---------------------------------------------------------------

        private static void BeginRecording()
        {
            _terrain = UnityEngine.Object.FindObjectOfType<Terrain>();
            if (_terrain == null)
            {
                Debug.LogError($"[HoleFlyoverRecorder] No Terrain found — aborting hole {_currentHole}");
                AbortCurrentHole(); return;
            }
            _terrainBaseY = _terrain.transform.position.y;

            var meta = FindHoleMetadata();
            if (meta == null)
            {
                Debug.LogError("[HoleFlyoverRecorder] No HoleMetadata in scene — aborting");
                AbortCurrentHole(); return;
            }

            string exportPath = GetExportPath(meta.courseId, meta.holeNumber);
            _keyframes = BuildFlyoverPath(exportPath, _terrain, _terrainBaseY);
            if (_keyframes == null || _keyframes.Length == 0)
            {
                Debug.LogError($"[HoleFlyoverRecorder] Failed to build camera path for hole {meta.holeNumber}");
                AbortCurrentHole(); return;
            }

            // Disable WalkCamera so FlyoverCamera becomes the active rendered view.
            var walkCam = UnityEngine.Object.FindObjectOfType<WalkCamera>();
            if (walkCam != null) walkCam.GetComponent<Camera>().enabled = false;

            // Create flyover camera with higher depth so it renders on top.
            EnsureTag(FlyoverCamTag);
            _flyoverCamGO = new GameObject("FlyoverCamera");
            _flyoverCamGO.tag = FlyoverCamTag;
            var cam = _flyoverCamGO.AddComponent<Camera>();
            cam.fieldOfView   = CameraFov;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane  = 3000f;
            cam.depth         = 10f; // render above any existing cameras
            // No AudioListener here — WalkCamera already has one

            UpdateCameraFromPath(0f);

            // Output dir
            string recordingsDir = Path.Combine(Application.dataPath, "..", "Recordings");
            Directory.CreateDirectory(recordingsDir);

            // Recorder settings
            var ctrlSettings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            ctrlSettings.SetRecordModeToManual();
            ctrlSettings.FrameRate = OutputFrameRate;
            ctrlSettings.FrameRatePlayback = FrameRatePlayback.Constant;
            ctrlSettings.CapFrameRate = true;
            ctrlSettings.ExitPlayMode = false; // we control Play Mode exit ourselves

            var movieSettings = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movieSettings.Enabled = true;
            movieSettings.OutputFormat = MovieRecorderSettings.VideoRecorderOutputFormat.MP4;
            movieSettings.CaptureAlpha = false;
            movieSettings.OutputFile = Path.Combine(recordingsDir, $"hole-{meta.holeNumber:D2}");

            var camInput = new CameraInputSettings();
            camInput.Source      = ImageSource.TaggedCamera;
            camInput.CameraTag   = FlyoverCamTag;
            camInput.OutputWidth  = OutputWidth;
            camInput.OutputHeight = OutputHeight;
            movieSettings.ImageInputSettings = camInput;

            ctrlSettings.AddRecorderSettings(movieSettings);

            _recorderController = new RecorderController(ctrlSettings);
            _recorderController.PrepareRecording();
            _recorderController.StartRecording();

            _recordStartTime = EditorApplication.timeSinceStartup;
            Debug.Log($"[HoleFlyoverRecorder] Recording hole {meta.holeNumber:D2} → {movieSettings.OutputFile}.mp4");
        }

        private static void StopAndCleanup()
        {
            try { _recorderController?.StopRecording(); } catch { }
            _recorderController = null;

            if (_flyoverCamGO != null)
                UnityEngine.Object.DestroyImmediate(_flyoverCamGO);
            _flyoverCamGO = null;
            _keyframes = null;

            // Re-enable WalkCamera if it was disabled.
            var walkCam = UnityEngine.Object.FindObjectOfType<WalkCamera>();
            if (walkCam != null) walkCam.GetComponent<Camera>().enabled = true;

            EditorUtility.ClearProgressBar();
        }

        private static void AbortCurrentHole()
        {
            StopAndCleanup();
            _state = RecState.WaitingForEditMode;
            EditorApplication.isPlaying = false;
        }

        private static void FinishBatch()
        {
            _state = RecState.Idle;
            _cancelRequested = false;
            _holeQueue.Clear();
            SessionState.SetBool(SK_ACTIVE, false);
            SessionState.SetBool(SK_BATCHING, false);
            EditorUtility.ClearProgressBar();
            Debug.Log("[HoleFlyoverRecorder] All recordings complete.");
        }

        // ---------------------------------------------------------------
        // Camera path
        // ---------------------------------------------------------------

        private struct FlyoverKeyframe
        {
            public Vector3 camPos;
            public Vector3 lookAtPos;
            public float   normalizedT;
        }

        private static FlyoverKeyframe[] BuildFlyoverPath(
            string exportPath, Terrain terrain, float terrainBaseY)
        {
            // --- Load data ---
            string anchorsPath  = Path.Combine(exportPath, "anchors.json");
            string greensPath   = Path.Combine(exportPath, "greens.json");
            string fairwayPath  = Path.Combine(exportPath, "fairway-contours.json");

            if (!File.Exists(anchorsPath) || !File.Exists(greensPath))
            {
                Debug.LogWarning($"[HoleFlyoverRecorder] Missing anchors/greens json at: {exportPath}");
                return null;
            }

            AnchorData[] anchors = JsonHelper.FromJsonFile<AnchorData>(anchorsPath);
            var greensFile       = JsonUtility.FromJson<GreensFileData>(File.ReadAllText(greensPath));

            Vector3? backTeePos = null;

            // Find back tee anchor
            var backAnchor = anchors?.FirstOrDefault(a => a.type != null && a.type.Contains("back"));
            if (backAnchor != null)
            {
                float y = terrain.SampleHeight(new Vector3(backAnchor.local.x, 0, backAnchor.local.z)) + terrainBaseY;
                backTeePos = new Vector3(backAnchor.local.x, y, backAnchor.local.z);
            }

            // Fallback: tee farthest from green centroid
            var green = greensFile?.greens?.FirstOrDefault();
            Vector3 greenPos = Vector3.zero;
            if (green != null)
            {
                float gy = terrain.SampleHeight(new Vector3(green.center_local.x, 0, green.center_local.z)) + terrainBaseY;
                greenPos = new Vector3(green.center_local.x, gy, green.center_local.z);
            }

            if (backTeePos == null && anchors != null)
            {
                var teeAnchors = anchors.Where(a => a.type != null && a.type.StartsWith("tee")).ToArray();
                if (teeAnchors.Length > 0 && green != null)
                {
                    var farthest = teeAnchors.OrderByDescending(a =>
                    {
                        var p = new Vector3(a.local.x, 0, a.local.z);
                        return Vector3.Distance(p, new Vector3(greenPos.x, 0, greenPos.z));
                    }).First();
                    float y = terrain.SampleHeight(new Vector3(farthest.local.x, 0, farthest.local.z)) + terrainBaseY;
                    backTeePos = new Vector3(farthest.local.x, y, farthest.local.z);
                }
            }

            if (backTeePos == null)
            {
                Debug.LogWarning("[HoleFlyoverRecorder] Could not determine back tee position.");
                return null;
            }

            Vector3 teePos = backTeePos.Value;
            Vector3 fwdXZ  = new Vector3(greenPos.x - teePos.x, 0, greenPos.z - teePos.z).normalized;
            if (fwdXZ == Vector3.zero) fwdXZ = Vector3.forward;

            // Fairway centroids sorted by distance from tee.
            List<Vector3> cruiseWaypoints = new List<Vector3>();
            cruiseWaypoints.Add(teePos);

            if (File.Exists(fairwayPath))
            {
                var fw = JsonUtility.FromJson<FairwayContoursFile>(File.ReadAllText(fairwayPath));
                if (fw?.fairways != null)
                {
                    var centroids = fw.fairways
                        .Where(r => r.center_local != null)
                        .Select(r =>
                        {
                            float cy = terrain.SampleHeight(new Vector3(r.center_local.x, 0, r.center_local.z)) + terrainBaseY;
                            return new Vector3(r.center_local.x, cy, r.center_local.z);
                        })
                        .OrderBy(p => Vector3.Distance(p, teePos))
                        .ToList();
                    cruiseWaypoints.AddRange(centroids);
                }
            }
            cruiseWaypoints.Add(greenPos);

            // --- Build keyframes at 60 fps ---
            int totalFrames = Mathf.RoundToInt(FlyoverDurationSeconds * OutputFrameRate);
            var frames = new FlyoverKeyframe[totalFrames];

            for (int i = 0; i < totalFrames; i++)
            {
                float t = (float)i / (totalFrames - 1);
                Vector3 pos, lookAt;

                // Cruise ends 15m before the green for a natural orbit entry.
                cruiseWaypoints[cruiseWaypoints.Count - 1] = greenPos - fwdXZ * 15f;

                float yStart = teePos.y  + DroneStartHeight;
                float yMid   = teePos.y  + DroneTeeHeight;
                float yEnd   = greenPos.y + DronePinHeight;

                // 1-second pause before the arc begins (t=0..0.05).
                // Uses lt=0 of the arc so position/lookAt are identical when movement starts.
                const float kPause  = 0.05f; // 1s at 20s total
                const float kOrbit  = 0.12f;
                const float kArcEnd = 1f - kOrbit; // 0.95

                // Shared arc evaluator — used by both the pause (lt=0) and the moving arc.
                System.Func<float, (Vector3, Vector3)> evalArc = (float lt) =>
                {
                    float yNorm = Mathf.Clamp01(lt / 0.20f);
                    float ySS   = yNorm * yNorm * (3f - 2f * yNorm);
                    float cy    = lt < 0.20f
                        ? Mathf.Lerp(yStart, yMid, ySS)
                        : Mathf.Lerp(yMid, yEnd, (lt - 0.20f) / 0.80f);
                    Vector3 xz  = CatmullRomXZ(cruiseWaypoints, lt);
                    Vector3 p   = new Vector3(xz.x, cy, xz.z);

                    float tl    = Mathf.Min(lt + 0.03f, 1f);
                    Vector3 lxz = CatmullRomXZ(cruiseWaypoints, tl);
                    float ly    = tl < 0.20f
                        ? Mathf.Lerp(yStart, yMid, Mathf.Clamp01(tl / 0.20f))
                        : Mathf.Lerp(yMid, yEnd, (tl - 0.20f) / 0.80f);
                    Vector3 la  = new Vector3(lxz.x, ly, lxz.z);
                    return (p, la);
                };

                Vector3 flagLookAt = greenPos + Vector3.up * 0.5f;

                if (t < kPause)
                {
                    // 1s pause: directly above tee, looking straight down.
                    pos    = teePos + new Vector3(0f, DroneStartHeight, 0f);
                    lookAt = teePos;
                }
                else if (t < kArcEnd)
                {
                    float lt = (t - kPause) / (kArcEnd - kPause);
                    (pos, lookAt) = evalArc(lt);

                    // Start of arc: blend lookAt from straight-down into forward arc direction.
                    if (lt < 0.15f)
                    {
                        float b = lt / 0.15f;
                        b = b * b * (3f - 2f * b); // smoothstep
                        lookAt = Vector3.Lerp(teePos, lookAt, b);
                    }
                    // End of arc: blend lookAt toward the flag so we arrive pointing at it.
                    else if (lt > 0.80f)
                    {
                        float b = (lt - 0.80f) / 0.20f;
                        b = b * b * (3f - 2f * b);
                        lookAt = Vector3.Lerp(lookAt, flagLookAt, b);
                    }
                }
                else
                {
                    // Orbit around pin. Base direction is -fwdXZ (tee side of green) so
                    // orbit starts exactly where the cruise ends — no positional jump.
                    float lt    = (t - kArcEnd) / kOrbit;
                    float angle = Mathf.Lerp(-15f, 15f, lt);
                    Vector3 offset = Quaternion.Euler(0, angle, 0) * (-fwdXZ) * 15f;
                    pos    = greenPos + new Vector3(offset.x, DronePinHeight, offset.z);
                    lookAt = flagLookAt;
                }

                frames[i] = new FlyoverKeyframe { camPos = pos, lookAtPos = lookAt, normalizedT = t };
            }

            return frames;
        }

        private static void UpdateCameraFromPath(float t)
        {
            if (_flyoverCamGO == null || _keyframes == null || _keyframes.Length == 0) return;

            int idx  = Mathf.Clamp(Mathf.RoundToInt(t * (_keyframes.Length - 1)), 0, _keyframes.Length - 1);
            var kf   = _keyframes[idx];

            _flyoverCamGO.transform.position = kf.camPos;
            Vector3 dir = (kf.lookAtPos - kf.camPos);
            if (dir.sqrMagnitude > 0.0001f)
                _flyoverCamGO.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }

        // Catmull-Rom spline evaluated on XZ, returns an XZ position (Y from terrain caller)
        private static Vector3 CatmullRomXZ(List<Vector3> pts, float t)
        {
            int n = pts.Count;
            if (n == 0) return Vector3.zero;
            if (n == 1) return pts[0];
            if (n == 2) return Vector3.Lerp(pts[0], pts[1], t);

            // Map t to segment
            float segCount = n - 1;
            float ft = t * segCount;
            int   si = Mathf.Clamp((int)ft, 0, n - 2);
            float st = ft - si;

            // Catmull-Rom control points (clamp endpoints)
            Vector3 p0 = pts[Mathf.Max(si - 1, 0)];
            Vector3 p1 = pts[si];
            Vector3 p2 = pts[Mathf.Min(si + 1, n - 1)];
            Vector3 p3 = pts[Mathf.Min(si + 2, n - 1)];

            float st2 = st * st;
            float st3 = st2 * st;

            float x = 0.5f * ((2f * p1.x) + (-p0.x + p2.x) * st
                              + (2f * p0.x - 5f * p1.x + 4f * p2.x - p3.x) * st2
                              + (-p0.x + 3f * p1.x - 3f * p2.x + p3.x) * st3);
            float z = 0.5f * ((2f * p1.z) + (-p0.z + p2.z) * st
                              + (2f * p0.z - 5f * p1.z + 4f * p2.z - p3.z) * st2
                              + (-p0.z + 3f * p1.z - 3f * p2.z + p3.z) * st3);

            return new Vector3(x, 0f, z);
        }

        // ---------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------

        private static string GetExportPath(string courseId, int holeNumber)
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            return Path.Combine(projectRoot, "Tools", "UHoleGeo", "output",
                courseId, "export", $"hole-{holeNumber:D2}");
        }

        private static string GetVideoScenePath(int holeNumber) =>
            $"Assets/Golf/Courses/lomond-country-club/Generated/Video/Hole_{holeNumber:D2}_Geo.unity";

        private static HoleMetadata FindHoleMetadata() =>
            UnityEngine.Object.FindObjectOfType<HoleMetadata>();

        private static void PersistQueue()
        {
            int[] arr = _holeQueue.ToArray();
            SessionState.SetString(SK_QUEUE, JsonHelper.ToJsonWrapped(arr));
        }

        private static void EnsureTag(string tag)
        {
            var tagManager  = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var tagsProp = tagManager.FindProperty("tags");
            for (int i = 0; i < tagsProp.arraySize; i++)
                if (tagsProp.GetArrayElementAtIndex(i).stringValue == tag) return;
            tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
            tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue = tag;
            tagManager.ApplyModifiedProperties();
        }
    }

    // Minimal JSON array helper (JsonUtility can't deserialize root arrays)
    internal static class JsonHelper
    {
        [Serializable] private class Wrapper<T> { public T[] items; }

        // Deserialize a raw JSON array e.g. [{...},{...}]
        public static T[] FromJson<T>(string json)
        {
            try
            {
                string wrapped = "{\"items\":" + json + "}";
                return JsonUtility.FromJson<Wrapper<T>>(wrapped)?.items;
            }
            catch { return null; }
        }

        // Deserialize the wrapped format stored by ToJsonWrapped
        public static T[] FromJsonWrapped<T>(string json)
        {
            try { return JsonUtility.FromJson<Wrapper<T>>(json)?.items; }
            catch { return null; }
        }

        public static T[] FromJsonFile<T>(string path)
        {
            try { return FromJson<T>(File.ReadAllText(path)); }
            catch { return null; }
        }

        // Serialize to wrapped format {"items":[...]}
        public static string ToJsonWrapped<T>(T[] arr)
        {
            var w = new Wrapper<T> { items = arr };
            return JsonUtility.ToJson(w);
        }
    }
}
#endif
