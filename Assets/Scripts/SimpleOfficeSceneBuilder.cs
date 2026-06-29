using UnityEngine;

/// <summary>
/// Builds a simple low-poly office/classroom scene using only Unity primitives.
/// Attach this script to any GameObject and run BuildScene from Play Mode,
/// or use the context menu in the Inspector.
/// </summary>
public class SimpleOfficeSceneBuilder : MonoBehaviour
{
    [SerializeField] private bool buildOnStart = false;

    private Transform environmentRoot;

    private Material floorMaterial;
    private Material wallMaterial;
    private Material deskMaterial;
    private Material chairMaterial;
    private Material computerMaterial;
    private Material screenMaterial;
    private Material routerMaterial;
    private Material networkPointMaterial;
    private Material outletMaterial;
    private Material doorMaterial;

    private void Start()
    {
        if (buildOnStart)
        {
            BuildScene();
        }
    }

    [ContextMenu("Build Simple Office Scene")]
    public void BuildScene()
    {
        CreateMaterials();
        CreateOrClearEnvironment();

        BuildRoom();
        BuildDesk("Desk_01", -4.2f, 0.9f);
        BuildChair("Chair_01", -4.2f, -0.55f);
        BuildComputer("Computer_01", -4.2f, 0.9f);
        BuildDesk("Desk_02", 4.2f, 0.9f);
        BuildChair("Chair_02", 4.2f, -0.55f);
        BuildComputer("Computer_02", 4.2f, 0.9f);
        BuildRouter();
        BuildWallDetails();
        BuildDoor();
        SetupCamera();
        SetupLight();
    }

    private void CreateMaterials()
    {
        floorMaterial = CreateMaterial("Mat_Floor_LightGray", new Color(0.78f, 0.80f, 0.82f));
        wallMaterial = CreateMaterial("Mat_Walls_LightBlue", new Color(0.63f, 0.78f, 0.88f));
        deskMaterial = CreateMaterial("Mat_Desk_Brown", new Color(0.45f, 0.25f, 0.12f));
        chairMaterial = CreateMaterial("Mat_Chair_Blue", new Color(0.20f, 0.42f, 0.72f));
        computerMaterial = CreateMaterial("Mat_Computer_DarkGray", new Color(0.12f, 0.13f, 0.15f));
        screenMaterial = CreateMaterial("Mat_Screen_DarkBlue", new Color(0.02f, 0.12f, 0.36f));
        routerMaterial = CreateMaterial("Mat_Router_DarkGray", new Color(0.18f, 0.18f, 0.20f));
        networkPointMaterial = CreateMaterial("Mat_NetworkPoint_Green", new Color(0.10f, 0.70f, 0.28f));
        outletMaterial = CreateMaterial("Mat_PowerOutlet_White", new Color(0.93f, 0.93f, 0.88f));
        doorMaterial = CreateMaterial("Mat_Door_Brown", new Color(0.35f, 0.18f, 0.08f));
    }

    private Material CreateMaterial(string materialName, Color color)
    {
        Material material = new Material(GetDefaultShader());
        material.name = materialName;
        material.color = color;
        return material;
    }

