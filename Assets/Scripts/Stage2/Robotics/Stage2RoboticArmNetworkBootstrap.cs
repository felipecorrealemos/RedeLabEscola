using UnityEngine;

[DefaultExecutionOrder(-70)]
public class Stage2RoboticArmNetworkBootstrap : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void ConfigureRoboticArmNetworkAdapters()
    {
        MissionManager.EnsureForCurrentScene();
        ConfigureArm("RoboticArm_Pipes", "Bra\u00e7o Rob\u00f3tico 1", "stage2-robotic-arm-01");
        ConfigureArm("RoboticArm_Beams", "Bra\u00e7o Rob\u00f3tico 2", "stage2-robotic-arm-02");
        ConfigureArm("RoboticArm_Ingots", "Bra\u00e7o Rob\u00f3tico 3", "stage2-robotic-arm-03");
    }

    private static void ConfigureArm(string objectName, string deviceName, string deviceId)
    {
        GameObject armObject = GameObject.Find(objectName);
        if (armObject == null)
        {
            return;
        }

        WiFiDevice wiFiDevice = armObject.GetComponent<WiFiDevice>();
        if (wiFiDevice == null)
        {
            Debug.LogWarning(objectName + " does not have a WiFiDevice component. Run the Stage2 robotic arm scene setup to add it persistently.", armObject);
            return;
        }

        wiFiDevice.ConfigureIdentity(WiFiDeviceType.RoboticArm, deviceId);

        RoboticArmNetworkAdapter adapter = armObject.GetComponent<RoboticArmNetworkAdapter>();
        if (adapter == null)
        {
            Debug.LogWarning(objectName + " does not have a RoboticArmNetworkAdapter component. Run the Stage2 robotic arm scene setup to add it persistently.", armObject);
            return;
        }

        adapter.ConfigureIdentity(deviceName, deviceId);
        adapter.ConfigureReferences(wiFiDevice, FindStatusRenderer(armObject.transform), null);
    }

    private static Renderer FindStatusRenderer(Transform root)
    {
        if (root == null)
        {
            return null;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            string lowerName = renderer.name.ToLowerInvariant();
            if (lowerName.Contains("light_yellow") || lowerName.Contains("light_red") || lowerName.Contains("indicator") || lowerName.Contains("status"))
            {
                return renderer;
            }
        }

        return null;
    }
}
