using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Golfin.Physics;

namespace Golfin.Physics.Viewer
{
    /// <summary>
    /// In-scene UI panel for the physics lab. Builds its own layout in Awake().
    /// Attach to a Canvas GameObject alongside PhysicsLabController on LabRoot.
    /// Size: 340×480 semi-transparent panel in the top-left corner.
    /// </summary>
    public class PhysicsLabUI : MonoBehaviour
    {
        [SerializeField] PhysicsLabController controller;
        [SerializeField] PresetScene initialScene = PresetScene.Range;

        // Runtime state
        PresetScene _scene;
        List<ShotPreset> _presets = new List<ShotPreset>();
        int  _selectedIndex;
        float _playRate = 1f;

        // UI refs (built in Awake)
        TMP_Dropdown _presetDropdown;
        TMP_Dropdown _cameraDropdown;
        Slider       _rateSlider;
        TMP_Text     _rateLabel;
        TMP_Text     _readoutText;
        TMP_Text     _deterLabel;
        TMP_Text     _notesText;

        // Discrete play rates
        static readonly float[] PlayRates       = { 0.25f, 1f, 4f, float.MaxValue };
        static readonly string[] PlayRateLabels = { "0.25×", "1×", "4×", "Instant" };

        // ── Unity lifecycle ────────────────────────────────────────────────────

        void Awake()
        {
            if (controller == null)
                controller = GetComponentInParent<PhysicsLabController>();

            _scene = initialScene;
        }

        void Start()
        {
            BuildUI();
            PopulatePresets();
            SubscribeController();
        }

        // ── Public API ─────────────────────────────────────────────────────────

        public void SetScene(PresetScene scene)
        {
            _scene = scene;
            if (_presetDropdown != null) PopulatePresets();
        }

        public void ShowReadout(ShotReadout r)
        {
            if (_readoutText == null) return;
            _readoutText.text =
                $"<b>{r.PresetDisplayName}</b>\n" +
                $"Carry:   {r.CarryMeters:F1} m  ({r.CarryMeters * 1.09361f:F1} yd)\n" +
                $"Total:   {r.TotalMeters:F1} m  ({r.TotalMeters * 1.09361f:F1} yd)\n" +
                $"Peak:    {r.MaxHeightMeters:F1} m\n" +
                $"Bounces: {r.BounceCount}\n" +
                $"Ended:   {r.TerminationReason} on {r.FinalSurface}\n" +
                $"Time:    {r.SimDurationSeconds:F2} s";
        }

        public void ShowDeterminism(bool passed, int count)
        {
            if (_deterLabel == null) return;
            _deterLabel.text  = passed ? $"✓ {count}/{count} identical" : "✗ drift detected";
            _deterLabel.color = passed ? Color.green : Color.red;
        }

        // ── Build UI ───────────────────────────────────────────────────────────

        void BuildUI()
        {
            // Root panel
            var panel = new GameObject("LabUIPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panel.transform.SetParent(transform, false);
            var panelRT = panel.GetComponent<RectTransform>();
            panelRT.anchorMin  = Vector2.zero;
            panelRT.anchorMax  = Vector2.zero;
            panelRT.pivot      = Vector2.zero;
            panelRT.anchoredPosition = new Vector2(10f, 10f);
            panelRT.sizeDelta  = new Vector2(340f, 480f);
            var panelImg = panel.GetComponent<Image>();
            panelImg.color = new Color(0f, 0f, 0f, 0.65f);

            // Vertical layout
            var vl = panel.AddComponent<VerticalLayoutGroup>();
            vl.padding   = new RectOffset(8, 8, 8, 8);
            vl.spacing   = 6f;
            vl.childForceExpandWidth  = true;
            vl.childForceExpandHeight = false;
            vl.childAlignment = TextAnchor.UpperCenter;

            // ── Preset dropdown
            AddLabel(panel, "PRESET");
            _presetDropdown = AddDropdown(panel, new string[] { "Loading…" }, OnPresetChanged);
            AddLabel(panel, "CAMERA");
            _cameraDropdown = AddDropdown(panel, new[] { "Chase", "Overhead", "Ground Level" }, OnCameraChanged);

            // ── Play-rate slider
            AddLabel(panel, "PLAY RATE");
            var rateRow = AddRow(panel);
            _rateLabel = AddText(rateRow, "1×", 13);
            _rateSlider = rateRow.AddComponent<Slider>();
            // Override Slider's own RectTransform
            var rateRT = rateRow.GetComponent<RectTransform>();
            rateRT.sizeDelta = new Vector2(0f, 24f);

            // Proper Slider: create the full slider GO manually
            _rateSlider = CreateSlider(panel, 0, PlayRates.Length - 1, 1, v =>
            {
                int idx = Mathf.RoundToInt(v);
                _playRate = PlayRates[idx];
                if (_rateLabel != null) _rateLabel.text = PlayRateLabels[idx];
                if (controller != null && controller.GetComponentInChildren<BallAnimator>() is BallAnimator ba)
                    ba.PlayRate = _playRate;
            });

            // ── Buttons row 1
            var row1 = AddRow(panel);
            row1.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 32f);
            row1.AddComponent<HorizontalLayoutGroup>().spacing = 4f;
            AddButton(row1, "Fire",         () => FireSelected());
            AddButton(row1, "Fire & Compare", () => FireCompare());

            // ── Buttons row 2
            var row2 = AddRow(panel);
            row2.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 32f);
            row2.AddComponent<HorizontalLayoutGroup>().spacing = 4f;
            AddButton(row2, "Fire ×5 (det.)", () => FireRepeatability());
            AddButton(row2, "Clear",           () => controller?.Clear());

