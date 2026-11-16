Shader "Custom/OcclusionGhost"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map (faint texture hint)", 2D) = "white" {}
        _TextureInfluence ("Texture Influence", Range(0.0, 1.0)) = 0.35

        [Header(Body Tint)]
        _TintColor ("Tint Color (A = opacity)", Color) = (0.55, 0.7, 0.85, 0.06)
        _LightInfluence ("Main Light Shading", Range(0.0, 1.0)) = 0.5

        [Header(Wireframe)]
        [HDR] _WireColor ("Wire Color (A = opacity)", Color) = (0.75, 0.9, 1.0, 0.35)
        _WireThickness ("Wire Thickness (pixels)", Range(0.25, 4.0)) = 1.0
        _WireSmoothing ("Wire Smoothing (pixels)", Range(0.1, 3.0)) = 1.0
        [Toggle] _HideDiagonals ("Hide Quad Diagonals", Float) = 1
        _WireFadeDistance ("Wire Fade Distance (0 = off)", Float) = 0

        [Header(Rim)]
        [HDR] _RimColor ("Rim Color (A = opacity)", Color) = (0.7, 0.85, 1.0, 0.2)
        _RimPower ("Rim Power", Range(0.5, 8.0)) = 3.0

        [Header(Occlusion)]
        _OcclusionFade ("Occlusion Fade", Range(0.0, 1.0)) = 1.0

        [Header(Shadows)]
        [Toggle] _CastShadows ("Cast Shadows (keeps original shadow)", Float) = 1

        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull Mode", Float) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half _TextureInfluence;
            half4 _TintColor;
            half _LightInfluence;
            half4 _WireColor;
            half _WireThickness;
            half _WireSmoothing;
            half _HideDiagonals;
            float _WireFadeDistance;
            half4 _RimColor;
            half _RimPower;
            half _CastShadows;
            half _OcclusionFade;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "GhostForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 4.0
            #pragma require geometry
            #pragma vertex GhostVert
            #pragma geometry GhostGeom
            #pragma fragment GhostFrag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // Longest edge must exceed the other two by this ratio to count as a quad diagonal (square diagonal = 1.414).
            #define GHOST_DIAGONAL_RATIO 1.2
            #define GHOST_EDGE_EXCLUDED 1e6

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 texcoord : TEXCOORD0;
            };

            struct VertToGeom
            {
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            struct GeomToFrag
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float3 barycentric : TEXCOORD3;
                nointerpolation float3 edgeExclusion : TEXCOORD4;
                float fogFactor : TEXCOORD5;
            };

            VertToGeom GhostVert(Attributes input)
            {
                VertToGeom output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
                return output;
            }

            // Barycentric component i measures distance to the edge opposite vertex i.
            float3 ComputeDiagonalExclusion(float3 p0, float3 p1, float3 p2)
            {
                float edge0 = distance(p1, p2);
                float edge1 = distance(p0, p2);
                float edge2 = distance(p0, p1);

                float3 exclusion = 0;
                if (_HideDiagonals < 0.5)
                    return exclusion;

                if (edge0 > edge1 * GHOST_DIAGONAL_RATIO && edge0 > edge2 * GHOST_DIAGONAL_RATIO)
                    exclusion.x = GHOST_EDGE_EXCLUDED;
                else if (edge1 > edge0 * GHOST_DIAGONAL_RATIO && edge1 > edge2 * GHOST_DIAGONAL_RATIO)
                    exclusion.y = GHOST_EDGE_EXCLUDED;
                else if (edge2 > edge0 * GHOST_DIAGONAL_RATIO && edge2 > edge1 * GHOST_DIAGONAL_RATIO)
                    exclusion.z = GHOST_EDGE_EXCLUDED;

                return exclusion;
            }

            [maxvertexcount(3)]
            void GhostGeom(triangle VertToGeom input[3], inout TriangleStream<GeomToFrag> stream)
            {
                float3 exclusion = ComputeDiagonalExclusion(input[0].positionWS, input[1].positionWS, input[2].positionWS);
                float3 corners[3] = { float3(1, 0, 0), float3(0, 1, 0), float3(0, 0, 1) };

                [unroll]
                for (int i = 0; i < 3; i++)
                {
                    GeomToFrag output;
                    output.positionWS = input[i].positionWS;
                    output.positionCS = TransformWorldToHClip(input[i].positionWS);
                    output.normalWS = input[i].normalWS;
                    output.uv = input[i].uv;
                    output.barycentric = corners[i];
                    output.edgeExclusion = exclusion;
                    output.fogFactor = ComputeFogFactor(output.positionCS.z);
                    stream.Append(output);
                }
            }

            // Returns 1 on a wire edge, 0 inside the face, with screen-space constant thickness.
            half EvaluateWire(float3 barycentric, float3 exclusion)
            {
                float3 pixelDistance = barycentric / max(fwidth(barycentric), 1e-5) + exclusion;
                float minDistance = min(pixelDistance.x, min(pixelDistance.y, pixelDistance.z));
                return 1.0 - smoothstep(_WireThickness, _WireThickness + _WireSmoothing, minDistance);
            }

            half4 GhostFrag(GeomToFrag input, bool isFrontFace : SV_IsFrontFace) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS) * (isFrontFace ? 1.0 : -1.0);
                float3 viewDirWS = normalize(GetWorldSpaceViewDir(input.positionWS));

                half3 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb;
                half3 bodyColor = _TintColor.rgb * lerp(half3(1, 1, 1), texColor, _TextureInfluence);

                Light mainLight = GetMainLight();
                half lambert = saturate(dot(normalWS, mainLight.direction)) * 0.5 + 0.5;
                bodyColor *= lerp(1.0, lambert * mainLight.color, _LightInfluence);

                half wire = EvaluateWire(input.barycentric, input.edgeExclusion);
                if (_WireFadeDistance > 0.0)
                    wire *= saturate(1.0 - distance(input.positionWS, GetCameraPositionWS()) / _WireFadeDistance);

                half rim = pow(1.0 - saturate(dot(normalWS, viewDirWS)), _RimPower);

                half3 color = lerp(bodyColor, _WireColor.rgb, wire * _WireColor.a);
                color += _RimColor.rgb * rim * _RimColor.a;
                half alpha = saturate(_TintColor.a + wire * _WireColor.a + rim * _RimColor.a);
                alpha *= _OcclusionFade;

                color = MixFog(color, input.fogFactor);
                return half4(color, alpha);
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
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag

            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            ShadowVaryings ShadowVert(ShadowAttributes input)
            {
                ShadowVaryings output;
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

            half4 ShadowFrag(ShadowVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                clip(min(_CastShadows, _OcclusionFade) - 0.5);
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
