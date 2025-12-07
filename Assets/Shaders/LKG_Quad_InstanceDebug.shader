Shader "Custom/LKG_Quad_InstancedDebug"
{
    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
            "Queue"="Overlay"
            "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Name "LKG_Quad_InstancedDebug"

            // Draw on top of everything for debugging
            ZWrite Off
            ZTest Always
            Cull Off
            Blend Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            // Instancing + stereo variants
            #pragma multi_compile_instancing
            #pragma multi_compile _ UNITY_SINGLE_PASS_STEREO

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert (Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.positionHCS = TransformObjectToHClip(IN.positionOS);
                OUT.uv          = IN.uv;

                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                return OUT;
            }

            half4 Frag (Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                float2 uv = IN.uv;

                // Eye index / view index debug
                #if defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_SINGLE_PASS_STEREO)
                    uint eye = unity_StereoEyeIndex;
                #else
                    uint eye = 0;
                #endif

                float3 col;
                if (eye == 0)
                    col = float3(1.0, uv.y, uv.x);   // red-ish
                else if (eye == 1)
                    col = float3(uv.x, 1.0, uv.y);   // green-ish
                else
                    col = float3(uv.x, uv.y, 1.0);   // blue-ish

                return half4(col, 1.0);
            }

            ENDHLSL
        }
    }
}
