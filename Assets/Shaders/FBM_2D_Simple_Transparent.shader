Shader "Custom/LKG_FBM_2D_Simple_Transparent"
{
    Properties
    {
        _Scale ("UV Scale", Float) = 3.0
        _Tick ("Tick", Float) = 0.0
        _Octaves ("Octaves", Int) = 5
        
        _Color1 ("Color 1", Color) = (0.101961, 0.619608, 0.666667, 1)
        _Color2 ("Color 2", Color) = (0.666667, 0.666667, 0.498039, 1)
        _Color3 ("Color 3", Color) = (0, 0, 0.164706, 1)
        
        _AlphaStart ("Alpha Start", Range(0.0, 1.0)) = 0.0
        _AlphaEnd ("Alpha End", Range(0.0, 1.0)) = 1.0
        
        _CutoutInnerRadius ("Cutout Inner Radius", Range(0.0, 1.0)) = 0.3
        _CutoutOuterRadius ("Cutout Outer Radius", Range(0.0, 1.0)) = 0.5
        _CutoutCenter ("Cutout Center", Vector) = (0.5, 0.5, 0, 0)
        _CutoutSizeX ("Cutout Size X", Range(0.0, 1.0)) = 0.5
        _CutoutSizeY ("Cutout Size Y", Range(0.0, 1.0)) = 0.5
        
        _SDFTex1 ("SDF Texture 1", 2D) = "white" {}
        _SDFTex2 ("SDF Texture 2", 2D) = "white" {}
        _SDFCrossfade ("SDF Crossfade", Range(0.0, 1.0)) = 0.0
        _SDFScale ("SDF Scale", Float) = 1.0
        _SDFThreshold ("SDF Threshold", Range(0.0, 0.5)) = 0.0
        _SDFMultiplier ("SDF Multiplier", Range(0.0, 1.0)) = 1.0
        _SDFDistortionScale ("SDF Distortion Scale", Range(0.0, 2.0)) = 0.1
        _SDFDistortionMinThreshold ("SDF Distortion Min Threshold", Range(0.0, 1.0)) = 0.0
        _SDFDistortionMaxThreshold ("SDF Distortion Max Threshold", Range(0.0, 2.0)) = 1.0
        [HDR]
        _SDFColor ("SDF Color", Color) = (1.0, 1.0, 1.0, 1.0)
        _ColorMultiplier ("Color Multiplier", Range(0.0, 1.0)) = 1.0
        
        _SDFPhrase ("SDF Phrase", 2D) = "white" {}
        _SDFPhraseScale ("SDF Phrase Scale", Float) = 1.0
        _SDFPhraseOffset ("SDF Phrase Offset", Vector) = (0.0, 0.0, 0, 0)
        _SDFPhraseThreshold ("SDF Phrase Threshold", Range(0.0, 0.5)) = 0.0
        _SDFPhraseMultiplier ("SDF Phrase Multiplier", Range(0.0, 1.0)) = 1.0
        _SDFPhraseDistortionScale ("SDF Phrase Distortion Scale", Range(0.0, 2.0)) = 0.1
        _SDFPhraseDistortionMinThreshold ("SDF Phrase Distortion Min Threshold", Range(0.0, 1.0)) = 0.0
        _SDFPhraseDistortionMaxThreshold ("SDF Phrase Distortion Max Threshold", Range(0.0, 2.0)) = 1.0
        [HDR]
        _SDFPhraseColor ("SDF Phrase Color", Color) = (1.0, 1.0, 1.0, 1.0)
        _SDFPhraseColorMultiplier ("SDF Phrase Color Multiplier", Range(0.0, 1.0)) = 1.0

        _SDFThresholdMax ("SDF Threshold Max", Range(0.0, 1.0)) = 0.5

        // Vignette
        _VignetteXSize ("Vignette X Size", Range(0.0, 1.0)) = 1.0
        _VignetteYSize ("Vignette Y Size", Range(0.0, 1.0)) = 1.0
        _VignetteEdgeSoften ("Vignette Edge Soften", Range(0.0, 1.0)) = 0.2
        
        _OpenClouds ("Open Clouds", Range(0.0, 1.0)) = 0.0
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
            Name "FBM_2D_Simple_Transparent"
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

            TEXTURE2D(_SDFTex1);
            SAMPLER(sampler_SDFTex1);
            float4 _SDFTex1_ST;
            
            TEXTURE2D(_SDFTex2);
            SAMPLER(sampler_SDFTex2);
            float4 _SDFTex2_ST;
            
            TEXTURE2D(_SDFPhrase);
            SAMPLER(sampler_SDFPhrase);
            float4 _SDFPhrase_ST;

            float _Scale;
            float _Tick;
            int _Octaves;
            
            float4 _Color1;
            float4 _Color2;
            float4 _Color3;
            float4 _SDFColor;
            float4 _SDFPhraseColor;
            float _SDFThresholdMax;
            
            float _AlphaStart;
            float _AlphaEnd;
            
            float _CutoutInnerRadius;
            float _CutoutOuterRadius;
            float2 _CutoutCenter;
            float _CutoutSizeX;
            float _CutoutSizeY;
            
            float _SDFScale;
            float _SDFThreshold;
            float _SDFMultiplier;
            float _SDFDistortionScale;
            float _SDFDistortionMinThreshold;
            float _SDFDistortionMaxThreshold;
            float _SDFCrossfade;
            
            float _SDFPhraseScale;
            float2 _SDFPhraseOffset;
            float _SDFPhraseThreshold;
            float _SDFPhraseMultiplier;
            float _SDFPhraseDistortionScale;
            float _SDFPhraseDistortionMinThreshold;
            float _SDFPhraseDistortionMaxThreshold;
            float _SDFPhraseColorMultiplier;
            
            float _ColorMultiplier;

            float  _VignetteXSize;
            float  _VignetteYSize;
            float  _VignetteEdgeSoften;
            
            float _OpenClouds;
            
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
                
                // Multiply f by mask: inside inner radius f becomes 0, outside outer radius f is unchanged
                // This creates a smooth elliptical cutout effect
                f *= cutoutMask;
                
                // Sample SDF texture and add to f (after cutout mask, so SDF is independent of mask)
                // SDF format: 0.5 is on the line, 0-0.5 is outside
                // Scale from center: offset to center, scale, then offset back
                float2 sdfUV = (IN.uv - 0.5) / _SDFScale + 0.5;
                
                sdfUV = saturate(sdfUV);
                // Sample first SDF texture (alpha channel contains the SDF value)
                float sdfValue1 = SAMPLE_TEXTURE2D(_SDFTex1, sampler_SDFTex1, sdfUV).r * 2.;
                float oSdfValue = sdfValue1;
                
                // Apply distortion
                sdfUV += (r - 0.5) * _SDFDistortionScale * (1.0 - smoothstep(_SDFDistortionMinThreshold, _SDFDistortionMaxThreshold, oSdfValue));
                sdfUV = saturate(sdfUV);
                
                // Sample both SDF textures with distortion applied
                float sdfValue1Distorted = SAMPLE_TEXTURE2D(_SDFTex1, sampler_SDFTex1, sdfUV).r;
                float sdfValue2Distorted = SAMPLE_TEXTURE2D(_SDFTex2, sampler_SDFTex2, sdfUV).r;
                
                // Crossfade between the two SDF textures
                float sdfValue = lerp(sdfValue1Distorted, sdfValue2Distorted, _SDFCrossfade);
                
                // Apply threshold to thin out the SDF
                // Threshold removes values below (0.5 - threshold), making the line thinner
                // Remap: values below (0.5 - threshold) become 0, values at 0.5 become 1
                float thresholdMin = _SDFThresholdMax - _SDFThreshold;
                float sdfProcessed;
                if (_SDFThreshold > 0.001)
                {
                    sdfProcessed = saturate((sdfValue - thresholdMin) / _SDFThreshold);
                }
                else
                {
                    sdfProcessed = sdfValue;
                }
                sdfValue = sdfProcessed;
                
                // Apply multiplier to control how much SDF gets added
                float sdfAdd = sdfValue * _SDFMultiplier;
                
                // Sample SDF Phrase texture (separate from crossfaded SDFs)
                float2 phraseUV = (IN.uv - 0.5) / _SDFPhraseScale + 0.5 + _SDFPhraseOffset;
                phraseUV = saturate(phraseUV);
                
                // Sample phrase SDF (alpha channel contains the SDF value)
                float phraseSdfValue = 1.0 - SAMPLE_TEXTURE2D(_SDFPhrase, sampler_SDFPhrase, phraseUV).r;
                float oPhraseSdfValue = phraseSdfValue;
                
                // Apply distortion to phrase SDF
                phraseUV += (r - 0.5) * _SDFPhraseDistortionScale * (1.0 - smoothstep(_SDFPhraseDistortionMinThreshold, _SDFPhraseDistortionMaxThreshold, oPhraseSdfValue));
                phraseUV = saturate(phraseUV);
                
                // Sample phrase SDF with distortion applied
                float phraseSdfValueDistorted = 1.0 - SAMPLE_TEXTURE2D(_SDFPhrase, sampler_SDFPhrase, phraseUV).r;
                
                // Apply threshold to thin out the phrase SDF
                float phraseThresholdMin = _SDFPhraseThreshold;
                float phraseSdfProcessed;
                if (_SDFPhraseThreshold > 0.001)
                {
                    phraseSdfProcessed = saturate((phraseSdfValueDistorted - phraseThresholdMin) / _SDFPhraseThreshold);
                }
                else
                {
                    phraseSdfProcessed = phraseSdfValueDistorted;
                }
                
                // Apply multiplier to control how much phrase SDF gets added
                float phraseSdfAdd = phraseSdfProcessed * _SDFPhraseMultiplier;
                
                // Combine both SDF adds
                float totalSdfAdd = sdfAdd + phraseSdfAdd;
                
                // Add SDF to f and clamp at 1.0 (after cutout, so SDF appears even in cutout area)
                f = saturate(f + totalSdfAdd);

                // Color mixing based on FBM values
                // Optimize: Precompute f squared once
                float f2 = f * f;
                
                color = lerp(_Color1.rgb, _Color2.rgb, saturate(f2 * 4.0));
                color = lerp(color, _Color3.rgb, saturate(abs(r.x)));


                // Optimize: Final color shaping using Horner's method: f*(f*(f + 0.6) + 0.5)
                // This reduces from 3 multiplications + 2 additions to 2 multiplications + 2 additions
                float fShape = f * (f * (f + 0.6) + 0.5);

                color = fShape * color;
                
                // Blend SDF color into smoke color based on SDF value
                // Higher SDF values (closer to 0.5/center of line) use more SDF color
                // Convert SDF value [0, 0.5] to blend factor [0, 1] where 0.5 = full SDF color
                ////float sdfBlendFactor = saturate(sdfValue * 2.0); // 0.5 -> 1.0, 0.0 -> 0.0
                color = lerp(color, _SDFColor.rgb, sdfValue * _ColorMultiplier);
                
                // Blend SDF Phrase color into smoke color based on phrase SDF value
                color = lerp(color, _SDFPhraseColor.rgb, phraseSdfProcessed * _SDFPhraseColorMultiplier);
                
                // Edge vignette effect (elliptical)
                float2 centeredUV = (IN.uv - 0.5) * 2.0; // -1 to 1 from center
                float2 vignetteDist = centeredUV / float2(_VignetteXSize, _VignetteYSize);
                float vignetteDistLength = length(vignetteDist); // Elliptical distance
                
                // Apply edge softening: smoothstep from (1 - soften) to 1
                float vignetteFactor = smoothstep(1.0 - _VignetteEdgeSoften, 1.0, vignetteDistLength);
                vignetteFactor = 1.0 - vignetteFactor; // Invert so edges are darker
                                
                // Calculate alpha based on f value with start/end range
                float alpha = smoothstep(_AlphaStart, _AlphaEnd, f * vignetteFactor);

                return half4(color, alpha);
            }

            ENDHLSL
        }
    }
}

