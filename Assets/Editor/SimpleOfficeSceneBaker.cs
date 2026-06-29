using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SimpleOfficeSceneBaker
{
    private const string MenuPath = "Tools/RedeLabEscola/Bake Simple Office Scene Into Current Scene";
    private const string TemporaryBuilderName = "__SimpleOfficeSceneBuilder_Temporary";

    [MenuItem(MenuPath)]
    public static void BakeSimpleOfficeScene()
    {
        GameObject temporaryBuilder = new GameObject(TemporaryBuilderName);
        SimpleOfficeSceneBuilder builder = temporaryBuilder.AddComponent<SimpleOfficeSceneBuilder>();

        builder.BuildScene();
        Object.DestroyImmediate(temporaryBuilder);

        DisableBuildOnStartOnExistingBuilders();
        MarkCurrentSceneDirty();

        Debug.Log("Simple office scene baked into the current scene. Save the scene to keep the generated GameObjects.");
    }

    private static void DisableBuildOnStartOnExistingBuilders()
    {
        SimpleOfficeSceneBuilder[] builders = Object.FindObjectsOfType<SimpleOfficeSceneBuilder>();

        foreach (SimpleOfficeSceneBuilder builder in builders)
        {
            SerializedObject serializedBuilder = new SerializedObject(builder);
            SerializedProperty buildOnStart = serializedBuilder.FindProperty("buildOnStart");

            if (buildOnStart != null)
            {
                buildOnStart.boolValue = false;
                serializedBuilder.ApplyModifiedProperties();
                EditorUtility.SetDirty(builder);
            }
        }
    }

    private static void MarkCurrentSceneDirty()
    {
        Scene activeScene = SceneManager.GetActiveScene();

        if (activeScene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(activeScene);
        }
    }
}
