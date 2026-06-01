using UnityEngine;

public class Boid : MonoBehaviour
{
    public static readonly System.Collections.Generic.List<Boid> ActiveBoids = new System.Collections.Generic.List<Boid>();

    [Header("Boid Settings")]
    public float speed = 5f;
    public float rotationSpeed = 5f;
    public float neighborRadius = 5f;

    [Header("Behavior Weights")]
    public float separationWeight = 1f;
    public float alignmentWeight = 1f;
    public float cohesionWeight = 1f;

    [Header("Obstacle Avoidance Settings")]
    public LayerMask obstacleMask;
    public float boundsRadius = 0.25f;
    public float collisionAvoidDst = 5f;
    public float avoidCollisionWeight = 30f;
    public float maxBoundsSpeed = 5f;
    public float maxSteerForce = 10f;

    private Vector3 separationVector;
    private Vector3 directionVector;
    private Vector3 cohesionPos;
    private Vector3 cohesionVector;
    private Vector3 velocity;
    private Vector3 acceleration;
    private int neighborCount;

    void OnEnable()
    {
        ActiveBoids.Add(this);
    }

    void OnDisable()
    {
        ActiveBoids.Remove(this);
    }

    void Start()
    {
        velocity = transform.forward * speed;
    }

    void Update()
    {
        InitializeBoid();

        Collider[] neighbors = Physics.OverlapSphere(transform.position, neighborRadius);

        foreach (Collider col in neighbors)
        {
            if (col.gameObject != gameObject && col.CompareTag("Boid"))
            {
                CalculateNeighborBoid(col);
            }
        }

        if (neighborCount > 0)
        {
            CalculateMoveVector();
        }
        else
        {
            acceleration += transform.forward * 0.5f;
        }

        if (IsHeadingForCollision())
        {
            Vector3 collisionAvoidDir;
            float distanceToObstacle;

            if (GetObstacleDistance(out collisionAvoidDir, out distanceToObstacle))
            {
                float proximityFactor = 1f - (distanceToObstacle / collisionAvoidDst);
                Vector3 collisionAvoidForce = SteerTowards(collisionAvoidDir) * avoidCollisionWeight * (1f + proximityFactor * 2f);

                acceleration += collisionAvoidForce;
            }
        }

        velocity += acceleration * Time.deltaTime;

        float currentSpeed = velocity.magnitude;
        Vector3 dir = velocity / currentSpeed;
        currentSpeed = Mathf.Clamp(currentSpeed, speed * 0.5f, maxBoundsSpeed);
        velocity = dir * currentSpeed;

        if (velocity != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(velocity);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        transform.position += velocity * Time.deltaTime;
    }

    private void InitializeBoid()
    {
        separationVector = Vector3.zero;
        directionVector = Vector3.zero;
        cohesionPos = Vector3.zero;
        acceleration = Vector3.zero;
        neighborCount = 0;
    }

    private void CalculateNeighborBoid(Collider col)
    {
        Vector3 separationDir = transform.position - col.transform.position;
        float distance = separationDir.magnitude;

        separationVector += separationDir.normalized * (1f - (distance / neighborRadius));
        directionVector += col.transform.forward;
        cohesionPos += col.transform.position;
        neighborCount++;
    }

    private void CalculateMoveVector()
    {
        if (separationVector != Vector3.zero) separationVector = separationVector.normalized;
        if (directionVector != Vector3.zero) directionVector = directionVector.normalized;

        Vector3 averageCohesionPos = cohesionPos / neighborCount;
        cohesionVector = averageCohesionPos - transform.position;
        if (cohesionVector != Vector3.zero) cohesionVector = cohesionVector.normalized;

        acceleration += (separationVector * separationWeight) +
                        (directionVector * alignmentWeight) +
                        (cohesionVector * cohesionWeight);
    }

    private bool IsHeadingForCollision()
    {
        RaycastHit hit;
        Vector3 moveDir = velocity != Vector3.zero ? velocity.normalized : transform.forward;

        if (Physics.SphereCast(transform.position, boundsRadius, moveDir, out hit, collisionAvoidDst, obstacleMask))
        {
            return true;
        }
        return false;
    }

    private bool GetObstacleDistance(out Vector3 avoidDir, out float distance)
    {
        RaycastHit hit;
        Vector3 moveDir;

        if (velocity != Vector3.zero)
        {
            moveDir = velocity.normalized;
        }
        else
        {
            moveDir = transform.forward;
        }

        if (Physics.SphereCast(transform.position, boundsRadius, moveDir, out hit, collisionAvoidDst, obstacleMask))
        {
            distance = hit.distance;
            avoidDir = ObstacleRays();
            return true;
        }

        avoidDir = transform.forward;
        distance = collisionAvoidDst;
        return false;
    }

    private Vector3 ObstacleRays()
    {
        Vector3[] rayDirections = BoidHelper.directions;
        Quaternion lookRotation = velocity != Vector3.zero ? Quaternion.LookRotation(velocity.normalized) : transform.rotation;

        for (int i = 0; i < rayDirections.Length; i++)
        {
            Vector3 dir = lookRotation * rayDirections[i];
            Ray ray = new Ray(transform.position, dir);
            if (!Physics.SphereCast(ray, boundsRadius, collisionAvoidDst, obstacleMask))
            {
                return dir;
            }
        }

        return transform.forward;
    }

    private Vector3 SteerTowards(Vector3 vector)
    {
        Vector3 desiredVelocity = vector.normalized * maxBoundsSpeed;
        Vector3 steer = desiredVelocity - velocity;
        return Vector3.ClampMagnitude(steer, maxSteerForce);
    }
}