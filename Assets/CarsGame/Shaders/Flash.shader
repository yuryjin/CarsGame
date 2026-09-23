// Unlit alpha-blended colour for effect spheres (wreck flashes, impacts). Colour and alpha come from
// _BaseColor, set per renderer through a MaterialPropertyBlock. Fogged, no lighting, no textures.
Shader "CarsGame/Flash"
{
    Properties { _BaseColor ("Color", Color) = (1, 1, 1, 1) }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Pass
        {
            Name "Flash"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ FOG_LINEAR
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float fogFactor : TEXCOORD0; };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                return half4(MixFog(_BaseColor.rgb, i.fogFactor), _BaseColor.a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
