using UnityEditor;
using UnityEngine;

public static class MixamoImportFixer
{
    private const string BaseModelPath = "Assets/Modelos 3D/Personagem/Aluno/Animacoes personagem 3d aluno/aluno a pose.fbx";

    private static readonly AnimationImportSettings[] AnimationPaths =
    {
        new("Assets/Modelos 3D/Personagem/Aluno/Animacoes personagem 3d aluno/personagem aluno 3d a pose@Idle.fbx", true),
        new("Assets/Modelos 3D/Personagem/Aluno/Animacoes personagem 3d aluno/personagem aluno 3d a pose@Walking.fbx", true),
        new("Assets/Modelos 3D/Personagem/Aluno/Animacoes personagem 3d aluno/personagem aluno 3d a pose@Walking (1).fbx", true),
        new("Assets/Modelos 3D/Personagem/Aluno/Animacoes personagem 3d aluno/personagem aluno 3d a pose@Carrying.fbx", true),
        new("Assets/Modelos 3D/Personagem/Aluno/Animacoes personagem 3d aluno/personagem aluno 3d a pose@Button Pushing.fbx", false),
    };

    [MenuItem("Tools/RedeLabEscola/Fix Mixamo Animation Imports")]
    public static void FixImports()
    {
        ConfigureBaseModel();

        var baseAvatar = AssetDatabase.LoadAssetAtPath<Avatar>(BaseModelPath);
        if (baseAvatar == null || !baseAvatar.isHuman)
        {
            Debug.LogError($"Base humanoid avatar not found or invalid: {BaseModelPath}");
            return;
        }

        foreach (var settings in AnimationPaths)
        {
            ConfigureAnimation(settings, baseAvatar);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Mixamo import settings fixed. Check the walk animation preview after Unity finishes reimporting.");
    }

    private static void ConfigureBaseModel()
    {
        var importer = AssetImporter.GetAtPath(BaseModelPath) as ModelImporter;
        if (importer == null)
        {
            Debug.LogError($"Model importer not found: {BaseModelPath}");
            return;
        }

        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importAnimation = false;
        importer.SaveAndReimport();
    }

    private static void ConfigureAnimation(AnimationImportSettings settings, Avatar sourceAvatar)
    {
        var importer = AssetImporter.GetAtPath(settings.Path) as ModelImporter;
        if (importer == null)
        {
            Debug.LogWarning($"Animation importer not found: {settings.Path}");
            return;
        }

        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
        importer.sourceAvatar = sourceAvatar;
        importer.importAnimation = true;

        var clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0)
        {
            clips = importer.defaultClipAnimations;
        }

        for (int i = 0; i < clips.Length; i++)
        {
            clips[i].loopTime = settings.Loops;
            clips[i].loopPose = clips[i].loopTime;
            clips[i].lockRootRotation = true;
            clips[i].keepOriginalOrientation = false;
            clips[i].lockRootHeightY = true;
            clips[i].keepOriginalPositionY = true;
            clips[i].lockRootPositionXZ = true;
            clips[i].keepOriginalPositionXZ = false;
        }

        importer.clipAnimations = clips;
        importer.SaveAndReimport();
    }

    private readonly struct AnimationImportSettings
    {
        public AnimationImportSettings(string path, bool loops)
        {
            Path = path;
            Loops = loops;
        }

        public string Path { get; }
        public bool Loops { get; }
    }
}
