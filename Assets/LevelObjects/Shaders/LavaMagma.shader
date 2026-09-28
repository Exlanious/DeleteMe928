Shader "LevelObjects/Lava Magma"
{
    Properties
    {
        _Tiling ("Tiling", Float) = 0.08
        _ScrollSpeed ("Scroll Speed", Float) = 0.02
        _AnimSpeed ("Animation Speed", Float) = 2
        _Brightness ("Brightness", Range(0, 4)) = 1
        _BumpStrength ("Bump Strength", Range(0, 3)) = 1
        _EmissionStrength ("Emission", Range(0, 8)) = 2
        _EmissionThreshold ("Emission Threshold", Range(0, 1.5)) = 0.3
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
            "UniversalMaterialType" = "Unlit"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "LavaMagma.hlsl"
            ENDHLSL
        }

        // Deferred renderers draw this pass. Forward renderers use UniversalForward above.
        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode" = "UniversalForwardOnly" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "LavaMagma.hlsl"
            ENDHLSL
        }
    }

    FallBack Off
}
