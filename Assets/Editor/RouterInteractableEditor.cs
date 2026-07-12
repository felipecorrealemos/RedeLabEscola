using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RouterInteractable))]
[CanEditMultipleObjects]
public class RouterInteractableEditor : Editor
{
    private const string WiFiRangePropertyName = "wifiRange";
    private static readonly GUIContent WiFiRangeLabel = new GUIContent("Wi-Fi Range");

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SerializedProperty property = serializedObject.GetIterator();
        bool enterChildren = true;
        while (property.NextVisible(enterChildren))
        {
            using (new EditorGUI.DisabledScope(property.propertyPath == "m_Script"))
            {
                if (property.propertyPath == WiFiRangePropertyName)
                {
                    DrawWiFiRangeProperty(property);
                }
                else
                {
                    EditorGUILayout.PropertyField(property, true);
                }
            }

            enterChildren = false;
        }

        serializedObject.ApplyModifiedProperties();
    }

    private static void DrawWiFiRangeProperty(SerializedProperty property)
    {
        Rect row = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight);
        row = EditorGUI.PrefixLabel(row, WiFiRangeLabel);

        const float buttonWidth = 28f;
        const float spacing = 4f;
        Rect minusRect = new Rect(row.x, row.y, buttonWidth, row.height);
        Rect valueRect = new Rect(minusRect.xMax + spacing, row.y, row.width - (buttonWidth * 2f) - (spacing * 2f), row.height);
        Rect plusRect = new Rect(valueRect.xMax + spacing, row.y, buttonWidth, row.height);

        if (GUI.Button(minusRect, "-"))
        {
            property.intValue = Mathf.Max(0, property.intValue - 1);
        }

        EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
        EditorGUI.BeginChangeCheck();
        int typedValue = EditorGUI.IntField(valueRect, GUIContent.none, property.intValue);
        if (EditorGUI.EndChangeCheck())
        {
            property.intValue = Mathf.Max(0, typedValue);
        }
        EditorGUI.showMixedValue = false;

        if (GUI.Button(plusRect, "+"))
        {
            property.intValue = Mathf.Max(0, property.intValue + 1);
        }
    }
}
