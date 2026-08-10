// PutterAimLine.shader — URP unlit strip shader for the putter aim-direction line.
//
// Deliberately the simplest transparent-unlit variant of the PutterGreenGrid setup
// (SPEC putter_aim_blue_line §8.3): no lighting, no shadows, no per-fragment math
// beyond returning a flat colour. All the geometry work (per-vertex Y sampled from
// the 0.5 m slope bake) happens on the CPU in PutterAimLine.cs, so the fragment
// stage stays a constant-colour write.
//
// Queue is Transparent+1 (3001) so the line always draws AFTER PutterGreenGrid
// (3000). Both passes are ZWrite Off, so draw order — not the depth buffer — is
// what resolves the two overlays against each other. This is the "bump render
// queue rather than raising the offset further" note in SPEC §4 § Sorting, applied
// up-front: the 2 cm mesh gap keeps the line off the grid geometrically, and the
// queue guarantees it wins the overlay compositing regardless.
//
//   _Color = #7AE9FF (SPEC §4 — provisional, Cesar locks from the first capture).
//            Pushed per-renderer via MaterialPropertyBlock by PutterAimLine.cs.

Shader "Golfin/PutterAimLine"
{
    Properties
    {
        _Color ("Line Color", Color) = (0.478431, 0.913725, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderType"      = "Transparent"
            "Queue"           = "Transparent+1"
            "RenderPipeline"  = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "PutterAimLineForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                Varyings OUT;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexPositionInputs vpi = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = vpi.positionCS;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Uniform brightness over the full length — SPEC §9 takeaway (b):
                // every comparable title renders the aim line uniformly bright,
                // so there is deliberately no fade-out gradient in v1.
                return half4(_Color.rgb, _Color.a);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
