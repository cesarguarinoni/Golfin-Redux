using System.Collections.Generic;
using UnityEngine;
using Golfin.Gameplay.Input;

// Keep in Golfin.Physics.Viewer (SPEC putter_aim_blue_line §1): this needs
// ShotController (Golfin.Gameplay.Input) and the PutterGreenReader slope bake, and
// Golfin.Gameplay.Input is autoReferenced:false — unreachable from Assembly-CSharp.
// Golfin.Physics.Viewer.asmdef already references everything used here, so this file
// costs ZERO asmdef edits. Same reasoning the PuttPathPredictor stub records.

namespace Golfin.Physics.Viewer
{
    /// <summary>
    /// Straight aim-direction line for putter aim — the companion visual to
    /// <see cref="PutterGreenReader"/>'s green-reading grid (Winning Putt reference).
    ///
    /// A world-space strip from the ball along the player's current aim heading,
    /// visible only while aiming with the putter, drawn above the grid. The player
    /// reads break off the grid and aims with this line; there is deliberately NO
    /// curve prediction — that is the puttpath_predictor L1 design lock, and SPEC §9
    /// confirms straight-line-plus-grid is what every comparable title ships.
    ///
    /// Lifecycle, aim gating and the ball-position override mirror
    /// <see cref="PutterGreenReader"/> exactly rather than inventing a second pattern
    /// (Lesson Q — no putter-specific divergence).
    ///
    /// Performance (SPEC §8), all three of which matter because putter aim is the
    /// longest-held state in the game and the device target is 60 fps:
    ///   • rebuild-on-dirty — <see cref="Update"/> is an early-out plus a couple of
    ///     comparisons while the player holds an aim; the mesh only rebuilds when the
    ///     aim yaw or ball actually moved (§8.1).
    ///   • zero steady-state GC — one <see cref="Mesh"/> for life, pre-sized vertex
    ///     list, topology written once, no LINQ / ToArray per rebuild (§8.2).
    ///   • vertex Y comes from the shared 0.5 m slope bake, never per-vertex
    ///     raycasts (§8.4).
    ///
    /// SPEC: putter_aim_blue_line (Rev 2).
    /// </summary>
    public class PutterAimLine : MonoBehaviour
    {
        // ── Inspector refs ───────────────────────────────────────────────────

        [Header("Rendering")]
        [SerializeField] private Material _lineMaterial;

        [Header("Lab Controller (for ball position)")]
        [SerializeField] private PhysicsLabController _labController;

        [Header("Shot Controller (for putter aim detection + aim heading)")]
        [SerializeField] private ShotController _shotController;

        [SerializeField, Tooltip("Green reader whose 0.5 m slope bake supplies per-vertex Y. " +
                                 "Left empty, the line self-wires from this GameObject / the scene.")]
        private PutterGreenReader _greenReader;

        // ── Tunables (SPEC §4 table — colour and width are provisional; Cesar locks
        //    them from the first capture, so they are all Inspector-live) ─────

        [Header("Line Parameters")]
        [SerializeField, Tooltip("Line length in metres from the ball along the aim heading. " +
                                 "Fixed 15 m — cup-aware trimming is explicitly out of scope.")]
        private float _lengthMeters = 15f;

        [SerializeField, Tooltip("Sample pitch in metres. 0.5 m matches the slope bake cell size, " +
                                 "so every vertex lands on a lattice the bake can answer exactly.")]
        private float _sampleStepMeters = 0.5f;

        [SerializeField, Tooltip("Strip width in metres (world space).")]
        private float _widthMeters = 0.08f;

        [SerializeField, Tooltip("Vertical offset (metres) above the sampled surface. 0.04 = 4 cm, " +
                                 "i.e. 2 cm above the grid's own 0.02 m lift — same gap the iter-4 " +
                                 "z-fight fix uses between grid and terrain.")]
        private float _surfaceYOffset = 0.04f;

