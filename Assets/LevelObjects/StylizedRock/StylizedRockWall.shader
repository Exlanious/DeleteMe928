// Adapted from the user-provided recursive triangle subdivision shader.
// Uses world coordinates, scene lighting and the existing lava height fog.
Shader "LevelObjects/Stylized Rock Wall"
{
    Properties
    {
        _BaseColor ("Rock Color", Color) = (0.38, 0.39, 0.40, 1)
        _RockScale ("Pattern Size (World Units)", Range(2, 40)) = 16
        _Iterations ("Triangle Detail", Range(4, 14)) = 12
        _Regularity ("Triangle Regularity", Range(1, 5)) = 2.2
        _BumpStrength ("Facet Strength", Range(0, 2)) = 0.55
        _PaletteStrength ("Reference Palette Blend", Range(0, 1)) = 0.3
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _RockScale;
                float _Iterations;
                float _Regularity;
                float _BumpStrength;
                float _PaletteStrength;
            CBUFFER_END
            #include "LavaHeightFog.hlsl"
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; half3 normalWS : TEXCOORD1; half fog : TEXCOORD2; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = p.positionCS;
                output.positionWS = p.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.fog = ComputeFogFactor(p.positionCS.z);
                return output;
            }
            float Random1(float2 p)
            {
                return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
            }
            float Side(float2 p, float2 a, float2 b)
            {
                return (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
            }
            bool Inside(float2 p, float2 a, float2 b, float2 c)
            {
                bool s1 = Side(p,a,b) < 0, s2 = Side(p,b,c) < 0, s3 = Side(p,c,a) < 0;
                return s1 == s2 && s2 == s3;
            }
            float EdgeDistance(float2 p, float2 a, float2 b)
            {
                float2 edge = b - a;
                return length(p - a - edge * saturate(dot(p-a,edge) / max(dot(edge,edge), 1e-10)));
            }
            half3 Palette(int i)
            {
                i = i % 6;
                if (i == 0) return half3(0.21,0.16,0.47);
                if (i == 1) return half3(0.44,0.64,0.70);
                if (i == 2) return half3(0.35,0.55,0.26);
                if (i == 3) return half3(0.26,0.22,0);
                if (i == 4) return half3(0.72,0.78,0.44);
                return half3(0.44,0.31,0.15);
            }
            half3 TriangleSurface(float2 uv, float footprint, out float3 normal)
            {
                float2 tile = floor(uv);
                float3 a = float3(tile + float2(0,1),0);
                float3 b = float3(tile + float2(1,1),0);
                float3 c = float3(tile,0);
                if (!Inside(uv,a.xy,b.xy,c.xy))
                {
                    a = float3(tile,0);
                    b = float3(tile + float2(1,1),0);
                    c = float3(tile + float2(1,0),0);
                }
                a.z = Random1(a.xy) * 0.05;
                b.z = Random1(b.xy) * 0.05;
                c.z = Random1(c.xy) * 0.05;
                float heightAffect = 0.05, colorMix = 1;
                half3 tint = half3(0.5,0.5,0.5);
                [loop] for (int i = 0; i < 14; i++)
                {
                    if (i >= (int)_Iterations) break;
                    float ab = distance(a.xy,b.xy), bc = distance(b.xy,c.xy), ca = distance(c.xy,a.xy);
                    // Stop subdividing once facets become smaller than a pixel.
                    if (max(ab,max(bc,ca)) < footprint * 2.5) break;
                    float3 temp = c;
                    if (ca > ab && ca > bc) { c = b; b = a; a = temp; }
                    else if (bc > ab) { c = a; a = b; b = temp; }
                    float pos = Random1(a.xy + b.xy) - 0.5;
                    pos = pow(abs(pos), max(_Regularity,1.0)) * 2 * sign(pos);
                    pos = clamp((pos + 1) * 0.5, 0.04, 0.96);
                    float3 d = lerp(a,b,pos);
                    d.z += (Random1(a.xy+b.xy+pos) * 2 - 1) * heightAffect;
                    if (Inside(uv,a.xy,d.xy,c.xy)) b = d;
                    else a = d;
                    float edge = min(EdgeDistance(uv,a.xy,b.xy), min(EdgeDistance(uv,b.xy,c.xy), EdgeDistance(uv,c.xy,a.xy)));
                    float distCenter = distance(uv,(a.xy+b.xy+c.xy)/3);
                    float amount = 1 - pow(saturate(edge / max(distCenter + edge, 1e-6)),1.2);
                    tint = lerp(tint,Palette(i),amount * colorMix);
                    heightAffect *= 0.7;
                    colorMix *= 0.75;
                }
                float3 facet = cross(b-a,c-a);
                if (facet.z < 0) facet = -facet;
                facet.xy *= _BumpStrength;
                normal = normalize(facet + float3(0,0,1e-9));
                return lerp(_BaseColor.rgb, tint, _PaletteStrength);
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float3 wallNormal = normalize(input.normalWS);
                float3 tangent = abs(wallNormal.y) < 0.7
                    ? normalize(cross(float3(0,1,0),wallNormal)) : float3(1,0,0);
                float3 up = normalize(cross(wallNormal,tangent));
                float2 uv = float2(dot(input.positionWS,tangent),dot(input.positionWS,up)) / max(_RockScale,0.1);
                float footprint = max(length(ddx(uv)),length(ddy(uv)));
                float3 facet;
                half3 albedo = TriangleSurface(uv,footprint,facet);
                float3 normal = normalize(tangent*facet.x + up*facet.y + wallNormal*facet.z);
                Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half3 illumination = SampleSH(normal) + light.color * saturate(dot(normal,light.direction))
                    * light.shadowAttenuation * light.distanceAttenuation;
                half3 color = albedo * illumination;
                return half4(ApplyLavaHeightFog(MixFog(color,input.fog),input.positionWS),1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
    FallBack "Universal Render Pipeline/Lit"
}
