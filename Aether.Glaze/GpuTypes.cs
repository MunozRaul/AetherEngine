using System.Runtime.InteropServices;

namespace Aether.Glaze;

/// <summary>
/// GPU-side mirror of a scene primitive, laid out to match the compute shaders
/// 'GpuPrimitive' struct byte-for-byte. Every field group is a full 16 bytes
/// (a vec4 or ivec4) so std430's memory alignment rules can't sneak in implicit padding.
/// The C# layout and the GLSL layout are guaranteed to agree.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct GpuPrimitive
{
    public const int Box = 0;
    public const int Plane = 1;

    // Box: Min.xyz (W unused), Plane: Normal.xyz (W unused)
    public float AX, AY, AZ, AW;
    // Box: Max.xyz (W unused), Plane: Distance in BX (BY/BZ/W unused)
    public float BX, BY, BZ, BW;

    public int Type;
    public int MaterialIndex;
    private readonly int _pad0;
    private readonly int _pad1;
}

/// <summary>
/// GPU-side mirror of Aether.Core.Material, matching the compute shader's 'GpuMaterial' struct.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct GpuMaterial
{
    public float AlbedoR, AlbedoG, AlbedoB, AlbedoPad;
    public float EmissiveR, EmissiveG, EmissiveB, EmissivePad;
}