        [SerializeField, Tooltip("Line colour. #7AE9FF per SPEC §4 — provisional.")]
        private Color _color = new Color(0.478431f, 0.913725f, 1f, 1f);   // #7AE9FF

        // Dirty-check thresholds (SPEC §8.1).
        private const float kYawEpsilonDeg   = 0.05f;
        private const float kBallEpsilonSqM  = 0.01f * 0.01f;   // 1 cm

        // ── Runtime state ────────────────────────────────────────────────────

        private bool  _aimActive;
        private float _aimYawRadians;

        // Nullable override for ball position, mirroring PutterGreenReader:144. Visual-gate
        // capture scripts set this when no PhysicsLabController ball is spawned; without it
        // the line renders at the origin in exactly the captures meant to prove it works.
        // Do NOT set from production code.
        private Vector3? _ballPositionOverride;

        // Aim-yaw override for the same capture scenarios (no live ShotController driving aim).
        private float? _aimYawOverride;

        // ── Mesh state ───────────────────────────────────────────────────────

        private GameObject   _lineMeshGO;
        private MeshFilter   _lineMeshFilter;
        private MeshRenderer _lineMeshRenderer;
        private Mesh         _lineMesh;
        private MaterialPropertyBlock _mpb;
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        // Pre-allocated vertex buffer, refilled in place every rebuild (§8.2).
        private readonly List<Vector3> _verts = new List<Vector3>(64);
        private int _sampleCount;          // vertices = 2 × _sampleCount
        private bool _topologyWritten;     // triangles + UVs are written once

        // Last-built state for the dirty check (§8.1).
        private bool    _hasBuilt;
        private Vector3 _builtBallPos;
        private float   _builtAimYawRadians;
        private Color   _builtColor;

        private bool _clearanceWarned;

        // ── Test / capture seams ─────────────────────────────────────────────

        /// <summary>True while the line is gated visible (putter aim in progress).</summary>
        public bool AimActive => _aimActive;

        /// <summary>Vertex count of the generated strip (0 before the first build).</summary>
        public int MeshVertexCount => _lineMesh != null ? _lineMesh.vertexCount : 0;

        /// <summary>Number of samples along the line in the last build.</summary>
        public int SampleCount => _sampleCount;

        /// <summary>Number of mesh rebuilds since load — the §8.1 dirty-check test seam.</summary>
        public int RebuildCount { get; private set; }

        /// <summary>
        /// Force putter aim active (screenshot verification and smoke-bot only).
        /// Mirrors <see cref="PutterGreenReader.SetAimActiveForTest"/>.
        /// Do NOT call from production code.
        /// </summary>
        public void SetAimActiveForTest(bool active)
        {
            _aimActive = active;
            if (_lineMeshGO != null)
                _lineMeshGO.SetActive(active);
        }

        /// <summary>
        /// Override the ball position the line is anchored to. Pass null to revert to
        /// <c>_labController.BallPosition</c>. Used by visual-gate capture scripts only.
        /// Same contract and priority order as
        /// <see cref="PutterGreenReader.SetBallPositionOverride"/> — SPEC §3 makes this
        /// non-optional: the captures that prove this feature works have no live ball.
        /// </summary>
        public void SetBallPositionOverride(Vector3? pos)
        {
            _ballPositionOverride = pos;
            _hasBuilt = false;   // force a rebuild on the next tick
        }

        /// <summary>
        /// Override the aim heading (radians, same convention as
        /// <c>ShotInputState.AimYawRadians</c>). Pass null to revert to the live
        /// ShotController heading. Visual-gate capture scripts only.
        /// </summary>
        public void SetAimYawOverride(float? yawRadians)
        {
            _aimYawOverride = yawRadians;
            _hasBuilt = false;
        }

        /// <summary>Force a synchronous rebuild (EditMode tests and capture scripts).</summary>
        public void RebuildForTest()
        {
            RebuildMesh(ResolveBallPosition(), ResolveAimYaw());
        }

