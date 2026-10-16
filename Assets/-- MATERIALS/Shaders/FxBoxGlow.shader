Shader "Custom/FxBoxGlow"
{
    Properties
    {
        [Header(Colors)]
        [HDR] _InteriorColor ("Interior Color", Color) = (0.02, 0.08, 0.35, 1)
        [HDR] _EdgeColor ("Edge Color", Color) = (0.5, 0.85, 1.4, 1)
        [HDR] _MistColor ("Mist Color", Color) = (0.08, 0.35, 0.85, 1)
        _EmissionStrength ("Emission Strength", Range(0, 6)) = 1.4

        [Header(Edge Lines)]
        _EdgeLineWidth ("Edge Line Width", Range(0.005, 0.08)) = 0.022
        _EdgeIntensity ("Edge Intensity", Range(0, 4)) = 1.2

        [Header(Soft Rim)]
        _FresnelPower ("Fresnel Power", Range(1, 12)) = 6
        _FresnelIntensity ("Fresnel Intensity", Range(0, 1)) = 0.2

        [Header(Mist)]
        _NoiseTex ("Mist Noise", 2D) = "white" {}
        _NoiseScale ("Noise Scale", Range(0.1, 8)) = 1.4
        _NoiseSpeed ("Noise Scroll XY / ZW", Vector) = (0.05, 0.03, -0.025, 0.04)
        _MistIntensity ("Mist Intensity", Range(0, 3)) = 1.5
        _MistContrast ("Mist Contrast", Range(0.5, 4)) = 1.3
        _MistFill ("Interior Fill", Range(0, 1)) = 0.45

        [Header(Blend)]
        _Alpha ("Alpha", Range(0, 1)) = 0.35
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

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _InteriorColor;
                half4 _EdgeColor;
                half4 _MistColor;
                half _EmissionStrength;
                half _EdgeLineWidth;
                half _EdgeIntensity;
                half _FresnelPower;
                half _FresnelIntensity;
                half4 _NoiseTex_ST;
                half _NoiseScale;
                half4 _NoiseSpeed;
                half _MistIntensity;
                half _MistContrast;
                half _MistFill;
                half _Alpha;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 viewDirWS : TEXCOORD2;
            };

            half ComputeThinEdgeLines(float3 positionOS, half lineWidth)
            {
                const half halfExt = 0.5h;
                float3 fromFace = halfExt - abs(positionOS);

                half w = max(lineWidth, 1e-4h);
                half nearX = 1.0h - smoothstep(0.0h, w, fromFace.x);
                half nearY = 1.0h - smoothstep(0.0h, w, fromFace.y);
                half nearZ = 1.0h - smoothstep(0.0h, w, fromFace.z);

                half alongX = nearY * nearZ;
                half alongY = nearX * nearZ;
                half alongZ = nearX * nearY;
                return saturate(max(alongX, max(alongY, alongZ)));
            }

            half SampleMist(float3 positionOS, float time)
            {
                float3 p = positionOS * _NoiseScale;
                float2 speedA = _NoiseSpeed.xy * time;
                float2 speedB = _NoiseSpeed.zw * time;

                half n1 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, p.xy + speedA).r;
                half n2 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, p.yz + speedB).r;
                half n3 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, p.xz - speedA * 0.7h).r;
                half n4 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, p.xy * 1.7h - speedB).r;
                return (n1 + n2 + n3 + n4) * 0.25h;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.positionOS = input.positionOS.xyz;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewDirWS = GetWorldSpaceViewDir(vertexInput.positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half edgeLine = ComputeThinEdgeLines(input.positionOS, _EdgeLineWidth);

                half mist = SampleMist(input.positionOS, _Time.y);
                mist = pow(saturate(mist), _MistContrast) * _MistIntensity;

                half3 fill =
                    _InteriorColor.rgb * _MistFill +
                    _MistColor.rgb * mist;

                half3 rgb = fill;
                rgb += _EdgeColor.rgb * edgeLine * _EdgeIntensity;

                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(input.viewDirWS);
                half fresnel = pow(1.0h - saturate(dot(normalWS, viewDirWS)), _FresnelPower);
                rgb += _EdgeColor.rgb * fresnel * _FresnelIntensity;

                rgb *= _EmissionStrength;

                half alpha = _Alpha * saturate(_MistFill * 0.35h + mist * 0.55h + edgeLine * 0.9h + fresnel * 0.15h);
                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
