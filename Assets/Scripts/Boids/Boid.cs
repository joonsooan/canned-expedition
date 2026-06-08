using UnityEngine;
using System.Collections.Generic;

public class Boid : MonoBehaviour
{
    public static readonly List<Boid> ActiveBoids = new List<Boid>();

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
    private Transform myTransform;
    private Collider[] neighborBuffer = new Collider[64];

    public Vector3 Position { get; private set; }
    public Vector3 ForwardVec { get; private set; }

    void Awake()
    {
        myTransform = transform;
    }

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
        Position = myTransform.position;
        ForwardVec = myTransform.forward;

        velocity = ForwardVec * speed;
        SetRandomColor();
    }

    void Update()
    {
        Position = myTransform.position;
        ForwardVec = myTransform.forward;

        InitializeBoid();
        FindNeighbors();
        ApplyNewBehavior();
        HandleObstacleAvoiding();
        UpdateMovement();
    }

    private void FindNeighbors()
    {
        int count = Physics.OverlapSphereNonAlloc(Position, neighborRadius, neighborBuffer);

        for (int i = 0; i < count; i++)
        {
            Collider col = neighborBuffer[i];
            if (col.gameObject != gameObject && col.CompareTag("Boid"))
            {
                Boid neighbor = col.GetComponent<Boid>();
                CalculateNeighborBoid(neighbor);
            }
        }
    }

    private void ApplyNewBehavior()
    {
        if (neighborCount > 0)
        {
            CalculateMoveVector();
        }
        else
        {
            acceleration += ForwardVec * 0.5f;
        }
    }

    private void HandleObstacleAvoiding()
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

    private void UpdateMovement()
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
            myTransform.rotation = Quaternion.Slerp(myTransform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        myTransform.position += velocity * Time.deltaTime;
    }

    private void SetRandomColor()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers != null && renderers.Length > 0)
        {
            MaterialPropertyBlock props = new MaterialPropertyBlock();
            Color pastelColor = Color.HSVToRGB(Random.Range(0f, 1f), Random.Range(0.3f, 0.5f), Random.Range(0.8f, 1.0f));
            props.SetColor("_BaseColor", pastelColor);
            props.SetColor("_Color", pastelColor);

            foreach (Renderer r in renderers)
            {
                r.SetPropertyBlock(props);
            }
        }
    }

    private void InitializeBoid()
    {
        separationVector = Vector3.zero;
        directionVector = Vector3.zero;
        cohesionPos = Vector3.zero;
        acceleration = Vector3.zero;
        neighborCount = 0;
    }

    private void CalculateNeighborBoid(Boid neighbor)
    {
        Vector3 separationDir = Position - neighbor.Position;
        float distance = separationDir.magnitude;

        separationVector += separationDir.normalized * (1f - (distance / neighborRadius));
        directionVector += neighbor.ForwardVec;
        cohesionPos += neighbor.Position;
        neighborCount++;
    }

    private void CalculateMoveVector()
    {
        if (separationVector != Vector3.zero) separationVector = separationVector.normalized;
        if (directionVector != Vector3.zero) directionVector = directionVector.normalized;

        Vector3 averageCohesionPos = cohesionPos / neighborCount;
        cohesionVector = averageCohesionPos - Position;
        if (cohesionVector != Vector3.zero) cohesionVector = cohesionVector.normalized;

        acceleration += (separationVector * separationWeight) +
                        (directionVector * alignmentWeight) +
                        (cohesionVector * cohesionWeight);
    }

    private bool IsHeadingForCollision()
    {
        RaycastHit hit;
        Vector3 moveDir = velocity != Vector3.zero ? velocity.normalized : ForwardVec;

        if (Physics.SphereCast(Position, boundsRadius, moveDir, out hit, collisionAvoidDst, obstacleMask))
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
            moveDir = ForwardVec;
        }

        if (Physics.SphereCast(Position, boundsRadius, moveDir, out hit, collisionAvoidDst, obstacleMask))
        {
            distance = hit.distance;
            avoidDir = ObstacleRays();
            return true;
        }

        avoidDir = ForwardVec;
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
            Ray ray = new Ray(Position, dir);
            if (!Physics.SphereCast(ray, boundsRadius, collisionAvoidDst, obstacleMask))
            {
                return dir;
            }
        }

        return ForwardVec;
    }

    private Vector3 SteerTowards(Vector3 vector)
    {
        Vector3 desiredVelocity = vector.normalized * maxBoundsSpeed;
        Vector3 steer = desiredVelocity - velocity;
        return Vector3.ClampMagnitude(steer, maxSteerForce);
    }
}