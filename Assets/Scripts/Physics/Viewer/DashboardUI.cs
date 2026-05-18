using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Golfin.Physics;
using Golfin.Physics.Math;

namespace Golfin.Physics.Viewer
{
    /// <summary>
    /// Dashboard UI for live parameter tuning. Builds its own layout in Start().
    /// Attach to a Canvas on PhysicsLab_Dashboard.unity.
    /// Left side: sliders for Aero / Wind / Surfaces / Putt.
    /// Right side: Fire button + readout (the main Game View shows the trajectory).
    /// </summary>
    public class DashboardUI : MonoBehaviour
    {
        [SerializeField] PhysicsLabController controller;

        // Local copies of configs for slider editing
        AeroConfig    _aero;
        WindConfig    _wind;
        SurfaceConfig _surface;
        PuttConfig    _putt;

        // Readout text
        TMP_Text _readoutText;

        static ShotPreset DashboardPreset =>
            System.Linq.Enumerable.FirstOrDefault(ShotPresetCatalog.ForScene(PresetScene.Dashboard));

        // ── Unity lifecycle ────────────────────────────────────────────────────

        void Start()
        {
            if (controller == null)
                controller = FindObjectOfType<PhysicsLabController>();
            if (controller == null) { Debug.LogError("[DashboardUI] No PhysicsLabController found."); return; }

            _aero    = controller.AeroCfg;
            _wind    = controller.WindCfg;
            _surface = controller.SurfaceCfg;
            _putt    = controller.PuttCfg;

            BuildUI();
            controller.OnShotFired += r =>
            {
                if (_readoutText != null)
                    _readoutText.text =
                        $"<b>{r.PresetDisplayName}</b>\n" +
                        $"Carry:  {r.CarryMeters:F1} m  ({r.CarryMeters * 1.09361f:F1} yd)\n" +
                        $"Total:  {r.TotalMeters:F1} m  ({r.TotalMeters * 1.09361f:F1} yd)\n" +
                        $"Peak:   {r.MaxHeightMeters:F1} m\n" +
                        $"Ended:  {r.TerminationReason}\n" +
                        $"Time:   {r.SimDurationSeconds:F2} s";
            };
        }

        // ── Build UI ───────────────────────────────────────────────────────────

        void BuildUI()
        {
            // Outer horizontal layout: left sliders | right controls
            var root = new GameObject("DashRoot", typeof(RectTransform));
            root.transform.SetParent(transform, false);
            var rootRT = root.GetComponent<RectTransform>();
            rootRT.anchorMin = Vector2.zero; rootRT.anchorMax = Vector2.one;
            rootRT.offsetMin = new Vector2(8, 8); rootRT.offsetMax = new Vector2(-8, -8);
            var hl = root.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 12f;
            hl.childForceExpandHeight = true;
            hl.childForceExpandWidth  = false;

            // ── Left column: sliders
            var left = MakeColumn(root, 460f);
            var scroll = MakeScrollView(left);
            var content = scroll;

            // Background
            var bg = root.GetComponent<Image>();
            if (bg == null) bg = root.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.7f);

            // ── AERO section
            AddSectionHeader(content, "AERO");
            AddFloatSlider(content, "Drag Cd",        0.10f, 0.70f, _aero.DragCoefficient.ToFloat(),     v => { _aero.DragCoefficient     = fp.FromFloat(v); controller.SetAeroConfig(_aero); });
            AddFloatSlider(content, "Lift Cl base",   0.00f, 0.40f, _aero.LiftCoefficientBase.ToFloat(), v => { _aero.LiftCoefficientBase  = fp.FromFloat(v); controller.SetAeroConfig(_aero); });
            AddFloatSlider(content, "Spin decay /s",  0.00f, 0.10f, _aero.SpinDecayRate.ToFloat(),       v => { _aero.SpinDecayRate        = fp.FromFloat(v); controller.SetAeroConfig(_aero); });

