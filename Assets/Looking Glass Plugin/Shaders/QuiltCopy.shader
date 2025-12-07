Shader "Multiview/Quilt Copy"
{
    Properties
    {
        _MainTexArray ("Color Texture Array", 2DArray) = "white" {}
        _MainTexArray2 ("Color Texture Array 2", 2DArray) = "white" {}
    }
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        Pass
        {

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            // #pragma multi_compile _ FORCED_VIEW
            #pragma multi_compile _ GEN_VIEWS_ON

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            UNITY_DECLARE_TEX2DARRAY(_MainTexArray);
            UNITY_DECLARE_TEX2DARRAY(_MainTexArray2);

            #pragma multi_compile _ GEN_VIEWS_ON
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/LookingGlass/LookingGlassSettings.hlsl"

            int quiltColumns;
            int quiltRows;

            half4 frag (v2f input) : SV_Target
            {
                float2 uv = input.uv;

                float2 tileSize = float2(1.0 / quiltColumns, 1.0 / quiltRows);
                int col = floor(uv.x / tileSize.x);
                int row = floor(uv.y / tileSize.y);
                int view = row * quiltColumns + col;

                float2 tileOrigin = float2(col, row) * tileSize;
                float2 localUV = (uv - tileOrigin) / tileSize;

                float4 outCol = float4(0, 0, 0, 1);

#if GEN_VIEWS_ON

                if (view % 2 == 0)
                    outCol = UNITY_SAMPLE_TEX2DARRAY_LOD(_MainTexArray, float3(localUV, view / 2), 0.0);
                else
                    outCol = UNITY_SAMPLE_TEX2DARRAY_LOD(_MainTexArray2, float3(localUV, view / 2), 0.0);
#else
                outCol = UNITY_SAMPLE_TEX2DARRAY_LOD(_MainTexArray, float3(localUV, view), 0.0);
#endif
                return outCol;
            }
            ENDCG
        }
    }
}
