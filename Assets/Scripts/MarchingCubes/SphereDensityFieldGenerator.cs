using Unity.Mathematics;
using UnityEngine;

public class SphereDensityFieldGenerator : MonoBehaviour
{
    [Range(0.01f, 100f)]
    [SerializeField] private float sphereRadius = 5f;

    public event System.Action Changed;

    public float SphereRadius => sphereRadius;

    public void Apply(Unity.Collections.NativeArray<float> densities, float3 origin, int resolution, float spacing)
    {
        float r = sphereRadius;
        float3 sdfCenter = transform.position; // Actually the center of the sphere
        for (int i = 0; i < densities.Length; i++)
        {
            float3 pos = DensityField.GetPosition(i, resolution, spacing, origin);
            densities[i] = math.length(pos - sdfCenter) - r;
        }
    }

    private void OnValidate()
    {
        Changed?.Invoke();
    }
}