            // ── WIND section
            AddSectionHeader(content, "WIND (BASE)");
            AddFloatSlider(content, "Wind X m/s", -20f, 20f, _wind.BaseVelocity.x.ToFloat(),
                v => { _wind.BaseVelocity = new fp3(fp.FromFloat(v), _wind.BaseVelocity.y, _wind.BaseVelocity.z); controller.SetWindConfig(_wind); });
            AddFloatSlider(content, "Wind Z m/s", -20f, 20f, _wind.BaseVelocity.z.ToFloat(),
                v => { _wind.BaseVelocity = new fp3(_wind.BaseVelocity.x, _wind.BaseVelocity.y, fp.FromFloat(v)); controller.SetWindConfig(_wind); });
            AddFloatSlider(content, "Gust amplitude", 0f, 1f, _wind.GustAmplitude.ToFloat(),
                v => { _wind.GustAmplitude = fp.FromFloat(v); controller.SetWindConfig(_wind); });

            // ── SURFACES section (collapsed: Fairway + Green only to keep it short)
            AddSectionHeader(content, "SURFACES");
            AddSurfaceSliders(content, SurfaceType.Fairway, "Fairway");
            AddSurfaceSliders(content, SurfaceType.Green,   "Green");
            AddSurfaceSliders(content, SurfaceType.Rough,   "Rough");

            // ── PUTT section
            AddSectionHeader(content, "PUTT");
            AddPuttSliders(content, SurfaceType.Green,       "Green");
            AddPuttSliders(content, SurfaceType.GreenCollar, "GreenCollar");
            // Cup capture speed gate (USGA lip-out anchor: 1.5 m/s).
            // Source: Penner, A.R. (2002) "The physics of putting." Canadian Journal of Physics 80(2): 83–96 (see lip-out analysis). Architect-locked 2026-05-14.
            AddFloatSlider(content, "  Cup capture m/s", 0f, 5f, _putt.CupCaptureSpeed.ToFloat(),
                v => { _putt.CupCaptureSpeed = fp.FromFloat(v); controller.SetPuttConfig(_putt); });

            // ── Right column: buttons + readout
            var right = MakeColumn(root, 260f);

            AddButton(right, "▶ Fire driver_calm", () => controller?.Fire(DashboardPreset));
            AddButton(right, "Reload CSVs",         () => { controller?.ReloadConfigs(); RefreshFromController(); });
            AddButton(right, "Reset to defaults",   () => { controller?.ResetToDefaults(); RefreshFromController(); });

            _readoutText = AddText(right, "No shot fired.", 15f);
            var le = _readoutText.gameObject.GetComponent<LayoutElement>();
            if (le == null) le = _readoutText.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 160f;
        }

        // ── Surface slider helpers ─────────────────────────────────────────────

        void AddSurfaceSliders(GameObject parent, SurfaceType st, string label)
        {
            AddLabel(parent, $"  {label}");
            AddFloatSlider(parent, $"  Restitution",      0f, 1f,   _surface[st].Restitution.ToFloat(),
                v => { var c = _surface[st]; c.Restitution = fp.FromFloat(v); SetSurface(st, c); });
            AddFloatSlider(parent, $"  Roll resist.",     0f, 1f,   _surface[st].RollingResistance.ToFloat(),
                v => { var c = _surface[st]; c.RollingResistance = fp.FromFloat(v); SetSurface(st, c); });
            AddFloatSlider(parent, $"  Stop speed m/s",  0f, 0.5f, _surface[st].StopSpeed.ToFloat(),
                v => { var c = _surface[st]; c.StopSpeed = fp.FromFloat(v); SetSurface(st, c); });
        }

        void AddPuttSliders(GameObject parent, SurfaceType st, string label)
        {
            AddLabel(parent, $"  {label}");
            AddFloatSlider(parent, $"  Roll resist.",    0f, 0.5f, _putt[st].RollingResistance.ToFloat(),
                v => { var c = _putt[st]; c.RollingResistance = fp.FromFloat(v); SetPutt(st, c); });
            AddFloatSlider(parent, $"  Stop speed m/s", 0f, 0.2f, _putt[st].StopSpeed.ToFloat(),
                v => { var c = _putt[st]; c.StopSpeed = fp.FromFloat(v); SetPutt(st, c); });
        }

        void SetSurface(SurfaceType st, SurfaceCoefficients c)
        {
            _surface.Coefficients[(int)st] = c;
            controller.SetSurfaceConfig(_surface);
        }