            // ── Determinism badge
            _deterLabel = AddText(panel, "", 13);
            _deterLabel.color = Color.white;

            // ── Readout panel
            _readoutText = AddText(panel, "No shot fired.", 12);
            var rdRT = _readoutText.GetComponent<RectTransform>();
            rdRT.sizeDelta = new Vector2(0f, 110f);

            // ── Notes footer
            _notesText = AddText(panel, "", 11);
            _notesText.color = new Color(0.8f, 0.8f, 0.8f, 1f);
        }

        void PopulatePresets()
        {
            _presets.Clear();
            _presets.AddRange(ShotPresetCatalog.ForScene(_scene));
            if (_presetDropdown == null) return;

            _presetDropdown.ClearOptions();
            var opts = new List<string>();
            foreach (var p in _presets) opts.Add(p.DisplayName);
            _presetDropdown.AddOptions(opts);
            _selectedIndex = 0;
            UpdateNotes();
        }

        void SubscribeController()
        {
            if (controller == null) return;
            controller.OnShotFired         += ShowReadout;
            controller.OnRepeatabilityResult += ShowDeterminism;
        }

        void OnDestroy()
        {
            if (controller == null) return;
            controller.OnShotFired         -= ShowReadout;
            controller.OnRepeatabilityResult -= ShowDeterminism;
        }

        // ── Button callbacks ───────────────────────────────────────────────────

        void FireSelected()
        {
            if (controller == null || _selectedIndex >= _presets.Count) return;
            var ba = controller.GetComponentInChildren<BallAnimator>();
            if (ba != null) ba.PlayRate = _playRate;
            controller.Fire(_presets[_selectedIndex]);
        }

        void FireCompare()
        {
            if (controller == null || _selectedIndex >= _presets.Count) return;
            var ba = controller.GetComponentInChildren<BallAnimator>();
            if (ba != null) ba.PlayRate = _playRate;
            controller.FireCompare(_presets[_selectedIndex]);
        }

        void FireRepeatability()
        {
            if (controller == null || _selectedIndex >= _presets.Count) return;
            controller.FireRepeatability(_presets[_selectedIndex], 5);
        }

        void OnPresetChanged(int idx)
        {
            _selectedIndex = idx;
            UpdateNotes();
        }

        void OnCameraChanged(int idx)
        {
            var cam = controller?.GetComponentInChildren<ChaseCamera>();
            if (cam == null) return;
            cam.SetMode((ChaseCamera.Mode)idx);
        }

        void UpdateNotes()
        {
            if (_notesText == null || _selectedIndex >= _presets.Count) return;
            _notesText.text = _presets.Count > 0 ? _presets[_selectedIndex].Notes : "";
        }

        // ── UI builder helpers ─────────────────────────────────────────────────

