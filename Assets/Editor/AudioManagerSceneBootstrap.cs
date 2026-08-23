using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class AudioManagerSceneBootstrap
{
    private const string PrefabPath = "Assets/Prefabs/Audio/AudioManager.prefab";

    static AudioManagerSceneBootstrap()
    {
        EditorApplication.delayCall += EnsureInLoadedScenes;
        EditorSceneManager.sceneOpened -= HandleSceneOpened;
        EditorSceneManager.sceneOpened += HandleSceneOpened;
    }

    private static void HandleSceneOpened(Scene scene, OpenSceneMode mode)
    {
        EditorApplication.delayCall += () => EnsureInScene(scene);
    }

    private static void EnsureInLoadedScenes()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            EnsureInScene(SceneManager.GetSceneAt(i));
        }
    }

    private static void EnsureInScene(Scene scene)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || !scene.IsValid() || !scene.isLoaded || string.IsNullOrEmpty(scene.path))
        {
            return;
        }

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.GetComponentInChildren<AudioManager>(true) != null)
            {
                return;
            }
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogError("Nao foi possivel encontrar o prefab do AudioManager em " + PrefabPath + ".");
            return;
        }

        GameObject instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
        if (instance == null)
        {
            Debug.LogError("Nao foi possivel adicionar o AudioManager a cena " + scene.name + ".");
            return;
        }

        Undo.RegisterCreatedObjectUndo(instance, "Adicionar AudioManager");
        instance.name = "AudioManager";
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = instance;
    }
}
