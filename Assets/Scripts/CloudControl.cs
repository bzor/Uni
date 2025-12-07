using UnityEngine;
using Unity.Mathematics;

public class CloudControl : MonoBehaviour
{
    [Header("Materials")]
    [SerializeField] private Material opaqueMaterial;
    [SerializeField] private Material transparentMaterial;
    [SerializeField] private Material midMaterial;
    
    [Header("Color Settings")]
    [SerializeField] private CloudColorSettings[] colorSettings;
    [SerializeField] private int currentSettingsIndex = 0;
    [SerializeField] private float transitionDuration = 2f;

    [SerializeField] private CloudColorSettings[] colorSettings1;
    [SerializeField] private CloudColorSettings[] colorSettings2;

    [Header("Cloud Control")]
    [Range(0f, 1f)] [SerializeField] private float openClouds = 0f;
    [SerializeField] private float currentSpeed = 1.0f;
    
    private float transitionTimer = 0f;
    private bool isTransitioning = false;
    private int targetSettingsIndex = 0;
    private float cloudTick = 0f;
    
    // Shader property IDs for Opaque shader
    private static readonly int Color1Property = Shader.PropertyToID("_Color1");
    private static readonly int Color2Property = Shader.PropertyToID("_Color2");
    private static readonly int Color3Property = Shader.PropertyToID("_Color3");
    private static readonly int Color4Property = Shader.PropertyToID("_Color4");
    private static readonly int BGColBottomProperty = Shader.PropertyToID("_BGColBottom");
    private static readonly int BGColTopProperty = Shader.PropertyToID("_BGColTop");
    
    // Shader property IDs for Transparent shader
    private static readonly int TransparentColor1Property = Shader.PropertyToID("_Color1");
    private static readonly int TransparentColor2Property = Shader.PropertyToID("_Color2");
    private static readonly int TransparentColor3Property = Shader.PropertyToID("_Color3");
    
    // Open Clouds property (both shaders)
    private static readonly int OpenCloudsProperty = Shader.PropertyToID("_OpenClouds");
    private static readonly int TickProperty = Shader.PropertyToID("_Tick");
    
    private void Start()
    {
        if (opaqueMaterial == null)
        {
            var renderer = GetComponent<Renderer>();
            if (renderer != null)
            {
                opaqueMaterial = renderer.material;
            }
        }
        
        // Apply initial settings
        if (colorSettings != null && colorSettings.Length > 0)
        {
            ApplySettings(colorSettings[currentSettingsIndex]);
            currentSpeed = colorSettings[currentSettingsIndex].timeScale;
        }
    }
    
    private void Update()
    {
        // Key press to cycle through colorSettings
        if (Input.GetKeyDown(KeyCode.C))
        {
            if (colorSettings != null && colorSettings.Length > 0)
            {
                currentSettingsIndex = (currentSettingsIndex + 1) % colorSettings.Length;
                ApplySettingsDirect(currentSettingsIndex);
                currentSpeed = colorSettings[currentSettingsIndex].timeScale;
            }
        }
        
        // Increment cloud tick
        cloudTick += Time.deltaTime * currentSpeed;
        
        float idleT = 1f - UniState.Instance.IdleT;
        float cloudsT;

        cloudsT = math.lerp(0f, 0.8f, math.smoothstep(0.2f, 1f, idleT));
        cloudsT = math.lerp(cloudsT, 1f, math.smoothstep(0f, 0.8f, UniState.Instance.SigilT));
        opaqueMaterial.SetFloat(OpenCloudsProperty, cloudsT);
        opaqueMaterial.SetFloat(TickProperty, cloudTick);

        cloudsT = math.lerp(0f, 0.8f, math.smoothstep(0f, 0.85f, idleT));
        cloudsT = math.lerp(cloudsT, 1f, math.smoothstep(0f, 0.8f, UniState.Instance.SigilT));
        transparentMaterial.SetFloat(OpenCloudsProperty, cloudsT);
        transparentMaterial.SetFloat(TickProperty, cloudTick);

        midMaterial.SetFloat(TickProperty, cloudTick);


        /*
        float colorT = math.smoothstep(0f, 1f, idleT);
        
        if (colorSettings != null && colorSettings.Length > 0)
        {
            float t = colorT;
            
            CloudColorSettings current = colorSettings[0];
            CloudColorSettings target = colorSettings[1];
            CloudColorSettings lerped = LerpSettings(current, target, t);
            ApplySettings(lerped);
            
            // Lerp currentSpeed between settings
            currentSpeed = math.lerp(current.timeScale, target.timeScale, t);
        }
        */
    }
    
