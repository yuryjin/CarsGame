// Stylized gradient skybox: zenith/horizon/ground gradient, sun disc + glow, moon, procedural stars.
// No textures. All maths in float (fp16 bands visibly on mobile); values arrive already in linear space
// (SkyDirector uploads them with SetVector). The same formula lives in SkyMath.cs for the reflection cubemap.
Shader "CarsGame/Sky"
{
    Properties
    {
        _ZenithColor ("Zenith", Vector) = (0.18, 0.42, 0.85, 1)
        _HorizonColor ("Horizon", Vector) = (0.75, 0.85, 0.95, 1)
        _GroundColor ("Ground", Vector) = (0.25, 0.28, 0.30, 1)
        _HorizonExp ("Horizon exponent", Float) = 0.55
        _SunDir ("Sun direction", Vector) = (0, 0.7, 0.7, 0)
        _SunColor ("Sun colour (linear, HDR)", Vector) = (2, 1.8, 1.4, 1)
        _SunSize ("Sun size (1-cos)", Float) = 0.0004
        _SunGlow ("Sun glow", Float) = 0.35
        _SunGlowPower ("Sun glow power", Float) = 24
        _MoonDir ("Moon direction", Vector) = (0, 0.6, -0.8, 0)
        _MoonColor ("Moon colour (linear)", Vector) = (0, 0, 0, 1)
        _MoonSize ("Moon size (1-cos)", Float) = 0.0006
        _StarsIntensity ("Stars", Float) = 0
        _StarDensity ("Star density", Float) = 150
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off ZTest LEqual
        Pass
        {
            Name "Sky"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ZenithColor, _HorizonColor, _GroundColor;
                float _HorizonExp;
                float4 _SunDir, _SunColor;
                float _SunSize, _SunGlow, _SunGlowPower;
                float4 _MoonDir, _MoonColor;
                float _MoonSize, _StarsIntensity, _StarDensity;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.dir = v.positionOS.xyz;           // Unity draws a unit sky mesh around the camera
                return o;
            }

            // Hoskins hash: no sin(), stable on mediump GPUs.
            float Hash12(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            // Upper hemisphere -> [0,1]^2 with little distortion (octahedral, y up).
            float2 OctEncodeUp(float3 n)
            {
                n /= (abs(n.x) + abs(n.y) + abs(n.z));
                return n.xz * 0.5 + 0.5;
            }

            float Disc(float cosAngle, float size1mCos)
            {
                // 1-cos grows ~ angle^2/2; smooth edge over the outer 30 % of the disc
                float d = 1.0 - cosAngle;
                return 1.0 - smoothstep(size1mCos * 0.7, size1mCos, d);
            }

            float4 Frag(Varyings i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float up = d.y;
                float3 col = up >= 0.0
                    ? lerp(_HorizonColor.rgb, _ZenithColor.rgb, pow(saturate(up), _HorizonExp))
                    : lerp(_HorizonColor.rgb, _GroundColor.rgb, saturate(-up * 3.0));

                // Sun: sharp disc + wide glow, both fade right below the horizon.
                float3 sd = normalize(_SunDir.xyz);
                float c = dot(d, sd);
                float sunVis = saturate((up + 0.08) * 12.0);
                float sun = Disc(c, _SunSize) + pow(saturate(c), _SunGlowPower) * _SunGlow;
                col += _SunColor.rgb * sun * sunVis;

                // Moon (only when the preset gives it a colour).
                if (_MoonColor.r + _MoonColor.g + _MoonColor.b > 0.0)
                {
                    float3 md = normalize(_MoonDir.xyz);
                    float mc = dot(d, md);
                    float moon = Disc(mc, _MoonSize) + pow(saturate(mc), 64.0) * 0.25;
                    col += _MoonColor.rgb * moon * saturate((up + 0.05) * 12.0);
                }

                // Stars: uniform branch, free in daytime presets.
                if (_StarsIntensity > 0.0 && up > 0.0)
                {
                    float2 uv = OctEncodeUp(d) * _StarDensity;
                    float2 cell = floor(uv);
                    float2 f = uv - cell;
                    float h = Hash12(cell);
                    float2 centre = float2(Hash12(cell + 7.1), Hash12(cell + 3.7)) * 0.6 + 0.2;
                    float dist = length(f - centre);
                    float size = 0.05 + 0.07 * Hash12(cell + 11.3);
                    float star = step(0.72, h) * (1.0 - smoothstep(size * 0.4, size, dist));
                    float twinkle = 0.7 + 0.3 * sin(_Time.y * (1.5 + 3.0 * h) + h * 20.0);
                    float3 tint = float3(0.8 + 0.2 * h, 0.88, 1.0);
                    col += tint * star * twinkle * _StarsIntensity * saturate(up * 3.0);
                }

                // Blue-noise-ish dither hides 8-bit banding in the smooth gradient.
                col += (InterleavedGradientNoise(i.positionCS.xy, 0) - 0.5) * (1.0 / 255.0);
                return float4(col, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
