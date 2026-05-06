using Unity.Mathematics;
using UnityEngine;

public struct FieldData
{
    public float3 position;
    public float density;
}

public class DensityField : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private int resolution = 16;
    [SerializeField] private float spacing = 1f;
    [SerializeField] private float sdfSphereRadius = 5f;

    [Header("Offsets")]
    [SerializeField] private float offSetX = 0f;
    [SerializeField] private float offSetY = 0f;
    [SerializeField] private float offSetZ = 0f;

    private FieldData[] fieldData;

    public void Start()
    {
        InitializeField();
        GenerateDensityField();
    }

    public void InitializeField()
    {
        fieldData = new FieldData[resolution * resolution * resolution];
        int index = 0;

        for (int z = 0; z < resolution; z++)
        {
            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    fieldData[index].position = new float3(x * spacing, y * spacing, z * spacing);
                    fieldData[index].density = 0f;
                    index++;
                }
            }
        }
    }

    public int GetIndex(int x, int y, int z)
    {
        return x + resolution * y + resolution * resolution * z;
    }

    private void GenerateDensityField()
    {
        if (fieldData == null) return;

        float3 center = new float3(resolution * spacing / 2f + offSetX, resolution * spacing / 2f + offSetY, resolution * spacing / 2f + offSetZ);

        for (int i = 0; i < fieldData.Length; i++)
        {
            float dist = math.distance(fieldData[i].position, center);
            float tempDensity = (sdfSphereRadius - dist) / sdfSphereRadius;
            fieldData[i].density = math.clamp(tempDensity, 0f, 1f);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (fieldData == null) return;

        for (int i = 0; i < fieldData.Length; i++)
        {
            Gizmos.color = Color.Lerp(Color.blue, Color.red, fieldData[i].density);
            Gizmos.DrawSphere(fieldData[i].position, 0.1f);
        }
    }
}
