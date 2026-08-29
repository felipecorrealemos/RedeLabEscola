using RedeLabEscola.Auth;
using RedeLabEscola.Menu;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class RedeLabOnlineResumeSceneSetup
{
    private const string MainMenuPath = "Assets/Scenes/MainMenu.unity";
    private const string OfficePath = "Assets/Scenes/SampleScene.unity";
    private const string FactoryPath = "Assets/Scenes/Stage2/Stage2_Factory.unity";

    [MenuItem("Tools/RedeLabEscola/Online/Setup New Game And Resume Scenes")]
    public static void SetupAll()
    {
        SetupMainMenu();
        SetupGameplayScene(OfficePath, false);
        SetupGameplayScene(FactoryPath, true);
        AssetDatabase.SaveAssets();
        Debug.Log("UI de Novo Jogo e pontos de retomada configurados nas cenas.");
    }

    private static void SetupMainMenu()
    {
        Scene scene = EditorSceneManager.OpenScene(MainMenuPath, OpenSceneMode.Single);
        MainMenuController controller = Object.FindObjectOfType<MainMenuController>(true);
        Canvas canvas = Object.FindObjectOfType<Canvas>(true);
        if (controller == null || canvas == null) throw new System.InvalidOperationException("MainMenu sem controller/canvas.");

        Transform existing = canvas.transform.Find("NewGameConfirmPanel");
        GameObject panel = existing != null ? existing.gameObject : CreateUiObject("NewGameConfirmPanel", canvas.transform, typeof(Image));
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        Stretch(panelRect);
        Image overlayImage = panel.GetComponent<Image>();
        overlayImage.color = new Color(0f, 0f, 0f, 0.72f);

        Transform windowTransform = panel.transform.Find("Window");
        GameObject window = windowTransform != null ? windowTransform.gameObject : CreateUiObject("Window", panel.transform, typeof(Image), typeof(VerticalLayoutGroup));
        RectTransform windowRect = window.GetComponent<RectTransform>();
        windowRect.anchorMin = windowRect.anchorMax = new Vector2(0.5f, 0.5f);
        windowRect.pivot = new Vector2(0.5f, 0.5f);
        windowRect.sizeDelta = new Vector2(660f, 360f);
        windowRect.anchoredPosition = Vector2.zero;
        Image windowImage = window.GetComponent<Image>();
        windowImage.color = new Color(0.025f, 0.07f, 0.085f, 0.98f);
        VerticalLayoutGroup vertical = window.GetComponent<VerticalLayoutGroup>();
        vertical.padding = new RectOffset(48, 48, 38, 34);
        vertical.spacing = 22f;
        vertical.childAlignment = TextAnchor.MiddleCenter;
        vertical.childControlWidth = true;
        vertical.childControlHeight = false;
        vertical.childForceExpandWidth = true;
        vertical.childForceExpandHeight = false;

        Text title = GetOrCreateText(window.transform, "TitleText", "TEM CERTEZA QUE DESEJA INICIAR UM NOVO JOGO?", 26, FontStyle.Bold, 70f);
        Text message = GetOrCreateText(window.transform, "MessageText", "Você possui um jogo em andamento. Ao iniciar um novo jogo, todo o progresso salvo será apagado e não poderá ser recuperado.", 20, FontStyle.Normal, 125f);

        Transform buttonsTransform = window.transform.Find("Buttons");
        GameObject buttons = buttonsTransform != null ? buttonsTransform.gameObject : CreateUiObject("Buttons", window.transform, typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        LayoutElement buttonsLayout = buttons.GetComponent<LayoutElement>();
        buttonsLayout.preferredHeight = 62f;
        HorizontalLayoutGroup horizontal = buttons.GetComponent<HorizontalLayoutGroup>();
        horizontal.spacing = 24f;
        horizontal.childAlignment = TextAnchor.MiddleCenter;
        horizontal.childControlWidth = false;
        horizontal.childControlHeight = true;
        horizontal.childForceExpandWidth = false;
        horizontal.childForceExpandHeight = true;

        Button cancel = GetOrCreateButton(buttons.transform, "CancelButton", "Cancelar", new Color(0.20f, 0.30f, 0.34f, 1f));
        Button confirm = GetOrCreateButton(buttons.transform, "ConfirmButton", "Iniciar novo jogo", new Color(0.78f, 0.20f, 0.16f, 1f));
        cancel.onClick = new Button.ButtonClickedEvent();
        confirm.onClick = new Button.ButtonClickedEvent();
        UnityEventTools.AddPersistentListener(cancel.onClick, controller.CancelNewGame);
        UnityEventTools.AddPersistentListener(confirm.onClick, controller.ConfirmNewGame);

        SerializedObject serialized = new SerializedObject(controller);
        serialized.FindProperty("newGameConfirmPanel").objectReferenceValue = panel;
        serialized.FindProperty("newGameConfirmTitle").objectReferenceValue = title;
        serialized.FindProperty("newGameConfirmMessage").objectReferenceValue = message;
        serialized.FindProperty("newGameCancelButton").objectReferenceValue = cancel;
        serialized.FindProperty("newGameConfirmButton").objectReferenceValue = confirm;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        panel.SetActive(false);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void SetupGameplayScene(string path, bool factory)
    {
        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        GameObject root = GameObject.Find("RedeLabOnlineResume");
        if (root == null) root = new GameObject("RedeLabOnlineResume");
        RedeLabSceneStateRestorer restorer = root.GetComponent<RedeLabSceneStateRestorer>();
        if (restorer == null) restorer = root.AddComponent<RedeLabSceneStateRestorer>();

        PlayerTopDownController player = Object.FindObjectOfType<PlayerTopDownController>(true);
        Vector3 playerPosition = player != null ? player.transform.position : Vector3.zero;
        Quaternion playerRotation = player != null ? player.transform.rotation : Quaternion.identity;

        SerializedObject serialized = new SerializedObject(restorer);
        if (factory)
        {
            Transform existingFactorySpawn = FindTransform("Stage2_PlayerSpawn");
            Transform spawn = GetOrCreateSpawn(root.transform, "SpawnFactory",
                existingFactorySpawn != null ? existingFactorySpawn.position : playerPosition,
                existingFactorySpawn != null ? existingFactorySpawn.rotation : playerRotation);
            serialized.FindProperty("spawnFactory").objectReferenceValue = spawn;
        }
        else
        {
            Transform room1 = FindRoom(1);
            Transform room2 = FindRoom(2);
            Transform room3 = FindRoom(3);
            Transform spawn1 = GetOrCreateSpawn(root.transform, "SpawnSala1", playerPosition, playerRotation);
            Transform spawn2 = GetOrCreateSpawn(root.transform, "SpawnSala2", ProvisionalRoomPosition(room2, playerPosition), playerRotation);
            Transform spawn3 = GetOrCreateSpawn(root.transform, "SpawnSala3", ProvisionalRoomPosition(room3, playerPosition), playerRotation);
            serialized.FindProperty("spawnSala1").objectReferenceValue = spawn1;
            serialized.FindProperty("spawnSala2").objectReferenceValue = spawn2;
            serialized.FindProperty("spawnSala3").objectReferenceValue = spawn3;
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static Vector3 ProvisionalRoomPosition(Transform room, Vector3 fallback)
    {
        if (room == null) return fallback;
        Vector3 result = room.position;
        result.y = fallback.y;
        return result;
    }

    private static Transform FindRoom(int number)
    {
        Transform[] all = Object.FindObjectsOfType<Transform>(true);
        foreach (Transform item in all)
        {
            string normalized = item.name.Trim().ToLowerInvariant();
            if (normalized == "sala " + number || normalized == "sala" + number) return item;
        }
        return null;
    }

    private static Transform FindTransform(string objectName)
    {
        Transform[] all = Object.FindObjectsOfType<Transform>(true);
        foreach (Transform item in all) if (item.name == objectName) return item;
        return null;
    }

    private static Transform GetOrCreateSpawn(Transform parent, string name, Vector3 position, Quaternion rotation)
    {
        Transform spawn = parent.Find(name);
        if (spawn == null)
        {
            spawn = new GameObject(name).transform;
            spawn.SetParent(parent, true);
            spawn.SetPositionAndRotation(position, rotation);
        }
        return spawn;
    }

    private static Text GetOrCreateText(Transform parent, string name, string content, int fontSize, FontStyle style, float height)
    {
        Transform existing = parent.Find(name);
        GameObject target = existing != null ? existing.gameObject : CreateUiObject(name, parent, typeof(Text), typeof(LayoutElement));
        Text text = target.GetComponent<Text>();
        text.text = content;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        target.GetComponent<LayoutElement>().preferredHeight = height;
        return text;
    }

    private static Button GetOrCreateButton(Transform parent, string name, string label, Color color)
    {
        Transform existing = parent.Find(name);
        GameObject target = existing != null ? existing.gameObject : CreateUiObject(name, parent, typeof(Image), typeof(Button), typeof(LayoutElement));
        target.GetComponent<Image>().color = color;
        LayoutElement layout = target.GetComponent<LayoutElement>();
        layout.preferredWidth = 245f;
        layout.preferredHeight = 58f;
        Text text = GetOrCreateText(target.transform, "Text", label, 20, FontStyle.Bold, 58f);
        Stretch(text.rectTransform);
        return target.GetComponent<Button>();
    }

    private static GameObject CreateUiObject(string name, Transform parent, params System.Type[] components)
    {
        GameObject target = new GameObject(name, typeof(RectTransform));
        foreach (System.Type component in components) if (target.GetComponent(component) == null) target.AddComponent(component);
        target.transform.SetParent(parent, false);
        return target;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
