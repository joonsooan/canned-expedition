using Unity.Mathematics;
using UnityEngine;

public struct FieldData
{
    public float3 position;
    public float density;
}

public enum DensityFieldMode
{
    Sphere,
    Terrain
}

public class DensityField : MonoBehaviour
{
    private static readonly int GizmoBufferProperty = Shader.PropertyToID("_GizmoBuffer");
    private const float IsoValue = 0f;

    [Header("Field")]
    [SerializeField] private DensityFieldMode fieldMode = DensityFieldMode.Sphere;
    [Tooltip("격자 한 변의 샘플 수")]
    [Range(2, 128)]
    [SerializeField] private int resolution = 16;
    [Tooltip("셀 간격 (월드 단위)")]
    [Range(0.01f, 20f)]
    [SerializeField] private float spacing = 1f;
    [SerializeField] private float3 fieldOffset;
    [SerializeField] private SphereDensityFieldGenerator generator;
    [SerializeField] private TerrainDensityFieldGenerator terrainGenerator;
    [Tooltip("밀도·메시 갱신 최소 간격(초)")]
    [Range(0.02f, 5f)]
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

    [Header("Editor")]
    [Range(0.001f, 1f)]
    [SerializeField] private float editorGizmoRadius = 0.05f;

    private FieldData[] fieldData;
    private ComputeBuffer gizmoBuffer;
    private ComputeBuffer argsBuffer;
    private Bounds bounds;
    private float timer;
    private Mesh isoSurfaceMesh;

    public FieldData[] FieldData => fieldData;
    public int Resolution => resolution;
    public float Spacing => spacing;
    public float3 FieldOffset => fieldOffset;
    public float SphereRadius => generator != null ? generator.SphereRadius : 0f;
    public float3 WorldOrigin => (float3)transform.position + fieldOffset;

    private void Awake()
    {
        isoSurfaceMesh = new Mesh { name = "IsoSurface" };
    }

    private void Start()
    {
        InitializeField();
        InitializeGizmo();
    }

    private void Update()
    {
        if (fieldData == null) return;

        if (timer > refreshRate)
        {
            RefreshFieldContents();
            if (gizmoBuffer != null)
                gizmoBuffer.SetData(fieldData);
            timer -= refreshRate;
        }

        timer += Time.deltaTime;

        if (drawDensityGizmo && gizmoBuffer != null && argsBuffer != null && gizmoMesh != null &&
            gizmoMaterial != null)
        {
            gizmoMaterial.SetFloat("_Size", gizmoSize);
            gizmoMaterial.SetFloat("_Alpha", gizmoAlpha);
            Graphics.DrawMeshInstancedIndirect(gizmoMesh, 0, gizmoMaterial, bounds, argsBuffer);
        }

        if (isoSurfaceMesh != null && surfaceMaterial != null && isoSurfaceMesh.vertexCount > 0)
        {
            Graphics.DrawMesh(isoSurfaceMesh, Matrix4x4.identity, surfaceMaterial, gameObject.layer);
        }
    }

    public void InitializeField()
    {
        timer = 0f;
        fieldData = new FieldData[resolution * resolution * resolution];
        RefreshFieldContents();
    }

    public int GetIndex(int x, int y, int z)
    {
        return x + resolution * y + resolution * resolution * z;
    }

    private void RefreshFieldContents()
    {
        if (fieldData == null) return;

        float3 centerCell = new float3(resolution / 2f, resolution / 2f, resolution / 2f);
        float3 origin = (float3)transform.position + fieldOffset;
        int index = 0;
        for (int z = 0; z < resolution; z++)
        {
            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    FieldData fd = fieldData[index];
                    fd.position = (new float3(x, y, z) - centerCell) * spacing + origin;
                    fd.density = 0f;
                    fieldData[index] = fd;
                    index++;
                }
            }
        }

        switch (fieldMode)
        {
            case DensityFieldMode.Sphere:
                if (generator != null)
                    generator.Apply(fieldData, origin);
                break;
            case DensityFieldMode.Terrain:
                if (terrainGenerator != null)
                    terrainGenerator.Apply(fieldData, origin);
                break;
        }

        if (isoSurfaceMesh != null)
            MarchingCubes.BuildMesh(isoSurfaceMesh, fieldData, resolution, IsoValue);

        UpdateBounds();
    }

    private void UpdateBounds()
    {
        float halfExtent = (resolution - 1) * spacing * 0.5f + gizmoSize;
        float3 boundsCenter = (float3)transform.position + fieldOffset;
        bounds = new Bounds((Vector3)boundsCenter, Vector3.one * (halfExtent * 2f));
    }

    private void InitializeGizmo()
    {
        if (fieldData == null || fieldData.Length == 0) return;

        int count = fieldData.Length;
        UpdateBounds();

        gizmoBuffer?.Release();
        gizmoBuffer = new ComputeBuffer(count, sizeof(float) * 4);
        gizmoBuffer.SetData(fieldData);

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
            args[3] = (uint)gizmoMesh.GetBaseVertex(0);
        }

        argsBuffer = new ComputeBuffer(1, sizeof(uint) * args.Length, ComputeBufferType.IndirectArguments);
        argsBuffer.SetData(args);
    }

    private void OnDrawGizmos()
    {
        if (!drawDensityGizmo || fieldData == null) return;

        for (int i = 0; i < fieldData.Length; i++)
        {
            float d = fieldData[i].density;
            Gizmos.color = d <= 0f ? Color.blue : Color.red;
            Gizmos.DrawSphere((Vector3)fieldData[i].position, editorGizmoRadius);
        }
    }

    private void OnDestroy()
    {
        gizmoBuffer?.Release();
        argsBuffer?.Release();
        if (isoSurfaceMesh != null)
            Destroy(isoSurfaceMesh);
    }
}
