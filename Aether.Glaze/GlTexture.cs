using OpenTK.Graphics.OpenGL4;

namespace Aether.Glaze;

/// <summary>
/// Uploads a CPU pixel buffer (RGBA bytes) to a GL texture, used to display raytracer output.
/// Also bindable as a compute-shader "image" so a compute shader can write into it directly
/// via imageStore(), instead of a CPU roundtrip through Upload().
/// </summary>
public class GlTexture : IDisposable
{
    public int Handle { get; }
    public int Width { get; }
    public int Height { get; }

    public GlTexture(int width, int height)
    {
        Width = width;
        Height = height;
        Handle = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, Handle);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

        // TexStorage2D allocates *immutable* storage with a sized internal format (Rgba8).
        // Compute shaders require this (via BindImageTexture) to write pixels with imageStore.
        // The old TexImage2D call created "mutable" storage, which image load/store doesnt support.
        GL.TexStorage2D(TextureTarget2d.Texture2D, 1, SizedInternalFormat.Rgba8, width, height);
    }

    // Call this every frame (or whenever the raytracer produces a new buffer)
    public void Upload(byte[] pixels)
    {
        GL.BindTexture(TextureTarget.Texture2D, Handle);
        GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, Width, Height,
                         PixelFormat.Rgba, PixelType.UnsignedByte, pixels);
    }

    // Binds this texture to an image unit so a compute shader can read/write it directly
    // (the shader-side declaration is 'layout(rgba8, binding = unit) uniform image2D ...').
    // This is basically so the shader can be told "here's the texture you're allowed to write into."
    public void BindAsImage(int unit, TextureAccess access)
        => GL.BindImageTexture(unit, Handle, 0, false, 0, access, SizedInternalFormat.Rgba8);

    public void Dispose() => GL.DeleteTexture(Handle);
}
