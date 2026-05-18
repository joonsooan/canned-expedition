using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public class TerrainDensityFieldGenerator : MonoBehaviour
{
    [Header("Terrain height")]
    [Range(-20f, 20f)]
    [SerializeField] private float baseHeight;
    [Range(0f, 10f)]
    [SerializeField] private float amplitude = 2f;

    [Header("Noise")]
    [SerializeField] private TerrainNoiseConfig noise = TerrainNoiseConfig.Default;

    public event System.Action Changed;

    [BurstCompile]
    private struct SampleHeightJob : IJobParallelFor
    {
        public NativeArray<float> densities;
        public float3 origin;
        public float3 sdfCenter;
        public int resolution;
        public float spacing;
        public float amplitude;
        public float baseHeight;
        public TerrainNoiseConfig config;

        public void Execute(int i)
        {
            int z = i / (resolution * resolution);
            int rem = i % (resolution * resolution);
            int y = rem / resolution;
            int x = rem % resolution;
            float3 centerCell = new float3(resolution / 2f, resolution / 2f, resolution / 2f);
            float3 pos = (new float3(x, y, z) - centerCell) * spacing + origin;

            float3 pRel = pos - sdfCenter;
            float n = TerrainNoiseSampler.SampleHeight(pRel.xz, config);
            densities[i] = pRel.y - amplitude * n - baseHeight;
        }
    }

    public void Apply(NativeArray<float> densities, float3 origin, int resolution, float spacing)
    {
        new SampleHeightJob
        {
            densities = densities,
            origin = origin,
            sdfCenter = transform.position,
            resolution = resolution,
            spacing = spacing,
            amplitude = amplitude,
            baseHeight = baseHeight,
            config = noise
        }.Schedule(densities.Length, 64).Complete();
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