        // ── Unity lifecycle (mirrors PutterGreenReader) ──────────────────────

        private void Awake()
        {
            // Same reason as PutterGreenReader.Awake: the MaterialPropertyBlock ctor calls a
            // Unity native API that is disallowed in a field initializer.
            _mpb = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            EnsureRuntimeRefs();

            if (_shotController != null)
                _shotController.OnStateChanged += OnShotStateChanged;

            if (_lineMeshGO != null)
                _lineMeshGO.SetActive(_aimActive);
        }

        private void OnDisable()
        {
            if (_shotController != null)
                _shotController.OnStateChanged -= OnShotStateChanged;
            _aimActive = false;
            if (_lineMeshGO != null)
                _lineMeshGO.SetActive(false);
        }

        private void OnDestroy()
        {
            DestroyLineMesh();
        }

        // ── Event handlers ───────────────────────────────────────────────────

        // Identical gate to PutterGreenReader.OnShotStateChanged — putter only, and only
        // in the three aim-ish states. The line therefore appears on entering putter aim
        // and hides the moment the putt is struck (State leaves Timing) or the player
        // switches off the putter (IsPutt goes false).
        private void OnShotStateChanged(ShotInputState state)
        {
            bool isPutterAim = _shotController != null
                && _shotController.IsPutt
                && (state.State == ShotState.Aiming
                 || state.State == ShotState.Pulling
                 || state.State == ShotState.Timing);

            _aimActive = isPutterAim;

            // Live aim heading, published every tick by ShotController.PublishState().
            // This is the one source of truth for aim inside this assembly — SPEC §5
            // explicitly forbids re-deriving MapViewController's formula here. The
            // convention is (cos θ, 0, sin θ), matching ShotConeView:434, which consumes
            // this same field.
            _aimYawRadians = state.AimYawRadians;

            if (_lineMeshGO != null)
                _lineMeshGO.SetActive(_aimActive);
        }

        // ── Update — dirty check only (SPEC §8.1) ────────────────────────────

        private void Update()
        {
            if (!_aimActive) return;
            TickIfDirty();
        }

        /// <summary>
        /// The §8.1 dirty check: rebuilds the strip only when the aim yaw moved more than
        /// 0.05°, the ball moved more than 1 cm, or the colour was retuned. Returns true if
        /// it rebuilt. Public so EditMode tests can assert the no-op path without a frame loop.
        /// </summary>
        public bool TickIfDirty()
        {
            Vector3 ballPos = ResolveBallPosition();
            float   aimYaw  = ResolveAimYaw();

            if (_hasBuilt
                && Mathf.Abs(Mathf.DeltaAngle(_builtAimYawRadians * Mathf.Rad2Deg,
                                              aimYaw * Mathf.Rad2Deg)) <= kYawEpsilonDeg
                && (ballPos - _builtBallPos).sqrMagnitude <= kBallEpsilonSqM
                && _builtColor == _color)
            {
                // Steady-state hold: nothing allocated, nothing uploaded, no draw-state churn.
                return false;
            }

            RebuildMesh(ballPos, aimYaw);
            return true;
        }

        private Vector3 ResolveBallPosition()
        {
            // Override wins, exactly as in PutterGreenReader.Update.
            return _ballPositionOverride.HasValue
                ? _ballPositionOverride.Value
                : (_labController != null ? _labController.BallPosition : Vector3.zero);
        }

        private float ResolveAimYaw()
        {
            return _aimYawOverride ?? _aimYawRadians;
        }

        // ── Mesh build ───────────────────────────────────────────────────────

