using UnityEngine;
using Unity.Mathematics;
using TMPro;

public class SDFControl : MonoBehaviour
{
    [SerializeField] private Material cloudMaterial;

    [SerializeField] private TMP_Text sigilPhraseText;

    // Formed settings (when transitionValue = 1)
    [Header("Formed Settings (TransitionValue = 1)")]
    [SerializeField] private float formedSDFScale = 1.0f;
    [SerializeField] private float formedSDFThreshold = 0.0f;
    [SerializeField] private float formedSDFMultiplier = 1.0f;
    [SerializeField] private float formedSDFDistortionScale = 0.1f;
    [SerializeField] private float formedSDFDistortionMinThreshold = 0.0f;
    [SerializeField] private float formedSDFDistortionMaxThreshold = 1.0f;
    [SerializeField] private Color formedSDFColor = Color.white;
    [SerializeField] private float formedColorMultiplier = 1.0f;

    // Unformed settings (when transitionValue = 0)
    [Header("Unformed Settings (TransitionValue = 0)")]
    [SerializeField] private float unformedSDFScale = 1.5f;
    [SerializeField] private float unformedSDFThreshold = 0.2f;
    [SerializeField] private float unformedSDFMultiplier = 0.5f;
    [SerializeField] private float unformedSDFDistortionScale = 0.3f;
    [SerializeField] private float unformedSDFDistortionMinThreshold = 0.2f;
    [SerializeField] private float unformedSDFDistortionMaxThreshold = 1.5f;
    [SerializeField] private Color unformedSDFColor = Color.yellow;
    [SerializeField] private float unformedColorMultiplier = 0.5f;

    private float currentCrossfadeValue = 0.0f;
    private float currentTransitionFactor = 0.0f;
    
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
        if (cloudMaterial != null)
        {
            // Initialize to unformed settings
            SetUnformedSettings();
            cloudMaterial.SetFloat(SDFCrossfadeProperty, 0.0f);
            sigilPhraseText.fontSharedMaterial.SetFloat(Shader.PropertyToID("_SDFPhraseColorMultiplier"), 0.0f);
            sigilPhraseText.fontSharedMaterial.SetFloat(Shader.PropertyToID("_SDFPhraseMultiplier"), 0.0f);
            sigilPhraseText.fontSharedMaterial.SetFloat(Shader.PropertyToID("_SDFPhraseDistortionScale"), 0.5f);
        }
    }

    private void OnEnable()
    {
        // Subscribe to reset sigil event
        if (UniState.Instance != null)
        {
            UniState.Instance.OnSigilStart += OnSigilStart;
        }
    }

    private void OnDisable()
    {
        // Unsubscribe from reset sigil event
        if (UniState.Instance != null)
        {
            UniState.Instance.OnSigilStart -= OnSigilStart;
        }
    }

    private void OnSigilStart()
    {
        // Set sigil with current sigil data from UniState
        if (UniState.Instance != null && UniState.Instance.currentSigilData != null)
        {
            SigilDataSO sigilData = UniState.Instance.currentSigilData;
            if (sigilData.pngTexture != null && !string.IsNullOrEmpty(sigilData.sigilPhrase))
            {
                SetSigil(sigilData.pngTexture, sigilData.sigilPhrase);
            }
        }
    }

    public void SetSigil(Texture2D sdfTex, string sigilPhrase)
    {
        cloudMaterial.SetTexture("_SDFTex1", sdfTex);
        currentCrossfadeValue = 0f;
        sigilPhraseText.text = sigilPhrase;
        sigilPhraseText.ForceMeshUpdate();
        cloudMaterial.SetFloat(Shader.PropertyToID("_SDFPhraseColorMultiplier"), 0.0f);
        cloudMaterial.SetFloat(Shader.PropertyToID("_SDFPhraseMultiplier"), 0.0f);
        cloudMaterial.SetFloat(Shader.PropertyToID("_SDFPhraseDistortionScale"), 0.5f);
        ApplyProperties();
    }
    
    public void SetUnformedSettings()
    {
        currentTransitionFactor = 0.0f;
        ApplyProperties();
    }

    private void Update()
    {
        ApplyProperties();
    }

    private void ApplyProperties()
    {
        if (cloudMaterial == null) return;

        // Use the manual transition factor directly
        float inT = UniState.Instance.SigilInT;
        float outT = UniState.Instance.SigilOutT;

        // Lerp all SDF properties from unformed to formed based on transitionFactor
        // transitionFactor: 0 = unformed, 1 = formed
        float currentSDFScale = Mathf.Lerp(formedSDFScale, unformedSDFScale, outT);
        float currentSDFThreshold = Mathf.Lerp(formedSDFThreshold, unformedSDFThreshold, outT);
        float currentSDFMultiplier = Mathf.Lerp(unformedSDFMultiplier, formedSDFMultiplier, inT * (1f - math.smoothstep(0f, 0.8f, outT)));
        float currentSDFDistortionScale = Mathf.Lerp(Mathf.Lerp(0.5f * Mathf.Lerp(unformedSDFDistortionScale, formedSDFDistortionScale, 0.5f), formedSDFDistortionScale, math.smoothstep(0f, 1f, inT)), unformedSDFDistortionScale, math.smoothstep(0f, 1f, outT));
        float currentSDFDistortionMinThreshold = Mathf.Lerp(formedSDFDistortionMinThreshold, unformedSDFDistortionMinThreshold, outT);
        float currentSDFDistortionMaxThreshold = Mathf.Lerp(formedSDFDistortionMaxThreshold, unformedSDFDistortionMaxThreshold, outT);
        Color currentSDFColor = Color.Lerp(formedSDFColor, unformedSDFColor, outT);
        float currentColorMultiplier = Mathf.Lerp(unformedColorMultiplier, formedColorMultiplier, math.smoothstep(0.8f, 1.0f, inT * (1f - outT)));

        // Apply all properties to material
        cloudMaterial.SetFloat(SDFScaleProperty, currentSDFScale);
        cloudMaterial.SetFloat(SDFThresholdProperty, currentSDFThreshold);
        cloudMaterial.SetFloat(SDFMultiplierProperty, currentSDFMultiplier);
        cloudMaterial.SetFloat(SDFDistortionScaleProperty, currentSDFDistortionScale);
        cloudMaterial.SetFloat(SDFDistortionMinThresholdProperty, currentSDFDistortionMinThreshold);
        cloudMaterial.SetFloat(SDFDistortionMaxThresholdProperty, currentSDFDistortionMaxThreshold);
        cloudMaterial.SetColor(SDFColorProperty, currentSDFColor);
        cloudMaterial.SetFloat(ColorMultiplierProperty, currentColorMultiplier);

        cloudMaterial.SetFloat(Shader.PropertyToID("_SDFPhraseColorMultiplier"), Mathf.Lerp(0.0f, 0.5f, math.smoothstep(0.8f, 1.0f, inT * (1f - outT))));
        cloudMaterial.SetFloat(Shader.PropertyToID("_SDFPhraseMultiplier"), Mathf.Lerp(0.0f, 0.5f, math.smoothstep(0f, 0.5f, inT * (1f - outT))));
        cloudMaterial.SetFloat(Shader.PropertyToID("_SDFPhraseDistortionScale"), Mathf.Lerp(0f, 0.5f, math.smoothstep(0f, 1.0f, outT)));

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
        }
    }
}

