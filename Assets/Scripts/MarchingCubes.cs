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
        var edgePoints = new Vector3[12];

        for (int z = 0; z < max; z++)
        {
            for (int y = 0; y < max; y++)
            {
                for (int x = 0; x < max; x++)
                {
                    int i0 = Index(resolution, x, y, z);
                    int i1 = Index(resolution, x + 1, y, z);
                    int i2 = Index(resolution, x + 1, y + 1, z);
                    int i3 = Index(resolution, x, y + 1, z);
                    int i4 = Index(resolution, x, y, z + 1);
                    int i5 = Index(resolution, x + 1, y, z + 1);
                    int i6 = Index(resolution, x + 1, y + 1, z + 1);
                    int i7 = Index(resolution, x, y + 1, z + 1);

                    float d0 = data[i0].density;
                    float d1 = data[i1].density;
                    float d2 = data[i2].density;
                    float d3 = data[i3].density;
                    float d4 = data[i4].density;
                    float d5 = data[i5].density;
                    float d6 = data[i6].density;
                    float d7 = data[i7].density;

                    float3 p0 = data[i0].position;
                    float3 p1 = data[i1].position;
                    float3 p2 = data[i2].position;
                    float3 p3 = data[i3].position;
                    float3 p4 = data[i4].position;
                    float3 p5 = data[i5].position;
                    float3 p6 = data[i6].position;
                    float3 p7 = data[i7].position;

                    int cubeIndex = 0;
                    if (d0 <= isovalue) cubeIndex |= 1;
                    if (d1 <= isovalue) cubeIndex |= 2;
                    if (d2 <= isovalue) cubeIndex |= 4;
                    if (d3 <= isovalue) cubeIndex |= 8;
                    if (d4 <= isovalue) cubeIndex |= 16;
                    if (d5 <= isovalue) cubeIndex |= 32;
                    if (d6 <= isovalue) cubeIndex |= 64;
                    if (d7 <= isovalue) cubeIndex |= 128;

                    int edgeMask = LookupTable.edgeTable[cubeIndex];
                    if (edgeMask == 0)
                        continue;

                    for (int e = 0; e < 12; e++)
                    {
                        if ((edgeMask & (1 << e)) == 0)
                            continue;

                        int a = EdgeVertices[e, 0];
                        int b = EdgeVertices[e, 1];
                        float3 va = default;
                        float3 vb = default;
                        float da = 0f;
                        float db = 0f;
                        switch (a)
                        {
                            case 0: va = p0; da = d0; break;
                            case 1: va = p1; da = d1; break;
                            case 2: va = p2; da = d2; break;
                            case 3: va = p3; da = d3; break;
                            case 4: va = p4; da = d4; break;
                            case 5: va = p5; da = d5; break;
                            case 6: va = p6; da = d6; break;
                            case 7: va = p7; da = d7; break;
                        }

                        switch (b)
                        {
                            case 0: vb = p0; db = d0; break;
                            case 1: vb = p1; db = d1; break;
                            case 2: vb = p2; db = d2; break;
                            case 3: vb = p3; db = d3; break;
                            case 4: vb = p4; db = d4; break;
                            case 5: vb = p5; db = d5; break;
                            case 6: vb = p6; db = d6; break;
                            case 7: vb = p7; db = d7; break;
                        }

                        edgePoints[e] = VertexInterp(isovalue, va, vb, da, db);
                    }

                    for (int t = 0; t < 16; t += 3)
                    {
                        int e0 = LookupTable.triangleTable[cubeIndex, t];
                        if (e0 < 0)
                            break;

                        int e1 = LookupTable.triangleTable[cubeIndex, t + 1];
                        int e2 = LookupTable.triangleTable[cubeIndex, t + 2];

                        int start = verts.Count;
                        verts.Add(edgePoints[e0]);
                        verts.Add(edgePoints[e1]);
                        verts.Add(edgePoints[e2]);
                        indices.Add(start);
                        indices.Add(start + 2);
                        indices.Add(start + 1);
                    }
                }
            }
        }

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
