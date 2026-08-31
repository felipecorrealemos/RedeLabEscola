using System.IO;
using RedeLabEscola.Auth;
using RedeLabEscola.Menu;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class RedeLabExitAndSessionSceneSetup
{
    private const string PrefabDirectory = "Assets/Prefabs/UI";
    private const string PrefabPath = PrefabDirectory + "/GameplayExitUI.prefab";
    private static readonly string[] GameplayScenes =
    {
        SceneNames.OfficePath,
        SceneNames.FactoryPath,
        SceneNames.ProviderPath
    };

    [MenuItem("Tools/RedeLabEscola/Online/Setup Exit And Session UI")]
    public static void SetupAll()
    {
        GameObject prefab = CreateOrUpdatePrefab();
        foreach (string scenePath in GameplayScenes) InstallInGameplayScene(scenePath, prefab);
        InstallMainMenuFallback();
        AssetDatabase.SaveAssets();
        Debug.Log("Fluxo de saida e mensagens de sessao configurados nas cenas e no prefab.");
    }

    private static GameObject CreateOrUpdatePrefab()
    {
        if (!Directory.Exists(PrefabDirectory)) Directory.CreateDirectory(PrefabDirectory);

        GameObject root = new GameObject("GameplayExitUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.localScale = Vector3.one;
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        GraphicRaycaster modalRaycaster = root.GetComponent<GraphicRaycaster>();
        modalRaycaster.enabled = false;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject menu = CreatePanel(root.transform, "PauseMenuPanel", new Vector2(420f, 190f));
        Text menuTitle = CreateText(menu.transform, "Title", "MENU", new Vector2(0f, 48f), new Vector2(360f, 44f), 24, FontStyle.Bold);
        Button leave = CreateButton(menu.transform, "LeaveGameButton", "Sair do jogo", new Vector2(0f, -35f), new Vector2(220f, 46f));

        GameObject confirmation = CreatePanel(root.transform, "QuitConfirmationPanel", new Vector2(460f, 230f));
        Text confirmationTitle = CreateText(confirmation.transform, "Title", "Deseja sair do jogo?", new Vector2(0f, 60f), new Vector2(410f, 44f), 24, FontStyle.Bold);
        Text hint = CreateText(confirmation.transform, "Hint", "Seu progresso salvo sera mantido.", new Vector2(0f, 15f), new Vector2(410f, 36f), 16, FontStyle.Normal);
        Button yes = CreateButton(confirmation.transform, "YesButton", "Sim", new Vector2(-82f, -66f), new Vector2(120f, 42f));
        Button no = CreateButton(confirmation.transform, "NoButton", "Nao", new Vector2(82f, -66f), new Vector2(120f, 42f));

        GameObject sessionMessage = new GameObject("SessionRenewalMessage", typeof(RectTransform), typeof(Image));
        sessionMessage.transform.SetParent(root.transform, false);
        RectTransform messageRect = sessionMessage.GetComponent<RectTransform>();
        messageRect.anchorMin = messageRect.anchorMax = new Vector2(0.5f, 1f);
        messageRect.pivot = new Vector2(0.5f, 1f);
        messageRect.anchoredPosition = new Vector2(0f, -24f);
        messageRect.sizeDelta = new Vector2(760f, 52f);
        Image sessionBackground = sessionMessage.GetComponent<Image>();
        sessionBackground.color = new Color(0.42f, 0.20f, 0.04f, 0.94f);
        sessionBackground.raycastTarget = false;
        Text sessionLabel = CreateText(sessionMessage.transform, "Message", "Sua sessao precisa ser renovada. O progresso sera sincronizado quando o acesso voltar.", Vector2.zero, new Vector2(720f, 44f), 16, FontStyle.Normal);
        sessionLabel.raycastTarget = false;

        QuitConfirmationDialog controller = root.AddComponent<QuitConfirmationDialog>();
        SerializedObject exitSerialized = new SerializedObject(controller);
        exitSerialized.FindProperty("menuPanel").objectReferenceValue = menu;
        exitSerialized.FindProperty("confirmationPanel").objectReferenceValue = confirmation;
        exitSerialized.FindProperty("leaveGameButton").objectReferenceValue = leave;
        exitSerialized.FindProperty("confirmButton").objectReferenceValue = yes;
        exitSerialized.FindProperty("cancelButton").objectReferenceValue = no;
        exitSerialized.FindProperty("modalRaycaster").objectReferenceValue = modalRaycaster;
        exitSerialized.FindProperty("menuTitleLabel").objectReferenceValue = menuTitle;
        exitSerialized.FindProperty("leaveGameLabel").objectReferenceValue = leave.GetComponentInChildren<Text>();
        exitSerialized.FindProperty("confirmationTitleLabel").objectReferenceValue = confirmationTitle;
        exitSerialized.FindProperty("confirmationHintLabel").objectReferenceValue = hint;
        exitSerialized.ApplyModifiedPropertiesWithoutUndo();

        RedeLabSessionStatusUI status = root.AddComponent<RedeLabSessionStatusUI>();
        SerializedObject statusSerialized = new SerializedObject(status);
        statusSerialized.FindProperty("messageRoot").objectReferenceValue = sessionMessage;
        statusSerialized.FindProperty("messageLabel").objectReferenceValue = sessionLabel;
        statusSerialized.ApplyModifiedPropertiesWithoutUndo();

        menu.SetActive(false);
        confirmation.SetActive(false);
        sessionMessage.SetActive(false);
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static void InstallInGameplayScene(string scenePath, GameObject prefab)
    {
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        GameObject existing = GameObject.Find("GameplayExitUI");
        if (existing != null) Object.DestroyImmediate(existing);
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        instance.name = "GameplayExitUI";
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void InstallMainMenuFallback()
    {
        Scene scene = EditorSceneManager.OpenScene(SceneNames.MainMenuPath, OpenSceneMode.Single);
        MainMenuController controller = Object.FindObjectOfType<MainMenuController>(true);
        Canvas canvas = Object.FindObjectOfType<Canvas>(true);
        if (controller == null || canvas == null) throw new System.InvalidOperationException("MainMenu sem controller/canvas.");

        Transform existing = canvas.transform.Find("QuitFallbackMessage");
        Text label;
        if (existing != null)
        {
            label = existing.GetComponent<Text>();
        }
        else
        {
            label = CreateText(canvas.transform, "QuitFallbackMessage", "Você pode fechar esta aba do navegador.", Vector2.zero, new Vector2(720f, 44f), 19, FontStyle.Bold);
        }
        RectTransform rect = label.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 42f);
        label.color = new Color(1f, 0.92f, 0.68f, 1f);
        label.gameObject.SetActive(false);

        SerializedObject serialized = new SerializedObject(controller);
        serialized.FindProperty("quitFallbackMessageLabel").objectReferenceValue = label;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static GameObject CreatePanel(Transform parent, string name, Vector2 size)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        panel.GetComponent<Image>().color = new Color(0.015f, 0.055f, 0.07f, 0.96f);
        return panel;
    }

    private static Text CreateText(Transform parent, string name, string content, Vector2 position, Vector2 size, int fontSize, FontStyle style)
    {
        GameObject target = new GameObject(name, typeof(RectTransform), typeof(Text));
        target.transform.SetParent(parent, false);
        RectTransform rect = target.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Text label = target.GetComponent<Text>();
        label.text = content;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = fontSize;
        label.fontStyle = style;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        return label;
    }

    private static Button CreateButton(Transform parent, string name, string content, Vector2 position, Vector2 size)
    {
        GameObject target = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        target.transform.SetParent(parent, false);
        RectTransform rect = target.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        target.GetComponent<Image>().color = new Color(0.88f, 0.94f, 0.96f, 1f);
        Text label = CreateText(target.transform, "Text", content, Vector2.zero, size, 18, FontStyle.Bold);
        label.color = new Color(0.04f, 0.09f, 0.11f, 1f);
        return target.GetComponent<Button>();
    }
}
