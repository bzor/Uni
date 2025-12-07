using System;
#if UNITY_IOS
using System.Runtime.InteropServices;
#if UNITY_EDITOR
using System.IO;
#endif
#else
using System.IO;
#endif
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

[ExecuteAlways]
#if UNITY_STANDALONE
[RequireComponent(typeof(BridgeInterface))]
#endif
public class CalibrationLoader : MonoBehaviour
{
    public TextAsset overrideCalibration;
    public MultiviewData multiviewData;
    public Camera[] multiviewCams;
    public RawImage lenticularRawImage;

    const string calKey = "LKG_calibration";

    /*
    - check in prefs for existing cal
    - load calibration from there
    - check in local storage for cal
    - load calibration from there
    - otherwise bring up file picker
    - load calibration from there
        - loading calibration means saving it to player prefs as well
    - set up data from calibration
    - if user taps with 4 fingers, bring up file picker again
    */

    public void LoadCalibration(string calibrationText)
    {
        multiviewData.cal = JsonUtility.FromJson<MultiviewData.RawCalibration>(calibrationText);
        SetupCalibration();
    }

    public void SetupCalibration()
    {
        multiviewData.CreateRenderTexturesIfNeeded(true);
        foreach (var cam in multiviewCams)
        {
            cam.targetTexture = multiviewData.colorRt;
            if (multiviewData.overrideCamFov)
                cam.fieldOfView = multiviewData.viewSettings.fov;
        }
        lenticularRawImage.texture = multiviewData.lenticularRt;
        lenticularRawImage.enabled = true;

        multiviewData.calibrationApplied = false;

#if UNITY_EDITOR
        var klakPreview = FindAnyObjectByType<KlakPreviewSender>();
        klakPreview.Setup();
#endif
    }

#if UNITY_IOS
    bool choosingCal = false;
    public bool iosCalValid;
    public bool screenConnected;

    public IEnumerator ScreenConnectedRoutine()
    {
        if (!screenConnected)
        {
            lenticularRawImage.enabled = false;
        }
        else
        {
            bool volumeAccessible = false;
            yield return new WaitForSeconds(1f);

            for (int i = 0; i < 2; i++)
            {
                yield return new WaitForSeconds(1f);
                Debug.Log("looking for lkg volume that was bookmarked...");
                volumeAccessible = _IsVolumeAccessible();
                if (volumeAccessible)
                    break;
            }
            if (volumeAccessible)
            {
                lenticularRawImage.enabled = true;
            }
            else
            {
                OpenFilePicker(OnFileLoaded);
            }
        }
    }
#endif

    void Update()
    {
        if (!MultiviewData.texturesGenerated)
            SetupCalibration();

#if UNITY_IOS

        if (Input.touchCount >= 4)
        {
            bool wasDragging = true;
            foreach (var touch in Input.touches)
            {
                wasDragging &= touch.phase == TouchPhase.Moved;
                wasDragging &= touch.deltaPosition.y < -500f * touch.deltaTime;
            }
            if (!choosingCal && wasDragging)
            {
                OpenFilePicker(OnFileLoaded);
                choosingCal = true;
            }
        }
        else
        {
            choosingCal = false;
        }
#endif
    }

    IEnumerator Start()
    {
        // 1. Editor or override calibration
        if (Application.isEditor)
        {
            HandleEditorCalibration();
            yield break;
        }

        if (overrideCalibration != null)
        {
            LoadCalibration(overrideCalibration.text);
            yield break;
        }

#if UNITY_IOS
        yield return StartCoroutine(HandleIOSCalibration());
#elif !UNITY_EDITOR
        // 2. Standalone build with BridgeInterface
        var bridgeInterface = GetComponent<BridgeInterface>();
        yield return StartCoroutine(bridgeInterface.ConnectAndListDisplays());
        LookingGlass.Toolkit.Display display;
        if (bridgeInterface.displays != null && bridgeInterface.displays.Count > 0)
        {
            Debug.Log("found display, loading calibration");
            display = bridgeInterface.displays[0];
            Debug.Log(display.calibration.rawJson);
            LoadCalibration(display.calibration.rawJson);

            // for now, just make sure user has lkg as their leftmost display if they have > 2 displays connected
            if (Display.displays.Length > 1)
            {
                Display.displays[1].Activate();
            }
            else
            {
                Debug.LogError("Looking glass not found, please connect a looking glass as a second monitor");
            }
            yield break;
        }
        else
        {
            Debug.Log("no display found, using default calibration");
            SetupCalibration();
            yield break;
        }
#endif
    }

