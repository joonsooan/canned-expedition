using Unity.Mathematics;

public enum BrushType
{
    Add,
    Subtract,
}

public struct BrushData
{
    public BrushType type;
    public float3 center;
    public float radius;
    public float strength;
}

public static class SdfBrush
{
    public static float Apply(float baseDensity, float3 samplePosition, BrushData brush)
    {
        float sphereSdf = math.length(brush.center - samplePosition) - brush.radius;
        sphereSdf -= brush.strength;

        switch (brush.type)
        {
            case BrushType.Add:
                return math.min(baseDensity, sphereSdf);

            case BrushType.Subtract:
                return math.max(baseDensity, -sphereSdf);

            default:
                return baseDensity;
        }
    }
}