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

    private bool hasLastBrush;
    private float3 lastBrushPosition;
    private BrushType lastBrushType;

    private void Update()
    {
        if (mainCamera == null || densityField == null)
            return;

        bool addPressed = Input.GetMouseButton(0);
        bool subtractPressed = Input.GetMouseButton(1);
        if (!addPressed && !subtractPressed)
        {
            hasLastBrush = false;
            return;
        }

        if (!TryGetWorldPos(out float3 worldPos))
            return;

        BrushType brushType = addPressed ? BrushType.Add : BrushType.Subtract;
        if (!ShouldPlaceBrush(worldPos, brushType))
            return;

        densityField.AddBrush(worldPos, brushRadius, brushStrength, brushType);
        lastBrushPosition = worldPos;
        lastBrushType = brushType;
        hasLastBrush = true;
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