    private void HandleEditorCalibration()
    {
#if UNITY_EDITOR
        var calPath = Path.Combine(Path.GetTempPath(), "recent_calibration.json");
        if (File.Exists(calPath))
        {
            Debug.Log("found calibration file at: " + calPath);
            var calRawJson = File.ReadAllText(calPath);
            LoadCalibration(calRawJson);
            return;
        }
#endif
        SetupCalibration();
    }

#if UNITY_IOS
private IEnumerator HandleIOSCalibration()
{
    if (Application.isEditor)
    {
        SetupCalibration();
        yield break;
    }

    screenConnected = _InitializeScreenMonitor();
    Debug.Log("screen connected: " + screenConnected);

    _RegisterScreenEventCallback(OnScreenEvent);

    onScreenEventCallback = (connected) =>
    {
        screenConnected = connected;
        StartCoroutine(ScreenConnectedRoutine());
    };

    lenticularRawImage.enabled = false;

    if (PlayerPrefs.HasKey(calKey) && _IsVolumeAccessible())
    {
        LoadCalibration(PlayerPrefs.GetString(calKey));
    }
    else
    {
        OpenFilePicker(OnFileLoaded);
    }
    yield break;
}
#endif
    // iOS calibration file handling
#if UNITY_IOS
    void OnFileLoaded(string fileText)
    {
        try
        {
            LoadCalibration(fileText);
        }
        catch
        {
            Debug.Log("failed to load calibration");
            return;
        }

        if (!Application.isEditor)
        {
            Debug.Log("loaded calibration, will save to playerprefs:");
            Debug.Log(fileText);
            PlayerPrefs.SetString(calKey, fileText);
        }
    }

    // Import the native function
    [DllImport("__Internal")]
    private static extern void _OpenDocumentPicker(FileSelectedCallback callback);

    [DllImport("__Internal")]
    private static extern bool _IsVolumeAccessible();

    // Define a delegate type for the callback
    private delegate void FileSelectedCallback(IntPtr result);

    private static Action<string> onFileSelectedCallback;

    // for screen stuff
    // Define delegate type matching the native callback (bool parameter)
    private delegate void ScreenEventCallback(bool connected);

    [DllImport("__Internal")]
    private static extern bool _InitializeScreenMonitor();

    // Store the Unity callback
    private static Action<bool> onScreenEventCallback;

    [DllImport("__Internal")]
    private static extern void _RegisterScreenEventCallback(ScreenEventCallback callback);

    [AOT.MonoPInvokeCallback(typeof(ScreenEventCallback))]
    private static void OnScreenEvent(bool connected)
    {
        Debug.Log(
            connected
                ? "Screen Connected Event Received in Unity!"
                : "Screen Disconnected Event Received in Unity!"
        );
        onScreenEventCallback?.Invoke(connected);
    }

    public void OpenFilePicker(Action<string> onFileSelected)
    {
        if (Application.platform == RuntimePlatform.IPhonePlayer)
        {
            onFileSelectedCallback = onFileSelected; // Store the callback
            _OpenDocumentPicker(OnFileSelected); // Pass static callback to native code
        }
        else
        {
            Debug.LogWarning("File picker is only supported on iOS.");
            onFileSelected?.Invoke(null);
        }
    }

    // Static method to handle the result from native code
    [AOT.MonoPInvokeCallback(typeof(FileSelectedCallback))]
    private static void OnFileSelected(IntPtr result)
    {
        if (onFileSelectedCallback != null)
        {
            if (result != IntPtr.Zero)
            {
                string fileContents = Marshal.PtrToStringAuto(result);
                onFileSelectedCallback.Invoke(fileContents);
            }
            else
            {
                onFileSelectedCallback.Invoke(null); // Handle null result
            }

            onFileSelectedCallback = null; // Clear the callback to avoid memory leaks
        }
    }

#else

    public string ReadLKGCalibrationFile()
    {
        try
        {
            // Get all available drives
            DriveInfo[] drives = DriveInfo.GetDrives();

            foreach (DriveInfo drive in drives)
            {
                // Ensure the drive is ready to be accessed
                if (drive.IsReady)
                {
                    // Construct the expected folder path
                    string folderPath = Path.Combine(
                        drive.RootDirectory.FullName,
                        "LKG_calibration"
                    );

                    // Check if the folder exists
                    if (Directory.Exists(folderPath))
                    {
                        // Construct the full file path
                        string filePath = Path.Combine(folderPath, "visual.json");

                        // Check if the file exists
                        if (File.Exists(filePath))
                        {
                            // Read and return the file contents
                            string fileContents = File.ReadAllText(filePath);
                            Debug.Log($"File found at: {filePath}");
                            return fileContents;
                        }
                        else
                        {
                            Debug.LogWarning($"File not found in folder: {folderPath}");
                        }
                    }
                }
            }

            Debug.LogError(
                "No drive with the folder 'LKG_calibration' was found, or the file does not exist."
            );
        }
        catch (Exception ex)
        {
            Debug.LogError($"An error occurred while searching for the file: {ex.Message}");
        }

        return null; // Return null if the file could not be read
    }

#endif
}
