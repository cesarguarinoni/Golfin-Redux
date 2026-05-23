// PutterGreenGrid.shader — URP custom shader for warped green-reading grid.
//
// Draws world-XZ-aligned grid lines via frac(worldPos.xz / _CellSize) math in
// the fragment shader. Lines follow the mesh surface in Y (drape with topology),
// but remain perfectly square in world-XZ plan view — L4 enforced by math.
//
// The mesh is a procedural heightfield covering the green polygon. Vertices sit
// at bake cell centers with Y sampled from the surface. Fragment math computes
// grid lines in world space, so the lines are NOT screen-space and do NOT depend
// on the mesh's UV coordinates.
//
// SPEC: puttpath_predictor_perf_and_design §Architecture §§ Render step → Shader.
//
// Defaults:
//   _CellSize        = 0.5  (matches bake cell size)
//   _LineWidth       = 0.04 (world-space width ~8% of cell)
//   _LineGlow        = 1.5  (line brightness multiplier vs vertex color)
//   _BackgroundAlpha = 0.0  (fully transparent between lines)
//   _BallPosition    = (0,0,0,0) — updated via MaterialPropertyBlock each frame.
//   _VisibleRadius   = 10.0 (fade beyond 10m from ball, per Q3)

Shader "Golfin/PutterGreenGrid"
{
    Properties
    {
        _CellSize        ("Cell Size (m)",          Float) = 0.5
        _LineWidth        ("Line Width (m)",         Float) = 0.04
        _LineGlow         ("Line Glow",              Float) = 1.5
        _BackgroundAlpha  ("Background Alpha",       Float) = 0.0
        _BallPosition     ("Ball World Position",    Vector) = (0, 0, 0, 0)
        _VisibleRadius    ("Visible Radius (m)",     Float) = 10.0
    }

    SubShader
    {
        Tags
        {
            "RenderType"      = "Transparent"
            "Queue"           = "Transparent"
            "RenderPipeline"  = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "PutterGreenGridForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            // Instancing support — used if the mesh is drawn via GPU instancing paths.
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // ── Vertex input / output ─────────────────────────────────────────

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color      : COLOR;       // vertex color from baked slope magnitude
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 positionWS   : TEXCOORD0;  // world-space position for fragment grid math
                float4 color        : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // ── Uniforms ──────────────────────────────────────────────────────

            CBUFFER_START(UnityPerMaterial)
                float  _CellSize;
                float  _LineWidth;
                float  _LineGlow;
                float  _BackgroundAlpha;
                float4 _BallPosition;
                float  _VisibleRadius;
            CBUFFER_END

            // ── Vertex shader ─────────────────────────────────────────────────

            Varyings vert(Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                Varyings OUT;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexPositionInputs vpi = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = vpi.positionCS;
                OUT.positionWS = vpi.positionWS;
                OUT.color      = IN.color;
                return OUT;
            }

            // ── Fragment shader ───────────────────────────────────────────────

            half4 frag(Varyings IN) : SV_Target
            {
                // ── World-XZ grid line computation ────────────────────────────
                //
                // frac(worldPos.xz / _CellSize) maps each world unit to [0,1]
                // within a cell. min(u, 1-u) gives the distance to the nearest
                // grid edge, ranging from 0 (on a line) to 0.5 (cell center).
                //
                // This is L4's mathematical guarantee: cells are ALWAYS square
                // in world-XZ regardless of camera angle or mesh topology.

                float uv_x = frac(IN.positionWS.x / _CellSize);
                float uv_z = frac(IN.positionWS.z / _CellSize);

                float edge_dist_x = min(uv_x, 1.0 - uv_x);
                float edge_dist_z = min(uv_z, 1.0 - uv_z);
                float edge_dist   = min(edge_dist_x, edge_dist_z);

                // smoothstep: 1.0 on the grid line, 0.0 at _LineWidth/2 away.
                float line_alpha = 1.0 - smoothstep(0.0, _LineWidth * 0.5, edge_dist);

                // ── Distance culling (Q3 — option b via MaterialPropertyBlock) ─
                //
                // _BallPosition is pushed per-frame by PutterGreenReader.Update()
                // via MaterialPropertyBlock. Shader fades alpha to 0 beyond
                // _VisibleRadius to match the 10m radius Q3 lock.

                float2 ballXZ   = _BallPosition.xz;
                float2 fragXZ   = IN.positionWS.xz;
                float  distSq   = dot(fragXZ - ballXZ, fragXZ - ballXZ);
                float  radiusSq = _VisibleRadius * _VisibleRadius;

                // Fade zone: start fade at 90% of radius, fully transparent at 100%.
                float fadeStart = 0.9 * _VisibleRadius;
                float fadeDist  = smoothstep(fadeStart * fadeStart, radiusSq, distSq);
                float distAlpha = 1.0 - fadeDist;

                // ── Combine ───────────────────────────────────────────────────

                float3 lineColor  = IN.color.rgb * _LineGlow;
                float  finalAlpha = max(line_alpha, _BackgroundAlpha) * distAlpha;

                return half4(lineColor, finalAlpha);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
