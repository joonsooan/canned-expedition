using System.Collections.Generic;
using UnityEngine;

public class BoidSystemManager : MonoBehaviour
{
    public static BoidSystemManager Instance { get; private set; }

    [Header("Simulation Mode")]
    public bool useGPUSimulation = false;
    public bool useSpatialHash = true;
    public float cellSize = 8f;

    [Header("Prefabs")]
    public GameObject preyPrefab;
    public GameObject predatorPrefab;

    [Header("Boundary Config")]
    public Vector3 boundaryCenter = Vector3.zero;
    public float boundaryRadius = 50f;
    public float boundaryWeight = 5f;

    [Header("GPU Resources")]
    public ComputeShader boidComputeShader;
    public Shader proceduralShader;

    // Structs matching compute shader
    public struct BoidGPUData
    {
        public Vector3 position;
        public Vector3 velocity;
        public Vector4 color;
        public float scale;
        public int isPredator;
    }

    public struct ObstacleData
    {
        public Vector3 position;
        public float radius;
    }

    // Properties for access by Boids
    public BoidSpatialHashGrid Grid => grid;

    private BoidSpatialHashGrid grid;
    
    // CPU Active Boids
    private readonly List<PreyBoid> activePrey = new List<PreyBoid>();
    private readonly List<PredatorBoid> activePredators = new List<PredatorBoid>();

    // GPU Compute Buffers
    private ComputeBuffer preyBuffer;
    private ComputeBuffer predatorBuffer;
    private ComputeBuffer obstacleBuffer;

    // CPU arrays mirroring GPU buffers
    private BoidGPUData[] preyData;
    private BoidGPUData[] predatorData;
    private ObstacleData[] obstacleData;

    // Extracted meshes and materials for procedural rendering
    private Mesh preyMesh;
    private Material preyProceduralMat;
    private Mesh predatorMesh;
    private Material predatorProceduralMat;

    // Cache parameters from prefabs
    private PreyBoid preyPrefabRef;
    private PredatorBoid predatorPrefabRef;
    private LayerMask obstacleMask;

    // GPU mode support
    private bool wasGPUMode = false;
    private int preyCount;
    private int predatorCount;
    private Boid dummyFollowTarget;

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

    public void InitializeSimulation(int numPrey, int numPredators, LayerMask obsMask)
    {
        preyCount = numPrey;
        predatorCount = numPredators;
        obstacleMask = obsMask;

        // Initialize Spatial Hash Grid
        grid = new BoidSpatialHashGrid(cellSize);

        // Prewarm CPU Pools
        if (BoidPool.Instance == null)
        {
            gameObject.AddComponent<BoidPool>();
        }
        BoidPool.Instance.Initialize(preyPrefab, predatorPrefab, transform, numPrey, numPredators);

        // Cache prefab reference scripts
        preyPrefabRef = preyPrefab.GetComponent<PreyBoid>();
        predatorPrefabRef = predatorPrefab.GetComponent<PredatorBoid>();

        // Extract meshes & build procedural materials
        ExtractMeshesAndMaterials();

        // Create dummy follow target for camera controller in GPU mode
        CreateDummyFollowTarget();

        // Initialize arrays
        preyData = new BoidGPUData[preyCount];
        predatorData = new BoidGPUData[predatorCount];

        // Collect Obstacles once
        CollectObstacles();

        // Spawn initial CPU boids
        SpawnInitialBoids();

        if (useGPUSimulation)
        {
            SwitchToGPU();
        }
        else
        {
            SwitchToCPU();
        }
    }

    private void OnDestroy()
    {
        ReleaseComputeBuffers();
    }

    private void Update()
    {
        // Handle simulation mode switching at runtime
        if (useGPUSimulation != wasGPUMode)
        {
            if (useGPUSimulation)
            {
                SwitchToGPU();
            }
            else
            {
                SwitchToCPU();
            }
        }

        if (useGPUSimulation)
        {
            RunGPUSimulation();
        }
        else
        {
            RunCPUSimulation();
        }
    }

