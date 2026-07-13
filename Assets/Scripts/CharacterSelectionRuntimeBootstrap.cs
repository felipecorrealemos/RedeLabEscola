using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CharacterSelectionRuntimeBootstrap : MonoBehaviour
{
    [SerializeField] private GameObject alunoPrefab;
    [SerializeField] private GameObject alunaPrefab;

    private void Awake()
    {
        BuildScene();
    }

    private void BuildScene()
    {
        Material floorMaterial = CreateMaterial("Selection Runtime Floor", new Color(0.44f, 0.52f, 0.48f));
        Material wallMaterial = CreateMaterial("Selection Runtime Backdrop", new Color(0.70f, 0.78f, 0.80f));
        Material platformMaterial = CreateMaterial("Selection Runtime Platform", new Color(0.19f, 0.27f, 0.30f));

        BuildEnvironment(floorMaterial, wallMaterial, platformMaterial);
        BuildCameraAndLights(out Light alunoLight, out Light alunaLight);

        CharacterSelectionController controller = new GameObject("CharacterSelectionController").AddComponent<CharacterSelectionController>();
        CharacterSelectionOption alunoOption = CreateOption("Aluno Option", alunoPrefab, CharacterSelectionChoice.Aluno, new Vector3(-1.18f, 0f, 0f), controller, alunoLight);
        CharacterSelectionOption alunaOption = CreateOption("Aluna Option", alunaPrefab, CharacterSelectionChoice.Aluna, new Vector3(1.18f, 0f, 0f), controller, alunaLight);

        BuildUi(controller, alunoOption, alunaOption);
    }

    private void BuildEnvironment(Material floorMaterial, Material wallMaterial, Material platformMaterial)
    {
        CreateCube("Floor", new Vector3(0f, -0.05f, 0f), new Vector3(7.5f, 0.1f, 5.5f), floorMaterial);
        CreateCube("Backdrop", new Vector3(0f, 1.8f, 1.85f), new Vector3(7.5f, 3.7f, 0.18f), wallMaterial);
        CreateCube("Aluno_Platform", new Vector3(-1.18f, 0.03f, 0.15f), new Vector3(1.6f, 0.06f, 1.25f), platformMaterial);
        CreateCube("Aluna_Platform", new Vector3(1.18f, 0.03f, 0.15f), new Vector3(1.6f, 0.06f, 1.25f), platformMaterial);
    }

    private void BuildCameraAndLights(out Light alunoLight, out Light alunaLight)
    {
        GameObject cameraObject = new GameObject("Character Selection Camera");
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.transform.position = new Vector3(0f, 2.05f, -6.35f);
        camera.transform.rotation = Quaternion.Euler(8f, 0f, 0f);
        camera.fieldOfView = 44f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.68f, 0.76f, 0.78f);

        GameObject keyLightObject = new GameObject("Key Light");
        Light keyLight = keyLightObject.AddComponent<Light>();
        keyLight.type = LightType.Directional;
        keyLight.intensity = 1.2f;
        keyLight.transform.rotation = Quaternion.Euler(45f, -20f, 0f);

        alunoLight = CreatePointLight("Aluno Highlight Light", new Vector3(-1.18f, 2.2f, -1.2f), false);
        alunaLight = CreatePointLight("Aluna Highlight Light", new Vector3(1.18f, 2.2f, -1.2f), false);

        GameObject fillLightObject = new GameObject("Soft Fill Light");
        Light fillLight = fillLightObject.AddComponent<Light>();
        fillLight.type = LightType.Point;
        fillLight.intensity = 1.35f;
        fillLight.range = 7f;
        fillLight.transform.position = new Vector3(0f, 2.1f, -2.8f);
    }

    private CharacterSelectionOption CreateOption(string name, GameObject prefab, CharacterSelectionChoice choice, Vector3 position, CharacterSelectionController controller, Light highlightLight)
    {
        GameObject optionRoot = new GameObject(name);
        optionRoot.transform.position = position;

        if (prefab != null)
        {
            GameObject preview = Instantiate(prefab, optionRoot.transform);
            preview.name = choice == CharacterSelectionChoice.Aluno ? "Aluno Preview" : "Aluna Preview";
            preview.transform.localPosition = Vector3.zero;
            preview.transform.localRotation = Quaternion.Euler(0f, GetPreviewRotationY(choice), 0f);
            preview.transform.localScale = Vector3.one;
            StripGameplayComponents(preview);
        }

        BoxCollider selectionCollider = optionRoot.AddComponent<BoxCollider>();
        selectionCollider.center = new Vector3(0f, 1.05f, 0f);
        selectionCollider.size = new Vector3(1.25f, 2.1f, 0.95f);

        CharacterSelectionOption option = optionRoot.AddComponent<CharacterSelectionOption>();
        option.Configure(choice, controller, optionRoot.transform, highlightLight);
        return option;
    }

    private float GetPreviewRotationY(CharacterSelectionChoice choice)
    {
        return choice == CharacterSelectionChoice.Aluna ? 213.666f : 192.865f;
    }

    private void StripGameplayComponents(GameObject preview)
    {
        foreach (PlayerTopDownController component in preview.GetComponentsInChildren<PlayerTopDownController>(true))
        {
            component.enabled = false;
            Destroy(component);
        }

        foreach (PlayerCharacterVisualApplier component in preview.GetComponentsInChildren<PlayerCharacterVisualApplier>(true))
        {
            component.enabled = false;
            Destroy(component);
        }

        foreach (Collider collider in preview.GetComponentsInChildren<Collider>(true))
        {
            collider.enabled = false;
            Destroy(collider);
        }

        foreach (Transform child in preview.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == "AnchorCarry" || child.name == "Anchor Carry" || child.name == "CarryAnchor")
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
        }
    }

    private void BuildUi(CharacterSelectionController controller, CharacterSelectionOption alunoOption, CharacterSelectionOption alunaOption)
    {
        GameObject canvasObject = new GameObject("Character Selection Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        Image alunoFrame = CreateSelectionFrame(canvasObject.transform, "Aluno Selection Frame", new Vector2(-285f, 585f));
        Image alunaFrame = CreateSelectionFrame(canvasObject.transform, "Aluna Selection Frame", new Vector2(285f, 585f));
        alunoOption.SetSelectionFrame(alunoFrame);
        alunaOption.SetSelectionFrame(alunaFrame);

        Text title = CreateText(canvasObject.transform, "Title", "Escolha seu personagem", 44, FontStyle.Bold, TextAnchor.MiddleCenter);
        ConfigureRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -42f), new Vector2(760f, 70f));

        Button confirmButton = CreateButton(canvasObject.transform, "Confirmar Button", "Comecar", new Vector2(0f, 92f));

        Text confirmationLabel = CreateText(canvasObject.transform, "Confirmation Label", "Escolha um personagem", 32, FontStyle.Bold, TextAnchor.MiddleCenter);
        ConfigureRect(confirmationLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 155f), new Vector2(760f, 60f));

        confirmButton.onClick.AddListener(controller.ConfirmAndStart);

        controller.Configure(alunoOption, alunaOption, confirmButton, confirmationLabel);

        if (FindObjectOfType<EventSystem>() == null)
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }
    }

    private Image CreateSelectionFrame(Transform parent, string name, Vector2 anchoredPosition)
    {
        GameObject frameObject = new GameObject(name, typeof(Image), typeof(Outline));
        frameObject.transform.SetParent(parent, false);
        ConfigureRect(frameObject.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), anchoredPosition, new Vector2(465f, 740f));

        Image image = frameObject.GetComponent<Image>();
        image.sprite = CreateFrameSprite();
        image.type = Image.Type.Sliced;
        image.color = new Color(0.93f, 0.96f, 0.94f, 0.25f);
        image.raycastTarget = false;

        Outline outline = frameObject.GetComponent<Outline>();
        outline.effectColor = new Color(0.93f, 0.96f, 0.94f, 0.75f);
        outline.effectDistance = new Vector2(2f, -2f);
        return image;
    }

    private Sprite CreateFrameSprite()
    {
        const int size = 32;
        const int border = 3;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Character Selection Frame Sprite",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        Color clear = Color.clear;
        Color white = Color.white;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool isBorder = x < border || x >= size - border || y < border || y >= size - border;
                texture.SetPixel(x, y, isBorder ? white : clear);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
    }

    private Button CreateButton(Transform parent, string name, string label, Vector2 anchoredPosition)
    {
        GameObject buttonObject = new GameObject(name, typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        ConfigureRect(buttonObject.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), anchoredPosition, new Vector2(310f, 68f));

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.93f, 0.96f, 0.94f, 0.96f);

        Button button = buttonObject.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = image.color;
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(0.74f, 0.82f, 0.78f, 1f);
        colors.disabledColor = new Color(0.45f, 0.50f, 0.49f, 0.7f);
        button.colors = colors;

        Text text = CreateText(buttonObject.transform, "Text", label, 28, FontStyle.Bold, TextAnchor.MiddleCenter);
        ConfigureRect(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        text.color = new Color(0.07f, 0.09f, 0.10f);

        return button;
    }

    private Text CreateText(Transform parent, string name, string value, int fontSize, FontStyle fontStyle, TextAnchor alignment)
    {
        GameObject textObject = new GameObject(name, typeof(Text));
        textObject.transform.SetParent(parent, false);

        Text text = textObject.GetComponent<Text>();
        text.text = value;
        text.font = GetBuiltInFont();
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = alignment;
        text.color = new Color(0.06f, 0.08f, 0.09f);
        return text;
    }

    private void ConfigureRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
    }

    private GameObject CreateCube(string name, Vector3 position, Vector3 scale, Material material)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.position = position;
        cube.transform.localScale = scale;
        cube.GetComponent<Renderer>().sharedMaterial = material;
        return cube;
    }

    private Light CreatePointLight(string name, Vector3 position, bool enabled)
    {
        GameObject lightObject = new GameObject(name);
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(0.95f, 1f, 0.92f);
        light.intensity = 0.8f;
        light.range = 3f;
        light.enabled = enabled;
        light.transform.position = position;
        return light;
    }

    private Material CreateMaterial(string name, Color color)
    {
        Shader shader = Shader.Find("Standard");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Lit");
        }

        return new Material(shader)
        {
            name = name,
            color = color
        };
    }

    private Font GetBuiltInFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
        {
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        return font;
    }
}
