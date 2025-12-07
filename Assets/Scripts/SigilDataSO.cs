using UnityEngine;

[CreateAssetMenu(fileName = "New Sigil Data", menuName = "Uni/Sigil Data")]
public class SigilDataSO : ScriptableObject
{
    public string sigilPhrase;
    public string sigilCode;
    public Texture2D pngTexture;
}