    private void LateUpdate()
    {
        if (useGPUSimulation)
        {
            // Procedurally render GPU boids
            RenderGPUBoids();

            // Keep the dummy camera follower updated to follow one boid
            UpdateDummyFollowTarget();
        }
    }

    #region CPU Simulation

    private void RunCPUSimulation()
    {
        // Rebuild Spatial Hash Grid
        grid.Clear();
        
        int preyLen = activePrey.Count;
        for (int i = 0; i < preyLen; i++)
        {
            grid.InsertPrey(activePrey[i]);
        }

        int predLen = activePredators.Count;
        for (int i = 0; i < predLen; i++)
        {
            grid.InsertPredator(activePredators[i]);
        }
    }

    private void SwitchToCPU()
    {
        // Read data back from GPU to CPU GameObjects if we were in GPU mode
        if (wasGPUMode)
        {
            ReadGPUDataToCPU();
            ReleaseComputeBuffers();
        }

        // Activate CPU GameObjects
        foreach (var prey in activePrey) prey.gameObject.SetActive(true);
        foreach (var pred in activePredators) pred.gameObject.SetActive(true);

        // Hide dummy follow target
        if (dummyFollowTarget != null) dummyFollowTarget.gameObject.SetActive(false);

        wasGPUMode = false;
    }

    private void SpawnInitialBoids()
    {
        PredatorPreySpawner spawner = FindFirstObjectByType<PredatorPreySpawner>();
        Vector3 spawnCenter = spawner != null ? spawner.transform.position : Vector3.zero;
        float preyRadius = spawner != null ? spawner.preySpawnRadius : 15f;
        float predRadius = spawner != null ? spawner.predatorSpawnRadius : 20f;

        for (int i = 0; i < preyCount; i++)
        {
            Vector3 pos = spawnCenter + Random.insideUnitSphere * preyRadius;
            Quaternion rot = Quaternion.Euler(Random.Range(0f, 360f), Random.Range(0f, 360f), Random.Range(0f, 360f));
            PreyBoid boid = BoidPool.Instance.GetPrey(pos, rot);
            activePrey.Add(boid);
        }

        for (int i = 0; i < predatorCount; i++)
        {
            Vector3 pos = spawnCenter + Random.insideUnitSphere * predRadius;
            Quaternion rot = Quaternion.Euler(Random.Range(0f, 360f), Random.Range(0f, 360f), Random.Range(0f, 360f));
            PredatorBoid boid = BoidPool.Instance.GetPredator(pos, rot);
            activePredators.Add(boid);
        }
    }

    #endregion

    #region GPU Simulation

