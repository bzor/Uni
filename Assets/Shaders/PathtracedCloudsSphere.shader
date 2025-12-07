Shader "Custom/PathtracedCloudsSphere"
{
    Properties
    {
        _NoiseTex ("Noise Texture", 2D) = "white" {}
        _BlueNoiseTex ("Blue Noise Texture", 2D) = "white" {}
        _TimeScale ("Time Scale", Float) = 0.5
        _MaxSteps ("Max Steps", Int) = 40
        _MarchSize ("March Size", Range(0.0, 0.3)) = 0.16
        _SphereRadius ("Sphere Radius", Float) = 1.2
        _SphereCenter ("Sphere Center", Vector) = (0.0, 0.0, 0.0, 0.0)
        _FBMStrength ("FBM Noise Strength", Range(0.0, 10.0)) = 1.0
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

            float _TimeScale;
            int _MaxSteps;
            float _MarchSize;
            float _SphereRadius;
            float3 _SphereCenter;
            float _FBMStrength;
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

            // Signed distance to sphere
            float sdSphere(float3 p, float radius)
            {
                return length(p) - radius;
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

            // Scene SDF
            float scene(float3 p)
            {
                // Offset by sphere center
                float distance = sdSphere(p - _SphereCenter, _SphereRadius);
                float f = fbm(p) * _FBMStrength;
                return -distance + f;
            }

            // Sphere intersection test - returns entry and exit distances
            bool intersectSphere(float3 rayOrigin, float3 rayDirection, float3 sphereCenter, float sphereRadius, out float tNear, out float tFar)
            {
                float3 oc = rayOrigin - sphereCenter;
                float a = dot(rayDirection, rayDirection);
                float b = 2.0 * dot(oc, rayDirection);
                float c = dot(oc, oc) - sphereRadius * sphereRadius;
                float discriminant = b * b - 4.0 * a * c;
                
                if (discriminant < 0.0)
                {
                    tNear = 0.0;
                    tFar = 0.0;
                    return false;
                }
                
                float sqrtDisc = sqrt(discriminant);
                tNear = (-b - sqrtDisc) / (2.0 * a);
                tFar = (-b + sqrtDisc) / (2.0 * a);
                
                // Swap if near > far
                if (tNear > tFar)
                {
                    float temp = tNear;
                    tNear = tFar;
                    tFar = temp;
                }
                
                return tFar > 0.0; // Ray intersects if far point is positive
            }

            // Volume raymarching with blue noise jittering and lighting
            float4 raymarch(float3 rayOrigin, float3 rayDirection, float offset)
            {
                float3 sunDirection = normalize(_SunPosition);
                float4 res = float4(0.0, 0.0, 0.0, 0.0);
                
                // Calculate sphere bounds (radius + FBM can extend it, so add some margin)
                float effectiveRadius = _SphereRadius + _FBMStrength * 0.5;
                float tNear, tFar;
                
                // Test sphere intersection to find valid raymarch range
                if (!intersectSphere(rayOrigin, rayDirection, _SphereCenter, effectiveRadius, tNear, tFar))
                {
                    return res; // Ray doesn't intersect sphere
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

