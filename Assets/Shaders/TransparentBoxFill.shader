Shader "Custom/TransparentBoxFill"
{
    Properties
    {
        [Header(Base)]
        _BaseColor ("Base Color", Color) = (0.25, 0.65, 1, 0.25)
        _Alpha ("Transparency", Range(0, 1)) = 0.25

        [Header(Edges)]
        _EdgeWidth ("Edge Thickness (World)", Range(0.001, 0.2)) = 0.04
        _EdgeStrength ("Edge Strength", Range(0, 3)) = 1.5
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

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Alpha;
                half _EdgeWidth;
                half _EdgeStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
            };

            // Edge thickness is measured in world units, independent of transform scale.
            half ComputeBoxEdge(float3 positionOS, half edgeWorldWidth)
            {
                const float3 halfExtents = float3(0.5, 0.5, 0.5);

                float3x3 objectToWorld3x3 = (float3x3)GetObjectToWorldMatrix();
                float3 scale = float3(
                    length(objectToWorld3x3[0]),
                    length(objectToWorld3x3[1]),
                    length(objectToWorld3x3[2]));

                float3 faceWorldSize = halfExtents * scale;
                float3 edgeNorm = edgeWorldWidth / max(faceWorldSize, 1e-4);

                float3 pNorm = abs(positionOS) / halfExtents;
                float3 dpNorm = fwidth(pNorm);
                float3 limitNorm = 1.0 - edgeNorm;

                float3 nearBoundary = smoothstep(limitNorm - dpNorm, limitNorm + dpNorm, pNorm);
                return max(
                    max(nearBoundary.x * nearBoundary.y, nearBoundary.y * nearBoundary.z),
                    nearBoundary.x * nearBoundary.z);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionOS = input.positionOS.xyz;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half edge = ComputeBoxEdge(input.positionOS, _EdgeWidth);

                half3 rgb = _BaseColor.rgb * (1.0h + edge * _EdgeStrength);
                half alpha = _Alpha * (1.0h + edge * _EdgeStrength);

                return half4(rgb, saturate(alpha));
            }
            ENDHLSL
        }
    }

    FallBack Off
}
