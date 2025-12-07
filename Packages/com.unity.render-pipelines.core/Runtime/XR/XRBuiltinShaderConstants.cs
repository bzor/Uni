using UnityEngine.Rendering;

namespace UnityEngine.Experimental.Rendering
{
    /// <summary>
    /// Helper static class used by render pipelines to setup stereo constants accessed by builtin shaders.
    /// </summary>
    public static class XRBuiltinShaderConstants
    {
        /// <summary>
        /// Cached unique id for unity_StereoCameraProjection
        /// </summary>
        static public readonly int unity_StereoCameraProjection = Shader.PropertyToID("unity_StereoCameraProjection_lkg");

        /// <summary>
        /// Cached unique id for unity_StereoCameraInvProjection
        /// </summary>
        static public readonly int unity_StereoCameraInvProjection = Shader.PropertyToID("unity_StereoCameraInvProjection_lkg");

        /// <summary>
        /// Cached unique id for unity_StereoMatrixV
        /// </summary>
        static public readonly int unity_StereoMatrixV = Shader.PropertyToID("unity_StereoMatrixV_lkg");

        /// <summary>
        /// Cached unique id for unity_StereoMatrixInvV
        /// </summary>
        static public readonly int unity_StereoMatrixInvV = Shader.PropertyToID("unity_StereoMatrixInvV_lkg");

        /// <summary>
        /// Cached unique id for unity_StereoMatrixP
        /// </summary>
        static public readonly int unity_StereoMatrixP = Shader.PropertyToID("unity_StereoMatrixP_lkg");

        /// <summary>
        /// Cached unique id for unity_StereoMatrixInvP
        /// </summary>
        static public readonly int unity_StereoMatrixInvP = Shader.PropertyToID("unity_StereoMatrixInvP_lkg");

        /// <summary>
        /// Cached unique id for unity_StereoMatrixVP
        /// </summary>
        static public readonly int unity_StereoMatrixVP = Shader.PropertyToID("unity_StereoMatrixVP_lkg");

        /// <summary>
        /// Cached unique id for unity_StereoMatrixInvVP
        /// </summary>
        static public readonly int unity_StereoMatrixInvVP = Shader.PropertyToID("unity_StereoMatrixInvVP_lkg");

        /// <summary>
        /// Cached unique id for unity_StereoWorldSpaceCameraPos
        /// </summary>
        static public readonly int unity_StereoWorldSpaceCameraPos = Shader.PropertyToID("unity_StereoWorldSpaceCameraPos_lkg");

        // Pre-allocate arrays to avoid GC
        // static Matrix4x4[] s_cameraProjMatrix = new Matrix4x4[2];
        // static Matrix4x4[] s_invCameraProjMatrix = new Matrix4x4[2];
        // static Matrix4x4[] s_viewMatrix = new Matrix4x4[2];
        // static Matrix4x4[] s_invViewMatrix = new Matrix4x4[2];
        // static Matrix4x4[] s_projMatrix = new Matrix4x4[2];
        // static Matrix4x4[] s_invProjMatrix = new Matrix4x4[2];
        // static Matrix4x4[] s_viewProjMatrix = new Matrix4x4[2];
        // static Matrix4x4[] s_invViewProjMatrix = new Matrix4x4[2];
        // static Vector4[] s_worldSpaceCameraPos = new Vector4[2];

        public static Matrix4x4[] s_cameraProjMatrix = new Matrix4x4[LookingGlassSettings.VIEWCOUNTn];
        public static Matrix4x4[] s_invCameraProjMatrix = new Matrix4x4[LookingGlassSettings.VIEWCOUNTn];
        public static Matrix4x4[] s_viewMatrix = new Matrix4x4[LookingGlassSettings.VIEWCOUNTn];
        public static Matrix4x4[] s_invViewMatrix = new Matrix4x4[LookingGlassSettings.VIEWCOUNTn];
        public static Matrix4x4[] s_projMatrix = new Matrix4x4[LookingGlassSettings.VIEWCOUNTn];
        public static Matrix4x4[] s_invProjMatrix = new Matrix4x4[LookingGlassSettings.VIEWCOUNTn];
        public static Matrix4x4[] s_viewProjMatrix = new Matrix4x4[LookingGlassSettings.VIEWCOUNTn];
        public static Matrix4x4[] s_invViewProjMatrix = new Matrix4x4[LookingGlassSettings.VIEWCOUNTn];
        public static Vector4[] s_worldSpaceCameraPos = new Vector4[LookingGlassSettings.VIEWCOUNTn];

