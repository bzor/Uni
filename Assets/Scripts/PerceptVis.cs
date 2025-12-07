using TMPro;
using UnityEngine;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine.VFX;
using UnityEngine.VFX.Utility;
using Random = Unity.Mathematics.Random;

public class PerceptVis : MonoBehaviour
{
    private const int POOL_SIZE = 8;
    private const int TEXTURE_SIZE = 512;
    
    public Camera textCam;
    public GameObject perceptNodePrefab;
    public TMP_Text perceptTextCapture;

    public VisualEffect vfxGraph;  

    public float phraseStartYPos = -2f;
    public float perceptSigilScaleMax = 0.6f;
    public float phraseTextOffsetMult = 0.01f;
    
    private PerceptData latestPercept;
    private Queue<PerceptData> pendingPercepts = new Queue<PerceptData>();
    private object queueLock = new object();
    
    // Pool management
    private Percept[] perceptPool = new Percept[POOL_SIZE];
    private Texture2D[] texturePool = new Texture2D[POOL_SIZE];
    private int nextAvailableIndex = 0;
    private bool useLeftPosition = true; // Alternates between left and right
    private const float Y_OVERLAP_THRESHOLD = 0.5f; // Minimum y distance between percepts

    private ExposedProperty[] perceptProps;

    private void Awake()
    {
        perceptProps = new ExposedProperty[8];
        perceptProps[0] = "PerceptPos0";
        perceptProps[1] = "PerceptPos1";
        perceptProps[2] = "PerceptPos2";
        perceptProps[3] = "PerceptPos3";
        perceptProps[4] = "PerceptPos4";
        perceptProps[5] = "PerceptPos5";
        perceptProps[6] = "PerceptPos6";
        perceptProps[7] = "PerceptPos7";
        
        // Pre-allocate textures (GC-friendly)
        for (int i = 0; i < POOL_SIZE; i++)
        {
            texturePool[i] = new Texture2D(TEXTURE_SIZE, TEXTURE_SIZE, TextureFormat.RGBA32, false);
            texturePool[i].filterMode = FilterMode.Bilinear;
            texturePool[i].wrapMode = TextureWrapMode.Clamp;
        }
        
        // Instantiate prefab pool
        if (perceptNodePrefab != null)
        {
            for (int i = 0; i < POOL_SIZE; i++)
            {
                GameObject instance = Instantiate(perceptNodePrefab, transform);
                instance.name = $"Percept_{i}";
                perceptPool[i] = instance.GetComponent<Percept>();
                instance.SetActive(false);
                
                if (perceptPool[i] == null)
                {
                    Debug.LogError($"Percept prefab missing Percept component at index {i}");
                }
            }
        }
    }
    
    private void OnEnable()
    {
        UniState.Instance.OnPercept += HandlePercept;
        UniState.Instance.OnSigilStart += OnSigilStart;
    }

    private void OnDisable()
    {
        if (UniState.Instance != null)
        {
            UniState.Instance.OnPercept -= HandlePercept;
            UniState.Instance.OnSigilStart -= OnSigilStart;
        }
    }

    private void HandlePercept(PerceptData percept)
    {
        // Queue percept update to be processed on main thread
        lock (queueLock)
        {
            pendingPercepts.Enqueue(percept);
        }
    }

    private void OnSigilStart()
    {
        // Stop animation on all active percepts
        for (int i = 0; i < POOL_SIZE; i++)
        {
            if (perceptPool[i] != null && perceptPool[i].active)
            {
                perceptPool[i].StopAnimation();
            }
        }
    }

    private void Update()
    {
        // Process pending percept updates on main thread
        lock (queueLock)
        {
            while (pendingPercepts.Count > 0)
            {
                PerceptData percept = pendingPercepts.Dequeue();
                ProcessPerceptOnMainThread(percept);
            }
        }

        for (int i = 0; i < POOL_SIZE; i++)
        {
            if (perceptPool[i].active)
            {
                vfxGraph.SetVector4(perceptProps[i], new Vector4(perceptPool[i].spherePos.x, perceptPool[i].spherePos.y, perceptPool[i].spherePos.z, perceptPool[i].sphereScaleT));
            }
            else
            {
                vfxGraph.SetVector4(perceptProps[i], new Vector4(0f, 0f, 0f, 0f));
            }
        }
    }

