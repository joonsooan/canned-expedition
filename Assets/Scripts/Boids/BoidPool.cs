using System.Collections.Generic;
using UnityEngine;

public class BoidPool : MonoBehaviour
{
    public static BoidPool Instance { get; private set; }

    private readonly Queue<PreyBoid> preyPool = new Queue<PreyBoid>();
    private readonly Queue<PredatorBoid> predatorPool = new Queue<PredatorBoid>();

    private GameObject preyPrefab;
    private GameObject predatorPrefab;
    private Transform parentTransform;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void Initialize(GameObject preyPrefab, GameObject predatorPrefab, Transform parent, int preyCount, int predatorCount)
    {
        this.preyPrefab = preyPrefab;
        this.predatorPrefab = predatorPrefab;
        this.parentTransform = parent;

        // Clear existing just in case
        preyPool.Clear();
        predatorPool.Clear();

        // Prewarm Prey
        for (int i = 0; i < preyCount; i++)
        {
            PreyBoid boid = CreateNewPrey();
            boid.gameObject.SetActive(false);
            preyPool.Enqueue(boid);
        }

        // Prewarm Predators
        for (int i = 0; i < predatorCount; i++)
        {
            PredatorBoid boid = CreateNewPredator();
            boid.gameObject.SetActive(false);
            predatorPool.Enqueue(boid);
        }
    }

    private PreyBoid CreateNewPrey()
    {
        GameObject go = Instantiate(preyPrefab, parentTransform);
        PreyBoid boid = go.GetComponent<PreyBoid>();
        if (boid == null)
        {
            boid = go.AddComponent<PreyBoid>();
        }
        return boid;
    }

    private PredatorBoid CreateNewPredator()
    {
        GameObject go = Instantiate(predatorPrefab, parentTransform);
        PredatorBoid boid = go.GetComponent<PredatorBoid>();
        if (boid == null)
        {
            boid = go.AddComponent<PredatorBoid>();
        }
        return boid;
    }

    public PreyBoid GetPrey(Vector3 position, Quaternion rotation)
    {
        PreyBoid boid = preyPool.Count > 0 ? preyPool.Dequeue() : CreateNewPrey();
        boid.transform.SetPositionAndRotation(position, rotation);
        boid.gameObject.SetActive(true);
        return boid;
    }

    public PredatorBoid GetPredator(Vector3 position, Quaternion rotation)
    {
        PredatorBoid boid = predatorPool.Count > 0 ? predatorPool.Dequeue() : CreateNewPredator();
        boid.transform.SetPositionAndRotation(position, rotation);
        boid.gameObject.SetActive(true);
        return boid;
    }

    public void ReturnPrey(PreyBoid prey)
    {
        if (prey != null)
        {
            prey.gameObject.SetActive(false);
            preyPool.Enqueue(prey);
        }
    }

    public void ReturnPredator(PredatorBoid predator)
    {
        if (predator != null)
        {
            predator.gameObject.SetActive(false);
            predatorPool.Enqueue(predator);
        }
    }
}
