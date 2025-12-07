# Notes for Looking Glass Unity Plugin Developer (4.0 / Unity 6.x / URP 17.x)

## 🔍 FINDINGS FROM CODE ANALYSIS

**Base-View Reconstruction Confirmed:**
- When `genViewsOn` is enabled: 24 views are rendered, 24 are synthesized via depth-based parallax reprojection
- When `genViewsOn` is disabled: All 48 views are rendered
- The `GenViews.shader` implements depth-based parallax reprojection using adjacent views and depth buffers
- Synthesis happens in a post-processing pass that samples from `colorRt` texture array

**Architecture:**
- Uses GPU instancing: `unity_StereoEyeIndex = inputInstanceID % LKG_VIEWCOUNT` (UnityInstancing.hlsl:220, 232)
- All views render to a 2D texture array (`colorRt`) with 48 slices (MultiviewData.cs:362-376)
- Each instance writes to a different slice via `SV_RenderTargetArrayIndex` (set by `UNITY_VERTEX_OUTPUT_STEREO` macro)
- Matrices stored in `unity_StereoMatrixVP_lkg[48]` arrays (UnityInput.hlsl)
- Custom shaders can access per-view matrices via `unity_StereoMatrixVP_lkg[unity_StereoEyeIndex]`
- Renderer uses `SetInstanceMultiplier(ViewCount)` to enable instancing (ScriptableRenderer.cs:1218-1219)
- View direction calculations (e.g., `GetWorldSpaceViewDir()`) automatically work per-instance

**Depth Handling:**
- Per-view depth buffers stored in texture arrays (`colorRt` has depth, `depthRt2` for synthesized views)
- GenViews shader uses depth for parallax reprojection

---

## 🚨 CRITICAL ISSUES (Must Resolve)

### 1. Custom Depth Writing (SV_Depth) Not Compatible with Multiview - IDENTIFIED ⚠️

**Problem:** Writing custom depth values using `SV_Depth` in shaders causes objects to disappear or fail to render in multiview.

**Root Cause Identified:**
- GPU instancing with texture arrays conflicts with per-pixel `SV_Depth` writes
- When using `SetInstanceMultiplier(ViewCount)`, each instance writes to a different slice of the texture array
- Custom depth values break the per-instance relationship between clip-space position and depth
- The depth buffer format (`GraphicsFormat.D32_SFloat_S8_UInt`) expects depth values consistent with per-instance clip-space positions
- Modifying depth per-pixel breaks this relationship, causing depth test failures or incorrect culling

**Evidence:**
- Writing `SV_Depth` in main pass: Object disappears from both scene view and quilt
- Writing `SV_Depth` in DepthOnly pass: Object disappears from quilt (works in scene view)
- `CameraMotionVectors.shader` only copies existing depth, doesn't modify it
- No examples in plugin codebase of custom depth modification with instancing

**Workaround - Parallax Mapping:**
- ✅ **Parallax mapping works perfectly** - offsets UVs based on view direction and height map
- No custom depth writing required
- Compatible with multiview instancing
- Provides 3D parallax effect visible in Looking Glass display
- Implementation uses `GetWorldSpaceViewDir()` which works per-instance automatically

**Outstanding Questions:**
- Is there any way to write custom depth per-pixel with multiview instancing?
- Would a separate render pass (without instancing) work for custom depth?
- Are there plans to support custom depth writing in future plugin versions?

**Impact:** Blocks per-pixel depth parallax effects, but parallax mapping provides a good alternative.

---

### 2. Matrix Access API
**Problem:** The matrices exposed in the public `HologramCamera` do NOT match the actual multiview matrices used for rendering.

**Findings from Code Analysis:**
- ✅ **Shader Access CONFIRMED:** Matrices are accessible in shaders via:
  - `unity_StereoMatrixVP_lkg[unity_StereoEyeIndex]` - access current view's matrix
  - `unity_StereoMatrixVP_lkg[viewIndex]` - access any specific view's matrix
  - `unity_MatrixVP` - automatically uses `unity_StereoMatrixVP_lkg[unity_StereoEyeIndex]` when `USING_STEREO_MATRICES` is defined
  - All matrices are in a `CBUFFER` called `UnityStereoViewBuffer` in `UnityInput.hlsl`
  - Available matrices: `unity_StereoMatrixVP_lkg`, `unity_StereoMatrixV_lkg`, `unity_StereoMatrixP_lkg`, `unity_StereoMatrixInvVP_lkg`, etc.

