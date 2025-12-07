Shader "Custom/LKG_FBM2D_Smoke_Layers"
{
    Properties
    {
        _Scale       ("UV Scale",          Float) = 1.0
        _SlowSpeed   ("Slow Scroll Speed", Float) = 0.4
        _FastSpeed   ("Fast Mod Speed",    Float) = 2.0

        _Octaves     ("Octaves (1-16)",     Float) = 8.0
        _Persistence ("Persistence",       Float) = 2.0

        _SmokeGain   ("Smoke Gain",        Float) = 1.0

        _Sharpness   ("Sharpness (pow)",   Float) = 1.3
        _Contrast    ("Contrast",          Float) = 1.4
        _Brightness  ("Brightness",        Float) = 0.0

        // Noise colors
        _ColorA      ("Noise Color A",     Color) = (0.5098, 0.2039, 0.0157, 1)
        _ColorB      ("Noise Color B",     Color) = (0.5294, 0.8078, 0.9804, 1)
        _ColorC      ("Noise Color C",     Color) = (1.0,   0.95,   0.7,    1)
        _ColorD      ("Noise Color D",     Color) = (0.8,   0.6,    0.4,    1)
        _ColorE      ("Noise Color E",     Color) = (0.3,   0.2,    0.1,    1)

        // Background vertical gradient
        _BgColorA    ("Background Bottom", Color) = (0.02, 0.02, 0.04, 1)
        _BgColorB    ("Background Top",    Color) = (0.10, 0.12, 0.18, 1)

        // Alpha range
        _NoiseAStart ("Noise A Start (alpha 0)", Float) = 0.25
        _NoiseAEnd   ("Noise A End (alpha 1)",   Float) = 0.8

        // Color band stops
        _ColorAStop ("Color A Stop", Float) = 0.25
        _ColorBStop ("Color B Stop", Float) = 0.5
        _ColorCStop ("Color C Stop", Float) = 0.75
        _ColorDStop ("Color D Stop", Float) = 0.9
        _ColorEStop ("Color E Stop", Float) = 1.0
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+0" "IgnoreProjector"="True" }

        Pass
        {
            Name "LKG_FBM2D_Smoke_Layers"
            Tags { "LightMode"="UniversalForwardOnly" }

            ZWrite On
            ZTest LEqual
            Cull Back
            Blend Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ UNITY_STEREO_INSTANCING_ENABLED

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float  _Scale;
            float  _SlowSpeed;
            float  _FastSpeed;

            float  _Octaves;
            float  _Persistence;

            float  _SmokeGain;

            float  _Sharpness;
            float  _Contrast;
            float  _Brightness;

            float4 _ColorA;
            float4 _ColorB;
            float4 _ColorC;
            float4 _ColorD;
            float4 _ColorE;

            float4 _BgColorA;
            float4 _BgColorB;

            float  _NoiseAStart;
            float  _NoiseAEnd;

            float  _ColorAStop;
            float  _ColorBStop;
            float  _ColorCStop;
            float  _ColorDStop;
            float  _ColorEStop;

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert (Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                
                float3 worldPos = TransformObjectToWorld(IN.positionOS);
                OUT.positionHCS = TransformWorldToHClip(worldPos);
                OUT.uv          = IN.uv;
                return OUT;
            }

            // --- Shadertoy FBM core ---

            float rand2(float2 co)
            {
                return frac(cos(dot(co, float2(12.9898, 78.233))) * 43758.5453);
            }

            float valueNoiseSimple(float2 vl)
            {
                float2 grid = floor(vl);
                float2 f = frac(vl);
                
                // Optimize: calculate grid points more efficiently
                float2 gridPnt3 = grid + float2(1.0, 0.0);
                float2 gridPnt2 = grid + float2(0.0, 1.0);
                float2 gridPnt4 = grid + float2(1.0, 1.0);

                // Sample corners
                float s = rand2(grid);
                float t = rand2(gridPnt3);
                float u = rand2(gridPnt2);
                float v = rand2(gridPnt4);

                // Optimize: use smoothstep directly on frac values
                float2 smoothF = f * f * (3.0 - 2.0 * f); // smoothstep(0,1,f) = f*f*(3-2*f)
                
                // Bilinear interpolation
                float interpX1 = lerp(s, t, smoothF.x);
                float interpX2 = lerp(u, v, smoothF.x);
                return lerp(interpX1, interpX2, smoothF.y);
            }

            float fractalNoise(float2 vl)
            {
                float persistance = _Persistence;
                float amplitude   = 0.5;
                float rez         = 0.0;
                float2 p          = vl;

                // Optimize: calculate octave count once and use proper loop bounds
                int oct = (int)clamp(round(_Octaves), 1.0, 16.0);
                float invPersistence = 1.0 / persistance; // Precompute division

                // Optimize: use exact loop count instead of break
                [unroll]
                for (int i = 0; i < 16; i++)
                {
                    if (i >= oct) break;

                    rez += amplitude * valueNoiseSimple(p);
                    amplitude *= invPersistence; // Multiply instead of divide
                    p *= persistance;
                }
                return rez;
            }

            float complexFBM(float2 p)
            {
                // Optimize: precompute time-based values once
                float slow = _Time.y * (1.0 / 2.5) * _SlowSpeed; // Multiply by inverse instead of divide
                float fast = _Time.y * 2.0 * _FastSpeed; // 1.0/0.5 = 2.0

                // Optimize: use sincos for combined sin/cos calculation
                float slowCos, slowSin;
                sincos(slow, slowSin, slowCos);
                float fastCos, fastSin;
                sincos(fast, fastSin, fastCos);

                float2 offset1 = float2(slowCos * 2.0, slowSin * 2.0);
                float2 offset2 = float2(-fastCos, fastSin);

                float2 p0 = p;
                float2 p1 = p0 + 2.0 * fractalNoise(p0 - offset2);
                float2 p2 = p0 + fractalNoise(p1);
                float2 p3 = p0 + offset1 + fractalNoise(p2);

                float base = fractalNoise(p3);

                return (1.0 + _SmokeGain) * base;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);
                
                float2 uv = IN.uv;

                // Background vertical gradient
                float bgT = saturate(uv.y);
                float3 bgCol = lerp(_BgColorA.rgb, _BgColorB.rgb, bgT);

                // FBM value in [0,1] after shaping
                float2 p = uv * _Scale;
                float v = complexFBM(p);
                v = saturate(v);

                // Optimize: only use pow if Sharpness != 1.0
                if (abs(_Sharpness - 1.0) > 0.001)
                    v = pow(v, _Sharpness);

                // Optimize: combine contrast and brightness in one operation
                v = saturate((v - 0.5) * _Contrast + 0.5 + _Brightness);
                
                // Optimize: precompute smoothstep values for color bands
                float colorAB = smoothstep(_ColorAStop, _ColorBStop, v);
                float colorBC = smoothstep(_ColorBStop, _ColorCStop, v);
                float colorCD = smoothstep(_ColorCStop, _ColorDStop, v);
                float colorDE = smoothstep(_ColorDStop, _ColorEStop, v);
                
                // 5-band color gradient (optimized)
                float3 noiseCol = lerp(_ColorA.rgb, _ColorB.rgb, colorAB);
                noiseCol = lerp(noiseCol, _ColorC.rgb, colorBC);
                noiseCol = lerp(noiseCol, _ColorD.rgb, colorCD);
                noiseCol = lerp(noiseCol, _ColorE.rgb, colorDE);
                
                // Optimize: compute alpha blend factor once
                float alphaBlend = smoothstep(_NoiseAStart, _NoiseAEnd, v);
                float3 finalCol = lerp(bgCol, noiseCol, alphaBlend);

                return half4(finalCol, 1.0);
            }

            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }

            ZWrite On
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragDepth
            #pragma multi_compile_instancing
            #pragma multi_compile _ UNITY_STEREO_INSTANCING_ENABLED

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                
                float3 worldPos = TransformObjectToWorld(IN.positionOS);
                OUT.positionHCS = TransformWorldToHClip(worldPos);
                return OUT;
            }

            half4 FragDepth(Varyings IN) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);
                return 0;
            }

            ENDHLSL
        }
    }
}
