using UnityEngine;

[CreateAssetMenu(fileName = "CloudColorSettings", menuName = "Uni/CloudColorSettings")]
public class CloudColorSettings : ScriptableObject
{
    [Header("Opaque Shader Colors")]
    public Color color1 = new Color(0.101961f, 0.619608f, 0.666667f, 1f);
    public Color color2 = new Color(0.666667f, 0.666667f, 0.498039f, 1f);
    public Color color3 = new Color(0f, 0f, 0.164706f, 1f);
    public Color color4 = new Color(0.666667f, 1f, 1f, 1f);
    
    [Header("Opaque Background Colors")]
    public Color bgColBottom = new Color(0f, 0f, 0f, 1f);
    public Color bgColTop = new Color(1f, 1f, 1f, 1f);
    
    [Header("Animation")]
    [Range(0f, 5f)] public float timeScale = 1f;
    
    [Header("Transparent Shader Colors")]
    public Color transparentColor1 = new Color(0.101961f, 0.619608f, 0.666667f, 1f);
    public Color transparentColor2 = new Color(0.666667f, 0.666667f, 0.498039f, 1f);
    public Color transparentColor3 = new Color(0f, 0f, 0.164706f, 1f);
}

