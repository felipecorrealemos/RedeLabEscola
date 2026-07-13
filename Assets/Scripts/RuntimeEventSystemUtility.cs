using UnityEngine;
using UnityEngine.EventSystems;

public static class RuntimeEventSystemUtility
{
    public static EventSystem EnsureSingleEventSystem()
    {
        EventSystem[] eventSystems = Object.FindObjectsOfType<EventSystem>(true);
        EventSystem selected = null;

        foreach (EventSystem eventSystem in eventSystems)
        {
            if (eventSystem == null)
            {
                continue;
            }

            if (selected == null || eventSystem.gameObject.scene.IsValid() && !selected.gameObject.scene.IsValid())
            {
                selected = eventSystem;
            }
        }

        if (selected == null)
        {
            GameObject eventSystemObject = new GameObject("EventSystem");
            selected = eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<StandaloneInputModule>();
            return selected;
        }

        if (selected.GetComponent<StandaloneInputModule>() == null)
        {
            selected.gameObject.AddComponent<StandaloneInputModule>();
        }

        foreach (EventSystem eventSystem in eventSystems)
        {
            if (eventSystem == null || eventSystem == selected)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(eventSystem.gameObject);
            }
            else
            {
                Object.DestroyImmediate(eventSystem.gameObject);
            }
        }

        return selected;
    }
}
