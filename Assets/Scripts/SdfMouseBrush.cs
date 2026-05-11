using Unity.Mathematics;
using UnityEngine;

public class SdfMouseBrush : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private DensityField densityField;

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
        return densityField.TryRayCastToField(ray, out worldPos);
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