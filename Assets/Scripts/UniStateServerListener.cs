using UnityEngine;
using System;
using System.Collections.Generic;
using SocketIOClient;
using SocketIOClient.Newtonsoft.Json;
using UnityEngine.UI;
using Newtonsoft.Json.Linq;


public class UniStateServerListener : MonoBehaviour
{
    private Texture2D sigilPNGTex;
    private Texture2D perceptPNGTex;
    private SocketIOUnity socket;
    private Queue<byte[]> pendingTextureLoads = new Queue<byte[]>();
    private Queue<PendingPerceptTexture> pendingPerceptTextures = new Queue<PendingPerceptTexture>();
    private object queueLock = new object();
    
    // Store sigil data from socket until texture loads
    private string pendingSigilPhrase;
    private string pendingSigilCode;
    
    private struct PendingPerceptTexture
    {
        public byte[] pngBytes;
        public PerceptData percept;
    }
    
    public SigilDataSO currentSigilData;
    
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
        //png texture setup
        sigilPNGTex = new Texture2D(512, 512);
        sigilPNGTex.filterMode = FilterMode.Bilinear;
        
        //percept texture setup (256x256)
        perceptPNGTex = new Texture2D(256, 256);
        perceptPNGTex.filterMode = FilterMode.Bilinear;
        
        // Create ScriptableObject instance if not assigned in inspector
        if (currentSigilData == null)
        {
            currentSigilData = ScriptableObject.CreateInstance<SigilDataSO>();
        }
        
