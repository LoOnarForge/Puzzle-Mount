Shader "Custom/LaserBeamLine"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        [HDR] _Color ("Tint", Color) = (1, 1, 1, 1)
        [HDR] _EmissionColor ("Emission Color", Color) = (0, 0, 0, 0)
        [HDR] _EmissionStrength ("Emission Strength", Float) = 1
        _EndFadeLength ("End Fade (UV length)", Range(0, 1)) = 0.18
        _EdgeSoftness ("Edge Softness", Range(0.01, 0.5)) = 0.22
        _MinBrightness ("Min Ambient Brightness", Range(0, 1)) = 0
        _LightInfluence ("Light Influence", Range(0, 1)) = 0
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

            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _ADDITIONAL_LIGHTS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _BaseColor;
                half4 _Color;
                half4 _EmissionColor;
                half _EmissionStrength;
                half _EndFadeLength;
                half _EdgeSoftness;
                half _MinBrightness;
                half _LightInfluence;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
            };

            half3 LambertLight(half3 normalWS, Light light)
            {
                return LightingLambert(light.color, light.direction, normalWS)
                    * light.distanceAttenuation
                    * light.shadowAttenuation;
            }

            half3 CalculateDirectLighting(half3 normalWS, float3 positionWS)
            {
                Light mainLight = GetMainLight();
                half3 lighting = LambertLight(normalWS, mainLight);

                #if defined(_ADDITIONAL_LIGHTS)
                    uint pixelLightCount = GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(pixelLightCount)
                        Light light = GetAdditionalLight(lightIndex, positionWS, half4(1, 1, 1, 1));
                        lighting += LambertLight(normalWS, light);
                    LIGHT_LOOP_END
                #endif

                return lighting;
            }

            half BeamAlpha(half2 uv)
            {
                half fadeStart = 1.0h - _EndFadeLength;
                half along = smoothstep(1.0h, fadeStart, uv.x);

                half edge = min(uv.y, 1.0h - uv.y) * 2.0h;
                half across = smoothstep(0.0h, _EdgeSoftness, edge);

                return along * across;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 sprite = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half shapeAlpha = BeamAlpha(input.uv);
                half opacity = sprite.a * _BaseColor.a * _Color.a;
                half alpha = opacity * shapeAlpha;

                half3 tint = sprite.rgb * _BaseColor.rgb * _Color.rgb;

                half3 normalWS = NormalizeNormalPerPixel(input.normalWS);
                half3 ambient = SampleSH(normalWS);
                ambient = max(ambient, half3(_MinBrightness, _MinBrightness, _MinBrightness));

                half3 lighting = ambient + CalculateDirectLighting(normalWS, input.positionWS);
                half3 lit = lerp(tint, tint * lighting, _LightInfluence);
                half3 emission = _EmissionColor.rgb * _EmissionStrength * shapeAlpha;
                half3 premulRgb = lit * alpha + emission;

                return half4(premulRgb, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
