using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Networking;
using Newtonsoft.Json.Linq;

public class UnitStateRestTester : MonoBehaviour
{
    private Texture2D sigilSDFTex;
    private Queue<byte[]> pendingTextureLoads = new Queue<byte[]>();
    private object queueLock = new object();
    private const string BASE_URL = "https://uni-cognizer-1.onrender.com";
    private const string AUTH_USERNAME = "admin";
    private const string AUTH_PASSWORD = "unieditor";

    private SDFControl sdfControl;
    private JArray mindMoments;
    private int curMindMoment = 0;
    public bool DataLoaded { get; private set; } = false;
    
    [Header("Sigil Loop Settings")]
    public float sigilCycleDelay = 10f; // Delay between sigil cycles in seconds
    
    // Event delegate for when data is loaded
    public delegate void DataLoadedHandler();
    public event DataLoadedHandler OnDataLoaded;
    
    private bool isTextureLoaded = false;
    public SigilDataSO currentSigilData;
    
    private void SetBasicAuth(UnityWebRequest request)
    {
        string auth = $"{AUTH_USERNAME}:{AUTH_PASSWORD}";
        string authEncoded = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(auth));
        request.SetRequestHeader("Authorization", $"Basic {authEncoded}");
    }
    
    private Texture2D CopyTexture(Texture2D source)
    {
        if (source == null) return null;
        
        // Create new texture with same dimensions and format, no mipmaps
        Texture2D copy = new Texture2D(source.width, source.height, source.format, false);
        
        // Copy pixel data using GetPixels/SetPixels (only copies base mip level, no mipmaps)
        copy.SetPixels(source.GetPixels());
        
        // Apply changes
        copy.Apply();
        
        return copy;
    }
    
    void Start()
    {
        // SDF texture setup
        sigilSDFTex = new Texture2D(512, 512);
        sigilSDFTex.filterMode = FilterMode.Bilinear;

        sdfControl = GetComponent<SDFControl>();
        
        // Create ScriptableObject instance if not assigned in inspector
        if (currentSigilData == null)
        {
            currentSigilData = ScriptableObject.CreateInstance<SigilDataSO>();
        }

        // Fetch recent mind moments
        StartCoroutine(FetchRecentMindMoments());
    }

    void Update()
    {
        // Process pending texture loads on main thread
        lock (queueLock)
        {
            while (pendingTextureLoads.Count > 0)
            {
                byte[] pngBytes = pendingTextureLoads.Dequeue();
                LoadTextureOnMainThread(pngBytes);
            }
        }
    }

    IEnumerator FetchRecentMindMoments()
    {
        string url = $"{BASE_URL}/api/mind-moments/recent";
        Debug.Log($"Fetching recent mind moments from: {url}");
        
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            // Set headers
            request.SetRequestHeader("Accept", "application/json");
            request.SetRequestHeader("User-Agent", "Unity-WebRequest");
            SetBasicAuth(request);
            
            yield return request.SendWebRequest();
            
            // Check response code first
            long responseCode = request.responseCode;
            Debug.Log($"Response code: {responseCode}");
            
            if (responseCode != 200)
            {
                string responseText = request.downloadHandler?.text ?? "null";
                Debug.LogError($"HTTP Error {responseCode}: {responseText}");
                Debug.LogError($"Request error: {request.error}");
                yield break;
            }
            
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Failed to fetch mind moments: {request.error}");
                Debug.LogError($"Response text: {request.downloadHandler?.text ?? "null"}");
                yield break;
            }
            
            bool parseSuccess = false;
            try
            {
                string jsonResponse = request.downloadHandler.text;
                Debug.Log($"Received JSON response length: {jsonResponse.Length}");
                
                JObject responseData = JObject.Parse(jsonResponse);
                mindMoments = responseData["moments"] as JArray;
                
                if (mindMoments == null || mindMoments.Count == 0)
                {
                    Debug.LogError("No moments found in response");
                    yield break;
                }
                
                Debug.Log($"Loaded {mindMoments.Count} mind moments");
                
                // Set data loaded flag and fire event
                DataLoaded = true;
                OnDataLoaded?.Invoke();
                
                // Start the sigil cycling loop
                StartCoroutine(SigilCycleLoop());
                
                parseSuccess = true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Exception parsing mind moments: {ex.Message}\n{ex.StackTrace}");
                yield break;
            }
        }
    }

    IEnumerator FetchCurrentMomentSDF()
    {
        if (mindMoments == null || curMindMoment < 0 || curMindMoment >= mindMoments.Count)
        {
            Debug.LogError($"Invalid moment index: {curMindMoment} (total moments: {mindMoments?.Count ?? 0})");
            yield break;
        }
        
        JObject currentMoment = mindMoments[curMindMoment] as JObject;
        string momentId = currentMoment["id"]?.Value<string>();
        
        if (string.IsNullOrEmpty(momentId))
        {
            Debug.LogError("Current moment ID is null or empty");
            yield break;
        }
        
        Debug.Log($"Fetching SDF for moment {curMindMoment}: {momentId}");
        yield return StartCoroutine(FetchSDFData(momentId));
    }

    IEnumerator FetchSDFData(string momentId)
    {
        string url = $"{BASE_URL}/api/sigils/{momentId}/sdf";
        Debug.Log($"Fetching SDF data from: {url}");
        
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            // Set headers
            request.SetRequestHeader("Accept", "application/json");
            request.SetRequestHeader("User-Agent", "Unity-WebRequest");
            SetBasicAuth(request);
            
            yield return request.SendWebRequest();
            
            // Check response code first
            long responseCode = request.responseCode;
            Debug.Log($"SDF Response code: {responseCode}");
            
            if (responseCode != 200)
            {
                string responseText = request.downloadHandler?.text ?? "null";
                Debug.LogError($"HTTP Error {responseCode}: {responseText}");
                Debug.LogError($"Request error: {request.error}");
                yield break;
            }
            
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Failed to fetch SDF data: {request.error}");
                Debug.LogError($"Response text: {request.downloadHandler?.text ?? "null"}");
                yield break;
            }
            
            string base64Data = null;
            try
            {
                string jsonResponse = request.downloadHandler.text;
                Debug.Log($"Received SDF JSON response length: {jsonResponse.Length}");
                
                JObject sdfData = JObject.Parse(jsonResponse);
                base64Data = sdfData["data"]?.Value<string>();
                
                if (string.IsNullOrEmpty(base64Data))
                {
                    Debug.LogError("SDF data is null or empty");
                    yield break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Exception parsing SDF data: {ex.Message}\n{ex.StackTrace}");
                yield break;
            }
            
            // Process base64 data outside try-catch (no yield statements here)
            if (!string.IsNullOrEmpty(base64Data))
            {
                try
                {
                    Debug.Log($"Base64 data length: {base64Data.Length}");
                    
                    // Decode base64 (this happens on main thread via coroutine, but we'll queue it anyway for consistency)
                    byte[] pngBytes = Convert.FromBase64String(base64Data);
                    Debug.Log($"PNG bytes length: {pngBytes.Length}");
                    
                    // Queue texture load to happen on main thread
                    lock (queueLock)
                    {
                        pendingTextureLoads.Enqueue(pngBytes);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Exception decoding base64: {ex.Message}\n{ex.StackTrace}");
                }
            }
        }
    }

    IEnumerator SigilCycleLoop()
    {
        // Wait for data to be loaded
        while (!DataLoaded || mindMoments == null || mindMoments.Count == 0)
        {
            yield return new WaitForSeconds(0.1f);
        }
        
        // Fetch first moment immediately
        isTextureLoaded = false;
        yield return StartCoroutine(FetchCurrentMomentSDF());
        
        // Wait for texture to load
        float timeout = 10f;
        float elapsed = 0f;
        while (!isTextureLoaded && elapsed < timeout)
        {
            yield return null;
            elapsed += Time.deltaTime;
        }
        
        // Main loop - cycle through moments (only runs after data is loaded)
        while (true)
        {
            // Wait for the cycle delay
            yield return new WaitForSeconds(sigilCycleDelay);
            
            // Cycle to next moment
            curMindMoment = (curMindMoment + 1) % mindMoments.Count;
            Debug.Log($"Cycling to moment {curMindMoment} of {mindMoments.Count}");
            
            // Reset texture loaded flag
            isTextureLoaded = false;
            
            // Fetch SDF for current moment
            yield return StartCoroutine(FetchCurrentMomentSDF());
            
            // Wait for texture to load (check every frame)
            timeout = 10f;
            elapsed = 0f;
            while (!isTextureLoaded && elapsed < timeout)
            {
                yield return null;
                elapsed += Time.deltaTime;
            }
            
            if (!isTextureLoaded)
            {
                Debug.LogWarning("Texture did not load within timeout period");
            }
        }
    }

    void LoadTextureOnMainThread(byte[] pngBytes)
    {
        try
        {
            bool sdfLoaded = sigilSDFTex.LoadImage(pngBytes);
            Debug.Log("SDF can load: " + sdfLoaded);
            
            if (sdfLoaded)
            {
                Debug.Log($"Texture loaded: {sigilSDFTex.width}x{sigilSDFTex.height}, format: {sigilSDFTex.format}");
                Debug.Log("SDF TEX DONE");

                // Get sigil phrase and code from current moment
                if (mindMoments != null && curMindMoment >= 0 && curMindMoment < mindMoments.Count)
                {
                    JObject currentMoment = mindMoments[curMindMoment] as JObject;
                    string sigilPhrase = currentMoment["sigil_phrase"]?.Value<string>();
                    string sigilCode = currentMoment["sigil_code"]?.Value<string>();
                    
                    // Update ScriptableObject with current sigil data
                    currentSigilData.sigilPhrase = sigilPhrase;
                    currentSigilData.sigilCode = sigilCode;
                    currentSigilData.pngTexture = CopyTexture(sigilSDFTex);
                    
                    // Call ShowSigil on UniState directly
                    isTextureLoaded = true;
                    UniState.Instance.ShowSigil(currentSigilData);
                }
                else
                {
                    Debug.LogError("Cannot get sigil phrase - invalid moment index or moments not loaded");
                }
                
                //sdfDisplay.texture = sigilSDFTex;
                //sdfMaterial.SetTexture("_SDFTex", sigilSDFTex);
            }
            else
            {
                Debug.LogError("Failed to load image data!");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Exception in LoadTextureOnMainThread: {ex.Message}\n{ex.StackTrace}");
        }
    }
}

