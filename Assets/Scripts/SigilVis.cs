using UnityEngine;
using UnityEngine.VFX;
using UnityEngine.Rendering;
using Unity.Mathematics;
using TMPro;

[RequireComponent(typeof(VisualEffect))]
public class SigilVis : MonoBehaviour
{
    public string meshPropertyName = "PointCacheMesh";
    public string pointCountPropertyName = "PointCount";
    public string sigilTPropertyName = "SigilT";
    
    [SerializeField] int pointCount = 10000;
    [SerializeField] float scale = 1f;
    [SerializeField, Range(0f, 1f)] float alphaThreshold = 0.1f;
    
    public Camera textCam;
    public TMP_Text perceptTextCapture;
    public GameObject sigilPhraseQuad;
    
    private const int TEXTURE_SIZE = 512;
    private Texture2D sigilPhraseTexture;
    private Renderer sigilPhraseRenderer;
    private Material sigilPhraseMaterial;
    
    private VisualEffect vfx;
    private Mesh pointMesh;
    
    private void Awake()
    {
        vfx = GetComponent<VisualEffect>();
        
        pointMesh = new Mesh();
        pointMesh.indexFormat = IndexFormat.UInt32; // allow >65k
        
        vfx.SetMesh(meshPropertyName, pointMesh);
        vfx.SetInt(pointCountPropertyName, pointCount);
        
        // Pre-allocate texture for sigil phrase
        sigilPhraseTexture = new Texture2D(TEXTURE_SIZE, TEXTURE_SIZE, TextureFormat.RGBA32, false);
        sigilPhraseTexture.filterMode = FilterMode.Bilinear;
        sigilPhraseTexture.wrapMode = TextureWrapMode.Clamp;
        sigilPhraseRenderer = sigilPhraseQuad.GetComponent<Renderer>();
        sigilPhraseMaterial = sigilPhraseRenderer.sharedMaterial;
    }
    
    private void OnEnable()
    {
        if (UniState.Instance != null)
        {
            UniState.Instance.OnSigilStart += OnSigilStart;
        }
        Color col = sigilPhraseMaterial.color;
        col.a = 0;
        sigilPhraseMaterial.color = col;
    }
    
    private void OnDisable()
    {
        if (UniState.Instance != null)
        {
            UniState.Instance.OnSigilStart -= OnSigilStart;
        }

        Color col = sigilPhraseMaterial.color;
        col.a = 0;
        sigilPhraseMaterial.color = col;
    }

    private void Update()
    {
        vfx.SetFloat(sigilTPropertyName, UniState.Instance.SigilT);
        
        Color color = Color.white;
        color.a = math.smoothstep(0.5f, 1f, UniState.Instance.SigilT);
        sigilPhraseMaterial.color = color;
    }
    
    private void OnSigilStart()
    {
        if (UniState.Instance != null && UniState.Instance.currentSigilData != null)
        {
            SigilDataSO sigilData = UniState.Instance.currentSigilData;
            if (sigilData.pngTexture != null)
            {
                GeneratePointsFromTexture(sigilData.pngTexture);
            }
            
            // Render sigil phrase to texture
            if (!string.IsNullOrEmpty(sigilData.sigilPhrase))
            {
                RenderSigilPhraseToTexture(sigilData.sigilPhrase);
            }
        }
    }
    
    private void RenderSigilPhraseToTexture(string sigilPhrase)
    {
        if (textCam == null || perceptTextCapture == null || sigilPhraseQuad == null)
        {
            Debug.LogWarning("SigilVis: Missing textCam, perceptTextCapture, or sigilPhraseQuad references");
            return;
        }
        
        // Set text on the text capture component
        perceptTextCapture.text = sigilPhrase;
        
        // Force camera render
        textCam.Render();
        
        // Copy RenderTexture to Texture2D
        if (textCam.targetTexture != null)
        {
            Graphics.CopyTexture(textCam.targetTexture, sigilPhraseTexture);
            
            // Assign texture to SigilPhrase quad material
            sigilPhraseMaterial.SetTexture("_MainTex", sigilPhraseTexture);
            Color col = sigilPhraseMaterial.color;
            col.a = 0;
            sigilPhraseMaterial.color = col;
        }
    }
    
    private void GeneratePointsFromTexture(Texture2D texture)
    {
        if (texture == null) return;
        
        // Read texture pixels (must be readable)
        Color[] pixels = texture.GetPixels();
        int width = texture.width;
        int height = texture.height;
        
        // Collect all non-alpha pixel positions
        System.Collections.Generic.List<Vector2> validPixels = new System.Collections.Generic.List<Vector2>();
        
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = y * width + x;
                if (pixels[index].a > alphaThreshold)
                {
                    // Store as UV coordinates (0-1)
                    validPixels.Add(new Vector2((float)x / width, (float)y / height));
                }
            }
        }
        
        if (validPixels.Count == 0)
        {
            Debug.LogWarning("No valid pixels found in texture");
            return;
        }
        
        // Scatter pointCount over valid pixels
        Vector3[] points = new Vector3[pointCount];
        
        for (int i = 0; i < pointCount; i++)
        {
            // Randomly select a valid pixel
            Vector2 uv = validPixels[UnityEngine.Random.Range(0, validPixels.Count)];
            
            // Convert UV to world position
            // Center around origin and scale
            float x = (uv.x - 0.5f) * scale;
            float y = (uv.y - 0.5f) * scale;
            points[i] = new Vector3(x, y, 0f);
        }
        
        // Update mesh
        pointMesh.vertices = points;
        
        if (pointMesh.GetIndexCount(0) == 0)
        {
            int[] indices = new int[pointCount];
            for (int i = 0; i < pointCount; i++)
            {
                indices[i] = i;
            }
            pointMesh.SetIndices(indices, MeshTopology.Points, 0, false);
        }
        
        pointMesh.UploadMeshData(false);
        vfx.SetMesh(meshPropertyName, pointMesh);
        vfx.SetInt(pointCountPropertyName, pointCount);
    }
    
    
}

