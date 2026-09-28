#ifndef LAVA_MAGMA_INCLUDED
#define LAVA_MAGMA_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

CBUFFER_START(UnityPerMaterial)
    float _Tiling;
    float _ScrollSpeed;
    float _AnimSpeed;
    float _Brightness;
    float _BumpStrength;
    float _EmissionStrength;
    float _EmissionThreshold;
CBUFFER_END

struct Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
    float3 normalWS : TEXCOORD1;
    UNITY_VERTEX_OUTPUT_STEREO
};

// random2 by Patricio Gonzalez Vivo
float2 MagmaRandom2(float2 p)
{
    return frac(sin(float2(
        dot(p, float2(127.1, 311.7)),
        dot(p, float2(269.5, 183.3)))) * 43758.5453);
}

// Gradient noise. Adapted from Inigo Quilez, iq/2013
// https://www.shadertoy.com/view/lsf3WH
float MagmaNoise(float2 st)
{
    float2 i = floor(st);
    float2 f = frac(st);
    float2 u = f * f * (3.0 - 2.0 * f);

    return lerp(
        lerp(dot(MagmaRandom2(i + float2(0.0, 0.0)), f - float2(0.0, 0.0)),
             dot(MagmaRandom2(i + float2(1.0, 0.0)), f - float2(1.0, 0.0)), u.x),
        lerp(dot(MagmaRandom2(i + float2(0.0, 1.0)), f - float2(0.0, 1.0)),
             dot(MagmaRandom2(i + float2(1.0, 1.0)), f - float2(1.0, 1.0)), u.x),
        u.y);
}

float3 MagmaLayer(float3 color, float2 uv, float detail, float power,
                  float colorMul, float glowRate, bool animate, float noiseAmount, float time)
{
    float3 rockColor = float3(0.09 + abs(sin(time * 0.75)) * 0.03, 0.02, 0.02);
    float minDistance = 1.0;
    uv *= detail;

    float2 cell = floor(uv);
    float2 fracUv = frac(uv);

    [unroll]
    for (int i = -1; i <= 1; i++)
    {
        [unroll]
        for (int j = -1; j <= 1; j++)
        {
            float2 cellDir = float2((float)i, (float)j);
            float2 randPoint = MagmaRandom2(cell + cellDir);
            randPoint += MagmaNoise(uv) * noiseAmount;
            randPoint = animate ? 0.5 + 0.5 * sin(time * 0.35 + 6.2831 * randPoint) : randPoint;
            minDistance = min(minDistance, length(cellDir + randPoint - fracUv));
        }
    }

    float powAdd = sin(uv.x * 2.0 + time * glowRate) + sin(uv.y * 2.0 + time * glowRate);
    float3 outColor = color * pow(minDistance, power + powAdd * 0.95) * colorMul;
    outColor = lerp(rockColor, outColor, minDistance);
    return outColor;
}

Varyings vert(Attributes input)
{
    Varyings output = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
    output.positionCS = positionInputs.positionCS;
    output.positionWS = positionInputs.positionWS;
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
    return output;
}

// Height-to-normal from screen derivatives, so the flat cube can show relief
// without a denser mesh. Based on Mikkelsen's bump mapping.
float3 PerturbNormal(float3 positionWS, float3 normalWS, float height, float strength)
{
    float3 dpdx = ddx(positionWS);
    float3 dpdy = ddy(positionWS);
    float dhdx = ddx(height);
    float dhdy = ddy(height);

    float3 r1 = cross(dpdy, normalWS);
    float3 r2 = cross(normalWS, dpdx);
    float det = dot(dpdx, r1);
    float3 grad = sign(det) * (dhdx * r1 + dhdy * r2);
    return normalize(abs(det) * normalWS - grad * strength);
}

float4 frag(Varyings input) : SV_Target
{
    float time = _Time.y * _AnimSpeed;

    // Project onto the face so the top of the lava volume and the thin sides
    // both get an even world-space pattern.
    float3 geomNormal = normalize(input.normalWS);
    float3 n = abs(geomNormal);
    float2 uv = input.positionWS.xz;
    if (n.x > n.y && n.x > n.z)
        uv = input.positionWS.zy;
    else if (n.z > n.y && n.z > n.x)
        uv = input.positionWS.xy;

    uv *= _Tiling;
    uv.x += _Time.y * _ScrollSpeed;

    float3 color = 0;
    color += MagmaLayer(float3(1.5, 0.45, 0.0), uv, 3.0, 2.5, 1.15, 1.5, false, 1.5, time);
    color += MagmaLayer(float3(1.5, 0.0, 0.0), uv, 6.0, 3.0, 0.4, 1.0, false, 0.0, time);
    color += MagmaLayer(float3(1.2, 0.4, 0.0), uv, 8.0, 4.0, 0.2, 1.9, true, 0.5, time);

    float luma = dot(max(color, 0), float3(0.2126, 0.7152, 0.0722));
    // Bright cracks sit lower than the dark crust, matching the magma pattern.
    float height = saturate(1.0 - luma);
    float3 bumpedNormal = PerturbNormal(input.positionWS, geomNormal, height, _BumpStrength);

    Light mainLight = GetMainLight();
    float3 lightDir = mainLight.direction;
    float geoTerm = dot(geomNormal, lightDir) * 0.5 + 0.5;
    float bumpTerm = dot(bumpedNormal, lightDir) * 0.5 + 0.5;
    float shade = clamp(bumpTerm / max(geoTerm, 0.15), 0.45, 1.55);
    // Recessed cracks get a little extra darkening. Emission is added after this.
    float cavity = lerp(0.72, 1.0, height);

    float hot = smoothstep(_EmissionThreshold, _EmissionThreshold + 0.25, luma);
    float3 emission = color * hot * _EmissionStrength;

    float3 viewDir = GetWorldSpaceNormalizeViewDir(input.positionWS);
    float3 halfDir = SafeNormalize(lightDir + viewDir);
    float spec = pow(saturate(dot(bumpedNormal, halfDir)), 32.0);
    float3 specColor = spec * mainLight.color * saturate(1.0 - hot) * 0.4 * saturate(_BumpStrength);

    float3 surface = color * _Brightness * shade * cavity;
    return float4(surface + emission + specColor, 1.0);
}

#endif