    private void RunGPUSimulation()
    {
        if (preyBuffer == null || predatorBuffer == null) return;

        // Update parameters
        boidComputeShader.SetInt("preyCount", preyCount);
        boidComputeShader.SetInt("predatorCount", predatorCount);
        boidComputeShader.SetInt("obstacleCount", obstacleData.Length);
        boidComputeShader.SetFloat("deltaTime", Time.deltaTime);

        // Bind Prey Settings
        boidComputeShader.SetFloat("preySpeed", preyPrefabRef.speed);
        boidComputeShader.SetFloat("preyRotationSpeed", preyPrefabRef.rotationSpeed);
        boidComputeShader.SetFloat("preyNeighborRadius", preyPrefabRef.neighborRadius);
        boidComputeShader.SetFloat("preySeparationWeight", preyPrefabRef.separationWeight);
        boidComputeShader.SetFloat("preyAlignmentWeight", preyPrefabRef.alignmentWeight);
        boidComputeShader.SetFloat("preyCohesionWeight", preyPrefabRef.cohesionWeight);
        boidComputeShader.SetFloat("preyMaxBoundsSpeed", preyPrefabRef.maxBoundsSpeed);
        boidComputeShader.SetFloat("preyMaxSteerForce", preyPrefabRef.maxSteerForce);
        boidComputeShader.SetFloat("predatorDetectRadius", preyPrefabRef.predatorDetectRadius);
        boidComputeShader.SetFloat("fleeWeight", preyPrefabRef.fleeWeight);

        // Bind Predator Settings
        boidComputeShader.SetFloat("predSpeed", predatorPrefabRef.speed);
        boidComputeShader.SetFloat("predRotationSpeed", predatorPrefabRef.rotationSpeed);
        boidComputeShader.SetFloat("predNeighborRadius", predatorPrefabRef.neighborRadius);
        boidComputeShader.SetFloat("predSeparationWeight", predatorPrefabRef.separationWeight);
        boidComputeShader.SetFloat("predMaxBoundsSpeed", predatorPrefabRef.maxBoundsSpeed);
        boidComputeShader.SetFloat("predMaxSteerForce", predatorPrefabRef.maxSteerForce);
        boidComputeShader.SetFloat("preyDetectRadius", predatorPrefabRef.preyDetectRadius);
        boidComputeShader.SetFloat("chaseWeight", predatorPrefabRef.chaseWeight);

        // Avoidance and boundary config
        boidComputeShader.SetFloat("avoidCollisionWeight", preyPrefabRef.avoidCollisionWeight);
        boidComputeShader.SetFloat("boundsRadius", preyPrefabRef.boundsRadius);
        boidComputeShader.SetFloat("collisionAvoidDst", preyPrefabRef.collisionAvoidDst);
        boidComputeShader.SetVector("boundaryCenter", boundaryCenter);
        boidComputeShader.SetFloat("boundaryRadius", boundaryRadius);
        boidComputeShader.SetFloat("boundaryWeight", boundaryWeight);

        // 1. Dispatch Prey Update Kernel
        int updatePreyKernel = boidComputeShader.FindKernel("UpdatePrey");
        boidComputeShader.SetBuffer(updatePreyKernel, "PreyBuffer", preyBuffer);
        boidComputeShader.SetBuffer(updatePreyKernel, "PredatorBuffer", predatorBuffer);
        boidComputeShader.SetBuffer(updatePreyKernel, "Obstacles", obstacleBuffer);
        
        int preyThreadGroups = Mathf.CeilToInt((float)preyCount / 64f);
        boidComputeShader.Dispatch(updatePreyKernel, preyThreadGroups, 1, 1);

        // 2. Dispatch Predator Update Kernel
        int updatePredatorsKernel = boidComputeShader.FindKernel("UpdatePredators");
        boidComputeShader.SetBuffer(updatePredatorsKernel, "PredatorBufferRW", predatorBuffer);
        boidComputeShader.SetBuffer(updatePredatorsKernel, "PreyBufferRO", preyBuffer);
        boidComputeShader.SetBuffer(updatePredatorsKernel, "Obstacles", obstacleBuffer);

        int predatorThreadGroups = Mathf.CeilToInt((float)predatorCount / 64f);
        boidComputeShader.Dispatch(updatePredatorsKernel, predatorThreadGroups, 1, 1);
    }

    private void RenderGPUBoids()
    {
        if (preyMesh != null && preyProceduralMat != null && preyBuffer != null)
        {
            preyProceduralMat.SetBuffer("BoidBuffer", preyBuffer);
            Graphics.DrawMeshInstancedProcedural(preyMesh, 0, preyProceduralMat, new Bounds(boundaryCenter, Vector3.one * boundaryRadius * 2f), preyCount, null, UnityEngine.Rendering.ShadowCastingMode.On, true);
        }

        if (predatorMesh != null && predatorProceduralMat != null && predatorBuffer != null)
        {
            predatorProceduralMat.SetBuffer("BoidBuffer", predatorBuffer);
            Graphics.DrawMeshInstancedProcedural(predatorMesh, 0, predatorProceduralMat, new Bounds(boundaryCenter, Vector3.one * boundaryRadius * 2f), predatorCount, null, UnityEngine.Rendering.ShadowCastingMode.On, true);
        }
    }

