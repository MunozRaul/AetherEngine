using OpenTK.Graphics.OpenGL4;

namespace Aether.Glaze;

/// <summary>
/// Uploads a CPU pixel buffer (RGBA bytes) to a GL texture — used to display raytracer output
/// </summary>
public class GlTexture : IDisposable
{
    public int Handle { get; }
    private readonly int _width, _height;

    public GlTexture(int width, int height)
    {
        _width = width;
        _height = height;
        Handle = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, Handle);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, width, height, 0,
                      PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
    }

    // Call this every frame (or whenever the raytracer produces a new buffer)
    public void Upload(byte[] pixels)
    {
        GL.BindTexture(TextureTarget.Texture2D, Handle);
        GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, _width, _height,
                         PixelFormat.Rgba, PixelType.UnsignedByte, pixels);
    }

    public void Dispose() => GL.DeleteTexture(Handle);
}
