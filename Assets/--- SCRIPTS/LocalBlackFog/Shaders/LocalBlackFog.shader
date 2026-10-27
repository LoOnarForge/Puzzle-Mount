Shader "Custom/LocalBlackFog"
{
    Properties
    {
        _FogDensity ("Fog Density", Range(0, 100)) = 2
        _FogEdgeSoftness ("Edge Softness", Range(0.001, 0.5)) = 0.08
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" }

        Pass
        {
            Name "LocalBlackFogComposite"
            ZTest Always
            ZWrite Off
            Cull Off
            Blend One Zero

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D_X(_FogSceneColor);
            SAMPLER(sampler_FogSceneColor);
            TEXTURE2D_X(_FogSceneDepth);
            SAMPLER(sampler_FogSceneDepth);

            struct FogVolumeGpu
            {
                float4x4 worldToLocal;
                float4x4 localToWorld;
                float4 referencePosition;
                float4 fogParams;
            };

            StructuredBuffer<FogVolumeGpu> _FogVolumes;
            int _FogVolumeCount;

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.uv = GetFullScreenTriangleTexCoord(input.vertexID);
                output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
                return output;
            }

            bool IntersectUnitBox(float3 ro, float3 rd, out float tEnter, out float tExit)
            {
                tEnter = -1e30;
                tExit = 1e30;

                [unroll]
                for (int axis = 0; axis < 3; axis++)
                {
                    float roA = ro[axis];
                    float rdA = rd[axis];

                    if (abs(rdA) < 1e-6)
                    {
                        if (roA < -0.5 || roA > 0.5)
                            return false;
                        continue;
                    }

                    float inv = 1.0 / rdA;
                    float t0 = (-0.5 - roA) * inv;
                    float t1 = (0.5 - roA) * inv;
                    float tMin = min(t0, t1);
                    float tMax = max(t0, t1);
                    tEnter = max(tEnter, tMin);
                    tExit = min(tExit, tMax);
                }

                return tExit >= max(tEnter, 0.0);
            }

            float VolumeEdgeFactor(float3 pOs, float edgeSoftness)
            {
                float edge = min(min(0.5 - abs(pOs.x), 0.5 - abs(pOs.y)), 0.5 - abs(pOs.z));
                return smoothstep(0.0, edgeSoftness, edge);
            }

            float VolumeDistanceObscurance(float3 pWs, float3 refPos, float clearRadius, float obscureRadius)
            {
                float dist = distance(pWs, refPos);
                return smoothstep(clearRadius, obscureRadius, dist);
            }

            float VolumeTransmittance(FogVolumeGpu vol, float3 camPos, float3 rd, float tScene)
            {
                float3 roOs = mul(vol.worldToLocal, float4(camPos, 1.0)).xyz;
                float3 rdOs = mul(vol.worldToLocal, float4(rd, 0.0)).xyz;

                float tEnterOs;
                float tExitOs;
                if (!IntersectUnitBox(roOs, rdOs, tEnterOs, tExitOs))
                    return 1.0;

                float3 pEnterOs = roOs + rdOs * tEnterOs;
                float3 pExitOs = roOs + rdOs * tExitOs;
                float3 pEnterWs = mul(vol.localToWorld, float4(pEnterOs, 1.0)).xyz;
                float3 pExitWs = mul(vol.localToWorld, float4(pExitOs, 1.0)).xyz;

                float tEnter = dot(pEnterWs - camPos, rd);
                float tExit = dot(pExitWs - camPos, rd);
                if (tEnter > tExit)
                {
                    float tmp = tEnter;
                    tEnter = tExit;
                    tExit = tmp;
                }

                tEnter = max(tEnter, 0.0);
                tExit = min(tExit, tScene);
                if (tEnter >= tExit)
                    return 1.0;

                float density = vol.fogParams.x;
                float edgeSoftness = vol.fogParams.y;
                float clearRadius = vol.fogParams.z;
                float obscureRadius = vol.fogParams.w;
                float3 refPos = vol.referencePosition.xyz;

                const float kStepMeters = 0.35;
                const int kMaxSteps = 64;
                float segment = tExit - tEnter;
                int steps = clamp((int)(segment / kStepMeters) + 1, 1, kMaxSteps);
                float dt = segment / steps;

                float transmittance = 1.0;
                for (int i = 0; i < kMaxSteps; i++)
                {
                    if (i >= steps)
                        break;

                    float t = tEnter + (i + 0.5) * dt;
                    float3 pWs = camPos + rd * t;
                    float3 pOs = mul(vol.worldToLocal, float4(pWs, 1.0)).xyz;

                    if (any(abs(pOs) > 0.501))
                        continue;

                    float edgeMul = VolumeEdgeFactor(pOs, edgeSoftness);
                    float distMul = VolumeDistanceObscurance(pWs, refPos, clearRadius, obscureRadius);
                    transmittance *= exp(-density * edgeMul * distMul * dt);
                }

                return transmittance;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                half3 scene = SAMPLE_TEXTURE2D_X(_FogSceneColor, sampler_FogSceneColor, uv).rgb;

                float rawDepth = SAMPLE_TEXTURE2D_X(_FogSceneDepth, sampler_FogSceneDepth, uv).r;
                float3 worldHit = ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
                float3 camPos = _WorldSpaceCameraPos;
                float3 rd = normalize(worldHit - camPos);
                float tScene = dot(worldHit - camPos, rd);

                float transmittance = 1.0;
                int count = min(_FogVolumeCount, 16);
                for (int v = 0; v < count; v++)
                {
                    FogVolumeGpu vol = _FogVolumes[v];
                    transmittance *= VolumeTransmittance(vol, camPos, rd, tScene);
                }

                return half4(scene * transmittance, 1.0);
            }
            ENDHLSL
        }
    }
}
