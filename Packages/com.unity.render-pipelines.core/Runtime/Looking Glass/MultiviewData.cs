using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Callbacks;
using System.IO;
#endif

public static class LookingGlassSettings
{
    // view count is defined in two places in entire project, here and in LookingGlassSettings.hlsl
    public static readonly int VIEWCOUNTn = 48;

    public static bool IsViewCountOrHalf(int n)
    {
        return n == VIEWCOUNTn || n == VIEWCOUNTn / 2;
    }

    [Flags]
    public enum BridgeLoggingFlags : uint {
        None = 0,
        Timing = 1,
        Messages = 2,
        Responses = 4,

        All = 2147483647
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(MultiviewData))]
public class MultiviewDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.LabelField("Plugin Version", MultiviewData.pluginVersion);
        EditorGUILayout.LabelField("Unity Version Target", MultiviewData.unityVersionTarget);

        base.OnInspectorGUI();

        MultiviewData data = (MultiviewData)target;

        // display quilt suffix so user can see it
        EditorGUILayout.LabelField("Quilt Suffix", data.QuiltSuffix);

        // add button to copy quilt suffix to clipboard
        if (GUILayout.Button("Copy Quilt Suffix to Clipboard"))
        {
            EditorGUIUtility.systemCopyBuffer = data.QuiltSuffix;
            Debug.Log($"Copied '{data.QuiltSuffix}' to clipboard.");
        }

        // add button to save quilt output to png
        if (GUILayout.Button("Save Quilt Output to PNG"))
        {
            data.SaveQuiltOutputToPNG();
        }
    }
}
#endif

[CreateAssetMenu(fileName = "Multiview Data", menuName = "Looking Glass/Multiview Data")]
public class MultiviewData : ScriptableObject
{
    public const string pluginVersion = "4.0.1-alpha";
    public const string unityVersionTarget = "6000.0.40f1";
    public RawCalibration cal;
    public RenderTexture colorRt;
    public RenderTexture colorRt2;
    public RenderTexture colorRtFinal;
    public RenderTexture colorRt2Final;
    public RenderTexture depthRt2;
    public RenderTexture lenticularRt;
    public Material[] lenticularMats;
    public Material lenticularMat => lenticularMats[0];
    public Material genViewsMat;
    public Material dofMat1;
    public Material dofMat2;
    public ComputeShader genViewsCompute;
    public RenderTexture genViewsDepthResult;
    public bool dofOn;
    public float dofStrength;
    public float dofVertical;
    public bool genViewsOn;
    public LookingGlassSettings.BridgeLoggingFlags bridgeLogging;

    public int ViewCount =>
        genViewsOn ? (LookingGlassSettings.VIEWCOUNTn / 2) : LookingGlassSettings.VIEWCOUNTn;

    [Serializable]
    public struct ViewSettings
    {
        public string name;
        public int width;
        public int height;
        public float viewCone;
        public float fov;
    }

    public ViewSettings[] viewSettingsList;
    public int viewSettingsIndex;
    public ViewSettings viewSettings => viewSettingsList[viewSettingsIndex];

    public bool overrideCamFov;
    public bool dontUseCalibrationViewSettings;

    public RenderTexture quiltOutput;
    public int quiltColumns => viewSettings.width > viewSettings.height ? 6 : 8;
    public int quiltRows => quiltColumns == 6 ? 8 : 6;
    [Range(0.1f, 1)]
    public float quiltScale = 1f;
    public Material quiltMat;
    public string QuiltSuffix => $"_qs{quiltColumns}x{quiltRows}a{(float)cal.screenW / cal.screenH:0.0##}";

    [NonSerialized]
    public bool masksGenerated;

    [NonSerialized]
    public RenderTargetIdentifier[] colorDepth2Array;

    // [Tooltip("Is copying the view textures to the quilt output")]
    // public bool isBlittingQuilt;

    // public KeyCode inGameScreenshotKey = KeyCode.F11;

    [Serializable]
    public struct RawCalibration
    {
        [Serializable]
        public struct CalibrationValue
        {
            public float value;

            public static implicit operator float(CalibrationValue calibrationValue)
            {
                return calibrationValue.value;
            }
        }

        [Serializable]
        public struct SubpixelCell
        {
            public float ROffsetX;
            public float ROffsetY;
            public float GOffsetX;
            public float GOffsetY;
            public float BOffsetX;
            public float BOffsetY;

            public static implicit operator Matrix4x4(SubpixelCell cell)
            {
                return new Matrix4x4(
                    new Vector4(cell.ROffsetX, cell.GOffsetX, cell.BOffsetX, 0),
                    new Vector4(cell.ROffsetY, cell.GOffsetY, cell.BOffsetY, 0),
                    new Vector4(0, 0, 0, 0),
                    new Vector4(0, 0, 0, 0)
                );
            }
        }