        // Called from OnEnable AND from the build path: neither Awake nor OnEnable runs for a
        // plain MonoBehaviour in EditMode, and a domain reload can null the property block, so
        // the build path cannot assume either has happened. Same defensive shape as
        // PutterGreenReader's repeated `if (_mpb == null)` guards.
        private void EnsureRuntimeRefs()
        {
            if (_mpb == null) _mpb = new MaterialPropertyBlock();

            if (_greenReader == null)
            {
                // The reader is the height source; in both shipping scenes it lives on this
                // same GameObject. Self-wire so a scene that only got the component added
                // still renders correctly, and fall back to a scene-wide lookup.
                _greenReader = GetComponent<PutterGreenReader>();
#if UNITY_2023_1_OR_NEWER
                if (_greenReader == null) _greenReader = FindAnyObjectByType<PutterGreenReader>();
#else
                if (_greenReader == null) _greenReader = FindObjectOfType<PutterGreenReader>();
#endif
            }
        }

        private void RebuildMesh(Vector3 ballPos, float aimYawRadians)
        {
            EnsureRuntimeRefs();
            EnsureLineMeshGO();

            int samples = Mathf.Max(2, Mathf.RoundToInt(_lengthMeters / Mathf.Max(0.01f, _sampleStepMeters)) + 1);
            if (samples != _sampleCount)
            {
                _sampleCount     = samples;
                _topologyWritten = false;    // only when the Inspector length/step changes
            }

            // Aim basis. (cos θ, 0, sin θ) per ShotInputState's convention; `right` is that
            // vector rotated −90° in XZ, so the strip is flat-in-plan and does not billboard.
            float sin = Mathf.Sin(aimYawRadians);
            float cos = Mathf.Cos(aimYawRadians);
            float halfW = _widthMeters * 0.5f;
            float rx = -sin * halfW;
            float rz =  cos * halfW;

            _verts.Clear();
            if (_verts.Capacity < _sampleCount * 2)
                _verts.Capacity = _sampleCount * 2;   // one-time growth only

            // Height comes from the shared bake (§8.4). Off-bake samples — the tail of a
            // 15 m line that runs past the green polygon — carry the last valid baked Y
            // forward rather than firing a raycast; the line is a direction read, and a
            // flat overhang tail beyond the green reads correctly while costing nothing.
            float lastY = ballPos.y;
            bool  haveBakedY = false;

            for (int i = 0; i < _sampleCount; i++)
            {
                float t = Mathf.Min(i * _sampleStepMeters, _lengthMeters);
                float px = ballPos.x + cos * t;
                float pz = ballPos.z + sin * t;

                if (_greenReader != null && _greenReader.TrySampleBakedSurfaceY(px, pz, out float bakedY))
                {
                    lastY = bakedY;
                    haveBakedY = true;
                }
                // else: hold lastY (ball Y before the first hit, last baked Y after).

                float py = lastY + _surfaceYOffset;
                _verts.Add(new Vector3(px - rx, py, pz - rz));
                _verts.Add(new Vector3(px + rx, py, pz + rz));
            }

            if (_lineMesh == null)
            {
                _lineMesh = new Mesh { name = "PutterAimLineMesh" };
                _lineMesh.MarkDynamic();          // rebuilt on aim change — never reallocated
            }

            if (!_topologyWritten)
            {
                // Vertex count changes only here, so clear first to avoid an index-range
                // complaint from the stale triangle buffer.
                _lineMesh.Clear();
                _lineMesh.SetVertices(_verts);
                WriteTopology();
                _topologyWritten = true;
            }
            else
            {
                // Steady path: positions only. Triangles and UVs never change (§8.2).
                _lineMesh.SetVertices(_verts);
            }

            _lineMesh.RecalculateBounds();
            _lineMeshFilter.sharedMesh = _lineMesh;

            // Colour via MaterialPropertyBlock — per-renderer, no material instance, and it
            // keeps Cesar's tuning round a live Inspector edit instead of an asset edit.
            _lineMeshRenderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(ColorId, _color);
            _lineMeshRenderer.SetPropertyBlock(_mpb);

            _lineMeshGO.SetActive(_aimActive);

            _hasBuilt           = true;
            _builtBallPos       = ballPos;
            _builtAimYawRadians = aimYawRadians;
            _builtColor         = _color;
            RebuildCount++;

            WarnIfClearanceLost(haveBakedY);
        }

