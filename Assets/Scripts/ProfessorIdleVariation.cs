using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class ProfessorIdleVariation : MonoBehaviour
{
    private const string DefaultIdleState = "Base Layer.Standard Idle";
    private const string DefaultPointTrigger = "Point";
    private const string DefaultCarryingParameter = "IsCarrying";

    [SerializeField] private Animator animator;
    [SerializeField, Min(0.1f)] private float minimumInterval = 15f;
    [SerializeField, Min(0.1f)] private float maximumInterval = 35f;
    [SerializeField, Range(0f, 1f)] private float pointingChance = 0.55f;
    [SerializeField] private string idleStateName = DefaultIdleState;
    [SerializeField] private string pointTrigger = DefaultPointTrigger;
    [SerializeField] private string carryingParameter = DefaultCarryingParameter;

    private bool hasPointTrigger;
    private bool hasCarryingParameter;
    private Coroutine variationRoutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneBootstrap()
    {
        SceneManager.sceneLoaded -= AttachToProfessorAnimators;
        SceneManager.sceneLoaded += AttachToProfessorAnimators;
    }

    private static void AttachToProfessorAnimators(Scene scene, LoadSceneMode mode)
    {
        Animator[] animators = FindObjectsOfType<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
        {
            Animator candidate = animators[i];
            if (candidate == null || candidate.gameObject.scene != scene || !HasPointTrigger(candidate))
            {
                continue;
            }

            if (candidate.GetComponent<ProfessorIdleVariation>() == null)
            {
                candidate.gameObject.AddComponent<ProfessorIdleVariation>();
            }
        }
    }

    private static bool HasPointTrigger(Animator candidate)
    {
        if (candidate.runtimeAnimatorController == null)
        {
            return false;
        }

        AnimatorControllerParameter[] parameters = candidate.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].name == DefaultPointTrigger
                && parameters[i].type == AnimatorControllerParameterType.Trigger)
            {
                return true;
            }
        }

        return false;
    }

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        CacheParameters();
        if (animator != null)
        {
            animator.applyRootMotion = false;
        }
    }

    private void OnEnable()
    {
        if (variationRoutine == null)
        {
            variationRoutine = StartCoroutine(RunIdleVariations());
        }
    }

    private void OnDisable()
    {
        if (variationRoutine != null)
        {
            StopCoroutine(variationRoutine);
            variationRoutine = null;
        }
    }

    private IEnumerator RunIdleVariations()
    {
        while (true)
        {
            float lowerBound = Mathf.Max(0.1f, minimumInterval);
            float upperBound = Mathf.Max(lowerBound, maximumInterval);
            yield return new WaitForSeconds(Random.Range(lowerBound, upperBound));

            if (!CanPoint() || Random.value > pointingChance)
            {
                continue;
            }

            animator.ResetTrigger(pointTrigger);
            animator.SetTrigger(pointTrigger);
        }
    }

    private bool CanPoint()
    {
        if (animator == null || !animator.isActiveAndEnabled || !hasPointTrigger || animator.IsInTransition(0))
        {
            return false;
        }

        if (hasCarryingParameter && animator.GetBool(carryingParameter))
        {
            return false;
        }

        return animator.GetCurrentAnimatorStateInfo(0).IsName(idleStateName);
    }

    private void CacheParameters()
    {
        hasPointTrigger = false;
        hasCarryingParameter = false;
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            return;
        }

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (parameter.name == pointTrigger && parameter.type == AnimatorControllerParameterType.Trigger)
            {
                hasPointTrigger = true;
            }
            else if (parameter.name == carryingParameter && parameter.type == AnimatorControllerParameterType.Bool)
            {
                hasCarryingParameter = true;
            }
        }
    }
}
