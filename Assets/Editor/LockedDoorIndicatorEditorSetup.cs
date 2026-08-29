using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class LockedDoorIndicatorEditorSetup
{
    private const string IconPath = "Assets/Imagens/Caminho_bloqueado.png";
    private const string CanvasName = "LockedDoorIndicatorCanvas";

    [MenuItem("Tools/RedeLabEscola/Doors/Ensure Shared Locked Door Indicator")]
    public static void EnsureSharedIndicator()
    {
        if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
        NetworkDoorDevice[] doors = Object.FindObjectsOfType<NetworkDoorDevice>(true);
        if (doors.Length == 0) return;
        RemovePerDoorIndicators(doors);
        Scene scene = doors[0].gameObject.scene;
        GameObject canvasObject = FindSceneRoot(scene, CanvasName);
        if (canvasObject == null)
        {
            canvasObject = new GameObject(CanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(LockedDoorIndicatorUI));
            SceneManager.MoveGameObjectToScene(canvasObject, scene);
            Undo.RegisterCreatedObjectUndo(canvasObject, "Create shared locked door indicator");
        }

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 65;
        LockedDoorIndicatorUI presenter = canvasObject.GetComponent<LockedDoorIndicatorUI>();
        if (presenter == null) presenter = Undo.AddComponent<LockedDoorIndicatorUI>(canvasObject);

        GameObject panelObject = GetOrCreateChild(canvasObject.transform, "LockedDoorIndicator", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
        RectTransform panel = panelObject.GetComponent<RectTransform>();
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
        if (panel.sizeDelta == Vector2.zero) panel.sizeDelta = new Vector2(160f, 128f);
        Image background = panelObject.GetComponent<Image>();
        if (background.sprite == null)
        {
            background.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
            background.type = Image.Type.Sliced;
            background.color = new Color(0f, 0f, 0f, 0.86f);
        }
        CanvasGroup group = panelObject.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        GameObject iconObject = GetOrCreateChild(panel, "BlockedIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 1f);
        iconRect.pivot = new Vector2(0.5f, 1f);
        if (iconRect.sizeDelta == Vector2.zero) iconRect.sizeDelta = new Vector2(82f, 82f);
        Image iconImage = iconObject.GetComponent<Image>();
        Sprite icon = AssetDatabase.LoadAssetAtPath<Sprite>(IconPath);
        iconImage.sprite = icon;
        iconImage.preserveAspect = true;

        GameObject labelObject = GetOrCreateChild(panel, "Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(1f, 0f);
        labelRect.anchoredPosition = new Vector2(0f, 8f);
        labelRect.sizeDelta = new Vector2(-12f, 28f);
        Text label = labelObject.GetComponent<Text>();
        label.text = "Porta Trancada";
        if (label.font == null) label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 17;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;

        presenter.ConfigureEditor(canvas, panel, group, iconRect, iconImage, label, icon);
        EditorUtility.SetDirty(presenter);
        EditorSceneManager.MarkSceneDirty(scene);
    }

    private static void RemovePerDoorIndicators(NetworkDoorDevice[] doors)
    {
        foreach (NetworkDoorDevice door in doors)
        {
            LockedDoorIndicatorUI oldPresenter = door.GetComponent<LockedDoorIndicatorUI>();
            if (oldPresenter != null) Undo.DestroyObjectImmediate(oldPresenter);
            Transform oldCanvas = door.transform.Find(CanvasName);
            if (oldCanvas != null) Undo.DestroyObjectImmediate(oldCanvas.gameObject);
        }
    }

    private static GameObject FindSceneRoot(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects()) if (root.name == objectName) return root;
        return null;
    }

    private static GameObject GetOrCreateChild(Transform parent, string childName, params System.Type[] components)
    {
        Transform existing = parent.Find(childName);
        if (existing != null) return existing.gameObject;
        GameObject child = new GameObject(childName, components);
        Undo.RegisterCreatedObjectUndo(child, "Create " + childName);
        child.transform.SetParent(parent, false);
        return child;
    }
}
