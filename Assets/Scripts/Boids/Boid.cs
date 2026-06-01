using UnityEngine;

public class Boid : MonoBehaviour
{
    [Header("Boid Settings")]
    public float speed = 5f;
    public float rotationSpeed = 5f;
    public float neighborRadius = 5f;

    [Header("Behavior Weights")]
    public float separationWeight = 1f;
    public float alignmentWeight = 1f;
    public float cohesionWeight = 1f;

    private Vector3 separationVector;
    private Vector3 directionVector;
    private Vector3 cohesionPos;
    private Vector3 cohesionVector;
    private Vector3 moveDirection;
    private int neighborCount;

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

        transform.position += transform.forward * speed * Time.deltaTime;
    }

    private void InitializeBoid()
    {
        separationVector = Vector3.zero;
        directionVector = Vector3.zero;
        cohesionPos = transform.position;
        neighborCount = 0;
    }

    private void CalculateNeighborBoid(Collider col)
    {
        Vector3 separationDir = transform.position - col.transform.position;
        separationVector += separationDir.normalized; // 다른 Boid와의 간격
        directionVector += col.transform.forward; // 진행 방향
        cohesionPos += col.transform.position; // 응집도 (중심점)
        neighborCount++;
    }

    private void CalculateMoveVector()
    {
        directionVector /= neighborCount; // 진행 방향 계산
        cohesionVector = cohesionPos / neighborCount - transform.position; // 중심점으로 이동하는 벡터

        moveDirection = (separationVector * separationWeight) +
                        (directionVector * alignmentWeight) +
                        (cohesionVector * cohesionWeight);

        if (moveDirection != Vector3.zero)
        {
            Quaternion rotation = Quaternion.LookRotation(moveDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, rotation, rotationSpeed * Time.deltaTime);
        }
    }
}
