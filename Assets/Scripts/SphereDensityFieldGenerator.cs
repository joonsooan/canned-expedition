using Unity.Mathematics;
using UnityEngine;

public class SphereDensityFieldGenerator : MonoBehaviour
{
    [SerializeField] private float sphereRadius = 5f;

    public float SphereRadius => sphereRadius;

    public void Apply(FieldData[] data, float3 sdfCenter)
    {
        float r = sphereRadius;
        for (int i = 0; i < data.Length; i++)
        {
            FieldData fd = data[i];
            fd.density = math.length(fd.position - sdfCenter) - r;
            data[i] = fd;
        }
    }
}
