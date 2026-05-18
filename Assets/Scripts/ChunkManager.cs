using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public class ChunkManager
{
    private const int ChunkSize = 8;
    private const float IsoValue = 0f;
    private const int MaxNewChunksPerFrame = 4;
    private int loadRadius = 3;

    private struct ChunkCoord
    {
        public readonly int x;
        public readonly int y;
        public readonly int z;

        public ChunkCoord(int x, int y, int z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is ChunkCoord other))
                return false;

            return x == other.x && y == other.y && z == other.z;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + x;
                hash = hash * 31 + y;
                hash = hash * 31 + z;
                return hash;
            }
        }
    }

    private class ChunkData
    {
        public ChunkCoord coord;
        public int3 minCell;
        public int3 maxCell;
        public readonly List<Vector3> vertices = new List<Vector3>(1024);
        public readonly List<int> indices = new List<int>(1536);
        public bool dirty;
    }

    [BurstCompile]
    private struct ApplyBrushesJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<int> sampleIndices;
        [ReadOnly] public NativeArray<BrushData> brushes;
        [NativeDisableParallelForRestriction] public NativeArray<FieldData> fieldData;
        public int startBrush;
        public int endBrush;

        public void Execute(int i)
        {
            int idx = sampleIndices[i];
            FieldData fd = fieldData[idx];
            float density = fd.density;
            for (int b = startBrush; b < endBrush; b++)
                density = SdfBrush.Apply(density, fd.position, brushes[b]);
            fd.density = density;
            fieldData[idx] = fd;
        }
    }

    private readonly Dictionary<ChunkCoord, ChunkData> activeChunks = new Dictionary<ChunkCoord, ChunkData>();
    private readonly List<Vector3> combinedVertices = new List<Vector3>(4096);
    private readonly List<int> combinedIndices = new List<int>(6144);
    private readonly List<ChunkCoord> coordsToRemove = new List<ChunkCoord>();
    private readonly Dictionary<ChunkCoord, int> appliedBrushCounts = new Dictionary<ChunkCoord, int>();

    private FieldData[] fieldData;
    private int resolution;
    private float spacing;
    private float3 worldOrigin;
    private Mesh isoSurfaceMesh;
    private Transform chunkLoadTarget;
    private int chunkCountPerAxis = 1;
    private int defaultAppliedBrushCount;
    private bool hasTargetChunk;
    private ChunkCoord lastTargetChunk;
    private bool hasPendingChunkLoads;

    public void Initialize(FieldData[] data, int fieldResolution, float fieldSpacing, float3 origin, Mesh mesh)
    {
        fieldData = data;
        resolution = fieldResolution;
        spacing = fieldSpacing;
        worldOrigin = origin;
        isoSurfaceMesh = mesh;
        ResetChunks();
    }

    public void SetLoadTarget(Transform target)
    {
        chunkLoadTarget = target;
        hasTargetChunk = false;
    }

    public void SetLoadRadius(int radius)
    {
        int clamped = math.max(1, radius);
        if (loadRadius == clamped)
            return;
        loadRadius = clamped;
        hasTargetChunk = false;
    }

    public void RebuildForFullField(List<BrushData> brushes)
    {
        appliedBrushCounts.Clear();
        defaultAppliedBrushCount = brushes.Count;
        ResetChunks();
        UpdateActiveChunks(true, brushes, null);
    }

    public bool UpdateActiveChunks(bool force, List<BrushData> brushes, List<BrushData> pendingBrushes)
    {
        if (fieldData == null || resolution < 2 || isoSurfaceMesh == null)
            return false;

        ChunkCoord targetChunk = GetTargetChunkCoord();
        if (!force && !hasPendingChunkLoads && hasTargetChunk && lastTargetChunk.Equals(targetChunk))
            return false;

        if (!force && pendingBrushes != null && pendingBrushes.Count > 0 && hasTargetChunk)
            RefreshPendingBrushes(brushes);

        hasTargetChunk = true;
        lastTargetChunk = targetChunk;

        var desired = new HashSet<ChunkCoord>();
        int minX = math.max(0, targetChunk.x - loadRadius);
        int maxX = math.min(chunkCountPerAxis - 1, targetChunk.x + loadRadius);
        int minY = math.max(0, targetChunk.y - loadRadius);
        int maxY = math.min(chunkCountPerAxis - 1, targetChunk.y + loadRadius);
        int minZ = math.max(0, targetChunk.z - loadRadius);
        int maxZ = math.min(chunkCountPerAxis - 1, targetChunk.z + loadRadius);

        for (int z = minZ; z <= maxZ; z++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    desired.Add(new ChunkCoord(x, y, z));
                }
            }
        }

        coordsToRemove.Clear();
        foreach (var pair in activeChunks)
        {
            if (!desired.Contains(pair.Key))
                coordsToRemove.Add(pair.Key);
        }

        bool changed = force || coordsToRemove.Count > 0;
        for (int i = 0; i < coordsToRemove.Count; i++)
        {
            activeChunks.Remove(coordsToRemove[i]);
        }

        hasPendingChunkLoads = false;
        int newChunksThisFrame = 0;
        foreach (var coord in desired)
        {
            if (activeChunks.ContainsKey(coord))
                continue;

            if (newChunksThisFrame >= MaxNewChunksPerFrame)
            {
                hasPendingChunkLoads = true;
                changed = true;
                continue;
            }

            activeChunks.Add(coord, CreateChunk(coord));
            newChunksThisFrame++;
            changed = true;
        }

        if (force)
        {
            foreach (var chunk in activeChunks.Values)
                chunk.dirty = true;
        }

        if (!changed)
            return false;

        ApplyMissingBrushesToDirtyChunks(brushes);
        RebuildDirtyChunks();
        RebuildCombinedMesh();
        return true;
    }

    public bool MarkBrushChunksDirty(BrushData brush)
    {
        if (!TryGetBrushCellBounds(brush, out int3 minCell, out int3 maxCell))
            return false;

        ChunkCoord minChunk = CellToChunkCoord(minCell);
        ChunkCoord maxChunk = CellToChunkCoord(maxCell);
        bool markedAny = false;

        for (int z = minChunk.z; z <= maxChunk.z; z++)
        {
            for (int y = minChunk.y; y <= maxChunk.y; y++)
            {
                for (int x = minChunk.x; x <= maxChunk.x; x++)
                {
                    var coord = new ChunkCoord(x, y, z);
                    if (!activeChunks.TryGetValue(coord, out ChunkData chunk))
                        continue;

                    chunk.dirty = true;
                    markedAny = true;
                }
            }
        }

        return markedAny;
    }

    public void RefreshPendingBrushes(List<BrushData> brushes)
    {
        ApplyMissingBrushesToDirtyChunks(brushes);
        RebuildDirtyChunks();
        RebuildCombinedMesh();
    }

    public bool TryGetChunkBounds(float3 worldPosition, out Bounds bounds)
    {
        bounds = default;
        if (fieldData == null || resolution < 2)
            return false;

        int maxCell = resolution - 2;
        int3 cell = WorldToCellCoord(worldPosition);
        if (cell.x < 0 || cell.y < 0 || cell.z < 0 ||
            cell.x > maxCell || cell.y > maxCell || cell.z > maxCell)
            return false;

        ChunkCoord coord = CellToChunkCoord(cell);
        ChunkData chunk = activeChunks.TryGetValue(coord, out ChunkData activeChunk)
            ? activeChunk
            : CreateChunk(coord);

        float3 centerCell = new float3(resolution / 2f, resolution / 2f, resolution / 2f);
        float3 min = (chunk.minCell - centerCell) * spacing + worldOrigin;
        float3 max = (chunk.maxCell + new int3(1, 1, 1) - centerCell) * spacing + worldOrigin;
        bounds = new Bounds((Vector3)((min + max) * 0.5f), (Vector3)(max - min));
        return true;
    }

    private void ResetChunks()
    {
        activeChunks.Clear();
        coordsToRemove.Clear();
        int cellCount = math.max(1, resolution - 1);
        chunkCountPerAxis = math.max(1, (cellCount + ChunkSize - 1) / ChunkSize);
        hasTargetChunk = false;
        hasPendingChunkLoads = false;
    }

    private ChunkData CreateChunk(ChunkCoord coord)
    {
        int3 minCell = new int3(coord.x, coord.y, coord.z) * ChunkSize;
        int maxCell = resolution - 1;
        int3 max = new int3(maxCell, maxCell, maxCell);
        int3 maxCellExclusive = math.min(minCell + ChunkSize, max);

        return new ChunkData
        {
            coord = coord,
            minCell = minCell,
            maxCell = maxCellExclusive,
            dirty = true
        };
    }

    private Transform GetChunkLoadTarget()
    {
        if (chunkLoadTarget != null)
            return chunkLoadTarget;

        Camera mainCamera = Camera.main;
        return mainCamera != null ? mainCamera.transform : null;
    }

    private ChunkCoord GetTargetChunkCoord()
    {
        Transform target = GetChunkLoadTarget();
        float3 targetPosition = target != null ? (float3)target.position : worldOrigin;
        int3 cell = WorldToCellCoord(targetPosition);
        int maxCell = resolution - 2;
        cell = math.clamp(cell, int3.zero, new int3(maxCell, maxCell, maxCell));
        return CellToChunkCoord(cell);
    }

    private int3 WorldToCellCoord(float3 worldPosition)
    {
        float3 centerCell = new float3(resolution / 2f, resolution / 2f, resolution / 2f);
        float3 grid = (worldPosition - worldOrigin) / spacing + centerCell;
        return (int3)math.floor(grid);
    }

    private ChunkCoord CellToChunkCoord(int3 cell)
    {
        int maxCell = resolution - 2;
        cell = math.clamp(cell, int3.zero, new int3(maxCell, maxCell, maxCell));
        int3 chunk = cell / ChunkSize;
        return new ChunkCoord(chunk.x, chunk.y, chunk.z);
    }

    private bool TryGetBrushCellBounds(BrushData brush, out int3 minCell, out int3 maxCell)
    {
        float influence = brush.radius + math.abs(brush.strength) + spacing;
        float3 extent = new float3(influence);
        int3 rawMin = WorldToCellCoord(brush.center - extent);
        int3 rawMax = WorldToCellCoord(brush.center + extent);
        int max = resolution - 2;

        if (rawMax.x < 0 || rawMax.y < 0 || rawMax.z < 0 ||
            rawMin.x > max || rawMin.y > max || rawMin.z > max)
        {
            minCell = int3.zero;
            maxCell = int3.zero;
            return false;
        }

        minCell = math.clamp(rawMin, int3.zero, new int3(max, max, max));
        maxCell = math.clamp(rawMax, int3.zero, new int3(max, max, max));
        return true;
    }

    private void ApplyMissingBrushesToDirtyChunks(List<BrushData> brushes)
    {
        if (brushes == null || brushes.Count == 0) return;

        bool anyDirty = false;
        foreach (var chunk in activeChunks.Values)
        {
            if (!chunk.dirty) continue;
            if (GetAppliedBrushCount(chunk.coord) < brushes.Count) { anyDirty = true; break; }
        }
        if (!anyDirty) return;

        var nativeBrushes = ListToNativeArray(brushes, Allocator.TempJob);
        var nativeField = new NativeArray<FieldData>(fieldData, Allocator.TempJob);

        foreach (var chunk in activeChunks.Values)
        {
            if (!chunk.dirty) continue;
            int appliedCount = GetAppliedBrushCount(chunk.coord);
            if (appliedCount >= brushes.Count) continue;
            RunBrushJobForChunk(chunk, nativeField, nativeBrushes, appliedCount, brushes.Count);
            appliedBrushCounts[chunk.coord] = brushes.Count;
        }

        nativeField.CopyTo(fieldData);
        nativeField.Dispose();
        nativeBrushes.Dispose();
    }

    private void RunBrushJobForChunk(ChunkData chunk, NativeArray<FieldData> nativeField, NativeArray<BrushData> nativeBrushes, int startBrush, int endBrush)
    {
        var indices = BuildChunkSampleIndices(chunk, Allocator.TempJob);
        new ApplyBrushesJob
        {
            sampleIndices = indices,
            brushes = nativeBrushes,
            fieldData = nativeField,
            startBrush = startBrush,
            endBrush = endBrush
        }.Schedule(indices.Length, 64).Complete();
        indices.Dispose();
    }

    private NativeArray<int> BuildChunkSampleIndices(ChunkData chunk, Allocator allocator)
    {
        int sampleMax = resolution - 1;
        int3 min = math.clamp(chunk.minCell, int3.zero, new int3(sampleMax, sampleMax, sampleMax));
        int3 max = math.clamp(chunk.maxCell, int3.zero, new int3(sampleMax, sampleMax, sampleMax));
        int3 size = max - min + new int3(1, 1, 1);
        var indices = new NativeArray<int>(size.x * size.y * size.z, allocator, NativeArrayOptions.UninitializedMemory);
        int i = 0;
        for (int z = min.z; z <= max.z; z++)
            for (int y = min.y; y <= max.y; y++)
                for (int x = min.x; x <= max.x; x++)
                    indices[i++] = GetIndex(x, y, z);
        return indices;
    }

    private static NativeArray<T> ListToNativeArray<T>(List<T> list, Allocator allocator) where T : struct
    {
        var arr = new NativeArray<T>(list.Count, allocator, NativeArrayOptions.UninitializedMemory);
        for (int i = 0; i < list.Count; i++) arr[i] = list[i];
        return arr;
    }

    private int GetAppliedBrushCount(ChunkCoord coord)
    {
        return appliedBrushCounts.TryGetValue(coord, out int count) ? count : defaultAppliedBrushCount;
    }

    private void RebuildDirtyChunks()
    {
        foreach (var chunk in activeChunks.Values)
        {
            if (!chunk.dirty)
                continue;

            chunk.vertices.Clear();
            chunk.indices.Clear();
            MarchingCubes.BuildMeshRange(fieldData, resolution, IsoValue, chunk.minCell, chunk.maxCell, chunk.vertices, chunk.indices);
            chunk.dirty = false;
        }
    }

    private void RebuildCombinedMesh()
    {
        combinedVertices.Clear();
        combinedIndices.Clear();

        foreach (var chunk in activeChunks.Values)
        {
            int offset = combinedVertices.Count;
            combinedVertices.AddRange(chunk.vertices);
            for (int i = 0; i < chunk.indices.Count; i++)
            {
                combinedIndices.Add(chunk.indices[i] + offset);
            }
        }

        MarchingCubes.ApplyMesh(isoSurfaceMesh, combinedVertices, combinedIndices);
    }

    private int GetIndex(int x, int y, int z)
    {
        return x + resolution * y + resolution * resolution * z;
    }
}