        /// <summary>
        /// Update the shader constant data used by the C++ builtin renderer.
        /// </summary>
        /// <param name="viewMatrix"> The new view matrix that XR shaders constant should update to use. </param>
        /// <param name="projMatrix"> The new projection matrix that XR shaders constant should update to use. </param>
        /// <param name="renderIntoTexture"> Determines the yflip state for the projection matrix. </param>
        /// <param name="viewIndex"> Index of the XR shader constant to update. </param>
        public static void UpdateBuiltinShaderConstants(Matrix4x4 viewMatrix, Matrix4x4 projMatrix, bool renderIntoTexture, int viewIndex)
        {
#if ENABLE_VR && ENABLE_XR_MODULE
            var gpuProjMatrix = GL.GetGPUProjectionMatrix(projMatrix, renderIntoTexture);
            var gpuViewProjMatrix = gpuProjMatrix * viewMatrix;

            s_cameraProjMatrix[viewIndex] = projMatrix;
            s_projMatrix[viewIndex] = gpuProjMatrix;
            s_viewMatrix[viewIndex] = viewMatrix;
            Matrix4x4.Inverse3DAffine(viewMatrix, ref s_invViewMatrix[viewIndex]);
            s_viewProjMatrix[viewIndex] = gpuViewProjMatrix;
            s_invCameraProjMatrix[viewIndex] = Matrix4x4.Inverse(projMatrix);
            s_invProjMatrix[viewIndex] = Matrix4x4.Inverse(gpuProjMatrix);
            s_invViewProjMatrix[viewIndex] = Matrix4x4.Inverse(gpuViewProjMatrix);
            s_worldSpaceCameraPos[viewIndex] = s_invViewMatrix[viewIndex].GetColumn(3);
#endif
        }

        /// <summary>
        /// Bind the shader constants used by the C++ builtin renderer via a command buffer. `UpdateBuiltinShaderConstants` should be called before to update the constants.
        /// This is required to maintain compatibility with legacy code and shaders.
        /// </summary>
        /// <param name="cmd"> Commandbuffer on which to set XR shader constants. </param>
        public static void SetBuiltinShaderConstants(CommandBuffer cmd)
        {
#if ENABLE_VR && ENABLE_XR_MODULE
            cmd.SetGlobalMatrixArray(unity_StereoCameraProjection, s_cameraProjMatrix);
            cmd.SetGlobalMatrixArray(unity_StereoCameraInvProjection, s_invCameraProjMatrix);
            cmd.SetGlobalMatrixArray(unity_StereoMatrixV, s_viewMatrix);
            cmd.SetGlobalMatrixArray(unity_StereoMatrixInvV, s_invViewMatrix);
            cmd.SetGlobalMatrixArray(unity_StereoMatrixP, s_projMatrix);
            cmd.SetGlobalMatrixArray(unity_StereoMatrixInvP, s_invProjMatrix);
            cmd.SetGlobalMatrixArray(unity_StereoMatrixVP, s_viewProjMatrix);
            cmd.SetGlobalMatrixArray(unity_StereoMatrixInvVP, s_invViewProjMatrix);
            cmd.SetGlobalVectorArray(unity_StereoWorldSpaceCameraPos, s_worldSpaceCameraPos);
#endif
        }

        /// <summary>
        /// Bind the shader constants used by the C++ builtin renderer via a raster command buffer. `UpdateBuiltinShaderConstants` should be called before to update the constants.
        /// This is required to maintain compatibility with legacy code and shaders.
        /// </summary>
        /// <param name="cmd"> RasterCommandbuffer on which to set XR shader constants. </param>
        public static void SetBuiltinShaderConstants(RasterCommandBuffer cmd)
        {
            SetBuiltinShaderConstants(cmd.m_WrappedCommandBuffer);
        }

