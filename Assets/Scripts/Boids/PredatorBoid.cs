using UnityEngine;

public class PredatorBoid : Boid
{
    [Header("Predator Settings")]
    public float preyDetectRadius = 15f;
    public float chaseWeight = 25f;
    public LayerMask preyMask;

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

        if (totalSteerForce.magnitude < maxSteerForce)
        {
            Collider[] preys = Physics.OverlapSphere(transform.position, preyDetectRadius, preyMask);
            if (preys != null && preys.Length > 0)
            {
                Collider nearestPrey = null;
                float nearestDist = Mathf.Infinity;

                foreach (var prey in preys)
                {
                    float dist = Vector3.Distance(transform.position, prey.transform.position);
                    if (dist < nearestDist)
                    {
                        nearestDist = dist;
                        nearestPrey = prey;
                    }
                }

                if (nearestPrey != null)
                {
                    Vector3 chaseDir = nearestPrey.transform.position - transform.position;
                    Vector3 chaseForce = SteerTowards(chaseDir) * chaseWeight;
                    AccumulateForce(ref totalSteerForce, chaseForce);
                }
            }
        }

        if (totalSteerForce.magnitude < maxSteerForce)
        {
            FindNeighbors();
            if (neighborCount > 0)
            {
                if (separationVector != Vector3.zero)
                {
                    Vector3 separationForce = separationVector.normalized * separationWeight;
                    AccumulateForce(ref totalSteerForce, separationForce);
                }
            }
        }

        acceleration = totalSteerForce;

        UpdateMovement();
    }

    protected override void FindNeighbors()
    {
        Collider[] neighbors = Physics.OverlapSphere(transform.position, neighborRadius);

        foreach (Collider col in neighbors)
        {
            if (col.gameObject != gameObject && col.GetComponent<PredatorBoid>() != null)
            {
                CalculateNeighborBoid(col);
            }
        }
    }
}
