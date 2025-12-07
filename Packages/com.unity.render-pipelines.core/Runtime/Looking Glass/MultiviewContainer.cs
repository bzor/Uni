using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.Rendering;

[ExecuteAlways]
public class MultiviewContainer : MonoBehaviour
{
    public MultiviewData data;
    public Camera cam;
    public float Fov => cam.fieldOfView;
    public float Size => transform.lossyScale.y;
    public float RelativeDist => 1f / Mathf.Tan(Fov * 0.5f * Mathf.Deg2Rad);
    public float nearClipFactor = 1f;
    public float farClipFactor = 1f;

    // gizmo
    public Color frustumColor = new Color32(0, 255, 124, 85);
    public Color middlePlaneColor = new Color32(145, 255, 0, 255);
    float[] cornerDists = new float[3];
    Vector3[] frustumCorners = new Vector3[12];

    [Serializable]
    public struct DofSettings
    {
        public bool autoSetFocus;
        public bool overrideSettings;
        public float focalLength;
        public float aperture;
    };

    // todo: remove, this is old
    [HideInInspector]
    public DofSettings dofSettings = new DofSettings()
    {
        autoSetFocus = true,
        overrideSettings = true,
        focalLength = 200f,
        aperture = 2f,
    };

    void Awake()
    {
#if !UNITY_EDITOR
#if UNITY_IOS
        Application.targetFrameRate = 60;
#else
        // activate display 2
        if (Display.displays.Length > 1)
        {
            Display.displays[1].Activate();
        }
#endif
#endif
    }

    public void ResetCamera(float normalizedView = 0.5f)
    {
        cam.aspect = data.cal.screenW / data.cal.screenH;

        // set near and far clip planes based on dist
        cam.nearClipPlane = Mathf.Max(RelativeDist * Size - nearClipFactor * Size, 0.1f);
        cam.farClipPlane = Mathf.Max(RelativeDist * Size + farClipFactor * Size, cam.nearClipPlane);

        // reset matrices, save center for later
        cam.transform.localPosition = Vector3.back * RelativeDist;
        cam.ResetWorldToCameraMatrix();
        cam.ResetProjectionMatrix();
        var projMatrix = cam.projectionMatrix;

        var relativeMaxOffset =
            RelativeDist * Mathf.Tan(Mathf.Deg2Rad * data.viewSettings.viewCone * 0.5f);
        var offset = relativeMaxOffset * (normalizedView * 2f - 1f);
        cam.transform.localPosition += Vector3.right * offset;
        projMatrix.m02 -= offset / cam.aspect;

        cam.projectionMatrix = projMatrix;

        Shader.SetGlobalFloat("lkg_maxOffset", relativeMaxOffset * Size);
        Shader.SetGlobalFloat("lkg_nearClip", cam.nearClipPlane);
        Shader.SetGlobalFloat("lkg_farClip", cam.farClipPlane);
        Shader.SetGlobalFloat("lkg_focalDist", RelativeDist * Size);
        Shader.SetGlobalFloat("lkg_focalDistInv", 1f / (RelativeDist * Size));
        Shader.SetGlobalMatrix("lkg_projMat", cam.projectionMatrix);
        Shader.SetGlobalFloat("lkg_aspect", cam.aspect);
        Shader.SetGlobalFloat("lkg_dofStrength", data.dofStrength);
        Shader.SetGlobalFloat("lkg_dofVertical", data.dofVertical);

        Shader.SetGlobalVector(
            "lkg_linearDepthParamsReversedZ",
            new Vector4(
                cam.farClipPlane / cam.nearClipPlane - 1f,
                1f,
                1f / cam.nearClipPlane - 1f / cam.farClipPlane,
                1f / cam.farClipPlane
            )
        );

        Shader.SetGlobalVector(
            "lkg_linearDepthParams",
            new Vector4(
                1f - cam.farClipPlane / cam.nearClipPlane,
                cam.farClipPlane / cam.nearClipPlane,
                1f / cam.farClipPlane - 1f / cam.nearClipPlane,
                1f / cam.nearClipPlane
            )
        );

        {
            data.genViewsCompute.SetFloat("lkg_maxOffset", relativeMaxOffset * Size);
            data.genViewsCompute.SetFloat("lkg_nearClip", cam.nearClipPlane);
            data.genViewsCompute.SetFloat("lkg_farClip", cam.farClipPlane);
            data.genViewsCompute.SetFloat("lkg_focalDist", RelativeDist * Size);
            data.genViewsCompute.SetFloat("lkg_focalDistInv", 1f / (RelativeDist * Size));
            data.genViewsCompute.SetMatrix("lkg_projMat", cam.projectionMatrix);

            data.genViewsCompute.SetVector(
                "lkg_linearDepthParamsReversedZ",
                new Vector4(
                    cam.farClipPlane / cam.nearClipPlane - 1f,
                    1f,
                    1f / cam.nearClipPlane - 1f / cam.farClipPlane,
                    1f / cam.farClipPlane
                )
            );

            data.genViewsCompute.SetVector(
                "lkg_linearDepthParams",
                new Vector4(
                    1f - cam.farClipPlane / cam.nearClipPlane,
                    cam.farClipPlane / cam.nearClipPlane,
                    1f / cam.farClipPlane - 1f / cam.nearClipPlane,
                    1f / cam.nearClipPlane
                )
            );
        }
    }

    void OnValidate()
    {
        ResetCamera();
    }

	void OnDrawGizmos()
    {
        if (cam == null)
            return;

        ResetCamera();

        Gizmos.color =
            QualitySettings.activeColorSpace == ColorSpace.Gamma
                ? frustumColor.gamma
                : frustumColor;
        cornerDists[0] = RelativeDist * Size;
        cornerDists[1] = cam.nearClipPlane;
        cornerDists[2] = cam.farClipPlane;
        for (int i = 0; i < cornerDists.Length; i++)
        {
            float dist = cornerDists[i];
            int offset = i * 4;
            frustumCorners[offset + 0] = cam.ViewportToWorldPoint(new Vector3(0, 0, dist));
            frustumCorners[offset + 1] = cam.ViewportToWorldPoint(new Vector3(0, 1, dist));
            frustumCorners[offset + 2] = cam.ViewportToWorldPoint(new Vector3(1, 1, dist));
            frustumCorners[offset + 3] = cam.ViewportToWorldPoint(new Vector3(1, 0, dist));
            // draw each square
            for (int j = 0; j < 4; j++)
            {
                Vector3 start = frustumCorners[offset + j];
                Vector3 end = frustumCorners[offset + (j + 1) % 4];
                if (i > 0)
                {
                    // draw a normal line for front and back
                    Gizmos.color =
                        QualitySettings.activeColorSpace == ColorSpace.Gamma
                            ? frustumColor.gamma
                            : frustumColor;
                    Gizmos.DrawLine(start, end);
                }
                else
                {
                    Gizmos.color =
                        QualitySettings.activeColorSpace == ColorSpace.Gamma
                            ? middlePlaneColor.gamma
                            : middlePlaneColor;
                    Gizmos.DrawLine(start, end);
                }
            }
        }
        // connect them
        for (int i = 0; i < 4; i++)
            Gizmos.DrawLine(frustumCorners[4 + i], frustumCorners[8 + i]);
    }
}