- ✅ **C# Access APPEARS POSSIBLE:** 
  - `Shader.PropertyToID("unity_StereoMatrixVP_lkg")` exists in `XRBuiltinShaderConstants.cs`
  - Should be accessible via: `Shader.GetGlobalMatrixArray(Shader.PropertyToID("unity_StereoMatrixVP_lkg"))`
  - However, this is not a documented/public API

**Outstanding Questions:**
- Is `Shader.GetGlobalMatrixArray()` the recommended/stable way to access these from C#?
- Is there a better public API (like `XRBuiltinShaderConstants.GetViewMatrix(int viewIndex)`)?
- For raymarching shaders that need all view matrices upfront, what's the recommended approach?
- Are these matrices guaranteed to be set before custom shaders execute?

**Impact:** Blocks raymarching and view-dependent effects that need C# access to matrices.

---

### 3. Custom Opaque Materials Not Appearing - SOLVED ✅
**Problem:** Custom opaque materials sometimes don't appear at all in multiview rendering.

**Solution:**
- ✅ **Stereo instancing setup required** (same as transparent shaders):
  - `#pragma multi_compile_instancing`
  - `#pragma multi_compile _ UNITY_STEREO_INSTANCING_ENABLED`
  - `UNITY_VERTEX_INPUT_INSTANCE_ID` in Attributes
  - `UNITY_VERTEX_OUTPUT_STEREO` in Varyings
  - `UNITY_SETUP_INSTANCE_ID(IN)` and `UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT)` in vertex shader
  - `UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN)` in fragment shader

- ✅ **DepthOnly pass required for opaque shaders:**
  - Opaque shaders MUST have a `DepthOnly` pass with `LightMode="DepthOnly"`
  - This pass writes depth values for proper sorting and culling
  - The depth pass also needs the same stereo instancing setup
  - Example:
    ```hlsl
    Pass
    {
        Name "DepthOnly"
        Tags { "LightMode"="DepthOnly" }
        ZWrite On
        ColorMask 0  // Only write depth, no color
        // ... same stereo instancing setup as main pass
    }
    ```

- ✅ **Standard opaque settings:**
  - `ZWrite On`, `ZTest LEqual` (not `ZWrite Off` / `ZTest Always`)
  - `Queue="Geometry"` or `Queue="Geometry+0"`
  - `LightMode="UniversalForwardOnly"` or `LightMode="UniversalForward"`

**Outstanding Questions:**
- Why is a DepthOnly pass required for opaque shaders but not transparent?
- Is this a multiview-specific requirement or a general URP requirement?
- Are there performance implications of having a DepthOnly pass?

**Impact:** Was blocking custom opaque rendering - now resolved.

---

### 4. RenderFeatures Not Executing Per-View
**Problem:** Custom `ScriptableRenderPass`/`RenderFeature` passes execute only once, not per-view.

**Findings from Code Analysis:**
- ✅ **RenderFeatures execute within LKG scope:** They run between `BeginRenderGraphLKGRendering` and `EndRenderGraphLKGRendering`
- ✅ **Instancing multiplier is already set:** `SetInstanceMultiplier(ViewCount)` is set globally in `BeginLKGPassData`
- ✅ **Keyword is already enabled:** `UNITY_STEREO_INSTANCING_ENABLED` is enabled globally
- ⚠️ **Shaders still need setup:** Fullscreen pass shaders need the same pragmas and macros as regular shaders:
  - `#pragma multi_compile_instancing`
  - `#pragma multi_compile _ UNITY_STEREO_INSTANCING_ENABLED`
  - `UNITY_VERTEX_OUTPUT_STEREO` in Varyings struct
  - `UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT)` in vertex shader
  - `UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN)` in fragment shader

**Outstanding Questions:**
- Do fullscreen passes need to render to a texture array instead of a regular texture?
- Are there any special considerations for `BlitPass` or `FullscreenPass` RenderFeatures?
- What render target should be used for multiview fullscreen passes?
- If a RenderFeature uses `Blit()` or `DrawMesh()`, does it automatically render to all views?
- Are custom pass injection points like `AfterRenderingTransparents`, `BeforeRenderingPostProcessing` supported?
- If a RenderFeature needs to access per-view data, how should it iterate through views?

**Impact:** Blocks custom post-processing and fullscreen effects.

---

## 📋 ARCHITECTURE & INTEGRATION - CONFIRMED ✅

### Render Pipeline Integration - CONFIRMED ✅

**Answer:** Plugin wraps URP's RenderGraph execution with multiview instancing.

