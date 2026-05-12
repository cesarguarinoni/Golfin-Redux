using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Golfin.Physics;
using Golfin.Gameplay.Input;

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
        TMP_Text _releaseFireLabel;
        TMP_Text _predictionLabel;

        // Club picker
        int      _clubIndex;
        TMP_Text _clubLabel;

        // Place ball picker
        int      _placementIndex;
        TMP_Text _placeBallLabel;

        // Debug flags panel
        bool       _debugPanelOpen;
        GameObject _debugContainer;
        TMP_Text[] _debugFlagLabels = new TMP_Text[9];

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
            _deterLabel.gameObject.SetActive(true);
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

            // Club cycler (manual override; sync'd when preset changes)
            AddLabel(panel, "CLUB");
            _clubLabel = AddCyclePicker(panel, PhysicsLabController.LabClubLabels[0],
                () => CycleClub(-1), () => CycleClub(+1));

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
            var predBtn = AddButton(row2, "Prediction: OFF", () => TogglePrediction());
            _predictionLabel = predBtn.GetComponentInChildren<TMP_Text>();
            var row3 = MakeButtonRow(panel);
            AddButton(row3, "Reset to Tee", () => controller?.ResetToTee());

            // Release-to-fire toggle (prominent row)
            var row4 = MakeButtonRow(panel);
            _releaseFireLabel = null;
            var releaseBtn = AddButton(row4, "Release Fire: OFF", () => ToggleReleaseFire());
            _releaseFireLabel = releaseBtn.GetComponentInChildren<TMP_Text>();

            // ── Place Ball section ─────────────────────────────────────────────
            AddLabel(panel, "PLACE BALL");
            _placeBallLabel = AddCyclePicker(panel, "— no hole loaded —",
                () => CyclePlacement(-1), () => CyclePlacement(+1));
            var placeRow = MakeButtonRow(panel);
            AddButton(placeRow, "Place Here", () => DoPlaceBall());
            AddButton(placeRow, "Reset to Tee", () => controller?.ResetToTee());

            // ── Determinism result — hidden until Fire×5 runs ─────────────────
            _deterLabel = AddText(panel, "", 16f);
            _deterLabel.color = Color.white;
            _deterLabel.gameObject.SetActive(false);

            // Readout — 7 lines at ~22px each; Truncate prevents overflow
            _readoutText = AddText(panel, "No shot fired.", 14f);
            _readoutText.overflowMode = TMPro.TextOverflowModes.Truncate;
            var rdLE = EnsureLE(_readoutText.gameObject);
            rdLE.preferredHeight = 155f;

            // Notes — 2–3 lines of small grey text
            _notesText = AddText(panel, "", 12f);
            _notesText.color = new Color(0.75f, 0.75f, 0.75f, 1f);
            _notesText.overflowMode = TMPro.TextOverflowModes.Truncate;
            EnsureLE(_notesText.gameObject).preferredHeight = 55f;

            // ── Debug Flags foldout ────────────────────────────────────────────
            var dbgHeaderRow = MakeButtonRow(panel);
            AddButton(dbgHeaderRow, "▶ DEBUG FLAGS", () => ToggleDebugPanel());

            _debugContainer = new GameObject("DebugFlagsPanel", typeof(RectTransform));
            _debugContainer.transform.SetParent(panel.transform, false);
            var vl2 = _debugContainer.AddComponent<VerticalLayoutGroup>();
            vl2.spacing = 4f;
            vl2.childForceExpandWidth  = true;
            vl2.childForceExpandHeight = false;
            vl2.childAlignment = TextAnchor.UpperCenter;
            EnsureLE(_debugContainer).flexibleHeight = 1f;

            // 9 flag toggle buttons (8 standard + PuttPathHeatmap)
            string[] flagNames = {
                "Show Cone Outline", "Show Arrow Trail", "Cancel On Slow Flick",
                "Single Pass Mode",  "Disable Overpower", "Disable Cone Fine Tune",
                "Force Perfect Timing", "Force Perfect Aim", "Putt Path Heatmap"
            };
            for (int i = 0; i < 9; i++)
            {
                int idx = i; // capture
                var flagRow = MakeButtonRow(_debugContainer);
                bool initVal = GetDebugFlag(ShotDebugFlags.Defaults, idx);
                var btn = AddButton(flagRow, $"{flagNames[idx]}: {BoolLabel(initVal)}", () => ToggleFlag(idx));
                _debugFlagLabels[idx] = btn.GetComponentInChildren<TMP_Text>();
            }

            // Reset defaults button
            var resetRow = MakeButtonRow(_debugContainer);
            AddButton(resetRow, "Reset Defaults", () => ResetDebugDefaults());

            _debugContainer.SetActive(false);
        }

        // ── Debug panel helpers ────────────────────────────────────────────────

        void ToggleDebugPanel()
        {
            _debugPanelOpen = !_debugPanelOpen;
            if (_debugContainer != null) _debugContainer.SetActive(_debugPanelOpen);
            RefreshDebugLabels();
        }

        void ToggleFlag(int flagIndex)
        {
            var sc = GetShotController();
            if (sc == null) return;
            var flags = sc.DebugFlags;
            bool newVal = !GetDebugFlag(flags, flagIndex);
            SetDebugFlag(ref flags, flagIndex, newVal);
            sc.DebugFlags = flags;
            if (_debugFlagLabels[flagIndex] != null)
            {
                string[] names = {
                    "Show Cone Outline", "Show Arrow Trail", "Cancel On Slow Flick",
                    "Single Pass Mode",  "Disable Overpower", "Disable Cone Fine Tune",
                    "Force Perfect Timing", "Force Perfect Aim", "Putt Path Heatmap"
                };
                _debugFlagLabels[flagIndex].text = $"{names[flagIndex]}: {BoolLabel(newVal)}";
            }
        }

        void ResetDebugDefaults()
        {
            var sc = GetShotController();
            if (sc == null) return;
            sc.DebugFlags = ShotDebugFlags.Defaults;
            RefreshDebugLabels();
        }

        void RefreshDebugLabels()
        {
            var sc = GetShotController();
            if (sc == null) return;
            var flags = sc.DebugFlags;
            string[] names = {
                "Show Cone Outline", "Show Arrow Trail", "Cancel On Slow Flick",
                "Single Pass Mode",  "Disable Overpower", "Disable Cone Fine Tune",
                "Force Perfect Timing", "Force Perfect Aim", "Putt Path Heatmap"
            };
            for (int i = 0; i < 9; i++)
            {
                if (_debugFlagLabels[i] != null)
                    _debugFlagLabels[i].text = $"{names[i]}: {BoolLabel(GetDebugFlag(flags, i))}";
            }
        }

        static bool GetDebugFlag(ShotDebugFlags f, int idx) => idx switch
        {
            0 => f.ShowConeOutline,
            1 => f.ShowArrowTrail,
            2 => f.CancelOnSlowFlick,
            3 => f.SinglePassMode,
            4 => f.DisableOverpower,
            5 => f.DisableConeFineTune,
            6 => f.ForcePerfectTiming,
            7 => f.ForcePerfectAim,
            8 => f.PuttPathHeatmap,
            _ => false
        };

        static void SetDebugFlag(ref ShotDebugFlags f, int idx, bool val)
        {
            switch (idx)
            {
                case 0: f.ShowConeOutline     = val; break;
                case 1: f.ShowArrowTrail      = val; break;
                case 2: f.CancelOnSlowFlick   = val; break;
                case 3: f.SinglePassMode      = val; break;
                case 4: f.DisableOverpower    = val; break;
                case 5: f.DisableConeFineTune = val; break;
                case 6: f.ForcePerfectTiming  = val; break;
                case 7: f.ForcePerfectAim     = val; break;
                case 8: f.PuttPathHeatmap     = val; break;
            }
        }

        static string BoolLabel(bool v) => v ? "ON" : "OFF";

        ShotController GetShotController()
        {
            return controller != null
                ? controller.GetComponentInChildren<ShotController>(true)
                : null;
        }

        // ── Club picker helpers ────────────────────────────────────────────────

        void CycleClub(int dir)
        {
            int count = PhysicsLabController.LabClubLabels.Length;
            _clubIndex = (_clubIndex + dir + count) % count;
            ApplyClubIndex(_clubIndex);
        }

        void ApplyClubIndex(int index)
        {
            _clubIndex = index;
            if (_clubLabel != null) _clubLabel.text = PhysicsLabController.LabClubLabels[index];
            controller?.SetClub(index);
            controller?.RecomputeMaxCarry();
        }

        // ── Place ball helpers ─────────────────────────────────────────────────

        void CyclePlacement(int dir)
        {
            if (controller == null || controller.PlacementEntries.Count == 0) return;
            int count = controller.PlacementEntries.Count;
            _placementIndex = (_placementIndex + dir + count) % count;
            if (_placeBallLabel != null)
                _placeBallLabel.text = controller.PlacementEntries[_placementIndex].Label;
        }

        void DoPlaceBall()
        {
            if (controller == null || controller.PlacementEntries.Count == 0) return;
            _placementIndex = Mathf.Clamp(_placementIndex, 0, controller.PlacementEntries.Count - 1);
            var entry = controller.PlacementEntries[_placementIndex];
            controller.PlaceBallAt(entry.WorldPos, entry.PreferredSurfaceTypeValue);
        }

        void RefreshPlacementPicker()
        {
            if (_placeBallLabel == null) return;
            if (controller == null || controller.PlacementEntries.Count == 0)
            {
                _placeBallLabel.text = "— no hole loaded —";
                _placementIndex = 0;
            }
            else
            {
                _placementIndex = Mathf.Clamp(_placementIndex, 0, controller.PlacementEntries.Count - 1);
                _placeBallLabel.text = controller.PlacementEntries[_placementIndex].Label;
            }
        }

        // ── Other cycle helpers ────────────────────────────────────────────────

        void TogglePrediction()
        {
            if (controller == null) return;
            controller.TogglePrediction();
            bool on = controller.PredictionVisible;
            if (_predictionLabel != null)
                _predictionLabel.text = on ? "Prediction: ON" : "Prediction: OFF";
        }

        void ToggleReleaseFire()
        {
            if (controller == null) return;
            bool next = !controller.GetReleaseToFire();
            controller.SetReleaseToFire(next);
            if (_releaseFireLabel != null)
                _releaseFireLabel.text = next ? "Release Fire: ON" : "Release Fire: OFF";
        }

        void CyclePreset(int dir)
        {
            if (_presets.Count == 0) return;
            _selectedIndex = (_selectedIndex + dir + _presets.Count) % _presets.Count;
            if (_presetLabel != null) _presetLabel.text = _presets[_selectedIndex].DisplayName;
            ApplyClubFromPreset();
            UpdateNotes();
        }

        void ApplyClubFromPreset()
        {
            if (_presets.Count == 0 || controller == null) return;
            string name = _presets[_selectedIndex].DisplayName.ToLowerInvariant();
            int club = 0; // Driver default
            if (name.Contains("iron"))       club = 1;
            else if (name.Contains("wedge")) club = 2;
            else if (name.Contains("putt"))  club = 3;
            ApplyClubIndex(club);
        }

        void CycleCamera(int dir)
        {
            _cameraIndex = (_cameraIndex + dir + CameraLabels.Length) % CameraLabels.Length;
            if (_cameraLabel != null) _cameraLabel.text = CameraLabels[_cameraIndex];
            var cam = UnityEngine.Object.FindObjectOfType<ChaseCamera>();
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
            ApplyClubFromPreset();
            UpdateNotes();
        }

        void SubscribeController()
        {
            if (controller == null) return;
            controller.OnShotFired               += ShowReadout;
            controller.OnRepeatabilityResult     += ShowDeterminism;
            controller.OnPlacementEntriesChanged += RefreshPlacementPicker;
        }

        void OnDestroy()
        {
            if (controller == null) return;
            controller.OnShotFired               -= ShowReadout;
            controller.OnRepeatabilityResult     -= ShowDeterminism;
            controller.OnPlacementEntriesChanged -= RefreshPlacementPicker;
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
            rt.sizeDelta = new Vector2(360f, 800f);
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
            var midImg = mid.GetComponent<Image>();
            midImg.color = new Color(0.18f, 0.18f, 0.18f, 1f);
            midImg.raycastTarget = false;
            var midLE = EnsureLE(mid);
            midLE.preferredWidth = 250f;
            midLE.flexibleWidth  = 0f;
            midLE.minWidth       = 250f;

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
            tmp.richText  = false;
            tmp.raycastTarget = false;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Ellipsis;

            MakeCycleBtn(row, ">", onNext, 40f);
            return tmp;
        }

        static void MakeCycleBtn(GameObject parent, string label,
                                  UnityEngine.Events.UnityAction cb, float width)
        {
            var go = new GameObject("Btn_" + label,
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent.transform, false);
            var goRT = go.GetComponent<RectTransform>();
            goRT.anchorMin = new Vector2(0f, 0f);
            goRT.anchorMax = new Vector2(0f, 1f);
            goRT.pivot     = new Vector2(0.5f, 0.5f);
            go.GetComponent<Image>().color = new Color(0.3f, 0.3f, 0.3f, 1f);
            var le = EnsureLE(go);
            le.preferredWidth  = width;
            le.flexibleWidth   = 0f;
            le.preferredHeight = 36f;
            le.flexibleHeight  = 0f;
            go.AddComponent<Button>().onClick.AddListener(cb);
            var tgo = new GameObject("T", typeof(RectTransform), typeof(CanvasRenderer));
            tgo.transform.SetParent(go.transform, false);
            var trt = tgo.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = trt.offsetMax = Vector2.zero;
            var tmp = tgo.AddComponent<TextMeshProUGUI>();
            tmp.text = label; tmp.fontSize = 18f;
            tmp.richText = false;
            tmp.raycastTarget = false;
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