        public string configVersion;
        public string serial;
        public CalibrationValue pitch;
        public CalibrationValue slope;
        public CalibrationValue center;
        public CalibrationValue fringe;
        public CalibrationValue viewCone; // this doesn't get used
        public CalibrationValue invView;
        public CalibrationValue verticalAngle;
        public CalibrationValue DPI;
        public CalibrationValue screenW;
        public CalibrationValue screenH;
        public CalibrationValue flipImageX;
        public CalibrationValue flipImageY;
        public CalibrationValue flipSubp;
        public CalibrationValue CellPatternMode;
        public SubpixelCell[] subpixelCells;
        Matrix4x4[] subpixelCellMatrixArray;
        bool subpixelCellMatrixArrayCached;

        public Matrix4x4[] GetSubpixelCellMatrixArray()
        {
            if (!subpixelCellMatrixArrayCached)
            {
                subpixelCellMatrixArray = new Matrix4x4[subpixelCells.Length];
                for (int i = 0; i < subpixelCells.Length; i++)
                {
                    subpixelCellMatrixArray[i] = subpixelCells[i];
                }
                subpixelCellMatrixArrayCached = true;
            }
            return subpixelCellMatrixArray;
        }

        public int GetRotated()
        {
#if !UNITY_EDITOR && UNITY_IOS
            return (serial.Contains("H") || serial.Contains("N")) ? 1 : 0;
#else
            return 0;
#endif
        }

        public float GetProcessedPitch()
        {
            return pitch * screenW / DPI * Mathf.Cos(Mathf.Atan(1f / slope));
        }

        public float GetProcessedSlope()
        {
            return screenH / (screenW * slope);
        }

        public float GetFlatPitch()
        {
            return pitch * screenW / DPI;
        }
    }

    [NonSerialized]
    public bool calibrationApplied = false;

    public static bool texturesGenerated = false;

#if UNITY_EDITOR
    [DidReloadScripts]
    static void OnScriptsReloaded()
    {
        texturesGenerated = false;
    }
#endif

    public void ApplyCalibration()
    {
        if (calibrationApplied && !Application.isEditor)
            return;

        foreach (var mat in lenticularMats)
        {
            mat.SetInt("screenW", Mathf.RoundToInt(cal.screenW));
            mat.SetInt("screenH", Mathf.RoundToInt(cal.screenH));
            mat.SetInt("rotated", cal.GetRotated());
            mat.SetInt(
                "rotScreenW",
                Mathf.RoundToInt(cal.GetRotated() == 1 ? cal.screenH : cal.screenW)
            );
            mat.SetInt(
                "rotScreenH",
                Mathf.RoundToInt(cal.GetRotated() == 1 ? cal.screenW : cal.screenH)
            );
            mat.SetFloat(
                "invRotScreenW",
                cal.GetRotated() == 1 ? 1f / cal.screenH : 1f / cal.screenW
            );
            mat.SetFloat("p_pitch", cal.GetProcessedPitch());
            mat.SetFloat("p_slope", cal.GetProcessedSlope());
            mat.SetFloat("center", cal.center);

            mat.SetFloat("pixelW", 1f / cal.screenW);
            mat.SetFloat("pixelH", 1f / cal.screenH);
            mat.SetFloat("aspect", cal.screenW / cal.screenH);

            if (cal.subpixelCells != null && cal.subpixelCells.Length > 0)
            {
                mat.SetInt("usesSubpixelCells", 1);
                mat.SetMatrixArray("subpixelCells", cal.GetSubpixelCellMatrixArray());
            }
            else
            {
                mat.SetInt("usesSubpixelCells", 0);
            }
        }

        genViewsMat.SetTexture("_MainTexArray", colorRt);
        genViewsMat.SetTexture("_MaskTexArray", genViewsDepthResult);

        dofMat1.SetTexture("_MainTexArray", colorRt);
        dofMat1.SetFloat("aspect", cal.screenW / cal.screenH);
        dofMat1.SetVector("inverseVP", new Vector4(1f / colorRt.width, 1f / colorRt.height));

        dofMat2.SetTexture("_MainTexArray", colorRt2);
        dofMat2.SetTexture("_DepthTexArray", depthRt2);
        dofMat2.SetFloat("aspect", cal.screenW / cal.screenH);
        dofMat2.SetVector("inverseVP", new Vector4(1f / colorRt.width, 1f / colorRt.height));

        quiltMat.SetInt("quiltColumns", quiltColumns);
        quiltMat.SetInt("quiltRows", quiltRows);

        genViewsCompute.SetTexture(0, "DepthResult", genViewsDepthResult);

        if (dofOn)
        {
            lenticularMat.SetTexture("_MainTexArray", colorRtFinal);
            lenticularMat.SetTexture("_MainTexArray2", colorRt2Final);
            quiltMat.SetTexture("_MainTexArray", colorRtFinal);
            quiltMat.SetTexture("_MainTexArray2", colorRt2Final);
        }
        else
        {
            lenticularMat.SetTexture("_MainTexArray", colorRt);
            lenticularMat.SetTexture("_MainTexArray2", colorRt2);
            quiltMat.SetTexture("_MainTexArray", colorRt);
            quiltMat.SetTexture("_MainTexArray2", colorRt2);
        }

        if (genViewsOn)
        {
            Shader.EnableKeyword("GEN_VIEWS_ON");
            lenticularMat.EnableKeyword("GEN_VIEWS_ON");
            quiltMat.EnableKeyword("GEN_VIEWS_ON");
        }
        else
        {
            Shader.DisableKeyword("GEN_VIEWS_ON");
            lenticularMat.DisableKeyword("GEN_VIEWS_ON");
            quiltMat.DisableKeyword("GEN_VIEWS_ON");
        }

        calibrationApplied = true;
    }

