using Unity.Mathematics;
using UnityEngine;

public class SdfMouseBrush : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private DensityField densityField;
    [SerializeField] private LayerMask raycastMask = ~0;

    [Header("Brush Settings")]
    [SerializeField] private float brushRadius = 0.5f;
    [SerializeField] private float brushStrength = 0.5f;
    [SerializeField] private float scrollSensitivity = 0.1f;
    [SerializeField] private float minRadius = 0.1f;
    [SerializeField] private float maxRadius = 10f;

    [Header("Brush Object")]
    [SerializeField] private GameObject brushObject;
    [SerializeField] private Color addColor = Color.green;
    [SerializeField] private Color subtractColor = Color.red;

    private bool hasLastBrush;
    private float3 lastBrushPosition;
    private BrushType lastBrushType;
    private Renderer brushRenderer;
    private Material brushMaterial;

    private void Start()
    {
        if (brushObject == null) return;
        brushRenderer = brushObject.GetComponentInChildren<Renderer>();
        if (brushRenderer != null)
            brushMaterial = brushRenderer.material;
    }

    private void OnDestroy()
    {
        if (brushMaterial != null)
            Destroy(brushMaterial);
    }

    private void Update()
    {
        if (mainCamera == null || densityField == null)
        {
            SetIndicatorActive(false);
            return;
        }

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0f)
        {
            brushRadius = Mathf.Clamp(brushRadius + scroll * scrollSensitivity * brushRadius, minRadius, maxRadius);
            hasLastBrush = false;
        }

        bool hasHit = TryGetWorldPos(out float3 worldPos);

        bool addPressed = Input.GetMouseButton(0);
        bool subtractPressed = Input.GetMouseButton(1);
        BrushType brushType = subtractPressed ? BrushType.Subtract : BrushType.Add;

        UpdateIndicator(hasHit, worldPos, brushType);

        if (!hasHit || (!addPressed && !subtractPressed))
        {
            hasLastBrush = false;
            return;
        }

        if (!ShouldPlaceBrush(worldPos, brushType))
            return;

        densityField.AddBrush(worldPos, brushRadius, brushStrength, brushType);
        lastBrushPosition = worldPos;
        lastBrushType = brushType;
        hasLastBrush = true;
    }

    private void UpdateIndicator(bool visible, float3 worldPos, BrushType brushType)
    {
        SetIndicatorActive(visible);
        if (!visible || brushObject == null) return;

        brushObject.transform.position = worldPos;
        brushObject.transform.localScale = Vector3.one * (brushRadius * 2f);

        if (brushMaterial != null)
            brushMaterial.color = brushType == BrushType.Add ? addColor : subtractColor;
    }

    private void SetIndicatorActive(bool active)
    {
        if (brushObject != null && brushObject.activeSelf != active)
            brushObject.SetActive(active);
    }

    private bool ShouldPlaceBrush(float3 worldPos, BrushType brushType)
    {
        if (!hasLastBrush || lastBrushType != brushType)
            return true;

        float spacing = math.max(brushRadius * 0.1f, 0.001f);
        return math.distancesq(worldPos, lastBrushPosition) >= spacing * spacing;
    }

    private bool TryGetWorldPos(out float3 worldPos)
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        worldPos = float3.zero;

        MeshCollider targetCollider = densityField.SurfaceCollider;
        if (targetCollider == null)
            return false;

        if (!Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, raycastMask, QueryTriggerInteraction.Ignore))
            return false;

        if (hit.collider != targetCollider)
            return false;

        worldPos = (float3)hit.point;
        return true;
    }

    private void OnDrawGizmos()
    {
        if (mainCamera == null || densityField == null)
            return;
        if (!TryGetWorldPos(out float3 worldPos))
            return;

        if (densityField.TryGetChunkBounds(worldPos, out Bounds chunkBounds))
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(chunkBounds.center, chunkBounds.size);
        }

        Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
        Gizmos.DrawSphere(worldPos, brushRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(worldPos, brushRadius);
    }
}