**Details:**
- ✅ **Uses existing URP Renderer:** Plugin modifies `UniversalRendererRenderGraph.OnRecordRenderGraph()` (line 811, 820)
- ✅ **Wraps OnMainRendering:** `BeginRenderGraphLKGRendering()` and `EndRenderGraphLKGRendering()` wrap `OnMainRendering()` (UniversalRendererRenderGraph.cs:811-820)
- ✅ **All standard passes included:** ForwardOpaque, ForwardTransparent, and all standard URP passes execute within the multiview scope
- ✅ **RenderFeatures execute per-view:** Custom `ScriptableRenderPass`/`RenderFeature` passes execute within the LKG scope and automatically render to all views via instancing
- ✅ **No special registration needed:** Custom shaders just need stereo instancing setup (see "Custom Shaders Only Appearing in 1-2 Views" solution)

**Outstanding Questions:**
- Are there any URP passes that are excluded or bypassed in multiview mode?
- How do overlay passes (UI, etc.) work with multiview?

---

### View Synthesis & Custom Passes - CONFIRMED ✅

**Answer:** View synthesis happens after all rendering passes, using depth-based reprojection.

**Details:**
- ✅ **Synthesis happens post-rendering:** `GenViews.shader` executes in `EndRenderGraphLKGRendering()` after all standard passes (ScriptableRenderer.cs:1305-1326)
- ✅ **Uses depth-based parallax reprojection:** Samples adjacent rendered views and uses depth to determine correct color/depth for synthesized views
- ✅ **All passes included in synthesis:** Standard rendering passes (opaque, transparent) are included in the `colorRt` texture array that GenViews samples from
- ✅ **DOF works with synthesized views:** Plugin includes DOF shaders that process synthesized views (ScriptableRenderer.cs:1328-1349)

**Outstanding Questions:**
- Can custom RenderFeatures hook into the synthesis stage?
- Are there limitations for transparent/overlay passes in synthesized views?

---

### API & Integration - CONFIRMED ✅

**Answer:** No special opt-in required - shaders automatically participate if they have proper stereo instancing setup.

**Details:**
- ✅ **No registration needed:** Custom shaders automatically render to all views if they include stereo instancing setup
- ✅ **Matrix access in shaders:** `unity_StereoMatrixVP_lkg[unity_StereoEyeIndex]` or `unity_StereoMatrixVP_lkg[viewIndex]`
- ⚠️ **C# matrix access unclear:** `Shader.GetGlobalMatrixArray()` appears possible but not documented (see "Matrix Access API" issue)

**Outstanding Questions:**
- Is there a recommended/public API for C# matrix access?
- Will there be a public API like `GetViewMatrix(int viewIndex)`?

---

## 🔧 TECHNICAL DETAILS - CONFIRMED ✅

### RenderGraph Integration - CONFIRMED ✅

**Answer:** Uses GPU instancing, not separate RenderGraph invocations.

**Details:**
- ✅ **Uses instancing:** `SetInstanceMultiplier(ViewCount)` creates 48 instances in a single draw call (ScriptableRenderer.cs:1219)
- ✅ **Single RenderGraph execution:** RenderGraph executes once, but with instancing enabled globally
- ✅ **RenderFeatures supported:** Custom RenderFeatures execute within the instancing scope and automatically render to all views
- ✅ **Fullscreen passes work:** Fullscreen passes (like `RaymarchSDFFeature`) work if their shaders have stereo instancing setup

**RenderGraph Flow:**
1. `BeginRenderGraphLKGRendering()` - Sets up instancing, enables keyword, sets matrices
2. `OnMainRendering()` - All standard URP passes + RenderFeatures (execute with instancing)
3. `EndRenderGraphLKGRendering()` - View synthesis (GenViews), DOF, final compositing

**Outstanding Questions:**
- Are there any RenderGraph passes that don't work with multiview instancing?
- Can you provide a visual diagram of the flow?

---

### Matrix Details - CONFIRMED ✅

**Answer:** Matrices are generated from camera positions, one per view, before rendering.

