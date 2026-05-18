using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

public struct Triangle
{
    public float3 v0;
    public float3 v1;
    public float3 v2;
}

public static class MarchingCubes
{
    private const float Epsilon = 1e-6f;

    [BurstCompile]
    private struct MarchingCubesJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float> densities;
        [ReadOnly] public NativeArray<int> edgeTable;
        [ReadOnly] public NativeArray<int> triangleTable;
        
        [NativeDisableParallelForRestriction]
        public NativeStream.Writer writer;

        public int resolution;
        public float spacing;
        public float3 origin;
        public float isovalue;
        public int3 minCell;
        public int numCellsX;
        public int numCellsY;

        public void Execute(int i)
        {
            int lx = i % numCellsX;
            int ly = (i / numCellsX) % numCellsY;
            int lz = i / (numCellsX * numCellsY);

            int x = minCell.x + lx;
            int y = minCell.y + ly;
            int z = minCell.z + lz;

            int c0 = Index(resolution, x, y, z);
            int c1 = Index(resolution, x + 1, y, z);
            int c2 = Index(resolution, x + 1, y + 1, z);
            int c3 = Index(resolution, x, y + 1, z);
            int c4 = Index(resolution, x, y, z + 1);
            int c5 = Index(resolution, x + 1, y, z + 1);
            int c6 = Index(resolution, x + 1, y + 1, z + 1);
            int c7 = Index(resolution, x, y + 1, z + 1);

            float d0 = densities[c0];
            float d1 = densities[c1];
            float d2 = densities[c2];
            float d3 = densities[c3];
            float d4 = densities[c4];
            float d5 = densities[c5];
            float d6 = densities[c6];
            float d7 = densities[c7];

            int cubeIndex = 0;
            if (d0 <= isovalue) cubeIndex |= 1;
            if (d1 <= isovalue) cubeIndex |= 2;
            if (d2 <= isovalue) cubeIndex |= 4;
            if (d3 <= isovalue) cubeIndex |= 8;
            if (d4 <= isovalue) cubeIndex |= 16;
            if (d5 <= isovalue) cubeIndex |= 32;
            if (d6 <= isovalue) cubeIndex |= 64;
            if (d7 <= isovalue) cubeIndex |= 128;

            int edgeMask = edgeTable[cubeIndex];
            if (edgeMask == 0) return;

            writer.BeginForEachIndex(i);

            float3 p0 = GetPositionInline(x, y, z, resolution, spacing, origin);
            float3 p1 = p0 + new float3(spacing, 0, 0);
            float3 p2 = p0 + new float3(spacing, spacing, 0);
            float3 p3 = p0 + new float3(0, spacing, 0);
            float3 p4 = p0 + new float3(0, 0, spacing);
            float3 p5 = p0 + new float3(spacing, 0, spacing);
            float3 p6 = p0 + new float3(spacing, spacing, spacing);
            float3 p7 = p0 + new float3(0, spacing, spacing);

            for (int t = 0; t < 16; t += 3)
            {
                int e0 = triangleTable[cubeIndex * 16 + t];
                if (e0 < 0) break;
                int e1 = triangleTable[cubeIndex * 16 + t + 1];
                int e2 = triangleTable[cubeIndex * 16 + t + 2];

                float3 v0 = InterpolateEdge(e0, isovalue, p0, p1, p2, p3, p4, p5, p6, p7, d0, d1, d2, d3, d4, d5, d6, d7);
                float3 v1 = InterpolateEdge(e1, isovalue, p0, p1, p2, p3, p4, p5, p6, p7, d0, d1, d2, d3, d4, d5, d6, d7);
                float3 v2 = InterpolateEdge(e2, isovalue, p0, p1, p2, p3, p4, p5, p6, p7, d0, d1, d2, d3, d4, d5, d6, d7);

                writer.Write(new Triangle { v0 = v0, v1 = v2, v2 = v1 });
            }

            writer.EndForEachIndex();
        }

