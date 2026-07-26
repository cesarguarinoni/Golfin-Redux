// CourseImporterWindow — SPEC §2 (Phase 2)
// Replaces the 36 hardcoded HoleGeoImporter menu items with a single EditorWindow.
//
// Menu: GOLFIN > Course Importer
// Shortcut: GOLFIN > Course Importer — Repeat Last (re-runs the last import)
//
// The window persists the last-selected course + hole + flat flag in EditorPrefs
// so the common path is: open window → click Import (muscle-memory parity).
//
// NOTE: The old menu items (Import/Geo/*) are preserved intentionally until this
//       window is verified working on ≥ 2 holes including a Flat variant (SPEC §2
//       "Do not delete the old menu items until the window is verified working").

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Golfin.CourseImport;          // HoleGeoImporter
using Golfin.Gameplay.Loop;         // ActiveCourseContext

namespace Golfin.Editor.CourseImporter
{
    public class CourseImporterWindow : EditorWindow
    {
        // ── EditorPrefs keys ────────────────────────────────────────────────
        private const string PrefCourse  = "CourseImporterWindow.LastCourse";
        private const string PrefHole    = "CourseImporterWindow.LastHole";
        private const string PrefFlat    = "CourseImporterWindow.LastWasFlat";

        // ── State ────────────────────────────────────────────────────────────
        private List<string> _courses     = new();
        private int          _courseIndex = 0;
        private int          _holeNumber  = 1;   // 1-based, matching HoleGeoImporter API
        private bool         _useFlat     = false;
        private Vector2      _scroll;
        private string       _lastStatus  = "";

        // ── Menu items ───────────────────────────────────────────────────────

        [MenuItem("GOLFIN/Course Importer", priority = 200)]
        public static void OpenWindow()
        {
            var win = GetWindow<CourseImporterWindow>("Course Importer");
            win.minSize = new Vector2(340, 460);
            win.Show();
        }

        [MenuItem("GOLFIN/Course Importer - Repeat Last", priority = 201)]
        public static void RepeatLast()
        {
            string course  = EditorPrefs.GetString(PrefCourse, "lomond-country-club");
            int    hole    = EditorPrefs.GetInt(PrefHole,    1);
            bool   flat    = EditorPrefs.GetBool(PrefFlat,   false);

            Debug.Log($"[CourseImporterWindow] Repeating last import: {course} Hole {hole:D2} {(flat ? "(Flat)" : "(Geo)")}");
            RunImport(course, hole, flat);
        }

        // ── Lifecycle ────────────────────────────────────────────────────────

        private void OnEnable()
        {
            RefreshCourseList();
            LoadPrefs();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Course Importer", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Import a hole scene from the UHole pipeline into the Unity project.",
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(6);

            // ── Course selector ──────────────────────────────────────────────
            DrawCourseSelector();
            EditorGUILayout.Space(4);

            // ── Flat toggle ──────────────────────────────────────────────────
            _useFlat = EditorGUILayout.Toggle("Import Flat Variant", _useFlat);
            EditorGUILayout.Space(6);

            EditorGUILayout.LabelField("Holes", EditorStyles.boldLabel);

            // ── Scrollable hole list ─────────────────────────────────────────
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));
            DrawHoleList();
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(4);

            // ── Import All button ────────────────────────────────────────────
            DrawImportAll();

