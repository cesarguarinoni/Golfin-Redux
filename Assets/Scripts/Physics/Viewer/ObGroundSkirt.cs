using UnityEngine;
using UnityEngine.Rendering;

namespace Golfin.Physics.Viewer
{
    /// <summary>
    /// Spawns a flat textured, lit ground plane that covers the world beyond the terrain boundary.
    /// Prevents the bare-skybox void when the ball exits the course.
    ///
    /// AMENDMENT A1 (2026-07-27): upgraded from Unlit/Color flat fill to URP/Lit + T_Rough_Albedo
    /// texture so the skirt reads as genuine continuation of OB/rough grass rather than a
    /// flat-fill slab. Fog-responsive (no fog disable); lit by scene ambient/directional light.
    ///
    /// ITER-5 FIX (2026-07-27): switch from mesh-UV tiling (0→uvS) to simple 0→1 mesh UVs +
    /// material.SetTextureScale("_BaseMap", (uvS,uvS)).  This is the canonical URP/Lit tiling
    /// path and avoids any GPU mipmap-selection artefact that might arise from extreme UV values
    /// (e.g. 300 UV units on a 3000 m quad).  Visual result is identical; the approach is cleaner.
    /// YEpsilon tightened 0.05→0.01 to minimise the visible gap at the terrain boundary edge.
    ///
    /// Texture and tint derive from terrain layer 8 (OB):
    ///   albedo   = T_Rough_Albedo (same as rough, tinted darker)
    ///   normal   = T_Rough_Normal (same normal as rough, scale 0.4)
    ///   tileSize = 10 m (from HoleGeoImporter.cs tileSizes[8])
    ///   tint     = diffuseRemapMax (0.75, 0.82, 0.55) from HoleGeoImporter.cs:1600
    ///
    /// Pattern-matched on WaterSplashController.cs (same folder, same Configure/null-safe idiom).
    /// </summary>
    public class ObGroundSkirt : MonoBehaviour
    {
        // ── OB tint (diffuseRemapMax, HoleGeoImporter.cs layer 8 line 1600) ───
        // Passed as _BaseColor on URP/Lit, which multiplies the T_Rough_Albedo texture
        // channel-wise — matching what the terrain layer does with diffuseRemapMin/Max.
        // Values from the importer: new Vector4(0.75f, 0.82f, 0.55f, 1f).
        static readonly Color OBTint = new Color(0.75f, 0.82f, 0.55f, 1f);

        // Normal map intensity — matches layers[8].normalScale in HoleGeoImporter.cs:1553.
        const float NormalScale = 0.4f;

        // Tile size for layer 8 (OB) from HoleGeoImporter.cs tileSizes[8] = 10f metres.
        // Used so the grass grain frequency matches the terrain edge.
        const float OBTileSize = 10f;

        // Epsilon below terrain SURFACE at the boundary edges (not terrain BASE Y).
        // ITER-8: seam fix moved baseY from terrain BASE to terrain SURFACE at each edge
        // (see Rebuild below).  1 cm is still the nudge amount.
        const float YEpsilon = 0.01f;

        // Size multiple of camera far-clip. 3× covers worst-case horizon gap.
        const float FarClipMultiple = 3f;

        // ── Runtime state ──────────────────────────────────────────────────────
        GameObject _skirtGO;
        Material   _skirtMat;
        Mesh       _skirtMesh;

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>
        /// Called by PhysicsLabController.OnHoleLoaded. Rebuilds the skirt for the newly
        /// active terrain. If Terrain.activeTerrain is null (LabScaffold flat-ground, test
        /// green), this is a no-op — no error, no log spam.
        /// </summary>
        public void Rebuild(Camera chaseCamera)
        {
            // ── ENTRY diagnostic — editor only ────────────────────────────────
#if UNITY_EDITOR
            Debug.Log("[ObGroundSkirt] Rebuild() ENTRY called");
#endif
            Destroy();

            // Prefer activeTerrain; fall back to FindObjectsOfType when the additive-load
            // sequence hasn't yet set activeTerrain (null during some OnHoleLoaded timings).
            var terrain = Terrain.activeTerrain;
            if (terrain == null)
            {
                var all = Object.FindObjectsOfType<Terrain>();
#if UNITY_EDITOR
                Debug.Log($"[ObGroundSkirt] activeTerrain=NULL, FindObjectsOfType found {all.Length} terrain(s)");
#endif
                if (all.Length > 0) terrain = all[0];
            }
            else
            {
#if UNITY_EDITOR
                Debug.Log($"[ObGroundSkirt] activeTerrain={terrain.name}");
#endif
            }
            if (terrain == null)
            {
#if UNITY_EDITOR
                Debug.Log("[ObGroundSkirt] Rebuild: terrain still null after fallback — aborting");
#endif
                return;
            }

            var terrainData = terrain.terrainData;
#if UNITY_EDITOR
            Debug.Log($"[ObGroundSkirt] terrainData={(terrainData != null ? terrainData.name : "NULL")}");
#endif
            if (terrainData == null) return;

            // ── Get T_Rough_Albedo + T_Rough_Normal from terrain layer 8 (OB) ─
            Texture2D albedo    = null;
            Texture2D normalMap = null;
            float     tileSize  = OBTileSize;

            var layers = terrainData.terrainLayers;
            if (layers != null && layers.Length > 8 && layers[8] != null)
            {
                albedo    = layers[8].diffuseTexture;
                normalMap = layers[8].normalMapTexture;
                if (layers[8].tileSize.x > 0f)
                    tileSize = layers[8].tileSize.x;
            }

            // Editor fallback: find by name if the layer reference is null.
#if UNITY_EDITOR
            if (albedo == null)
            {
                var guids = UnityEditor.AssetDatabase.FindAssets("T_Rough_Albedo t:Texture2D");
                if (guids.Length > 0)
                    albedo = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(
                        UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]));
            }
            if (normalMap == null)
            {
                var guids = UnityEditor.AssetDatabase.FindAssets("T_Rough_Normal t:Texture2D");
                if (guids.Length > 0)
                    normalMap = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(
                        UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]));
            }
