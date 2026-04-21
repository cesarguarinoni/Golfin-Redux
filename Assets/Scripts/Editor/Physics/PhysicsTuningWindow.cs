using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Golfin.Physics;
using Golfin.Physics.Math;
using Golfin.Physics.Runtime;

namespace Golfin.Physics.Editor
{
    /// <summary>
    /// Window > Physics > Tuning — tweak aero coefficients and validate carries against Trackman targets.
    /// </summary>
    public class PhysicsTuningWindow : EditorWindow
    {
        [MenuItem("Window/Physics/Tuning")]
        public static void ShowWindow() => GetWindow<PhysicsTuningWindow>("Physics Tuning");

        // Constant-mode coefficients
        private float _dragCoeff = 0.25f;
        private float _liftCoeffBase = 0.20f;
        private float _spinRateRef = 300f;

        // LUT mode toggles
        private bool _useDragLut = true;
        private bool _useLiftLut = true;

        // Tolerance for pass/fail colouring
        private float _passTolerance = 5f;

        private List<ClubSpec> _clubs = new List<ClubSpec>();
        private List<float> _actualCarries = new List<float>();  // yards, -1 = not yet run
        private string _lastMode = "";

        private CoefficientLut _dragLut;
        private CoefficientLut _liftLut;

        private bool _showDragLutTable = false;
        private bool _showLiftLutTable = false;

        // Surfaces section
        private bool _showSurfacesSection = true;
        private SurfaceConfig _surfaceConfig = SurfaceConfig.Default;
        private string _dropTestResultText = "";

        // Wind section
        private bool _showWindSection = true;
        private float _windBaseX = 0f;
        private float _windBaseY = 0f;
        private float _windBaseZ = 0f;
        private float _windGustAmplitude = 0f;
        private float _windGustFrequency = 0.5f;
        private float _windAltitudeFactor = 0f;
        private uint  _windSeed = 0;
        private string _windPreviewText = "";

        private Vector2 _scroll;

        void OnEnable() => ReloadCSVs();

