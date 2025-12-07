using Klak.Spout;
using Klak.Syphon;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class KlakPreviewSender : MonoBehaviour
{
    public MultiviewData multiviewData;
    public SpoutSender spoutSender;
    public SyphonServer syphonServer;

    bool shouldSetup;
    bool shouldTeardown;
    bool logToConsole;

#if UNITY_EDITOR
    void Update()
    {
        if (shouldSetup)
        {
            Setup();
            shouldSetup = false;
        }

        if (shouldTeardown)
        {
            Teardown();
            shouldTeardown = false;
        }
    }

    public void Setup()
    {
        if (logToConsole)
            Debug.Log("setting up preview");
        if (multiviewData != null && multiviewData.lenticularRt != null)
        {
            if (spoutSender != null)
            {
                spoutSender.enabled =
                    Application.platform == RuntimePlatform.WindowsEditor
                    || Application.platform == RuntimePlatform.WindowsPlayer;

                if (spoutSender.enabled)
                    spoutSender.sourceTexture = multiviewData.lenticularRt;
                else
                    spoutSender.sourceTexture = null;
            }

            if (syphonServer != null)
            {
                syphonServer.enabled =
                    Application.platform == RuntimePlatform.OSXEditor
                    || Application.platform == RuntimePlatform.OSXPlayer;

                if (syphonServer.enabled)
                {
                    syphonServer.SourceTexture = multiviewData.lenticularRt;
                }
                else
                    syphonServer.SourceTexture = null;
            }
        }
    }

    public void Teardown()
    {
        if (logToConsole)
            Debug.Log("tearing down preview");

        if (spoutSender != null)
        {
            spoutSender.sourceTexture = null;
            syphonServer.enabled = false;
        }

        if (syphonServer != null)
        {
            syphonServer.SourceTexture = null;
            syphonServer.enabled = false;
        }
    }
#endif
}
