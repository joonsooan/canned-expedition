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
    private struct SampleHeightJob2D : IJobParallelFor
    {
        [WriteOnly] [NativeDisableParallelForRestriction] public NativeArray<float> densities;
        public float3 origin;
        public float3 sdfCenter;
        public int resolution;
        public float spacing;
        public float amplitude;
        public float baseHeight;
        public TerrainNoiseConfig config;

        public void Execute(int index2D)
        {
            int z = index2D / resolution;
            int x = index2D % resolution;
            
            float centerCell = resolution / 2f;
            
            float posX = (x - centerCell) * spacing + origin.x;
            float posZ = (z - centerCell) * spacing + origin.z;
            
            float2 pRelXZ = new float2(posX - sdfCenter.x, posZ - sdfCenter.z);
            float n = TerrainNoiseSampler.SampleHeight(pRelXZ, config);
            float baseDensityVal = -amplitude * n - baseHeight;

            for (int y = 0; y < resolution; y++)
            {
                float posY = (y - centerCell) * spacing + origin.y;
                float pRelY = posY - sdfCenter.y;
                int index3D = x + (resolution * y) + (resolution * resolution * z);
                densities[index3D] = pRelY + baseDensityVal;
            }
        }
    }

    public void Apply(NativeArray<float> densities, float3 origin, int resolution, float spacing)
    {
        new SampleHeightJob2D
        {
            densities = densities,
            origin = origin,
            sdfCenter = transform.position,
            resolution = resolution,
            spacing = spacing,
            amplitude = amplitude,
            baseHeight = baseHeight,
            config = noise
        }.Schedule(resolution * resolution, 16).Complete();
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