#endif

            // ── Skirt geometry ─────────────────────────────────────────────────
            float far  = chaseCamera != null ? chaseCamera.farClipPlane : 500f;
            float size = far * FarClipMultiple;

            // uvS = number of texture tiles across the full skirt quad at OBTileSize.
            // e.g. far=500 → size=1500 → uvS=150 (one 10 m tile per 10 m world).
            float uvS = size / tileSize;

            Vector3 terrainPos  = terrain.transform.position;
            Vector3 terrainSize = terrainData.size;
            float   centreX     = terrainPos.x + terrainSize.x * 0.5f;
            float   centreZ     = terrainPos.z + terrainSize.z * 0.5f;

            // ── ITER-8 seam fix: sample terrain SURFACE height at all four edge midpoints.
            // ITER-5 used terrainPos.y - epsilon (terrain BASE Y) which for Hole 6 placed
            // the skirt at Y≈0 while the terrain surface at the OB edge is Y≈13.7, producing
            // a 13.7 m sky band visible from void-facing angles.
            // Fix: skirt at terrainPos.y + min(edgeSurfaceHeight) - epsilon — never more than
            // 1 cm below the lowest terrain edge, seamlessly continuous from all angles.
            float cX2      = centreX;                                 // reuse
            float cZ2      = centreZ;
            float rEdgeX   = terrainPos.x + terrainSize.x - 0.5f;    // 0.5 m inside right edge
            float lEdgeX   = terrainPos.x + 0.5f;                    // 0.5 m inside left edge
            float fEdgeZ   = terrainPos.z + terrainSize.z - 0.5f;    // 0.5 m inside far edge
            float bEdgeZ   = terrainPos.z + 0.5f;                    // 0.5 m inside near edge
            float hRight   = terrain.SampleHeight(new Vector3(rEdgeX, 0f, cZ2));
            float hLeft    = terrain.SampleHeight(new Vector3(lEdgeX, 0f, cZ2));
            float hFwd     = terrain.SampleHeight(new Vector3(cX2,    0f, fEdgeZ));
            float hBack    = terrain.SampleHeight(new Vector3(cX2,    0f, bEdgeZ));
            float minEdgeH = Mathf.Min(hRight, hLeft, hFwd, hBack);
            float   baseY  = terrainPos.y + minEdgeH - YEpsilon;

            // ITER-5: Use simple 0→1 mesh UVs (canonical approach) and set tiling via
            // material.SetTextureScale("_BaseMap") below.  Avoids extreme UV values
            // that might coerce the GPU into coarser mip levels.
            float h = size * 0.5f;

            _skirtMesh = new Mesh { name = "[ObSkirt_Mesh]" };
            var mesh = _skirtMesh;
            mesh.vertices = new Vector3[]
            {
                new Vector3(-h, 0f, -h),
                new Vector3(-h, 0f,  h),
                new Vector3( h, 0f,  h),
                new Vector3( h, 0f, -h),
            };
            // Simple 0→1 UVs — material SetTextureScale drives the world-space tiling.
            mesh.uv = new Vector2[]
            {
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 0f),
            };
            mesh.triangles = new int[] { 0, 1, 2,  0, 2, 3 };
            mesh.RecalculateNormals();

            // ── GameObject + Renderer ──────────────────────────────────────────
            _skirtGO = new GameObject("[ObGroundSkirt]");
            _skirtGO.transform.position = new Vector3(centreX, baseY, centreZ);

            var mf = _skirtGO.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;

            var mr = _skirtGO.AddComponent<MeshRenderer>();

            // ── Material: URP/Lit (AMENDMENT A1) ──────────────────────────────
            // Use lit shader so skirt responds to scene ambient + directional light
            // and allows scene fog (Exp2 density 0.01) to blend the distant horizon.
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Diffuse");

            _skirtMat = new Material(shader) { name = "[ObSkirt_Mat]" };

            // ── Albedo texture + tiling ────────────────────────────────────────
            if (albedo != null)
            {
                _skirtMat.SetTexture("_BaseMap", albedo);  // URP/Lit
                _skirtMat.SetTexture("_MainTex", albedo);  // Standard fallback
            }

            // ITER-5: Set tiling via material SetTextureScale (canonical URP path).
            // Equivalent to material tiling knob in the Inspector.
            // uvS tiles must span the entire quad at OBTileSize-metre intervals.
            _skirtMat.SetTextureScale("_BaseMap", new Vector2(uvS, uvS));  // URP/Lit
            _skirtMat.SetTextureScale("_MainTex", new Vector2(uvS, uvS));  // Standard

            // ── Tint ──────────────────────────────────────────────────────────
            // Mirrors diffuseRemapMax on OB layer 8 (HoleGeoImporter.cs:1600).
            _skirtMat.SetColor("_BaseColor", OBTint);  // URP/Lit
            _skirtMat.SetColor("_Color",     OBTint);  // Standard fallback

            // ── Normal map (optional) ──────────────────────────────────────────
            if (normalMap != null)
            {
                _skirtMat.SetTexture("_BumpMap", normalMap);
                // Tile the normal map at the same world-space frequency as the albedo.
                _skirtMat.SetTextureScale("_BumpMap", new Vector2(uvS, uvS));
                if (_skirtMat.HasProperty("_BumpScale"))
                    _skirtMat.SetFloat("_BumpScale", NormalScale);
                // ITER-8: URP/Lit requires this keyword to be explicitly enabled on a
                // runtime-created Material; without it the bump map is silently ignored
                // even when the texture slot is populated → no lighting variation → stddev ≈ 4.
                _skirtMat.EnableKeyword("_NORMALMAP");
            }

            // ── Matte grass (no specular sheen) ───────────────────────────────
            if (_skirtMat.HasProperty("_Metallic"))
                _skirtMat.SetFloat("_Metallic",    0f);
            if (_skirtMat.HasProperty("_Smoothness"))
                _skirtMat.SetFloat("_Smoothness",  0f);
            if (_skirtMat.HasProperty("_Glossiness"))
                _skirtMat.SetFloat("_Glossiness",  0f);

            // Receive shadows OFF for URP/Lit.
            if (_skirtMat.HasProperty("_ReceiveShadows"))
                _skirtMat.SetFloat("_ReceiveShadows", 0f);

            // Do NOT disable fog. Scene Exp2 fog (density 0.01, grey 0.5) naturally
            // blends the distant skirt toward the horizon — one of the three A1 seam fixes.

            mr.sharedMaterial = _skirtMat;

            // Mobile perf — mirror BallTrailController.EnsureTrail flags block.
            mr.shadowCastingMode    = ShadowCastingMode.Off;
            mr.receiveShadows       = false;
            mr.lightProbeUsage      = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;

            // ── Diagnostic log (iter-8) — editor only ─────────────────────────
