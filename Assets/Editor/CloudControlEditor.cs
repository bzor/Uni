using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(CloudControl))]
public class CloudControlEditor : Editor
{
    private SerializedProperty colorSettingsProperty;
    private SerializedProperty currentSettingsIndexProperty;
    
    private void OnEnable()
    {
        colorSettingsProperty = serializedObject.FindProperty("colorSettings");
        currentSettingsIndexProperty = serializedObject.FindProperty("currentSettingsIndex");
    }
    
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        CloudControl cloudControl = (CloudControl)target;
        
        if (cloudControl == null || colorSettingsProperty == null) return;
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Quick Settings", EditorStyles.boldLabel);
        
        if (colorSettingsProperty.arraySize > 0)
        {
            int currentIndex = currentSettingsIndexProperty.intValue;
            
            EditorGUILayout.BeginHorizontal();
            for (int i = 0; i < colorSettingsProperty.arraySize; i++)
            {
                bool isCurrent = i == currentIndex;
                GUI.backgroundColor = isCurrent ? Color.green : Color.white;
                
                if (GUILayout.Button($"Settings {i}", GUILayout.Height(30)))
                {
                    cloudControl.ApplySettingsDirect(i);
                    currentSettingsIndexProperty.intValue = i;
                    serializedObject.ApplyModifiedProperties();
                }
                
                GUI.backgroundColor = Color.white;
            }
            EditorGUILayout.EndHorizontal();
        }
        else
        {
            EditorGUILayout.HelpBox("Add color settings to the array above to enable quick switching.", MessageType.Info);
        }
    }
}

