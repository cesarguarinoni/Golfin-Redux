#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Golfin.SceneSnapshot
{
    public sealed class ManualSceneSnapshotWindow : EditorWindow
    {
        private const string ExtraRootsKey = "Golfin.SceneSnapshot.ExtraImporterRoots";

        [MenuItem("Window/Golfin/Manual Scene Snapshot")]
        private static void Open() => GetWindow<ManualSceneSnapshotWindow>("Scene Snapshot");

        // Extra importer roots (EditorPrefs-persisted)
        private List<string> _extraRoots = new List<string>();
        private ReorderableList _extraRootsList;

        // Audit state
        private bool _auditDone;
        private string _auditedScenePath;
        private List<SceneSnapshotCapture.RootAuditEntry> _auditResults;
        private Vector2 _auditScroll;

        // Restore report
        private RestoreReport _restoreReport;
        private Vector2 _restoreScroll;

        // Help foldout
        private bool _helpOpen;

        private void OnEnable()
        {
            LoadExtraRoots();
            BuildRootsList();
            EditorSceneManager.sceneOpened += OnSceneChanged;
            EditorSceneManager.sceneSaved += OnSceneSaved;
        }

        private void OnDisable()
        {
            EditorSceneManager.sceneOpened -= OnSceneChanged;
            EditorSceneManager.sceneSaved -= OnSceneSaved;
        }

        private void OnSceneChanged(Scene s, OpenSceneMode m) { _auditDone = false; _auditResults = null; _restoreReport = null; Repaint(); }
        private void OnSceneSaved(Scene s) { Repaint(); }

        private void OnGUI()
        {
            var scene = GetActiveScene();
            string scenePath = scene.IsValid() ? scene.path : "";
            bool sceneIsSaved = !string.IsNullOrEmpty(scenePath);

            // ── Active scene
            EditorGUILayout.LabelField("Active Scene", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(sceneIsSaved ? scenePath : "(unsaved scene)", EditorStyles.helpBox);

            // ── Snapshot path
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("Snapshot File", EditorStyles.boldLabel);
            string snapPath = sceneIsSaved ? SceneSnapshotCapture.GetSnapshotPath(scenePath) : "—";
            EditorGUILayout.LabelField(snapPath, EditorStyles.helpBox);

            EditorGUILayout.Space(6);

            // ── Extra importer root names
            EditorGUILayout.LabelField("Extra Importer Root Names (skip list)", EditorStyles.boldLabel);
            _extraRootsList.DoLayoutList();

            EditorGUILayout.Space(4);

            // ── Audit button
            bool auditForCurrentScene = _auditDone && _auditedScenePath == scenePath;
            using (new EditorGUI.DisabledScope(!sceneIsSaved))
            {
                if (GUILayout.Button("Run Audit"))
                {
                    RunAudit(scene, scenePath);
                }
            }

            if (_auditResults != null && _auditResults.Count > 0)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Audit Results", EditorStyles.boldLabel);
                _auditScroll = EditorGUILayout.BeginScrollView(_auditScroll, GUILayout.Height(120));
                foreach (var entry in _auditResults)
                {
                    Color orig = GUI.color;
                    GUI.color = entry.Result == SceneSnapshotCapture.ClassificationResult.Manual
                        ? new Color(0.6f, 1f, 0.6f) : new Color(1f, 0.85f, 0.5f);
                    EditorGUILayout.LabelField($"  [{entry.Result}]  {entry.Name}  — {entry.MatchedRule}");
                    GUI.color = orig;
                }
                EditorGUILayout.EndScrollView();
            }

            EditorGUILayout.Space(4);

            // ── Capture button (disabled until audit done for current scene)
            using (new EditorGUI.DisabledScope(!sceneIsSaved || !auditForCurrentScene))
            {
                if (GUILayout.Button("Capture"))
                    DoCapture(scene, scenePath);
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            // ── Snapshot summary
            bool snapExists = sceneIsSaved && File.Exists(snapPath);
            EditorGUILayout.LabelField("Snapshot Summary", EditorStyles.boldLabel);
            if (snapExists)
            {
                try
                {
                    var existing = JsonUtility.FromJson<SceneSnapshotData>(File.ReadAllText(snapPath));
                    if (existing != null)
                    {
                        EditorGUILayout.LabelField($"  Captured: {existing.CapturedAtIso}");
                        EditorGUILayout.LabelField($"  Props: {existing.Props.Count}");
                        if (existing.Terrain != null)
                        {
                            EditorGUILayout.LabelField($"  Terrain trees: {existing.Terrain.TreeInstances.Count}");
                            EditorGUILayout.LabelField($"  Detail layers: {existing.Terrain.DetailLayers.Count}");
                        }
                        else
                        {
                            EditorGUILayout.LabelField("  No terrain snapshot.");
                        }
                    }
                }
                catch { EditorGUILayout.HelpBox("Snapshot file exists but could not be parsed.", MessageType.Warning); }
            }
            else
            {
                EditorGUILayout.LabelField("  No snapshot file found.", EditorStyles.miniLabel);
            }

            EditorGUILayout.Space(4);

            // ── Restore button
            using (new EditorGUI.DisabledScope(!snapExists))
            {
                if (GUILayout.Button("Restore"))
                    DoRestore(scene, scenePath);
            }

            // ── Restore report
            if (_restoreReport != null)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Restore Report", EditorStyles.boldLabel);
                _restoreScroll = EditorGUILayout.BeginScrollView(_restoreScroll, GUILayout.Height(100));
                DrawReportSection("Updated", _restoreReport.Updated, new Color(0.6f, 1f, 0.6f));
                DrawReportSection("Created", _restoreReport.Created, new Color(0.6f, 0.8f, 1f));
                DrawReportSection("Skipped", _restoreReport.Skipped, new Color(0.9f, 0.9f, 0.5f));
                DrawReportSection("Failed",  _restoreReport.Failed,  new Color(1f, 0.5f, 0.5f));
                EditorGUILayout.EndScrollView();
            }

            EditorGUILayout.Space(6);

            // ── Help foldout
            _helpOpen = EditorGUILayout.Foldout(_helpOpen, "Help", true);
            if (_helpOpen)
            {
                EditorGUILayout.HelpBox(
                    "Workflow:\n" +
                    "1. Before regenerating a hole: Run Audit, then Capture.\n" +
                    "2. Regenerate the scene via the importer.\n" +
                    "3. Open the scene, open this window, click Restore.\n\n" +
                    "Restore merges by GUID — existing objects are updated in-place, " +
                    "missing prefabs are re-instantiated. Objects added after capture are left alone.\n\n" +
                    "Note: root-level props store world position/scale. If you re-parent a root " +
                    "prop under a different parent, drift may occur on restore.",
                    MessageType.None);
            }
        }

        // ─── Operations ────────────────────────────────────────────────

        private void RunAudit(Scene scene, string scenePath)
        {
            _auditResults = SceneSnapshotCapture.AuditRoots(scene, _extraRoots);
            _auditDone = true;
            _auditedScenePath = scenePath;
            Repaint();
        }

        private void DoCapture(Scene scene, string scenePath)
        {
            var snap = SceneSnapshotCapture.Capture(scene, _extraRoots);

            if (SceneSnapshotCapture.WouldWipeSnapshot(snap, scenePath))
            {
                bool proceed = EditorUtility.DisplayDialog(
                    "Capture would erase snapshot",
                    "The current capture has no manual props, but your existing snapshot is non-empty. " +
                    "This would wipe your saved content. Proceed?",
                    "Proceed", "Cancel");
                if (!proceed) return;
            }

            SceneSnapshotCapture.Save(snap, scenePath);
            Debug.Log($"[SceneSnapshot] Captured {snap.Props.Count} props to {SceneSnapshotCapture.GetSnapshotPath(scenePath)}");
            Repaint();
        }

        private void DoRestore(Scene scene, string scenePath)
        {
            SceneSnapshotData snap;
            try { snap = SceneSnapshotRestore.Load(scenePath); }
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog("Snapshot Error", ex.Message, "OK");
                return;
            }

            if (snap == null)
            {
                EditorUtility.DisplayDialog("No Snapshot", $"No snapshot found at:\n{SceneSnapshotCapture.GetSnapshotPath(scenePath)}", "OK");
                return;
            }

            _restoreReport = SceneSnapshotRestore.Restore(scene, snap);
            Repaint();
        }

        // ─── Extra roots persistence ──────────────────────────────────

        private void LoadExtraRoots()
        {
            _extraRoots = new List<string>();
            string raw = EditorPrefs.GetString(ExtraRootsKey, "");
            if (!string.IsNullOrEmpty(raw))
                _extraRoots.AddRange(raw.Split('|').Where(s => !string.IsNullOrEmpty(s)));
        }

        private void SaveExtraRoots()
        {
            EditorPrefs.SetString(ExtraRootsKey, string.Join("|", _extraRoots));
        }

        private void BuildRootsList()
        {
            _extraRootsList = new ReorderableList(_extraRoots, typeof(string), true, true, true, true);

            _extraRootsList.drawHeaderCallback = rect =>
                EditorGUI.LabelField(rect, "Root Names to Skip (importer-generated)");

            _extraRootsList.drawElementCallback = (rect, index, active, focused) =>
            {
                rect.y += 2;
                rect.height = EditorGUIUtility.singleLineHeight;
                string before = _extraRoots[index];
                string after = EditorGUI.TextField(rect, before);
                if (after != before)
                {
                    _extraRoots[index] = after;
                    SaveExtraRoots();
                    _auditDone = false; // invalidate audit
                }
            };

            _extraRootsList.onAddCallback = list =>
            {
                _extraRoots.Add("");
                SaveExtraRoots();
                _auditDone = false;
            };

            _extraRootsList.onRemoveCallback = list =>
            {
                _extraRoots.RemoveAt(list.index);
                SaveExtraRoots();
                _auditDone = false;
            };
        }

        // ─── Helpers ─────────────────────────────────────────────────

        private static Scene GetActiveScene()
        {
            // Prefer the active scene in the editor
            var scene = SceneManager.GetActiveScene();
            return scene;
        }

        private static void DrawReportSection(string label, List<ReportEntry> entries, Color col)
        {
            if (entries.Count == 0) return;
            Color orig = GUI.color;
            GUI.color = col;
            EditorGUILayout.LabelField($"{label} ({entries.Count}):", EditorStyles.boldLabel);
            GUI.color = orig;
            foreach (var e in entries)
            {
                string line = string.IsNullOrEmpty(e.Reason)
                    ? $"  {e.Name} [{e.Guid}]"
                    : $"  {e.Name} [{e.Guid}] — {e.Reason}";
                EditorGUILayout.LabelField(line, EditorStyles.miniLabel);
            }
        }
    }
}
#endif
