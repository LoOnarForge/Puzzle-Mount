Shader "Custom/TimXRaySilhouette"
{
    Properties
    {
        _FillColor ("Fill Color", Color) = (0.2, 0.75, 1.0, 0.25)
        _RimColor ("Rim Color", Color) = (0.4, 0.9, 1.0, 0.9)
        _RimPower ("Rim Power", Range(0.5, 6.0)) = 2.2
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _FillColor;
            half4 _RimColor;
            float _RimPower;
        CBUFFER_END

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
        };

        Varyings Vert(Attributes input)
        {
            Varyings output;
            VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
            output.positionCS = posInputs.positionCS;
            output.positionWS = posInputs.positionWS;
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            return output;
        }
        ENDHLSL

        Pass
        {
            Name "TimVisibleMask"
            Cull Back
            ZWrite Off
            ZTest LEqual
            ColorMask 0

            Stencil
            {
                Ref 64
                ReadMask 64
                WriteMask 64
                Comp Always
                Pass Replace
            }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragMask

            half4 FragMask(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "TimOccludedSilhouette"
            Cull Back
            ZWrite Off
            ZTest Greater
            Blend SrcAlpha OneMinusSrcAlpha

            Stencil
            {
                Ref 64
                ReadMask 64
                WriteMask 64
                Comp NotEqual
                Pass Replace
            }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragSilhouette

            half4 FragSilhouette(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(GetWorldSpaceViewDir(input.positionWS));
                float ndotv = saturate(dot(normalWS, viewDirWS));
                float rim = pow(1.0 - ndotv, _RimPower);
                return lerp(_FillColor, _RimColor, rim);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
