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

        // Live in-memory config (editable via sliders)
        private float _dragCoeff = 0.25f;
        private float _liftCoeffBase = 0.20f;
        private float _spinRateRef = 300f;

        private List<ClubSpec> _clubs = new List<ClubSpec>();
        private List<float> _actualCarries = new List<float>();  // yards, -1 = not yet run

        private Vector2 _scroll;

        void OnEnable() => ReloadCSVs();

        void OnGUI()
        {
            EditorGUILayout.LabelField("Aerodynamic Coefficients", EditorStyles.boldLabel);
            _dragCoeff    = EditorGUILayout.Slider("Drag Coefficient (Cd)",   _dragCoeff,    0.10f, 0.40f);
            _liftCoeffBase = EditorGUILayout.Slider("Lift Coefficient Base (Cl)", _liftCoeffBase, 0.10f, 0.35f);
            _spinRateRef  = EditorGUILayout.Slider("Spin Rate Reference (rad/s)", _spinRateRef, 100f, 500f);

            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Run Validation")) RunValidation();
            if (GUILayout.Button("Reload CSVs"))   ReloadCSVs();
            if (GUILayout.Button("Save aero.csv")) SaveAeroCSV();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();

            // Club table
            EditorGUILayout.LabelField("Club Carry Validation", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Club",         GUILayout.Width(120));
            GUILayout.Label("Speed (m/s)",  GUILayout.Width(80));
            GUILayout.Label("Angle (°)",    GUILayout.Width(70));
            GUILayout.Label("Spin (rpm)",   GUILayout.Width(80));
            GUILayout.Label("Expected (yd)",GUILayout.Width(90));
            GUILayout.Label("Actual (yd)",  GUILayout.Width(80));
            GUILayout.Label("Error %",      GUILayout.Width(60));
            EditorGUILayout.EndHorizontal();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            for (int i = 0; i < _clubs.Count; i++)
            {
                var c = _clubs[i];
                float actual  = i < _actualCarries.Count ? _actualCarries[i] : -1f;
                float expected = c.ExpectedCarryYd.ToFloat();
                float errPct  = actual >= 0f && expected > 0f ? Mathf.Abs(actual - expected) / expected * 100f : -1f;

                // Row colour
                if (errPct >= 0f)
                {
                    Color rowColor = errPct <= 5f ? Color.green : errPct <= 10f ? Color.yellow : Color.red;
                    var orig = GUI.color;
                    GUI.color = rowColor;
                    EditorGUILayout.BeginHorizontal();
                    GUI.color = orig;
                }
                else
                {
                    EditorGUILayout.BeginHorizontal();
                }

                GUILayout.Label(c.Id,                          GUILayout.Width(120));
                GUILayout.Label($"{c.BallSpeedMps:F1}",        GUILayout.Width(80));
                GUILayout.Label($"{c.LaunchAngleDeg:F1}",      GUILayout.Width(70));
                GUILayout.Label($"{c.SpinRateRpm:F0}",         GUILayout.Width(80));
                GUILayout.Label($"{expected:F0}",              GUILayout.Width(90));
                GUILayout.Label(actual >= 0f ? $"{actual:F0}" : "—", GUILayout.Width(80));
                GUILayout.Label(errPct >= 0f ? $"{errPct:F1}%" : "—", GUILayout.Width(60));
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
        }

        private void ReloadCSVs()
        {
            _clubs = PhysicsConfigLoader.LoadClubSpecs();
            _actualCarries = new List<float>(new float[_clubs.Count]);
            for (int i = 0; i < _actualCarries.Count; i++) _actualCarries[i] = -1f;
            Repaint();
        }

        private void RunValidation()
        {
            var cfg = BuildConfig();
            _actualCarries = new List<float>();

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

        private AeroConfig BuildConfig() => new AeroConfig
        {
            AirDensity          = fp.FromFloat(1.225f),
            BallMass            = fp.FromFloat(0.04593f),
            BallCrossSection    = fp.FromFloat(0.001432f),
            DragCoefficient     = fp.FromFloat(_dragCoeff),
            LiftCoefficientBase = fp.FromFloat(_liftCoeffBase),
            SpinRateReference   = fp.FromFloat(_spinRateRef),
            LiftMaxMultiplier   = fp.FromFloat(1.5f),
        };

        private void SaveAeroCSV()
        {
            string path = Application.dataPath + "/Resources/Physics/aero.csv";
            string content =
                "key,value,units,notes\n" +
                $"air_density,1.225,kg/m^3,sea-level 15C\n" +
                $"ball_mass,0.04593,kg,USGA max\n" +
                $"ball_cross_section,0.001432,m^2,radius 0.02135m\n" +
                $"drag_coefficient,{_dragCoeff:F4},dimensionless,\n" +
                $"lift_coefficient_base,{_liftCoeffBase:F4},dimensionless,scaled by spin in code\n" +
                $"spin_rate_reference,{_spinRateRef:F1},rad/s,~2865 rpm driver baseline\n" +
                $"lift_max_multiplier,1.5,dimensionless,cap on spin-scaled Cl\n";
            File.WriteAllText(path, content);
            AssetDatabase.Refresh();
            Debug.Log($"[PhysicsTuning] Saved aero.csv: Cd={_dragCoeff:F4} Cl={_liftCoeffBase:F4} spinRef={_spinRateRef:F1}");
        }
    }
}
