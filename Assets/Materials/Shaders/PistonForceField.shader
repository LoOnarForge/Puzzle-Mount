Shader "Custom/PistonForceField"
{
    Properties
    {
        [Header(Base)]
        _MainTex ("Texture", 2D) = "white" {}
        [HDR] _MainColor ("Main Color", Color) = (0.25, 0.65, 1, 0.35)
        _Alpha ("Transparency", Range(0, 1)) = 0.35

        [Header(Scroll)]
        _ScrollSpeed ("UV Scroll Speed", Vector) = (0, 0.12, 0, 0)

        [Header(Force Field)]
        _FresnelPower ("Fresnel Power", Range(0.1, 8)) = 2.5
        _FresnelStrength ("Fresnel Strength", Range(0, 2)) = 0.75

        [Header(Box Edges)]
        _EdgeWidth ("Edge Width", Range(0.005, 0.2)) = 0.06
        [HDR] _EdgeColor ("Edge Color", Color) = (0.5, 0.9, 1, 1)
        _EdgeStrength ("Edge Strength", Range(0, 3)) = 1.5
        _EdgeAlphaBoost ("Edge Alpha Boost", Range(0, 1)) = 0.55

        [Header(Bloom)]
        [HDR] _EmissionColor ("Emission Color", Color) = (0.4, 0.75, 1, 1)
        _EmissionStrength ("Emission Strength", Range(0, 5)) = 1.25
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _MainColor;
                half _Alpha;
                float4 _ScrollSpeed;
                half _FresnelPower;
                half _FresnelStrength;
                half _EdgeWidth;
                half4 _EdgeColor;
                half _EdgeStrength;
                half _EdgeAlphaBoost;
                half4 _EmissionColor;
                half _EmissionStrength;
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
                float3 positionOS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float3 viewDirWS : TEXCOORD3;
            };

            // Highlights cube edges and corners in object space (Unity cube verts sit at +/- 0.5).
            half ComputeBoxEdge(float3 positionOS, half edgeWidth)
            {
                float3 p = abs(positionOS);
                float3 dp = fwidth(positionOS);
                half limit = 0.5h - edgeWidth;
                float3 nearBoundary = smoothstep(limit - dp, limit + dp, p);
                return max(
                    max(nearBoundary.x * nearBoundary.y, nearBoundary.y * nearBoundary.z),
                    nearBoundary.x * nearBoundary.z);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);

                output.positionCS = vertexInput.positionCS;
                output.positionOS = input.positionOS.xyz;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewDirWS = GetWorldSpaceViewDir(vertexInput.positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 scrolledUv = input.uv + _ScrollSpeed.xy * _Time.y;
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, scrolledUv);

                half3 normal = normalize(input.normalWS);
                half3 viewDir = normalize(input.viewDirWS);
                half fresnel = pow(1.0h - saturate(dot(normal, viewDir)), _FresnelPower);
                half edge = ComputeBoxEdge(input.positionOS, _EdgeWidth);

                half3 baseRgb = tex.rgb * _MainColor.rgb;
                half baseAlpha = tex.a * _Alpha;

                half3 fresnelRgb = _MainColor.rgb * fresnel * _FresnelStrength;
                half fresnelAlpha = fresnel * _FresnelStrength * _Alpha;

                half3 edgeRgb = _EdgeColor.rgb * edge * _EdgeStrength;
                half edgeAlpha = edge * _EdgeAlphaBoost;

                half emissionMask = baseAlpha + fresnel * 0.5h + edge;
                half3 emission = _EmissionColor.rgb * _EmissionStrength * emissionMask;

                half3 color = baseRgb + fresnelRgb + edgeRgb + emission;
                half alpha = saturate(baseAlpha + fresnelAlpha + edgeAlpha);

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
