Shader "Custom/RaymarchSDF_Mesh"
{
    Properties
    {
        _MaxDistance ("Max Ray Distance", Float) = 20.0
        _MaxSteps    ("Max Steps",       Float) = 128.0
        _Epsilon     ("Surface Epsilon", Float) = 0.001
        _SphereRadius("Sphere Radius",   Float) = 1.0
        _SphereHeight("Sphere Height",   Float) = 0.5
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
            "Queue"="Geometry+100"
            "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Name "RaymarchSDF_Mesh"

            // We’re drawing “fake geometry” and want to provide our own depth
            ZWrite On
            ZTest Always
            Cull Off
            Blend Off

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

            // ---------- Vertex / Varyings for a standard mesh quad ----------

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
            };

            Varyings Vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS);
                OUT.uv          = IN.uv; // Use mesh UVs as screen UVs
                return OUT;
            }

            // ---------- SDF Scene ----------

            float sdSphere(float3 p, float r)
            {
                return length(p) - r;
            }

            float sdPlane(float3 p)
            {
                return p.y; // plane at y=0
            }

            float mapScene(float3 p, out int matID)
            {
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
                        return true;

                    tHit += dist;

                    if (tHit > _MaxDistance)
                        break;
                }

                return false;
            }

            float3 Shade(float3 p, float3 n, int matID)
            {
                float3 baseColor =
                    (matID == 1) ? float3(0.9, 0.2, 0.4) : // sphere
                    (matID == 2) ? float3(0.2, 0.5, 0.2) : // ground
                                   float3(0.0, 0.0, 0.0);

                Light mainLight = GetMainLight();
                float3 L = normalize(mainLight.direction);
                float3 N = n;
                float3 V = normalize(_WorldSpaceCameraPos - p);

                float NdotL = saturate(dot(N, -L)); // URP directional light direction
                float3 diffuse  = baseColor * NdotL * mainLight.color;

                float3 H = normalize(-L + V);
                float  spec = pow(saturate(dot(N, H)), 32.0);
                float3 specular = spec * mainLight.color * 0.4;

                float3 ambient = baseColor * 0.1;

                return diffuse + specular + ambient;
            }

            float ComputeDepth01(float3 positionWS)
            {
                float4 positionCS = TransformWorldToHClip(positionWS);
                float depth = positionCS.z / positionCS.w;

                #if UNITY_REVERSED_Z
                    depth = 1.0 - depth;
                #endif

                return saturate(depth);
            }

            struct FragOutput
            {
                half4 color : SV_Target;
                float depth : SV_Depth;
            };

FragOutput Frag (Varyings IN)
{
    FragOutput OUT;
    float2 uv = IN.uv;
    OUT.color = half4(uv.x, uv.y, 0.0, 1.0);

    #if UNITY_REVERSED_Z
        OUT.depth = 0.0;
    #else
        OUT.depth = 1.0;
    #endif

    return OUT;
}


            ENDHLSL
        }
    }
}
