Shader "Custom/OcclusionGhost"
{
    Properties
    {
        [Header(Body Tint)]
        _TintColor ("Tint Color (A = opacity)", Color) = (0.55, 0.7, 0.85, 0.06)
        _LightInfluence ("Main Light Shading", Range(0.0, 1.0)) = 0.5

        [Header(Rim)]
        [HDR] _RimColor ("Rim Color (A = opacity)", Color) = (0.7, 0.85, 1.0, 0.2)
        _RimPower ("Rim Power", Range(0.5, 8.0)) = 3.0

        [HideInInspector] _GhostOpacity ("Ghost Opacity", Range(0.0, 1.0)) = 1.0

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

        CBUFFER_START(UnityPerMaterial)
            half4 _TintColor;
            half _LightInfluence;
            half4 _RimColor;
            half _RimPower;
            half _GhostOpacity;
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
            #pragma target 2.0
            #pragma vertex GhostVert
            #pragma fragment GhostFrag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float fogFactor : TEXCOORD2;
            };

            Varyings GhostVert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
                return output;
            }

            half4 GhostFrag(Varyings input, bool isFrontFace : SV_IsFrontFace) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS) * (isFrontFace ? 1.0 : -1.0);
                float3 viewDirWS = normalize(GetWorldSpaceViewDir(input.positionWS));

                half3 bodyColor = _TintColor.rgb;

                Light mainLight = GetMainLight();
                half lambert = saturate(dot(normalWS, mainLight.direction)) * 0.5 + 0.5;
                bodyColor *= lerp(1.0, lambert * mainLight.color, _LightInfluence);

                half rim = pow(1.0 - saturate(dot(normalWS, viewDirWS)), _RimPower);
                half3 color = bodyColor + _RimColor.rgb * rim * _RimColor.a;
                half alpha = saturate(_TintColor.a + rim * _RimColor.a) * _GhostOpacity;

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
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack Off
}