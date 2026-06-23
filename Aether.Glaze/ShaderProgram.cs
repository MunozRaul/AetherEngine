using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace Aether.Glaze;

/// <summary>
/// Wraps OpenGL shader compilation and uniform setting
/// </summary>
public class ShaderProgram : IDisposable
{
    private readonly int _handle;

    public ShaderProgram(string vertSource, string fragSource)
    {
        int vert = CompileShader(ShaderType.VertexShader, vertSource);
        int frag = CompileShader(ShaderType.FragmentShader, fragSource);

        _handle = GL.CreateProgram();
        GL.AttachShader(_handle, vert);
        GL.AttachShader(_handle, frag);
        GL.LinkProgram(_handle);
        GL.DeleteShader(vert);
        GL.DeleteShader(frag);
    }

    public void Use() => GL.UseProgram(_handle);

    public void SetMatrix4(string name, Matrix4 mat)
    {
        int loc = GL.GetUniformLocation(_handle, name);
        GL.UniformMatrix4(loc, false, ref mat);
    }

    public void SetInt(string name, int value)
        => GL.Uniform1(GL.GetUniformLocation(_handle, name), value);

    private static int CompileShader(ShaderType type, string source)
    {
        int shader = GL.CreateShader(type);
        GL.ShaderSource(shader, source);
        GL.CompileShader(shader);
        GL.GetShader(shader, ShaderParameter.CompileStatus, out int ok);
        if (ok == 0) throw new Exception(GL.GetShaderInfoLog(shader));
        return shader;
    }

    public void SetVector3(string name, Vector3 val)
    {
        int loc = GL.GetUniformLocation(_handle, name);
        GL.Uniform3(loc, val.X, val.Y, val.Z);
    }

    public void Dispose() => GL.DeleteProgram(_handle);
}
