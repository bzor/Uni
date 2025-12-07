using System;
using UnityEngine;
using Unity.Mathematics;
using Random = UnityEngine.Random;

public class Percept : MonoBehaviour
{
    public bool active { get; private set; }
    
    private GameObject phraseQuad;
    private Renderer phraseRenderer;
    private Material phraseMaterialInstance;
    private float fadeInDuration = 1.4f;
    private float fadeOutDuration = 1f;
    private float holdDuration = 5.0f;
    
    private float animationTimer = 0f;
    private enum AnimationState { None, FadeIn, Hold, FadeOut }
    private AnimationState currentState = AnimationState.None;

    [HideInInspector]
    public Vector3 phrasePos;
    private Transform phraseTransform;

    [HideInInspector]
    public Vector3 spherePos;
    [HideInInspector]
    public float sphereScaleT = 0f;
    [HideInInspector]
    public float sphereScaleMax = 0.6f;
    
    private Vector3 sphereBasePos;
    private Transform sphereTransform;
    private Renderer sphereRenderer;
    private Material sphereMaterialInstance;

    private Transform sigilDotTransform;

    private Transform phraseDotTransform;
    private Vector3 phraseDotOffset;

    private Transform lineTransform;

    private float animTick = 0;
    
    private void Awake()
    {
        // Find Phrase child (should be a quad with a renderer)
        phraseTransform = transform.Find("Phrase");
        if (phraseTransform != null)
        {
            phraseQuad = phraseTransform.gameObject;
            phraseRenderer = phraseQuad.GetComponent<Renderer>();
            
            if (phraseRenderer != null && phraseRenderer.material != null)
            {
                // Create material instance to avoid sharing textures across percepts
                phraseMaterialInstance = new Material(phraseRenderer.material);
                phraseRenderer.material = phraseMaterialInstance;
            }
        }

        sphereTransform = transform.Find("Sphere");
        if (sphereTransform != null)
        {
            sphereRenderer = sphereTransform.GetComponent<Renderer>();
            if (sphereRenderer != null && sphereRenderer.material != null)
            {
                // Create material instance to avoid sharing textures across percepts
                sphereMaterialInstance = new Material(sphereRenderer.material);
                sphereRenderer.material = sphereMaterialInstance;
                Debug.Log($"Percept Awake: Sphere material instance created for {gameObject.name}");
            }
            else
            {
                Debug.LogWarning($"Percept Awake: Sphere renderer or material is null for {gameObject.name}, renderer={sphereRenderer != null}, material={sphereRenderer?.material != null}");
            }
        }
        else
        {
            Debug.LogWarning($"Percept Awake: Sphere transform not found for {gameObject.name}");
        }
        
        phraseDotTransform = transform.Find("PhraseDot");
        lineTransform = transform.Find("Line");
        sigilDotTransform = sphereTransform.Find("SigilDot");
        
        active = false;
        SetAlpha(0f);
    }
    
    public void StartAnimation()
    {
        active = true;
        animationTimer = 0f;
        animTick = Random.Range(0f, Mathf.PI * 2f);
        currentState = AnimationState.FadeIn;
        phraseQuad.SetActive(true);
        Color col = Color.white;
        col.a = 0;
        phraseMaterialInstance.color = col;
    }

    public void StopAnimation()
    {
        currentState = AnimationState.FadeOut;
        animationTimer = 0f;
        sphereScaleT = 1f;
    }

    private void OnEnable()
    {
        Color col = Color.white;
        col.a = 0;
        phraseMaterialInstance.color = col;
    }

    private void OnDisable()
    {
        Color col = Color.white;
        col.a = 0;
        phraseMaterialInstance.color = col;
    }