    public void CreateRenderTexturesIfNeeded(bool force)
    {
        if (!force && colorRt != null && colorRt.IsCreated())
            return;

        if (!dontUseCalibrationViewSettings)
        {
            viewSettingsIndex = 0; // portrait orientation
            if (
                cal.serial.Contains("-8K")
                || cal.serial.Contains("-2K")
                || cal.serial.Contains("-4K")
                || cal.serial.Contains("-A")
                || cal.serial.Contains("-B")
                || cal.serial.Contains("-C")
                || cal.serial.Contains("-D")
                || cal.serial.Contains("-G")
                || cal.serial.Contains("-J")
                || cal.serial.Contains("-L")
                || cal.serial.Contains("-R")
            )
            {
                viewSettingsIndex = 1; // landscape orientation
            }
            else if (cal.serial.Contains("-E"))
            {
                viewSettingsIndex = 2; // go
            }
            else if (
                cal.serial.Contains("-PORT")
                || cal.serial.Contains("-F")
            )
            {
                viewSettingsIndex = 3; // old portrait
            }
        }

        colorRt = new RenderTexture(
            viewSettings.width,
            viewSettings.height,
            GraphicsFormat.B10G11R11_UFloatPack32,
            GraphicsFormat.D32_SFloat_S8_UInt,
            0
        )
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            dimension = TextureDimension.Tex2DArray,
            // volumeDepth = LookingGlassSettings.VIEWCOUNT
            volumeDepth = ViewCount
        };
        colorRt.Create();

        colorRt2 = new RenderTexture(
            viewSettings.width,
            viewSettings.height,
            GraphicsFormat.B10G11R11_UFloatPack32,
            GraphicsFormat.None,
            0
        )
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            dimension = TextureDimension.Tex2DArray,
            // volumeDepth = LookingGlassSettings.VIEWCOUNT - 1
            volumeDepth = ViewCount - 1
        };
        colorRt2.Create();

        colorRtFinal = new RenderTexture(
            viewSettings.width,
            viewSettings.height,
            GraphicsFormat.B10G11R11_UFloatPack32,
            GraphicsFormat.None,
            0
        )
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            dimension = TextureDimension.Tex2DArray,
            // volumeDepth = LookingGlassSettings.VIEWCOUNT
            volumeDepth = ViewCount
        };
        colorRtFinal.Create();

        colorRt2Final = new RenderTexture(
            viewSettings.width,
            viewSettings.height,
            GraphicsFormat.B10G11R11_UFloatPack32,
            GraphicsFormat.None,
            0
        )
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            dimension = TextureDimension.Tex2DArray,
            // volumeDepth = LookingGlassSettings.VIEWCOUNT - 1
            volumeDepth = ViewCount - 1
        };
        colorRt2Final.Create();

        depthRt2 = new RenderTexture(
            viewSettings.width,
            viewSettings.height,
            GraphicsFormat.R16_UNorm,
            GraphicsFormat.None,
            0
        )
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            dimension = TextureDimension.Tex2DArray,
            // volumeDepth = LookingGlassSettings.VIEWCOUNT - 1
            volumeDepth = ViewCount - 1
        };
        depthRt2.Create();

        colorDepth2Array = new[]
        {
            new RenderTargetIdentifier(colorRt2),
            new RenderTargetIdentifier(depthRt2)
        };

        int screenW = cal.GetRotated() == 0 ? (int)cal.screenW : (int)cal.screenH;
        int screenH = cal.GetRotated() == 0 ? (int)cal.screenH : (int)cal.screenW;

        lenticularRt = new RenderTexture(
            screenW,
            screenH,
            GraphicsFormat.R8G8B8A8_SRGB,
            GraphicsFormat.D32_SFloat_S8_UInt,
            0
        )
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
        };
        lenticularRt.Create();

        genViewsDepthResult = new RenderTexture(
            viewSettings.width,
            viewSettings.height,
            GraphicsFormat.R8_UNorm,
            GraphicsFormat.None,
            0
        )
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            dimension = TextureDimension.Tex2DArray,
            // volumeDepth = LookingGlassSettings.VIEWCOUNT - 1,
            volumeDepth = ViewCount - 1,
            enableRandomWrite = true
        };
        genViewsDepthResult.Create();