        static TMP_Text AddLabel(GameObject parent, string text)
        {
            var go = new GameObject("Label_" + text, typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(parent.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0f, 18f);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text      = text;
            tmp.fontSize  = 12;
            tmp.color     = new Color(0.7f, 0.7f, 0.7f, 1f);
            return tmp;
        }

        static TMP_Text AddText(GameObject parent, string text, float size)
        {
            var go = new GameObject("Text_" + text.Substring(0, Mathf.Min(12, text.Length)),
                                    typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(parent.transform, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text     = text;
            tmp.fontSize = size;
            tmp.color    = Color.white;
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = size * 1.5f;
            return tmp;
        }

        static TMP_Dropdown AddDropdown(GameObject parent, string[] options, UnityEngine.Events.UnityAction<int> cb)
        {
            var go  = new GameObject("Dropdown", typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 28f);
            var dd  = go.AddComponent<TMP_Dropdown>();
            dd.AddOptions(new List<string>(options));
            dd.onValueChanged.AddListener(cb);
            return dd;
        }

        static Button AddButton(GameObject parent, string label, UnityEngine.Events.UnityAction cb)
        {
            var go  = new GameObject("Btn_" + label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent.transform, false);
            var rt  = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0f, 28f);
            go.GetComponent<Image>().color = new Color(0.25f, 0.45f, 0.7f, 1f);
            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(cb);
            var le  = go.AddComponent<LayoutElement>();
            le.flexibleWidth = 1f;

            var txtGO = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer));
            txtGO.transform.SetParent(go.transform, false);
            var txtRT = txtGO.GetComponent<RectTransform>();
            txtRT.anchorMin = Vector2.zero; txtRT.anchorMax = Vector2.one;
            txtRT.offsetMin = txtRT.offsetMax = Vector2.zero;
            var tmp = txtGO.AddComponent<TextMeshProUGUI>();
            tmp.text      = label;
            tmp.fontSize  = 11;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color     = Color.white;

            return btn;
        }

        static Slider CreateSlider(GameObject parent, float min, float max, float defaultVal,
                                   UnityEngine.Events.UnityAction<float> cb)
        {
            var go = new GameObject("Slider", typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0f, 24f);

            // Background
            var bg = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            bg.transform.SetParent(go.transform, false);
            var bgRT = bg.GetComponent<RectTransform>();
            bgRT.anchorMin = new Vector2(0, 0.25f); bgRT.anchorMax = new Vector2(1, 0.75f);
            bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;
            bg.GetComponent<Image>().color = new Color(0.3f, 0.3f, 0.3f, 1f);

            // Fill area
            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(go.transform, false);
            var faRT = fillArea.GetComponent<RectTransform>();
            faRT.anchorMin = new Vector2(0, 0.25f); faRT.anchorMax = new Vector2(1, 0.75f);
            faRT.offsetMin = faRT.offsetMax = Vector2.zero;

            var fill = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fill.transform.SetParent(fillArea.transform, false);
            var fillRT = fill.GetComponent<RectTransform>();
            fillRT.anchorMin = Vector2.zero; fillRT.anchorMax = new Vector2(0.5f, 1f);
            fill.GetComponent<Image>().color = new Color(0.3f, 0.6f, 1f, 1f);

            // Handle
            var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(go.transform, false);
            var haRT = handleArea.GetComponent<RectTransform>();
            haRT.anchorMin = Vector2.zero; haRT.anchorMax = Vector2.one;
            haRT.offsetMin = haRT.offsetMax = Vector2.zero;

            var handle = new GameObject("Handle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            handle.transform.SetParent(handleArea.transform, false);
            var hRT = handle.GetComponent<RectTransform>();
            hRT.sizeDelta = new Vector2(20f, 0f);
            handle.GetComponent<Image>().color = Color.white;

            var slider = go.AddComponent<Slider>();
            slider.fillRect    = fill.GetComponent<RectTransform>();
            slider.handleRect  = handle.GetComponent<RectTransform>();
            slider.minValue    = min;
            slider.maxValue    = max;
            slider.value       = defaultVal;
            slider.wholeNumbers = true;
            slider.onValueChanged.AddListener(cb);

            return slider;
        }

        static GameObject AddRow(GameObject parent)
        {
            var go = new GameObject("Row", typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            return go;
        }
    }
}
