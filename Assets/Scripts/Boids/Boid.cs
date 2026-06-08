using UnityEngine;

public class Boid : MonoBehaviour
{
    public static readonly System.Collections.Generic.List<Boid> ActiveBoids = new System.Collections.Generic.List<Boid>();

    [Header("Boid Settings")]
    public float speed = 5f;
    public float rotationSpeed = 5f;
    public float neighborRadius = 5f;

    [Header("Color Settings")]
    public bool useRandomColor = true;
    public Color boidColor = Color.white;

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

    protected Vector3 separationVector;
    protected Vector3 directionVector;
    protected Vector3 cohesionPos;
    protected Vector3 cohesionVector;
    protected Vector3 velocity;
    protected Vector3 acceleration;
    protected int neighborCount;

    protected virtual void OnEnable()
    {
        ActiveBoids.Add(this);
    }

    protected virtual void OnDisable()
    {
        ActiveBoids.Remove(this);
    }

    protected virtual void Start()
    {
        velocity = transform.forward * speed;
        SetRandomColor();
    }

    protected virtual void Update()
    {
        InitializeBoid();
        FindNeighbors();
        ApplyFlockingBehaviors();
        HandleObstacleAvoidance();
        UpdateMovement();
    }

    protected virtual void FindNeighbors()
    {
        Collider[] neighbors = Physics.OverlapSphere(transform.position, neighborRadius);

        foreach (Collider col in neighbors)
        {
            if (col.gameObject != gameObject && col.CompareTag("Boid"))
            {
                CalculateNeighborBoid(col);
            }
        }
    }

    protected virtual void ApplyFlockingBehaviors()
    {
        if (neighborCount > 0)
        {
            CalculateMoveVector();
        }
        else
        {
            acceleration += transform.forward * 0.5f;
        }
    }

    protected virtual void HandleObstacleAvoidance()
    {
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
    }

    protected virtual void UpdateMovement()
    {
        velocity += acceleration * Time.deltaTime;

        float currentSpeed = velocity.magnitude;
        if (currentSpeed > 0f)
        {
            Vector3 dir = velocity / currentSpeed;
            currentSpeed = Mathf.Clamp(currentSpeed, speed * 0.5f, maxBoundsSpeed);
            velocity = dir * currentSpeed;
        }

        if (velocity != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(velocity);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        transform.position += velocity * Time.deltaTime;
    }

    protected virtual void SetRandomColor()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers != null && renderers.Length > 0)
        {
            MaterialPropertyBlock props = new MaterialPropertyBlock();
            Color colorToApply = useRandomColor
                ? Color.HSVToRGB(Random.Range(0f, 1f), Random.Range(0.3f, 0.5f), Random.Range(0.8f, 1.0f))
                : boidColor;
            props.SetColor("_BaseColor", colorToApply);
            props.SetColor("_Color", colorToApply);

            foreach (Renderer r in renderers)
            {
                r.SetPropertyBlock(props);
            }
        }
    }

    protected virtual void InitializeBoid()
    {
        separationVector = Vector3.zero;
        directionVector = Vector3.zero;
        cohesionPos = Vector3.zero;
        acceleration = Vector3.zero;
        neighborCount = 0;
    }

    protected virtual void CalculateNeighborBoid(Collider col)
    {
        Vector3 separationDir = transform.position - col.transform.position;
        float distance = separationDir.magnitude;

        separationVector += separationDir.normalized * (1f - (distance / neighborRadius));
        directionVector += col.transform.forward;
        cohesionPos += col.transform.position;
        neighborCount++;
    }

    protected virtual void CalculateMoveVector()
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

    protected virtual bool IsHeadingForCollision()
    {
        RaycastHit hit;
        Vector3 moveDir = velocity != Vector3.zero ? velocity.normalized : transform.forward;

        if (Physics.SphereCast(transform.position, boundsRadius, moveDir, out hit, collisionAvoidDst, obstacleMask))
        {
            return true;
        }
        return false;
    }

    protected virtual bool GetObstacleDistance(out Vector3 avoidDir, out float distance)
    {
        RaycastHit hit;
        Vector3 moveDir = velocity != Vector3.zero ? velocity.normalized : transform.forward;

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

    protected virtual Vector3 ObstacleRays()
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

    protected virtual Vector3 SteerTowards(Vector3 vector)
    {
        Vector3 desiredVelocity = vector.normalized * maxBoundsSpeed;
        Vector3 steer = desiredVelocity - velocity;
        return Vector3.ClampMagnitude(steer, maxSteerForce);
    }
}