#ifndef TIM_CUTOUT_INCLUDE_HLSL
#define TIM_CUTOUT_INCLUDE_HLSL

float _TimCutoutEnabled;
float4 _TimCutoutNearCenter;
float4 _TimCutoutFarCenter;
float4 _TimCutoutAxisForward;
float4 _TimCutoutAxisRight;
float4 _TimCutoutShapeParams;        // x: nearHalfW, y: nearHalfH, z: farHalfW, w: farHalfH
float4 _TimCutoutVisualParams;       // x: edgeSoftness, y: noiseScale, z: noiseStrength, w: edgeGlowWidth
half4 _TimCutoutEdgeColor;           // rgb: glow color, a: glow intensity
float4 _TimCutoutFeetBoxCenter;      // xyz: center, w: enabled (0 or 1)
float4 _TimCutoutFeetBoxHalfExtents; // xyz: half extents, w: feather

float TimCutoutHash31(float3 p)
{
    p = frac(p * 0.1031);
    p += dot(p, p.yzx + 33.33);
    return frac((p.x + p.y) * p.z);
}

float TimCutoutValueNoise3D(float3 x)
{
    float3 i = floor(x);
    float3 f = frac(x);
    f = f * f * (3.0 - 2.0 * f);

    float n000 = TimCutoutHash31(i + float3(0.0, 0.0, 0.0));
    float n100 = TimCutoutHash31(i + float3(1.0, 0.0, 0.0));
    float n010 = TimCutoutHash31(i + float3(0.0, 1.0, 0.0));
    float n110 = TimCutoutHash31(i + float3(1.0, 1.0, 0.0));
    float n001 = TimCutoutHash31(i + float3(0.0, 0.0, 1.0));
    float n101 = TimCutoutHash31(i + float3(1.0, 0.0, 1.0));
    float n011 = TimCutoutHash31(i + float3(0.0, 1.0, 1.0));
    float n111 = TimCutoutHash31(i + float3(1.0, 1.0, 1.0));

    float nx00 = lerp(n000, n100, f.x);
    float nx10 = lerp(n010, n110, f.x);
    float nx01 = lerp(n001, n101, f.x);
    float nx11 = lerp(n011, n111, f.x);

    float nxy0 = lerp(nx00, nx10, f.y);
    float nxy1 = lerp(nx01, nx11, f.y);

    return lerp(nxy0, nxy1, f.z) * 2.0 - 1.0;
}

float TimCutoutOrganicNoise(float3 positionWS, float scale)
{
    float3 p = positionWS * scale;
    float n1 = TimCutoutValueNoise3D(p);
    float n2 = TimCutoutValueNoise3D(p * 2.13 + float3(17.1, 31.7, 11.3));
    return n1 * 0.65 + n2 * 0.35;
}

float TimCutoutInterleavedGradientNoise(float2 pixelCoord)
{
    return frac(52.9829189 * frac(dot(pixelCoord, float2(0.06711056, 0.00583715))));
}

void EvaluateTimCutout(float3 positionWS, float2 pixelCoord, out float cutProgress, out float edgeGlow)
{
    cutProgress = 0.0;
    edgeGlow = 0.0;

    if (_TimCutoutEnabled < 0.5)
        return;

    float3 nearCenter = _TimCutoutNearCenter.xyz;
    float3 farCenter = _TimCutoutFarCenter.xyz;
    float3 axisForward = _TimCutoutAxisForward.xyz;
    float3 axisRight = _TimCutoutAxisRight.xyz;

    float spanLength = max(dot(farCenter - nearCenter, axisForward), 0.001);
    float depthAlong = dot(positionWS - nearCenter, axisForward);
    float softness = max(_TimCutoutVisualParams.x, 0.02);

    // Never cut anything behind the far plane (behind Tim) or behind the near plane.
    if (depthAlong > spanLength || depthAlong < -softness)
        return;

    float u = saturate(depthAlong / spanLength);
    float3 sliceCenter = lerp(nearCenter, farCenter, u);
    float halfW = lerp(_TimCutoutShapeParams.x, _TimCutoutShapeParams.z, u);
    float halfH = lerp(_TimCutoutShapeParams.y, _TimCutoutShapeParams.w, u);

    float dx = abs(dot(positionWS - sliceCenter, axisRight));
    float dy = abs(positionWS.y - sliceCenter.y);

    // Rounded-rectangle cross-section matching the occlusion volume bounds.
    float cornerRadius = min(halfW, halfH) * 0.45;
    float2 q = float2(dx, dy) - (float2(halfW, halfH) - cornerRadius);
    float sdCross = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - cornerRadius;

    float noise = TimCutoutOrganicNoise(positionWS, _TimCutoutVisualParams.y) * _TimCutoutVisualParams.z;

    // Fade noise near the far plane so nothing behind Tim is ever pushed into the cutout.
    float farCapFade = saturate((spanLength - depthAlong) / max(softness * 0.5, 0.05));
    float crossCut = 1.0 - smoothstep(-softness * 0.2, softness * 0.8, sdCross + noise * farCapFade);
    float nearMask = smoothstep(-softness * 0.5, 0.0, depthAlong);
    float farMask = 1.0 - smoothstep(spanLength - 0.06, spanLength, depthAlong);
    float rawCut = crossCut * nearMask * farMask;

    // Protect geometry inside or below the Tim feet-safe box (stairs, floors, low rocks).
    float feetProtect = 0.0;
    if (_TimCutoutFeetBoxCenter.w > 0.5)
    {
        float3 feetDelta = abs(positionWS - _TimCutoutFeetBoxCenter.xyz) - _TimCutoutFeetBoxHalfExtents.xyz;
        float sdFeetBox = length(max(feetDelta, 0.0)) + min(max(feetDelta.x, max(feetDelta.y, feetDelta.z)), 0.0);
        float feetFeather = max(_TimCutoutFeetBoxHalfExtents.w, 0.01);
        feetProtect = 1.0 - smoothstep(-feetFeather, 0.0, sdFeetBox);
    }

    cutProgress = saturate(rawCut * (1.0 - feetProtect));
    if (cutProgress <= 0.001)
        return;

    float dither = TimCutoutInterleavedGradientNoise(pixelCoord);
    clip((1.0 - cutProgress) - dither - 0.0001);

    float glowWidth = max(_TimCutoutVisualParams.w, 0.01);
    float rampIn = smoothstep(0.0, glowWidth, cutProgress);
    float peak = smoothstep(0.0, 0.85, cutProgress);
    edgeGlow = saturate(rampIn * (0.45 + 0.55 * peak));
}

void ApplyTimCutout(float3 positionWS, float2 pixelCoord, inout half3 color)
{
    float cutProgress;
    float edgeGlow;
    EvaluateTimCutout(positionWS, pixelCoord, cutProgress, edgeGlow);

    if (edgeGlow > 0.001)
    {
        half3 rimTint = _TimCutoutEdgeColor.rgb * _TimCutoutEdgeColor.a;
        color = lerp(color, color * 0.45 + rimTint, saturate(edgeGlow * 0.85));
    }
}

void ApplyTimCutoutClipOnly(float3 positionWS, float2 pixelCoord)
{
    float cutProgress;
    float edgeGlow;
    EvaluateTimCutout(positionWS, pixelCoord, cutProgress, edgeGlow);
}

#endif