    private void ProcessPerceptOnMainThread(PerceptData percept)
    {
        latestPercept = percept;
        Debug.Log($"Percept received: {percept.sigilPhrase} | Type: {percept.type} | Session: {percept.sessionId}");

        // Find next available inactive percept
        Percept availablePercept = FindNextInactivePercept();
        if (availablePercept == null)
        {
            Debug.LogWarning("No available percept in pool, skipping");
            return;
        }
        availablePercept.gameObject.SetActive(true);
        
        // Set initial position (alternate left/right)
        float xPos = useLeftPosition ? 1.7f : -1.7f;
        float yPos = phraseStartYPos;
        float sphereX = UnityEngine.Random.Range(0.2f, 0.6f) * (useLeftPosition ? 1f : -1f);
        useLeftPosition = !useLeftPosition; // Toggle for next time
        
        // Check for y overlaps with active percepts and adjust position
        yPos = FindNonOverlappingYPosition(yPos);
        
        // Set position on percept
        availablePercept.SetStart(new Vector3(xPos, yPos, 0f), new Vector3(sphereX, yPos + 0.5f, 0.4f));
        
        // Set PNG texture on sphere if available
        if (percept.pngTexture != null)
        {
            Debug.Log($"Setting sphere texture for percept: {percept.sigilPhrase}, texture size: {percept.pngTexture.width}x{percept.pngTexture.height}");
            availablePercept.SetSphereTexture(percept.pngTexture);
        }
        else
        {
            Debug.LogWarning($"Percept texture is null for: {percept.sigilPhrase}");
        }
        
        // Set text and render to texture for phrase quad
        perceptTextCapture.text = percept.sigilPhrase;
        
        // Force camera render
        textCam.Render();
        var phraseDotOffsetX = perceptTextCapture.textBounds.size.x * phraseTextOffsetMult * (useLeftPosition ? -1f : 1f);
        
        // Get the texture index for this percept
        int textureIndex = System.Array.IndexOf(perceptPool, availablePercept);
        
        // Copy RenderTexture to Texture2D using Graphics.CopyTexture (faster than ReadPixels)
        Graphics.CopyTexture(textCam.targetTexture, texturePool[textureIndex]);
        
        // Assign texture to percept phrase quad and start animation
        availablePercept.SetTexture(texturePool[textureIndex], phraseDotOffsetX);
        availablePercept.sphereScaleMax = perceptSigilScaleMax;
        availablePercept.StartAnimation();
    }
    
    private float FindNonOverlappingYPosition(float initialY)
    {
        float yPos = initialY;
        bool foundOverlap = true;
        int maxAttempts = 20; // Prevent infinite loop
        int attempts = 0;
        
        while (foundOverlap && attempts < maxAttempts)
        {
            foundOverlap = false;
            
            // Check all active percepts
            for (int i = 0; i < POOL_SIZE; i++)
            {
                if (perceptPool[i] != null && perceptPool[i].active)
                {
                    Vector3 otherPos = perceptPool[i].phrasePos;
                    
                    // Check if y positions are too close
                    if (Mathf.Abs(otherPos.y - yPos) < Y_OVERLAP_THRESHOLD)
                    {
                        foundOverlap = true;
                        // Move this one down by the threshold amount
                        yPos = otherPos.y - Y_OVERLAP_THRESHOLD;
                        break;
                    }
                }
            }
            
            attempts++;
        }
        
        return yPos;
    }
    
    private Percept FindNextInactivePercept()
    {
        // Circular search starting from nextAvailableIndex
        for (int i = 0; i < POOL_SIZE; i++)
        {
            int index = (nextAvailableIndex + i) % POOL_SIZE;
            if (perceptPool[index] != null && !perceptPool[index].active)
            {
                nextAvailableIndex = (index + 1) % POOL_SIZE;
                return perceptPool[index];
            }
        }
        return null;
    }
}
