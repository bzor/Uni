Shader "Custom/PathtracedClouds"
{
    Properties
    {
        _NoiseTex ("Noise Texture", 2D) = "white" {}
        _BlueNoiseTex ("Blue Noise Texture", 2D) = "white" {}
        _SDFTex1 ("SDF Texture 1", 2D) = "white" {}
        _SDFTex2 ("SDF Texture 2", 2D) = "white" {}
        _SDFCrossfade ("SDF Crossfade", Range(0.0, 1.0)) = 0.0
        _SDFDepth ("SDF Extrusion Depth", Float) = 1.0
        _SDFScale ("SDF Scale", Float) = 1.0
        _SDFThreshold ("SDF Threshold (Line Thickness)", Range(0.0, 1.0)) = 1.0
        _SDFCenter ("SDF Center", Vector) = (0.0, 0.0, 0.0, 0.0)
        _TimeScale ("Time Scale", Float) = 0.5
        _MaxSteps ("Max Steps", Int) = 40
        _MarchSize ("March Size", Range(0.0, 0.3)) = 0.16
        _FBMStrength ("FBM Noise Strength", Range(0.0, 10.0)) = 1.0
        _CloudDensity ("Cloud Density", Range(0.0, 2.0)) = 1.0
        _CloudThickness ("Cloud Thickness", Range(0.0, 1.0)) = 0.5
        _SunPosition ("Sun Position", Vector) = (1.0, 0.0, 0.0, 0.0)
        _SunColor ("Sun Color", Color) = (1.0, 0.5, 0.3, 1.0)
        _SkyBaseColor ("Sky Base Color", Color) = (0.7, 0.7, 0.9, 1.0)
        _SkyGradientColor ("Sky Gradient Color", Color) = (0.9, 0.75, 0.9, 1.0)
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
            
            TEXTURE2D(_SDFTex1);
            SAMPLER(sampler_SDFTex1);
            float4 _SDFTex1_ST;
            
            TEXTURE2D(_SDFTex2);
            SAMPLER(sampler_SDFTex2);
            float4 _SDFTex2_ST;

            float _TimeScale;
            int _MaxSteps;
            float _MarchSize;
            float3 _SDFCenter;
            float _FBMStrength;
            float _CloudDensity;
            float _CloudThickness;
            float _SDFDepth;
            float _SDFScale;
            float _SDFThreshold;
            float _SDFCrossfade;
            float3 _SunPosition;
            float4 _SunColor;
            float4 _SkyBaseColor;
            float4 _SkyGradientColor;
            float _DebugRay;
            float _FlipRayZ;
            float _InvertViewIndex;

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

            // 3D noise using texture
            float noise(float3 x)
            {
                float3 p = floor(x);
                float3 f = frac(x);
                f = f * f * (3.0 - 2.0 * f); // smoothstep

                // Calculate UV coordinates
                float2 uv = (p.xy + float2(37.0, 239.0) * p.z) + f.xy;
                
                // Convert to texture coordinates [0, 1]
                // The +0.5 centers the sample in the texel, /256.0 scales to texture size
                // Note: Texture must be set to "Repeat" wrap mode in import settings
                float2 texUV = (uv + 0.5) / 256.0;
                
                // Wrap UVs to [0, 1] range for proper tiling
                texUV = frac(texUV);
                
                // Sample texture
                float2 tex = SAMPLE_TEXTURE2D_LOD(_NoiseTex, sampler_NoiseTex, texUV, 0.0).yx;

                return lerp(tex.x, tex.y, f.z) * 2.0 - 1.0;
            }

            // Fractal Brownian Motion
            float fbm(float3 p)
            {
                float3 q = p + _Time.y * _TimeScale * float3(1.0, -0.2, -1.0);
                float g = noise(q);

                float f = 0.0;
                float scale = 0.5;
                float factor = 2.02;

                [unroll]
                for (int i = 0; i < 8; i++)
                {
                    f += scale * noise(q);
                    q *= factor;
                    factor += 0.21;
                    scale *= 0.5;
                }

                return f;
            }

            // Sample 2D SDF texture and extrude along Z axis (depth)
            // SDF format: 0.5 = on the line, standard signed distance field format
            float sampleSDF(float3 p)
            {
                // Offset by SDF center
                float3 localPos = p - _SDFCenter;
                
                // Sample SDF at XY coordinates (front-facing plane)
                // Divide by scale: larger scale = sample smaller UV range = bigger appearance
                float2 sdfUV = (localPos.xy / _SDFScale) * 0.5 + 0.5; // Center and scale
                
                // Don't clamp - allow UVs to go outside [0,1] so it doesn't clip when rotating
                // The SDF texture will handle out-of-bounds (should be set to clamp in import settings)
                
                // Sample both SDF textures (0.5 = on line, standard signed distance field format)
                float sdfValue1 = SAMPLE_TEXTURE2D_LOD(_SDFTex1, sampler_SDFTex1, sdfUV, 0.0).a;
                float sdfValue2 = SAMPLE_TEXTURE2D_LOD(_SDFTex2, sampler_SDFTex2, sdfUV, 0.0).a;
                
                // Crossfade between the two SDF textures
                float sdfValue = lerp(sdfValue1, sdfValue2, _SDFCrossfade);
                
                // Convert to signed distance field: 0.5 = surface, <0.5 = inside, >0.5 = outside
                // Standard SDF format: distance = (value - 0.5) * 2.0 gives [-1, 1] where 0 is surface
                float sdfDistance = (sdfValue - 0.5) * 2.0;
                
                // Apply threshold to control line thickness
                // Threshold controls how far from the line we still consider "inside"
                // Lower threshold value = thicker lines (more values considered "inside")
                // Convert threshold [0,1] to distance threshold [0, 1.0]
                // When threshold is 1.0, only exactly 0.5 is inside (thin line)
                // When threshold is 0.0, everything is inside (very thick)
                float thickness = (1.0 - _SDFThreshold); // Invert: lower threshold = thicker
                
                // We want clouds ON the line, so create a tube around the line
                // abs(sdfDistance) - thickness: negative near line (clouds), positive far away (no clouds)
                // When thickness is larger, more area around the line becomes negative (thicker clouds)
                // This creates negative distance on the line, which when negated in scene() becomes positive (clouds)
                sdfDistance = abs(sdfDistance) - thickness;
                
                // For the scene function (return -distance + f), we want:
                // -distance to be negative when far (so f can overcome it)
                // -distance to be 0 when on line (so f determines density)
                // So we keep distance positive, and the negative in scene() handles it
                
                // Extrude along Z axis (depth) - check if Z is within extrusion depth
                float zDist = abs(localPos.z);
                float extrusionDist = zDist - _SDFDepth * 0.5;
                
                // Combine SDF distance with Z extrusion
                // Use max to create an intersection (union would be min)
                float finalDistance = max(sdfDistance, extrusionDist);
                
                return finalDistance;
            }

            // Scene SDF
            float scene(float3 p)
            {
                // Use 2D SDF texture with Z-axis extrusion
                float distance = sampleSDF(p);
                
                // Add FBM noise with cloud thickness control
                // CloudThickness reduces noise strength to make clouds thinner/more transparent
                float f = fbm(p) * _FBMStrength * _CloudThickness;
                
                // Apply cloud density multiplier
                // This scales the final density to make clouds more/less visible
                return (-distance + f) * _CloudDensity;
            }

            // AABB intersection test for SDF (bounding box)
            bool intersectAABB(float3 rayOrigin, float3 rayDirection, float3 boxMin, float3 boxMax, out float tNear, out float tFar)
            {
                float3 invDir = 1.0 / max(abs(rayDirection), float3(0.0001, 0.0001, 0.0001));
                float3 t0 = (boxMin - rayOrigin) * invDir;
                float3 t1 = (boxMax - rayOrigin) * invDir;
                
                float3 tMin = min(t0, t1);
                float3 tMax = max(t0, t1);
                
                tNear = max(max(tMin.x, tMin.y), tMin.z);
                tFar = min(min(tMax.x, tMax.y), tMax.z);
                
                return tFar >= tNear && tFar > 0.0;
            }

            // Volume raymarching with blue noise jittering and lighting
            float4 raymarch(float3 rayOrigin, float3 rayDirection, float offset)
            {
                float3 sunDirection = normalize(_SunPosition);
                float4 res = float4(0.0, 0.0, 0.0, 0.0);
                
                // For SDF, use AABB intersection (bounding box)
                // Estimate bounds: SDF scale determines XY extent, depth determines Z extent
                // Add margin for FBM noise
                float margin = _FBMStrength * 0.5;
                float3 boxMin = _SDFCenter - float3(_SDFScale * 0.5 + margin, _SDFScale * 0.5 + margin, _SDFDepth * 0.5 + margin);
                float3 boxMax = _SDFCenter + float3(_SDFScale * 0.5 + margin, _SDFScale * 0.5 + margin, _SDFDepth * 0.5 + margin);
                
                float tNear, tFar;
                if (!intersectAABB(rayOrigin, rayDirection, boxMin, boxMax, tNear, tFar))
                {
                    return res; // Ray doesn't intersect bounds
                }
                
                // Clamp to positive distances only
                tNear = max(0.0, tNear);
                tFar = max(tNear, tFar);
                
                // Start depth at intersection point with blue noise offset
                float depth = tNear + _MarchSize * offset;
                
                // Limit max distance to sphere exit
                float maxDepth = tFar;
                
                [loop]
                for (int i = 0; i < _MaxSteps; i++)
                {
                    // Stop if we've passed the sphere
                    if (depth > maxDepth)
                        break;
                    
                    float3 p = rayOrigin + depth * rayDirection;
                    float density = scene(p);

                    // Only accumulate density if positive
                    if (density > 0.0)
                    {
                        // Directional derivative for fast diffuse lighting
                        float diffuse = saturate((scene(p) - scene(p + 0.3 * sunDirection)) / 0.3);
                        
                        // Lighting calculation
                        float3 lin = _SkyBaseColor.rgb * 1.1 + 0.8 * _SunColor.rgb * diffuse;
                        
                        // Cloud color (white to black based on density)
                        float4 color = float4(lerp(float3(1.0, 1.0, 1.0), float3(0.0, 0.0, 0.0), density), density);
                        color.rgb *= lin;
                        color.rgb *= color.a;
                        res += color * (1.0 - res.a);
                    }

                    depth += _MarchSize;

                    // Early exit if fully opaque
                    if (res.a >= 0.99)
                        break;
                }

                return res;
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

                // Reconstruct world-space point on quad plane from screen coordinates
                // This ensures correct per-view perspective using the per-view VP matrix
                float2 uv = IN.uv;
                float2 ndc = uv * 2.0 - 1.0; // Convert to NDC [-1, 1]
                
                // Reconstruct world position using inverse VP matrix (per-view in multiview)
                // Use the quad's actual depth to get correct Z
                float4 clipPos = float4(ndc.x, ndc.y, 0.0, 1.0);
                float4 worldPos4 = mul(UNITY_MATRIX_I_VP, clipPos);
                worldPos4 /= worldPos4.w;
                
                // Use actual quad world position for correct depth
                // But calculate ray direction from reconstructed screen-space point
                float3 quadWorldPos = IN.worldPos;
                
                // Calculate ray direction from camera to quad world position
                // This should give correct parallax per-view
                float3 rayDirection = normalize(quadWorldPos - rayOrigin);
                
                // Optional Z flip to fix inverted parallax (if needed)
                if (_FlipRayZ > 0.5)
                {
                    rayDirection.z = -rayDirection.z;
                }

                // Debug: visualize ray direction, camera position, or view index
                if (_DebugRay > 0.5)
                {
                    // Visualize ray direction as color
                    // Or uncomment to visualize camera position offset:
                    // float3 camOffset = rayOrigin - float3(0, 0, 0);
                    // return half4(camOffset * 10.0 + 0.5, 1.0);
                    // Or visualize view index:
                    // float viewIdx = (float)unity_StereoEyeIndex / 48.0;
                    // return half4(viewIdx, viewIdx, viewIdx, 1.0);
                    return half4(rayDirection * 0.5 + 0.5, 1.0);
                }

                // Calculate sky color
                float3 skyColor = lerp(_SkyBaseColor.rgb, _SkyGradientColor.rgb, smoothstep(0.0, 1.0, uv.y));
;
                
                // Sample blue noise for jittering (reduces banding)
                // Use screen position for blue noise sampling (matches original GLSL)
                float2 blueNoiseUV = IN.positionHCS.xy / 1024.0;
                float blueNoise = SAMPLE_TEXTURE2D(_BlueNoiseTex, sampler_BlueNoiseTex, blueNoiseUV).r;
                
                // Temporal accumulation using frame counter (modulo 32)
                // Use _Time.y as frame counter approximation
                float frameOffset = frac(blueNoise + frac(_Time.y * 60.0) / sqrt(0.5));
                
                // Perform raymarching with blue noise offset
                float4 cloudResult = raymarch(rayOrigin, rayDirection, frameOffset);

                // Composite sky and clouds
                float3 finalColor = skyColor * (1.0 - cloudResult.a) + cloudResult.rgb;

                return half4(finalColor, 1.0);
            }

            ENDHLSL
        }
    }
}

