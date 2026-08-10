#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Golfin.Physics.Viewer.Editor
{
    /// <summary>
    /// Save/restore the Editor scene setup around a capture run.
    ///
    /// Every capture launcher in this folder stages its own hierarchy (LabScaffold single +
    /// Hole_NN_Geo additive) before entering play mode. Without a restore on the way out,
    /// that staged hierarchy simply BECOMES the editor hierarchy — which is why Hole_06_Geo
    /// "kept reappearing" (Cesar, 2026-08-05). It is not only clutter: a leftover hole scene
    /// makes ScanForLoadedHoleSceneAtStartup wire the lab to the WRONG HOLE on the next play
    /// run that does not pre-clean (see SmokeRunner2fMenu's defensive sweep).
    ///
    /// Usage:
    ///   Capture(Key)  — call ONCE, BEFORE the first OpenScene of the run.
    ///   Restore(Key)  — call from the launcher's EnteredEditMode handler, after disarming.
    ///
    /// The setup is persisted as JSON in SessionState (NOT EditorPrefs — it must not leak
    /// across projects or sessions) so it survives the domain reloads into and out of play mode.
    ///
    /// Hole scenes are closed WITHOUT saving. Hole_NN_Geo is generated content with no merge
    /// driver — nothing in this file may ever write one.
    /// </summary>
    public static class CaptureSceneSetup
    {
        [System.Serializable]
        class Entry
        {
            public string path;
            public bool   isLoaded;
            public bool   isActive;
            public bool   isSubScene;
        }

        [System.Serializable]
        class Payload
        {
            public List<Entry> entries = new List<Entry>();
        }

        /// <summary>
        /// Snapshot the current Editor scene setup under <paramref name="sessionKey"/>.
        /// Call BEFORE any OpenScene. A setup containing an unsaved/untitled scene (empty path)
        /// cannot be restored by Unity, so it is not recorded and the run leaves the hierarchy
        /// staged (logged) rather than throwing on the way out.
        /// </summary>
        public static void Capture(string sessionKey)
        {
            var setup = EditorSceneManager.GetSceneManagerSetup();
            if (setup == null || setup.Length == 0)
            {
                SessionState.EraseString(sessionKey);
                return;
            }

            var payload = new Payload();
            foreach (var s in setup)
            {
                // Filter Hole_NN_Geo entries FIRST — they are staged content by definition and
                // must never be recorded as "user's pre-run setup" (that is the resurrection bug).
                if (IsHoleGeoScene(s.path))
                {
                    Debug.Log($"[CaptureSceneSetup] Excluding staged hole scene from snapshot: {s.path}");
                    continue;
                }

                if (string.IsNullOrEmpty(s.path))
                {
                    Debug.LogWarning("[CaptureSceneSetup] Current scene setup contains an unsaved/untitled " +
                                     "scene — cannot snapshot it. The capture run will leave its staged " +
                                     "hierarchy open instead of restoring.");
                    SessionState.EraseString(sessionKey);
                    return;
                }

                payload.entries.Add(new Entry
                {
                    path       = s.path,
                    isLoaded   = s.isLoaded,
                    isActive   = s.isActive,
                    isSubScene = s.isSubScene
                });
            }

            // If filtering left zero entries (user had only hole scenes open), erase and log.
            if (payload.entries.Count == 0)
            {
                SessionState.EraseString(sessionKey);
                Debug.Log("[CaptureSceneSetup] All scene entries were staged hole scenes — nothing to snapshot.");
                return;
            }

            SessionState.SetString(sessionKey, JsonUtility.ToJson(payload));
            Debug.Log($"[CaptureSceneSetup] Snapshot taken ({payload.entries.Count} scene(s)): " +
                      string.Join(", ", payload.entries.ConvertAll(e => System.IO.Path.GetFileNameWithoutExtension(e.path))));
        }

        /// <summary>
        /// Close every staged Hole_NN_Geo scene WITHOUT saving, then restore the setup snapshotted
        /// by <see cref="Capture"/> and clear the snapshot. Safe to call when no snapshot exists.
        /// </summary>
        public static void Restore(string sessionKey)
        {
            // Close staged hole scenes first, explicitly without saving, so RestoreSceneManagerSetup
            // can never be asked to persist generated geometry.
            CloseStagedHoleScenes();

            string json = SessionState.GetString(sessionKey, "");
            if (string.IsNullOrEmpty(json))
            {
                Debug.Log("[CaptureSceneSetup] No scene-setup snapshot to restore (staged hole scenes were still closed).");
                return;
            }
            SessionState.EraseString(sessionKey);

            Payload payload;
            try { payload = JsonUtility.FromJson<Payload>(json); }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[CaptureSceneSetup] Could not parse scene-setup snapshot: {e.Message}");
                return;
            }
            if (payload == null || payload.entries == null || payload.entries.Count == 0) return;

            var setup = new List<SceneSetup>();
            foreach (var e in payload.entries)
            {
                if (string.IsNullOrEmpty(e.path)) continue;

                // Defence-in-depth: skip any hole scenes that snuck into a stale pre-fix snapshot.
                if (IsHoleGeoScene(e.path))
                {
                    Debug.Log($"[CaptureSceneSetup] Skipping stale hole scene entry in snapshot: {e.path}");
                    continue;
                }

                if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(e.path) == null)
                {
                    Debug.LogWarning($"[CaptureSceneSetup] Snapshotted scene no longer exists: {e.path} — skipping restore.");
                    return;
                }
                setup.Add(new SceneSetup
                {
                    path       = e.path,
                    isLoaded   = e.isLoaded,
                    isActive   = e.isActive,
                    isSubScene = e.isSubScene
                });
            }
            if (setup.Count == 0) return;

            // RestoreSceneManagerSetup requires exactly one active scene.
            bool anyActive = setup.Exists(s => s.isActive);
            if (!anyActive) setup[0].isActive = true;

            try
            {
                EditorSceneManager.RestoreSceneManagerSetup(setup.ToArray());
                Debug.Log("[CaptureSceneSetup] Restored pre-run scene setup: " +
                          string.Join(", ", setup.ConvertAll(s => System.IO.Path.GetFileNameWithoutExtension(s.path))));
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[CaptureSceneSetup] RestoreSceneManagerSetup failed: {e.Message}");
            }
        }

        /// <summary>
        /// Close every open Hole_NN_Geo scene without saving. Never writes a generated scene.
        /// Returns how many scenes were closed.
        ///
        /// THE one implementation of "sweep staged hole scenes" in the project — the EditMode
        /// fixtures that stage hole scenes (RealHoleTerrainTests, BakedPivotRegressionTests) and
        /// StagedHoleSceneGuard all call this rather than re-rolling the loop
        /// (hole_scene_leftover_v3). <paramref name="logContext"/> only changes the log prefix so
        /// each caller is identifiable in Editor.log; the default reproduces the original message.
        /// </summary>
        public static int CloseStagedHoleScenes(string logContext = "CaptureSceneSetup")
        {
            int closed = 0;
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                var s = SceneManager.GetSceneAt(i);
                if (!s.IsValid() || string.IsNullOrEmpty(s.name)) continue;
                if (!IsHoleGeoScene(s.name)) continue;

                Debug.Log($"[{logContext}] Closing staged hole scene without saving: {s.name}");
                EditorSceneManager.CloseScene(s, true);
                closed++;
            }
            return closed;
        }

        /// <summary>
        /// Returns true when <paramref name="nameOrPath"/> identifies a generated Hole_NN_Geo scene.
        /// Works on both a scene name ("Hole_06_Geo") and a full asset path
        /// ("Assets/…/Hole_06_Geo.unity"). Null/empty inputs safely return false.
        ///
        /// Shared predicate — see <see cref="CloseStagedHoleScenes"/>.
        /// </summary>
        public static bool IsHoleGeoScene(string nameOrPath)
        {
            string n = System.IO.Path.GetFileNameWithoutExtension(nameOrPath ?? "");
            return n.StartsWith("Hole_") && n.EndsWith("_Geo");
        }

        // ── Staged-scene window (hole_scene_leftover_v3) ───────────────────────────
        //
        // A cooperative "an automated run is staging hole scenes right now" flag, so
        // StagedHoleSceneGuard can never close a scene out from under an EditMode sweep
        // that is still using it. SessionState (not EditorPrefs) so it dies with the
        // Editor session — a killed editor leaves no stale inhibit behind.
        //
        // Deliberately NOT TestRunnerApi: hooking that would drag a test-framework asmdef
        // reference into assemblies that do not have one (SPEC § Layer 2).

        const string StagedWindowKey = "Golfin.SceneHygiene.StagedSceneWindow";

        // Belt-and-braces expiry: a run that dies without reaching its teardown must not
        // inhibit the guard for the rest of the Editor session.
        const double StagedWindowMaxMinutes = 45.0;

        /// <summary>Mark the start of an automated run that stages hole scenes.</summary>
        public static void BeginStagedSceneWindow(string label)
        {
            SessionState.SetString(StagedWindowKey,
                $"{label}|{System.DateTime.UtcNow.Ticks}");
        }

        /// <summary>Mark the end of the run opened by <see cref="BeginStagedSceneWindow"/>.</summary>
        public static void EndStagedSceneWindow()
        {
            SessionState.EraseString(StagedWindowKey);
        }

        /// <summary>
        /// True while an automated run has declared it is staging hole scenes and that
        /// declaration has not expired. <paramref name="label"/> receives the declaring caller.
        /// </summary>
        public static bool IsStagedSceneWindowActive(out string label)
        {
            label = null;
            string raw = SessionState.GetString(StagedWindowKey, "");
            if (string.IsNullOrEmpty(raw)) return false;

            int bar = raw.LastIndexOf('|');
            if (bar < 0) { SessionState.EraseString(StagedWindowKey); return false; }

            if (!long.TryParse(raw.Substring(bar + 1), out long ticks))
            {
                SessionState.EraseString(StagedWindowKey);
                return false;
            }

            var age = System.DateTime.UtcNow - new System.DateTime(ticks, System.DateTimeKind.Utc);
            if (age.TotalMinutes > StagedWindowMaxMinutes)
            {
                Debug.LogWarning($"[CaptureSceneSetup] Staged-scene window from '{raw.Substring(0, bar)}' " +
                                 $"expired after {age.TotalMinutes:F0} min without a matching End — clearing it.");
                SessionState.EraseString(StagedWindowKey);
                return false;
            }

            label = raw.Substring(0, bar);
            return true;
        }

        /// <summary>
        /// Strip a serialized capture-host component of type <typeparamref name="T"/> out of the
        /// open LabScaffold scene and save the scene ONCE, clean. No-op (and no save) when there
        /// is no residue — so a launcher that never serializes its host never writes the scene.
        ///
        /// This exists for hosts that were serialized by an older build of a launcher; the
        /// current launchers inject their host at EnteredPlayMode and never save it.
        /// </summary>
        public static void StripSerializedHost<T>(string labSceneName = "LabScaffold") where T : MonoBehaviour
        {
            Scene lab = SceneManager.GetSceneByName(labSceneName);
            if (!lab.IsValid() || !lab.isLoaded) return;

            bool found = false;
            foreach (var go in lab.GetRootGameObjects())
            {
                foreach (var host in go.GetComponentsInChildren<T>(true))
                {
                    Object.DestroyImmediate(host);
                    found = true;
                }
            }
            if (!found) return;

            EditorSceneManager.MarkSceneDirty(lab);
            EditorSceneManager.SaveScene(lab);
            Debug.Log($"[CaptureSceneSetup] Stripped serialized {typeof(T).Name} residue from {labSceneName} and saved it clean.");
        }
    }
}
#endif