#if UNITY_EDITOR
            Debug.Log(
                $"[ObGroundSkirt] Rebuild: far={far:F0}m  size={size:F0}m  " +
                $"tileSize={tileSize:F1}m  uvS={uvS:F1}  " +
                $"layerCount={layers?.Length ?? 0}  " +
                $"albedo={(albedo != null ? albedo.name : "NULL")} " +
                $"({(albedo != null ? albedo.width : 0)}x{(albedo != null ? albedo.height : 0)})  " +
                $"normalMap={(normalMap != null ? normalMap.name : "NULL")}  " +
                $"shader={shader?.name ?? "NULL"}  " +
                $"edgeH R={hRight:F3} L={hLeft:F3} Fwd={hFwd:F3} Bk={hBack:F3} min={minEdgeH:F3}  " +
                $"baseY={baseY:F3}  centreXZ=({centreX:F1},{centreZ:F1})");
#endif
        }

        /// <summary>
        /// Destroy the skirt object and its material. Safe to call when no skirt exists.
        /// </summary>
        public void Destroy()
        {
            if (_skirtGO != null)
            {
                if (Application.isPlaying)
                    Object.Destroy(_skirtGO);
                else
                    Object.DestroyImmediate(_skirtGO);
                _skirtGO = null;
            }
            if (_skirtMat != null)
            {
                if (Application.isPlaying)
                    Object.Destroy(_skirtMat);
                else
                    Object.DestroyImmediate(_skirtMat);
                _skirtMat = null;
            }
            if (_skirtMesh != null)
            {
                if (Application.isPlaying)
                    Object.Destroy(_skirtMesh);
                else
                    Object.DestroyImmediate(_skirtMesh);
                _skirtMesh = null;
            }
        }

        void OnDestroy() => Destroy();

        // ── Editor/test seams ──────────────────────────────────────────────────
#if UNITY_EDITOR
        /// <summary>Whether a skirt GO is currently alive.</summary>
        public bool SkirtExists => _skirtGO != null;

        /// <summary>The tint colour applied to the OB albedo texture (diffuseRemapMax).</summary>
        public static Color SkirtTint => OBTint;

        /// <summary>Tile size used for UV generation (metres).</summary>
        public static float SkirtTileSize => OBTileSize;
#endif
    }
}
