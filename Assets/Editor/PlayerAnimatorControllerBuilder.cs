using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class PlayerAnimatorControllerBuilder
{
    private const string ControllerPath = "Assets/Modelos 3D/Personagem/Aluno/Animacoes personagem 3d aluno/player Animator Controller.controller";
    private const string IdlePath = "Assets/Modelos 3D/Personagem/Aluno/Animacoes personagem 3d aluno/personagem aluno 3d a pose@Idle.fbx";
    private const string WalkingPath = "Assets/Modelos 3D/Personagem/Aluno/Animacoes personagem 3d aluno/personagem aluno 3d a pose@Walking.fbx";
    private const string CarryingPath = "Assets/Modelos 3D/Personagem/Aluno/Animacoes personagem 3d aluno/personagem aluno 3d a pose@Carrying.fbx";
    private const string WalkingCarryingPath = "Assets/Modelos 3D/Personagem/Aluno/Animacoes personagem 3d aluno/personagem aluno 3d a pose@Walking (1).fbx";
    private const string ButtonPushingPath = "Assets/Modelos 3D/Personagem/Aluno/Animacoes personagem 3d aluno/personagem aluno 3d a pose@Button Pushing.fbx";

    [MenuItem("Tools/RedeLabEscola/Rebuild Player Animator Controller")]
    public static void Rebuild()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        }

        controller.parameters = new AnimatorControllerParameter[0];
        controller.layers = new AnimatorControllerLayer[0];

        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("IsCarrying", AnimatorControllerParameterType.Bool);
        controller.AddParameter("PushButton", AnimatorControllerParameterType.Trigger);

        controller.AddLayer("Base Layer");
        var layer = controller.layers[0];
        var stateMachine = layer.stateMachine;

        foreach (var childState in stateMachine.states)
        {
            stateMachine.RemoveState(childState.state);
        }

        foreach (var transition in stateMachine.anyStateTransitions)
        {
            stateMachine.RemoveAnyStateTransition(transition);
        }

        var idle = AddState(stateMachine, "Idle", IdlePath, new Vector3(300, 90, 0));
        var walking = AddState(stateMachine, "Walking", WalkingPath, new Vector3(300, 230, 0));
        var carrying = AddState(stateMachine, "Carrying", CarryingPath, new Vector3(560, 90, 0));
        var walkingCarrying = AddState(stateMachine, "Walking Carrying", WalkingCarryingPath, new Vector3(560, 230, 0));
        var buttonPushing = AddState(stateMachine, "Button Pushing", ButtonPushingPath, new Vector3(820, 150, 0));

        stateMachine.defaultState = idle;

        AddFloatTransition(idle, walking, "Speed", AnimatorConditionMode.Greater, 0.1f);
        AddFloatTransition(walking, idle, "Speed", AnimatorConditionMode.Less, 0.1f);
        AddBoolTransition(idle, carrying, "IsCarrying", true);
        AddBoolTransition(carrying, idle, "IsCarrying", false);
        AddFloatTransition(carrying, walkingCarrying, "Speed", AnimatorConditionMode.Greater, 0.1f);
        AddFloatTransition(walkingCarrying, carrying, "Speed", AnimatorConditionMode.Less, 0.1f);
        AddBoolTransition(walking, walkingCarrying, "IsCarrying", true);
        AddBoolTransition(walkingCarrying, walking, "IsCarrying", false);

        var pushTransition = stateMachine.AddAnyStateTransition(buttonPushing);
        pushTransition.hasExitTime = false;
        pushTransition.duration = 0.08f;
        pushTransition.canTransitionToSelf = false;
        pushTransition.AddCondition(AnimatorConditionMode.If, 0f, "PushButton");

        var pushReturn = buttonPushing.AddTransition(idle);
        pushReturn.hasExitTime = true;
        pushReturn.exitTime = 0.9f;
        pushReturn.duration = 0.1f;

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Player Animator Controller rebuilt.");
    }

    private static AnimatorState AddState(AnimatorStateMachine stateMachine, string name, string clipPath, Vector3 position)
    {
        var state = stateMachine.AddState(name, position);
        state.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        state.writeDefaultValues = true;
        return state;
    }

    private static void AddFloatTransition(AnimatorState from, AnimatorState to, string parameter, AnimatorConditionMode mode, float threshold)
    {
        var transition = from.AddTransition(to);
        transition.hasExitTime = false;
        transition.duration = 0.15f;
        transition.AddCondition(mode, threshold, parameter);
    }

    private static void AddBoolTransition(AnimatorState from, AnimatorState to, string parameter, bool value)
    {
        var transition = from.AddTransition(to);
        transition.hasExitTime = false;
        transition.duration = 0.15f;
        transition.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, parameter);
    }
}
