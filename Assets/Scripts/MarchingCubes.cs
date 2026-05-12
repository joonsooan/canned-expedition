using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

public static class MarchingCubes
{
    private static readonly int[,] EdgeVertices =
    {
        { 0, 1 }, { 1, 2 }, { 2, 3 }, { 3, 0 },
        { 4, 5 }, { 5, 6 }, { 6, 7 }, { 7, 4 },
        { 0, 4 }, { 1, 5 }, { 2, 6 }, { 3, 7 }
    };

    private const float Epsilon = 1e-6f;

    public static void BuildMesh(Mesh mesh, FieldData[] data, int resolution, float isovalue)
    {
        mesh.Clear();

        if (data == null || resolution < 2 || data.Length < resolution * resolution * resolution)
            return;

        var verts = new List<Vector3>(4096);
        var indices = new List<int>(6144);
        int max = resolution - 1;
        BuildMeshRange(data, resolution, isovalue, int3.zero, new int3(max, max, max), verts, indices);
        ApplyMesh(mesh, verts, indices);
    }

    public static void BuildMeshRange(
        FieldData[] data,
        int resolution,
        float isovalue,
        int3 minCell,
        int3 maxCell,
        List<Vector3> verts,
        List<int> indices)
    {
        if (data == null || resolution < 2 || data.Length < resolution * resolution * resolution)
            return;
        if (verts == null || indices == null)
            return;

        int max = resolution - 1;
        minCell = math.clamp(minCell, int3.zero, new int3(max - 1, max - 1, max - 1));
        maxCell = math.clamp(maxCell, minCell, new int3(max, max, max));
        if (math.any(maxCell <= minCell))
            return;

        var edgeVertexCache = new Dictionary<long, int>();
        var cornerIndices = new int[8];
        var edgeIndices = new int[12];

        for (int z = minCell.z; z < maxCell.z; z++)
        {
            for (int y = minCell.y; y < maxCell.y; y++)
            {
                for (int x = minCell.x; x < maxCell.x; x++)
                {
                    cornerIndices[0] = Index(resolution, x, y, z);
                    cornerIndices[1] = Index(resolution, x + 1, y, z);
                    cornerIndices[2] = Index(resolution, x + 1, y + 1, z);
                    cornerIndices[3] = Index(resolution, x, y + 1, z);
                    cornerIndices[4] = Index(resolution, x, y, z + 1);
                    cornerIndices[5] = Index(resolution, x + 1, y, z + 1);
                    cornerIndices[6] = Index(resolution, x + 1, y + 1, z + 1);
                    cornerIndices[7] = Index(resolution, x, y + 1, z + 1);

                    int cubeIndex = 0;
                    if (data[cornerIndices[0]].density <= isovalue) cubeIndex |= 1;
                    if (data[cornerIndices[1]].density <= isovalue) cubeIndex |= 2;
                    if (data[cornerIndices[2]].density <= isovalue) cubeIndex |= 4;
                    if (data[cornerIndices[3]].density <= isovalue) cubeIndex |= 8;
                    if (data[cornerIndices[4]].density <= isovalue) cubeIndex |= 16;
                    if (data[cornerIndices[5]].density <= isovalue) cubeIndex |= 32;
                    if (data[cornerIndices[6]].density <= isovalue) cubeIndex |= 64;
                    if (data[cornerIndices[7]].density <= isovalue) cubeIndex |= 128;

                    int edgeMask = LookupTable.edgeTable[cubeIndex];
                    if (edgeMask == 0)
                        continue;

                    for (int e = 0; e < 12; e++)
                    {
                        if ((edgeMask & (1 << e)) == 0)
                            continue;

                        int ca = cornerIndices[EdgeVertices[e, 0]];
                        int cb = cornerIndices[EdgeVertices[e, 1]];
                        long key = EdgeKey(ca, cb);

                        if (!edgeVertexCache.TryGetValue(key, out int vertIndex))
                        {
                            FieldData fda = data[ca];
                            FieldData fdb = data[cb];
                            vertIndex = verts.Count;
                            verts.Add(VertexInterp(isovalue, fda.position, fdb.position, fda.density, fdb.density));
                            edgeVertexCache[key] = vertIndex;
                        }

                        edgeIndices[e] = vertIndex;
                    }

                    for (int t = 0; t < 16; t += 3)
                    {
                        int e0 = LookupTable.triangleTable[cubeIndex, t];
                        if (e0 < 0)
                            break;

                        int e1 = LookupTable.triangleTable[cubeIndex, t + 1];
                        int e2 = LookupTable.triangleTable[cubeIndex, t + 2];

                        indices.Add(edgeIndices[e0]);
                        indices.Add(edgeIndices[e2]);
                        indices.Add(edgeIndices[e1]);
                    }
                }
            }
        }
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

    private static int Index(int resolution, int x, int y, int z)
    {
        return x + resolution * y + resolution * resolution * z;
    }

    private static long EdgeKey(int a, int b)
    {
        if (a > b) { int t = a; a = b; b = t; }
        return ((long)a << 32) | (uint)b;
    }

    private static Vector3 VertexInterp(float isolevel, float3 p1, float3 p2, float valp1, float valp2)
    {
        if (math.abs(isolevel - valp1) < Epsilon)
            return (Vector3)p1;
        if (math.abs(isolevel - valp2) < Epsilon)
            return (Vector3)p2;
        if (math.abs(valp1 - valp2) < Epsilon)
            return (Vector3)((p1 + p2) * 0.5f);

        float mu = (isolevel - valp1) / (valp2 - valp1);
        return (Vector3)(p1 + mu * (p2 - p1));
    }
}