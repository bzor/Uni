using TMPro;
using UnityEngine;

public class FpsCounter : MonoBehaviour
{
    const int count = 20;

    int index;
    float[] previousTimes = new float[count];

    float updateEvery = 1f;
    float updateTimer = 0f;

    string text;

    void Update()
    {
        previousTimes[index++] = Time.unscaledDeltaTime;
        if (index >= count)
            index = 0;

        updateTimer += Time.unscaledDeltaTime;
        if (updateTimer > updateEvery)
        {
            float dt = 0f;
            for (int i = 0; i < count; i++)
            {
                dt += previousTimes[i];
            }
            dt /= count;
            text = $"{(1f / dt).ToString("#.0")} fps";
            updateTimer = 0f;
        }
    }

    void OnGUI()
    {
        var oldMat = GUI.matrix;
        var scale = 3;
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1));

        GUI.Box(
            new Rect(20, 20, 80, 30),
            text
        );

        GUI.matrix = oldMat;
    }
}
