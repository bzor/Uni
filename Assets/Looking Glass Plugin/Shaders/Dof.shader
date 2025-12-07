Shader "Looking Glass/Dof"
{
    Properties
    {
        _MainTexArray ("Color Texture Array", 2DArray) = "white" {}
        _DepthTexArray ("Depth Texture Array", 2DArray) = "white" {}
    }
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            // Stencil
            // {
            //     Ref 1           // Reference value to test against
            //     Comp Equal      // Pass if stencil == Ref
            //     Pass Keep       // Keep stencil value unchanged
            // }

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

            SamplerState sampler_point_clamp;
            SamplerState sampler_linear_clamp;

            #pragma multi_compile _ USE_CAMERA_DEPTH

            #ifdef USE_CAMERA_DEPTH
            #define _DepthTexArray _CameraDepthTexture
            #endif

            // UNITY_DECLARE_TEX2DARRAY(Mask1);
            // UNITY_DECLARE_TEX2DARRAY(Mask2);

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
            uniform half aspect;
            uniform float2 inverseVP;
            uniform half lkg_dofStrength;
            uniform half lkg_dofVertical;

            half MyLinearEyeDepth(half rawdepth) {
#if UNITY_REVERSED_Z
                return 1.0 / (lkg_linearDepthParamsReversedZ.z * rawdepth + lkg_linearDepthParamsReversedZ.w);
#else
                return 1.0 / (lkg_linearDepthParams.z * rawdepth + lkg_linearDepthParams.w);
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
            #define SAMPLE_LINEAR_DEPTH(uv, view) (MyLinearEyeDepth(UNITY_SAMPLE_TEX2DARRAY_SAMPLER_LOD(_DepthTexArray, _point_clamp, float3(uv, view), 0.0).r))
            #define SAMPLE_DEPTH(uv, view) (UNITY_SAMPLE_TEX2DARRAY_SAMPLER_LOD(_DepthTexArray, _point_clamp, float3(uv, view), 0.0).r)

            // get color for channel
            // col[i] = UNITY_SAMPLE_TEX2DARRAY_LOD(
            //     _MainTexArray,
            //     float3(uv.x + xOffset, uv.y, viewLoop[bestSide]),
            //     0.0
            // )[i];
            #define SAMPLE_COLOR(uv, view) (UNITY_SAMPLE_TEX2DARRAY_SAMPLER_LOD(_MainTexArray, _point_clamp, float3(uv, view), 0.0))

            static const half2 dofCone[] = {

                // 6 point
                // half2(-0.5, -0.866),
                // half2(-0.5, 0.866),
                // half2(1.0, 0.0),
                // half2(-0.866, -0.5),
                // half2(0.866, -0.5),
                // half2(0.0, 1.0),

                // 5 point
                half2(0.0, 1.0),
                half2(0.6, -0.8),
                half2(-0.95, 0.3),
                half2(0.95, 0.3),
                half2(-0.6, -0.8),
            };




// including this modified fxaa shader
/**
Basic FXAA implementation based on the code on geeks3d.com with the
modification that the texture2DLod stuff was removed since it's
unsupported by WebGL.

--

From:
https://github.com/mitsuhiko/webgl-meincraft

Copyright (c) 2011 by Armin Ronacher.

Some rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are
met:

    * Redistributions of source code must retain the above copyright
      notice, this list of conditions and the following disclaimer.

    * Redistributions in binary form must reproduce the above
      copyright notice, this list of conditions and the following
      disclaimer in the documentation and/or other materials provided
      with the distribution.

    * The names of the contributors may not be used to endorse or
      promote products derived from this software without specific
      prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
"AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

#ifndef FXAA_REDUCE_MIN
    #define FXAA_REDUCE_MIN   (1.0/ 128.0)
#endif
#ifndef FXAA_REDUCE_MUL
    #define FXAA_REDUCE_MUL   (1.0 / 8.0)
#endif
#ifndef FXAA_SPAN_MAX
    #define FXAA_SPAN_MAX     8.0
#endif

//optimized version for mobile, where dependent
//texture reads can be a bottleneck
float4 fxaa(float2 uv, float2 inverseVP,
            // float2 v_rgbNW, float2 v_rgbNE,
            // float2 v_rgbSW, float2 v_rgbSE,
            // float2 v_rgbM,
            float view) {
    float4 color;
    // float2 inverseVP = float2(1.0 / resolution.x, 1.0 / resolution.y);
    // float3 rgbNW = texture2D(tex, v_rgbNW).xyz;
    // float3 rgbNE = texture2D(tex, v_rgbNE).xyz;
    // float3 rgbSW = texture2D(tex, v_rgbSW).xyz;
    // float3 rgbSE = texture2D(tex, v_rgbSE).xyz;
    // float4 texColor = texture2D(tex, v_rgbM);

    // float3 rgbNW = SAMPLE_COLOR(v_rgbNW, view).xyz;
    // float3 rgbNE = SAMPLE_COLOR(v_rgbNE, view).xyz;
    // float3 rgbSW = SAMPLE_COLOR(v_rgbSW, view).xyz;
    // float3 rgbSE = SAMPLE_COLOR(v_rgbSE, view).xyz;
    // float4 texColor = SAMPLE_COLOR(v_rgbM, view);

    float testOff = 0.49;
    testOff = 0;
    // uv -= 0.5 * inverseVP;

    float2 v_rgbNW = uv + float2(-1, 1) * inverseVP * testOff;
    float2 v_rgbNE = uv + float2(1, 1) * inverseVP * testOff;
    float2 v_rgbSW = uv + float2(-1, -1) * inverseVP * testOff;
    float2 v_rgbSE = uv + float2(1, -1) * inverseVP * testOff;
    float2 v_rgbM = uv;

    float3 rgbNW = SAMPLE_COLOR(v_rgbNW, view).xyz;
    float3 rgbNE = SAMPLE_COLOR(v_rgbNE, view).xyz;
    float3 rgbSW = SAMPLE_COLOR(v_rgbSW, view).xyz;
    float3 rgbSE = SAMPLE_COLOR(v_rgbSE, view).xyz;
    float4 texColor = SAMPLE_COLOR(v_rgbM, view);

    float3 rgbM  = texColor.xyz;
    float3 luma = float3(0.299, 0.587, 0.114);
    float lumaNW = dot(rgbNW, luma);
    float lumaNE = dot(rgbNE, luma);
    float lumaSW = dot(rgbSW, luma);
    float lumaSE = dot(rgbSE, luma);
    float lumaM  = dot(rgbM,  luma);
    float lumaMin = min(lumaM, min(min(lumaNW, lumaNE), min(lumaSW, lumaSE)));
    float lumaMax = max(lumaM, max(max(lumaNW, lumaNE), max(lumaSW, lumaSE)));

    float2 dir;
    dir.x = -((lumaNW + lumaNE) - (lumaSW + lumaSE));
    dir.y =  ((lumaNW + lumaSW) - (lumaNE + lumaSE));

    float dirReduce = max((lumaNW + lumaNE + lumaSW + lumaSE) *
                          (0.25 * FXAA_REDUCE_MUL), FXAA_REDUCE_MIN);

    float rcpDirMin = 1.0 / (min(abs(dir.x), abs(dir.y)) + dirReduce);
    dir = min(float2(FXAA_SPAN_MAX, FXAA_SPAN_MAX),
              max(float2(-FXAA_SPAN_MAX, -FXAA_SPAN_MAX),
              dir * rcpDirMin)) * inverseVP * testOff;

    float3 rgbA = 0.5 * (
        // texture2D(tex, fragCoord * inverseVP + dir * (1.0 / 3.0 - 0.5)).xyz +
        // texture2D(tex, fragCoord * inverseVP + dir * (2.0 / 3.0 - 0.5)).xyz
        SAMPLE_COLOR(uv + dir * (1.0 / 3.0 - 0.5), view).xyz +
        SAMPLE_COLOR(uv + dir * (2.0 / 3.0 - 0.5), view).xyz
    );
    float3 rgbB = rgbA * 0.5 + 0.25 * (
        // texture2D(tex, fragCoord * inverseVP + dir * -0.5).xyz +
        // texture2D(tex, fragCoord * inverseVP + dir * 0.5).xyz
        SAMPLE_COLOR(uv + dir * -0.5, view).xyz +
        SAMPLE_COLOR(uv + dir * 0.5, view).xyz
    );

    float lumaB = dot(rgbB, luma);
    if ((lumaB < lumaMin) || (lumaB > lumaMax))
        color = float4(rgbA, texColor.a);
    else
        color = float4(rgbB, texColor.a);
    return color;
}

// end included modified fxaa shader






            // #define DOF_BLUR_AMT(depth) ((lkg_focalDist - depth) / (lkg_focalDist - (depth > lkg_focalDist ? lkg_farClip : lkg_nearClip)))
            #define DOF_BLUR_AMT(depth) (abs(lkg_focalDist - depth) / lkg_maxOffset)

            half4 frag (v2f input) : SV_Target
            {
                #define LINEAR_STEPS 40
                #define DOF_COUNT 5

                half totalDist = lkg_farClip - lkg_nearClip;
                half stepDist = totalDist / (LINEAR_STEPS);

                int view = input.slice;
                half originalDepth01 = SAMPLE_DEPTH(input.uv, view);
                half originalDepth = MyLinearEyeDepth(originalDepth01);

                half4 originalCol = SAMPLE_COLOR(input.uv, view);

                if (originalDepth01 == 0)
                    return originalCol;

                half4 col = originalCol;
                half blur = DOF_BLUR_AMT(originalDepth) * 1.2 - 0.2;
                half total = 1.0;

                if (blur < 0.2)
                {
                    // originalCol = fxaa(input.uv, inverseVP, view);
                    return originalCol;
                }

                for (int i = 0; i < DOF_COUNT; i++)
                {
                    half2 offset = dofCone[i] * half2(1.0, aspect) * half2(lkg_dofStrength, lkg_dofStrength * lkg_dofVertical) * blur * 0.01;
                    half curDepth = SAMPLE_LINEAR_DEPTH(input.uv + offset, view);
                    if (curDepth > originalDepth * 0.98)
                    {
                        col += SAMPLE_COLOR(input.uv + offset, view);
                        total += 1.0;
                    }
                }
                return col / total;
            }
            ENDCG
        }
    }
}
