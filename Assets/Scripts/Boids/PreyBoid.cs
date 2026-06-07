using UnityEngine;

public class PreyBoid : Boid
{
    [Header("Prey Settings")]
    public float predatorDetectRadius = 10f;
    public float fleeWeight = 40f;
    public LayerMask predatorMask;

    private bool AccumulateForce(ref Vector3 totalForce, Vector3 forceToApply)
    {
        float magnitudeSoFar = totalForce.magnitude;
        float magnitudeRemaining = maxSteerForce - magnitudeSoFar;

        if (magnitudeRemaining <= 0)
        {
            return false;
        }

        float magnitudeToAdd = forceToApply.magnitude;

        if (magnitudeToAdd < magnitudeRemaining)
        {
            totalForce += forceToApply;
        }
        else
        {
            totalForce += forceToApply.normalized * magnitudeRemaining;
            return false;
        }

        return true;
    }

    protected override void Update()
    {
        InitializeBoid();

        Vector3 totalSteerForce = Vector3.zero;

        bool hasPredators = false;
        Vector3 fleeVector = Vector3.zero;

        if (BoidSystemManager.Instance != null && BoidSystemManager.Instance.useSpatialHash)
        {
            BoidSystemManager.Instance.Grid.GetPredatorNeighbors(transform.position, predatorDetectRadius, tempPredatorNeighbors);
            int predCount = tempPredatorNeighbors.Count;
            if (predCount > 0)
            {
                hasPredators = true;
                for (int i = 0; i < predCount; i++)
                {
                    PredatorBoid pred = tempPredatorNeighbors[i];
                    Vector3 toPrey = transform.position - pred.transform.position;
                    float dist = toPrey.magnitude;
                    if (dist > 0f)
                    {
                        fleeVector += (toPrey / dist) / dist;
                    }
                }
            }
        }
        else
        {
            Collider[] predators = Physics.OverlapSphere(transform.position, predatorDetectRadius, predatorMask);
            if (predators != null && predators.Length > 0)
            {
                hasPredators = true;
                foreach (var pred in predators)
                {
                    Vector3 toPrey = transform.position - pred.transform.position;
                    float dist = toPrey.magnitude;
                    if (dist > 0f)
                    {
                        fleeVector += (toPrey / dist) / dist;
                    }
                }
            }
        }

        if (hasPredators && fleeVector != Vector3.zero)
        {
            Vector3 fleeForce = SteerTowards(fleeVector) * fleeWeight;
            AccumulateForce(ref totalSteerForce, fleeForce);
        }

        if (totalSteerForce.magnitude < maxSteerForce)
        {
            if (IsHeadingForCollision())
            {
                Vector3 collisionAvoidDir;
                float distanceToObstacle;

                if (GetObstacleDistance(out collisionAvoidDir, out distanceToObstacle))
                {
                    float proximityFactor = 1f - (distanceToObstacle / collisionAvoidDst);
                    Vector3 collisionAvoidForce = SteerTowards(collisionAvoidDir) * avoidCollisionWeight * (1f + proximityFactor * 2f);

                    AccumulateForce(ref totalSteerForce, collisionAvoidForce);
                }
            }
        }

        if (totalSteerForce.magnitude < maxSteerForce)
        {
            FindNeighbors();

            if (neighborCount > 0)
            {
                if (separationVector != Vector3.zero) separationVector = separationVector.normalized;
                if (directionVector != Vector3.zero) directionVector = directionVector.normalized;

                Vector3 averageCohesionPos = cohesionPos / neighborCount;
                cohesionVector = averageCohesionPos - transform.position;
                if (cohesionVector != Vector3.zero) cohesionVector = cohesionVector.normalized;

                Vector3 flockingForce = (separationVector * separationWeight) +
                                       (directionVector * alignmentWeight) +
                                       (cohesionVector * cohesionWeight);

                AccumulateForce(ref totalSteerForce, flockingForce);
            }
            else
            {
                Vector3 forwardForce = transform.forward * 0.5f;
                AccumulateForce(ref totalSteerForce, forwardForce);
            }
        }

        acceleration = totalSteerForce;

        UpdateMovement();
    }

    protected override void FindNeighbors()
    {
        if (BoidSystemManager.Instance != null && BoidSystemManager.Instance.useSpatialHash)
        {
            BoidSystemManager.Instance.Grid.GetPreyNeighbors(transform.position, neighborRadius, tempPreyNeighbors);
            int preyCount = tempPreyNeighbors.Count;
            for (int i = 0; i < preyCount; i++)
            {
                PreyBoid neighbor = tempPreyNeighbors[i];
                if (neighbor.gameObject != gameObject)
                {
                    CalculateNeighborBoid(neighbor);
                }
            }
        }
        else
        {
            Collider[] neighbors = Physics.OverlapSphere(transform.position, neighborRadius);
            foreach (Collider col in neighbors)
            {
                if (col.gameObject != gameObject && col.GetComponent<PreyBoid>() != null)
                {
                    CalculateNeighborBoid(col);
                }
            }
        }
    }
}