    private void SwitchToGPU()
    {
        // Sync current CPU boid state to GPU structures
        WriteCPUDataToGPUArray();

        // Initialize GPU Compute Buffers
        ReleaseComputeBuffers();

        preyBuffer = new ComputeBuffer(preyCount, sizeof(float) * 12 + sizeof(int));
        preyBuffer.SetData(preyData);

        predatorBuffer = new ComputeBuffer(predatorCount, sizeof(float) * 12 + sizeof(int));
        predatorBuffer.SetData(predatorData);

        if (obstacleData.Length > 0)
        {
            obstacleBuffer = new ComputeBuffer(obstacleData.Length, sizeof(float) * 4);
            obstacleBuffer.SetData(obstacleData);
        }
        else
        {
            // Dummy buffer to avoid null crash
            obstacleBuffer = new ComputeBuffer(1, sizeof(float) * 4);
        }

        // Deactivate CPU GameObjects
        foreach (var prey in activePrey) prey.gameObject.SetActive(false);
        foreach (var pred in activePredators) pred.gameObject.SetActive(false);

        // Show and place dummy follow target
        if (dummyFollowTarget != null)
        {
            dummyFollowTarget.gameObject.SetActive(true);
            UpdateDummyFollowTarget();
        }

        wasGPUMode = true;
    }

    private void WriteCPUDataToGPUArray()
    {
        for (int i = 0; i < preyCount; i++)
        {
            PreyBoid b = activePrey[i];
            preyData[i] = new BoidGPUData
            {
                position = b.transform.position,
                velocity = b.transform.forward * b.speed, // Initial velocity estimate
                color = GetBoidColor(b),
                scale = b.transform.localScale.z,
                isPredator = 0
            };
        }

        for (int i = 0; i < predatorCount; i++)
        {
            PredatorBoid b = activePredators[i];
            predatorData[i] = new BoidGPUData
            {
                position = b.transform.position,
                velocity = b.transform.forward * b.speed,
                color = GetBoidColor(b),
                scale = b.transform.localScale.z,
                isPredator = 1
            };
        }
    }

    private void ReadGPUDataToCPU()
    {
        if (preyBuffer == null || predatorBuffer == null) return;

        preyBuffer.GetData(preyData);
        predatorBuffer.GetData(predatorData);

        for (int i = 0; i < preyCount; i++)
        {
            PreyBoid b = activePrey[i];
            b.transform.position = preyData[i].position;
            if (preyData[i].velocity != Vector3.zero)
            {
                b.transform.rotation = Quaternion.LookRotation(preyData[i].velocity);
            }
        }

        for (int i = 0; i < predatorCount; i++)
        {
            PredatorBoid b = activePredators[i];
            b.transform.position = predatorData[i].position;
            if (predatorData[i].velocity != Vector3.zero)
            {
                b.transform.rotation = Quaternion.LookRotation(predatorData[i].velocity);
            }
        }
    }

    private void ReleaseComputeBuffers()
    {
        if (preyBuffer != null) { preyBuffer.Release(); preyBuffer = null; }
        if (predatorBuffer != null) { predatorBuffer.Release(); predatorBuffer = null; }
        if (obstacleBuffer != null) { obstacleBuffer.Release(); obstacleBuffer = null; }
    }

    #endregion

    #region Dummy Follow Target for Camera

    private void CreateDummyFollowTarget()
    {
        GameObject go = new GameObject("BoidDummyFollowTarget");
        go.transform.parent = transform;
        dummyFollowTarget = go.AddComponent<Boid>();
        
        // Disable Update on the dummy follow target, we will drive its position manually
        dummyFollowTarget.enabled = false;
        go.SetActive(false);
    }