        //socket
        var uri = new Uri("https://uni-cognizer-1.onrender.com/");
        socket = new SocketIOUnity(uri);
        socket.JsonSerializer = new NewtonsoftJsonSerializer();
        SetListeners();
        ConnectSocket();
    }

    void SetListeners()
    {
        socket.On("sigil", OnSigil);
        socket.On("mindMoment", OnMindMoment);
        socket.On("perceptReceived", OnPerceptReceived);
        socket.On("phase", OnPhase);
        socket.OnConnected += (sender, e) =>
        {
            Debug.Log("socket.OnConnected");
        };
        socket.OnDisconnected += (sender, e) =>
        {
            Debug.Log("socket.OnDisconnected");
        };
        socket.OnPing += (sender, e) =>
        {
            Debug.Log("Ping");
        };
        socket.OnPong += (sender, e) =>
        {
            Debug.Log("Pong: " + e.TotalMilliseconds);
        };        
    }

    void ConnectSocket()
    {
        Debug.Log("connecting socket...");
        socket.Connect();
    }

    async void OnApplicationQuit()
    {
        if (socket != null && socket.Connected) 
        {
            await socket.DisconnectAsync();
        }
        socket?.Dispose();
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
            
            while (pendingPerceptTextures.Count > 0)
            {
                PendingPerceptTexture pending = pendingPerceptTextures.Dequeue();
                LoadPerceptTextureOnMainThread(pending.pngBytes, pending.percept);
            }
        }
    }

    void OnSigil(SocketIOResponse response)
    {
        try
        {
            var data = response.GetValue<JObject>();
            string phrase = data["sigilPhrase"]?.Value<string>();
            Debug.Log($"OnSigil: {phrase}");
            
            string code = data["sigilCode"]?.Value<string>();
            
            // Store sigil phrase and code for when texture loads
            pendingSigilPhrase = phrase;
            pendingSigilCode = code;
            
            var png = data["png"];
            string base64Data = null;
            if (png != null)
            {
                if (png.Type == JTokenType.String)
                {
                    base64Data = png.Value<string>();
                }
                else if (png.Type == JTokenType.Object)
                {
                    base64Data = png["data"]?.Value<string>();
                }
            }
            
            if (string.IsNullOrEmpty(base64Data))
            {
                Debug.LogError("PNG data is null or empty");
                return;
            }
            
            // Decode base64 on background thread (this is safe)
            byte[] pngBytes = Convert.FromBase64String(base64Data);
            
            // Queue texture load to happen on main thread
            lock (queueLock)
            {
                pendingTextureLoads.Enqueue(pngBytes);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Exception in OnSigil: {ex.Message}\n{ex.StackTrace}");
        }
    }

    void LoadTextureOnMainThread(byte[] pngBytes)
    {
        try
        {
            bool pngLoaded = sigilPNGTex.LoadImage(pngBytes);
            
            if (pngLoaded)
            {
                // Update ScriptableObject with current sigil data
                currentSigilData.sigilPhrase = pendingSigilPhrase;
                currentSigilData.sigilCode = pendingSigilCode;
                currentSigilData.pngTexture = CopyTexture(sigilPNGTex);
                
                // Call ShowSigil on UniState directly
                UniState.Instance.ShowSigil(currentSigilData);
                
                // Clear pending data
                pendingSigilPhrase = null;
                pendingSigilCode = null;
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

    void OnMindMoment(SocketIOResponse response)
    {
        Debug.Log("OnMindMoment");
        var data = response.GetValue<JObject>();
        
        //mindMomentText.text = data["mindMoment"].Value<string>();
        Debug.Log("Mind Moment: " + data["mindMoment"].Value<string>());
        Debug.Log("Mind Moment Sigil Phrase: " + data["sigilPhrase"].Value<string>());
    }

    void OnPerceptReceived(SocketIOResponse response)
    {
        try
        {
            var data = response.GetValue<JObject>();
            var perceptDataObj = data["data"] as JObject;
            
            if (perceptDataObj != null)
            {
                // Extract common fields: sigilPhrase and drawCalls
                string sigilPhrase = perceptDataObj["sigilPhrase"]?.Value<string>();
                string drawCalls = perceptDataObj["drawCalls"]?.ToString(); // Convert JToken to string
                
                // Create PerceptData struct
                PerceptData percept = new PerceptData
                {
                    sessionId = data["sessionId"]?.Value<string>(),
                    type = data["type"]?.Value<string>(),
                    timestamp = data["timestamp"]?.Value<string>(),
                    sigilPhrase = sigilPhrase,
                    drawCalls = drawCalls,
                    pngTexture = null // Will be set after texture loads
                };
                
                // Extract PNG data if present (nested in data.pngData)
                var dataObj = perceptDataObj["data"] as JObject;
                string base64Data = null;
                if (dataObj != null)
                {
                    var png = dataObj["pngData"];
                    if (png != null)
                    {
                        if (png.Type == JTokenType.String)
                        {
                            base64Data = png.Value<string>();
                        }
                        else if (png.Type == JTokenType.Object)
                        {
                            base64Data = png["data"]?.Value<string>();
                        }
                    }
                }
                
                // Also check if pngData is directly on perceptDataObj for backwards compatibility
                if (string.IsNullOrEmpty(base64Data))
                {
                    var png = perceptDataObj["pngData"];
                    if (png != null)
                    {
                        if (png.Type == JTokenType.String)
                        {
                            base64Data = png.Value<string>();
                        }
                        else if (png.Type == JTokenType.Object)
                        {
                            base64Data = png["data"]?.Value<string>();
                        }
                    }
                }
                
                if (!string.IsNullOrEmpty(base64Data))
                {
                    // Decode base64 on background thread (this is safe)
                    byte[] pngBytes = Convert.FromBase64String(base64Data);
                    
                    // Queue texture load to happen on main thread
                    lock (queueLock)
                    {
                        pendingPerceptTextures.Enqueue(new PendingPerceptTexture
                        {
                            pngBytes = pngBytes,
                            percept = percept
                        });
                    }
                }
                else
                {
                    // No texture data, pass percept immediately
                    UniState.Instance.AddPercept(percept);
                    Debug.Log($"Percept added (no texture): {percept.type} - {percept.sigilPhrase}");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Exception in OnPerceptReceived: {ex.Message}\n{ex.StackTrace}");
        }
    }
    
    void LoadPerceptTextureOnMainThread(byte[] pngBytes, PerceptData percept)
    {
        try
        {
            bool pngLoaded = perceptPNGTex.LoadImage(pngBytes);
            
            if (pngLoaded)
            {
                // Update percept with loaded texture
                Texture2D copiedTexture = CopyTexture(perceptPNGTex);
                percept.pngTexture = copiedTexture;
                
                Debug.Log($"Percept texture loaded: {percept.type} - {percept.sigilPhrase}, texture size: {copiedTexture?.width}x{copiedTexture?.height}, texture null: {copiedTexture == null}");
                
                // Pass to UniState
                UniState.Instance.AddPercept(percept);
                
                Debug.Log($"Percept added with texture: {percept.type} - {percept.sigilPhrase}, pngTexture null: {percept.pngTexture == null}");
            }
            else
            {
                Debug.LogError("Failed to load percept image data!");
                // Still add percept without texture
                UniState.Instance.AddPercept(percept);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Exception in LoadPerceptTextureOnMainThread: {ex.Message}\n{ex.StackTrace}");
            // Still add percept without texture on error
            UniState.Instance.AddPercept(percept);
        }
    }

    void OnPhase(SocketIOResponse response)
    {
        try
        {
            var data = response.GetValue<JObject>();
            string phase = data["phase"]?.Value<string>();
            
            if (!string.IsNullOrEmpty(phase))
            {
                Debug.Log($"OnPhase: {phase}");
                
                if (phase == "SIGILOUT")
                {
                    // Call HideSigil when SIGILOUT phase starts (replaces clearDisplay behavior)
                    UniState.Instance.HideSigil();
                }
                else if (phase == "SPOOL")
                {
                    // Call OnSpool when SPOOL phase starts
                    UniState.Instance.OnSpool();
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Exception in OnPhase: {ex.Message}\n{ex.StackTrace}");
        }
    }
}