using System.Collections.Generic;
using UnityEngine;

public class BoidSpatialHashGrid
{
    private readonly float cellSize;
    
    // Grids for Prey and Predators
    private readonly Dictionary<Vector3Int, List<PreyBoid>> preyGrid = new Dictionary<Vector3Int, List<PreyBoid>>();
    private readonly Dictionary<Vector3Int, List<PredatorBoid>> predatorGrid = new Dictionary<Vector3Int, List<PredatorBoid>>();

    // List pools to avoid allocations
    private readonly List<List<PreyBoid>> preyListPool = new List<List<PreyBoid>>();
    private readonly List<List<PredatorBoid>> predatorListPool = new List<List<PredatorBoid>>();

    public BoidSpatialHashGrid(float cellSize)
    {
        this.cellSize = cellSize;
    }

    public void Clear()
    {
        // Return Prey lists to pool
        foreach (var list in preyGrid.Values)
        {
            list.Clear();
            preyListPool.Add(list);
        }
        preyGrid.Clear();

        // Return Predator lists to pool
        foreach (var list in predatorGrid.Values)
        {
            list.Clear();
            predatorListPool.Add(list);
        }
        predatorGrid.Clear();
    }

    private Vector3Int GetCellCoords(Vector3 position)
    {
        return new Vector3Int(
            Mathf.FloorToInt(position.x / cellSize),
            Mathf.FloorToInt(position.y / cellSize),
            Mathf.FloorToInt(position.z / cellSize)
        );
    }

    private List<PreyBoid> GetOrCreatePreyList(Vector3Int cellKey)
    {
        if (preyGrid.TryGetValue(cellKey, out var list))
        {
            return list;
        }

        if (preyListPool.Count > 0)
        {
            list = preyListPool[preyListPool.Count - 1];
            preyListPool.RemoveAt(preyListPool.Count - 1);
        }
        else
        {
            list = new List<PreyBoid>();
        }
        preyGrid[cellKey] = list;
        return list;
    }

    private List<PredatorBoid> GetOrCreatePredatorList(Vector3Int cellKey)
    {
        if (predatorGrid.TryGetValue(cellKey, out var list))
        {
            return list;
        }

        if (predatorListPool.Count > 0)
        {
            list = predatorListPool[predatorListPool.Count - 1];
            predatorListPool.RemoveAt(predatorListPool.Count - 1);
        }
        else
        {
            list = new List<PredatorBoid>();
        }
        predatorGrid[cellKey] = list;
        return list;
    }

    public void InsertPrey(PreyBoid prey)
    {
        Vector3Int coords = GetCellCoords(prey.transform.position);
        List<PreyBoid> cell = GetOrCreatePreyList(coords);
        cell.Add(prey);
    }

    public void InsertPredator(PredatorBoid predator)
    {
        Vector3Int coords = GetCellCoords(predator.transform.position);
        List<PredatorBoid> cell = GetOrCreatePredatorList(coords);
        cell.Add(predator);
    }

    public void GetPreyNeighbors(Vector3 position, float radius, List<PreyBoid> results)
    {
        results.Clear();
        Vector3Int centerCoords = GetCellCoords(position);
        int cellRadius = Mathf.CeilToInt(radius / cellSize);
        float radiusSq = radius * radius;

        for (int x = -cellRadius; x <= cellRadius; x++)
        {
            for (int y = -cellRadius; y <= cellRadius; y++)
            {
                for (int z = -cellRadius; z <= cellRadius; z++)
                {
                    Vector3Int coords = new Vector3Int(centerCoords.x + x, centerCoords.y + y, centerCoords.z + z);
                    if (preyGrid.TryGetValue(coords, out var cell))
                    {
                        int count = cell.Count;
                        for (int i = 0; i < count; i++)
                        {
                            PreyBoid prey = cell[i];
                            float distSq = (prey.transform.position - position).sqrMagnitude;
                            if (distSq <= radiusSq)
                            {
                                results.Add(prey);
                            }
                        }
                    }
                }
            }
        }
    }

    public void GetPredatorNeighbors(Vector3 position, float radius, List<PredatorBoid> results)
    {
        results.Clear();
        Vector3Int centerCoords = GetCellCoords(position);
        int cellRadius = Mathf.CeilToInt(radius / cellSize);
        float radiusSq = radius * radius;

        for (int x = -cellRadius; x <= cellRadius; x++)
        {
            for (int y = -cellRadius; y <= cellRadius; y++)
            {
                for (int z = -cellRadius; z <= cellRadius; z++)
                {
                    Vector3Int coords = new Vector3Int(centerCoords.x + x, centerCoords.y + y, centerCoords.z + z);
                    if (predatorGrid.TryGetValue(coords, out var cell))
                    {
                        int count = cell.Count;
                        for (int i = 0; i < count; i++)
                        {
                            PredatorBoid predator = cell[i];
                            float distSq = (predator.transform.position - position).sqrMagnitude;
                            if (distSq <= radiusSq)
                            {
                                results.Add(predator);
                            }
                        }
                    }
                }
            }
        }
    }
}
