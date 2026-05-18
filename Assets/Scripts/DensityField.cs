using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

using Unity.Collections;

public enum DensityFieldMode
{
    Sphere,
    Terrain
}

public class DensityField : MonoBehaviour
{
    private static readonly int GizmoBufferProperty = Shader.PropertyToID("_GizmoBuffer");
    private const float IsoValue = 0f;

    private readonly List<BrushData> brushes = new List<BrushData>();
    private readonly List<BrushData> pendingBrushes = new List<BrushData>();
    private readonly ChunkManager chunkManager = new ChunkManager();

    [Header("Chunk Rendering")]
    [SerializeField] private Transform chunkTarget;
    [Range(1, 16)]
    [SerializeField] private int chunkRenderDistance = 3;

    [Header("Field")]
    [SerializeField] private DensityFieldMode fieldMode = DensityFieldMode.Sphere;
    [Tooltip("격자 한 변의 샘플 수")]
    [Range(2, 256)]
    [SerializeField] private int resolution = 16;
    [Tooltip("셀 간격 (월드 단위)")]
    [Range(0.01f, 20f)]
    [SerializeField] private float spacing = 1f;
    [SerializeField] private float3 fieldOffset;
    [SerializeField] private SphereDensityFieldGenerator generator;
    [SerializeField] private TerrainDensityFieldGenerator terrainGenerator;
    [Tooltip("밀도·메시 갱신 최소 간격(초)")]
    [Range(0.02f, 1000f)]
    [SerializeField] private float refreshRate = 0.3f;

    [Header("GPU Gizmo")]
    [SerializeField] private bool drawDensityGizmo = true;
    [SerializeField] private Mesh gizmoMesh;
    [SerializeField] private Material gizmoMaterial;
    [Range(0.001f, 2f)]
    [SerializeField] private float gizmoSize = 0.1f;
    [Range(0f, 1f)]
    [SerializeField] private float gizmoAlpha = 1f;

    [Header("Iso surface")]
    [SerializeField] private Material surfaceMaterial;
    [SerializeField] private MeshCollider surfaceCollider;

    [Header("Editor")]
    [SerializeField] private float editorGizmoRadius = 0.05f;

    private NativeArray<float> densities;
    private ComputeBuffer gizmoBuffer;
    private ComputeBuffer argsBuffer;
    private Bounds bounds;
    private float timer;
    private Mesh surfaceMesh;
    private bool isDirty = true;
    private bool requiresFullRefresh = true;
    private SphereDensityFieldGenerator subscribedGenerator;
    private TerrainDensityFieldGenerator subscribedTerrainGenerator;

    public NativeArray<float> Densities => densities;
    public int Resolution => resolution;
    public float Spacing => spacing;
    public float3 FieldOffset => fieldOffset;
    public float SphereRadius => generator != null ? generator.SphereRadius : 0f;
    public float3 WorldOrigin => (float3)transform.position + fieldOffset;
    public MeshCollider SurfaceCollider => surfaceCollider;

    public void SetChunkLoadTarget(Transform target)
    {
        chunkManager.SetLoadTarget(target);
        if (!densities.IsCreated || surfaceMesh == null)
            return;

        if (chunkManager.UpdateActiveChunks(true, brushes, pendingBrushes))
        {
            SyncSurfaceCollider();
            UpdateBounds();
        }
    }

    public bool TryGetChunkBounds(float3 worldPosition, out Bounds chunkBounds)
    {
        return chunkManager.TryGetChunkBounds(worldPosition, out chunkBounds);
    }

    private void Awake()
    {
        surfaceMesh = new Mesh { name = "SurfaceMesh" };
    }

    private void OnEnable()
    {
        SubscribeGeneratorChanges();
    }

    private void OnDisable()
    {
        UnsubscribeGeneratorChanges();
    }

    private void Start()
    {
        if (chunkTarget != null)
            chunkManager.SetLoadTarget(chunkTarget);
        InitializeField();
        InitializeGizmo();
    }

    private void OnValidate()
    {
        SubscribeGeneratorChanges();
        chunkManager.SetLoadRadius(chunkRenderDistance);
    }