        private float3 InterpolateEdge(int edge, float isovalue, 
            float3 p0, float3 p1, float3 p2, float3 p3, float3 p4, float3 p5, float3 p6, float3 p7,
            float d0, float d1, float d2, float d3, float d4, float d5, float d6, float d7)
        {
            float3 pa = p0, pb = p0;
            float da = 0, db = 0;
            switch(edge) {
                case 0: pa = p0; pb = p1; da = d0; db = d1; break;
                case 1: pa = p1; pb = p2; da = d1; db = d2; break;
                case 2: pa = p2; pb = p3; da = d2; db = d3; break;
                case 3: pa = p3; pb = p0; da = d3; db = d0; break;
                case 4: pa = p4; pb = p5; da = d4; db = d5; break;
                case 5: pa = p5; pb = p6; da = d5; db = d6; break;
                case 6: pa = p6; pb = p7; da = d6; db = d7; break;
                case 7: pa = p7; pb = p4; da = d7; db = d4; break;
                case 8: pa = p0; pb = p4; da = d0; db = d4; break;
                case 9: pa = p1; pb = p5; da = d1; db = d5; break;
                case 10: pa = p2; pb = p6; da = d2; db = d6; break;
                case 11: pa = p3; pb = p7; da = d3; db = d7; break;
            }
            return VertexInterp(isovalue, pa, pb, da, db);
        }

        private float3 VertexInterp(float isolevel, float3 p1, float3 p2, float valp1, float valp2)
        {
            if (math.abs(isolevel - valp1) < Epsilon) return p1;
            if (math.abs(isolevel - valp2) < Epsilon) return p2;
            if (math.abs(valp1 - valp2) < Epsilon) return (p1 + p2) * 0.5f;

            float mu = (isolevel - valp1) / (valp2 - valp1);
            return p1 + mu * (p2 - p1);
        }

        private float3 GetPositionInline(int x, int y, int z, int resolution, float spacing, float3 origin)
        {
            float3 centerCell = new float3(resolution / 2f, resolution / 2f, resolution / 2f);
            return (new float3(x, y, z) - centerCell) * spacing + origin;
        }

        private int Index(int res, int x, int y, int z)
        {
            return x + res * y + res * res * z;
        }
    }

    public static void BuildMeshRange(
        NativeArray<float> densities,
        int resolution,
        float spacing,
        float3 origin,
        float isovalue,
        int3 minCell,
        int3 maxCell,
        List<Vector3> verts,
        List<int> indices,
        NativeArray<int> edgeTable,
        NativeArray<int> triangleTable)
    {
        if (!densities.IsCreated || resolution < 2) return;
        if (verts == null || indices == null) return;

        int max = resolution - 1;
        minCell = math.clamp(minCell, int3.zero, new int3(max - 1, max - 1, max - 1));
        maxCell = math.clamp(maxCell, minCell, new int3(max, max, max));
        if (math.any(maxCell <= minCell)) return;

        int numCellsX = maxCell.x - minCell.x;
        int numCellsY = maxCell.y - minCell.y;
        int numCellsZ = maxCell.z - minCell.z;
        int totalCells = numCellsX * numCellsY * numCellsZ;
        
        var stream = new NativeStream(totalCells, Allocator.TempJob);

        new MarchingCubesJob
        {
            densities = densities,
            edgeTable = edgeTable,
            triangleTable = triangleTable,
            writer = stream.AsWriter(),
            resolution = resolution,
            spacing = spacing,
            origin = origin,
            isovalue = isovalue,
            minCell = minCell,
            numCellsX = numCellsX,
            numCellsY = numCellsY
        }.Schedule(totalCells, 32).Complete();

        var reader = stream.AsReader();
        for (int i = 0; i < stream.ForEachCount; i++)
        {
            int count = reader.BeginForEachIndex(i);
            for (int j = 0; j < count; j++)
            {
                var tri = reader.Read<Triangle>();
                int startIdx = verts.Count;
                verts.Add((Vector3)tri.v0);
                verts.Add((Vector3)tri.v1);
                verts.Add((Vector3)tri.v2);
                indices.Add(startIdx);
                indices.Add(startIdx + 1);
                indices.Add(startIdx + 2);
            }
            reader.EndForEachIndex();
        }

        stream.Dispose();
    }

    public static void ApplyMesh(Mesh mesh, List<Vector3> verts, List<int> indices)
    {
        mesh.Clear();
        if (verts.Count == 0)
            return;

        mesh.indexFormat = verts.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
        mesh.SetVertices(verts);
        mesh.SetTriangles(indices, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }
}