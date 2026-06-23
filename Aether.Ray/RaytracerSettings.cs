namespace Aether.Ray;

public class RaytracerSettings
{
    public int Width { get; init; } = 800;
    public int Height { get; init; } = 600;
    public float FovDegrees { get; init; } = 75f;
    public int MaxBounces { get; init; } = 3;
}
