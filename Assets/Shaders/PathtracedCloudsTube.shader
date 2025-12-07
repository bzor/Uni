Shader "Custom/PathtracedCloudsTube"
{
    Properties
    {
        _NoiseTex ("Noise Texture", 2D) = "white" {}
        _BlueNoiseTex ("Blue Noise Texture", 2D) = "white" {}
        _TimeScale ("Time Scale", Float) = 1.0
        _MaxSteps ("Max Steps", Int) = 130
        _TubeRadius ("Tube Radius", Float) = 2.5
        _TubeCenter ("Tube Center", Vector) = (0.0, 0.0, 0.0, 0.0)
        _NoiseStrength ("Noise Strength", Range(0.0, 2.0)) = 1.0
        _DensityThreshold ("Density Threshold", Range(0.0, 1.0)) = 0.3
        _FogColor ("Fog Color", Color) = (0.06, 0.11, 0.11, 0.1)
        _VignetteStrength ("Vignette Strength", Range(0.0, 1.0)) = 0.7
        [Toggle] _DebugRay ("Debug Ray Direction", Float) = 0
        [Toggle] _FlipRayZ ("Flip Ray Z (Fix Parallax)", Float) = 0
        [Toggle] _InvertViewIndex ("Invert View Index (Fix Parallax)", Float) = 0
    }

    SubShader
    {
        Tags 
        { 
            "RenderPipeline"="UniversalPipeline" 
            "RenderType"="Transparent" 
            "Queue"="Transparent" 
        }

        Pass
        {
            Name "PathtracedClouds"
            Tags { "LightMode"="UniversalForwardOnly" }

            ZWrite Off
            ZTest Always
            Cull Back
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ UNITY_STEREO_INSTANCING_ENABLED

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);
            float4 _NoiseTex_ST;
            
            TEXTURE2D(_BlueNoiseTex);
            SAMPLER(sampler_BlueNoiseTex);
            float4 _BlueNoiseTex_ST;

            float _TimeScale;
            int _MaxSteps;
            float _TubeRadius;
            float3 _TubeCenter;
            float _NoiseStrength;
            float _DensityThreshold;
            float4 _FogColor;
            float _VignetteStrength;
            float _DebugRay;
            float _FlipRayZ;
            float _InvertViewIndex;
            
            // Constant rotation matrix for noise
            static const float3x3 m3 = float3x3(
                0.33338, 0.56034, -0.71817,
                -0.87887, 0.32651, -0.15323,
                0.15162, 0.69596, 0.61339
            ) * 1.93;

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
                float3 worldPos : TEXCOORD1;
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
                OUT.worldPos = worldPos;
                return OUT;
            }

            // 2x2 rotation matrix
            float2x2 rot(float a)
            {
                float c = cos(a);
                float s = sin(a);
                return float2x2(c, s, -s, c);
            }
            
            // Magnitude squared
            float mag2(float2 p)
            {
                return dot(p, p);
            }
            
            // Linear step function
            float linstep(float mn, float mx, float x)
            {
                return saturate((x - mn) / (mx - mn));
            }
            
            // Displacement function
            float2 disp(float t)
            {
                return float2(sin(t * 0.22), cos(t * 0.175)) * 2.0;
            }
            
            // Procedural noise for tube clouds
            float hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }
            
            float noise(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                
                float n = i.x + i.y * 57.0 + 113.0 * i.z;
                return lerp(
                    lerp(lerp(hash(n + 0.0), hash(n + 1.0), f.x),
                         lerp(hash(n + 57.0), hash(n + 58.0), f.x), f.y),
                    lerp(lerp(hash(n + 113.0), hash(n + 114.0), f.x),
                         lerp(hash(n + 170.0), hash(n + 171.0), f.x), f.y), f.z);
            }
            
            // Tube volume map function
            float2 map(float3 p, float time, float prm1)
            {
                // Offset by tube center
                p = p - _TubeCenter;
                
                float3 p2 = p;
                float2 dispVal = disp(p.z);
                p2.xy -= dispVal;
                
                // Rotate XY plane
                float rotAngle = sin(p.z + time) * (0.1 + prm1 * 0.05) + time * 0.09;
                p.xy = mul(rot(rotAngle), p.xy);
                
                float cl = mag2(p2.xy);
                float d = 0.0;
                p *= 0.61;
                float z = 1.0;
                float trk = 1.0;
                float dspAmp = 0.1 + prm1 * 0.2;
                
                [unroll]
                for (int i = 0; i < 5; i++)
                {
                    p += sin(p.zxy * 0.75 * trk + time * trk * 0.8) * dspAmp;
                    d -= abs(dot(cos(p), sin(p.yzx)) * z);
                    z *= 0.57;
                    trk *= 1.4;
                    p = mul(m3, p);
                }
                
                d = abs(d + prm1 * 3.0) + prm1 * 0.3 - _TubeRadius;
                return float2(d + cl * 0.2 + 0.25, cl);
            }
            
            // Get saturation of color
            float getsat(float3 c)
            {
                float mi = min(min(c.x, c.y), c.z);
                float ma = max(max(c.x, c.y), c.z);
                return (ma - mi) / (ma + 1e-7);
            }
            
            // Interpolate colors preserving saturation
            float3 iLerp(float3 a, float3 b, float x)
            {
                float3 ic = lerp(a, b, x) + float3(1e-6, 0.0, 0.0);
                float sd = abs(getsat(ic) - lerp(getsat(a), getsat(b), x));
                float3 dir = normalize(float3(2.0 * ic.x - ic.y - ic.z, 2.0 * ic.y - ic.x - ic.z, 2.0 * ic.z - ic.y - ic.x));
                float lgt = dot(float3(1.0, 1.0, 1.0), ic);
                float ff = dot(dir, normalize(ic));
                ic += 1.5 * dir * sd * ff * lgt;
                return saturate(ic);
            }

            // Volume raymarching with dynamic step sizing and fog
            float4 render(float3 ro, float3 rd, float time, float prm1)
            {
                float4 rez = float4(0.0, 0.0, 0.0, 0.0);
                const float ldst = 8.0;
                float3 lpos = float3(disp(time + ldst) * 0.5, time + ldst);
                float t = 1.5;
                float fogT = 0.0;
                
                [loop]
                for (int i = 0; i < _MaxSteps; i++)
                {
                    if (rez.a > 0.99)
                        break;
                    
                    float3 pos = ro + t * rd;
                    float2 mpv = map(pos, time, prm1);
                    float den = saturate(mpv.x - _DensityThreshold) * 1.12;
                    float dn = saturate(mpv.x + 2.0);
                    
                    float4 col = float4(0.0, 0.0, 0.0, 0.0);
                    if (mpv.x > 0.6)
                    {
                        // Cloud color based on position and noise
                        float3 cloudColor = sin(float3(5.0, 0.4, 0.2) + mpv.y * 0.1 + sin(pos.z * 0.4) * 0.5 + 1.8) * 0.5 + 0.5;
                        col = float4(cloudColor, 0.08);
                        col *= den * den * den;
                        col.rgb *= linstep(4.0, -2.5, mpv.x) * 2.3;
                        
                        // Lighting with multiple samples for better quality
                        float dif = saturate((den - map(pos + 0.8, time, prm1).x) / 9.0);
                        dif += saturate((den - map(pos + 0.35, time, prm1).x) / 2.5);
                        col.rgb *= den * (float3(0.005, 0.045, 0.075) + 1.5 * float3(0.033, 0.07, 0.03) * dif);
                    }
                    
                    // Fog evaluation
                    float fogC = exp(t * 0.2 - 2.2);
                    col.rgba += _FogColor * saturate(fogC - fogT);
                    fogT = fogC;
                    
                    rez = rez + col * (1.0 - rez.a);
                    
                    // Dynamic step sizing based on density
                    t += clamp(0.5 - dn * dn * 0.05, 0.09, 0.3);
                }
                
                return saturate(rez);
            }
            
            half4 Frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                // Get per-view camera world position for Looking Glass multiview
                #if defined(USING_STEREO_MATRICES) && defined(UNITY_STEREO_INSTANCING_ENABLED)
                    uint viewIdx = unity_StereoEyeIndex;
                    // Test: invert view index if parallax is swapped
                    if (_InvertViewIndex > 0.5)
                    {
                        // Assuming 48 views, invert the index
                        viewIdx = (48 - 1) - viewIdx;
                    }
                    float3 rayOrigin = unity_StereoWorldSpaceCameraPos_lkg[viewIdx];
                #else
                    float3 rayOrigin = UNITY_MATRIX_I_V._14_24_34;
                #endif

                // Setup time and camera for tube clouds
                float time = _Time.y * _TimeScale * 3.0;
                
                // Reconstruct per-pixel world position on quad plane
                // This ensures correct per-view perspective using the per-view VP matrix
                float2 uv = IN.uv;
                float2 ndc = uv * 2.0 - 1.0; // Convert to NDC [-1, 1]
                
                // Reconstruct world position using inverse VP matrix (per-view in multiview)
                float4 clipPos = float4(ndc.x, ndc.y, 0.0, 1.0);
                float4 worldPos4 = mul(UNITY_MATRIX_I_VP, clipPos);
                worldPos4 /= worldPos4.w;
                
                // Use actual quad world position Z for correct depth
                float3 quadWorldPos = IN.worldPos;
                float3 perPixelWorldPos = float3(worldPos4.xy, quadWorldPos.z);
                
                // Use actual Unity camera position (per-view for multiview)
                // This ensures correct parallax on Looking Glass
                float3 ro = rayOrigin;
                
                // Calculate per-pixel ray direction from camera to reconstructed world position
                // This gives correct parallax per-view and per-pixel (same as working shader)
                float3 rd = normalize(perPixelWorldPos - rayOrigin);
                
                // Calculate tube movement parameters
                float dspAmp = 0.85;
                
                // Optional Z flip to fix inverted parallax (if needed)
                if (_FlipRayZ > 0.5)
                {
                    rd.z = -rd.z;
                }

                // Debug: visualize ray direction
                if (_DebugRay > 0.5)
                {
                    return half4(rd * 0.5 + 0.5, 1.0);
                }
                
                // Calculate parameter for color interpolation
                float prm1 = smoothstep(-0.4, 0.4, sin(_Time.y * 0.3));
                
                // Transform ray to tube's local coordinate system
                // The tube moves through world space, so we need to transform coordinates
                // to make it appear the camera is moving through a stationary tube
                float3 tubeOffset = _TubeCenter + float3(0.0, 0.0, time);
                float2 tubeDisp = disp(tubeOffset.z);
                tubeOffset.xy += tubeDisp * dspAmp;
                
                // Transform both ray origin and direction to tube's moving coordinate system
                // This preserves the ray's relationship while moving the coordinate system
                float3 roLocal = ro - tubeOffset;
                // Note: rd stays the same since it's a direction vector, not a position
                
                // Render clouds with transformed coordinates
                float4 scn = render(roLocal, rd, time, prm1);
                
                // Color interpolation preserving saturation
                float3 col = scn.rgb;
                col = iLerp(col.bgr, col.rgb, clamp(1.0 - prm1, 0.05, 1.0));
                
                // Color grading (ensure positive values for pow)
                col = pow(max(col, 0.0), float3(0.55, 0.65, 0.6)) * float3(1.0, 0.97, 0.9);
                
                // Vignette (ensure positive values for pow)
                float vignetteValue = 16.0 * uv.x * uv.y * (1.0 - uv.x) * (1.0 - uv.y);
                float vignette = pow(max(vignetteValue, 0.0), 0.12) * _VignetteStrength + (1.0 - _VignetteStrength);
                col *= vignette;

                return half4(col, 1.0);
            }

            ENDHLSL
        }
    }
}

