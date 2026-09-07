// ─────────────────────────────────────────────────────────────────────────────
// DefaultFontReadoutFixer — Quick task `da_q2_default_font_readouts`
// (design_consistency_audit § 3.2, fix group Q2).
//
// 41 TextMeshProUGUI stat readouts were still bound to `LiberationSans SDF`,
// Unity's default font, which DESIGN_TOKENS.md says is NEVER a token. This
// re-binds every one of them to a Rubik face and PRINTS THE SITE LIST it
// changed, so the report quotes the tool's own output instead of a hand-kept
// list of paths.
//
// WHY A SCRIPT AND NOT 41 INSPECTOR CLICKS: the sites live on inactive screens
// (the Compare panels, the Settings UserProfile submenu), one of them is EMPTY
// today and renders in Liberation the moment it gets text, and a hand pass has
// no artifact a reviewer can re-derive. The site JSON is that artifact.
//
// THE FACE PER SITE. Default is Rubik-VariableFont_wght SDF (Medium/Regular —
// the body/label face; a value is not a heading). The ONE exception is declared
// in `Overrides` below: where a readout's whole row is Rubik-SemiBold, the
// readout matches its row rather than the default. Every override is asserted
// to match EXACTLY ONE object — a suffix that matches none, or two, aborts the
// run rather than silently applying the default.
//
// m_sharedMaterial FOLLOWS m_fontAsset. TMP's `font` setter does that itself,
// but it is re-asserted through SerializedObject so a stale material can never
// survive: a label whose font asset moved and whose material did not renders
// the OLD atlas and looks completely unfixed.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Golfin.EditorTools.UIFidelity
{
    public static class DefaultFontReadoutFixer
    {
        public const string LiberationGuid    = "8f586378b4e144a9851e7b34d9b748ee";
        public const string RubikVariableGuid = "0e84913c86a5b7f4881cb73d5e80728f";
        public const string RubikSemiBoldGuid = "39fb7824ee463ab408c7f2e76c362562";

        const string ShellScenePath = "Assets/Scenes/ShellScene.unity";
        const string OutDir   = "Docs/Diagnostics/_capture/design_audit";
        const string SiteFile = OutDir + "/q2_font_swap_sites.json";

        static readonly string[] PrefabPaths =
        {
            "Assets/Prefabs/UI/Roster/CharacterThumbnailCard.prefab",
            "Assets/Prefabs/UI/Roster/StatBar.prefab",
        };

        /// <summary>
        /// Path-suffix → font GUID, for readouts that must match their ROW instead of taking the
        /// default body face. `FeedbackText` is the only one: every other label in its section
        /// (`UsernameLabel`, the input field's `Text`, `SaveUsernameButton/Label`) is bound to
        /// Rubik-SemiBold SDF, so binding it to the variable face would make the section's one
        /// error message the only Medium run in an all-SemiBold block.
        /// </summary>
        static readonly (string Suffix, string Guid)[] Overrides =
        {
            ("UserProfileSubmenu/UsernameSection/FeedbackText", RubikSemiBoldGuid),
        };

        [MenuItem("GOLFIN/Design Audit/Fix Q2 — default-font readouts to Rubik", priority = 410)]
        public static void FixMenu() => Fix();

        [MenuItem("GOLFIN/Design Audit/Dump the two Roster prefabs (EN)", priority = 411)]
        public static void DumpRosterPrefabsMenu() => DumpRosterPrefabs();

        // ── the fix ──────────────────────────────────────────────────────────

        /// <summary>Runs the swap and returns the human summary (also logged).</summary>
        public static string Fix()
        {
            var variable  = Load(RubikVariableGuid);
            var semiBold  = Load(RubikSemiBoldGuid);
            if (variable == null || semiBold == null)
                return Fail("ERROR: a Rubik font asset could not be loaded — nothing changed.");

            var rows = new List<Row>();
            var overrideHits = new int[Overrides.Length];

            // PREFABS FIRST. If a prefab were fixed after its instances, every instance would carry
            // a redundant font override pinning it to the value it already inherits.
            foreach (var prefabPath in PrefabPaths)
            {
                var root = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    int n = SwapUnder(root, prefabPath, rows, overrideHits, variable, semiBold);
                    if (n > 0) PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }

            // SCENE. Every root, including the screens that are inactive at author time — the
            // Compare panels and the Settings UserProfile submenu are exactly there.
            var scene = EditorSceneManager.GetSceneByPath(ShellScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
                scene = EditorSceneManager.OpenScene(ShellScenePath, OpenSceneMode.Single);

            int sceneChanged = 0;
            foreach (var root in scene.GetRootGameObjects())
                sceneChanged += SwapUnder(root, ShellScenePath, rows, overrideHits, variable, semiBold);

            // An override that matched nothing on a run that DID find work means the object moved
            // or was renamed, and the readout it named would have silently taken the default face.
            // A run that found nothing at all is just a re-run over already-fixed content, and the
            // override legitimately matches nothing — that must not be reported as a failure.
            for (int i = 0; i < Overrides.Length; i++)
                if (overrideHits[i] > 1 || (overrideHits[i] == 0 && rows.Count > 0))
                    return Fail($"ERROR: override '{Overrides[i].Suffix}' matched {overrideHits[i]} " +
                                $"objects across {rows.Count} swapped label(s) (expected exactly 1) — " +
                                "scene NOT saved, re-check the path.");

            if (rows.Count == 0)
            {
                Debug.Log("[Q2] nothing to do: no label is bound to LiberationSans SDF.");
                return "[Q2] nothing to do: no label is bound to LiberationSans SDF.";
            }

            if (sceneChanged > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();

            WriteSites(rows);

            var sb = new StringBuilder();
            sb.AppendLine($"[Q2] swapped {rows.Count} label(s) off LiberationSans SDF " +
                          $"({sceneChanged} in {ShellScenePath}, {rows.Count - sceneChanged} in prefabs).");
            foreach (var g in rows.GroupBy(r => r.NewFont))
                sb.AppendLine($"  -> {g.Key}: {g.Count()}");
            int over = rows.Count(r => r.OverflowsAfter);
            sb.AppendLine(over == 0
                ? "  no readout's preferred width exceeds its rect after the swap."
                : $"  WARNING: {over} readout(s) now exceed their rect width — see {SiteFile}.");
            sb.Append($"  site list: {SiteFile}");

            string summary = sb.ToString();
            Debug.Log(summary);
            return summary;
        }

        static int SwapUnder(GameObject root, string container, List<Row> rows, int[] overrideHits,
                             TMP_FontAsset variable, TMP_FontAsset semiBold)
        {
            int n = 0;
            foreach (var tmp in root.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (tmp == null || tmp.font == null) continue;
                if (AssetGuid(tmp.font) != LiberationGuid) continue;

                string path = HierarchyPath(tmp.transform);
                var target = variable;
                for (int i = 0; i < Overrides.Length; i++)
                    if (path.EndsWith(Overrides[i].Suffix))
                    {
                        overrideHits[i]++;
                        target = Overrides[i].Guid == RubikSemiBoldGuid ? semiBold : variable;
                    }

                var row = new Row
                {
                    Container    = container,
                    Path         = path,
                    Text         = tmp.text ?? "",
                    FontSize     = tmp.fontSize,
                    AutoSize     = tmp.enableAutoSizing,
                    AutoMin      = tmp.fontSizeMin,
                    AutoMax      = tmp.fontSizeMax,
                    FontStyle    = tmp.fontStyle.ToString(),
                    WrapMode     = tmp.textWrappingMode.ToString(),
                    Overflow     = tmp.overflowMode.ToString(),
                    RectWidth    = tmp.rectTransform.rect.width,
                    RectHeight   = tmp.rectTransform.rect.height,
                    OldFont      = tmp.font.name,
                    WidthBefore  = tmp.GetPreferredValues(tmp.text).x,
                    HeightBefore = tmp.GetPreferredValues(tmp.text).y,
                };

                // The `font` setter is the only path that reloads the atlas and rebuilds the
                // material references, so the preferred width measured below is the NEW face's
                // metrics rather than the old one's.
                //
                // It also writes m_TextStyleHashCode 0 -> -1183493901 on a label that had never
                // resolved a style. That is `TMP_Style.NormalStyle.hashCode` — TMP's textStyle
                // getter treats a 0 hash as Normal and stamps the hash the first time it is read,
                // so the two values are the same style. Writing 0 back through SerializedObject
                // does not hold (OnValidate re-resolves and re-stamps it), so it is left as TMP
                // wrote it and called out here instead of appearing unexplained in the diff.
                tmp.font = target;
                var so = new SerializedObject(tmp);
                so.FindProperty("m_fontAsset").objectReferenceValue = target;
                so.FindProperty("m_sharedMaterial").objectReferenceValue = target.material;
                so.ApplyModifiedPropertiesWithoutUndo();  // registers the prefab-instance override
                EditorUtility.SetDirty(tmp);
                if (PrefabUtility.IsPartOfPrefabInstance(tmp))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(tmp);

                row.NewFont      = target.name;
                row.NewFontGuid  = AssetGuid(target);
                row.NewMatGuid   = AssetGuid(tmp.fontSharedMaterial);
                row.WidthAfter   = tmp.GetPreferredValues(tmp.text).x;
                row.HeightAfter  = tmp.GetPreferredValues(tmp.text).y;
                row.PrefabInstance = PrefabUtility.IsPartOfPrefabInstance(tmp);
                rows.Add(row);
                n++;
            }
            return n;
        }

        // ── the prefab dumps ─────────────────────────────────────────────────

        /// <summary>
        /// `audit_numbers.py` reports the two Roster prefabs' Liberation count as a `+N` beside the
        /// in-screen count, and reads it from `PREFAB_*` dumps. The play-mode runner cannot reach a
        /// prefab that no scene instantiates, so the dump is taken here, from prefab contents.
        /// </summary>
        public static string DumpRosterPrefabs()
        {
            var lines = new List<string>();
            foreach (var prefabPath in PrefabPaths)
            {
                var root = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    string name = "PREFAB_" + Path.GetFileNameWithoutExtension(prefabPath);
                    lines.Add(DesignAuditDumper.Dump(root, name, "en", "prefab contents: " + prefabPath));
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            string s = "[Q2] " + string.Join("\n[Q2] ", lines);
            Debug.Log(s);
            return s;
        }

        // ── helpers ──────────────────────────────────────────────────────────

        struct Row
        {
            public string Container, Path, Text, FontStyle, WrapMode, Overflow, OldFont, NewFont, NewFontGuid, NewMatGuid;
            public float FontSize, AutoMin, AutoMax, RectWidth, RectHeight;
            public float WidthBefore, HeightBefore, WidthAfter, HeightAfter;
            public bool AutoSize, PrefabInstance;

            public bool Wraps => WrapMode != TextWrappingModes.NoWrap.ToString();

            /// <summary>A non-wrapping readout whose text is wider than its rect will clip or
            /// ellipsise. 0.5 px of slack absorbs float noise in the glyph metrics.</summary>
            public bool OverflowsAfter =>
                !Wraps && RectWidth > 0f && WidthAfter > RectWidth + 0.5f;
        }

        static void WriteSites(List<Row> rows)
        {
            Directory.CreateDirectory(OutDir);
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"task\": \"da_q2_default_font_readouts\",");
            sb.AppendLine($"  \"from\": \"LiberationSans SDF ({LiberationGuid})\",");
            sb.AppendLine($"  \"count\": {rows.Count},");
            sb.AppendLine("  \"sites\": [");
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                sb.Append("    {")
                  .Append(Q("container") + ":" + Q(r.Container) + ",")
                  .Append(Q("path") + ":" + Q(r.Path) + ",")
                  .Append(Q("text") + ":" + Q(r.Text) + ",")
                  .Append(Q("oldFont") + ":" + Q(r.OldFont) + ",")
                  .Append(Q("newFont") + ":" + Q(r.NewFont) + ",")
                  .Append(Q("newFontGuid") + ":" + Q(r.NewFontGuid) + ",")
                  .Append(Q("newMaterialGuid") + ":" + Q(r.NewMatGuid) + ",")
                  .Append(Q("fontSize") + ":" + F(r.FontSize) + ",")
                  .Append(Q("autoSize") + ":" + (r.AutoSize ? "true" : "false") + ",")
                  .Append(Q("autoSizeMin") + ":" + F(r.AutoMin) + ",")
                  .Append(Q("autoSizeMax") + ":" + F(r.AutoMax) + ",")
                  .Append(Q("fontStyle") + ":" + Q(r.FontStyle) + ",")
                  .Append(Q("textWrappingMode") + ":" + Q(r.WrapMode) + ",")
                  .Append(Q("overflowMode") + ":" + Q(r.Overflow) + ",")
                  .Append(Q("rectWidth") + ":" + F(r.RectWidth) + ",")
                  .Append(Q("rectHeight") + ":" + F(r.RectHeight) + ",")
                  .Append(Q("preferredWidthBefore") + ":" + F(r.WidthBefore) + ",")
                  .Append(Q("preferredWidthAfter") + ":" + F(r.WidthAfter) + ",")
                  .Append(Q("preferredHeightBefore") + ":" + F(r.HeightBefore) + ",")
                  .Append(Q("preferredHeightAfter") + ":" + F(r.HeightAfter) + ",")
                  .Append(Q("prefabInstance") + ":" + (r.PrefabInstance ? "true" : "false") + ",")
                  .Append(Q("overflowsAfter") + ":" + (r.OverflowsAfter ? "true" : "false"))
                  .Append(i == rows.Count - 1 ? "}\n" : "},\n");
            }
            sb.AppendLine("  ]");
            sb.AppendLine("}");
            File.WriteAllText(SiteFile, sb.ToString());
        }

        static TMP_FontAsset? Load(string guid) =>
            AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));

        static string AssetGuid(Object? o)
        {
            if (o == null) return "";
            return AssetDatabase.TryGetGUIDAndLocalFileIdentifier(o, out string guid, out long _) ? guid : "";
        }

        static string HierarchyPath(Transform t)
        {
            var parts = new List<string>();
            for (var cur = t; cur != null; cur = cur.parent) parts.Add(cur.name);
            parts.Reverse();
            return string.Join("/", parts);
        }

        static string Fail(string msg) { Debug.LogError(msg); return msg; }
        static string F(float v) => v.ToString("0.####", CultureInfo.InvariantCulture);
        static string Q(string s) => "\"" + (s ?? "")
            .Replace("\\", "\\\\").Replace("\"", "\\\"")
            .Replace("\n", "\\n").Replace("\r", "").Replace("\t", " ") + "\"";
    }
}
