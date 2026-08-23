using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class ProfessorCharacterSetup
{
    private const string StandardIdleAnimationPath = "Assets/Modelos 3D/Personagem/Professor/animacoes/Y Bot@Standard Idle.fbx";
    private const string PointingAnimationPath = "Assets/Modelos 3D/Personagem/Professor/animacoes/Y Bot@Pointing.fbx";
    private const string CarryingAnimationPath = "Assets/Modelos 3D/Personagem/Aluno/Animacoes personagem 3d aluno/personagem aluno 3d a pose@Carrying.fbx";
    private const string ProfessorControllerPath = "Assets/Modelos 3D/Personagem/Professor/animacoes/Animator Controller professor.controller";
    private const string AnimatorGraphCleanupSessionKey = "RedeLabEscola.AnimatorGraphCleanup.V1";

    [InitializeOnLoadMethod]
    private static void CloseStaleAnimatorGraphOnce()
    {
        if (SessionState.GetBool(AnimatorGraphCleanupSessionKey, false))
        {
            return;
        }

        SessionState.SetBool(AnimatorGraphCleanupSessionKey, true);
        EditorApplication.delayCall += CloseAnimatorWindows;
    }
    [MenuItem("Tools/RedeLabEscola/Fix Professor Character")]
    public static void FixProfessorCharacter()
    {
        CloseAnimatorWindows();
        Selection.objects = new Object[0];
        ConfigureAnimationClip(StandardIdleAnimationPath, true);
        ConfigureAnimationClip(PointingAnimationPath, false);

        var standardIdleClip = FindAnimationClip(StandardIdleAnimationPath);
        var pointingClip = FindAnimationClip(PointingAnimationPath);
        var carryingClip = FindAnimationClip(CarryingAnimationPath);
        if (standardIdleClip == null)
        {
            Debug.LogError($"Professor Standard Idle clip not found: {StandardIdleAnimationPath}");
            return;
        }

        if (pointingClip == null)
        {
            Debug.LogError($"Professor Pointing clip not found: {PointingAnimationPath}");
            return;
        }

        if (carryingClip == null)
        {
            Debug.LogError($"Professor carrying animation clip not found: {CarryingAnimationPath}");
            return;
        }

        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ProfessorControllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(ProfessorControllerPath);
        }
        if (controller == null)
        {
            Debug.LogError($"Could not create Professor Animator Controller at: {ProfessorControllerPath}");
            return;
        }

        controller.parameters = new AnimatorControllerParameter[0];
        if (controller.layers == null || controller.layers.Length == 0)
        {
            controller.AddLayer("Base Layer");
        }

        AnimatorControllerLayer[] layers = controller.layers;
        layers[0].iKPass = true;
        controller.layers = layers;

        var stateMachine = controller.layers[0].stateMachine;
        foreach (ChildAnimatorState childState in stateMachine.states)
        {
            stateMachine.RemoveState(childState.state);
        }
        foreach (AnimatorStateTransition transition in stateMachine.anyStateTransitions)
        {
            stateMachine.RemoveAnyStateTransition(transition);
        }

        controller.AddParameter("Point", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("IsCarrying", AnimatorControllerParameterType.Bool);
        var idleState = stateMachine.AddState("Standard Idle", new Vector3(300f, 120f, 0f));
        idleState.motion = standardIdleClip;
        idleState.writeDefaultValues = true;
        stateMachine.defaultState = idleState;

        var pointingState = stateMachine.AddState("Pointing", new Vector3(540f, 120f, 0f));
        pointingState.motion = pointingClip;
        pointingState.writeDefaultValues = true;

        var carryingState = stateMachine.AddState("Carrying", new Vector3(540f, 260f, 0f));
        carryingState.motion = carryingClip;
        carryingState.writeDefaultValues = true;

        AnimatorStateTransition startPointing = idleState.AddTransition(pointingState);
        startPointing.hasExitTime = false;
        startPointing.hasFixedDuration = true;
        startPointing.duration = 0.12f;
        startPointing.AddCondition(AnimatorConditionMode.If, 0f, "Point");

        AnimatorStateTransition finishPointing = pointingState.AddTransition(idleState);
        finishPointing.hasExitTime = true;
        finishPointing.exitTime = 0.92f;
        finishPointing.hasFixedDuration = true;
        finishPointing.duration = 0.12f;

        AnimatorStateTransition pointToCarrying = pointingState.AddTransition(carryingState);
        pointToCarrying.hasExitTime = false;
        pointToCarrying.duration = 0.04f;
        pointToCarrying.AddCondition(AnimatorConditionMode.If, 0f, "IsCarrying");

        AnimatorStateTransition startCarrying = idleState.AddTransition(carryingState);
        startCarrying.hasExitTime = false;
        startCarrying.duration = 0.04f;
        startCarrying.AddCondition(AnimatorConditionMode.If, 0f, "IsCarrying");

        AnimatorStateTransition stopCarrying = carryingState.AddTransition(idleState);
        stopCarrying.hasExitTime = false;
        stopCarrying.duration = 0.04f;
        stopCarrying.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsCarrying");

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("Professor Animator configured with Standard Idle, occasional Pointing and preserved Carrying behavior.");
    }

    private static void CloseAnimatorWindows()
    {
        EditorWindow[] windows = Resources.FindObjectsOfTypeAll<EditorWindow>();
        foreach (EditorWindow window in windows)
        {
            if (window != null && window.GetType().FullName == "UnityEditor.Graphs.AnimatorControllerTool")
            {
                window.Close();
            }
        }
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

    private static void ConfigureAnimationClip(string assetPath, bool loopTime)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
        if (importer == null)
        {
            return;
        }

        bool importerChanged = false;
        if (importer.animationType != ModelImporterAnimationType.Human)
        {
            importer.animationType = ModelImporterAnimationType.Human;
            importerChanged = true;
        }

        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0)
        {
            clips = importer.defaultClipAnimations;
        }

        bool changed = false;
        foreach (ModelImporterClipAnimation clip in clips)
        {
            if (clip == null)
            {
                continue;
            }

            if (clip.loopTime != loopTime || clip.loopPose != loopTime
                || !clip.lockRootRotation || !clip.lockRootHeightY || !clip.lockRootPositionXZ)
            {
                clip.loopTime = loopTime;
                clip.loopPose = loopTime;
                clip.lockRootRotation = true;
                clip.lockRootHeightY = true;
                clip.lockRootPositionXZ = true;
                changed = true;
            }
        }

        if (!changed && !importerChanged)
        {
            return;
        }

        importer.clipAnimations = clips;
        importer.SaveAndReimport();
    }
}
