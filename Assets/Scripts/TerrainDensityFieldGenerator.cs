using Unity.Mathematics;
using UnityEngine;

public class TerrainDensityFieldGenerator : MonoBehaviour
{
    [Header("Terrain height")]
    [Range(-100f, 100f)]
    [SerializeField] private float baseHeight;
    [Range(0f, 50f)]
    [SerializeField] private float amplitude = 2f;

    [Header("Noise")]
    [SerializeField] private TerrainNoiseConfig noise = TerrainNoiseConfig.Default;

    public event System.Action Changed;

    public void Apply(FieldData[] data, float3 sdfCenter)
    {
        float amp = amplitude;
        float bh = baseHeight;
        TerrainNoiseConfig cfg = noise;

        for (int i = 0; i < data.Length; i++)
        {
            FieldData fd = data[i];
            float3 pRel = fd.position - sdfCenter;
            float n = TerrainNoiseSampler.SampleHeight(pRel.xz, cfg);
            float h = amp * n + bh;
            fd.density = pRel.y - h;
            data[i] = fd;
        }
    }

    private void OnValidate()
    {
        var n = noise;
        n.octaves = math.clamp(n.octaves, 1, 16);
        n.lacunarity = math.max(1e-4f, n.lacunarity);
        n.persistence = math.clamp(n.persistence, 1e-4f, 1f);
        n.frequency = math.max(1e-6f, n.frequency);
        noise = n;
        Changed?.Invoke();
    }
}
