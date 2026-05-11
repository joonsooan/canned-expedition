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
    public static float Apply(float3 sdfCenter, BrushData brush)
    {
        float sphereSdf = math.length(brush.center - sdfCenter) - brush.radius;

        switch (brush.type)
        {
            case BrushType.Add:
                return math.min(brush.strength, sphereSdf);

            case BrushType.Subtract:
                return math.max(brush.strength, -sphereSdf);

            default:
                return 0;
        }
    }
}