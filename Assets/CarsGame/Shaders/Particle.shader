// Unlit alpha-blended vertex-colour particles (dust). Fogged, no lighting, no textures.
Shader "CarsGame/Particle"
{
    Properties { _Tint ("Tint", Color) = (1, 1, 1, 1) }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            Name "Particle"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ FOG_LINEAR
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : TEXCOORD0; float2 uv : TEXCOORD1; float fogFactor : TEXCOORD2; };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.color = v.color * _Tint;
                o.uv = v.uv;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                // soft round puff from the billboard uv
                float2 d = i.uv - 0.5;
                half mask = saturate(1.0 - dot(d, d) * 4.0);
                mask *= mask;
                half3 col = MixFog(i.color.rgb, i.fogFactor);
                return half4(col, i.color.a * mask);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
