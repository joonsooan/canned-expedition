using Unity.Mathematics;
using UnityEngine;

public class TerrainDensityFieldGenerator : MonoBehaviour
{
    [SerializeField] private float baseHeight;
    [SerializeField] private float amplitude = 2f;
    [SerializeField] private float frequency = 0.15f;

    public void Apply(FieldData[] data, float3 sdfCenter)
    {
        float freq = frequency;
        float amp = amplitude;
        float bh = baseHeight;

        for (int i = 0; i < data.Length; i++)
        {
            FieldData fd = data[i];
            float3 p = fd.position - sdfCenter;
            float h = amp * (math.sin(p.x * freq) * 0.5f + math.cos(p.z * freq) * 0.5f) + bh;
            fd.density = p.y - h;
            data[i] = fd;
        }
    }
}