    public void TransitionToSettings(int index)
    {
        if (colorSettings == null || colorSettings.Length == 0) return;
        if (index < 0 || index >= colorSettings.Length) return;
        if (index == currentSettingsIndex && !isTransitioning) return;
        
        targetSettingsIndex = index;
        isTransitioning = true;
        transitionTimer = 0f;
    }
    
    private CloudColorSettings LerpSettings(CloudColorSettings a, CloudColorSettings b, float t)
    {
        // Create a temporary settings object for lerping
        CloudColorSettings result = ScriptableObject.CreateInstance<CloudColorSettings>();
        
        // Lerp opaque colors
        result.color1 = Color.Lerp(a.color1, b.color1, t);
        result.color2 = Color.Lerp(a.color2, b.color2, t);
        result.color3 = Color.Lerp(a.color3, b.color3, t);
        result.color4 = Color.Lerp(a.color4, b.color4, t);
        
        // Lerp background colors
        result.bgColBottom = Color.Lerp(a.bgColBottom, b.bgColBottom, t);
        result.bgColTop = Color.Lerp(a.bgColTop, b.bgColTop, t);
        
        // Lerp time scale
        result.timeScale = Mathf.Lerp(a.timeScale, b.timeScale, t);
        
        // Lerp transparent colors
        result.transparentColor1 = Color.Lerp(a.transparentColor1, b.transparentColor1, t);
        result.transparentColor2 = Color.Lerp(a.transparentColor2, b.transparentColor2, t);
        result.transparentColor3 = Color.Lerp(a.transparentColor3, b.transparentColor3, t);
        
        return result;
    }
    
    public void ApplySettings(CloudColorSettings settings)
    {
        midMaterial.SetColor(Color1Property, settings.color1);
        midMaterial.SetColor(Color2Property, settings.color2);
        midMaterial.SetColor(Color3Property, settings.color3);
        midMaterial.SetColor(Color4Property, settings.color4);
        midMaterial.SetColor(BGColBottomProperty, settings.bgColBottom);
        midMaterial.SetColor(BGColTopProperty, settings.bgColTop);

        opaqueMaterial.SetColor(Color1Property, settings.color1);
        opaqueMaterial.SetColor(Color2Property, settings.color2);
        opaqueMaterial.SetColor(Color3Property, settings.color3);
        opaqueMaterial.SetColor(Color4Property, settings.color4);
        opaqueMaterial.SetColor(BGColBottomProperty, settings.bgColBottom);
        opaqueMaterial.SetColor(BGColTopProperty, settings.bgColTop);

        transparentMaterial.SetColor(TransparentColor1Property, settings.transparentColor1);
        transparentMaterial.SetColor(TransparentColor2Property, settings.transparentColor2);
        transparentMaterial.SetColor(TransparentColor3Property, settings.transparentColor3);
    }
    
    public void ApplySettingsDirect(int index)
    {
        if (colorSettings == null || colorSettings.Length == 0) return;
        if (index < 0 || index >= colorSettings.Length) return;
        
        ApplySettings(colorSettings[index]);
        
        // Update the current index
        currentSettingsIndex = index;
    }
}

