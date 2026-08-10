Shader "Custom/PowerLine"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)

        [Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Scale", Float) = 1

        [HDR] _PowerColor ("Power Color (Per Renderer)", Color) = (1, 1, 1, 1)
        _PowerAmount ("Power Amount (Per Renderer)", Range(0, 1)) = 0
        _PulseSpeed ("Pulse Speed (Per Renderer)", Float) = 1
        _PulseAmount ("Pulse Amount (Per Renderer)", Range(0, 1)) = 0.2
        _PulsePhase ("Pulse Phase (Per Renderer)", Float) = 0

        [HDR] _EmissionStrength ("Emission Strength", Float) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back
            Offset -1, -1

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap);
            SAMPLER(sampler_BumpMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _BumpScale;
                half _EmissionStrength;
            CBUFFER_END

            // Per-renderer props live here only. When instancing is off, Unity expands
            // these macros to a regular CBUFFER so MaterialPropertyBlock overrides work.
            // When instancing is on, MPB values are written into the instanced array.
            UNITY_INSTANCING_BUFFER_START(PowerLineProps)
                UNITY_DEFINE_INSTANCED_PROP(half4, _PowerColor)
                UNITY_DEFINE_INSTANCED_PROP(half, _PowerAmount)
                UNITY_DEFINE_INSTANCED_PROP(half, _PulseSpeed)
                UNITY_DEFINE_INSTANCED_PROP(half, _PulseAmount)
                UNITY_DEFINE_INSTANCED_PROP(half, _PulsePhase)
            UNITY_INSTANCING_BUFFER_END(PowerLineProps)

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half4 tangentWS : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            half3 CalculateLighting(half3 albedo, half3 normalWS, float3 positionWS, float4 shadowCoord)
            {
                Light mainLight = GetMainLight(shadowCoord);
                half3 lighting = LightingLambert(mainLight.color, mainLight.direction, normalWS)
                    * mainLight.distanceAttenuation
                    * mainLight.shadowAttenuation;

                #if defined(_ADDITIONAL_LIGHTS)
                    uint additionalLightCount = GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(additionalLightCount)
                        Light light = GetAdditionalLight(lightIndex, positionWS);
                        lighting += LightingLambert(light.color, light.direction, normalWS)
                            * light.distanceAttenuation
                            * light.shadowAttenuation;
                    LIGHT_LOOP_END
                #endif

                half3 bakedGI = SampleSH(normalWS);
                return albedo * (lighting + bakedGI);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.normalWS = normalInput.normalWS;
                output.tangentWS = half4(normalInput.tangentWS, input.tangentOS.w);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half3 albedo = baseSample.rgb * _BaseColor.rgb;
                half alpha = baseSample.a * _BaseColor.a;

                half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale);
                half3 bitangentWS = cross(input.normalWS, input.tangentWS.xyz) * input.tangentWS.w;
                half3 normalWS = TransformTangentToWorld(normalTS, half3x3(input.tangentWS.xyz, bitangentWS, input.normalWS));
                normalWS = NormalizeNormalPerPixel(normalWS);

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                half3 litColor = CalculateLighting(albedo, normalWS, input.positionWS, shadowCoord);

                half powerAmount = UNITY_ACCESS_INSTANCED_PROP(PowerLineProps, _PowerAmount);
                half pulseSpeed = UNITY_ACCESS_INSTANCED_PROP(PowerLineProps, _PulseSpeed);
                half pulseAmount = UNITY_ACCESS_INSTANCED_PROP(PowerLineProps, _PulseAmount);
                half pulsePhase = UNITY_ACCESS_INSTANCED_PROP(PowerLineProps, _PulsePhase);
                half4 powerColor = UNITY_ACCESS_INSTANCED_PROP(PowerLineProps, _PowerColor);

                half pulse = 1.0h + pulseAmount * sin(_Time.y * pulseSpeed + pulsePhase);
                half3 emission = powerColor.rgb * _EmissionStrength * powerAmount * pulse * alpha;

                return half4(litColor + emission, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
