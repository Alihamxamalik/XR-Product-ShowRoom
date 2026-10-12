// The product's surface. URP physically based lighting, plus the four things the showroom needs:
//
//   Tint mask     One mask (texture RGB x vertex colour RGB) marks up to three regions; each has its
//                 own colour and finish, set from script. The base texture is multiplied by the tint.
//   Colour sweep  A change of colour travels across the product as a noisy, glowing front instead of
//                 snapping. The front is measured along an axis in the product's own space.
//   Hover rim     A soft rim light while a hand is near.
//   Occlusion     Meta's Depth API: real things in front of the product hide it.
//
// Three regions is a deliberate limit: it keeps the whole product on one texture set and one shader.
Shader "MRShowroom/Product Lit"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Colour", Color) = (1, 1, 1, 1)
        _Metallic("Metallic", Range(0, 1)) = 0
        _Smoothness("Smoothness", Range(0, 1)) = 0.35

        [Header(Tint regions)]
        [NoScaleOffset] _TintMask("Tint Mask (R, G, B = regions)", 2D) = "white" {}
        _TintRFrom("R: colour sweeping out", Color) = (1, 1, 1, 1)
        _TintRTo("R: colour", Color) = (1, 1, 1, 1)
        _TintRSweep("R: sweep", Range(0, 1)) = 1
        _TintRSurfaceFrom("R: metallic, smoothness (out)", Vector) = (0, 0.35, 0, 0)
        _TintRSurfaceTo("R: metallic, smoothness", Vector) = (0, 0.35, 0, 0)
        _TintGFrom("G: colour sweeping out", Color) = (1, 1, 1, 1)
        _TintGTo("G: colour", Color) = (1, 1, 1, 1)
        _TintGSweep("G: sweep", Range(0, 1)) = 1
        _TintGSurfaceFrom("G: metallic, smoothness (out)", Vector) = (0, 0.35, 0, 0)
        _TintGSurfaceTo("G: metallic, smoothness", Vector) = (0, 0.35, 0, 0)
        _TintBFrom("B: colour sweeping out", Color) = (1, 1, 1, 1)
        _TintBTo("B: colour", Color) = (1, 1, 1, 1)
        _TintBSweep("B: sweep", Range(0, 1)) = 1
        _TintBSurfaceFrom("B: metallic, smoothness (out)", Vector) = (0, 0.35, 0, 0)
        _TintBSurfaceTo("B: metallic, smoothness", Vector) = (0, 0.35, 0, 0)

        [Header(Colour sweep)]
        _SweepAxis("Axis (product space)", Vector) = (1, 0, 0, 0)
        _SweepRange("Start, 1 / length", Vector) = (0, 1, 0, 0)
        _SweepSoftness("Edge softness", Range(0.005, 0.3)) = 0.05
        _SweepNoise("Edge noise", Range(0, 0.3)) = 0.07
        [HDR] _SweepGlow("Edge glow", Color) = (1, 1, 1, 1)

        [Header(Sheen)]
        _SheenPosition("Position (-1 = off)", Float) = -1
        _SheenWidth("Width", Range(0.01, 0.5)) = 0.14
        [HDR] _SheenColor("Colour", Color) = (0.9, 0.95, 1, 1)

        [Header(Hover rim)]
        _Rim("Amount", Range(0, 1)) = 0
        _RimPower("Power", Range(0.5, 8)) = 2.4
        [HDR] _RimColor("Colour", Color) = (0.72, 0.92, 0.35, 1)

        [Header(Fabric weave)]
        _DetailScale("Scale", Float) = 520
        _DetailStrength("Strength", Range(0, 1)) = 0

        [Header(Mixed reality)]
        _OcclusionBias("Depth occlusion bias", Range(0, 0.2)) = 0.03
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        // Every pass shares this block, laid out identically, so the SRP Batcher can batch the product's parts.
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _Metallic;
            half _Smoothness;
            half4 _TintRFrom;
            half4 _TintRTo;
            half _TintRSweep;
            half4 _TintRSurfaceFrom;
            half4 _TintRSurfaceTo;
            half4 _TintGFrom;
            half4 _TintGTo;
            half _TintGSweep;
            half4 _TintGSurfaceFrom;
            half4 _TintGSurfaceTo;
            half4 _TintBFrom;
            half4 _TintBTo;
            half _TintBSweep;
            half4 _TintBSurfaceFrom;
            half4 _TintBSurfaceTo;
            float4 _SweepAxis;
            float4 _SweepRange;
            half _SweepSoftness;
            half _SweepNoise;
            half4 _SweepGlow;
            float _SheenPosition;
            half _SheenWidth;
            half4 _SheenColor;
            half _Rim;
            half _RimPower;
            half4 _RimColor;
            float _DetailScale;
            half _DetailStrength;
            half _OcclusionBias;
            float4x4 _ProductWorldToLocal;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile_instancing
            // Set globally by Meta's EnvironmentDepthManager once depth is available.
            #pragma multi_compile _ HARD_OCCLUSION SOFT_OCCLUSION

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.meta.xr.sdk.core/Shaders/EnvironmentDepth/URP/EnvironmentOcclusionURP.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_TintMask);
            SAMPLER(sampler_TintMask);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                // xyz: position in the product's space (for the noise), w: 0-1 along the sweep axis.
                float4 sweep : TEXCOORD3;
                half3 mask : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.mask = input.color.rgb;

                float3 positionPS = mul(_ProductWorldToLocal, float4(position.positionWS, 1.0)).xyz;
                output.sweep.xyz = positionPS;
                output.sweep.w = (dot(positionPS, _SweepAxis.xyz) - _SweepRange.x) * _SweepRange.y;
                return output;
            }

            // Cheap, smooth and good enough to break up a straight edge.
            half EdgeNoise(float3 p)
            {
                return (sin(p.x * 41.0 + sin(p.y * 57.0)) + sin(p.y * 47.0 + sin(p.z * 61.0))
                    + sin(p.z * 53.0 + sin(p.x * 43.0))) * (1.0 / 6.0);
            }

            // How far the new look has reached at this pixel (x), and how close the pixel is to the front (y).
            half2 Sweep(half progress, half along)
            {
                half soft = _SweepSoftness;
                half front = lerp(-soft, 1.0 + soft, progress);
                half reached = 1.0 - smoothstep(front - soft, front + soft, along);
                half edge = saturate(1.0 - abs(along - front) / (soft * 2.0));
                return half2(reached, edge * edge);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 base = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                half3 mask = SAMPLE_TEXTURE2D(_TintMask, sampler_TintMask, input.uv).rgb * input.mask;

                half along = input.sweep.w + EdgeNoise(input.sweep.xyz) * _SweepNoise;
                half2 r = Sweep(_TintRSweep, along);
                half2 g = Sweep(_TintGSweep, along);
                half2 b = Sweep(_TintBSweep, along);

                half3 albedo = base.rgb;
                half2 surface = half2(_Metallic, _Smoothness);

                albedo = lerp(albedo, base.rgb * lerp(_TintRFrom.rgb, _TintRTo.rgb, r.x), mask.r);
                surface = lerp(surface, lerp(_TintRSurfaceFrom.xy, _TintRSurfaceTo.xy, r.x), mask.r);
                albedo = lerp(albedo, base.rgb * lerp(_TintGFrom.rgb, _TintGTo.rgb, g.x), mask.g);
                surface = lerp(surface, lerp(_TintGSurfaceFrom.xy, _TintGSurfaceTo.xy, g.x), mask.g);
                albedo = lerp(albedo, base.rgb * lerp(_TintBFrom.rgb, _TintBTo.rgb, b.x), mask.b);
                surface = lerp(surface, lerp(_TintBSurfaceFrom.xy, _TintBSurfaceTo.xy, b.x), mask.b);

                // A woven look for fabric, from the UVs alone: no extra texture. It fades out as the
                // threads approach the size of a pixel, where a pattern with no mip maps would shimmer.
                float2 weave = input.uv * float2(_DetailScale, _DetailScale * 0.7);
                half threads = sin(weave.x) * sin(weave.y) * 0.5 + 0.5;
                half resolved = saturate(1.45 - max(fwidth(weave.x), fwidth(weave.y)) * 0.65);
                albedo *= 1.0 - _DetailStrength * resolved * 0.3 * threads;

                half3 normalWS = normalize(input.normalWS);
                half3 viewWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.positionCS = input.positionCS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = viewWS;
                inputData.bakedGI = SampleSH(normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                // The glow only shows on a region while that region is mid-sweep.
                half glow = r.y * mask.r + g.y * mask.g + b.y * mask.b;
                half sheen = saturate(1.0 - abs(input.sweep.w - _SheenPosition) / _SheenWidth);
                sheen = sheen * sheen * step(-0.5, _SheenPosition);
                half rim = pow(1.0 - saturate(dot(normalWS, viewWS)), _RimPower) * _Rim;

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedo;
                surfaceData.metallic = surface.x;
                surfaceData.smoothness = surface.y;
                surfaceData.occlusion = 1.0;
                surfaceData.alpha = 1.0;
                surfaceData.normalTS = half3(0, 0, 1);
                surfaceData.emission = glow * _SweepGlow.rgb + sheen * _SheenColor.rgb + rim * _RimColor.rgb;

                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                // Opaque: full alpha covers the room behind it...
                color.a = 1.0;
                // ...except where something real is in front. The eye buffer is blended over passthrough
                // as premultiplied alpha, so colour and alpha are scaled together.
                META_DEPTH_OCCLUDE_OUTPUT_PREMULTIPLY_WORLDPOS(input.positionWS, color, _OcclusionBias);
                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag

            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings ShadowVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                output.positionCS = positionCS;
                return output;
            }

            half4 ShadowFrag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DepthVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 DepthFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return input.positionCS.z;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Simple Lit"
}