        /// <summary>
        /// Update and bind shader constants used by the C++ builtin renderer given the XRPass. For better control of setting up builtin shader constants, see `UpdateBuiltinShaderConstants`
        /// and `SetBuiltinShaderConstants` which do the same logic but could take in custom projection and view matricies instead.
        /// This is required to maintain compatibility with legacy code and shaders.
        /// </summary>
        /// <param name="xrPass"> The new XRPass that XR shader constants should update to use. </param>
        /// <param name="cmd"> CommandBuffer on which to set XR shader constants. </param>
        /// <param name="renderIntoTexture"> Determines the yflip state for the projection matrix. </param>
        public static void Update(XRPass xrPass, CommandBuffer cmd, bool renderIntoTexture)
        {
#if ENABLE_VR && ENABLE_XR_MODULE
            if (xrPass.enabled)
            {
                cmd.SetViewProjectionMatrices(xrPass.GetViewMatrix(), xrPass.GetProjMatrix());

                if (xrPass.singlePassEnabled)
                {
                    for (int viewIndex = 0; viewIndex < 2; ++viewIndex)
                    {
                        s_cameraProjMatrix[viewIndex]     = xrPass.GetProjMatrix(viewIndex);
                        s_viewMatrix[viewIndex]           = xrPass.GetViewMatrix(viewIndex);
                        s_projMatrix[viewIndex]           = GL.GetGPUProjectionMatrix(s_cameraProjMatrix[viewIndex], renderIntoTexture);
                        s_viewProjMatrix[viewIndex]       = s_projMatrix[viewIndex] * s_viewMatrix[viewIndex];

                        s_invCameraProjMatrix[viewIndex]  = Matrix4x4.Inverse(s_cameraProjMatrix[viewIndex]);
                        Matrix4x4.Inverse3DAffine(s_viewMatrix[viewIndex], ref s_invViewMatrix[viewIndex]);
                        s_invProjMatrix[viewIndex]        = Matrix4x4.Inverse(s_projMatrix[viewIndex]);
                        s_invViewProjMatrix[viewIndex]    = Matrix4x4.Inverse(s_viewProjMatrix[viewIndex]);

                        s_worldSpaceCameraPos[viewIndex]  = s_invViewMatrix[viewIndex].GetColumn(3);
                    }

                    cmd.SetGlobalMatrixArray(unity_StereoCameraProjection, s_cameraProjMatrix);
                    cmd.SetGlobalMatrixArray(unity_StereoCameraInvProjection, s_invCameraProjMatrix);
                    cmd.SetGlobalMatrixArray(unity_StereoMatrixV, s_viewMatrix);
                    cmd.SetGlobalMatrixArray(unity_StereoMatrixInvV, s_invViewMatrix);
                    cmd.SetGlobalMatrixArray(unity_StereoMatrixP, s_projMatrix);
                    cmd.SetGlobalMatrixArray(unity_StereoMatrixInvP, s_invProjMatrix);
                    cmd.SetGlobalMatrixArray(unity_StereoMatrixVP, s_viewProjMatrix);
                    cmd.SetGlobalMatrixArray(unity_StereoMatrixInvVP, s_invViewProjMatrix);
                    cmd.SetGlobalVectorArray(unity_StereoWorldSpaceCameraPos, s_worldSpaceCameraPos);
                }
            }
#endif
        }

        // lkg edit
        // todo: this shouldn't just pass in the view and proj mats for 2 views but pass in all

        static Matrix4x4[] projMats = new Matrix4x4[LookingGlassSettings.VIEWCOUNTn];
        static Matrix4x4[] viewMats = new Matrix4x4[LookingGlassSettings.VIEWCOUNTn];

        public static void LookingGlassMatrixUpdate(ref Matrix4x4[] _viewMats, ref Matrix4x4[] _projMats)
        {
            for (int viewIndex = 0; viewIndex < LookingGlassSettings.VIEWCOUNTn; viewIndex++)
            {
                projMats[viewIndex] = _projMats[viewIndex];
                viewMats[viewIndex] = _viewMats[viewIndex];

                // s_cameraProjMatrix[viewIndex]     = xrPass.GetProjMatrix(viewIndex);
                // s_viewMatrix[viewIndex]           = xrPass.GetViewMatrix(viewIndex);
                s_cameraProjMatrix[viewIndex]     = projMats[viewIndex];
                s_viewMatrix[viewIndex]           = viewMats[viewIndex];
                s_projMatrix[viewIndex]           = GL.GetGPUProjectionMatrix(s_cameraProjMatrix[viewIndex], true);
                s_viewProjMatrix[viewIndex]       = s_projMatrix[viewIndex] * s_viewMatrix[viewIndex];

                s_invCameraProjMatrix[viewIndex]  = Matrix4x4.Inverse(s_cameraProjMatrix[viewIndex]);
                Matrix4x4.Inverse3DAffine(s_viewMatrix[viewIndex], ref s_invViewMatrix[viewIndex]);
                s_invProjMatrix[viewIndex]        = Matrix4x4.Inverse(s_projMatrix[viewIndex]);
                s_invViewProjMatrix[viewIndex]    = Matrix4x4.Inverse(s_viewProjMatrix[viewIndex]);

                s_worldSpaceCameraPos[viewIndex]  = s_invViewMatrix[viewIndex].GetColumn(3);
            }
        }

