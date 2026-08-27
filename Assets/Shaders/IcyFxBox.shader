Shader "Custom/IcyFxBox"
{
    Properties
    {
        [Header(Textures)]
        _MainTex ("Main", 2D) = "white" {}
        _NoiseTex ("UV Noise", 2D) = "black" {}
        _GlowTex ("Edge Glow", 2D) = "black" {}

        [Header(Ice Colors)]
        [HDR] _ColorDark ("Dark Ice", Color) = (0, 0.03, 0.32, 1)
        [HDR] _ColorBright ("Bright Ice", Color) = (0, 0.34, 0.56, 1)
        [HDR] _GlowColor ("Glow", Color) = (0.34, 0.49, 0.86, 1)

        [Header(Noise)]
        _NoiseScroll ("Noise Scroll", Vector) = (0, 0.2, 0, 0.32)
        _NoiseStrength ("Noise Strength", Range(0, 1)) = 0.3

        [Header(Look)]
        _Brightness ("Brightness", Range(0, 4)) = 0.83
        _Contrast ("Contrast", Range(0, 2)) = 1.36
        _Alpha ("Alpha", Range(0, 1)) = 1
        _Emission ("Emission", Range(0, 6)) = 1
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

            Blend One One
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
            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);
            TEXTURE2D(_GlowTex);
            SAMPLER(sampler_GlowTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _NoiseTex_ST;
                float4 _GlowTex_ST;
                half4 _ColorDark;
                half4 _ColorBright;
                half4 _GlowColor;
                half4 _NoiseScroll;
                half _NoiseStrength;
                half _Brightness;
                half _Contrast;
                half _Alpha;
                half _Emission;
            CBUFFER_END

            static const half _ColorThreshold = 0.568h;
            static const half _ColorSmoothness = 0.5h;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            half Luma(half3 rgb)
            {
                return dot(rgb, half3(0.299h, 0.587h, 0.114h));
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 mainUV = input.uv * _MainTex_ST.xy + _MainTex_ST.zw;
                float2 noiseUV = input.uv * _NoiseTex_ST.xy + _NoiseTex_ST.zw + _Time.y * _NoiseScroll.zw;
                float2 glowUV = input.uv * _GlowTex_ST.xy + _GlowTex_ST.zw;

                half2 uvDistort = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, noiseUV).rg * _NoiseStrength;
                half4 mainSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, mainUV + uvDistort);

                half lum = Luma(mainSample.rgb);
                lum = saturate((lum - 0.5h) * _Contrast + 0.5h) * _Brightness;
                half blend = smoothstep(_ColorThreshold - _ColorSmoothness, _ColorThreshold + _ColorSmoothness, lum);

                half3 rgb = lerp(_ColorDark.rgb, _ColorBright.rgb, blend);
                half alpha = mainSample.a * _Alpha;

                half3 glow = SAMPLE_TEXTURE2D(_GlowTex, sampler_GlowTex, glowUV).rgb * _GlowColor.rgb;
                rgb += alpha * glow * _GlowColor.a;

                rgb *= alpha * _Emission;
                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
