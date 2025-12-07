using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.Rendering;

[CustomEditor(typeof(ProjectSetupScriptableObject))] // Replace with your ScriptableObject class name
public class MyScriptableObjectEditor : Editor
{
    [MenuItem("Looking Glass/Project Setup")]
    public static void OpenProjectSetup()
    {
        // Find the ScriptableObject in the project
        string[] guids = AssetDatabase.FindAssets("t:ProjectSetupScriptableObject");
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            ScriptableObject setup = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);

            if (setup != null)
            {
                // Select the object in the project and open it in the Inspector
                Selection.activeObject = setup;
                EditorGUIUtility.PingObject(setup);
            }
            else
            {
                Debug.LogError("Project Setup asset found but could not be loaded.");
            }
        }
        else
        {
            Debug.LogError("No Project Setup asset found. Please create one.");
        }
    }

    public override void OnInspectorGUI()
    {
        // Draw the default inspector
        DrawDefaultInspector();

        // Get reference to the target ScriptableObject
        var scriptableObject = (ProjectSetupScriptableObject)target;
        var multiviewRpAsset = scriptableObject.multiviewRPAsset;

        EditorGUILayout.Space();

        // Check the default render pipeline asset in Graphics Settings
        var currentRPAsset = GraphicsSettings.defaultRenderPipeline;
        if (currentRPAsset != multiviewRpAsset)
        {
            EditorGUILayout.HelpBox(
                "Default Render Pipeline Asset is not set to multiview RP asset.",
                MessageType.Warning
            );
            if (GUILayout.Button("Set Default to multiview RP asset"))
            {
                GraphicsSettings.defaultRenderPipeline = multiviewRpAsset;
                EditorUtility.SetDirty(GraphicsSettings.GetGraphicsSettings());

                Debug.Log("Set Default to multiview RP asset");
            }
        }
        else
        {
            EditorGUILayout.HelpBox(
                "Default Render Pipeline Asset is correctly set to multiview RP asset.",
                MessageType.Info
            );
        }

        // Check each Quality Setting
        QualitySettings.ForEach(() =>
        {
            EditorGUILayout.Space();

            var qualityRPAsset = QualitySettings.renderPipeline;
            var qsName = QualitySettings.names[QualitySettings.GetQualityLevel()];
            if (qualityRPAsset != multiviewRpAsset)
            {
                EditorGUILayout.HelpBox(
                    $"Quality Level '{qsName}' is not using multiview RP asset.",
                    MessageType.Warning
                );
                if (GUILayout.Button($"Set '{qsName}' to multiview RP asset"))
                {
                    QualitySettings.renderPipeline = multiviewRpAsset;
                    EditorUtility.SetDirty(QualitySettings.GetQualitySettings());

                    Debug.Log($"Set '{qsName}' to multiview RP asset");
                }
            }
            else
            {
                EditorGUILayout.HelpBox(
                    $"Quality Level '{qsName}' is correctly using multiview RP asset.",
                    MessageType.Info
                );
            }
        });
    }

    [InitializeOnEnterPlayMode]
    static void OnEnterPlaymodeInEditor(EnterPlayModeOptions options)
    {
        var projectSetupGuids = AssetDatabase.FindAssets(
            $"t:{typeof(ProjectSetupScriptableObject).Name}"
        );

        if (projectSetupGuids == null || projectSetupGuids.Length == 0)
        {
            Debug.LogWarning(
                "Looking Glass `Project Setup` object is missing, please repair Looking Glass plugin!"
            );
            return;
        }

        // Debug.Log(projectSetupGuids.Length);
        // Debug.Log(projectSetupGuids[0]);
        var assetPath = AssetDatabase.GUIDToAssetPath(projectSetupGuids[0]);
        // Debug.Log(assetPath);

        var scriptableObject = AssetDatabase.LoadAssetAtPath<ProjectSetupScriptableObject>(
            assetPath
        );
        if (scriptableObject == null)
        {
            Debug.LogWarning(
                "Looking Glass `Project Setup` object is invalid, please repair Looking Glass plugin!"
            );
            return;
        }
        if (scriptableObject.multiviewRPAsset == null)
        {
            Debug.LogWarning(
                "Looking Glass `Project Setup` multiview RP Asset is invalid, please repair Looking Glass plugin!"
            );
            return;
        }

        var multiviewRpAsset = scriptableObject.multiviewRPAsset;

        // Check the default render pipeline asset in Graphics Settings
        var currentRPAsset = GraphicsSettings.defaultRenderPipeline;
        if (currentRPAsset != multiviewRpAsset)
        {
            Debug.LogWarning(
                "Default Render Pipeline Asset is not set to multiview RP asset. Please fix in Looking Glass -> Project Setup"
            );
        }

        // Check each Quality Setting
        QualitySettings.ForEach(() =>
        {
            var qualityRPAsset = QualitySettings.renderPipeline;
            var qsName = QualitySettings.names[QualitySettings.GetQualityLevel()];
            if (qualityRPAsset != multiviewRpAsset)
            {
                Debug.LogWarning(
                    $"Quality Level '{qsName}' is not using multiview RP asset. Please fix in Looking Glass -> Project Setup"
                );
            }
        });
    }
}

[CreateAssetMenu(fileName = "Project Setup", menuName = "Looking Glass/Project Setup", order = 1)]
public class ProjectSetupScriptableObject : ScriptableObject
{
    public RenderPipelineAsset multiviewRPAsset;
}
