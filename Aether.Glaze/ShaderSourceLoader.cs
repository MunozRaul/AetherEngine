namespace Aether.Glaze;

/// <summary>
/// Loads GLSL source files from disk relative to the running executable, so shader edits
/// don't require recompiling C#. Centralized here so the base-path logic only lives in one place.
/// </summary>
public static class ShaderSourceLoader
{
    public static string Load(string relativePath)
        => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, relativePath));
}
