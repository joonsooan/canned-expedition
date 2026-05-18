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
        public NativeArray<FieldData> data;
        public float3 sdfCenter;
        public float amplitude;
        public float baseHeight;
        public TerrainNoiseConfig config;

        public void Execute(int i)
        {
            FieldData fd = data[i];
            float3 pRel = fd.position - sdfCenter;
            float n = TerrainNoiseSampler.SampleHeight(pRel.xz, config);
            fd.density = pRel.y - amplitude * n - baseHeight;
            data[i] = fd;
        }
    }

    public void Apply(FieldData[] data, float3 sdfCenter)
    {
        var native = new NativeArray<FieldData>(data, Allocator.TempJob);

        new SampleHeightJob
        {
            data = native,
            sdfCenter = sdfCenter,
            amplitude = amplitude,
            baseHeight = baseHeight,
            config = noise
        }.Schedule(data.Length, 64).Complete();

        native.CopyTo(data);
        native.Dispose();
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
