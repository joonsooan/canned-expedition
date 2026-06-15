using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

public class BoidSpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    public GameObject boidPrefab;
    public GameObject boidParent;
    public int spawnCount;
    public float spawnRadius;

    void Start()
    {
        SpawnBoids();
    }

    private void SpawnBoids()
    {
        for (int i = 0; i < spawnCount; i++)
        {
            Vector3 randPos = transform.position + UnityEngine.Random.insideUnitSphere * spawnRadius;

            Quaternion randRotation = Quaternion.Euler(
                UnityEngine.Random.Range(0f, 360f),
                UnityEngine.Random.Range(0f, 360f),
                UnityEngine.Random.Range(0f, 360f)
            );

            Instantiate(boidPrefab, randPos, randRotation, boidParent.transform);
        }
    }

    private void Update()
    {
        var boids = Boid.ActiveBoids;
        int count = boids.Count;
        if (count == 0) return;

        // Allocate NativeArrays
        NativeArray<float3> positions = new NativeArray<float3>(count, Allocator.TempJob);
        NativeArray<float3> forwards = new NativeArray<float3>(count, Allocator.TempJob);
        
        NativeArray<float3> separationForces = new NativeArray<float3>(count, Allocator.TempJob);
        NativeArray<float3> alignmentForces = new NativeArray<float3>(count, Allocator.TempJob);
        NativeArray<float3> cohesionForces = new NativeArray<float3>(count, Allocator.TempJob);
        NativeArray<int> neighborCounts = new NativeArray<int>(count, Allocator.TempJob);

        // Gather settings
        float neighborRadius = 5f;
        float neighborSqrRadius = 25f;
        
        if (count > 0 && boids[0] != null)
        {
            neighborRadius = boids[0].neighborRadius;
            neighborSqrRadius = neighborRadius * neighborRadius;
        }

        for (int i = 0; i < count; i++)
        {
            if (boids[i] != null)
            {
                positions[i] = boids[i].Position;
                forwards[i] = boids[i].ForwardVec;
            }
        }

        // Setup Job
        BoidSimulationJob job = new BoidSimulationJob
        {
            positions = positions,
            forwards = forwards,
            neighborRadius = neighborRadius,
            neighborSqrRadius = neighborSqrRadius,
            separationForces = separationForces,
            alignmentForces = alignmentForces,
            cohesionForces = cohesionForces,
            neighborCounts = neighborCounts
        };

        // Schedule Job (runs in parallel across worker threads)
        JobHandle handle = job.Schedule(count, 64);
        
        // Wait for completion
        handle.Complete();

        // Write outputs back
        for (int i = 0; i < count; i++)
        {
            if (boids[i] != null)
            {
                boids[i].jobSeparation = separationForces[i];
                boids[i].jobAlignment = alignmentForces[i];
                boids[i].jobCohesion = cohesionForces[i];
                boids[i].jobNeighborCount = neighborCounts[i];
            }
        }

        // Dispose native arrays
        positions.Dispose();
        forwards.Dispose();
        separationForces.Dispose();
        alignmentForces.Dispose();
        cohesionForces.Dispose();
        neighborCounts.Dispose();
    }

    [Header("Gizmo Settings")]
    public bool drawGridGizmos = true;

    private void OnDrawGizmos()
    {
        // Gizmos no longer draw grid since spatial hash is removed
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, spawnRadius);
    }
}
