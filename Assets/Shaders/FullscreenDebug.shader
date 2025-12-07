Shader "Custom/FullscreenDebug"
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
            Name "FullscreenDebug"

            ZWrite Off
            ZTest Always
            Cull Off
            Blend Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
            };

            Varyings Vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = GetFullScreenTriangleVertexPosition(IN.vertexID);
                OUT.uv          = GetFullScreenTriangleTexCoord(IN.vertexID);
                return OUT;
            }

            half4 Frag (Varyings IN) : SV_Target
            {
                // Loud debug color + slight gradient so we see it's our pass
                float2 uv = IN.uv;
                return half4(1.0, uv.x, uv.y, 1.0); // bright pink-ish
            }

            ENDHLSL
        }
    }
}