    private void Update()
    {
        if (!densities.IsCreated) return;

        bool hadPendingBrushes = pendingBrushes.Count > 0;
        if (chunkManager.UpdateActiveChunks(false, brushes, pendingBrushes))
        {
            if (hadPendingBrushes)
            {
                pendingBrushes.Clear();
                timer = 0f;
                isDirty = false;
                if (drawDensityGizmo) UpdateGizmoBuffer();
            }
            SyncSurfaceCollider();
            UpdateBounds();
        }

        timer += Time.deltaTime;

        if (isDirty && timer >= refreshRate)
        {
            if (requiresFullRefresh)
                RefreshFieldContents();
            else
                RefreshPendingBrushes();
            if (drawDensityGizmo) UpdateGizmoBuffer();
            timer = 0f;
            isDirty = false;
        }

        if (drawDensityGizmo && gizmoBuffer != null && argsBuffer != null && gizmoMesh != null &&
            gizmoMaterial != null)
        {
            gizmoMaterial.SetFloat("_Size", gizmoSize);
            gizmoMaterial.SetFloat("_Alpha", gizmoAlpha);
            Graphics.DrawMeshInstancedIndirect(gizmoMesh, 0, gizmoMaterial, bounds, argsBuffer);
        }

        if (surfaceMesh != null && surfaceMaterial != null && surfaceMesh.vertexCount > 0)
        {
            Graphics.DrawMesh(surfaceMesh, Matrix4x4.identity, surfaceMaterial, gameObject.layer);
        }
    }

    public void InitializeField()
    {
        timer = 0f;
        if (densities.IsCreated) densities.Dispose();
        densities = new NativeArray<float>(resolution * resolution * resolution, Allocator.Persistent);
        chunkManager.Initialize(densities, resolution, spacing, WorldOrigin, surfaceMesh);
        chunkManager.SetLoadRadius(chunkRenderDistance);
        RefreshFieldContents();
        isDirty = false;
    }

    private void SubscribeGeneratorChanges()
    {
        if (subscribedGenerator == generator && subscribedTerrainGenerator == terrainGenerator)
            return;

        UnsubscribeGeneratorChanges();

        subscribedGenerator = generator;
        subscribedTerrainGenerator = terrainGenerator;

        if (subscribedGenerator != null)
            subscribedGenerator.Changed += HandleGeneratorChanged;
        if (subscribedTerrainGenerator != null)
            subscribedTerrainGenerator.Changed += HandleGeneratorChanged;
    }

    private void UnsubscribeGeneratorChanges()
    {
        if (subscribedGenerator != null)
            subscribedGenerator.Changed -= HandleGeneratorChanged;
        if (subscribedTerrainGenerator != null)
            subscribedTerrainGenerator.Changed -= HandleGeneratorChanged;

        subscribedGenerator = null;
        subscribedTerrainGenerator = null;
    }

    private void HandleGeneratorChanged()
    {
        if (!Application.isPlaying || surfaceMesh == null)
        {
            isDirty = true;
            requiresFullRefresh = true;
            return;
        }

        RefreshFieldContents();
        UpdateGizmoBuffer();
        timer = 0f;
        isDirty = false;
    }

    public int GetIndex(int x, int y, int z)
    {
        return x + resolution * y + resolution * resolution * z;
    }

    public static float3 GetPosition(int index, int resolution, float spacing, float3 origin)
    {
        int z = index / (resolution * resolution);
        int rem = index % (resolution * resolution);
        int y = rem / resolution;
        int x = rem % resolution;
        float3 centerCell = new float3(resolution / 2f, resolution / 2f, resolution / 2f);
        return (new float3(x, y, z) - centerCell) * spacing + origin;
    }

    private void RefreshFieldContents()
    {
        brushes.Clear();
        pendingBrushes.Clear();

        float3 origin = InitializeFieldAndDensity();
        ApplyGenerator(origin);
        ApplyBrushes();
        pendingBrushes.Clear();
        requiresFullRefresh = false;

        chunkManager.Initialize(densities, resolution, spacing, origin, surfaceMesh);
        chunkManager.SetLoadRadius(chunkRenderDistance);
        chunkManager.RebuildForFullField(brushes);
        SyncSurfaceCollider();
        UpdateBounds();
    }

    private void RefreshPendingBrushes()
    {
        if (pendingBrushes.Count == 0) return;

        chunkManager.RefreshPendingBrushes(brushes);
        pendingBrushes.Clear();
        SyncSurfaceCollider();
        UpdateBounds();
    }

    private float3 InitializeFieldAndDensity()
    {
        float3 origin = (float3)transform.position + fieldOffset;
        for (int i = 0; i < densities.Length; i++)
        {
            densities[i] = 0f;
        }

        return origin;
    }

