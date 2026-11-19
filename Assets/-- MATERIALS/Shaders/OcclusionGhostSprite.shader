Shader "Custom/OcclusionGhostSprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}

        [Header(Body Tint)]
        _TintColor ("Tint Color (A = opacity scale)", Color) = (0.55, 0.7, 0.85, 0.45)
        _LightInfluence ("Main Light Shading", Range(0.0, 1.0)) = 0.35

        [Header(Existing Sprite Layers)]
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _HighlightColor ("Highlight Color", Color) = (1, 1, 1, 1)

        [HDR] _RimColor ("Rim Color (A = strength)", Color) = (0.7, 0.85, 1.0, 0.15)
        _RimPower ("Rim Power", Range(0.5, 8.0)) = 2.5

        [HideInInspector] _GhostOpacity ("Ghost Opacity", Range(0.0, 1.0)) = 1.0

        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull Mode", Float) = 0
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
            Name "SpriteGhostForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _TintColor;
                half _LightInfluence;
                half4 _BaseColor;
                half4 _HighlightColor;
                half4 _RimColor;
                half _RimPower;
                half _GhostOpacity;
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
                float3 normalWS : TEXCOORD2;
                float fogFactor : TEXCOORD3;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 sprite = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half3 spriteRgb = sprite.rgb * _BaseColor.rgb * _HighlightColor.rgb;
                half spriteAlpha = sprite.a * _BaseColor.a * _HighlightColor.a;

                half3 normalWS = normalize(input.normalWS);
                Light mainLight = GetMainLight();
                half lambert = saturate(dot(normalWS, mainLight.direction)) * 0.5 + 0.5;
                half3 litSprite = spriteRgb * lerp(1.0, lambert * mainLight.color, _LightInfluence);

                half3 body = litSprite * _TintColor.rgb;
                half alpha = spriteAlpha * _TintColor.a;

                float3 viewDirWS = normalize(GetWorldSpaceViewDir(input.positionWS));
                half rim = pow(1.0 - saturate(dot(normalWS, viewDirWS)), _RimPower);
                body += _RimColor.rgb * rim * _RimColor.a;
                alpha += rim * _RimColor.a;
                alpha = saturate(alpha) * _GhostOpacity;

                body = MixFog(body, input.fogFactor);
                return half4(body, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}