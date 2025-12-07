Shader "Looking Glass/Gen Views"
{
    Properties
    {
        _MainTexArray ("Color Texture Array", 2DArray) = "white" {}
        _DepthTexArray ("Depth Texture Array", 2DArray) = "white" {}
        _MaskTexArray ("Mask Texture Array", 2DArray) = "white" {}
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

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                uint instanceID : SV_InstanceID;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                uint slice : SV_RenderTargetArrayIndex;
            };

            struct f2a
            {
                half4 color : SV_Target0;
                half depth : SV_Target1;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.slice = v.instanceID;
                return o;
            }

            UNITY_DECLARE_TEX2DARRAY(_MainTexArray);
            UNITY_DECLARE_TEX2DARRAY(_DepthTexArray);
            UNITY_DECLARE_TEX2DARRAY(_CameraDepthTexture);
            UNITY_DECLARE_TEX2DARRAY(_MaskTexArray);

            #pragma multi_compile _ USE_CAMERA_DEPTH

            #ifdef USE_CAMERA_DEPTH
            #define _DepthTexArray _CameraDepthTexture
            #endif


            #pragma multi_compile _ GEN_VIEWS_ON
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/LookingGlass/LookingGlassSettings.hlsl"

            // linear depth stuff
            // ******************

            uniform half lkg_nearClip;
            uniform half lkg_farClip;
            uniform half lkg_focalDist;
            uniform half lkg_focalDistInv;
            uniform half lkg_perspW;
            uniform half lkg_maxOffset;
            uniform half4x4 lkg_projMat;
            uniform half4 lkg_linearDepthParamsReversedZ;
            uniform half4 lkg_linearDepthParams;

            half MyLinearEyeDepth(half rawdepth) {
                half rd = max(rawdepth, 0.0001);
#if UNITY_REVERSED_Z
                return 1.0 / (lkg_linearDepthParamsReversedZ.z * rd + lkg_linearDepthParamsReversedZ.w);
#else
                return 1.0 / (lkg_linearDepthParams.z * rd + lkg_linearDepthParams.w);
#endif
            }

            half MyLinear01Depth (half ed) {
                return (ed - lkg_nearClip) / (lkg_farClip - lkg_nearClip);
            }


            // this is essentially this set of commands
            // float distFromFocus = dist - lkg_focalDist;
            // float offsetAtDist = offsetLoop[side] * distFromFocus / lkg_focalDist;
            // float uv_offset = -offsetAtDist * 0.5 / dist * lkg_projMat[0][0];
            #define UV_OFFSET_AT_DIST(dist, offset) (-(offset * (dist - lkg_focalDist) * lkg_focalDistInv) * 0.5 / dist * lkg_projMat[0][0])

            // just this:
            // depth01 = UNITY_SAMPLE_TEX2DARRAY_LOD(
            //     _DepthTexArray,
            //     float3(uv.x + offsetViewspace, uv.y, viewLoop[side]),
            //     0.0
            // ).r;
            // depthEye = MyLinearEyeDepth(depth01);
            #define SAMPLE_LINEAR_DEPTH(uv, view) (MyLinearEyeDepth(UNITY_SAMPLE_TEX2DARRAY_LOD(_DepthTexArray, float3(uv, view), 0.0).r))
            #define SAMPLE_DEPTH(uv, view) (UNITY_SAMPLE_TEX2DARRAY_LOD(_DepthTexArray, float3(uv, view), 0.0).r)

            // get color for channel
            // col[i] = UNITY_SAMPLE_TEX2DARRAY_LOD(
            //     _MainTexArray,
            //     float3(uv.x + xOffset, uv.y, viewLoop[bestSide]),
            //     0.0
            // )[i];
            #define SAMPLE_COLOR(uv, view) (UNITY_SAMPLE_TEX2DARRAY_LOD(_MainTexArray, float3(uv, view), 0.0))

            // f2a frag (v2f i) : SV_Target
            f2a frag (v2f i)
            {
                #define LINEAR_STEPS 40
                #define BINARY_STEPS 4

                f2a output;

                half totalDist = lkg_farClip - lkg_nearClip;
                half stepDist = totalDist / (LINEAR_STEPS);

                bool hit = false;
                bool permaFail = false;
                half finalDepth01, finalDepth, finalOffset;
                half originalDepth01 = SAMPLE_DEPTH(i.uv, i.slice);
                half originalDepth = MyLinearEyeDepth(originalDepth01);
                half offsetBase = (0.5 / (LKG_VIEWCOUNT - 1.0)) * lkg_maxOffset * 2.0;
                // half offsetBase = 0.0;

                // skip based on mask
                half maskVal = UNITY_SAMPLE_TEX2DARRAY_LOD(_MaskTexArray, float3(i.uv, i.slice), 0.0).r;
                // if (maskVal != 0)
                // {
                //     output.color = half4(0,0,1,1);
                //     output.depth = originalDepth01;
                //     return output;
                // }

                if (maskVal == 0)
                {
                    // try cheap offset parallax first, from both sides
                    for (int side = 0; side < 2; side++)
                    // for (int side = 2; side >= 0; side--)
                    // int side = 0;
                    // if (false)
                    {
                        half offset = offsetBase * (1 - side * 2);

                        // check depth, move over, check the depth there and see if it's roughly what we expected.
                        half firstDepth = side == 0 ? originalDepth : SAMPLE_LINEAR_DEPTH(i.uv, i.slice + side);

                        finalOffset = UV_OFFSET_AT_DIST(firstDepth, offset);
                        finalDepth01 = SAMPLE_DEPTH(i.uv + float2(finalOffset, 0.0), i.slice + side);
                        finalDepth = MyLinearEyeDepth(finalDepth01);

                        half checkDepth2 = SAMPLE_LINEAR_DEPTH(i.uv - float2(finalOffset * 0.5, 0), i.slice + 1 - side);

                        // #define CHECK_STEPS 3
                        // half checkDepths[CHECK_STEPS];
                        // for (int ci = 0; ci < CHECK_STEPS; ci++)
                        // {
                        //     checkDepths[ci] = SAMPLE_LINEAR_DEPTH(i.uv + float2(finalOffset * (float(ci + 1) / (CHECK_STEPS + 1)), 0.0), i.slice + side);
                        // }

                        if (
                            // abs(firstDepth - finalDepth) < stepDist
                            // && abs(checkDepth2 - finalDepth) < stepDist
                            abs(firstDepth - finalDepth) < stepDist
                            // && abs(checkDepths[0] - finalDepth) < stepDist
                            // && abs(checkDepths[1] - finalDepth) < stepDist
                            // && abs(checkDepths[2] - finalDepth) < stepDist
                        )
                        {
                                // if (!permaFail)
                                // {

                                output.color = SAMPLE_COLOR(i.uv + float2(finalOffset, 0.0), i.slice + side);
                                // output.color = half4(0,0,0,1);
                                output.depth = finalDepth01;
                                return output;

                                // }
                        }
                        // else
                        // {
                        //     permaFail = true;
                        //     output.color = half4(1,side,0,1);
                        // }
                    }

                }


                    output.color = half4(1,0,0,1);
                    output.depth = originalDepth01;
                    // return output;

                // if (!permaFail)
                // {
                //     return output;
                // }

                // output.depth = finalDepth01;

                // output.color = half4(0,0,1,1);
                // return output;

                // return 0;

                // now try a search if that didn't work
                for (int side = 0; side < 2; side++)
                // for (int side = 2; side >= 0; side--)
                // if (false)
                {
                    // linear steps
                    half offset = offsetBase * (1 - side * 2);
                    half finalMarchOffset = 0.0;
                    half currentDist = lkg_nearClip;
                    for (int j = 0; j < LINEAR_STEPS; j++)
                    {
                        currentDist += stepDist;
                        half marchOffset = UV_OFFSET_AT_DIST(currentDist, offset);
                        half marchDepth01 = SAMPLE_DEPTH(i.uv + float2(marchOffset, 0.0), i.slice + side);
                        half marchDepth = MyLinearEyeDepth(marchDepth01);
                        half marchAcc = abs(marchDepth - currentDist);

                        if (marchAcc < stepDist * 0.5)
                        {
                            // // return SAMPLE_COLOR(i.uv + float2(marchOffset, 0.0), i.slice + side);
                            // hit = true;
                            // finalDepth = marchDepth;
                            // finalMarchOffset = marchOffset;
                            // break;

                            // // return (SAMPLE_COLOR(i.uv, i.slice) + SAMPLE_COLOR(i.uv, i.slice + 1)) * 0.5;
                            // // half compDepth = SAMPLE_LINEAR_DEPTH(i.uv + float2(marchOffset, 0.0), i.slice + side);

                            output.color = SAMPLE_COLOR(i.uv + float2(marchOffset, 0.0), i.slice + side);
                            output.depth = marchDepth01;
                            return output;
                        }
                    }

                    // // binary steps
                    // // if (false)
                    // if (hit)
                    // {
                    //     half binSize = stepDist;
                    //     half binDist = currentDist;
                    //     finalDepth = currentDist - stepDist;
                    //     finalOffset = finalMarchOffset;
                    //     for (int j = 0; j < BINARY_STEPS; j++)
                    //     {
                    //         binSize *= 0.5;

                    //         half binOffset = UV_OFFSET_AT_DIST(binDist, offset);
                    //         half binDepth = SAMPLE_LINEAR_DEPTH(i.uv + float2(binOffset, 0.0), i.slice + side);
                    //         if (binDepth < binDist)
                    //         {
                    //             finalDepth = binDepth;
                    //             binDist -= binSize;
                    //             finalOffset = binOffset;
                    //         }
                    //         else
                    //         {
                    //             binDist += binSize;
                    //         }
                    //     }

                    //     // todo: check if finalDepth is "within" object by checking reverse depth?
                    //     // return SAMPLE_COLOR(i.uv + float2(finalOffset, 0.0), i.slice + side);
                    //     output.color = SAMPLE_COLOR(i.uv + float2(finalOffset, 0.0), i.slice + side);
                    //     output.depth = finalDepth;
                    //     return output;
                    //     // return SampleColorWithDof(i.uv + float2(finalOffset, 0.0), i.slice + side, finalDepth + stepDist);
                    // }
                }

                // if no hits, just return
                // return fixed4(0, 0, 0, 1);

                    // output.color = half4(0,1,0,1);
                    // output.depth = originalDepth01;
                    // return output;

                // return (SAMPLE_COLOR(i.uv, i.slice) + SAMPLE_COLOR(i.uv, i.slice + 1)) * 0.5;
                output.color = (SAMPLE_COLOR(i.uv, i.slice) + SAMPLE_COLOR(i.uv, i.slice + 1)) * 0.5;
                // output.color = half4(1,0,1,1);
                output.depth = max(originalDepth01, finalDepth01);
                return output;
                // return SampleColorWithDof(i.uv, i.slice, originalDepth + stepDist);
                // return SAMPLE_COLOR(i.uv, i.slice);
            }
            ENDCG
        }
    }
}
