using Aether.Core;
using Aether.Ray;
using Aether.Ray.Shapes;
using OpenTK.Graphics.OpenGL4;
using GlVector3 = OpenTK.Mathematics.Vector3;
using SysVector3 = System.Numerics.Vector3;

namespace Aether.Glaze;

/// <summary>
/// GPU port of my Aether.Ray.CpuRaytracer. Instead of a CPU Parallel.For loop that returns a
/// byte[] the app then uploads to a texture, this uploads the scene to GPU buffers once per
/// frame and dispatches a compute shader that writes pixels directly into a GL texture.
/// That way i dont need a CPU-side pixel loop at all.
/// </summary>
public class GpuRaytracer : IDisposable
{
    // Must match local_size_x/local_size_y in ComputeSource below.
    private const int WorkgroupSize = 8;

    // Must match the 'binding = N' qualifiers in ComputeSource below.
    private const int PrimitiveBinding = 1;
    private const int MaterialBinding = 2;
    private const int ImageUnit = 0;

    private readonly RaytracerSettings _settings;
    private readonly SunLight _sun;
    private readonly float _fovScale;

    private readonly ShaderProgram _program;
    private readonly GpuBuffer<GpuPrimitive> _primitiveBuffer = new();
    private readonly GpuBuffer<GpuMaterial> _materialBuffer = new();

    public GpuRaytracer(RaytracerSettings settings, SunLight sun)
    {
        _settings = settings;
        _sun = sun;
        _fovScale = MathF.Tan(MathF.PI / 180f * settings.FovDegrees * 0.5f);
        _program = new ShaderProgram(ShaderSourceLoader.Load(@"Shaders\raytrace.comp"));
    }

    public void Render(Scene scene, Camera camera, GlTexture target)
    {
        UploadScene(scene);

        _program.Use();
        _primitiveBuffer.BindBase(PrimitiveBinding);
        _materialBuffer.BindBase(MaterialBinding);
        target.BindAsImage(ImageUnit, TextureAccess.WriteOnly);

        _program.SetVector3("uCamPos", ToGl(camera.Position));
        _program.SetVector3("uCamForward", ToGl(camera.Forward));
        _program.SetVector3("uCamRight", ToGl(camera.Right));
        _program.SetVector3("uCamUp", ToGl(camera.Up));
        // Read straight off the target texture (not _settings) so aspect stays correct
        // even after the caller resizes the render target to match a resized window.
        _program.SetFloat("uAspect", (float)target.Width / target.Height);
        _program.SetFloat("uFovScale", _fovScale);
        _program.SetVector3("uSunToSun", ToGl(_sun.ToSun));
        _program.SetVector3("uSunColor", ToGl(_sun.Color));
        _program.SetFloat("uSunIntensity", _sun.Intensity);

        // One invocation per pixel, grouped into WorkgroupSize x WorkgroupSize tiles.
        // Round up so partially-filled edge tiles are still dispatched (the shader itself
        // guards out-of-bounds pixels, see the `if (pixel.x >= size.x ...)` check below).
        int groupsX = (target.Width + WorkgroupSize - 1) / WorkgroupSize;
        int groupsY = (target.Height + WorkgroupSize - 1) / WorkgroupSize;
        GL.DispatchCompute(groupsX, groupsY, 1);

        // The compute shader writes through imageStore(). This barrier guarantees those writes
        // are finished and visible before anything downstream (our later BlitFramebuffer call,
        // or a future texture sample) reads from the same texture.
        GL.MemoryBarrier(MemoryBarrierFlags.ShaderImageAccessBarrierBit |
                          MemoryBarrierFlags.FramebufferBarrierBit |
                          MemoryBarrierFlags.TextureFetchBarrierBit);
    }

    private void UploadScene(Scene scene)
    {
        GpuPrimitive[] primitives = new GpuPrimitive[scene.Objects.Count];
        GpuMaterial[] materials = new GpuMaterial[scene.Objects.Count];

        for (int i = 0; i < scene.Objects.Count; i++)
        {
            SceneObject obj = scene.Objects[i];
            materials[i] = ToGpuMaterial(obj.Material);
            primitives[i] = ToGpuPrimitive(obj.Hittable, i);
        }

        _primitiveBuffer.Upload(primitives);
        _materialBuffer.Upload(materials);
    }

    private static GpuMaterial ToGpuMaterial(Material m) => new()
    {
        AlbedoR = m.Albedo.X,
        AlbedoG = m.Albedo.Y,
        AlbedoB = m.Albedo.Z,
        EmissiveR = m.Emissive.X,
        EmissiveG = m.Emissive.Y,
        EmissiveB = m.Emissive.Z,
    };

    // NOTE: this switch is the same "known shape types" limitation flagged in the README
    // scene-unification TODO: every new IHittable needs a case added here to be GPU-visible.
    private static GpuPrimitive ToGpuPrimitive(IHittable hittable, int materialIndex) => hittable switch
    {
        Box box => new GpuPrimitive
        {
            Type = GpuPrimitive.Box,
            MaterialIndex = materialIndex,
            AX = box.Min.X,
            AY = box.Min.Y,
            AZ = box.Min.Z,
            BX = box.Max.X,
            BY = box.Max.Y,
            BZ = box.Max.Z,
        },
        InfinitePlane plane => new GpuPrimitive
        {
            Type = GpuPrimitive.Plane,
            MaterialIndex = materialIndex,
            AX = plane.Normal.X,
            AY = plane.Normal.Y,
            AZ = plane.Normal.Z,
            BX = plane.Distance,
        },
        _ => throw new NotSupportedException($"{hittable.GetType().Name} has no GPU representation yet.")
    };

    private static GlVector3 ToGl(SysVector3 v) => new(v.X, v.Y, v.Z);

    public void Dispose()
    {
        _program.Dispose();
        _primitiveBuffer.Dispose();
        _materialBuffer.Dispose();
    }
}
