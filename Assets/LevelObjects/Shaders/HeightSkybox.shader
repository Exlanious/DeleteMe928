Shader "Skybox/Height Gradient"
{
    Properties
    {
        _Height01 ("Height Blend", Range(0, 1)) = 0
        _LowHorizon ("Low Horizon", Color) = (0.95, 0.33, 0.12, 1)
        _LowZenith ("Low Zenith", Color) = (0.12, 0.055, 0.14, 1)
        _HighHorizon ("High Horizon", Color) = (0.34, 0.78, 0.92, 1)
        _HighZenith ("High Zenith", Color) = (0.075, 0.12, 0.38, 1)
        _Ground ("Below Horizon", Color) = (0.065, 0.025, 0.055, 1)
        _GradientPower ("Gradient Curve", Range(0.1, 4)) = 0.65
        _Exposure ("Exposure", Range(0, 4)) = 1
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Height01;
                half4 _LowHorizon, _LowZenith, _HighHorizon, _HighZenith, _Ground;
                float _GradientPower, _Exposure;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 direction : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.direction = input.positionOS.xyz;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float y = normalize(input.direction).y;
                float height = smoothstep(0, 1, saturate(_Height01));
                half3 horizon = lerp(_LowHorizon.rgb, _HighHorizon.rgb, height);
                half3 zenith = lerp(_LowZenith.rgb, _HighZenith.rgb, height);
                half3 color = lerp(horizon, zenith, pow(saturate(y), _GradientPower));
                color = lerp(color, _Ground.rgb, smoothstep(0, 0.45, -y));
                return half4(color * _Exposure, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
