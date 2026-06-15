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

    private const int BucketCount = 4096;
    private readonly List<Boid>[] grid = new List<Boid>[BucketCount];
    private readonly HashSet<int> drawnCells = new HashSet<int>();

    public BoidSpatialHash(float cellSize)
    {
        this.cellSize = cellSize;
        for (int i = 0; i < BucketCount; i++)
        {
            grid[i] = new List<Boid>();
        }
    }

    public void Clear()
    {
        for (int i = 0; i < BucketCount; i++)
        {
            grid[i].Clear();
        }
    }

    public void AddBoid(Boid boid)
    {
        int key = GetKey(boid.Position);
        int index = GetBucketIndex(key);
        grid[index].Add(boid);
    }

    public void ProcessNeighbors(Boid boid, float neighborSqrRadius, float neighborRadius,
                                 ref Vector3 separationVector, ref Vector3 directionVector,
                                 ref Vector3 cohesionPos, ref int neighborCount)
    {
        Vector3 pos = boid.Position;
        float myPosX = pos.x;
        float myPosY = pos.y;
        float myPosZ = pos.z;

        float invCellSize = 1f / cellSize;
        int cx = Mathf.FloorToInt(myPosX * invCellSize);
        int cy = Mathf.FloorToInt(myPosY * invCellSize);
        int cz = Mathf.FloorToInt(myPosZ * invCellSize);

        float invNeighborRadius = 1f / neighborRadius;

        for (int x = -1; x <= 1; x++)
        {
            int hashX = (17 * 23 + cx + x) * 23;
            for (int y = -1; y <= 1; y++)
            {
                int hashY = (hashX + cy + y) * 23;
                for (int z = -1; z <= 1; z++)
                {
                    int key = hashY + cz + z;

                    int index = key % BucketCount;
                    if (index < 0) index += BucketCount;

                    List<Boid> cellList = grid[index];
                    int cellCount = cellList.Count;

                    for (int i = 0; i < cellCount; i++)
                    {
                        Boid neighbor = cellList[i];
                        if (ReferenceEquals(neighbor, boid))
                        {
                            continue;
                        }

                        float dx = neighbor.Position.x - myPosX;
                        float dy = neighbor.Position.y - myPosY;
                        float dz = neighbor.Position.z - myPosZ;
                        float sqrDst = dx * dx + dy * dy + dz * dz;

                        if (sqrDst < neighborSqrRadius)
                        {
                            float distance = Mathf.Sqrt(sqrDst);
                            if (distance > 0f)
                            {
                                float invDistance = 1f / distance;
                                float factor = invDistance * (1f - (distance * invNeighborRadius));
                                separationVector.x -= dx * factor;
                                separationVector.y -= dy * factor;
                                separationVector.z -= dz * factor;
                            }
                            directionVector.x += neighbor.ForwardVec.x;
                            directionVector.y += neighbor.ForwardVec.y;
                            directionVector.z += neighbor.ForwardVec.z;

                            cohesionPos.x += neighbor.Position.x;
                            cohesionPos.y += neighbor.Position.y;
                            cohesionPos.z += neighbor.Position.z;
                            neighborCount++;
                        }
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

    private int GetBucketIndex(int key)
    {
        int index = key % BucketCount;
        return index < 0 ? index + BucketCount : index;
    }

    public void DrawGridGizmos()
    {
        Gizmos.color = Color.yellow;
        drawnCells.Clear();

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
