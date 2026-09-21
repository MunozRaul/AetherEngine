using OpenTK.Graphics.OpenGL4;
using System.Runtime.InteropServices;

namespace Aether.Glaze;

/// <summary>
/// A GPU-side array the CPU can (re)upload to and a compute shader can read as a
/// Shader Storage Buffer Object (SSBO), the GPU equivalent of a growable array,
/// readable/writable from GLSL via 'layout(std430, binding = N) buffer { ... }'.
/// Essentially this wraps a GL buffer as a Shader Storage Buffer Object (SSBO), 
/// basically "an array GLSL can read/write directly."
/// </summary>
public class GpuBuffer<T> : IDisposable where T : unmanaged
{
    public int Handle { get; }

    public GpuBuffer() => Handle = GL.GenBuffer();

    // Re-uploads the whole array. Simple and correct, not yet optimized for partial updates.
    public void Upload(T[] data, BufferUsageHint hint = BufferUsageHint.DynamicDraw)
    {
        GL.BindBuffer(BufferTarget.ShaderStorageBuffer, Handle);
        GL.BufferData(BufferTarget.ShaderStorageBuffer, data.Length * Marshal.SizeOf<T>(), data, hint);
    }

    // Binds this buffer to an SSBO binding point (must match the 'binding = N'
    // qualifier on the corresponding 'buffer' block in the compute shader).
    public void BindBase(int bindingIndex)
        => GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, bindingIndex, Handle);

    public void Dispose() => GL.DeleteBuffer(Handle);
}
