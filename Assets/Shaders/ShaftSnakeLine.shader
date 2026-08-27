Shader "Custom/ShaftSnakeLine"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        [HDR] _Color ("Color", Color) = (1, 1, 1, 1)
        [HDR] _EmissionColor ("Emission Color", Color) = (1, 1, 1, 1)
        _EmissionStrength ("Emission Strength", Float) = 2
        _FadePower ("Vertex Fade Power", Range(0.1, 4)) = 1
        [Toggle] _CrossSectionOnly ("Cross Section Only (ignore length repeat)", Float) = 1
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
            "PreviewType" = "Plane"
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
                half4 _Color;
                half4 _EmissionColor;
                half _EmissionStrength;
                half _FadePower;
                half _CrossSectionOnly;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 sampleUV = TRANSFORM_TEX(input.uv, _MainTex);
                if (_CrossSectionOnly > 0.5)
                    sampleUV.x = _MainTex_ST.z + _MainTex_ST.x * 0.5;

                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, sampleUV);
                half fade = pow(saturate(input.color.a), _FadePower);
                half alpha = tex.a * _Color.a * fade;
                half3 tint = tex.rgb * _Color.rgb * input.color.rgb;
                half3 emission = _EmissionColor.rgb * _EmissionStrength * alpha * input.color.rgb;
                return half4(tint + emission, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
