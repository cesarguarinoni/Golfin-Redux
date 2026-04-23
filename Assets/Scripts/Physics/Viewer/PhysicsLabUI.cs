using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Golfin.Physics;

namespace Golfin.Physics.Viewer
{
    /// <summary>
    /// In-scene UI panel for the physics lab. Builds its own layout in Start().
    /// Uses cycle pickers instead of TMP_Dropdown (no template required).
    /// </summary>
    public class PhysicsLabUI : MonoBehaviour
    {
        [SerializeField] PhysicsLabController controller;
        [SerializeField] PresetScene initialScene = PresetScene.Range;

        PresetScene _scene;
        List<ShotPreset> _presets = new List<ShotPreset>();
        int   _selectedIndex;
        float _playRate = 1f;

        TMP_Text _presetLabel;
        TMP_Text _cameraLabel;
        TMP_Text _rateLabel;
        TMP_Text _readoutText;
        TMP_Text _deterLabel;
        TMP_Text _notesText;

        static readonly float[]  PlayRates      = { 0.25f, 1f, 4f, float.MaxValue };
        static readonly string[] PlayRateLabels = { "0.25×", "1×", "4×", "Instant" };
        static readonly string[] CameraLabels   = { "Chase", "Overhead", "Ground" };
        int _cameraIndex;
        int _rateIndex = 1;

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

