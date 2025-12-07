using UnityEngine;

[CreateAssetMenu(fileName = "ThoughtProcessSettings", menuName = "Uni/ThoughtProcessSettings")]
public class ThoughtProcessSettings : ScriptableObject
{
    [Header("Separation")]
    public float separationDist = 0.2f;
    
    [Header("Pull")]
    public float pullDist = 1.0f;
    public Vector3 listeningPosition = Vector3.zero;
    public float listeningRadius = 0.3f;
    public Vector3 idlePosition = Vector3.zero;
    public float idleRadius = 0.3f;
    public float pullForceMultiplier = 1.0f;
    public float drag = 0.1f;
    
    [Header("Lissajous Pattern")]
    public float idleSpeed = 1.0f;
    public float listeningSpeed = 1.0f;
    public float idleFreqX = 1.0f;
    public float idleFreqY = 2.0f;
    public float idleFreqZ = 3.0f;
    public float listeningFreqX = 1.0f;
    public float listeningFreqY = 2.0f;
    public float listeningFreqZ = 3.0f;
    public float phaseOffsetScale = 1.0f;
    public float phaseMultiplierX = 1.0f;
    public float phaseMultiplierY = 1.3f;
    public float phaseMultiplierZ = 0.7f;
    
    [Header("Breathing")]
    public float breathingFreq = 1.0f;
    public float breathingAmp = 0.1f;
    
    [Header("Sigil")]
    [Range(0.5f, 10f)] public float sigilScale = 1.0f;
    public Vector3 sigilOffset0 = Vector3.zero;
    public Vector3 sigilOffset1 = Vector3.zero;
    public float sigilZOffset = 0.1f;
    
    [Header("Velocity")]
    public float maxVelocity = 2.0f;
    
    [Header("Spark Appearance")]
    [ColorUsage(false, true)] public Color sparkEmission = Color.white;
    public Color sparkColor = Color.white;
    public float sparkScale = 1.0f;
}

