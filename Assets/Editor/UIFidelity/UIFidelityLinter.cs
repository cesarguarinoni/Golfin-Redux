// Assets/Editor/UIFidelity/UIFidelityLinter.cs
// Automated fidelity detection for Figma->Unity UI prefabs.
// Two layers:
//   1) Render-health  — universal "this WILL render badly" checks, no reference needed
//                       (9-slice collapse/oval, non-9-slice distortion, flat-fill, Outline-border, tiny text)
//   2) Node-spec      — per-element expected values parsed from a Figma node (w/h/radius/sprite/color/font)
// Both instantiate the prefab under a temp canvas + ForceUpdateCanvases so RectTransform.rect is resolved.
// Origin: stamina_boost_shop menu-row session (every issue Cesar caught by eye => a check here).
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Golfin.EditorTools.UIFidelity
{
    [Serializable] public class UISpecElement
    {
        public string name;
        public float w = -1, h = -1, radius = -1, fontSize = -1;
        public bool requireSprite = false;   // node shows an image/gradient/border fill => flat fill is a HARD FAIL
        public string color = "";            // expected hex "#RRGGBB" (rim/fill/text), optional
        public string fontWeight = "";       // "Bold" | "SemiBold" | "Medium" | "Regular", optional
    }
    [Serializable] public class UISpec { public UISpecElement[] elements; }

    public struct Finding
    {
        public string sev, path, check, detail;
        public Finding(string s, string p, string c, string d) { sev = s; path = p; check = c; detail = d; }
    }

    public static class UIFidelityLinter
    {
        // ---- entry point: lint a prefab, optionally against a spec json ----
        public static string LintPrefab(string prefabPath, string specJsonPath = null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) return "PREFAB NOT FOUND: " + prefabPath;

            var holder = new GameObject("__LINT__");
            var canvasGO = new GameObject("cv", typeof(Canvas), typeof(CanvasScaler));
            canvasGO.transform.SetParent(holder.transform, false);
            var cv = canvasGO.GetComponent<Canvas>(); cv.renderMode = RenderMode.WorldSpace;
            var crt = (RectTransform)canvasGO.transform;
            var prt = prefab.GetComponent<RectTransform>();
            crt.sizeDelta = prt != null ? prt.sizeDelta : new Vector2(1170, 400);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvasGO.transform);
            Canvas.ForceUpdateCanvases();

            var findings = new List<Finding>();
            findings.AddRange(RenderHealth(inst));
            if (!string.IsNullOrEmpty(specJsonPath) && File.Exists(specJsonPath))
            {
                var spec = JsonUtility.FromJson<UISpec>(File.ReadAllText(specJsonPath));
                findings.AddRange(SpecCheck(inst, spec));
            }

            UnityEngine.Object.DestroyImmediate(holder);
            return Report(prefabPath, findings);
        }

        // ---- Layer 1: universal render-health ----
        public static List<Finding> RenderHealth(GameObject root)
        {
            var f = new List<Finding>();
            foreach (var img in root.GetComponentsInChildren<Image>(true))
            {
                var rt = img.rectTransform;
                var sz = rt.rect.size;
                var sp = img.sprite;
                string path = P(img.transform, root.transform);

                if (sp == null)
                {
                    // A hairline (<=3px on either axis) is a legitimate divider/rule/line, not a
                    // fabricated panel — a solid-colour fill is correct there, so don't flag it.
                    if (sz.x > 3f && sz.y > 3f)
                        f.Add(new Finding("WARN", path, "flat-fill",
                            $"Image has no sprite — flat {Hex(img.color)} fill with sharp corners. Verify intended, not a fabricated placeholder."));
                    continue;
                }
                if (sp.name == "UISprite")
                    f.Add(new Finding("WARN", path, "default-sprite", "uses Unity's built-in UISprite — likely a placeholder."));

                float ppu = Mathf.Max(0.0001f, img.pixelsPerUnitMultiplier);
                bool sliced9 = img.type == Image.Type.Sliced && sp.border != Vector4.zero;

                // 9-slice collapse => oval / distorted corners (the S_PillStadium-oval bug)
                if (sliced9)
                {
                    float bx = (sp.border.x + sp.border.z) / ppu;
                    float by = (sp.border.y + sp.border.w) / ppu;
                    if (bx > sz.x + 0.5f)
                        f.Add(new Finding("FAIL", path, "9slice-collapse-x",
                            $"slice borders {bx:0.#}px exceed width {sz.x:0.#}px → corners collapse (oval). Raise pixelsPerUnitMultiplier (now {ppu:0.##})."));
                    if (by > sz.y + 0.5f)
                        f.Add(new Finding("FAIL", path, "9slice-collapse-y",
                            $"slice borders {by:0.#}px exceed height {sz.y:0.#}px → corners collapse (oval). Raise pixelsPerUnitMultiplier (now {ppu:0.##})."));
                }

                // non-9-sliced sprite stretched off native aspect => rounded corners distort (the BUY-radius bug).
                // Skip extreme-aspect sprites (native aspect <0.25 or >4): gradients/scrims/dividers/lines are
                // MEANT to stretch and have no meaningful corners to distort (else an 8x256 scrim false-fails).
                if (sp.border == Vector4.zero && !img.preserveAspect && sz.y > 1 && sp.rect.height > 1
                    && img.GetComponent<AspectRatioFitter>() == null)
                {
                    float nativeAsp = sp.rect.width / sp.rect.height;
                    float renderAsp = sz.x / sz.y;
                    float dev = Mathf.Abs(renderAsp - nativeAsp) / nativeAsp;
                    if (dev > 0.15f && nativeAsp >= 0.25f && nativeAsp <= 4f)
                        f.Add(new Finding("WARN", path, "nonuniform-stretch",
                            $"non-9-sliced sprite '{sp.name}' stretched {dev * 100:0}% off native aspect ({nativeAsp:0.00}→{renderAsp:0.00}) → any rounded corners distort. Use a 9-sliced sprite (or preserveAspect)."));
                }
            }

            foreach (var o in root.GetComponentsInChildren<Outline>(true))
                f.Add(new Finding("WARN", P(o.transform, root.transform), "outline-border",
                    "Outline component present — does not produce a crisp Npx border (rule C5). Use a two-layer rounded rim."));

            foreach (var t in root.GetComponentsInChildren<TextMeshProUGUI>(true))
                if (t.fontSize > 0 && t.fontSize < 10)
                    f.Add(new Finding("INFO", P(t.transform, root.transform), "tiny-text", $"fontSize {t.fontSize:0.#} < 10."));

            return f;
        }

        // ---- Layer 2: node-spec checks ----
        public static List<Finding> SpecCheck(GameObject root, UISpec spec)
        {
            var f = new List<Finding>();
            if (spec == null || spec.elements == null) return f;
            foreach (var e in spec.elements)
            {
                var t = FindByName(root.transform, e.name);
                if (t == null) { f.Add(new Finding("FAIL", e.name, "missing", "spec element not found in prefab.")); continue; }
                var rt = t as RectTransform;
                var sz = rt != null ? rt.rect.size : Vector2.zero;
                var img = t.GetComponent<Image>();

                if (e.w > 0 && Mathf.Abs(sz.x - e.w) > 2f) f.Add(new Finding("FAIL", e.name, "width", $"expected {e.w}, got {sz.x:0.#}."));
                if (e.h > 0 && Mathf.Abs(sz.y - e.h) > 2f) f.Add(new Finding("FAIL", e.name, "height", $"expected {e.h}, got {sz.y:0.#}."));

                if (e.requireSprite)
                {
                    if (img == null || img.sprite == null)
                        f.Add(new Finding("FAIL", e.name, "require-sprite", "node shows a sprite/gradient/border but built element is a flat fill (null sprite). HARD FAIL."));
                }

                if (e.radius >= 0 && img != null && img.sprite != null && img.type == Image.Type.Sliced && img.sprite.border != Vector4.zero)
                {
                    float builtR = img.sprite.border.x / Mathf.Max(0.0001f, img.pixelsPerUnitMultiplier);
                    if (Mathf.Abs(builtR - e.radius) > 2.5f)
                        f.Add(new Finding("FAIL", e.name, "radius", $"expected {e.radius}px, built ~{builtR:0.#}px (border {img.sprite.border.x}/ppuMult {img.pixelsPerUnitMultiplier:0.##})."));
                }

                if (!string.IsNullOrEmpty(e.color) && img != null)
                {
                    if (ColorUtility.TryParseHtmlString(e.color, out var want))
                    {
                        var got = img.color;
                        float d = Mathf.Abs(want.r - got.r) + Mathf.Abs(want.g - got.g) + Mathf.Abs(want.b - got.b);
                        if (d > 0.12f) f.Add(new Finding("FAIL", e.name, "color", $"expected {e.color}, got {Hex(got)}."));
                    }
                }

                if (e.fontSize > 0 || !string.IsNullOrEmpty(e.fontWeight))
                {
                    var tmp = t.GetComponent<TextMeshProUGUI>();
                    if (tmp == null) f.Add(new Finding("FAIL", e.name, "not-text", "spec expects text but no TextMeshProUGUI."));
                    else
                    {
                        if (e.fontSize > 0 && Mathf.Abs(tmp.fontSize - e.fontSize) > 1.5f)
                            f.Add(new Finding("FAIL", e.name, "font-size", $"expected {e.fontSize}, got {tmp.fontSize:0.#}."));
                        if (!string.IsNullOrEmpty(e.fontWeight))
                        {
                            bool bold = (tmp.fontStyle & FontStyles.Bold) != 0;
                            bool wantBold = e.fontWeight.Equals("Bold", StringComparison.OrdinalIgnoreCase);
                            string fontName = tmp.font != null ? tmp.font.name : "";
                            bool nameMatches = fontName.IndexOf(e.fontWeight, StringComparison.OrdinalIgnoreCase) >= 0;
                            if (wantBold && !bold && !nameMatches)
                                f.Add(new Finding("WARN", e.name, "font-weight", $"expected {e.fontWeight}; style={tmp.fontStyle}, font='{fontName}'."));
                        }
                    }
                }
            }
            return f;
        }

        // ---- report ----
        static string Report(string prefabPath, List<Finding> findings)
        {
            int fail = 0, warn = 0, info = 0;
            var sb = new StringBuilder();
            sb.AppendLine("UI FIDELITY LINT: " + prefabPath);
            foreach (var f in findings)
            {
                if (f.sev == "FAIL") fail++; else if (f.sev == "WARN") warn++; else info++;
                sb.AppendLine($"[{f.sev}] {f.path}  ::{f.check}::  {f.detail}");
            }
            sb.AppendLine($"— {fail} FAIL, {warn} WARN, {info} INFO —");
            sb.AppendLine(fail == 0 ? "RESULT: PASS (health)" : "RESULT: FAIL (health)");

            var jsonSb = new StringBuilder("{\"prefab\":\"" + prefabPath + "\",\"fail\":" + fail + ",\"warn\":" + warn + ",\"findings\":[");
            for (int i = 0; i < findings.Count; i++)
            {
                var f = findings[i];
                jsonSb.Append((i > 0 ? "," : "") + "{\"sev\":\"" + f.sev + "\",\"path\":\"" + Esc(f.path) + "\",\"check\":\"" + f.check + "\",\"detail\":\"" + Esc(f.detail) + "\"}");
            }
            jsonSb.Append("]}");
            var outDir = "Docs/Diagnostics/_capture";
            Directory.CreateDirectory(outDir);
            var name = Path.GetFileNameWithoutExtension(prefabPath);
            File.WriteAllText(Path.Combine(outDir, name + "_lint.json"), jsonSb.ToString());
            return sb.ToString();
        }

        // ---- helpers ----
        static Transform FindByName(Transform root, string n)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == n) return t;
            return null;
        }
        static string P(Transform t, Transform root)
        {
            var s = t.name; while (t.parent != null && t != root) { t = t.parent; if (t != root) s = t.name + "/" + s; } return s;
        }
        static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGBA(c);
        static string Esc(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\'");
    }
}