        // for inverted culling
        public static void LookingGlassMatrixFlip(Matrix4x4 muls)
        {
            for (int viewIndex = 0; viewIndex < LookingGlassSettings.VIEWCOUNTn; viewIndex++)
            {
                // s_cameraProjMatrix[viewIndex]     = xrPass.GetProjMatrix(viewIndex);
                // s_viewMatrix[viewIndex]           = xrPass.GetViewMatrix(viewIndex);
                s_cameraProjMatrix[viewIndex]     = projMats[viewIndex];
                s_viewMatrix[viewIndex]           = viewMats[viewIndex];

                // if (flipped)
                // {
                //     // s_cameraProjMatrix[viewIndex].m11 *= -1f;
                //     // s_cameraProjMatrix[viewIndex].m13 *= -1f;
                //     s_cameraProjMatrix[viewIndex].m32 *= -1f;
                // }
                for (int i = 0; i < s_cameraProjMatrix.Length; i++)
                {
                    s_cameraProjMatrix[viewIndex][i] *= muls[i];
                }

                s_projMatrix[viewIndex]           = GL.GetGPUProjectionMatrix(s_cameraProjMatrix[viewIndex], true);
                // s_projMatrix[viewIndex]           = GL.GetGPUProjectionMatrix(s_cameraProjMatrix[viewIndex], !flipped);

                // if (flipped)
                // {
                //     // s_cameraProjMatrix[viewIndex].m11 *= -1f;
                //     // s_cameraProjMatrix[viewIndex].m13 *= -1f;

                //     s_projMatrix[viewIndex].m11 *= -1f;
                //     // s_projMatrix[viewIndex].m13 *= -1f;
                //     s_projMatrix[viewIndex].m31 *= -1f;
                // }

                s_viewProjMatrix[viewIndex]       = s_projMatrix[viewIndex] * s_viewMatrix[viewIndex];

                s_invCameraProjMatrix[viewIndex]  = Matrix4x4.Inverse(s_cameraProjMatrix[viewIndex]);
                Matrix4x4.Inverse3DAffine(s_viewMatrix[viewIndex], ref s_invViewMatrix[viewIndex]);
                s_invProjMatrix[viewIndex]        = Matrix4x4.Inverse(s_projMatrix[viewIndex]);
                s_invViewProjMatrix[viewIndex]    = Matrix4x4.Inverse(s_viewProjMatrix[viewIndex]);

                s_worldSpaceCameraPos[viewIndex]  = s_invViewMatrix[viewIndex].GetColumn(3);
            }
        }

        public static void LookingGlassUpdate(RasterCommandBuffer cmd)
        {
            cmd.SetGlobalMatrixArray(unity_StereoCameraProjection, s_cameraProjMatrix);
            cmd.SetGlobalMatrixArray(unity_StereoCameraInvProjection, s_invCameraProjMatrix);
            cmd.SetGlobalMatrixArray(unity_StereoMatrixV, s_viewMatrix);
            cmd.SetGlobalMatrixArray(unity_StereoMatrixInvV, s_invViewMatrix);
            cmd.SetGlobalMatrixArray(unity_StereoMatrixP, s_projMatrix);
            cmd.SetGlobalMatrixArray(unity_StereoMatrixInvP, s_invProjMatrix);
            cmd.SetGlobalMatrixArray(unity_StereoMatrixVP, s_viewProjMatrix);
            cmd.SetGlobalMatrixArray(unity_StereoMatrixInvVP, s_invViewProjMatrix);
            cmd.SetGlobalVectorArray(unity_StereoWorldSpaceCameraPos, s_worldSpaceCameraPos);
        }
    }
}