    private void UpdateDummyFollowTarget()
    {
        if (dummyFollowTarget == null || preyBuffer == null) return;

        // Retrieve position of the first prey boid from the GPU
        preyBuffer.GetData(preyData);
        
        if (preyData.Length > 0)
        {
            dummyFollowTarget.transform.position = preyData[0].position;
            if (preyData[0].velocity != Vector3.zero)
            {
                dummyFollowTarget.transform.rotation = Quaternion.LookRotation(preyData[0].velocity);
            }
        }
    }

    #endregion

    #region Helpers

    private void ExtractMeshesAndMaterials()
    {
        // Extract from Prey Prefab
        GetMeshAndMaterialFromPrefab(preyPrefab, out preyMesh, out var preyOrigMat);
        preyProceduralMat = new Material(proceduralShader);
        if (preyOrigMat != null)
        {
            if (preyOrigMat.HasProperty("_BaseColor"))
                preyProceduralMat.SetColor("_Color", preyOrigMat.GetColor("_BaseColor"));
            else if (preyOrigMat.HasProperty("_Color"))
                preyProceduralMat.SetColor("_Color", preyOrigMat.GetColor("_Color"));
        }

        // Extract from Predator Prefab
        GetMeshAndMaterialFromPrefab(predatorPrefab, out predatorMesh, out var predOrigMat);
        predatorProceduralMat = new Material(proceduralShader);
        if (predOrigMat != null)
        {
            if (predOrigMat.HasProperty("_BaseColor"))
                predatorProceduralMat.SetColor("_Color", predOrigMat.GetColor("_BaseColor"));
            else if (predOrigMat.HasProperty("_Color"))
                predatorProceduralMat.SetColor("_Color", predOrigMat.GetColor("_Color"));
        }
    }

    private void GetMeshAndMaterialFromPrefab(GameObject prefab, out Mesh mesh, out Material material)
    {
        mesh = null;
        material = null;
        if (prefab == null) return;

        MeshFilter filter = prefab.GetComponentInChildren<MeshFilter>();
        if (filter != null) mesh = filter.sharedMesh;

        Renderer renderer = prefab.GetComponentInChildren<Renderer>();
        if (renderer != null) material = renderer.sharedMaterial;
    }

    private Color GetBoidColor(Boid boid)
    {
        // Read color from property block or renderer
        Renderer r = boid.GetComponentInChildren<Renderer>();
        if (r != null)
        {
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            r.GetPropertyBlock(block);
            if (block.HasColor("_BaseColor")) return block.GetColor("_BaseColor");
            if (block.HasColor("_Color")) return block.GetColor("_Color");
            if (r.sharedMaterial != null)
            {
                if (r.sharedMaterial.HasProperty("_BaseColor")) return r.sharedMaterial.GetColor("_BaseColor");
                if (r.sharedMaterial.HasProperty("_Color")) return r.sharedMaterial.GetColor("_Color");
            }
        }
        return boid.useRandomColor ? Color.white : boid.boidColor;
    }

    private void CollectObstacles()
    {
        Collider[] colliders = FindObjectsByType<Collider>(FindObjectsSortMode.None);
        List<ObstacleData> obsList = new List<ObstacleData>();
        
        foreach (var col in colliders)
        {
            // Ignore triggers and check if layer matches the obstacle mask
            if (!col.isTrigger && ((1 << col.gameObject.layer) & obstacleMask.value) != 0)
            {
                ObstacleData obs = new ObstacleData
                {
                    position = col.bounds.center,
                    radius = col.bounds.extents.magnitude // approximate radius of the collider bounds
                };
                obsList.Add(obs);
            }
        }

        obstacleData = obsList.ToArray();
    }

    private void OnDrawGizmosSelected()
    {
        // Draw Boundary
        Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
        Gizmos.DrawWireSphere(boundaryCenter, boundaryRadius);

        // Draw Obstacles in scene view for debug
        if (obstacleData != null)
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.4f);
            foreach (var obs in obstacleData)
            {
                Gizmos.DrawWireSphere(obs.position, obs.radius);
            }
        }
    }

    #endregion
}