**Details:**
- ✅ **Generated per-view:** Plugin iterates through all views and captures camera matrices (ScriptableRenderer.cs:1195-1202)
- ✅ **Set before rendering:** `XRBuiltinShaderConstants.LookingGlassMatrixUpdate()` sets all matrices globally before `OnMainRendering()` (ScriptableRenderer.cs:1205)
- ✅ **Used by RenderGraph:** These are the same matrices used by the internal RenderGraph via `unity_MatrixVP` (which redirects to `unity_StereoMatrixVP_lkg[unity_StereoEyeIndex]`)
- ✅ **Synthesized views:** GenViews shader uses depth-based reprojection, not separate matrices (synthesized views don't have their own camera matrices)

**Outstanding Questions:**
- Are synthesized views using any special post-processing beyond depth reprojection?

---

## 📚 DOCUMENTATION REQUESTS

**Questions:**
- Can you document how the 4.0 multiview pipeline differs from 3.x?
  - Which parts of URP are preserved?
  - Which parts are bypassed or overridden?
  - How motion vectors, depth, color buffers are handled?
  - How Gen-Views fills out all quilt views?
- Is the plugin expected to support heavy shader work (SDFs, raymarching, fullscreen compute-style passes)?
- Or is the recommendation to stay on 3.x for now?

---

## ✅ VERSION SUPPORT

**Questions:**
- Is Unity 6.2 + URP 17.x officially supported in Plugin 4.0?
- If not, what versions are tested?
- Is there known incompatibility with RenderGraph changes in 6.x?

---

## 🎯 REQUESTS FOR DELIVERABLES

1. **API Documentation:** Code example showing how to:
   - Access correct per-view matrices in a shader
   - Inject a ScriptableRenderPass that executes in all views

2. **Architecture Diagram:** Visual showing the 4.0 render pipeline flow

3. **Migration Guide:** From 3.x to 4.0 for projects with custom shaders/render features

---

## ✅ ANSWERED QUESTIONS (From Code Analysis)

### Shapes Plugin Only Appearing in 1-2 Views - PARTIALLY SOLVED ⚠️

**Problem:** Shapes plugin (by Freya Holmér) was only rendering in the first 1-2 views when Gen Views was enabled.

**Root Cause:**
- The Shapes plugin's generated shader files (154 shaders in `Assets/Shapes/Shaders/Generated Shaders/Resources/`) were missing the `UNITY_STEREO_INSTANCING_ENABLED` pragma
- The core shader files (`*.cginc`) already had all the stereo instancing macros (`UNITY_VERTEX_OUTPUT_STEREO`, etc.)
- However, the generated shader files only had `#pragma multi_compile_instancing` but were missing `#pragma multi_compile _ UNITY_STEREO_INSTANCING_ENABLED`

**Solution Applied:**
- ✅ **Added missing pragma to all 154 generated shader files**
- Added `#pragma multi_compile _ UNITY_STEREO_INSTANCING_ENABLED` after each `#pragma multi_compile_instancing` line
- Applied to all passes in each shader (Pass, DepthOnly, Picking, Selection)
- Used automated script to batch update all files
- ✅ **Updated ShapesRenderPass.cs to use correct render targets:**
  - Changed from `resourceData.cameraColor` to check `activeColorTexture` with fallback
  - Added depth attachment setup for proper depth testing
  - Added `AllowGlobalStateModification(true)` to ensure instancing state is preserved

**Files Fixed:**
- All shaders in `Assets/Shapes/Shaders/Generated Shaders/Resources/`
- Includes: Polygon, Triangle, Quad, Rect, Disc, Sphere, Cone, Cuboid, Torus, Line 2D, Line 3D, Polyline 2D, Regular Polygon, Texture
- All blend modes: Opaque, Transparent, Additive, Multiplicative, Screen, Subtractive, Darken, Lighten, ColorDodge, ColorBurn, LinearBurn
- `Assets/Shapes/Scripts/Runtime/Immediate Mode/ShapesRenderPass.cs`

**Outstanding Issue:**
- ⚠️ **Shapes render in Scene View but not in Looking Glass quilt/display**
- Render pass executes correctly (visible in Scene View)
- Shaders have proper stereo instancing setup
- Render pass uses correct render targets (`cameraColor` / `activeColorTexture`)
- **Possible causes:**
  - RenderGraph may not be correctly handling texture array render targets for custom RenderFeatures
  - `SetRenderAttachment` may not work correctly with texture arrays in multiview mode
  - Render pass may need to explicitly declare texture array usage
  - Draw calls may not be respecting the instancing multiplier in RenderGraph context

**Next Steps to Investigate:**
- Check if render pass needs to use different API for texture array render targets
- Verify that `SetInstanceMultiplier()` state is preserved in RenderGraph passes
- Check if render pass needs to explicitly bind the texture array slices
- Consider if RenderFeature needs special handling for Looking Glass multiview system

**Impact:** Shaders are fixed and should work, but RenderFeature integration with multiview texture arrays needs further investigation.

---

### Custom Depth Writing Investigation - COMPLETED ✅

**Investigation:** Attempted to implement per-pixel depth parallax using `SV_Depth` semantic.

**Findings:**
- ❌ **Custom depth writing incompatible with multiview instancing**
- Root cause: GPU instancing + texture arrays + custom depth creates conflicts
- Writing `SV_Depth` in main pass: Object disappears completely
- Writing `SV_Depth` in DepthOnly pass: Object disappears from quilt (works in scene view)
- Depth buffer expects values consistent with per-instance clip-space positions

**Solution Implemented:**
- ✅ **Parallax mapping** - UV offset based on view direction and height map
- Works perfectly with multiview instancing
- No custom depth writing required
- Provides visible 3D parallax effect on Looking Glass display
- Uses `GetWorldSpaceViewDir()` which automatically works per-instance

**Implementation Details:**
- View direction calculated in vertex shader using `GetWorldSpaceViewDir(worldPos)`
- Transformed to tangent space for parallax calculations
- Simple parallax mapping (no raymarching) for safety and performance
- Safety checks for NaN/Inf values and division by zero
- Properties: `_ParallaxScale` (intensity), `_ParallaxSteps` (unused, kept for compatibility)

**Code Location:** `Assets/Shaders/FBM2D_Smoke_Depth.shader`

---

### Custom Shaders Only Appearing in 1-2 Views - SOLVED ✅

**Answer:** Custom shaders need proper stereo instancing setup to render in all views.

**Solution:**
1. Add `#pragma multi_compile_instancing` and `#pragma multi_compile _ UNITY_STEREO_INSTANCING_ENABLED`
2. Add `UNITY_VERTEX_INPUT_INSTANCE_ID` to `Attributes` struct
3. Add `UNITY_VERTEX_OUTPUT_STEREO` to `Varyings` struct
4. Call `UNITY_SETUP_INSTANCE_ID(IN)` in vertex shader
5. Call `UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT)` in vertex shader
6. Call `UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN)` in fragment shader (if you need `unity_StereoEyeIndex`)

**Code Example:**
```hlsl
struct Attributes
{
    float3 positionOS : POSITION;
    float2 uv : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionHCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    UNITY_VERTEX_OUTPUT_STEREO
};

Varyings Vert(Attributes IN)
{
    Varyings OUT;
    UNITY_SETUP_INSTANCE_ID(IN);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
    // ... rest of vertex shader
}

half4 Frag(Varyings IN) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);
    // ... rest of fragment shader
    // Can now use unity_StereoEyeIndex if needed
    // Can access matrices via unity_StereoMatrixVP_lkg[unity_StereoEyeIndex]
}
```

**Why it works:** The renderer sets `SetInstanceMultiplier(ViewCount)` which creates multiple instances. The `UNITY_VERTEX_OUTPUT_STEREO` macro adds `stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex` which tells the GPU which slice of the texture array to render to.

---

### Custom Opaque Materials Not Appearing - SOLVED ✅

**Answer:** Opaque shaders need both stereo instancing setup AND a DepthOnly pass.

**Solution:**
1. **Stereo instancing setup** (same as transparent shaders - see above)
2. **DepthOnly pass required:**
   ```hlsl
   Pass
   {
       Name "DepthOnly"
       Tags { "LightMode"="DepthOnly" }
       ZWrite On
       ColorMask 0  // Only write depth, no color
       Cull Back

       HLSLPROGRAM
       #pragma vertex Vert
       #pragma fragment FragDepth
       #pragma multi_compile_instancing
       #pragma multi_compile _ UNITY_STEREO_INSTANCING_ENABLED

       // Same stereo instancing setup as main pass
       struct Attributes
       {
           float3 positionOS : POSITION;
           UNITY_VERTEX_INPUT_INSTANCE_ID
       };

       struct Varyings
       {
           float4 positionHCS : SV_POSITION;
           UNITY_VERTEX_OUTPUT_STEREO
       };

       Varyings Vert(Attributes IN)
       {
           Varyings OUT;
           UNITY_SETUP_INSTANCE_ID(IN);
           UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
           float3 worldPos = TransformObjectToWorld(IN.positionOS);
           OUT.positionHCS = TransformWorldToHClip(worldPos);
           return OUT;
       }

       half4 FragDepth(Varyings IN) : SV_Target
       {
           UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);
           return 0;  // ColorMask 0 means this won't be written anyway
       }
       ENDHLSL
   }
   ```

3. **Standard opaque settings:**
   - `ZWrite On`, `ZTest LEqual`
   - `Queue="Geometry"` or `Queue="Geometry+0"`
   - `LightMode="UniversalForwardOnly"` or `LightMode="UniversalForward"`

**Why it works:** The depth prepass filters objects by looking for a `DepthOnly` pass. Without it, opaque objects may be excluded from rendering. The depth pass also needs stereo instancing to write depth for all views correctly.

---

### Base-View Reconstruction - CONFIRMED ✅

**Answer:** Yes, 4.0 uses base-view + depth-based reprojection.

**Details:**
- When `genViewsOn` is true: 24 views are rendered, 24 are synthesized
- When `genViewsOn` is false: All 48 views are rendered
- Synthesis uses `GenViews.shader` which performs depth-based parallax reprojection
- The shader samples adjacent views and uses depth to determine correct color/depth for synthesized views

---

### Architecture - CONFIRMED ✅

**Answer:** Uses GPU instancing with texture arrays.

**Details:**
- All views render to a 2D texture array (`colorRt`) with 48 slices
- Uses `unity_StereoEyeIndex = inputInstanceID % LKG_VIEWCOUNT` to determine view index
- Matrices stored in `unity_StereoMatrixVP_lkg[48]` arrays
- Renderer enables `UNITY_STEREO_INSTANCING_ENABLED` keyword and calls `SetInstanceMultiplier(ViewCount)` in `BeginLKGPassData`

---

### Depth Handling - CONFIRMED ✅

**Answer:** Per-view depth buffers stored in texture arrays.

**Details:**
- `colorRt` has depth buffer attached (GraphicsFormat.D32_SFloat_S8_UInt)
- `depthRt2` stores depth for synthesized views (ViewCount - 1 slices)
- GenViews shader uses depth for parallax reprojection

**Important Limitation:**
- ⚠️ **Custom depth writing (`SV_Depth`) is NOT compatible with multiview instancing**
- Writing custom depth per-pixel breaks the per-instance depth relationship
- Use parallax mapping instead for depth-based visual effects

---

### Path-Traced Clouds Shader - SOLVED ✅

**Problem:** Implementing a path-traced volumetric clouds shader (ported from GLSL) that works correctly with Looking Glass multiview rendering.

**Solution:**
- ✅ **Per-view camera position access:** Use `unity_StereoWorldSpaceCameraPos_lkg[unity_StereoEyeIndex]` for correct camera positions per-view
- ✅ **Ray direction calculation:** Calculate ray direction from camera to quad's world position using `normalize(quadWorldPos - rayOrigin)`
- ✅ **Stereo instancing setup:** Full stereo instancing macros required (same as other custom shaders)
- ✅ **Transparent rendering:** Use `Queue="Transparent"` with `Blend SrcAlpha OneMinusSrcAlpha` for volumetric effects

**Implementation Details:**
- Ray origin: `unity_StereoWorldSpaceCameraPos_lkg[unity_StereoEyeIndex]` (per-view camera position)
- Ray direction: `normalize(IN.worldPos - rayOrigin)` (from camera to quad world position)
- Scene SDF: Sphere with FBM noise for cloud density
- Volume raymarching: Accumulates density along ray with proper alpha blending
- Noise texture: Requires 256x256 texture with `Wrap Mode="Repeat"` and proper filtering

**Key Finding:**
- Using the quad's world position directly to calculate ray direction works correctly for multiview
- `GetWorldSpaceViewDir()` can also be used but requires negation: `-normalize(GetWorldSpaceViewDir(worldPos))`
- Screen-space UV reconstruction works but is more complex; direct world-space calculation is simpler and correct

**Shader Properties:**
- `_NoiseTex`: 256x256 noise texture (must be Repeat wrap mode)
- `_TimeScale`: Animation speed
- `_MaxSteps`: Raymarch steps (default 100)
- `_MarchSize`: Step size (default 0.08)
- `_SphereRadius`: Cloud sphere radius
- `_SphereCenter`: World-space position of cloud sphere
- `_FBMStrength`: Noise intensity multiplier
- `_CloudColor`: Cloud color
- `_SkyColor`: Background/sky color

**Code Location:** `Assets/Shaders/PathtracedClouds.shader`

**Impact:** Successfully ported GLSL raymarching shader to URP with full multiview support. Works correctly in both Scene view and Looking Glass display.

