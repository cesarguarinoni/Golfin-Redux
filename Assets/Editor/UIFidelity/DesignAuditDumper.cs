// ─────────────────────────────────────────────────────────────────────────────
// DesignAuditDumper — `design_consistency_audit` Phase 0.2.
//
// READS ONLY. It walks a live screen root in PLAY MODE and writes what every text,
// image and button actually IS, so the audit measures the running game rather than
// the serialized YAML. Nothing here mutates a scene, a prefab or an asset; the
// task's A10 depends on that staying true.
//
// WHY RENDERED PX, NOT THE SERIALIZED SIZE. Many cards live under a scaled parent,
// so `fontSize` alone proves nothing — a 46.2 under a 0.66 parent and a 30.5 under
// a 1.0 parent are the same glyph on screen. The dump therefore carries BOTH, and
// the audit's F/H dimensions are judged on:
//
//     renderedPx = fontSize × rectTransform.lossyScale.y ÷ canvas.scaleFactor
//
// which is stated in every JSON header so a reader never has to guess which number
// a finding was argued from. `scaleFactor` divides out the CanvasScaler, leaving px
// in the 1170×2532 design space where a Figma px IS a Unity px (playbook §2).
//
// INACTIVE CHILDREN ARE INCLUDED, tagged `active:false` — the Inventory tabs and the
// Settings submenus are hidden states, and "the hidden tab uses LiberationSans" is a
// finding the player eventually sees.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.EditorTools.UIFidelity
{
    public static class DesignAuditDumper
    {
        const string OutDir = "Docs/Diagnostics/_capture/design_audit";

        /// <summary>The font asset Unity falls back to when nobody assigned one. Never a token.</summary>
        public const string DefaultFontName = "LiberationSans SDF";

        // `LocalizedText.key` and `.japaneseFontScale` are PRIVATE, so they are read by reflection.
        //
        // Reflect off the INSTANCE's own type, never `Type.GetType("LocalizedText, Assembly-CSharp")`
        // — that returns null here, because LocalizedText lives in the `Golfin.Localization` asmdef
        // and not in Assembly-CSharp. The assembly-qualified lookup fails SILENTLY: every locKey
        // would have dumped as "" and every jpFontScale as null, and the JA pass would have
        // concluded that nothing on any screen is localized. Caught by probing the lookup before
        // the first real dump rather than by reading the output later.
        static readonly Dictionary<System.Type, FieldInfo?> KeyFieldCache = new();
        static readonly Dictionary<System.Type, FieldInfo?> JpFieldCache = new();

        static FieldInfo? PrivateField(System.Type t, string name, Dictionary<System.Type, FieldInfo?> cache)
        {
            if (cache.TryGetValue(t, out var f)) return f;
            f = t.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            cache[t] = f;
            return f;
        }

        /// <summary>
        /// Walk <paramref name="root"/> and write `design_audit/&lt;screenName&gt;.json`.
        /// Returns the path written, or an error string beginning "ERROR" — callers log it.
        /// </summary>
        public static string Dump(GameObject root, string screenName, string? locale = null, string? via = null)
        {
            if (root == null) return "ERROR: root is null for " + screenName;

            var canvas = root.GetComponentInParent<Canvas>();
            float scaleFactor = canvas != null ? canvas.scaleFactor : 1f;
            if (scaleFactor <= 0f) scaleFactor = 1f;

            int nTmp = 0, nImg = 0, nBtn = 0, nLib = 0, nOutline = 0, nShadow = 0;

            var texts = new StringBuilder();
            foreach (var tmp in root.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                nTmp++;
                string fontName = tmp.font != null ? tmp.font.name : "<NONE>";
                if (fontName == DefaultFontName) nLib++;

                var rt = tmp.rectTransform;
                float rendered = tmp.fontSize * rt.lossyScale.y / scaleFactor;

                var outline = tmp.GetComponent<Outline>();
                var shadow  = tmp.GetComponent<Shadow>();
                // Outline derives from Shadow in uGUI, so an Outline would be double-counted.
                if (outline != null) nOutline++;
                if (shadow != null && outline == null) nShadow++;

                object? locComp = null;
                foreach (var mb in tmp.GetComponents<MonoBehaviour>())
                    if (mb != null && mb.GetType().Name == "LocalizedText") { locComp = mb; break; }

                string locKey = "";
                string jpScale = "null";
                if (locComp != null)
                {
                    var lt = locComp.GetType();
                    var kf = PrivateField(lt, "key", KeyFieldCache);
                    var jf = PrivateField(lt, "japaneseFontScale", JpFieldCache);
                    if (kf != null) locKey = kf.GetValue(locComp) as string ?? "";
                    if (jf != null) jpScale = F(System.Convert.ToSingle(jf.GetValue(locComp)));
                }

                // TMP outline lives on the MATERIAL, not the component.
                float matOutline = 0f; string matOutlineCol = "";
                var mat = tmp.fontSharedMaterial;
                if (mat != null && mat.HasProperty(ShaderUtilities.ID_OutlineWidth))
                {
                    matOutline = mat.GetFloat(ShaderUtilities.ID_OutlineWidth);
                    if (mat.HasProperty(ShaderUtilities.ID_OutlineColor))
                        matOutlineCol = Hex(mat.GetColor(ShaderUtilities.ID_OutlineColor));
                }

                texts.Append(texts.Length == 0 ? "" : ",\n    ");
                texts.Append("{")
                     .Append(Q("path") + ":" + Q(Path(tmp.transform, root.transform)) + ",")
                     .Append(Q("active") + ":" + (tmp.gameObject.activeInHierarchy ? "true" : "false") + ",")
                     .Append(Q("font") + ":" + Q(fontName) + ",")
                     .Append(Q("fontStyle") + ":" + Q(tmp.fontStyle.ToString()) + ",")
                     .Append(Q("fontWeight") + ":" + Q(tmp.fontWeight.ToString()) + ",")
                     // AUTO-SIZING MAKES `fontSize` A RESULT, NOT AN AUTHORED VALUE. TMP returns the
                     // size it computed to fit the rect, so the same label reports a different
                     // fontSize when anything about its layout or font changes — the tripwire showed
                     // 49.05 -> 51 from a font swap alone. A finding that says "this label is the
                     // wrong size" is meaningless on an auto-sizing label unless it names the bounds,
                     // so both are dumped and the audit judges autosized sites on min/max, never on
                     // the momentary value.
                     .Append(Q("fontSize") + ":" + F(tmp.fontSize) + ",")
                     .Append(Q("autoSize") + ":" + (tmp.enableAutoSizing ? "true" : "false") + ",")
                     .Append(Q("autoSizeMin") + ":" + F(tmp.fontSizeMin) + ",")
                     .Append(Q("autoSizeMax") + ":" + F(tmp.fontSizeMax) + ",")
                     .Append(Q("lossyScaleY") + ":" + F(rt.lossyScale.y) + ",")
                     .Append(Q("renderedPx") + ":" + F(rendered) + ",")
                     .Append(Q("color") + ":" + Q(Hex(tmp.color)) + ",")
                     .Append(Q("matOutlineWidth") + ":" + F(matOutline) + ",")
                     .Append(Q("matOutlineColor") + ":" + Q(matOutlineCol) + ",")
                     .Append(Q("outlineComponent") + ":" + (outline != null ? "true" : "false") + ",")
                     .Append(Q("shadowComponent") + ":" + (shadow != null && outline == null ? "true" : "false") + ",")
                     .Append(Q("locKey") + ":" + Q(locKey) + ",")
                     .Append(Q("jpFontScale") + ":" + jpScale + ",")
                     .Append(Q("text") + ":" + Q(Clip(tmp.text, 40)))
                     .Append("}");
            }

            var images = new StringBuilder();
            foreach (var img in root.GetComponentsInChildren<Image>(true))
            {
                nImg++;
                var sp = img.sprite;
                string spriteName = sp != null ? sp.name : "<NONE>";
                string guid = "";
                if (sp != null)
                {
                    string p = AssetDatabase.GetAssetPath(sp);
                    if (!string.IsNullOrEmpty(p)) guid = AssetDatabase.AssetPathToGUID(p);
                }
                var outline = img.GetComponent<Outline>();
                var shadow  = img.GetComponent<Shadow>();
                if (outline != null) nOutline++;
                if (shadow != null && outline == null) nShadow++;

                var size = img.rectTransform.rect.size;
                var border = sp != null ? sp.border : Vector4.zero;

                images.Append(images.Length == 0 ? "" : ",\n    ");
                images.Append("{")
                      .Append(Q("path") + ":" + Q(Path(img.transform, root.transform)) + ",")
                      .Append(Q("active") + ":" + (img.gameObject.activeInHierarchy ? "true" : "false") + ",")
                      .Append(Q("sprite") + ":" + Q(spriteName) + ",")
                      .Append(Q("spriteGuid") + ":" + Q(guid) + ",")
                      .Append(Q("type") + ":" + Q(img.type.ToString()) + ",")
                      .Append(Q("ppuMultiplier") + ":" + F(img.pixelsPerUnitMultiplier) + ",")
                      .Append(Q("color") + ":" + Q(Hex(img.color)) + ",")
                      .Append(Q("width") + ":" + F(size.x) + "," + Q("height") + ":" + F(size.y) + ",")
                      .Append(Q("spriteBorder") + ":" + Q($"{border.x},{border.y},{border.z},{border.w}") + ",")
                      .Append(Q("preserveAspect") + ":" + (img.preserveAspect ? "true" : "false") + ",")
                      .Append(Q("raycastTarget") + ":" + (img.raycastTarget ? "true" : "false") + ",")
                      .Append(Q("outlineComponent") + ":" + (outline != null ? "true" : "false") + ",")
                      .Append(Q("shadowComponent") + ":" + (shadow != null && outline == null ? "true" : "false"))
                      .Append("}");
            }

            var buttons = new StringBuilder();
            foreach (var btn in root.GetComponentsInChildren<Button>(true))
            {
                nBtn++;
                bool feedback = false;
                foreach (var mb in btn.GetComponents<MonoBehaviour>())
                    if (mb != null && mb.GetType().Name == "ButtonPressFeedback") { feedback = true; break; }

                var size = ((RectTransform)btn.transform).rect.size;
                buttons.Append(buttons.Length == 0 ? "" : ",\n    ");
                buttons.Append("{")
                       .Append(Q("path") + ":" + Q(Path(btn.transform, root.transform)) + ",")
                       .Append(Q("active") + ":" + (btn.gameObject.activeInHierarchy ? "true" : "false") + ",")
                       .Append(Q("width") + ":" + F(size.x) + "," + Q("height") + ":" + F(size.y) + ",")
                       .Append(Q("buttonPressFeedback") + ":" + (feedback ? "true" : "false"))
                       .Append("}");
            }

            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  " + Q("screen") + ":" + Q(screenName) + ",");
            sb.AppendLine("  " + Q("locale") + ":" + Q(locale ?? "") + ",");
            // HOW this screen was reached. A screen with no player path from a fresh session is
            // re-seated with ShowScreen, and that must be visible in the artifact — a forced dump
            // read later as a real-navigation one would launder PIPELINE_HARDENING rule 2.
            sb.AppendLine("  " + Q("reachedVia") + ":" + Q(via ?? "unspecified") + ",");
            sb.AppendLine("  " + Q("rootPath") + ":" + Q(root.name) + ",");
            sb.AppendLine("  " + Q("canvasScaleFactor") + ":" + F(scaleFactor) + ",");
            sb.AppendLine("  " + Q("renderedPxFormula") +
                          ":" + Q("fontSize * rectTransform.lossyScale.y / canvas.scaleFactor") + ",");
            sb.AppendLine("  " + Q("counts") + ":{" +
                          Q("tmp") + ":" + nTmp + "," +
                          Q("image") + ":" + nImg + "," +
                          Q("button") + ":" + nBtn + "," +
                          Q("liberationSans") + ":" + nLib + "," +
                          Q("outlineComponents") + ":" + nOutline + "," +
                          Q("shadowComponents") + ":" + nShadow + "},");
            sb.AppendLine("  " + Q("texts") + ":[");
            sb.AppendLine("    " + texts);
            sb.AppendLine("  ],");
            sb.AppendLine("  " + Q("images") + ":[");
            sb.AppendLine("    " + images);
            sb.AppendLine("  ],");
            sb.AppendLine("  " + Q("buttons") + ":[");
            sb.AppendLine("    " + buttons);
            sb.AppendLine("  ]");
            sb.AppendLine("}");

            Directory.CreateDirectory(OutDir);
            // LOCALE IS PART OF THE FILENAME. Both passes used to write `<Screen>.json`, so an EN
            // run silently overwrote the JA dump of the same screen and vice versa. The corpus then
            // held a MIX of locales, and locale-INVARIANT properties (Image.Type.Filled, the
            // non-autosized TMP count) came out different per locale — which is impossible, and is
            // how the red-team caught it. Suffixing makes the two corpora physically incapable of
            // clobbering each other.
            string suffix = string.IsNullOrEmpty(locale) ? "" : "__" + locale;
            string outPath = System.IO.Path.Combine(OutDir, screenName + suffix + ".json");
            File.WriteAllText(outPath, sb.ToString());
            Debug.Log($"[DesignAuditDumper] {screenName}: tmp={nTmp} img={nImg} btn={nBtn} " +
                      $"liberationSans={nLib} outline={nOutline} shadow={nShadow} -> {outPath}");
            return outPath;
        }

        // ── helpers ──────────────────────────────────────────────────────────

        /// <summary>Rendered px for one label — the formula the JSON header states, exposed so a
        /// test can pin it on a nested scaled rect rather than re-deriving it.</summary>
        public static float RenderedPx(TextMeshProUGUI tmp, float canvasScaleFactor)
        {
            if (tmp == null) return 0f;
            if (canvasScaleFactor <= 0f) canvasScaleFactor = 1f;
            return tmp.fontSize * tmp.rectTransform.lossyScale.y / canvasScaleFactor;
        }

        public static bool IsDefaultFont(TextMeshProUGUI tmp) =>
            tmp != null && tmp.font != null && tmp.font.name == DefaultFontName;

        static string Path(Transform t, Transform root)
        {
            var parts = new List<string>();
            var cur = t;
            while (cur != null && cur != root) { parts.Add(cur.name); cur = cur.parent; }
            parts.Reverse();
            return string.Join("/", parts);
        }

        static string Clip(string? s, int n)
        {
            s = (s ?? "").Replace("\n", "\\n").Replace("\r", "");
            return s.Length <= n ? s : s.Substring(0, n) + "…";
        }

        static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGBA(c);
        static string F(float v) => v.ToString("0.####", CultureInfo.InvariantCulture);
        static string Q(string s) => "\"" + Esc(s) + "\"";

        static string Esc(string? s) => (s ?? "")
            .Replace("\\", "\\\\").Replace("\"", "\\\"")
            .Replace("\n", "\\n").Replace("\r", "").Replace("\t", " ");
    }
}