            // ── Status label ─────────────────────────────────────────────────
            if (!string.IsNullOrEmpty(_lastStatus))
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox(_lastStatus, MessageType.Info);
            }
        }

        // ── Drawing helpers ──────────────────────────────────────────────────

        private void DrawCourseSelector()
        {
            using var row = new EditorGUILayout.HorizontalScope();
            EditorGUILayout.LabelField("Course", GUILayout.Width(60));

            if (_courses.Count == 0)
            {
                EditorGUILayout.LabelField("(no courses found under Assets/Golf/Courses/)");
                if (GUILayout.Button("Refresh", GUILayout.Width(65)))
                    RefreshCourseList();
                return;
            }

            int newIndex = EditorGUILayout.Popup(_courseIndex, _courses.ToArray());
            if (newIndex != _courseIndex)
            {
                _courseIndex = newIndex;
                OnCourseChanged();
            }

            if (GUILayout.Button("↺", GUILayout.Width(26)))
                RefreshCourseList();
        }

        private void DrawHoleList()
        {
            string courseSlug = CurrentCourse();

            for (int h = 1; h <= 18; h++)
            {
                using var row = new EditorGUILayout.HorizontalScope();

                EditorGUILayout.LabelField($"Hole {h:D2}", GUILayout.Width(60));

                // Check if Flat scene actually exists for this hole
                bool flatExists = FlatSceneExists(courseSlug, h);

                if (GUILayout.Button("Import Geo", GUILayout.Width(100)))
                {
                    _holeNumber = h;
                    _useFlat    = false;
                    SavePrefs();
                    RunImport(courseSlug, h, flat: false);
                    SetStatus($"Imported {courseSlug} Hole {h:D2} Geo.");
                }

                using (new EditorGUI.DisabledGroupScope(!flatExists))
                {
                    string flatLabel = flatExists ? "Import Flat" : "No Flat";
                    if (GUILayout.Button(flatLabel, GUILayout.Width(100)))
                    {
                        _holeNumber = h;
                        _useFlat    = true;
                        SavePrefs();
                        RunImport(courseSlug, h, flat: true);
                        SetStatus($"Imported {courseSlug} Hole {h:D2} Flat.");
                    }
                }
            }
        }

        private void DrawImportAll()
        {
            using var row = new EditorGUILayout.HorizontalScope();
            if (GUILayout.Button("Import All Holes (Geo)", GUILayout.ExpandWidth(true)))
            {
                string slug = CurrentCourse();
                for (int h = 1; h <= 18; h++)
                    HoleGeoImporter.ImportGeoHole(slug, h);
                SetStatus($"Import All Geo complete for {slug}.");
            }

            if (GUILayout.Button("Import All (Flat)", GUILayout.ExpandWidth(true)))
            {
                string slug = CurrentCourse();
                for (int h = 1; h <= 18; h++)
                {
                    if (FlatSceneExists(slug, h))
                        HoleGeoImporter.ImportGeoHoleFlat(slug, h);
                }
                SetStatus($"Import All Flat complete for {slug}.");
            }
        }

        // ── Logic helpers ────────────────────────────────────────────────────

        private static void RunImport(string courseSlug, int holeNumber, bool flat)
        {
            // Update the static bus so any downstream bake steps pick up the right slug
            ActiveCourseContext.Set(courseSlug, ToDisplayName(courseSlug));

            if (flat)
                HoleGeoImporter.ImportGeoHoleFlat(courseSlug, holeNumber);
            else
                HoleGeoImporter.ImportGeoHole(courseSlug, holeNumber);
        }

        private void OnCourseChanged()
        {
            string slug = CurrentCourse();
            ActiveCourseContext.Set(slug, ToDisplayName(slug));
            SavePrefs();
        }

        private void RefreshCourseList()
        {
            _courses.Clear();
            string coursesRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "Golf/Courses"));
            if (Directory.Exists(coursesRoot))
            {
                var dirs = Directory.GetDirectories(coursesRoot)
                    .Select(Path.GetFileName)
                    .OrderBy(x => x)
                    .ToList();
                _courses.AddRange(dirs);
            }

            if (_courses.Count == 0)
            {
                Debug.LogWarning("[CourseImporterWindow] No course directories found under Assets/Golf/Courses/");
            }

            // Re-align index to the persisted slug after refresh
            string savedSlug = EditorPrefs.GetString(PrefCourse, "lomond-country-club");
            int idx = _courses.IndexOf(savedSlug);
            _courseIndex = idx >= 0 ? idx : 0;
        }

        private string CurrentCourse()
        {
            if (_courses.Count == 0) return "lomond-country-club";
            _courseIndex = Mathf.Clamp(_courseIndex, 0, _courses.Count - 1);
            return _courses[_courseIndex];
        }

        private static bool FlatSceneExists(string courseSlug, int holeNumber)
        {
            string path = $"Assets/Golf/Courses/{courseSlug}/Generated/Hole_{holeNumber:D2}_Geo_Flat.unity";
            return File.Exists(Path.GetFullPath(path.Replace("/", Path.DirectorySeparatorChar.ToString())
                .Replace('/', Path.DirectorySeparatorChar)));
        }

        private static string ToDisplayName(string slug)
        {
            // "lomond-country-club" → "Lomond Country Club"
            return System.Globalization.CultureInfo.CurrentCulture
                .TextInfo.ToTitleCase(slug.Replace('-', ' '));
        }

        private void SetStatus(string msg)
        {
            _lastStatus = $"[{DateTime.Now:HH:mm:ss}] {msg}";
            Repaint();
        }

        // ── Pref helpers ─────────────────────────────────────────────────────

        private void LoadPrefs()
        {
            string savedSlug = EditorPrefs.GetString(PrefCourse, "lomond-country-club");
            int idx = _courses.IndexOf(savedSlug);
            if (idx >= 0) _courseIndex = idx;

            _holeNumber = EditorPrefs.GetInt(PrefHole, 1);
            _useFlat    = EditorPrefs.GetBool(PrefFlat, false);
        }

        private void SavePrefs()
        {
            EditorPrefs.SetString(PrefCourse, CurrentCourse());
            EditorPrefs.SetInt(PrefHole,      _holeNumber);
            EditorPrefs.SetBool(PrefFlat,     _useFlat);
        }
    }
}
