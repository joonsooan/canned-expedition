using Unity.Mathematics;

public static class TerrainNoiseSampler
{
    private const int MinOctaves = 1;
    private const int MaxOctaves = 16;

    public static float SampleHeight(float2 xzLocal, TerrainNoiseConfig c)
    {
        float2 p = xzLocal * c.frequency + c.offset + SeedOffset(c.seed);

        switch (c.noiseType)
        {
            case TerrainNoiseType.Simplex2D:
                return noise.snoise(p);

            case TerrainNoiseType.ClassicGradient2D:
                return noise.cnoise(p);

            case TerrainNoiseType.FbmSimplex2D:
                return Fbm(p, c, false);

            case TerrainNoiseType.FbmClassic2D:
                return Fbm(p, c, true);

            case TerrainNoiseType.RidgedSimplex2D:
                return Ridged(p, c);

            case TerrainNoiseType.Cellular2D:
                return CellularHeight(p);

            default:
                return noise.snoise(p);
        }
    }

    private static float2 SeedOffset(uint seed)
    {
        uint x = seed * 1597334677u;
        uint y = x ^ 3812015801u;
        return new float2(
            ((x & 0xFFFFu) / 65535f) * 400f - 200f,
            ((y & 0xFFFFu) / 65535f) * 400f - 200f);
    }

    private static int ClampOctaves(int octaves)
    {
        return math.clamp(octaves, MinOctaves, MaxOctaves);
    }

    private static float Fbm(float2 p, TerrainNoiseConfig c, bool classic)
    {
        int o = ClampOctaves(c.octaves);
        float sum = 0f;
        float amp = 1f;
        float norm = 0f;
        float f = 1f;

        for (int i = 0; i < o; i++)
        {
            float n = classic ? noise.cnoise(p * f) : noise.snoise(p * f);
            sum += n * amp;
            norm += amp;
            amp *= c.persistence;
            f *= c.lacunarity;
        }

        return norm > 0f ? sum / norm : 0f;
    }

    private static float Ridged(float2 p, TerrainNoiseConfig c)
    {
        int o = ClampOctaves(c.octaves);
        float sum = 0f;
        float amp = 1f;
        float norm = 0f;
        float f = 1f;

        for (int i = 0; i < o; i++)
        {
            float n = noise.snoise(p * f);
            n = 1f - math.abs(n);
            n *= n;
            sum += n * amp;
            norm += amp;
            amp *= c.persistence;
            f *= c.lacunarity;
        }

        if (norm <= 0f)
            return 0f;

        float t = sum / norm;
        return t * 2f - 1f;
    }

    private static float CellularHeight(float2 p)
    {
        float2 cell = noise.cellular(p);
        float d = cell.y - cell.x;
        return math.clamp(d * 6f, -1f, 1f);
    }
}
