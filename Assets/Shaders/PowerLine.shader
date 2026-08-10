Shader "Custom/PowerLine"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _HighlightColor ("Highlight Color", Color) = (1, 1, 1, 1)
        [HDR] _EmissionColor ("Emission Color", Color) = (0, 0, 0, 0)
        [HDR] _EmissionStrength ("Emission Strength", Float) = 2
        _MinBrightness ("Min Brightness", Range(0, 1)) = 0.35
        _LightInfluence ("Light Influence", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "CanUseSpriteAtlas" = "True"
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _HighlightColor;
                half4 _EmissionColor;
                half _EmissionStrength;
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
                half3 normalWS : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 sprite = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half3 tint = sprite.rgb * _BaseColor.rgb * _HighlightColor.rgb;
                half alpha = sprite.a * _BaseColor.a * _HighlightColor.a;

                half3 normalWS = normalize(input.normalWS);
                Light mainLight = GetMainLight();
                half ndotl = saturate(dot(normalWS, mainLight.direction));
                half3 ambient = SampleSH(normalWS);
                half3 lighting = mainLight.color * ndotl * mainLight.distanceAttenuation + ambient;
                lighting = max(lighting, half3(_MinBrightness, _MinBrightness, _MinBrightness));

                half3 lit = lerp(tint, tint * lighting, _LightInfluence);
                half3 emission = _EmissionColor.rgb * _EmissionStrength * alpha;
                return half4(lit + emission, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