    private Shader GetDefaultShader()
    {
        Shader shader = Shader.Find("Standard");

        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Lit");
        }

        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        return shader;
    }

    private void CreateOrClearEnvironment()
    {
        GameObject existingEnvironment = GameObject.Find("Environment");

        if (existingEnvironment == null)
        {
            existingEnvironment = new GameObject("Environment");
        }

        environmentRoot = existingEnvironment.transform;

        for (int i = environmentRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = environmentRoot.GetChild(i);

            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }
    }

    private void BuildRoom()
    {
        CreateCube("Floor", new Vector3(0f, -0.05f, 0f), new Vector3(24f, 0.1f, 8f), floorMaterial);

        CreateCube("Wall_North", new Vector3(0f, 1f, 4f), new Vector3(24f, 2f, 0.25f), wallMaterial);
        CreateCube("Wall_East", new Vector3(12f, 1f, 0f), new Vector3(0.25f, 2f, 8f), wallMaterial);
        CreateCube("Wall_West", new Vector3(-12f, 1f, 0f), new Vector3(0.25f, 2f, 8f), wallMaterial);

        // South wall is split into two parts to leave an opening for the door.
        CreateCube("Wall_South_Left", new Vector3(-6.65f, 1f, -4f), new Vector3(10.7f, 2f, 0.25f), wallMaterial);
        CreateCube("Wall_South_Right", new Vector3(6.65f, 1f, -4f), new Vector3(10.7f, 2f, 0.25f), wallMaterial);
    }

    private void BuildDesk(string deskName, float centerX, float centerZ)
    {
        GameObject desk = CreateEmpty(deskName);

        CreateCube("Desk_Top", new Vector3(centerX, 1.05f, centerZ), new Vector3(3f, 0.2f, 1.4f), deskMaterial, desk.transform);
        CreateCube("Desk_Leg_Front_Left", new Vector3(centerX - 1.3f, 0.5f, centerZ - 0.6f), new Vector3(0.18f, 1f, 0.18f), deskMaterial, desk.transform);
        CreateCube("Desk_Leg_Front_Right", new Vector3(centerX + 1.3f, 0.5f, centerZ - 0.6f), new Vector3(0.18f, 1f, 0.18f), deskMaterial, desk.transform);
        CreateCube("Desk_Leg_Back_Left", new Vector3(centerX - 1.3f, 0.5f, centerZ + 0.6f), new Vector3(0.18f, 1f, 0.18f), deskMaterial, desk.transform);
        CreateCube("Desk_Leg_Back_Right", new Vector3(centerX + 1.3f, 0.5f, centerZ + 0.6f), new Vector3(0.18f, 1f, 0.18f), deskMaterial, desk.transform);
    }

    private void BuildChair(string chairName, float centerX, float centerZ)
    {
        GameObject chair = CreateEmpty(chairName);

        CreateCube("Chair_Seat", new Vector3(centerX, 0.55f, centerZ), new Vector3(1f, 0.18f, 1f), chairMaterial, chair.transform);
        CreateCube("Chair_Back", new Vector3(centerX, 1.15f, centerZ - 0.45f), new Vector3(1f, 1.1f, 0.18f), chairMaterial, chair.transform);
        CreateCube("Chair_Leg_Front_Left", new Vector3(centerX - 0.4f, 0.25f, centerZ + 0.35f), new Vector3(0.14f, 0.5f, 0.14f), chairMaterial, chair.transform);
        CreateCube("Chair_Leg_Front_Right", new Vector3(centerX + 0.4f, 0.25f, centerZ + 0.35f), new Vector3(0.14f, 0.5f, 0.14f), chairMaterial, chair.transform);
        CreateCube("Chair_Leg_Back_Left", new Vector3(centerX - 0.4f, 0.25f, centerZ - 0.35f), new Vector3(0.14f, 0.5f, 0.14f), chairMaterial, chair.transform);
        CreateCube("Chair_Leg_Back_Right", new Vector3(centerX + 0.4f, 0.25f, centerZ - 0.35f), new Vector3(0.14f, 0.5f, 0.14f), chairMaterial, chair.transform);
    }

    private void BuildComputer(string computerName, float centerX, float deskCenterZ)
    {
        GameObject computer = CreateEmpty(computerName);

        CreateCube("Monitor", new Vector3(centerX, 1.75f, deskCenterZ + 0.25f), new Vector3(1.1f, 0.75f, 0.12f), computerMaterial, computer.transform);
        CreateCube("Monitor_Screen", new Vector3(centerX, 1.75f, deskCenterZ + 0.18f), new Vector3(0.9f, 0.55f, 0.04f), screenMaterial, computer.transform);
        CreateCube("Monitor_Stand", new Vector3(centerX, 1.25f, deskCenterZ + 0.25f), new Vector3(0.16f, 0.35f, 0.16f), computerMaterial, computer.transform);
        GameObject computerBase = CreateCube("Computer_Base", new Vector3(centerX + 1.05f, 1.48f, deskCenterZ + 0.2f), new Vector3(0.55f, 0.65f, 0.5f), computerMaterial, computer.transform);
        CreateCube("Keyboard", new Vector3(centerX, 1.2f, deskCenterZ - 0.45f), new Vector3(1.1f, 0.08f, 0.32f), computerMaterial, computer.transform);

        computerBase.AddComponent<MovableDevice>();
        computerBase.AddComponent<ComputerInteractable>();
        CreateComputerDropPoint(computer, computerBase);
    }

    private void CreateComputerDropPoint(GameObject computer, GameObject computerBase)
    {
        GameObject dropPoint = new GameObject("Computer_Base_DropPoint");
        dropPoint.transform.SetParent(computer.transform);
        dropPoint.transform.position = computerBase.transform.position;
        dropPoint.transform.rotation = computerBase.transform.rotation;

        dropPoint.AddComponent<DeviceDropZone>();
        dropPoint.AddComponent<BoxCollider>().isTrigger = true;
    }

    private void BuildRouter()
    {
        GameObject router = CreateEmpty("Router");
        router.AddComponent<RouterInteractable>();

        CreateCube("Router_Body", new Vector3(8.5f, 0.2f, 2.9f), new Vector3(1.1f, 0.25f, 0.65f), routerMaterial, router.transform);
        CreateCube("Router_Light_1", new Vector3(8.2f, 0.35f, 2.55f), new Vector3(0.12f, 0.06f, 0.04f), networkPointMaterial, router.transform);
        CreateCube("Router_Light_2", new Vector3(8.45f, 0.35f, 2.55f), new Vector3(0.12f, 0.06f, 0.04f), networkPointMaterial, router.transform);
        CreateCube("Router_Antenna_Left", new Vector3(8.05f, 0.75f, 3.15f), new Vector3(0.08f, 0.9f, 0.08f), routerMaterial, router.transform);
        CreateCube("Router_Antenna_Right", new Vector3(8.95f, 0.75f, 3.15f), new Vector3(0.08f, 0.9f, 0.08f), routerMaterial, router.transform);
    }

    private void BuildWallDetails()
    {
        CreateNetworkPoint("NetworkPoint_01", new Vector3(-4.2f, 0.85f, 3.86f));
        CreateCube("PowerOutlet_01", new Vector3(-3.55f, 0.45f, 3.86f), new Vector3(0.4f, 0.28f, 0.08f), outletMaterial);
        CreateNetworkPoint("NetworkPoint_02", new Vector3(4.2f, 0.85f, 3.86f));
        CreateCube("PowerOutlet_02", new Vector3(4.85f, 0.45f, 3.86f), new Vector3(0.4f, 0.28f, 0.08f), outletMaterial);
    }

    private void CreateNetworkPoint(string pointName, Vector3 position)
    {
        GameObject networkPoint = CreateCube(pointName, position, new Vector3(0.35f, 0.35f, 0.08f), networkPointMaterial);
        networkPoint.AddComponent<NetworkJackConnectionPoint>();
    }

    private void BuildDoor()
    {
        CreateCube("Door", new Vector3(0f, 0.9f, -4.13f), new Vector3(1.8f, 1.8f, 0.12f), doorMaterial);
    }

    private void SetupCamera()
    {
        Camera camera = Camera.main;

        if (camera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            camera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
        }

        camera.name = "Main Camera";
        camera.transform.position = new Vector3(0f, 16f, -12f);
        camera.transform.rotation = Quaternion.Euler(55f, 0f, 0f);
        camera.orthographic = true;
        camera.orthographicSize = 4.6f;
        camera.clearFlags = CameraClearFlags.Skybox;
    }

    private void SetupLight()
    {
        Light directionalLight = FindObjectOfType<Light>();

        if (directionalLight == null || directionalLight.type != LightType.Directional)
        {
            GameObject lightObject = new GameObject("Directional Light");
            directionalLight = lightObject.AddComponent<Light>();
        }

        directionalLight.name = "Directional Light";
        directionalLight.type = LightType.Directional;
        directionalLight.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        directionalLight.intensity = 1.2f;
    }

    private GameObject CreateEmpty(string objectName)
    {
        GameObject gameObject = new GameObject(objectName);
        gameObject.transform.SetParent(environmentRoot);
        gameObject.transform.localPosition = Vector3.zero;
        return gameObject;
    }

    private GameObject CreateCube(string objectName, Vector3 position, Vector3 scale, Material material)
    {
        return CreateCube(objectName, position, scale, material, environmentRoot);
    }

    private GameObject CreateCube(string objectName, Vector3 position, Vector3 scale, Material material, Transform parent)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = objectName;
        cube.transform.SetParent(parent);
        cube.transform.position = position;
        cube.transform.localScale = scale;

        Renderer renderer = cube.GetComponent<Renderer>();
        renderer.sharedMaterial = material;

        return cube;
    }
}
