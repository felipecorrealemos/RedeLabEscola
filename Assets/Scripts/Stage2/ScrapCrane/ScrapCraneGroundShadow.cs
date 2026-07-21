using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ScrapCraneGroundShadow : MonoBehaviour
{
    private static Mesh generatedQuadMesh;
    private static Texture2D generatedShadowTexture;

    [Header("References")]
    [SerializeField] private Transform trackedTarget;
    [SerializeField] private Transform floorSpace;

    [Header("Placement")]
    [SerializeField] private bool useRaycastToFloor;
    [SerializeField] private LayerMask floorMask = ~0;
    [SerializeField] private float raycastStartHeight = 30f;
    [SerializeField] private float raycastDistance = 80f;
    [SerializeField] private float floorLocalY = -22.62f;
    [SerializeField] private float surfaceOffset = 0.035f;

    [Header("Visual")]
    [SerializeField] private Vector2 shadowSize = new Vector2(2.2f, 2.2f);
    [SerializeField, Range(0f, 1f)] private float maxAlpha = 0.65f;
    [SerializeField, Min(0.01f)] private float maxVisibleHeight = 14f;

    private MeshRenderer meshRenderer;
    private MaterialPropertyBlock propertyBlock;
    private Color baseColor = Color.white;

    private void Reset()
    {
        ConfigureRenderer();
    }

    private void Awake()
    {
        ConfigureRenderer();
    }

    private void LateUpdate()
    {
        UpdateShadow();
    }

    public void AssignReferences(Transform target, Transform space)
    {
        trackedTarget = target;
        floorSpace = space;
    }

    public void ConfigureDefaults(Material material, Vector2 size, float localFloorY)
    {
        ConfigureRenderer();
        if (meshRenderer != null && material != null)
        {
            meshRenderer.sharedMaterial = material;
            if (material.HasProperty("_Color"))
            {
                baseColor = material.color;
            }
        }

        shadowSize = size;
        floorLocalY = localFloorY;
        ApplyScale();
    }

    private void ConfigureRenderer()
    {
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter != null)
        {
            Mesh quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            meshFilter.sharedMesh = quad != null ? quad : GetGeneratedQuadMesh();
        }

        if (meshRenderer == null)
        {
            meshRenderer = GetComponent<MeshRenderer>();
        }

        if (meshRenderer != null)
        {
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            if (meshRenderer.sharedMaterial == null)
            {
                Shader shader = Shader.Find("Unlit/Transparent");
                if (shader != null)
                {
                    Material material = new Material(shader)
                    {
                        color = new Color(0f, 0f, 0f, 0.35f)
                    };
                    material.mainTexture = GetGeneratedShadowTexture();
                    meshRenderer.sharedMaterial = material;
                    baseColor = material.color;
                }
            }
            else if (meshRenderer.sharedMaterial.HasProperty("_Color"))
            {
                baseColor = meshRenderer.sharedMaterial.color;
            }
        }

        ApplyScale();
    }

    private void UpdateShadow()
    {
        if (trackedTarget == null)
        {
            return;
        }

        Vector3 targetPosition = trackedTarget.position;
        Vector3 shadowPosition;
        float heightAboveFloor;

        if (useRaycastToFloor && Physics.Raycast(targetPosition + Vector3.up * raycastStartHeight, Vector3.down, out RaycastHit hit, raycastDistance, floorMask, QueryTriggerInteraction.Ignore))
        {
            shadowPosition = hit.point + hit.normal * surfaceOffset;
            heightAboveFloor = Mathf.Max(0f, targetPosition.y - hit.point.y);
            transform.rotation = Quaternion.FromToRotation(Vector3.forward, hit.normal);
        }
        else
        {
            Transform space = floorSpace != null ? floorSpace : transform.parent;
            if (space != null)
            {
                Vector3 localPosition = space.InverseTransformPoint(targetPosition);
                localPosition.y = floorLocalY + surfaceOffset;
                shadowPosition = space.TransformPoint(localPosition);
                heightAboveFloor = Mathf.Max(0f, space.InverseTransformPoint(targetPosition).y - floorLocalY);
            }
            else
            {
                shadowPosition = new Vector3(targetPosition.x, surfaceOffset, targetPosition.z);
                heightAboveFloor = Mathf.Max(0f, targetPosition.y);
            }

            transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }

        transform.position = shadowPosition;
        ApplyAlpha(heightAboveFloor);
    }

    private void ApplyScale()
    {
        transform.localScale = new Vector3(Mathf.Max(0.01f, shadowSize.x), Mathf.Max(0.01f, shadowSize.y), 1f);
    }

    private void ApplyAlpha(float heightAboveFloor)
    {
        if (meshRenderer == null)
        {
            return;
        }

        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        float heightFactor = 1f - Mathf.Clamp01(heightAboveFloor / Mathf.Max(0.01f, maxVisibleHeight));
        Color color = baseColor;
        color.a *= maxAlpha * Mathf.Lerp(0.35f, 1f, heightFactor);
        meshRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor("_Color", color);
        meshRenderer.SetPropertyBlock(propertyBlock);
    }

    private static Mesh GetGeneratedQuadMesh()
    {
        if (generatedQuadMesh != null)
        {
            return generatedQuadMesh;
        }

        generatedQuadMesh = new Mesh
        {
            name = "ScrapCraneShadowQuad"
        };
        generatedQuadMesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3(0.5f, -0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f)
        };
        generatedQuadMesh.uv = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f)
        };
        generatedQuadMesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        generatedQuadMesh.RecalculateBounds();
        return generatedQuadMesh;
    }

    private static Texture2D GetGeneratedShadowTexture()
    {
        if (generatedShadowTexture != null)
        {
            return generatedShadowTexture;
        }

        const int size = 64;
        generatedShadowTexture = new Texture2D(size, size, TextureFormat.ARGB32, false)
        {
            name = "ScrapCraneGeneratedShadow",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Color[] pixels = new Color[size * size];
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.48f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center) / radius;
                float alpha = Mathf.Clamp01(1f - distance);
                alpha = alpha * alpha * (3f - 2f * alpha);
                pixels[y * size + x] = new Color(0f, 0f, 0f, alpha);
            }
        }

        generatedShadowTexture.SetPixels(pixels);
        generatedShadowTexture.Apply(false, true);
        return generatedShadowTexture;
    }
}
