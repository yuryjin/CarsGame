// Vertex-colour car body shader for generated car art (Tools/art, Docs/AI/ArtPipelineSample.md).
// Same lighting as CarsGame/StylizedLit, but vertex alpha is a paint mask instead of an emission mask:
//   alpha 1   -> paint: albedo = vertex rgb * _BaseColor (CarPaint sets _BaseColor per car via a property block)
//   alpha 0   -> fixed colour (glass, trim, turret): _BaseColor is ignored, so the paint never tints it
//   alpha 0.5 -> fixed colour + emission (_EmissionBoost; follows the lamps-on global if _EmissionByLamps)
// One material and one draw call for the whole painted body including its trim.
Shader "CarsGame/StylizedCarPaint"
{
    Properties
    {
        _BaseColor ("Paint colour (masked by vertex alpha)", Color) = (1, 1, 1, 1)
        _Wrap ("Half-Lambert wrap", Range(0, 1)) = 0.35
        _ShadowFloor ("Shadow floor", Range(0, 1)) = 0.35
        _EmissionBoost ("Emission for alpha=0.5 faces", Float) = 1.5
        [Toggle] _EmissionByLamps ("Emission follows lamps-on global", Float) = 0
        [HideInInspector] _FogOverride ("Unused (kept for the shared CBUFFER layout)", Float) = 0
        [Toggle] _Flat ("Derivative flat shading", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half _Wrap;
            half _ShadowFloor;
            half _EmissionBoost;
            half _EmissionByLamps;
            half _FogOverride;
            half _Flat;
        CBUFFER_END
        float4 _CarsHorizonColor;
        float4 _CarsMountainColor;
        float _CarsLampsOn;
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ FOG_LINEAR
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half4 color : TEXCOORD2;
                float fogFactor : TEXCOORD3;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.color = v.color;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                half a = i.color.a;
                half paint = step(0.75h, a);
                half glow = step(0.25h, a) * (1.0h - paint);
                half3 baseRgb = SRGBToLinear(i.color.rgb);
                half3 albedo = baseRgb * lerp(half3(1, 1, 1), _BaseColor.rgb, paint);
                half3 n = normalize(i.normalWS);
                if (_Flat > 0.5)
                {
                    float3 fn = normalize(cross(ddy(i.positionWS), ddx(i.positionWS)));
                    n = dot(fn, n) < 0.0 ? -fn : fn;
                }

                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light mainLight = GetMainLight(shadowCoord, i.positionWS, half4(1, 1, 1, 1));
                half ndl = dot(n, mainLight.direction);
                half diffuse = saturate(ndl * (1.0 - _Wrap) + _Wrap);
                half shadow = lerp(_ShadowFloor, 1.0, mainLight.shadowAttenuation * mainLight.distanceAttenuation);
                half3 direct = mainLight.color * diffuse * shadow;
                half3 ambient = SampleSH(n);

                #if defined(_SCREEN_SPACE_OCCLUSION)
                    AmbientOcclusionFactor ao = GetScreenSpaceAmbientOcclusion(GetNormalizedScreenSpaceUV(i.positionCS));
                    ambient *= ao.indirectAmbientOcclusion;
                    direct *= ao.directAmbientOcclusion;
                #endif

                half3 col = albedo * (direct + ambient);
                half lamps = lerp(1.0, _CarsLampsOn, _EmissionByLamps);
                col += baseRgb * (glow * _EmissionBoost * lamps);
                col = MixFog(col, i.fogFactor);
                return half4(col, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings ShadowVert(Attributes v)
            {
                Varyings o;
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                o.positionCS = positionCS;
                return o;
            }

            half4 ShadowFrag(Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings DepthVert(Attributes v) { Varyings o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz); return o; }
            half DepthFrag(Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex DepthNormalsVert
            #pragma fragment DepthNormalsFrag
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; half3 normalWS : TEXCOORD0; };
            Varyings DepthNormalsVert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }
            half4 DepthNormalsFrag(Varyings i) : SV_Target { return half4(normalize(i.normalWS), 0); }
            ENDHLSL
        }
    }
    FallBack Off
}