    private void ApplyGenerator(float3 origin)
    {
        switch (fieldMode)
        {
            case DensityFieldMode.Sphere:
                if (generator != null)
                    generator.Apply(densities, origin, resolution, spacing);
                break;
            case DensityFieldMode.Terrain:
                if (terrainGenerator != null)
                    terrainGenerator.Apply(densities, origin, resolution, spacing);
                break;
        }
    }

    private void UpdateBounds()
    {
        float halfExtent = (resolution - 1) * spacing * 0.5f + gizmoSize;
        float3 boundsCenter = (float3)transform.position + fieldOffset;
        bounds = new Bounds((Vector3)boundsCenter, Vector3.one * (halfExtent * 2f));
    }

    private void InitializeGizmo()
    {
        if (!densities.IsCreated || densities.Length == 0) return;

        int count = densities.Length;
        UpdateBounds();

        gizmoBuffer?.Release();
        gizmoBuffer = new ComputeBuffer(count, sizeof(float) * 4);
        UpdateGizmoBuffer();

        if (gizmoMaterial != null)
        {
            gizmoMaterial.EnableKeyword("PROCEDURAL_INSTANCING_ON");
            gizmoMaterial.SetBuffer(GizmoBufferProperty, gizmoBuffer);
            gizmoMaterial.SetFloat("_Size", gizmoSize);
            gizmoMaterial.SetFloat("_Alpha", gizmoAlpha);
        }

        argsBuffer?.Release();
        uint[] args = new uint[5];
        if (gizmoMesh != null)
        {
            args[0] = gizmoMesh.GetIndexCount(0);
            args[1] = (uint)count;
            args[2] = gizmoMesh.GetIndexStart(0);
            args[3] = gizmoMesh.GetBaseVertex(0);
        }

        argsBuffer = new ComputeBuffer(1, sizeof(uint) * args.Length, ComputeBufferType.IndirectArguments);
        argsBuffer.SetData(args);
    }

    private void OnDrawGizmos()
    {
        if (!drawDensityGizmo || !densities.IsCreated) return;

        float3 origin = WorldOrigin;
        for (int i = 0; i < densities.Length; i++)
        {
            float d = densities[i];
            float3 pos = GetPosition(i, resolution, spacing, origin);
            Gizmos.color = d <= 0f ? Color.blue : Color.red;
            Gizmos.DrawSphere((Vector3)pos, editorGizmoRadius);
        }
    }

    private void OnDestroy()
    {
        if (densities.IsCreated) densities.Dispose();
        if (surfaceCollider != null)
            surfaceCollider.sharedMesh = null;
        gizmoBuffer?.Release();
        argsBuffer?.Release();
        if (surfaceMesh != null)
            Destroy(surfaceMesh);
        chunkManager.Dispose();
    }

    private void SyncSurfaceCollider()
    {
        if (surfaceCollider == null)
        {
            Debug.LogWarning("Mesh Collider not assigned");
            return;
        }

        surfaceCollider.sharedMesh = null;
        if (surfaceMesh != null && surfaceMesh.vertexCount > 0)
            surfaceCollider.sharedMesh = surfaceMesh;
    }

    public void AddBrush(float3 center, float radius, float strength, BrushType type)
    {
        BrushData brush = new BrushData
        {
            center = center,
            radius = radius,
            strength = strength,
            type = type
        };

        if (!chunkManager.MarkBrushChunksDirty(brush))
            return;

        brushes.Add(brush);
        pendingBrushes.Add(brush);
        isDirty = true;
    }

    private void ApplyBrushes()
    {
        ApplyBrushes(brushes);
    }

    private void ApplyBrushes(List<BrushData> source)
    {
        if (source.Count == 0) return;
        float3 origin = WorldOrigin;

        for (int i = 0; i < densities.Length; i++)
        {
            float3 pos = GetPosition(i, resolution, spacing, origin);
            float density = densities[i];
            foreach (var brush in source)
            {
                density = SdfBrush.Apply(density, pos, brush);
            }
            densities[i] = density;
        }
    }

    private void UpdateGizmoBuffer()
    {
        if (gizmoBuffer != null && densities.IsCreated)
        {
            var gizmoData = new NativeArray<float4>(densities.Length, Allocator.Temp);
            float3 origin = WorldOrigin;
            for (int i = 0; i < densities.Length; i++)
            {
                float3 pos = GetPosition(i, resolution, spacing, origin);
                gizmoData[i] = new float4(pos, densities[i]);
            }
            gizmoBuffer.SetData(gizmoData);
            gizmoData.Dispose();
        }
    }
}