#if UNITY_EDITOR
        ModifyRenderTextureAsset(
            quiltOutput,
            Mathf.FloorToInt(viewSettings.width * quiltScale) * quiltColumns,
            Mathf.FloorToInt(viewSettings.height * quiltScale) * quiltRows
        );
#else
        // get rid of quilt output if it exists
        if (quiltOutput != null)
        {
            if (quiltOutput.IsCreated())
                quiltOutput.Release();
            Destroy(quiltOutput);
        }
#endif
        texturesGenerated = true;
    }

#if UNITY_EDITOR
    public static void ModifyRenderTextureAsset(RenderTexture rt, int width, int height)
    {
        if (rt == null)
        {
            Debug.LogError("RenderTexture is null.");
            return;
        }

        string path = AssetDatabase.GetAssetPath(rt);
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogError("RenderTexture is not an asset.");
            return;
        }

        rt.Release();
        rt.width = width;
        rt.height = height;
        rt.Create();
        EditorUtility.SetDirty(rt);
        AssetDatabase.SaveAssets();
        // Debug.Log($"RenderTexture asset '{rt.name}' resized to {width}x{height}.");
    }

    [MenuItem("Looking Glass/Save QuiltOutput to PNG _F11")]
    public static void SaveQuiltOutputToPNGMenu()
    {
        // Find the first MultiviewData asset in the project
        string[] guids = AssetDatabase.FindAssets("t:MultiviewData");
        if (guids.Length == 0)
        {
            Debug.LogError("No MultiviewData asset found.");
            return;
        }
        string assetPath = AssetDatabase.GUIDToAssetPath(guids[0]);
        MultiviewData data = AssetDatabase.LoadAssetAtPath<MultiviewData>(assetPath);
        data.SaveQuiltOutputToPNG();
    }

    public void SaveQuiltOutputToPNG()
    {
        var quiltW = Mathf.FloorToInt(viewSettings.width * quiltScale) * quiltColumns;
        var quiltH = Mathf.FloorToInt(viewSettings.height * quiltScale) * quiltRows;

        if (quiltOutput == null || quiltOutput.width != quiltW || quiltOutput.height != quiltH)
        {
            ModifyRenderTextureAsset(quiltOutput, quiltW, quiltH);
        }

        RenderTexture rt = quiltOutput;
        RenderTexture currentRT = RenderTexture.active;
        RenderTexture.active = rt;

        Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();

        RenderTexture.active = currentRT;

        // Only apply correction if in Linear color space
        if (QualitySettings.activeColorSpace == ColorSpace.Linear)
        {
            Color[] pixels = tex.GetPixels();
            System.Threading.Tasks.Parallel.For(0, pixels.Length, i =>
            {
                pixels[i].r = Mathf.LinearToGammaSpace(pixels[i].r);
                pixels[i].g = Mathf.LinearToGammaSpace(pixels[i].g);
                pixels[i].b = Mathf.LinearToGammaSpace(pixels[i].b);
            });
            tex.SetPixels(pixels);
            tex.Apply();
        }

        byte[] pngData = tex.EncodeToPNG();
        DestroyImmediate(tex);

        string recordingsPath = Path.Combine(Application.dataPath, "../Recordings");
        if (!Directory.Exists(recordingsPath))
            Directory.CreateDirectory(recordingsPath);

        // Find the next available numbered filename
        int fileIndex = 0;
        string fileName;
        string filePath;
        do
        {
            fileName = $"{Application.productName}_{fileIndex:D3}{QuiltSuffix}.png";
            filePath = Path.Combine(recordingsPath, fileName);
            fileIndex++;
        } while (File.Exists(filePath));

        File.WriteAllBytes(filePath, pngData);
        Debug.Log($"Saved quiltOutput to {filePath}");
    }
#endif
}