        // Triangles + UVs, written once per topology. Two triangles per segment, wound so
        // the strip faces +Y (the shader is Cull Off anyway, but a consistent winding keeps
        // the mesh sane for anything that inspects it).
        private void WriteTopology()
        {
            int segs = _sampleCount - 1;
            var tris = new int[segs * 6];
            var uvs  = new Vector2[_sampleCount * 2];

            for (int s = 0; s < segs; s++)
            {
                int v = s * 2;
                int t = s * 6;
                tris[t + 0] = v;
                tris[t + 1] = v + 2;
                tris[t + 2] = v + 1;
                tris[t + 3] = v + 1;
                tris[t + 4] = v + 2;
                tris[t + 5] = v + 3;
            }

            for (int i = 0; i < _sampleCount; i++)
            {
                float uy = _sampleCount > 1 ? i / (float)(_sampleCount - 1) : 0f;
                uvs[i * 2]     = new Vector2(0f, uy);
                uvs[i * 2 + 1] = new Vector2(1f, uy);
            }

            _lineMesh.SetTriangles(tris, 0, calculateBounds: false);
            _lineMesh.SetUVs(0, uvs);
        }

        private void EnsureLineMeshGO()
        {
            if (_lineMeshGO != null) return;

            _lineMeshGO = new GameObject("PutterAimLineMesh");
            _lineMeshGO.transform.SetParent(transform, worldPositionStays: false);

            _lineMeshFilter   = _lineMeshGO.AddComponent<MeshFilter>();
            _lineMeshRenderer = _lineMeshGO.AddComponent<MeshRenderer>();

            _lineMeshRenderer.sharedMaterial = _lineMaterial;

            // Unlit overlay: no shadows either way, matching PutterGreenReader:508-509.
            _lineMeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _lineMeshRenderer.receiveShadows    = false;
            // One draw call: no per-object light probes or reflection probe blending needed.
            _lineMeshRenderer.lightProbeUsage      = UnityEngine.Rendering.LightProbeUsage.Off;
            _lineMeshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            _lineMeshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        }

        private void DestroyLineMesh()
        {
            if (_lineMeshGO != null)
            {
                if (Application.isPlaying) Destroy(_lineMeshGO);
#if UNITY_EDITOR
                else DestroyImmediate(_lineMeshGO);
#else
                else Destroy(_lineMeshGO);
#endif
                _lineMeshGO       = null;
                _lineMeshFilter   = null;
                _lineMeshRenderer = null;
            }

            if (_lineMesh != null)
            {
                if (Application.isPlaying) Destroy(_lineMesh);
#if UNITY_EDITOR
                else DestroyImmediate(_lineMesh);
#else
                else Destroy(_lineMesh);
#endif
                _lineMesh = null;
            }

            _topologyWritten = false;
            _hasBuilt        = false;
        }

        // The grid's lift and the line's lift are independently serialized, so a future
        // retune of either could quietly close the gap the z-fight defence depends on.
        // Say so once, loudly, instead of shipping a shimmering line.
        private void WarnIfClearanceLost(bool sampledFromBake)
        {
            if (_clearanceWarned || !sampledFromBake || _greenReader == null) return;
            float gap = _surfaceYOffset - _greenReader.SurfaceYOffset;
            if (gap > 0.005f) return;
            _clearanceWarned = true;
            Debug.LogWarning(
                $"[PutterAimLine] Clearance over the green grid is {gap * 100f:F1} cm " +
                $"(line offset {_surfaceYOffset:F3} m vs grid offset {_greenReader.SurfaceYOffset:F3} m). " +
                "Expect z-fighting — raise the line offset or lower the grid's.");
        }
    }
}
