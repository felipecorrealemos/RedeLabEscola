using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class MainMenuVerticalLayoutRefinement
{
    private const string ScenePath = "Assets/Scenes/MainMenu.unity";
    private const string RoundedTexturePath = "Assets/Materials/Menu/Menu_UI_Rounded.asset";

    [MenuItem("Tools/RedeLabEscola/Refine Main Menu Vertical Layout")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject panelObject = GameObject.Find("Bottom Menu Panel");
        GameObject rowObject = GameObject.Find("Button Row");
        if (panelObject == null || rowObject == null)
        {
            Debug.LogError("Main Menu panel or button container was not found.");
            return;
        }

        Sprite roundedSprite = GetOrCreateRoundedSprite();
        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 0.5f);
        panelRect.anchorMax = new Vector2(0f, 0.5f);
        panelRect.pivot = new Vector2(0f, 0.5f);
        panelRect.anchoredPosition = new Vector2(52f, -145f);
        panelRect.sizeDelta = new Vector2(360f, 390f);

        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.sprite = roundedSprite;
        panelImage.type = Image.Type.Sliced;
        panelImage.color = new Color(0.025f, 0.055f, 0.065f, 0.90f);

        HorizontalLayoutGroup horizontal = rowObject.GetComponent<HorizontalLayoutGroup>();
        if (horizontal != null) Object.DestroyImmediate(horizontal);
        VerticalLayoutGroup vertical = rowObject.GetComponent<VerticalLayoutGroup>();
        if (vertical == null) vertical = rowObject.AddComponent<VerticalLayoutGroup>();
        vertical.padding = new RectOffset(8, 8, 8, 8);
        vertical.spacing = 16f;
        vertical.childAlignment = TextAnchor.MiddleCenter;
        vertical.childControlWidth = true;
        vertical.childControlHeight = true;
        vertical.childForceExpandWidth = false;
        vertical.childForceExpandHeight = false;

        RectTransform rowRect = rowObject.GetComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0.5f, 0.5f);
        rowRect.anchorMax = new Vector2(0.5f, 0.5f);
        rowRect.pivot = new Vector2(0.5f, 0.5f);
        rowRect.anchoredPosition = Vector2.zero;
        rowRect.sizeDelta = new Vector2(310f, 330f);

        foreach (Button button in rowObject.GetComponentsInChildren<Button>(true))
        {
            LayoutElement layout = button.GetComponent<LayoutElement>();
            if (layout == null) layout = button.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = 285f;
            layout.preferredHeight = 58f;

            Image image = button.GetComponent<Image>();
            image.sprite = roundedSprite;
            image.type = Image.Type.Sliced;
            image.color = new Color(0.12f, 0.30f, 0.38f, 0.98f);

            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.24f, 1.24f, 1.24f, 1f);
            colors.pressedColor = new Color(0.72f, 0.82f, 0.86f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.48f, 0.52f, 0.52f, 0.72f);
            colors.fadeDuration = 0.20f;
            button.transition = Selectable.Transition.ColorTint;
            button.colors = colors;

            Text label = button.GetComponentInChildren<Text>(true);
            if (label != null) label.fontSize = 22;
        }

        GameObject professor = GameObject.Find("Professor");
        if (professor != null)
        {
            professor.transform.position = new Vector3(-2.15f, 0f, 0.65f);
            professor.transform.rotation = Quaternion.Euler(0f, 165f, 0f);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Main Menu vertical layout refined and saved.");
    }

    public static Sprite GetOrCreateRoundedSprite()
    {
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(RoundedTexturePath);
        if (texture == null)
        {
            const int size = 64;
            const float radius = 14f;
            texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Menu UI Rounded Texture",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(radius - x, 0f, x - (size - 1f - radius));
                    float dy = Mathf.Max(radius - y, 0f, y - (size - 1f - radius));
                    float alpha = Mathf.Clamp01(radius + 0.5f - Mathf.Sqrt(dx * dx + dy * dy));
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            texture.Apply();
            AssetDatabase.CreateAsset(texture, RoundedTexturePath);
        }

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(RoundedTexturePath);
        if (sprite == null)
        {
            sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(16f, 16f, 16f, 16f));
            sprite.name = "Menu UI Rounded Sprite";
            AssetDatabase.AddObjectToAsset(sprite, texture);
            AssetDatabase.ImportAsset(RoundedTexturePath);
        }
        return sprite;
    }
}
