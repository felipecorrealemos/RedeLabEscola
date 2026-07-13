using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerCharacterVisualApplier : MonoBehaviour
{
    [SerializeField] private GameObject alunaVisualPrefab;
    [SerializeField] private Transform alunoVisualRoot;
    [SerializeField] private Transform visualParent;
    [SerializeField] private PlayerTopDownController topDownController;

    private void Awake()
    {
        if (SceneManager.GetActiveScene().name == "CharacterSelection")
        {
            return;
        }

        ApplySelection(CharacterSelectionState.GetChoiceOrDefault(CharacterSelectionChoice.Aluno));
    }

    private void Reset()
    {
        topDownController = GetComponent<PlayerTopDownController>();
    }

    private void ApplySelection(CharacterSelectionChoice choice)
    {
        if (topDownController == null)
        {
            topDownController = GetComponent<PlayerTopDownController>();
        }

        if (alunoVisualRoot == null)
        {
            alunoVisualRoot = FindVisualChild("modelo");
        }

        if (visualParent == null)
        {
            visualParent = transform;
        }

        Animator alunoAnimator = alunoVisualRoot != null ? alunoVisualRoot.GetComponentInChildren<Animator>(true) : null;

        if (choice != CharacterSelectionChoice.Aluna)
        {
            if (alunoVisualRoot != null)
            {
                alunoVisualRoot.gameObject.SetActive(true);
            }

            ConfigureAnimator(alunoAnimator, alunoAnimator != null ? alunoAnimator.runtimeAnimatorController : null);
            return;
        }

        if (alunoVisualRoot != null)
        {
            alunoVisualRoot.gameObject.SetActive(false);
        }

        if (alunaVisualPrefab == null)
        {
            Debug.LogWarning("Prefab visual da Aluna nao configurado no PlayerCharacterVisualApplier.");
            return;
        }

        GameObject alunaVisual = Instantiate(alunaVisualPrefab, visualParent);
        alunaVisual.name = "Aluna Visual";
        alunaVisual.transform.localPosition = Vector3.zero;
        alunaVisual.transform.localRotation = Quaternion.identity;
        alunaVisual.transform.localScale = Vector3.one;

        Animator animator = alunaVisual.GetComponentInChildren<Animator>();
        RuntimeAnimatorController sharedController = alunoAnimator != null ? alunoAnimator.runtimeAnimatorController : null;
        ConfigureAnimator(animator, sharedController);
    }

    private void ConfigureAnimator(Animator activeAnimator, RuntimeAnimatorController sharedController)
    {
        if (activeAnimator != null)
        {
            if (sharedController != null)
            {
                activeAnimator.runtimeAnimatorController = sharedController;
            }

            activeAnimator.applyRootMotion = false;
            activeAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        topDownController?.SetCharacterAnimator(activeAnimator);
    }

    private Transform FindVisualChild(string childName)
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name == childName)
            {
                return children[i];
            }
        }

        return null;
    }
}
