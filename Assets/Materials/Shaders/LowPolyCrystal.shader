Shader "Custom/LowPolyCrystal"
{
    Properties
    {
        [Header(Colors)]
        [HDR] _FaceColor ("Face Color", Color) = (0.05, 0.15, 0.45, 1)
        [HDR] _RimColor ("Rim Color", Color) = (0.3, 0.85, 1.4, 1)

        [Header(Fresnel)]
        _FresnelPower ("Fresnel Power", Range(0.5, 12)) = 2.5
        _BaseSuppress ("Flat Base Suppress", Range(0, 1)) = 0.95

        [Header(Crease Edges)]
        _CreaseStrength ("Crease Strength", Range(0, 20)) = 6
        _CreaseSharpness ("Crease Sharpness", Range(1, 16)) = 4

        [Header(Specular)]
        [HDR] _SpecularColor ("Specular Color", Color) = (1, 1, 1, 1)
        _SpecularStrength ("Specular Strength", Range(0, 4)) = 1.2
        _SpecularPower ("Specular Power", Range(8, 256)) = 64

        [Header(Transparency)]
        _Alpha ("Alpha", Range(0, 1)) = 0.5
        _EdgeAlpha ("Edge Alpha Boost", Range(0, 0.5)) = 0.25
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
            Name "Crystal"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _FaceColor;
                half4 _RimColor;
                half _FresnelPower;
                half _BaseSuppress;
                half _CreaseStrength;
                half _CreaseSharpness;
                half4 _SpecularColor;
                half _SpecularStrength;
                half _SpecularPower;
                half _Alpha;
                half _EdgeAlpha;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
                half fogFactor : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewDirWS = GetWorldSpaceViewDir(vertexInput.positionWS);
                output.fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half3 normalWS = normalize(input.normalWS);
                half3 viewDirWS = normalize(input.viewDirWS);
                half ndotv = saturate(dot(normalWS, viewDirWS));

                half fresnel = pow(1.0h - ndotv, _FresnelPower);
                fresnel *= 1.0h - saturate(-normalWS.y) * _BaseSuppress;

                half3 rgb = lerp(_FaceColor.rgb, _RimColor.rgb, fresnel);

                half crease = length(float2(abs(fwidth(normalWS.x)) + abs(fwidth(normalWS.y)) + abs(fwidth(normalWS.z)),
                                              abs(fwidth(fresnel))));
                crease = pow(saturate(crease * _CreaseStrength), _CreaseSharpness);
                rgb += _RimColor.rgb * crease;

                Light mainLight = GetMainLight();
                half3 halfDir = normalize(mainLight.direction + viewDirWS);
                half spec = pow(saturate(dot(normalWS, halfDir)), _SpecularPower) * _SpecularStrength;
                rgb += _SpecularColor.rgb * spec;

                half alpha = lerp(_Alpha * 0.55h, _Alpha, fresnel);
                alpha = saturate(alpha + crease * _EdgeAlpha);

                rgb = MixFog(rgb, input.fogFactor);
                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