        public void SetScene(PresetScene scene)
        {
            _scene = scene;
            PopulatePresets();
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

        void BuildUI()
        {
            var panel = MakePanel();

            // Preset cycler
            AddLabel(panel, "PRESET");
            _presetLabel = AddCyclePicker(panel, "Loading…",
                () => CyclePreset(-1), () => CyclePreset(+1));

            // Camera cycler
            AddLabel(panel, "CAMERA");
            _cameraLabel = AddCyclePicker(panel, CameraLabels[0],
                () => CycleCamera(-1), () => CycleCamera(+1));

            // Play-rate cycler
            AddLabel(panel, "PLAY RATE");
            _rateLabel = AddCyclePicker(panel, PlayRateLabels[_rateIndex],
                () => CycleRate(-1), () => CycleRate(+1));

            // Action buttons
            var row1 = MakeButtonRow(panel);
            AddButton(row1, "Fire",          () => FireSelected());
            AddButton(row1, "Fire & Compare", () => FireCompare());
            var row2 = MakeButtonRow(panel);
            AddButton(row2, "Fire ×5 (det.)", () => FireRepeatability());
            AddButton(row2, "Clear",           () => controller?.Clear());
            var row3 = MakeButtonRow(panel);
            AddButton(row3, "Reset to Tee", () => controller?.ResetToTee());

            _deterLabel = AddText(panel, "", 16f);
            _deterLabel.color = Color.white;

            _readoutText = AddText(panel, "No shot fired.", 15f);
            var rdLE = EnsureLE(_readoutText.gameObject);
            rdLE.preferredHeight = 130f;

            _notesText = AddText(panel, "", 13f);
            _notesText.color = new Color(0.75f, 0.75f, 0.75f, 1f);
        }

        // ── Cycle helpers ──────────────────────────────────────────────────────

        void CyclePreset(int dir)
        {
            if (_presets.Count == 0) return;
            _selectedIndex = (_selectedIndex + dir + _presets.Count) % _presets.Count;
            if (_presetLabel != null) _presetLabel.text = _presets[_selectedIndex].DisplayName;
            UpdateNotes();
        }

        void CycleCamera(int dir)
        {
            _cameraIndex = (_cameraIndex + dir + CameraLabels.Length) % CameraLabels.Length;
            if (_cameraLabel != null) _cameraLabel.text = CameraLabels[_cameraIndex];
            var cam = controller?.GetComponentInChildren<ChaseCamera>();
            cam?.SetMode((ChaseCamera.Mode)_cameraIndex);
        }

        void CycleRate(int dir)
        {
            _rateIndex = (_rateIndex + dir + PlayRates.Length) % PlayRates.Length;
            _playRate  = PlayRates[_rateIndex];
            if (_rateLabel != null) _rateLabel.text = PlayRateLabels[_rateIndex];
            var ba = controller?.GetComponentInChildren<BallAnimator>();
            if (ba != null) ba.PlayRate = _playRate;
        }

        void PopulatePresets()
        {
            _presets.Clear();
            _presets.AddRange(ShotPresetCatalog.ForScene(_scene));
            _selectedIndex = 0;
            if (_presetLabel != null)
                _presetLabel.text = _presets.Count > 0 ? _presets[0].DisplayName : "—";
            UpdateNotes();
        }

        void SubscribeController()
        {
            if (controller == null) return;
            controller.OnShotFired            += ShowReadout;
            controller.OnRepeatabilityResult  += ShowDeterminism;
        }

        void OnDestroy()
        {
            if (controller == null) return;
            controller.OnShotFired            -= ShowReadout;
            controller.OnRepeatabilityResult  -= ShowDeterminism;
        }

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

        void UpdateNotes()
        {
            if (_notesText == null || _presets.Count == 0) return;
            _notesText.text = _selectedIndex < _presets.Count ? _presets[_selectedIndex].Notes : "";
        }

        // ── UI builder helpers ─────────────────────────────────────────────────

        GameObject MakePanel()
        {
            var panel = new GameObject("LabUIPanel",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panel.transform.SetParent(transform, false);
            var rt = panel.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot     = Vector2.zero;
            rt.anchoredPosition = new Vector2(16f, 16f);
            rt.sizeDelta = new Vector2(360f, 560f);
            panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.72f);
            var vl = panel.AddComponent<VerticalLayoutGroup>();
            vl.padding   = new RectOffset(10, 10, 10, 10);
            vl.spacing   = 7f;
            vl.childForceExpandWidth  = true;
            vl.childForceExpandHeight = false;
            vl.childAlignment = TextAnchor.UpperCenter;
            return panel;
        }

        static void AddLabel(GameObject parent, string text)
        {
            var go = new GameObject("Lbl_" + text, typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(parent.transform, false);
            EnsureLE(go).preferredHeight = 20f;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text     = text;
            tmp.fontSize = 14f;
            tmp.color    = new Color(0.65f, 0.65f, 0.65f, 1f);
        }

        static TMP_Text AddText(GameObject parent, string text, float size)
        {
            var go = new GameObject("Txt", typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(parent.transform, false);
            EnsureLE(go).preferredHeight = size * 1.5f;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text     = text;
            tmp.fontSize = size;
            tmp.color    = Color.white;
            return tmp;
        }

        // Cycle picker: [<] label [>]
        static TMP_Text AddCyclePicker(GameObject parent, string initialText,
                                       UnityEngine.Events.UnityAction onPrev,
                                       UnityEngine.Events.UnityAction onNext)
        {
            var row = new GameObject("CyclePicker", typeof(RectTransform));
            row.transform.SetParent(parent.transform, false);
            EnsureLE(row).preferredHeight = 36f;
            var hl = row.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 4f;
            hl.childForceExpandHeight = true;
            hl.childForceExpandWidth  = false;
            row.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 36f);

            MakeCycleBtn(row, "<", onPrev, 40f);

            var mid = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            mid.transform.SetParent(row.transform, false);
            mid.GetComponent<Image>().color = new Color(0.18f, 0.18f, 0.18f, 1f);
            EnsureLE(mid).flexibleWidth = 1f;

            // TMP_Text must be on a child — a GameObject can only have one Graphic
            var midText = new GameObject("T", typeof(RectTransform), typeof(CanvasRenderer));
            midText.transform.SetParent(mid.transform, false);
            var mtRT = midText.GetComponent<RectTransform>();
            mtRT.anchorMin = Vector2.zero; mtRT.anchorMax = Vector2.one;
            mtRT.offsetMin = mtRT.offsetMax = Vector2.zero;
            var tmp = midText.AddComponent<TextMeshProUGUI>();
            tmp.text      = initialText;
            tmp.fontSize  = 15f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color     = Color.white;

            MakeCycleBtn(row, ">", onNext, 40f);
            return tmp;
        }

        static void MakeCycleBtn(GameObject parent, string label,
                                  UnityEngine.Events.UnityAction cb, float width)
        {
            var go = new GameObject("Btn_" + label,
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent.transform, false);
            go.GetComponent<Image>().color = new Color(0.3f, 0.3f, 0.3f, 1f);
            var le = EnsureLE(go); le.preferredWidth = width; le.flexibleWidth = 0f;
            go.AddComponent<Button>().onClick.AddListener(cb);
            var tgo = new GameObject("T", typeof(RectTransform), typeof(CanvasRenderer));
            tgo.transform.SetParent(go.transform, false);
            var trt = tgo.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = trt.offsetMax = Vector2.zero;
            var tmp = tgo.AddComponent<TextMeshProUGUI>();
            tmp.text = label; tmp.fontSize = 18f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
        }

        static GameObject MakeButtonRow(GameObject parent)
        {
            var row = new GameObject("BtnRow", typeof(RectTransform));
            row.transform.SetParent(parent.transform, false);
            EnsureLE(row).preferredHeight = 36f;
            var hl = row.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 5f;
            hl.childForceExpandHeight = true;
            hl.childForceExpandWidth  = true;
            return row;
        }

        static Button AddButton(GameObject parent, string label, UnityEngine.Events.UnityAction cb)
        {
            var go = new GameObject("Btn_" + label,
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent.transform, false);
            go.GetComponent<Image>().color = new Color(0.22f, 0.42f, 0.68f, 1f);
            EnsureLE(go).flexibleWidth = 1f;
            var btn = go.AddComponent<Button>(); btn.onClick.AddListener(cb);
            var tgo = new GameObject("T", typeof(RectTransform), typeof(CanvasRenderer));
            tgo.transform.SetParent(go.transform, false);
            var trt = tgo.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = trt.offsetMax = Vector2.zero;
            var tmp = tgo.AddComponent<TextMeshProUGUI>();
            tmp.text = label; tmp.fontSize = 14f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            return btn;
        }

        static LayoutElement EnsureLE(GameObject go)
        {
            var le = go.GetComponent<LayoutElement>();
            if (le == null) le = go.AddComponent<LayoutElement>();
            return le;
        }
    }
}
