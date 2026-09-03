// Unlit additive vertex-colour shader for night light pools on the asphalt. Fades with distance fog and
// is scaled by the SkyDirector global _CarsLampsOn (0 by day => contributes nothing).
Shader "CarsGame/Additive"
{
    Properties { _Intensity ("Intensity", Float) = 1 }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Blend One One
        ZWrite Off
        Cull Off
        Pass
        {
            Name "Additive"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ FOG_LINEAR
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half _Intensity;
            CBUFFER_END
            float _CarsLampsOn;
            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : TEXCOORD0; float fogFactor : TEXCOORD1; };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.color = v.color;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                half3 c = SRGBToLinear(i.color.rgb) * i.color.a * _Intensity * _CarsLampsOn;
                // additive: fade to nothing instead of towards the fog colour
                half fog = ComputeFogIntensity(i.fogFactor);
                return half4(c * fog, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
