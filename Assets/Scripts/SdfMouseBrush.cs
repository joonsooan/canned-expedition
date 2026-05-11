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

    private void Update()
    {
        if (mainCamera == null || densityField == null)
            return;

        if (!TryGetWorldPos(out float3 worldPos))
            return;

        if (Input.GetMouseButton(0))
        {
            densityField.AddBrush(worldPos, brushRadius, brushStrength, BrushType.Add);
        }
        else if (Input.GetMouseButton(1))
        {
            densityField.AddBrush(worldPos, brushRadius, brushStrength, BrushType.Subtract);
        }
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

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(worldPos, brushRadius);
    }
}