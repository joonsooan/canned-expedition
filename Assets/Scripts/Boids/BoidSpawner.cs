using UnityEngine;

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
            Vector3 randPos = transform.position + Random.insideUnitSphere * spawnRadius;

            Quaternion randRotation = Quaternion.Euler(
                Random.Range(0f, 360f),
                Random.Range(0f, 360f),
                Random.Range(0f, 360f)
            );

            Instantiate(boidPrefab, randPos, randRotation, boidParent.transform);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, spawnRadius);
    }
}
