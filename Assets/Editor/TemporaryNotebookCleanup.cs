using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class TemporaryNotebookCleanup
{
    [MenuItem("Tools/RedeLabEscola/Scene/Remove Temporary Notebook Duplicates")]
    public static void CleanupActiveOfficeScene()
    {
        if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.name != SceneNames.Office) return;

        int removed = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root != null && root.name == "Notebook")
            {
                Object.DestroyImmediate(root);
                removed++;
            }
        }

        if (removed > 0)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"Removidos {removed} notebooks temporários criados indevidamente.");
        }
    }
}
