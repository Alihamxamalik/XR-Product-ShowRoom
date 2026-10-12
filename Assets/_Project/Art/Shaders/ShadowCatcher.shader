// An invisible plane that shows only the shadow falling on it, so the product casts a real
// shadow onto the user's real table. It writes the shadow as black with alpha, which the headset
// composites over passthrough as a darkening of the room. Fades out towards its edge so the
// plane itself never shows.
Shader "MRShowroom/Shadow Catcher"
{
    Properties
    {
        _ShadowColor("Shadow colour (alpha = strength)", Color) = (0, 0, 0, 0.42)
        _FadeStart("Edge fade start", Range(0, 1)) = 0.55
        _OcclusionBias("Depth occlusion bias", Range(0, 0.2)) = 0.03
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent-20"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "ShadowCatcher"
            Tags { "LightMode" = "UniversalForward" }

            // The second pair keeps the frame buffer's alpha correct, so this composites over passthrough.
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ HARD_OCCLUSION SOFT_OCCLUSION

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.meta.xr.sdk.core/Shaders/EnvironmentDepth/URP/EnvironmentOcclusionURP.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShadowColor;
                half _FadeStart;
                half _OcclusionBias;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord, input.positionWS, half4(1, 1, 1, 1));
                half shadow = 1.0 - mainLight.shadowAttenuation;

                // Round falloff from the middle of the plane.
                half fromCentre = length(input.uv * 2.0 - 1.0);
                half fade = 1.0 - smoothstep(_FadeStart, 1.0, fromCentre);

                // A real hand over the table hides the shadow under it, as it would a real one.
                half visible = META_DEPTH_GET_OCCLUSION_VALUE_WORLDPOS(input.positionWS, _OcclusionBias);

                return half4(_ShadowColor.rgb, _ShadowColor.a * shadow * fade * visible);
            }
            ENDHLSL
        }
    }
}
