using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class ProfessorCharacterSetup
{
    private const string ProfessorAnimationPath = "Assets/Modelos 3D/Personagem/Professor/animacoes/professor@Standing W_Briefcase Idle.fbx";
    private const string ProfessorControllerPath = "Assets/Modelos 3D/Personagem/Professor/animacoes/Animator Controller professor.controller";
    private const string AutoFixSessionKey = "RedeLabEscola.ProfessorCharacterSetup.AutoFixDone";

    [InitializeOnLoadMethod]
    private static void RunOnceAfterReload()
    {
        if (SessionState.GetBool(AutoFixSessionKey, false))
        {
            return;
        }

        SessionState.SetBool(AutoFixSessionKey, true);
        EditorApplication.delayCall += FixProfessorCharacter;
    }

    [MenuItem("Tools/RedeLabEscola/Fix Professor Character")]
    public static void FixProfessorCharacter()
    {
        Selection.objects = new Object[0];
        ConfigureProfessorIdleClipLoop();

        var professorClip = FindAnimationClip(ProfessorAnimationPath);
        if (professorClip == null)
        {
            Debug.LogError($"Professor animation clip not found: {ProfessorAnimationPath}");
            return;
        }

        if (System.IO.File.Exists(ProfessorControllerPath))
        {
            System.IO.File.Delete(ProfessorControllerPath);
            AssetDatabase.ImportAsset(ProfessorControllerPath, ImportAssetOptions.ForceUpdate);
        }

        var controller = AnimatorController.CreateAnimatorControllerAtPath(ProfessorControllerPath);
        if (controller == null)
        {
            Debug.LogError($"Could not create Professor Animator Controller at: {ProfessorControllerPath}");
            return;
        }

        var stateMachine = controller.layers[0].stateMachine;
        var idleState = stateMachine.AddState("Idle", new Vector3(300f, 120f, 0f));
        idleState.motion = professorClip;
        idleState.writeDefaultValues = true;
        stateMachine.defaultState = idleState;

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("Professor Animator Controller rebuilt with a valid Base Layer and Idle state.");
    }

    [MenuItem("Tools/RedeLabEscola/Clear Editor Selection")]
    public static void ClearEditorSelection()
    {
        Selection.objects = new Object[0];
        Debug.Log("Editor selection cleared.");
    }

    private static AnimationClip FindAnimationClip(string assetPath)
    {
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
        {
            if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__", System.StringComparison.Ordinal))
            {
                return clip;
            }
        }

        return null;
    }

    private static void ConfigureProfessorIdleClipLoop()
    {
        var importer = AssetImporter.GetAtPath(ProfessorAnimationPath) as ModelImporter;
        if (importer == null)
        {
            return;
        }

        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0)
        {
            clips = importer.defaultClipAnimations;
        }

        bool changed = false;
        foreach (ModelImporterClipAnimation clip in clips)
        {
            if (clip == null || clip.loopTime)
            {
                continue;
            }

            clip.loopTime = true;
            clip.loopPose = true;
            changed = true;
        }

        if (!changed)
        {
            return;
        }

        importer.clipAnimations = clips;
        importer.SaveAndReimport();
    }
}