    private void Update()
    {
        if (currentState == AnimationState.None) return;
        
        animationTimer += Time.deltaTime;
        animTick += Time.smoothDeltaTime;

        phrasePos.y += Time.smoothDeltaTime * 0.3f;
        sphereBasePos.y += Time.smoothDeltaTime * 0.35f;
        phraseTransform.position = phrasePos;

        var animT = 0f;

        switch (currentState)
        {
            case AnimationState.FadeIn:
                if (animationTimer >= fadeInDuration)
                {
                    SetAlpha(1f);
                    currentState = AnimationState.Hold;
                    animationTimer = 0f;
                    sphereScaleT = 1f;
                    animT = 1f;
                }
                else
                {
                    animT = animationTimer / fadeInDuration;
                    SetAlpha(math.smoothstep(0.6f, 1f, animT));
                }
                break;
                
            case AnimationState.Hold:
                animT = 1f;
                if (animationTimer >= holdDuration)
                {
                    currentState = AnimationState.FadeOut;
                    animationTimer = 0f;
                    sphereScaleT = 1f;
                }
                break;
                
            case AnimationState.FadeOut:
                if (animationTimer >= fadeOutDuration)
                {
                    SetAlpha(0f);
                    currentState = AnimationState.None;
                    active = false;
                    phraseQuad.SetActive(false);
                    gameObject.SetActive(false);
                    animT = 0f;
                }
                else
                {
                    animT = 1f - animationTimer / fadeOutDuration;
                    SetAlpha(math.smoothstep(0.6f, 1f, animT));
                }
                break;
        }

        var scale = Vector3.one;
        float theta = math.sin(animTick * 0.3f);
        float swayAmp = 1f;
        spherePos = new Vector3(sphereBasePos.x + math.sin(theta) * 0.4f, sphereBasePos.y, sphereBasePos.z + - 0.3f + math.cos(theta * 1.354f) * 0.3f);
        sphereTransform.position = spherePos;
        sphereScaleT = math.smoothstep(0, 0.2f, animT);
        sphereTransform.localScale = sphereScaleMax * new Vector3(sphereScaleT, sphereScaleT, sphereScaleT);

        scale = 0.05f * math.smoothstep(0.1f, 0.3f, animT) * Vector3.one;
        sigilDotTransform.localScale = scale;

        var phraseDotPos = phrasePos + phraseDotOffset;
        phraseDotTransform.position = phraseDotPos;
        scale = math.smoothstep(0.4f, 0.6f, animT) * 0.025f * Vector3.one;
        phraseDotTransform.localScale = scale;

        lineTransform.position = sigilDotTransform.position;
        lineTransform.LookAt(phraseDotPos);
        var lineW = 0.008f;
        scale = new Vector3(lineW, lineW, 1f);
        scale.z = math.smoothstep(0.3f, 0.5f, animT) * math.distance(sigilDotTransform.position, phraseDotPos);
        lineTransform.localScale = scale;
    }
    
    private void SetAlpha(float alpha)
    {
        if (phraseMaterialInstance != null)
        {
            Color color = phraseMaterialInstance.color;
            color.a = alpha;
            phraseMaterialInstance.color = color;
        }
    }
    
    public void SetTexture(Texture2D texture, float textWidth)
    {
        if (phraseMaterialInstance != null)
        {
            //phraseMaterialInstance.mainTexture = texture;
            phraseMaterialInstance.SetTexture("_MainTex", texture);
        }
        phraseDotOffset = new Vector3(-textWidth, 0.9f, 0);
    }
    
    public void SetSphereTexture(Texture2D texture)
    {
        if (sphereMaterialInstance != null)
        {
            sphereMaterialInstance.mainTexture = texture;
            sphereMaterialInstance.color = Color.white;
            Debug.Log($"SetSphereTexture called: texture={texture != null}, texture size={texture?.width}x{texture?.height}, material={sphereMaterialInstance != null}");
        }
        else
        {
            Debug.LogError($"SetSphereTexture failed: sphereMaterialInstance is null! sphereRenderer={sphereRenderer != null}, sphereTransform={sphereTransform != null}");
        }
    }
    
    public void SetStart(Vector3 phrasePos, Vector3 spherePos)
    {
        this.phrasePos = phrasePos;
        sphereBasePos = spherePos;
        sphereScaleT = 0f;
        animTick = Random.Range(0f, Mathf.PI * 2f);
        var pos = new Vector3((spherePos.x > 0f) ? 0.5f : -0.5f, 0f, 0f);
        sigilDotTransform.localPosition = pos;
    }
}

