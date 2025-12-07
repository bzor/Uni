#ifndef UNIVERSAL_DOTS_PRAGMAS_INCLUDED
#define UNIVERSAL_DOTS_PRAGMAS_INCLUDED

// lkg edit
// we covering our bases with this one
#ifndef LKG_PRAGMAS
#define LKG_PRAGMAS
#pragma multi_compile _ UNITY_STEREO_INSTANCING_ENABLED
#endif

#ifndef HAVE_VFX_MODIFICATION
    #pragma multi_compile _ DOTS_INSTANCING_ON
    #if UNITY_PLATFORM_ANDROID || (UNITY_PLATFORM_WEBGL && !SHADER_API_WEBGPU) || UNITY_PLATFORM_UWP
        #pragma target 3.5 DOTS_INSTANCING_ON
    #else
        #pragma target 4.5 DOTS_INSTANCING_ON
    #endif
#endif // HAVE_VFX_MODIFICATION

#endif // UNIVERSAL_DOTS_PRAGMAS_INCLUDED
