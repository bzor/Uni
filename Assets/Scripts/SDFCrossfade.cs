using UnityEngine;
using Unity.Mathematics;

public class SDFCrossfade : MonoBehaviour
{
    [SerializeField] private Material cloudMaterial;
    [SerializeField] private float cycleDuration = 8.0f; // Total duration for one full cycle (Tex1 -> Tex2 -> Tex1)
    [SerializeField] private float crossfadeDuration = 2.0f; // Duration of the crossfade itself

    // Formed settings (when crossfade is at 0 or 1)
    [Header("Formed Settings (Crossfade = 0 or 1)")]
    [SerializeField] private float formedSDFScale = 1.0f;
    [SerializeField] private float formedSDFThreshold = 0.0f;
    [SerializeField] private float formedSDFMultiplier = 1.0f;
    [SerializeField] private float formedSDFDistortionScale = 0.1f;
    [SerializeField] private float formedSDFDistortionMinThreshold = 0.0f;
    [SerializeField] private float formedSDFDistortionMaxThreshold = 1.0f;
    [SerializeField] private Color formedSDFColor = Color.white;
    [SerializeField] private float formedColorMultiplier = 1.0f;

    // Transition settings (when crossfade is at 0.5)
    [Header("Transition Settings (Crossfade = 0.5)")]
    [SerializeField] private float transitionSDFScale = 1.5f;
    [SerializeField] private float transitionSDFThreshold = 0.2f;
    [SerializeField] private float transitionSDFMultiplier = 0.5f;
    [SerializeField] private float transitionSDFDistortionScale = 0.3f;
    [SerializeField] private float transitionSDFDistortionMinThreshold = 0.2f;
    [SerializeField] private float transitionSDFDistortionMaxThreshold = 1.5f;
    [SerializeField] private Color transitionSDFColor = Color.yellow;
    [SerializeField] private float transitionColorMultiplier = 0.5f;

    private float timer = 0.0f;
    
    // Shader property IDs
    private static readonly int SDFCrossfadeProperty = Shader.PropertyToID("_SDFCrossfade");
    private static readonly int SDFScaleProperty = Shader.PropertyToID("_SDFScale");
    private static readonly int SDFThresholdProperty = Shader.PropertyToID("_SDFThreshold");
    private static readonly int SDFMultiplierProperty = Shader.PropertyToID("_SDFMultiplier");
    private static readonly int SDFDistortionScaleProperty = Shader.PropertyToID("_SDFDistortionScale");
    private static readonly int SDFDistortionMinThresholdProperty = Shader.PropertyToID("_SDFDistortionMinThreshold");
    private static readonly int SDFDistortionMaxThresholdProperty = Shader.PropertyToID("_SDFDistortionMaxThreshold");
    private static readonly int SDFColorProperty = Shader.PropertyToID("_SDFColor");
    private static readonly int ColorMultiplierProperty = Shader.PropertyToID("_ColorMultiplier");

    private void Start()
    {
        if (cloudMaterial == null)
        {
            var renderer = GetComponent<Renderer>();
            if (renderer != null)
            {
                cloudMaterial = renderer.material;
            }
        }

        if (cloudMaterial != null)
        {
            // Initialize to formed settings
            SetFormedSettings();
            cloudMaterial.SetFloat(SDFCrossfadeProperty, 0.0f);
        }
    }

