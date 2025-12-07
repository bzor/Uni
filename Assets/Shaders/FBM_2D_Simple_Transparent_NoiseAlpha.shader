Shader "Custom/LKG_FBM_2D_Simple_Transparent_NoiseAlpha"
{
    Properties
    {
        _Scale ("UV Scale", Float) = 3.0
        _Tick ("Tick", Float) = 0.0
        _Octaves ("Octaves", Int) = 5
        
        _Color1 ("Color 1", Color) = (0.101961, 0.619608, 0.666667, 1)
        _Color2 ("Color 2", Color) = (0.666667, 0.666667, 0.498039, 1)
        _Color3 ("Color 3", Color) = (0, 0, 0.164706, 1)
        _Color4 ("Color 4", Color) = (0.666667, 1, 1, 1)
        
        _BGColBottom ("Background Color Bottom", Color) = (0.0, 0.0, 0.0, 1.0)
        _BGColTop ("Background Color Top", Color) = (1.0, 1.0, 1.0, 1.0)
        _BGColorAlphaStart ("Background Color Alpha Start", Range(0.0, 1.0)) = 0.0
        _BGColorAlphaEnd ("Background Color Alpha End", Range(0.0, 1.0)) = 1.0

        // Alpha thresholds based on noise value
        _AlphaMin ("Alpha Min", Range(0.0, 1.0)) = 0.0
        _AlphaMax ("Alpha Max", Range(0.0, 1.0)) = 1.0

        // Vignette
        _VignetteXSize ("Vignette X Size", Range(0.0, 1.0)) = 1.0
        _VignetteYSize ("Vignette Y Size", Range(0.0, 1.0)) = 1.0
        _VignetteEdgeSoften ("Vignette Edge Soften", Range(0.0, 1.0)) = 0.2
        
        _OpenClouds ("Open Clouds", Range(0.0, 1.0)) = 0.0
        
        // Cutout
        _CutoutInnerRadius ("Cutout Inner Radius", Range(0.0, 1.0)) = 0.3
        _CutoutOuterRadius ("Cutout Outer Radius", Range(0.0, 1.0)) = 0.5
        _CutoutCenter ("Cutout Center", Vector) = (0.5, 0.5, 0, 0)
        _CutoutSizeX ("Cutout Size X", Range(0.0, 1.0)) = 0.5
        _CutoutSizeY ("Cutout Size Y", Range(0.0, 1.0)) = 0.5
    }

    SubShader
    {
        Tags 
        { 
            "RenderPipeline"="UniversalPipeline" 
            "RenderType"="Transparent" 
            "Queue"="Transparent" 
            "IgnoreProjector"="True" 
        }

        Pass
        {
            Name "FBM_2D_Simple_Transparent_NoiseAlpha"
            Tags { "LightMode"="UniversalForwardOnly" }

            ZWrite Off
            ZTest LEqual
            Cull Back
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ UNITY_STEREO_INSTANCING_ENABLED

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float _Scale;
            float _Tick;
            int _Octaves;
            
            float4 _Color1;
            float4 _Color2;
            float4 _Color3;
            float4 _Color4;
            
            float4 _BGColBottom;
            float4 _BGColTop;
            float _BGColorAlphaStart;
            float _BGColorAlphaEnd;

            float _AlphaMin;
            float _AlphaMax;

            float  _VignetteXSize;
            float  _VignetteYSize;
            float  _VignetteEdgeSoften;
            
            float  _OpenClouds;
            
            // Cutout
            float _CutoutInnerRadius;
            float _CutoutOuterRadius;
            float2 _CutoutCenter;
            float _CutoutSizeX;
            float _CutoutSizeY;

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                
                float3 worldPos = TransformObjectToWorld(IN.positionOS);
                OUT.positionHCS = TransformWorldToHClip(worldPos);
                OUT.uv = IN.uv;
                return OUT;
            }

            // Random function based on dot product
            float random(float2 st)
            {
                return frac(sin(dot(st.xy, float2(12.9898, 78.233))) * 43758.5453123);
            }

            // Noise function with bilinear interpolation
            // Based on Morgan McGuire @morgan3d
            // https://www.shadertoy.com/view/4dS3Wd
            float noise(float2 st)
            {
                float2 i = floor(st);
                float2 f = frac(st);

                // Four corners in 2D of a tile
                float a = random(i);
                float b = random(i + float2(1.0, 0.0));
                float c = random(i + float2(0.0, 1.0));
                float d = random(i + float2(1.0, 1.0));

                // Smoothstep interpolation
                float2 u = f * f * (3.0 - 2.0 * f);

                // Bilinear interpolation
                return lerp(a, b, u.x) +
                       (c - a) * u.y * (1.0 - u.x) +
                       (d - b) * u.x * u.y;
            }

            // Fractal Brownian Motion with rotation to reduce axial bias
            // Optimize: Precompute rotation matrix constants (cos(0.5) and sin(0.5))
            static const float ROT_COS = 0.877582562; // cos(0.5)
            static const float ROT_SIN = 0.479425539; // sin(0.5)
            static const float2x2 ROT_MATRIX = float2x2(ROT_COS, ROT_SIN, -ROT_SIN, ROT_COS);
            static const float2 SHIFT = float2(100.0, 100.0);
            
            float fbm(float2 st)
            {
                float v = 0.0;
                float a = 0.5;
                
                int numOctaves = clamp(_Octaves, 1, 8);
                
                // Optimize: Use exact loop count instead of break
                [unroll]
                for (int i = 0; i < 8; i++)
                {
                    if (i >= numOctaves) break;
                    
                    v += a * noise(st);
                    st = mul(ROT_MATRIX, st) * 2.0 + SHIFT;
                    a *= 0.5;
                }
                
                return v;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);
                
                float2 st = IN.uv * _Scale;
                float time = _Tick;
                
                float3 color = float3(0.0, 0.0, 0.0);

                // First FBM pass for q
                float2 q = float2(0.0, 0.0);
                q.x = fbm(st + 0.00 * time);
                q.y = fbm(st + float2(1.0, 1.0));

                // Second FBM pass for r (using q as offset)
                float2 r = float2(0.0, 0.0);
                r.x = fbm(st + 1.0 * q + float2(1.7, 9.2) + 0.15 * time);
                r.y = fbm(st + 1.0 * q + float2(8.3, 2.8) + 0.126 * time);

                // Final FBM value
                float f = fbm(st + r);

                // Color mixing based on FBM values
                // Optimize: Precompute f squared once
                float f2 = f * f;
                
                // Mix color1 and color2 based on f
                color = lerp(_Color1.rgb, _Color2.rgb, saturate(f2 * 4.0));

                // Mix with color3 based on length of q
                color = lerp(color, _Color3.rgb, saturate(length(q)));

                // Optimize: r.x is a scalar, so length(r.x) was incorrect - use abs(r.x) instead
                // For mixing, we can use abs(r.x) directly
                color = lerp(color, _Color4.rgb, saturate(abs(r.x)));

                // Optimize: Final color shaping using Horner's method: f*(f*(f + 0.6) + 0.5)
                // This reduces from 3 multiplications + 2 additions to 2 multiplications + 2 additions
                float fShape = f * (f * (f + 0.6) + 0.5);

                //color = float3(f, f, f);
                color = fShape * color;
                
                // Blend background colors with final color based on color.r
                // Use inverse lerp (smoothstep) with color.r as the t value
                float bgBlendFactor = smoothstep(_BGColorAlphaStart, _BGColorAlphaEnd, 1.0 - color.r);
                float3 bgColor = lerp(_BGColBottom.rgb, _BGColTop.rgb, IN.uv.y);
                
                // Blend background color with final color
                color.rgb = lerp(color.rgb, bgColor, bgBlendFactor);

                // Calculate elliptical cutout mask
                // Offset to center (in UV space)
                float2 cutoutUV = IN.uv - _CutoutCenter;
                
                // Calculate elliptical distance: sqrt((x/sizeX)^2 + (y/sizeY)^2)
                // Scale by 0.5 so that 1.0 size = full screen (UV goes 0-1, so max distance from center is 0.5)
                float2 scaledUV = cutoutUV / float2(_CutoutSizeX * 0.5, _CutoutSizeY * 0.5);
                float cutoutDistFromCenter = length(scaledUV);
                
                // Create smooth mask: 0.0 inside inner radius, 1.0 outside outer radius
                // Smooth transition between inner and outer radius
                float cutoutMask = smoothstep(_CutoutInnerRadius, lerp(_CutoutOuterRadius * 0.5, _CutoutOuterRadius, _OpenClouds), cutoutDistFromCenter);
                float cutoutMult = _OpenClouds;
                
                // Apply multiplier to fade in the cutout
                cutoutMask = lerp(1.0, cutoutMask, cutoutMult);
                
                // Fade color to black based on cutout mask (instead of fading alpha)
                // When mask is 0 (inside cutout), color becomes black; when mask is 1 (outside), color is unchanged
                color.rgb = lerp(float3(0.0, 0.0, 0.0), color.rgb, cutoutMask);

                // Edge vignette effect (elliptical)
                float2 centeredUV = (IN.uv - 0.5) * 2.0; // -1 to 1 from center
                float2 vignetteDist = centeredUV / float2(_VignetteXSize, _VignetteYSize);
                float vignetteDistLength = length(vignetteDist); // Elliptical distance
                
                // Apply edge softening: smoothstep from (1 - soften) to 1
                float vignetteFactor = smoothstep(1.0 - _VignetteEdgeSoften, 1.0, vignetteDistLength);
                vignetteFactor = 1.0 - vignetteFactor; // Invert so edges are darker
                
                color *= vignetteFactor;

                // Calculate alpha based on noise value (f) using min/max thresholds
                float alpha = smoothstep(_AlphaMin, _AlphaMax, f);

                return half4(color, alpha);
            }

            ENDHLSL
        }
    }
}

