using UnityEngine;
using System.Collections.Generic;

public class Boid : MonoBehaviour
{
    public static readonly List<Boid> ActiveBoids = new List<Boid>();

    [Header("Boid Settings")]
    public float speed = 5f;
    public float rotationSpeed = 5f;
    public float neighborRadius = 5f;
    public bool setRandomColor = true;

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
    private float neighborSqrRadius;
    private Transform myTransform;

    [HideInInspector] public Vector3 jobSeparation;
    [HideInInspector] public Vector3 jobAlignment;
    [HideInInspector] public Vector3 jobCohesion;
    [HideInInspector] public int jobNeighborCount;

    [HideInInspector] public Vector3 Position;
    [HideInInspector] public Vector3 ForwardVec;

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
        neighborSqrRadius = neighborRadius * neighborRadius;
        Position = myTransform.position;
        ForwardVec = myTransform.forward;

        velocity = ForwardVec * speed;
        if (setRandomColor) SetRandomColor();
    }

    void Update()
    {
        Position = myTransform.position;
        ForwardVec = myTransform.forward;

        InitializeBoid();

        // Read forces calculated by the multithreaded Burst job
        separationVector = jobSeparation;
        directionVector = jobAlignment;
        cohesionPos = jobCohesion;
        neighborCount = jobNeighborCount;

        ApplyNewBehavior();
        HandleObstacleAvoiding();
        UpdateMovement();
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
        RaycastHit hit;
        Vector3 moveDir = velocity != Vector3.zero ? velocity.normalized : ForwardVec;

        if (Physics.SphereCast(Position, boundsRadius, moveDir, out hit, collisionAvoidDst, obstacleMask))
        {
            float distanceToObstacle = hit.distance;
            Vector3 collisionAvoidDir = ObstacleRays();
            float proximityFactor = 1f - (distanceToObstacle / collisionAvoidDst);
            Vector3 collisionAvoidForce = SteerTowards(collisionAvoidDir) * avoidCollisionWeight * (1f + proximityFactor * 2f);

            acceleration += collisionAvoidForce;
        }
    }

    private void UpdateMovement()
    {
        velocity += acceleration * Time.deltaTime;

        float sqrSpeed = velocity.sqrMagnitude;
        if (sqrSpeed > 0f)
        {
            float currentSpeed = Mathf.Sqrt(sqrSpeed);
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



    private Vector3 ObstacleRays()
    {
        Vector3[] rayDirections = BoidHelper.directions;
        Quaternion lookRotation = velocity != Vector3.zero ? Quaternion.LookRotation(velocity.normalized) : transform.rotation;

        for (int i = 0; i < rayDirections.Length; i++)
        {
            Vector3 dir = lookRotation * rayDirections[i];
            Ray ray = new Ray(Position, dir);
            // 정면과 가장 가까운 방향부터 검사하여 충돌이 없는 첫 번째 방향을 반환
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