    private void Update()
    {
        if (cloudMaterial == null) return;

        timer += Time.deltaTime;

        // Calculate phase in a full back-and-forth cycle (0 to cycleDuration * 2)
        float fullCycleTime = cycleDuration * 2.0f;
        float phase = timer % fullCycleTime;

        float crossfadeValue;

        // Phase 1: Hold Texture 1, then crossfade to Texture 2
        if (phase < cycleDuration)
        {
            float holdDuration = cycleDuration - crossfadeDuration;
            if (phase < holdDuration)
            {
                // Holding Texture 1
                crossfadeValue = 0.0f;
            }
            else
            {
                // Crossfading from Texture 1 to Texture 2
                float progress = (phase - holdDuration) / crossfadeDuration;
                crossfadeValue = progress;
            }
        }
        // Phase 2: Hold Texture 2, then crossfade back to Texture 1
        else
        {
            float holdDuration = cycleDuration - crossfadeDuration;
            float phaseInSecondHalf = phase - cycleDuration;
            if (phaseInSecondHalf < holdDuration)
            {
                // Holding Texture 2
                crossfadeValue = 1.0f;
            }
            else
            {
                // Crossfading from Texture 2 to Texture 1
                float progress = (phaseInSecondHalf - holdDuration) / crossfadeDuration;
                crossfadeValue = 1.0f - progress;
            }
        }

        // Calculate transition factor: 0 at crossfade 0/1, 1 at crossfade 0.5
        // Use smoothstep for smoother transitions
        float transitionFactor = 1.0f - Mathf.Abs(crossfadeValue - 0.5f) * 2.0f;
        transitionFactor = Mathf.SmoothStep(0.0f, 1.0f, transitionFactor);

        // Lerp all SDF properties from formed to transition based on transitionFactor
        float currentSDFScale = Mathf.Lerp(formedSDFScale, transitionSDFScale, transitionFactor);
        float currentSDFThreshold = Mathf.Lerp(formedSDFThreshold, transitionSDFThreshold, transitionFactor);
        float currentSDFMultiplier = Mathf.Lerp(formedSDFMultiplier, transitionSDFMultiplier, transitionFactor);
        float currentSDFDistortionScale = Mathf.Lerp(formedSDFDistortionScale, transitionSDFDistortionScale, transitionFactor);
        float currentSDFDistortionMinThreshold = Mathf.Lerp(formedSDFDistortionMinThreshold, transitionSDFDistortionMinThreshold, transitionFactor);
        float currentSDFDistortionMaxThreshold = Mathf.Lerp(formedSDFDistortionMaxThreshold, transitionSDFDistortionMaxThreshold, transitionFactor);
        Color currentSDFColor = Color.Lerp(formedSDFColor, transitionSDFColor, transitionFactor);
        float currentColorMultiplier = Mathf.Lerp(formedColorMultiplier, transitionColorMultiplier, math.smoothstep(0.8f, 1.0f, transitionFactor));

        // Apply all properties to material
        cloudMaterial.SetFloat(SDFCrossfadeProperty, crossfadeValue);
        cloudMaterial.SetFloat(SDFScaleProperty, currentSDFScale);
        cloudMaterial.SetFloat(SDFThresholdProperty, currentSDFThreshold);
        cloudMaterial.SetFloat(SDFMultiplierProperty, currentSDFMultiplier);
        cloudMaterial.SetFloat(SDFDistortionScaleProperty, currentSDFDistortionScale);
        cloudMaterial.SetFloat(SDFDistortionMinThresholdProperty, currentSDFDistortionMinThreshold);
        cloudMaterial.SetFloat(SDFDistortionMaxThresholdProperty, currentSDFDistortionMaxThreshold);
        cloudMaterial.SetColor(SDFColorProperty, currentSDFColor);
        cloudMaterial.SetFloat(ColorMultiplierProperty, currentColorMultiplier);
    }

    private void SetFormedSettings()
    {
        if (cloudMaterial == null) return;

        cloudMaterial.SetFloat(SDFScaleProperty, formedSDFScale);
        cloudMaterial.SetFloat(SDFThresholdProperty, formedSDFThreshold);
        cloudMaterial.SetFloat(SDFMultiplierProperty, formedSDFMultiplier);
        cloudMaterial.SetFloat(SDFDistortionScaleProperty, formedSDFDistortionScale);
        cloudMaterial.SetFloat(SDFDistortionMinThresholdProperty, formedSDFDistortionMinThreshold);
        cloudMaterial.SetFloat(SDFDistortionMaxThresholdProperty, formedSDFDistortionMaxThreshold);
        cloudMaterial.SetColor(SDFColorProperty, formedSDFColor);
        cloudMaterial.SetFloat(ColorMultiplierProperty, formedColorMultiplier);
    }

    private void OnDestroy()
    {
        if (cloudMaterial != null)
        {
            cloudMaterial.SetFloat(SDFCrossfadeProperty, 0.0f);
            //SetFormedSettings();
        }
    }
}
