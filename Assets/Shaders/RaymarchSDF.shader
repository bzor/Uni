Shader "Custom/RaymarchSDF"
{
    Properties
    {
        _MaxDistance ("Max Ray Distance", Float) = 20.0
        _MaxSteps    ("Max Steps",       Float) = 128.0
        _Epsilon     ("Surface Epsilon", Float) = 0.001
        _SphereRadius("Sphere Radius",   Float) = 1.0
        _SphereHeight("Sphere Height",   Float) = 0.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry+100" }

        // Fullscreen procedural, we want to always write our depth for our “fake” geometry
        ZWrite On
        ZTest Always
        Cull Off
        Blend Off

        Pass
        {
            Name "RaymarchSDF"

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // ---------- Properties ----------
            float _MaxDistance;
            float _MaxSteps;
            float _Epsilon;
            float _SphereRadius;
            float _SphereHeight;

            // ---------- Fullscreen Vertex ----------

            struct Attributes
            {
                float3 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
            };

            Varyings Vert (Attributes IN)
            {
                Varyings OUT;

                // We use a fullscreen triangle; the mesh will pass us coords in [-1,1]
                OUT.positionHCS = float4(IN.positionOS.xy, 0.0, 1.0);
                OUT.uv = IN.positionOS.xy * 0.5f + 0.5f;  // [-1,1] -> [0,1]

                return OUT;
            }

            // ---------- SDF Scene ----------

            // Sphere SDF
            float sdSphere(float3 p, float r)
            {
                return length(p) - r;
            }

            // Plane SDF (y = 0 plane)
            float sdPlane(float3 p)
            {
                return p.y;
            }

            // Scene SDF: return signed distance and material id (for fun)
            float mapScene(float3 p, out int matID)
            {
                // Sphere at some height above the plane
                float3 spherePos = p - float3(0.0, _SphereHeight, 0.0);
                float dSphere = sdSphere(spherePos, _SphereRadius);
                float dPlane  = sdPlane(p);

                float d = dSphere;
                matID = 1; // sphere

                if (dPlane < d)
                {
                    d = dPlane;
                    matID = 2; // ground
                }

                return d;
            }

            // Estimate normal by sampling the SDF in a small neighborhood
            float3 estimateNormal(float3 p)
            {
                const float eps = 0.001;
                int dummy;
                float3 ex = float3(eps, 0, 0);
                float3 ey = float3(0, eps, 0);
                float3 ez = float3(0, 0, eps);

                float dx = mapScene(p + ex, dummy) - mapScene(p - ex, dummy);
                float dy = mapScene(p + ey, dummy) - mapScene(p - ey, dummy);
                float dz = mapScene(p + ez, dummy) - mapScene(p - ez, dummy);

                return normalize(float3(dx, dy, dz));
            }

            // ---------- Raymarch ----------

            bool Raymarch(float3 ro, float3 rd, out float tHit, out int matID)
            {
                tHit = 0.0;
                matID = 0;

                [loop]
                for (int i = 0; i < (int)_MaxSteps; i++)
                {
                    float3 p = ro + rd * tHit;

                    float dist = mapScene(p, matID);

                    if (dist < _Epsilon)
                    {
                        return true; // Hit surface
                    }

                    tHit += dist;

                    if (tHit > _MaxDistance)
                        break;
                }

                return false;
            }

            // Simple lighting: one directional light + ambient
            float3 Shade(float3 p, float3 n, int matID)
            {
                // Basic colors per material
                float3 baseColor =
                    (matID == 1) ? float3(0.9, 0.2, 0.4) : // sphere
                    (matID == 2) ? float3(0.2, 0.5, 0.2) : // ground
                                   float3(0.0, 0.0, 0.0);

                // Get main directional light from URP
                Light mainLight = GetMainLight();
                float3 L = normalize(mainLight.direction);
                float3 N = n;
                float3 V = normalize(_WorldSpaceCameraPos - p);

                float NdotL = saturate(dot(N, -L)); // lights in URP point *toward* the object
                float3 diffuse = baseColor * NdotL * mainLight.color;

                // Simple specular
                float3 H = normalize(-L + V);
                float spec = pow(saturate(dot(N, H)), 32.0);
                float3 specular = spec * mainLight.color * 0.4;

                float3 ambient = baseColor * 0.1;

                return diffuse + specular + ambient;
            }

            // ---------- Depth Conversion ----------

            // Turn world position into a normalized depth for SV_Depth
            float ComputeDepth01(float3 positionWS)
            {
                // To homogeneous clip space:
                float4 positionCS = TransformWorldToHClip(positionWS);

                // Perspective divide
                float depth = positionCS.z / positionCS.w;

                // Handle reversed Z (Unity on D3D, etc.)
                #if UNITY_REVERSED_Z
                    depth = 1.0 - depth;
                #endif

                return saturate(depth);
            }

            // ---------- Fragment ----------

            struct FragOutput
            {
                half4 color : SV_Target;
                float depth : SV_Depth;
            };

            FragOutput Frag (Varyings IN)
            {
                FragOutput OUT;

                float2 uv = IN.uv;
                float2 ndc = uv * 2.0 - 1.0; // [-1,1]

                // Clip space position at far plane
                float4 clipPos = float4(ndc, 1.0, 1.0);

                // Use inverse view-projection to reconstruct a world-space point on the far plane
                float4 worldPos4 = mul(UNITY_MATRIX_I_VP, clipPos);
                worldPos4 /= worldPos4.w;

                float3 rayOriginWS = _WorldSpaceCameraPos;
                float3 rayDirWS = normalize(worldPos4.xyz - rayOriginWS);

                float tHit;
                int matID;

                bool hit = Raymarch(rayOriginWS, rayDirWS, tHit, matID);

                if (hit)
                {
                    float3 hitPosWS = rayOriginWS + rayDirWS * tHit;
                    float3 normalWS = estimateNormal(hitPosWS);
                    float3 color = Shade(hitPosWS, normalWS, matID);

                    OUT.color = half4(color, 1.0);
                    OUT.depth = ComputeDepth01(hitPosWS);
                }
                else
                {
                    // Background
                    float3 sky = float3(0.02, 0.02, 0.05) + float3(0.1, 0.15, 0.3) * uv.y;
                    OUT.color = half4(sky, 1.0);

                    // Put this at far plane (no geometry)
                    #if UNITY_REVERSED_Z
                        OUT.depth = 0.0;
                    #else
                        OUT.depth = 1.0;
                    #endif
                }

                return OUT;
            }

            ENDHLSL
        }
    }
}
