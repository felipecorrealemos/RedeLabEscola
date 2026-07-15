using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ConveyorItemSpawner : MonoBehaviour
{
    [Serializable]
    public class ProductDefinition
    {
        public GameObject prefab;
        public string productId = "Product";
        public float probabilityWeight = 1f;
        public Vector3 initialRotation;
        public Vector3 scale = Vector3.one;
    }

    [SerializeField] private ConveyorController conveyorController;
    [SerializeField] private ConveyorPath conveyorPath;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private List<ProductDefinition> products = new List<ProductDefinition>();
    [SerializeField] private ConveyorSpawnMode spawnMode = ConveyorSpawnMode.Sequential;
    [SerializeField] private float spawnInterval = 1.5f;
    [SerializeField] private int maximumActiveItems = 80;
    [SerializeField] private bool spawnAutomatically = true;
    [SerializeField] private bool pauseSpawningWhenBlocked = true;
    [SerializeField] private int itemsPerCycle = 0;
    [SerializeField] private float intervalBetweenCycles = 0f;
    [SerializeField] private bool createPlaceholderWhenPrefabMissing = true;
    [SerializeField] private Vector3 placeholderSize = new Vector3(0.45f, 0.25f, 0.45f);
    [SerializeField] private int spawnedInCurrentCycle;
    [SerializeField] private bool pausedByJam;
    [SerializeField] private float nextSpawnTimer;

    private int sequentialIndex;
    private float cycleDelayTimer;

    public IReadOnlyList<ProductDefinition> Products => products;
    public bool PausedByJam => pausedByJam;

    public void EnsureMinimumActiveItems(int minimumActiveItems)
    {
        maximumActiveItems = Mathf.Max(maximumActiveItems, minimumActiveItems);
    }

    public void Configure(ConveyorController controller, ConveyorPath path, Transform point, List<ProductDefinition> productDefinitions)
    {
        conveyorController = controller;
        conveyorPath = path;
        spawnPoint = point;

        if (!HasConfiguredProducts() && productDefinitions != null)
        {
            products = productDefinitions;
        }
    }

    public bool HasConfiguredProducts()
    {
        if (products == null || products.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < products.Count; i++)
        {
            if (products[i] != null && products[i].prefab != null)
            {
                return true;
            }
        }

        return false;
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void Start()
    {
        nextSpawnTimer = Mathf.Max(0.05f, spawnInterval);
    }

    private void OnValidate()
    {
        spawnInterval = Mathf.Max(0.05f, spawnInterval);
        maximumActiveItems = Mathf.Max(1, maximumActiveItems);
        intervalBetweenCycles = Mathf.Max(0f, intervalBetweenCycles);
        placeholderSize = new Vector3(Mathf.Max(0.05f, placeholderSize.x), Mathf.Max(0.05f, placeholderSize.y), Mathf.Max(0.05f, placeholderSize.z));
    }

    private void Update()
    {
        if (!spawnAutomatically || pausedByJam)
        {
            return;
        }

        if (itemsPerCycle > 0 && spawnedInCurrentCycle >= itemsPerCycle)
        {
            cycleDelayTimer += Time.deltaTime;
            if (cycleDelayTimer < intervalBetweenCycles)
            {
                return;
            }

            cycleDelayTimer = 0f;
            spawnedInCurrentCycle = 0;
        }

        nextSpawnTimer -= Time.deltaTime;
        if (nextSpawnTimer > 0f)
        {
            return;
        }

        if (TrySpawnNextItem())
        {
            nextSpawnTimer = spawnInterval;
            spawnedInCurrentCycle++;
        }
        else if (!pauseSpawningWhenBlocked)
        {
            nextSpawnTimer = spawnInterval;
        }
        else
        {
            nextSpawnTimer = 0.2f;
        }
    }

    public void SetPausedByJam(bool paused)
    {
        pausedByJam = paused;
    }

    public bool TrySpawnNextItem()
    {
        ResolveReferences();

        if (conveyorController == null || conveyorPath == null || !conveyorPath.IsValid())
        {
            return false;
        }

        if (!conveyorController.CanSpawn || conveyorController.ActiveItems.Count >= maximumActiveItems)
        {
            return false;
        }

        float spawnDistance = spawnPoint != null ? conveyorPath.GetClosestDistance(spawnPoint.position) : 0f;
        if (conveyorController.IsSpawnBlocked(spawnDistance))
        {
            return false;
        }

        ProductDefinition product = ChooseProduct();
        if (product == null)
        {
            return false;
        }

        float lateralOffset = conveyorController.GetNextLateralOffset();
        ConveyorPathSample spawnSample = conveyorPath.GetSample(spawnDistance);
        Vector3 spawnPosition = spawnSample.Position + spawnSample.Lateral * lateralOffset;
        Quaternion spawnRotation = spawnSample.Direction.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(spawnSample.Direction, Vector3.up)
            : Quaternion.Euler(product.initialRotation);

        GameObject itemObject = CreateItemObject(product, spawnPosition, spawnRotation);
        if (itemObject == null)
        {
            return false;
        }

        itemObject.name = string.IsNullOrWhiteSpace(product.productId) ? "ConveyorItem" : product.productId;
        itemObject.transform.SetParent(conveyorController.transform, true);
        itemObject.transform.localScale = product.scale == Vector3.zero ? Vector3.one : product.scale;
        itemObject.transform.rotation = Quaternion.Euler(product.initialRotation);
        SetLayerRecursively(itemObject, LayerMaskToLayer(conveyorController.ConveyorItemLayer));

        ConveyorItem item = itemObject.GetComponent<ConveyorItem>();
        if (item == null)
        {
            item = itemObject.AddComponent<ConveyorItem>();
        }

        if (item == null)
        {
            Debug.LogError($"Could not add ConveyorItem to spawned product '{itemObject.name}'. Check prefab components.", itemObject);
            Destroy(itemObject);
            return false;
        }

        conveyorController.RegisterItem(item);
        item.Initialize(conveyorController, conveyorPath, product.productId, spawnDistance, lateralOffset);
        return true;
    }

    private void ResolveReferences()
    {
        if (conveyorController == null)
        {
            conveyorController = GetComponentInParent<ConveyorController>();
        }

        if (conveyorPath == null)
        {
            conveyorPath = conveyorController != null ? conveyorController.ConveyorPath : GetComponentInParent<ConveyorPath>();
        }

        if (spawnPoint == null)
        {
            spawnPoint = transform;
        }
    }

    private ProductDefinition ChooseProduct()
    {
        if (products == null || products.Count == 0)
        {
            return createPlaceholderWhenPrefabMissing ? CreateImplicitProductDefinition() : null;
        }

        if (spawnMode == ConveyorSpawnMode.Sequential)
        {
            ProductDefinition product = products[sequentialIndex % products.Count];
            sequentialIndex++;
            return product;
        }

        float totalWeight = 0f;
        for (int i = 0; i < products.Count; i++)
        {
            totalWeight += Mathf.Max(0f, products[i].probabilityWeight);
        }

        if (totalWeight <= 0f)
        {
            return products[0];
        }

        float randomValue = UnityEngine.Random.Range(0f, totalWeight);
        for (int i = 0; i < products.Count; i++)
        {
            randomValue -= Mathf.Max(0f, products[i].probabilityWeight);
            if (randomValue <= 0f)
            {
                return products[i];
            }
        }

        return products[products.Count - 1];
    }

    private ProductDefinition CreateImplicitProductDefinition()
    {
        return new ProductDefinition
        {
            productId = "ConveyorItem_Placeholder",
            probabilityWeight = 1f,
            initialRotation = Vector3.zero,
            scale = Vector3.one
        };
    }

    private GameObject CreateItemObject(ProductDefinition product, Vector3 spawnPosition, Quaternion spawnRotation)
    {
        if (product.prefab != null)
        {
            return Instantiate(product.prefab, spawnPosition, spawnRotation);
        }

        if (!createPlaceholderWhenPrefabMissing)
        {
            return null;
        }

        GameObject placeholder = GameObject.CreatePrimitive(PrimitiveType.Cube);
        placeholder.transform.SetPositionAndRotation(spawnPosition, spawnRotation);
        placeholder.transform.localScale = placeholderSize;
        Renderer renderer = placeholder.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = GetPlaceholderColor(product.productId);
        }

        return placeholder;
    }

    private Color GetPlaceholderColor(string productId)
    {
        int hash = string.IsNullOrWhiteSpace(productId) ? 0 : productId.GetHashCode();
        float hue = Mathf.Abs(hash % 360) / 360f;
        return Color.HSVToRGB(hue, 0.55f, 0.9f);
    }

    private int LayerMaskToLayer(LayerMask layerMask)
    {
        int mask = layerMask.value;
        if (mask == 0)
        {
            return gameObject.layer;
        }

        for (int i = 0; i < 32; i++)
        {
            if ((mask & (1 << i)) != 0)
            {
                return i;
            }
        }

        return gameObject.layer;
    }

    private void SetLayerRecursively(GameObject target, int layer)
    {
        if (target == null || layer < 0)
        {
            return;
        }

        target.layer = layer;
        foreach (Transform child in target.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }
}
