using UnityEngine;

public class PredatorPreySpawner : MonoBehaviour
{
    [Header("Prey Spawn Settings")]
    public GameObject preyPrefab;
    public int preySpawnCount = 100;
    public float preySpawnRadius = 15f;

    [Header("Predator Spawn Settings")]
    public GameObject predatorPrefab;
    public int predatorSpawnCount = 5;
    public float predatorSpawnRadius = 20f;

    [Header("Parent Settings")]
    public GameObject boidParent;

    void Start()
    {
        SpawnBoids();
    }

    private void SpawnBoids()
    {
        Transform parent = boidParent != null ? boidParent.transform : transform;

        for (int i = 0; i < preySpawnCount; i++)
        {
            Vector3 randPos = transform.position + Random.insideUnitSphere * preySpawnRadius;
            Quaternion randRotation = Quaternion.Euler(Random.Range(0f, 360f), Random.Range(0f, 360f), Random.Range(0f, 360f));
            Instantiate(preyPrefab, randPos, randRotation, parent);
        }

        for (int i = 0; i < predatorSpawnCount; i++)
        {
            Vector3 randPos = transform.position + Random.insideUnitSphere * predatorSpawnRadius;
            Quaternion randRotation = Quaternion.Euler(Random.Range(0f, 360f), Random.Range(0f, 360f), Random.Range(0f, 360f));
            Instantiate(predatorPrefab, randPos, randRotation, parent);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, preySpawnRadius);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, predatorSpawnRadius);
    }
}
