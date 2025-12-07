using UnityEngine;
using Unity.Mathematics;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public class ThoughtProcess : MonoBehaviour
{
    public int sparksCount = 64;
    public GameObject sparkPrefab;
    public ThoughtProcessSettings settings;
    
    [Header("SDF Control")]
    public SDFControl sdfControl;
    
    private float3[] sparkPositions;
    private float3[] sparkVelocities;
    private float3[] sparkStartPositions; // Store starting positions
    private float3[] sparkIdlePositions; // Idle state positions
    private float3[] sparkListeningPositions; // Listening state positions
    private float3[] sparkSigilPositions; // Listening state positions
    private GameObject[] sparkInstances;
    private Material sparkMaterial; // Shared material for all sparks
    
    public Material SparkMaterial => sparkMaterial;
    public Color SparkColor => settings != null ? settings.sparkColor : Color.white;

    private UniStateType currentState;

    private float idleTick = 0;
    private float perceptTick = 0;
    
    private float currentRadius = 0.3f; // Current radius lerped between idle and listening
    
    private Vector3[] sigilPoints;
    private SigilDataSO currentSigilData;

    private void Start()
    {
        currentState = UniStateType.IDLE;
        
        sparkPositions = new float3[sparksCount];
        sparkVelocities = new float3[sparksCount];
        sparkStartPositions = new float3[sparksCount];
        sparkIdlePositions = new float3[sparksCount];
        sparkListeningPositions = new float3[sparksCount];
        sparkSigilPositions = new float3[sparksCount];
        sparkInstances = new GameObject[sparksCount];
        
        // Get the shared material from the nested "Sphere" GameObject in the prefab
        if (sparkPrefab != null)
        {
            Transform sphereTransform = sparkPrefab.transform.Find("Sphere");
            if (sphereTransform != null)
            {
                Renderer prefabRenderer = sphereTransform.GetComponent<Renderer>();
                if (prefabRenderer != null)
                {
                    sparkMaterial = prefabRenderer.sharedMaterial;
                }
            }
        }
        
        for (int i = 0; i < sparksCount; i++)
        {
            Vector3 randomSphere = UnityEngine.Random.insideUnitSphere * 0.2f;
            sparkStartPositions[i] = new float3(randomSphere.x, randomSphere.y, randomSphere.z) + new float3(0f, 0f, 0.8f);

            sparkPositions[i] = sparkStartPositions[i];
            sparkVelocities[i] = UnityEngine.Random.insideUnitSphere * 0.5f;
            sparkInstances[i] = Instantiate(sparkPrefab, sparkPositions[i], Quaternion.identity);
            
            // Get material from nested "Sphere" GameObject in first instance if not already set
            if (sparkMaterial == null && i == 0)
            {
                Transform sphereTransform = sparkInstances[i].transform.Find("Sphere");
                if (sphereTransform != null)
                {
                    Renderer renderer = sphereTransform.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        sparkMaterial = renderer.sharedMaterial;
                    }
                }
            }
        }
        
        // Set color and emission on the material after all sparks are instantiated
        if (sparkMaterial != null && settings != null)
        {
            sparkMaterial.color = settings.sparkColor;
            sparkMaterial.SetColor("_EmissionColor", settings.sparkEmission);
            sparkMaterial.EnableKeyword("_EMISSION");
        }
    }
    
    private void Update()
    {
        if (settings != null && sparkMaterial != null)
        {
            float colorLerp = math.lerp(0.02f, 1f, math.smoothstep(0.6f, 1.0f, 1f - UniState.Instance.IdleT));
            sparkMaterial.color = settings.sparkColor * colorLerp;
            sparkMaterial.SetColor("_EmissionColor", settings.sparkEmission * colorLerp);
        }
        
        // Update state logic functions based on their T values (allows multiple states during transitions)
        if (UniState.Instance.IdleT > 0f)
        {
            IdleStateLogic();
        }
        
        if (UniState.Instance.PerceptT > 0f)
        {
            PerceptStateLogic();
        }
        
        if (UniState.Instance.SigilT > 0f)
        {
            SigilStateLogic();
        }
        
        // Lerp radius between idle and listening based on ListeningT
        float listeningPosT = math.smoothstep(0f, 1f, UniState.Instance.PerceptT);
        float listeningRadiusT = math.smoothstep(0.8f, 1f, UniState.Instance.PerceptT);
        currentRadius = math.lerp(settings.idleRadius, settings.listeningRadius, listeningRadiusT);
        
        // Lerp between idle and listening positions based on ListeningT
        for (int i = 0; i < sparksCount; i++)
        {
            sparkPositions[i] = math.lerp(sparkIdlePositions[i], sparkListeningPositions[i], listeningPosT);
            sparkPositions[i] = math.lerp(sparkPositions[i], new float3(0, 0, -1.5f) + sparkStartPositions[i] * 0.5f, math.smoothstep(0f, 0.5f, UniState.Instance.SigilT));
            sparkPositions[i] = math.lerp(sparkPositions[i], sparkSigilPositions[i], math.smoothstep(0.4f, 0.8f, UniState.Instance.SigilT));
            sparkInstances[i].transform.position = sparkPositions[i];
            sparkInstances[i].transform.localScale = settings != null ? Vector3.one * settings.sparkScale : Vector3.one * 0.03f;
        }
        
        /*
        // Control SDF transition based on sigilT
        if(UniState.Instance.currentState == UniStateType.SIGIL)
        {
            sdfControl.SetTransition(math.smoothstep(0f, 0.4f, 1f - UniState.Instance.SigilT));
        }
        else
        {
            sdfControl.SetTransition(math.smoothstep(0f, 1f, 1f - UniState.Instance.SigilT));
        }
        */        
    }

    private void OnEnable()
    {
        // Subscribe to state changes
        if (UniState.Instance != null)
        {
            UniState.Instance.OnStateChanged += OnStateChanged;
        }
    }

    private void OnDisable()
    {
        // Unsubscribe from state changes
        if (UniState.Instance != null)
        {
            UniState.Instance.OnStateChanged -= OnStateChanged;
        }
    }
    
    private void OnStateChanged(UniStateType newState)
    {
        UniStateType previousState = currentState;
        currentState = newState;
        
        switch (newState)
        {
            case UniStateType.IDLE:
                OnIdleState();
                break;
            case UniStateType.SIGIL:
                OnSigilState();
                break;
            case UniStateType.PERCEPTS:
                OnPerceptState();
                break;
        }
    }
    
    private void OnIdleState()
    {
        // Called when entering IDLE state
    }

    private void IdleStateLogic()
    {
        idleTick += Time.deltaTime * settings.idleSpeed;
        
        float3 idlePos = new float3(settings.idlePosition.x, settings.idlePosition.y, settings.idlePosition.z);
        
        for (int i = 0; i < sparksCount; i++)
        {
            // Offset phase by particle ID to create cloud distribution
            float phaseOffset = (float)i / sparksCount * math.PI * 2.0f * settings.phaseOffsetScale;
            
            // Calculate Lissajous pattern in polar coordinates (spherical)
            // Theta: azimuthal angle (around z-axis)
            float theta = settings.idleFreqX * idleTick + phaseOffset * settings.phaseMultiplierX;
            // Phi: polar angle (from z-axis)
            float phi = settings.idleFreqY * idleTick + phaseOffset * settings.phaseMultiplierY;
            // Radius variation (optional, can use freqZ for this)
            float r = currentRadius * (1.0f + math.sin(settings.idleFreqZ * idleTick + phaseOffset * settings.phaseMultiplierZ) * 0.1f);
            
            // Convert spherical to Cartesian coordinates
            // x = r * sin(phi) * cos(theta)
            // y = r * sin(phi) * sin(theta)
            // z = r * cos(phi)
            float sinPhi = math.sin(phi);
            float3 lissajousOffset = new float3(
                r * sinPhi * math.cos(theta),
                r * sinPhi * math.sin(theta),
                r * math.cos(phi)
            );
            
            // Set idle position to idle position + Lissajous offset
            sparkIdlePositions[i] = idlePos + lissajousOffset;
        }
    }
    
    private void PerceptStateLogic()
    {
        perceptTick += Time.deltaTime * settings.listeningSpeed;
        
        float3 listeningPos = new float3(settings.listeningPosition.x, settings.listeningPosition.y, settings.listeningPosition.z);
        
        for (int i = 0; i < sparksCount; i++)
        {
            // Offset phase by particle ID to create cloud distribution
            float phaseOffset = (float)i / sparksCount * math.PI * 2.0f * settings.phaseOffsetScale;
            
            // Calculate Lissajous pattern in polar coordinates (spherical)
            // Theta: azimuthal angle (around z-axis)
            float theta = settings.listeningFreqX * perceptTick + phaseOffset * settings.phaseMultiplierX;
            // Phi: polar angle (from z-axis)
            float phi = settings.listeningFreqY * perceptTick + phaseOffset * settings.phaseMultiplierY;
            // Radius variation (optional, can use freqZ for this)
            float r = currentRadius * (1.0f + math.sin(settings.listeningFreqZ * perceptTick + phaseOffset * settings.phaseMultiplierZ) * 0.1f);
            
            // Convert spherical to Cartesian coordinates
            // x = r * sin(phi) * cos(theta)
            // y = r * sin(phi) * sin(theta)
            // z = r * cos(phi)
            float sinPhi = math.sin(phi);
            float3 lissajousOffset = new float3(
                r * sinPhi * math.cos(theta),
                r * sinPhi * math.sin(theta),
                r * math.cos(phi)
            );
            
            // Set listening position to listening position + Lissajous offset
            sparkListeningPositions[i] = listeningPos + lissajousOffset;
        }
    }
    
    private void SigilStateLogic()
    {
        if (sigilPoints == null || sigilPoints.Length == 0) return;
        
        int halfSparksCount = sparksCount / 2;
        
        for (int i = 0; i < sparksCount; i++)
        {
            int sigilPointIndex = i % sigilPoints.Length;
            float3 basePos = new float3(sigilPoints[sigilPointIndex].x, sigilPoints[sigilPointIndex].y, sigilPoints[sigilPointIndex].z);
            
            // First half get positive z offset, second half get negative z offset
            float zOffsetValue = settings != null ? settings.sigilZOffset : 0.1f;
            float zOffset = i < halfSparksCount ? zOffsetValue : -zOffsetValue;
            sparkSigilPositions[i] = new float3(basePos.x, basePos.y, basePos.z + zOffset);
        }
    }

    private void OnSigilState()
    {
    }

    private void OnPerceptState()
    {
        // Handle LISTENING state
    }
}