        void OnGUI()
        {
            EditorGUILayout.LabelField("Aerodynamic Coefficients (Constant Mode)", EditorStyles.boldLabel);
            _dragCoeff    = EditorGUILayout.Slider("Drag Coefficient (Cd)",       _dragCoeff,    0.10f, 0.40f);
            _liftCoeffBase = EditorGUILayout.Slider("Lift Coefficient Base (Cl)",  _liftCoeffBase, 0.10f, 0.35f);
            _spinRateRef  = EditorGUILayout.Slider("Spin Rate Reference (rad/s)", _spinRateRef,  100f,  500f);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("LUT Mode", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            _useDragLut = EditorGUILayout.Toggle("Use Drag LUT (Cd vs speed)", _useDragLut);
            if (EditorGUI.EndChangeCheck() && _actualCarries.Count > 0) RunValidation();

            if (_dragLut.IsValid)
            {
                _showDragLutTable = EditorGUILayout.Foldout(_showDragLutTable, $"Drag LUT ({_dragLut.X.Length} rows)");
                if (_showDragLutTable) DrawLutTable(_dragLut, "speed (m/s)", "Cd");
            }
            else
            {
                EditorGUILayout.HelpBox("Drag LUT not loaded (aero_drag_lut.csv missing or invalid)", MessageType.Warning);
            }

            EditorGUILayout.Space();

            EditorGUI.BeginChangeCheck();
            _useLiftLut = EditorGUILayout.Toggle("Use Lift LUT (Cl vs spin param S)", _useLiftLut);
            if (EditorGUI.EndChangeCheck() && _actualCarries.Count > 0) RunValidation();

            if (_liftLut.IsValid)
            {
                _showLiftLutTable = EditorGUILayout.Foldout(_showLiftLutTable, $"Lift LUT ({_liftLut.X.Length} rows)");
                if (_showLiftLutTable) DrawLutTable(_liftLut, "S = r·ω/|v|", "Cl");
            }
            else
            {
                EditorGUILayout.HelpBox("Lift LUT not loaded (aero_lift_lut.csv missing or invalid)", MessageType.Warning);
            }

            EditorGUILayout.Space();
            _passTolerance = EditorGUILayout.Slider("Pass Tolerance %", _passTolerance, 5f, 15f);

            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Run Validation")) RunValidation();
            if (GUILayout.Button("Reload CSVs"))   ReloadCSVs();
            if (GUILayout.Button("Save aero.csv")) SaveAeroCSV();
            EditorGUILayout.EndHorizontal();

            // ── Wind Section ──────────────────────────────────────────────
            EditorGUILayout.Space();
            _showWindSection = EditorGUILayout.Foldout(_showWindSection, "Wind", toggleOnLabelClick: true);
            if (_showWindSection)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("Base Velocity (m/s)", EditorStyles.boldLabel);
                _windBaseX = EditorGUILayout.FloatField("Base X (east+)", _windBaseX);
                _windBaseY = EditorGUILayout.FloatField("Base Y (up+)",   _windBaseY);
                _windBaseZ = EditorGUILayout.FloatField("Base Z (north+)", _windBaseZ);

                EditorGUILayout.Space();
                _windGustAmplitude  = EditorGUILayout.Slider("Gust Amplitude (0–0.5)", _windGustAmplitude,  0f, 0.5f);
                _windGustFrequency  = EditorGUILayout.Slider("Gust Frequency Hz",       _windGustFrequency, 0.1f, 2f);
                _windAltitudeFactor = EditorGUILayout.FloatField("Altitude Factor",     _windAltitudeFactor);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel("Seed");
                uint newSeed;
                if (uint.TryParse(EditorGUILayout.TextField(_windSeed.ToString()), out newSeed))
                    _windSeed = newSeed;
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Reload wind.csv")) ReloadWindCSV();
                if (GUILayout.Button("Save wind.csv"))   SaveWindCSV();
                if (GUILayout.Button("Preview at (0,0,0)")) PreviewWind();
                EditorGUILayout.EndHorizontal();

                if (_windPreviewText.Length > 0)
                    EditorGUILayout.HelpBox(_windPreviewText, MessageType.Info);

                EditorGUI.indentLevel--;
            }

            // ── Surfaces Section ──────────────────────────────────────────────────────
            EditorGUILayout.Space();
            _showSurfacesSection = EditorGUILayout.Foldout(_showSurfacesSection, "Surfaces", toggleOnLabelClick: true);
            if (_showSurfacesSection)
            {
                EditorGUI.indentLevel++;
                var surfaceNames = System.Enum.GetNames(typeof(SurfaceType));
                for (int i = 0; i < surfaceNames.Length && i < _surfaceConfig.Coefficients.Length; i++)
                {
                    EditorGUILayout.LabelField(surfaceNames[i], EditorStyles.boldLabel);
                    EditorGUI.indentLevel++;
                    var c = _surfaceConfig.Coefficients[i];
                    c.Restitution       = fp.FromFloat(EditorGUILayout.Slider("Restitution (Cr)",        c.Restitution.ToFloat(),       0f, 1f));
                    c.TangentFriction   = fp.FromFloat(EditorGUILayout.Slider("Tangent Friction (μ)",    c.TangentFriction.ToFloat(),   0f, 1f));
                    c.RollingResistance = fp.FromFloat(EditorGUILayout.Slider("Rolling Resistance (1/s)",c.RollingResistance.ToFloat(), 0f, 2f));
                    c.StopSpeed         = fp.FromFloat(EditorGUILayout.Slider("Stop Speed (m/s)",        c.StopSpeed.ToFloat(),         0f, 1f));
                    _surfaceConfig.Coefficients[i] = c;
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.Space();
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Reload surfaces.csv"))
                {
                    _surfaceConfig = PhysicsConfigLoader.LoadSurfaceConfig();
                    _dropTestResultText = "";
                    Repaint();
                }
                if (GUILayout.Button("Simulate drop test"))
                    RunDropTest();
                EditorGUILayout.EndHorizontal();

                if (_dropTestResultText.Length > 0)
                    EditorGUILayout.HelpBox(_dropTestResultText, MessageType.Info);

                EditorGUI.indentLevel--;
            }

            if (_lastMode.Length > 0)
                EditorGUILayout.LabelField($"Last run: {_lastMode}", EditorStyles.miniLabel);

            EditorGUILayout.Space();

            // Club table
            EditorGUILayout.LabelField("Club Carry Validation", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Club",          GUILayout.Width(120));
            GUILayout.Label("Speed (m/s)",   GUILayout.Width(80));
            GUILayout.Label("Angle (°)",     GUILayout.Width(70));
            GUILayout.Label("Spin (rpm)",    GUILayout.Width(80));
            GUILayout.Label("Expected (yd)", GUILayout.Width(90));
            GUILayout.Label("Actual (yd)",   GUILayout.Width(80));
            GUILayout.Label("Error %",       GUILayout.Width(60));
            GUILayout.Label("Mode",          GUILayout.Width(50));
            EditorGUILayout.EndHorizontal();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            for (int i = 0; i < _clubs.Count; i++)
            {
                var c = _clubs[i];
                float actual   = i < _actualCarries.Count ? _actualCarries[i] : -1f;
                float expected = c.ExpectedCarryYd.ToFloat();
                float errPct   = actual >= 0f && expected > 0f ? Mathf.Abs(actual - expected) / expected * 100f : -1f;

                if (errPct >= 0f)
                {
                    Color rowColor = errPct <= _passTolerance ? Color.green
                                   : errPct <= _passTolerance * 2f ? Color.yellow
                                   : Color.red;
                    var orig = GUI.color;
                    GUI.color = rowColor;
                    EditorGUILayout.BeginHorizontal();
                    GUI.color = orig;
                }
                else
                {
                    EditorGUILayout.BeginHorizontal();
                }

                string modeLabel = actual >= 0f
                    ? ((_useDragLut && _dragLut.IsValid ? "D" : "d") + (_useLiftLut && _liftLut.IsValid ? "L" : "l"))
                    : "—";

                GUILayout.Label(c.Id,                                GUILayout.Width(120));
                GUILayout.Label($"{c.BallSpeedMps:F1}",              GUILayout.Width(80));
                GUILayout.Label($"{c.LaunchAngleDeg:F1}",            GUILayout.Width(70));
                GUILayout.Label($"{c.SpinRateRpm:F0}",               GUILayout.Width(80));
                GUILayout.Label($"{expected:F0}",                    GUILayout.Width(90));
                GUILayout.Label(actual >= 0f ? $"{actual:F0}" : "—", GUILayout.Width(80));
                GUILayout.Label(errPct >= 0f ? $"{errPct:F1}%" : "—", GUILayout.Width(60));
                GUILayout.Label(modeLabel,                           GUILayout.Width(50));
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Mode legend: D=DragLUT active  d=constant Cd  L=LiftLUT active  l=linear Cl", EditorStyles.miniLabel);
        }

        private void DrawLutTable(CoefficientLut lut, string xLabel, string yLabel)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(xLabel, GUILayout.Width(120));
            GUILayout.Label(yLabel, GUILayout.Width(80));
            EditorGUILayout.EndHorizontal();
            for (int i = 0; i < lut.X.Length; i++)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label($"{lut.X[i].ToFloat():F4}", GUILayout.Width(120));
                GUILayout.Label($"{lut.Y[i].ToFloat():F4}", GUILayout.Width(80));
                EditorGUILayout.EndHorizontal();
            }
            EditorGUI.indentLevel--;
        }

        private void ReloadCSVs()
        {
            _clubs = PhysicsConfigLoader.LoadClubSpecs();
            _dragLut = PhysicsConfigLoader.LoadDragLut();
            _liftLut = PhysicsConfigLoader.LoadLiftLut();
            _surfaceConfig = PhysicsConfigLoader.LoadSurfaceConfig();
            _actualCarries = new List<float>(new float[_clubs.Count]);
            for (int i = 0; i < _actualCarries.Count; i++) _actualCarries[i] = -1f;
            Repaint();
        }

        private void RunDropTest()
        {
            // Ball dropped from 30m above origin, zero horizontal velocity, 3000 rpm backspin.
            float rps  = 3000f * 2f * Mathf.PI / 60f;
            var origin = new fp3(fp.Zero, fp.FromInt(30), fp.Zero);
            var vel    = fp3.Zero;
            var spin   = new SpinState(new fp3(fp.FromInt(-1), fp.Zero, fp.Zero), fp.FromFloat(rps));
            var input  = new ShotInput(origin, vel, fp.FromInt(30), spin);
            var aero   = BuildConfig();
            var traj   = BallSimulation.Simulate(
                input, new FlatGround(fp.Zero), aero, WindConfig.Calm,
                new ConstantSurfaceProvider(SurfaceType.Green), _surfaceConfig);

            float stopDist = Mathf.Sqrt(
                traj.finalPosition.x.ToFloat() * traj.finalPosition.x.ToFloat() +
                traj.finalPosition.z.ToFloat() * traj.finalPosition.z.ToFloat());
            int bounces = 0;
            foreach (var h in traj.terrainHits) if (!h.IsStop) bounces++;

            _dropTestResultText =
                $"Termination: {traj.termination}  |  Bounces: {bounces}  |  " +
                $"Stop dist from drop: {stopDist:F2} m  |  " +
                $"Final pos: ({traj.finalPosition.x.ToFloat():F2}, {traj.finalPosition.z.ToFloat():F2})";
            Repaint();
        }

        private void RunValidation()
        {
            var cfg = BuildConfig();
            _actualCarries = new List<float>();
            string modeStr = (_useDragLut && _dragLut.IsValid ? "DragLUT" : "const-Cd")
                           + " + " + (_useLiftLut && _liftLut.IsValid ? "LiftLUT" : "linear-Cl");
            _lastMode = modeStr;

            foreach (var club in _clubs)
            {
                float angleRad = club.LaunchAngleDeg.ToFloat() * Mathf.Deg2Rad;
                float spd      = club.BallSpeedMps.ToFloat();
                var origin     = new fp3(fp.Zero, fp.Zero, fp.Zero);
                var velocity   = new fp3(
                    fp.Zero,
                    fp.FromDouble(spd * Mathf.Sin(angleRad)),
                    fp.FromDouble(spd * Mathf.Cos(angleRad)));

                float rps = club.SpinRateRpm.ToFloat() * 2f * Mathf.PI / 60f;
                var spin  = new SpinState(new fp3(fp.FromInt(-1), fp.Zero, fp.Zero), fp.FromFloat(rps));
                var input = new ShotInput(origin, velocity, fp.FromInt(30), spin);
                var traj  = BallSimulation.Simulate(input, new FlatGround(fp.Zero), cfg);

                float carryM  = traj.finalPosition.z.ToFloat();
                float carryYd = carryM * 1.09361f;
                _actualCarries.Add(carryYd);
            }
            Repaint();
        }

        private AeroConfig BuildConfig()
        {
            var cfg = PhysicsConfigLoader.LoadAeroConfig();
            // Override scalar coefficients with slider values
            cfg.DragCoefficient     = fp.FromFloat(_dragCoeff);
            cfg.LiftCoefficientBase = fp.FromFloat(_liftCoeffBase);
            cfg.SpinRateReference   = fp.FromFloat(_spinRateRef);
            // Override LUT mode flags with checkbox values
            cfg.UseDragLut = _useDragLut;
            cfg.UseLiftLut = _useLiftLut;
            return cfg;
        }

        private WindConfig BuildWindConfig()
        {
            return new WindConfig
            {
                BaseVelocity      = new Golfin.Physics.Math.fp3(
                    Golfin.Physics.Math.fp.FromFloat(_windBaseX),
                    Golfin.Physics.Math.fp.FromFloat(_windBaseY),
                    Golfin.Physics.Math.fp.FromFloat(_windBaseZ)),
                GustAmplitude     = Golfin.Physics.Math.fp.FromFloat(_windGustAmplitude),
                GustFrequency     = Golfin.Physics.Math.fp.FromFloat(_windGustFrequency),
                AltitudeFactor    = Golfin.Physics.Math.fp.FromFloat(_windAltitudeFactor),
                AltitudeRefMeters = Golfin.Physics.Math.fp.FromInt(10),
                Seed              = _windSeed,
            };
        }

        private void ReloadWindCSV()
        {
            var cfg = Golfin.Physics.Runtime.PhysicsConfigLoader.LoadWindConfig();
            _windBaseX           = cfg.BaseVelocity.x.ToFloat();
            _windBaseY           = cfg.BaseVelocity.y.ToFloat();
            _windBaseZ           = cfg.BaseVelocity.z.ToFloat();
            _windGustAmplitude   = cfg.GustAmplitude.ToFloat();
            _windGustFrequency   = cfg.GustFrequency.ToFloat();
            _windAltitudeFactor  = cfg.AltitudeFactor.ToFloat();
            _windSeed            = cfg.Seed;
            _windPreviewText     = "";
            Repaint();
            Debug.Log("[PhysicsTuning] wind.csv reloaded");
        }

        private void PreviewWind()
        {
            var cfg = BuildWindConfig();
            var w   = WindModel.SampleWind(
                Golfin.Physics.Math.fp3.Zero,
                Golfin.Physics.Math.fp.Zero,
                cfg);
            float mag = Mathf.Sqrt(w.x.ToFloat() * w.x.ToFloat() + w.y.ToFloat() * w.y.ToFloat() + w.z.ToFloat() * w.z.ToFloat());
            _windPreviewText = $"Wind at (0,0,0) t=0 → ({w.x.ToFloat():F3}, {w.y.ToFloat():F3}, {w.z.ToFloat():F3}) m/s  |w|={mag:F3} m/s";
            Repaint();
        }

        private void SaveWindCSV()
        {
            string path = Application.dataPath + "/Resources/Physics/wind.csv";
            string content =
                "key,value,units,notes\n" +
                $"base_x,{_windBaseX:F4},m/s,east-positive steady wind component\n" +
                $"base_y,{_windBaseY:F4},m/s,vertical wind (updraft/downdraft); usually 0\n" +
                $"base_z,{_windBaseZ:F4},m/s,north-positive steady wind component\n" +
                $"gust_amplitude,{_windGustAmplitude:F4},dimensionless,0=calm 0.2=moderate gusts\n" +
                $"gust_frequency,{_windGustFrequency:F4},Hz,gust oscillation rate\n" +
                $"altitude_factor,{_windAltitudeFactor:F4},dimensionless,0=no altitude profile\n" +
                $"altitude_ref_meters,10.0,m,altitude reference\n" +
                $"seed,{_windSeed},uint,PRNG seed for gust phase\n";
            File.WriteAllText(path, content);
            AssetDatabase.Refresh();
            Debug.Log($"[PhysicsTuning] Saved wind.csv: base=({_windBaseX:F2},{_windBaseY:F2},{_windBaseZ:F2}) gust={_windGustAmplitude:F2} seed={_windSeed}");
        }

        private void SaveAeroCSV()
        {
            string path = Application.dataPath + "/Resources/Physics/aero.csv";
            string content =
                "key,value,units,notes\n" +
                $"air_density,1.225,kg/m^3,sea-level 15C\n" +
                $"ball_mass,0.04593,kg,USGA max\n" +
                $"ball_cross_section,0.001432,m^2,radius 0.02135m\n" +
                $"ball_radius,0.02135,m,for spin parameter S = r·ω/v\n" +
                $"drag_coefficient,{_dragCoeff:F4},dimensionless,constant-mode Cd (LUT fallback)\n" +
                $"lift_coefficient_base,{_liftCoeffBase:F4},dimensionless,constant-mode Cl base (LUT fallback)\n" +
                $"spin_rate_reference,{_spinRateRef:F1},rad/s,constant-mode only\n" +
                $"lift_max_multiplier,1.5,dimensionless,constant-mode only\n" +
                $"use_drag_lut,{(_useDragLut ? 1 : 0)},bool,1=velocity-indexed Cd LUT 0=constant Cd\n" +
                $"use_lift_lut,{(_useLiftLut ? 1 : 0)},bool,1=spin-parameter Cl LUT 0=linear-capped Cl\n";
            File.WriteAllText(path, content);
            AssetDatabase.Refresh();
            Debug.Log($"[PhysicsTuning] Saved aero.csv: Cd={_dragCoeff:F4} Cl={_liftCoeffBase:F4} dragLUT={_useDragLut} liftLUT={_useLiftLut}");
        }
    }
}