        void SetPutt(SurfaceType st, SurfaceCoefficients c)
        {
            _putt.Coefficients[(int)st] = c;
            controller.SetPuttConfig(_putt);
        }

        void RefreshFromController()
        {
            if (controller == null) return;
            _aero    = controller.AeroCfg;
            _wind    = controller.WindCfg;
            _surface = controller.SurfaceCfg;
            _putt    = controller.PuttCfg;
            // Sliders would need re-building to reflect new values; for MVP just log
            Debug.Log("[DashboardUI] Configs refreshed from controller.");
        }

        // ── UI builder helpers ─────────────────────────────────────────────────

        static GameObject MakeColumn(GameObject parent, float width)
        {
            var go = new GameObject("Col", typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(width, 0f);
            var vl = go.AddComponent<VerticalLayoutGroup>();
            vl.childForceExpandWidth = true;
            vl.childForceExpandHeight = false;
            vl.spacing = 4f;
            vl.padding = new RectOffset(4, 4, 4, 4);
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.flexibleHeight = 1f;
            return go;
        }

        static GameObject MakeScrollView(GameObject parent)
        {
            // Simplified: just return parent (scroll not critical for lab MVP)
            return parent;
        }

        static void AddSectionHeader(GameObject parent, string text)
        {
            var go = new GameObject("Header_" + text, typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(parent.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0f, 22f);
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 22f;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text     = $"── {text} ──";
            tmp.fontSize = 16f;
            tmp.color    = new Color(0.9f, 0.75f, 0.3f, 1f);
        }

        static void AddLabel(GameObject parent, string text)
        {
            var go = new GameObject("Lbl_" + text.Trim(), typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(parent.transform, false);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 16f);
            var le = go.AddComponent<LayoutElement>(); le.preferredHeight = 16f;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text     = text;
            tmp.fontSize = 14f;
            tmp.color    = new Color(0.7f, 0.7f, 0.7f, 1f);
        }

        static TMP_Text AddText(GameObject parent, string text, float size)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(parent.transform, false);
            var le = go.AddComponent<LayoutElement>(); le.preferredHeight = size * 1.6f;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text     = text;
            tmp.fontSize = size;
            tmp.color    = Color.white;
            return tmp;
        }

        static void AddFloatSlider(GameObject parent, string label, float min, float max, float current,
                                   UnityEngine.Events.UnityAction<float> cb)
        {
            var row = new GameObject("SliderRow_" + label.Trim(), typeof(RectTransform));
            row.transform.SetParent(parent.transform, false);
            var hl = row.AddComponent<HorizontalLayoutGroup>();
            hl.childForceExpandWidth  = false;
            hl.childForceExpandHeight = true;
            hl.spacing = 4f;
            var rowLE = row.AddComponent<LayoutElement>();
            rowLE.preferredHeight = 28f;

            // Label
            var lgo = new GameObject("L", typeof(RectTransform), typeof(CanvasRenderer));
            lgo.transform.SetParent(row.transform, false);
            var lle = lgo.AddComponent<LayoutElement>();
            lle.preferredWidth = 110f;
            lle.flexibleWidth  = 0f;
            var ltmp = lgo.AddComponent<TextMeshProUGUI>();
            ltmp.text = label; ltmp.fontSize = 13f; ltmp.color = Color.white;
            ltmp.alignment = TextAlignmentOptions.MidlineLeft;

            // Value display
            var vgo = new GameObject("V", typeof(RectTransform), typeof(CanvasRenderer));
            vgo.transform.SetParent(row.transform, false);
            var vle = vgo.AddComponent<LayoutElement>();
            vle.preferredWidth = 44f;
            vle.flexibleWidth  = 0f;
            var vtmp = vgo.AddComponent<TextMeshProUGUI>();
            vtmp.text = current.ToString("F2"); vtmp.fontSize = 13f; vtmp.color = Color.white;
            vtmp.alignment = TextAlignmentOptions.MidlineRight;

            // Slider container
            var sgo = new GameObject("Slider", typeof(RectTransform));
            sgo.transform.SetParent(row.transform, false);
            var sle = sgo.AddComponent<LayoutElement>();
            sle.flexibleWidth   = 1f;
            sle.preferredHeight = 28f;

            // Background track (middle 40% of height)
            var bgGO = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            bgGO.transform.SetParent(sgo.transform, false);
            var bgRT = bgGO.GetComponent<RectTransform>();
            bgRT.anchorMin = new Vector2(0f, 0.3f);
            bgRT.anchorMax = new Vector2(1f, 0.7f);
            bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;
            bgGO.GetComponent<Image>().color = new Color(0.25f, 0.25f, 0.25f, 1f);

            // Fill Area (inset so fill doesn't overshoot at max)
            var faGO = new GameObject("Fill Area", typeof(RectTransform));
            faGO.transform.SetParent(sgo.transform, false);
            var faRT = faGO.GetComponent<RectTransform>();
            faRT.anchorMin = new Vector2(0f, 0.3f);
            faRT.anchorMax = new Vector2(1f, 0.7f);
            faRT.offsetMin = new Vector2(5f, 0f);
            faRT.offsetMax = new Vector2(-14f, 0f);

            // Fill (Slider sets anchorMax.x = normalizedValue)
            var fillGO = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fillGO.transform.SetParent(faGO.transform, false);
            var fillRT = fillGO.GetComponent<RectTransform>();
            fillRT.anchorMin = Vector2.zero;
            fillRT.anchorMax = Vector2.one;
            fillRT.offsetMin = fillRT.offsetMax = Vector2.zero;
            fillGO.GetComponent<Image>().color = new Color(0.25f, 0.55f, 0.85f, 1f);

            // Handle Slide Area (inset by half handle width so handle stays in bounds)
            var haGO = new GameObject("Handle Slide Area", typeof(RectTransform));
            haGO.transform.SetParent(sgo.transform, false);
            var haRT = haGO.GetComponent<RectTransform>();
            haRT.anchorMin = Vector2.zero;
            haRT.anchorMax = Vector2.one;
            haRT.offsetMin = new Vector2(7f, 0f);
            haRT.offsetMax = new Vector2(-7f, 0f);

            // Handle
            var handleGO = new GameObject("Handle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            handleGO.transform.SetParent(haGO.transform, false);
            var handleRT = handleGO.GetComponent<RectTransform>();
            handleRT.anchorMin = new Vector2(0f, 0f);
            handleRT.anchorMax = new Vector2(0f, 1f);
            handleRT.sizeDelta = new Vector2(14f, -4f);
            handleRT.anchoredPosition = Vector2.zero;
            handleGO.GetComponent<Image>().color = Color.white;

            var slider = sgo.AddComponent<Slider>();
            slider.fillRect   = fillRT;
            slider.handleRect = handleRT;
            slider.direction  = Slider.Direction.LeftToRight;
            slider.minValue   = min;
            slider.maxValue   = max;
            slider.value      = current;

            slider.onValueChanged.AddListener(v =>
            {
                vtmp.text = v.ToString("F2");
                cb(v);
            });
        }

        static Button AddButton(GameObject parent, string label, UnityEngine.Events.UnityAction cb)
        {
            var go  = new GameObject("Btn_" + label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent.transform, false);
            var rt  = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0f, 32f);
            go.GetComponent<Image>().color = new Color(0.2f, 0.4f, 0.65f, 1f);
            var btn = go.AddComponent<Button>(); btn.onClick.AddListener(cb);
            var le  = go.AddComponent<LayoutElement>(); le.preferredHeight = 32f;

            var txtGO = new GameObject("Lbl", typeof(RectTransform), typeof(CanvasRenderer));
            txtGO.transform.SetParent(go.transform, false);
            var txtRT = txtGO.GetComponent<RectTransform>();
            txtRT.anchorMin = Vector2.zero; txtRT.anchorMax = Vector2.one;
            txtRT.offsetMin = txtRT.offsetMax = Vector2.zero;
            var tmp = txtGO.AddComponent<TextMeshProUGUI>();
            tmp.text      = label;
            tmp.fontSize  = 15f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color     = Color.white;
            return btn;
        }
    }
}
