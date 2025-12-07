Shader "Custom/LKG_FBM2D_Smoke_Opaque"
{
    Properties
    {
        _Scale       ("UV Scale",          Float) = 1.0
        _SlowSpeed   ("Slow Scroll Speed", Float) = 0.4
        _FastSpeed   ("Fast Mod Speed",    Float) = 2.0

        _Octaves     ("Octaves (1-8)",     Float) = 8.0
        _Persistence ("Persistence",       Float) = 2.0

        _SmokeGain   ("Smoke Gain",        Float) = 1.0

        _Sharpness   ("Sharpness (pow)",   Float) = 1.3
        _Contrast    ("Contrast",          Float) = 1.4
        _Brightness  ("Brightness",        Float) = 0.0

        // Noise colors
        _ColorA      ("Noise Color A",     Color) = (0.5098, 0.2039, 0.0157, 1)
        _ColorB      ("Noise Color B",     Color) = (0.5294, 0.8078, 0.9804, 1)
        _ColorC      ("Noise Color C",     Color) = (1.0,   0.95,   0.7,    1)

        // Background vertical gradient
        _BgColorA    ("Background Bottom", Color) = (0.02, 0.02, 0.04, 1)
        _BgColorB    ("Background Top",    Color) = (0.10, 0.12, 0.18, 1)

        // Alpha range
        _NoiseAStart ("Noise A Start (alpha 0)", Float) = 0.25
        _NoiseAEnd   ("Noise A End (alpha 1)",   Float) = 0.8

        // Color band starts
        _ColorAStart ("Color A Start", Float) = 0.25
        _ColorBStart ("Color B Start", Float) = 0.5
        _ColorCStart ("Color C Start", Float) = 0.8

        // Vignette
        _VignetteXSize ("Vignette X Size", Range(0.0, 1.0)) = 1.0
        _VignetteYSize ("Vignette Y Size", Range(0.0, 1.0)) = 1.0
        _VignetteEdgeSoften ("Vignette Edge Soften", Range(0.0, 1.0)) = 0.2
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+0" "IgnoreProjector"="True" }

        Pass
        {
            Name "LKG_FBM2D_Smoke_Opaque"
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

            float4 _BgColorA;
            float4 _BgColorB;

            float  _NoiseAStart;
            float  _NoiseAEnd;

            float  _ColorAStart;
            float  _ColorBStart;
            float  _ColorCStart;

            float  _VignetteXSize;
            float  _VignetteYSize;
            float  _VignetteEdgeSoften;

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
                float minStep = 1.0;

                float2 grid     = floor(vl);
                float2 gridPnt1 = grid;
                float2 gridPnt2 = float2(grid.x, grid.y + minStep);
                float2 gridPnt3 = float2(grid.x + minStep, grid.y);
                float2 gridPnt4 = float2(gridPnt3.x, gridPnt2.y);

                float s = rand2(grid);
                float t = rand2(gridPnt3);
                float u = rand2(gridPnt2);
                float v = rand2(gridPnt4);

                float x1       = smoothstep(0.0, 1.0, frac(vl.x));
                float interpX1 = lerp(s, t, x1);
                float interpX2 = lerp(u, v, x1);

                float y      = smoothstep(0.0, 1.0, frac(vl.y));
                float interpY = lerp(interpX1, interpX2, y);

                return interpY;
            }

            float fractalNoise(float2 vl)
            {
                float persistance = _Persistence;
                float amplitude   = 0.5;
                float rez         = 0.0;
                float2 p          = vl;

                int oct = (int)clamp(round(_Octaves), 1.0, 16.0);

                [unroll]
                for (int i = 0; i < 16; i++)
                {
                    if (i >= oct) break;

                    rez += amplitude * valueNoiseSimple(p);
                    amplitude /= persistance;
                    p *= persistance;
                }
                return rez;
            }

            float complexFBM(float2 p)
            {
                float sound = _SmokeGain;

                float slow = _Time.y / 2.5 * _SlowSpeed;
                float fast = _Time.y / 0.5 * _FastSpeed;

                float slowMult = 2.;
                float fastMult = 1.;
                float2 offset1 = float2(cos(slow) * slowMult, sin(slow) * slowMult);
                float2 offset2 = float2(-cos(fast) * fastMult, sin(fast) * fastMult);

                float2 p0 = p;
                float2 p1 = p0 + 2.0 * fractalNoise(p0 - offset2);
                float2 p2 = p0 + fractalNoise(p1);
                float2 p3 = p0 + offset1 + fractalNoise(p2);

                float base = fractalNoise(p3);

                return (1.0 + sound) * base;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);
                
                float2 uv = IN.uv;

                // Background vertical gradient
                float bgT    = saturate(uv.y);
                float3 bgCol = lerp(_BgColorA.rgb, _BgColorB.rgb, bgT);

                // FBM value in [0,1] after shaping
                float2 p = uv * _Scale;
                float v = complexFBM(p);
                v = saturate(v);

                if (_Sharpness > 0.0)
                    v = pow(v, _Sharpness);

                v = (v - 0.5) * _Contrast + 0.5 + _Brightness;
                v = saturate(v);
                
                // 3-band color gradient
                float3 noiseCol;

                noiseCol = lerp(_ColorA.rgb, _ColorB.rgb, smoothstep(_ColorAStart, _ColorBStart, v));
                noiseCol = lerp(noiseCol, _ColorC.rgb, smoothstep(_ColorBStart, _ColorCStart, v));
                
                float3 finalCol = lerp(bgCol, noiseCol, smoothstep(_NoiseAStart, _NoiseAEnd, v));

                // Vignette effect
                float2 centeredUV = abs(IN.uv - 0.5) * 2.0; // 0 at center, 1 at edge
                float2 vignetteDist = centeredUV / float2(_VignetteXSize, _VignetteYSize);
                float vignetteDistMax = max(vignetteDist.x, vignetteDist.y);
                
                // Apply edge softening: smoothstep from (1 - soften) to 1
                float vignetteFactor = smoothstep(1.0 - _VignetteEdgeSoften, 1.0, vignetteDistMax);
                vignetteFactor = 1.0 - vignetteFactor; // Invert so edges are darker
                
                finalCol *= vignetteFactor;

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
