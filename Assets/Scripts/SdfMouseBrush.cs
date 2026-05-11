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
        if (Input.GetMouseButton(0))
        {
            densityField.AddBrush(GetWorldPos(), brushRadius, brushStrength, BrushType.Add);
        }
        else if (Input.GetMouseButton(1))
        {
            densityField.AddBrush(GetWorldPos(), brushRadius, brushStrength, BrushType.Subtract);
        }
    }

    private float3 GetWorldPos()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        float3 worldPos;
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            worldPos = (float3)hit.point;
        }
        else
        {
            worldPos = float3.zero;
        }
        return worldPos;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(GetWorldPos(), brushRadius);
    }
}