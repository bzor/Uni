Shader "Multiview/Lenticular"
{
    Properties
    {
        _MainTexArray ("Color Texture Array", 2DArray) = "white" {}
        _MainTexArray2 ("Color Texture Array 2", 2DArray) = "white" {}
        _ForcedView ("Forced View", Range(0.0, 1.0)) = 0.5
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

            #pragma multi_compile _ FORCED_VIEW
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

            uniform float p_pitch;
            uniform float p_slope;
            uniform half center;

            uniform int screenW;
            uniform int screenH;
            uniform bool rotated;
            uniform int rotScreenW;
            uniform int rotScreenH;
            uniform float invRotScreenW;
            uniform bool usesSubpixelCells;

            uniform float pixelW;
            uniform float pixelH;

            // [which subpixel cell] [r, g, or b] [x or y]
            uniform half4x4 subpixelCells[2];

            uniform half _ForcedView;

            half4 frag (v2f input) : SV_Target
            {
                float2 uv = input.uv;
                uv.x = (floor(uv.x * rotScreenW) + 0.5) * invRotScreenW;

                if (rotated)
                    uv = float2(1.0 - uv.y, uv.x);

                uint dither_x = uint(input.uv.x * rotScreenW) % 2;
                uint dither_y = uint(input.uv.y * rotScreenH) % 2;
                half dither = dither_x ^ dither_y;

                float3 n_views = uv.x + uv.y * p_slope;

                // oled stuff
                if (!usesSubpixelCells) {
                    n_views[1] += 1.0 * pixelW / 3.0;
                    n_views[2] += 2.0 * pixelW / 3.0;
                } else {
                    int cell = uint(uv.y * screenH) % uint(2);
                    n_views[0] = uv.x + subpixelCells[cell][0][0] * pixelW;
                    n_views[1] = uv.x + subpixelCells[cell][1][0] * pixelW;
                    n_views[2] = uv.x + subpixelCells[cell][2][0] * pixelW;

                    n_views[0] += (uv.y + subpixelCells[cell][0][1] * pixelH) * p_slope;
                    n_views[1] += (uv.y + subpixelCells[cell][1][1] * pixelH) * p_slope;
                    n_views[2] += (uv.y + subpixelCells[cell][2][1] * pixelH) * p_slope;
                }

                n_views *= p_pitch;
                n_views -= center;
                n_views = 1.0 - frac(n_views);
                // n_views = clamp(n_views, 0.00001, 0.99999);

                half4 col4 = half4(0, 0, 0, 1);
                #define col col4[i]
                for (int i = 0; i < 3; i++)
                {
                    half normView = n_views[i];
#ifdef FORCED_VIEW
                    normView = _ForcedView;
#endif

#if GEN_VIEWS_ON
                    half blendView = normView * (LKG_VIEWCOUNT * 2 - 1);
                    // blendView = floor(blendView);
                    uint viewDouble = uint(floor(blendView));
                    uint inBetween = viewDouble % 2;
                    uint view2 = viewDouble / 2;
                    uint view1 = view2 + inBetween;

                    half blendRight = frac(blendView);
                    half blendLeft = 1.0 - blendRight;

                    half blend1 = inBetween == 0 ? blendLeft : blendRight;
                    half blend2 = inBetween == 0 ? blendRight : blendLeft;

                    col =
                        UNITY_SAMPLE_TEX2DARRAY_LOD(
                            _MainTexArray, float3(uv, view1), 0.0
                        )[i] * blend1
                        + UNITY_SAMPLE_TEX2DARRAY_LOD(
                            _MainTexArray2, float3(uv, view2), 0.0
                        )[i] * blend2;
#else
                    half blendView = normView * LKG_VIEWCOUNT;
                    uint view1 = floor(blendView);
                    uint view2 = view1 + 1;
                    half blendRight = frac(blendView);
                    half blendLeft = 1.0 - blendRight;
                    col =
                        UNITY_SAMPLE_TEX2DARRAY_LOD(
                            _MainTexArray, float3(uv, view1), 0.0
                        )[i] * blendLeft
                        + UNITY_SAMPLE_TEX2DARRAY_LOD(
                            _MainTexArray, float3(uv, view2), 0.0
                        )[i] * blendRight;
#endif
                }


                return col4;
            }
            ENDCG
        }
    }
}
