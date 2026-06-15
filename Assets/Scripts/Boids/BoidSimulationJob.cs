using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

[BurstCompile]
public struct BoidSimulationJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<float3> positions;
    [ReadOnly] public NativeArray<float3> forwards;

    public float neighborRadius;
    public float neighborSqrRadius;

    public NativeArray<float3> separationForces;
    public NativeArray<float3> alignmentForces;
    public NativeArray<float3> cohesionForces;
    public NativeArray<int> neighborCounts;

    public void Execute(int index)
    {
        float3 myPos = positions[index];
        float3 myForward = forwards[index];

        float3 separation = float3.zero;
        float3 alignment = float3.zero;
        float3 cohesion = float3.zero;
        int count = 0;

        int numBoids = positions.Length;
        for (int i = 0; i < numBoids; i++)
        {
            if (i == index) continue;

            float3 neighborPos = positions[i];
            float3 dir = myPos - neighborPos;
            float sqrDst = math.lengthsq(dir);

            if (sqrDst < neighborSqrRadius)
            {
                float distance = math.sqrt(sqrDst);
                if (distance > 0f)
                {
                    separation += dir / distance * (1f - (distance / neighborRadius));
                }
                alignment += forwards[i];
                cohesion += neighborPos;
                count++;
            }
        }

        separationForces[index] = separation;
        alignmentForces[index] = alignment;
        cohesionForces[index] = cohesion;
        neighborCounts[index] = count;
    }
}
