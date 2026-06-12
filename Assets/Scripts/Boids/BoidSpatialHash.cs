using UnityEngine;
using System.Collections.Generic;

public class BoidSpatialHash
{
    private float cellSize;
    public float CellSize
    {
        get => cellSize;
        set => cellSize = value;
    }
    private readonly Dictionary<int, List<Boid>> grid = new Dictionary<int, List<Boid>>();
    private readonly Stack<List<Boid>> listPool = new Stack<List<Boid>>();

    public BoidSpatialHash(float cellSize)
    {
        this.cellSize = cellSize;
    }

    public void Clear()
    {
        foreach (var pair in grid)
        {
            pair.Value.Clear();
            listPool.Push(pair.Value);
        }
        grid.Clear();
    }

    public void AddBoid(Boid boid)
    {
        int key = GetKey(boid.Position);
        if (!grid.TryGetValue(key, out List<Boid> cellList))
        {
            cellList = listPool.Count > 0 ? listPool.Pop() : new List<Boid>();
            grid[key] = cellList;
        }
        cellList.Add(boid);
    }

    public void GetNeighbors(Boid boid, List<Boid> results)
    {
        Vector3 pos = boid.Position;
        int cx = Mathf.FloorToInt(pos.x / cellSize);
        int cy = Mathf.FloorToInt(pos.y / cellSize);
        int cz = Mathf.FloorToInt(pos.z / cellSize);

        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                for (int z = -1; z <= 1; z++)
                {
                    int key = HashCoords(cx + x, cy + y, cz + z);
                    if (grid.TryGetValue(key, out List<Boid> cellList))
                    {
                        results.AddRange(cellList);
                    }
                }
            }
        }
    }

    private int GetKey(Vector3 pos)
    {
        return HashCoords(
            Mathf.FloorToInt(pos.x / cellSize),
            Mathf.FloorToInt(pos.y / cellSize),
            Mathf.FloorToInt(pos.z / cellSize)
        );
    }

    private int HashCoords(int x, int y, int z)
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 23 + x;
            hash = hash * 23 + y;
            hash = hash * 23 + z;
            return hash;
        }
    }

    public void DrawGridGizmos()
    {
        Gizmos.color = Color.yellow;
        HashSet<int> drawnCells = new HashSet<int>();

        int count = Boid.ActiveBoids.Count;
        for (int i = 0; i < count; i++)
        {
            Boid boid = Boid.ActiveBoids[i];
            if (boid == null) continue;

            Vector3 pos = boid.Position;
            int cx = Mathf.FloorToInt(pos.x / cellSize);
            int cy = Mathf.FloorToInt(pos.y / cellSize);
            int cz = Mathf.FloorToInt(pos.z / cellSize);

            int key = HashCoords(cx, cy, cz);
            if (!drawnCells.Contains(key))
            {
                drawnCells.Add(key);

                Vector3 cellCenter = new Vector3(
                    cx * cellSize + cellSize * 0.5f,
                    cy * cellSize + cellSize * 0.5f,
                    cz * cellSize + cellSize * 0.5f
                );

                Gizmos.DrawWireCube(cellCenter, Vector3.one * cellSize);
            }
        }
    }
}

public static class BoidSystem
{
    public static BoidSpatialHash SpatialHash { get; private set; }
    private static int lastFrameCount = -1;

    public static void EnsureGridBuilt()
    {
        if (Time.frameCount != lastFrameCount)
        {
            lastFrameCount = Time.frameCount;

            float targetCellSize = 5f;
            int boidCount = Boid.ActiveBoids.Count;
            for (int i = 0; i < boidCount; i++)
            {
                Boid activeBoid = Boid.ActiveBoids[i];
                if (activeBoid != null)
                {
                    targetCellSize = activeBoid.neighborRadius;
                    break;
                }
            }

            if (SpatialHash == null)
            {
                SpatialHash = new BoidSpatialHash(targetCellSize);
            }
            else if (SpatialHash.CellSize != targetCellSize)
            {
                SpatialHash.CellSize = targetCellSize;
            }

            SpatialHash.Clear();
            int count = Boid.ActiveBoids.Count;
            for (int i = 0; i < count; i++)
            {
                Boid activeBoid = Boid.ActiveBoids[i];
                if (activeBoid != null)
                {
                    SpatialHash.AddBoid(activeBoid);
                }
            }
        }
    }
}
