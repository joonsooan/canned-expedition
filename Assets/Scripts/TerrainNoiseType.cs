using System;
using Unity.Mathematics;
using UnityEngine;

public enum TerrainNoiseType
{
    Simplex2D,
    ClassicGradient2D,
    FbmSimplex2D,
    FbmClassic2D,
    RidgedSimplex2D,
    Cellular2D
}

[Serializable]
public struct TerrainNoiseConfig
{
    public TerrainNoiseType noiseType;

    [Tooltip("XZ 좌표 스케일 (값이 클수록 더 잘게 반복)")]
    [Range(0.001f, 2f)]
    public float frequency;

    [Tooltip("FBM / Ridged 옥타브 수 (단일 레이어 모드에서는 1로 취급)")]
    [Range(1, 16)]
    public int octaves;

    [Tooltip("옥타브마다 주파수 배율")]
    [Range(1f, 8f)]
    public float lacunarity;

    [Tooltip("옥타브마다 진폭 배율")]
    [Range(0.01f, 1f)]
    public float persistence;

    [Range(0, 2147483647)]
    public uint seed;

    public float2 offset;

    public static TerrainNoiseConfig Default => new TerrainNoiseConfig
    {
        noiseType = TerrainNoiseType.FbmSimplex2D,
        frequency = 0.08f,
        octaves = 5,
        lacunarity = 2f,
        persistence = 0.5f,
        seed = 1u,
        offset = float2.zero
    };
}
