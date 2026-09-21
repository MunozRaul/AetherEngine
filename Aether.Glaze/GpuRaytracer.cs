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
        _program = new ShaderProgram(ComputeSource);
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
        _program.SetFloat("uAspect", (float)_settings.Width / _settings.Height);
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

    // GLSL port of CpuRaytracer.Render/TraceRay/SkyColor. Runs once per pixel, in parallel,
    // entirely on the GPU.
    private const string ComputeSource = @"
        #version 430

        layout(local_size_x = 8, local_size_y = 8) in;

        layout(rgba8, binding = 0) writeonly uniform image2D uOutputImage;

        struct GpuPrimitive
        {
            vec4  a;     // box: min.xyz | plane: normal.xyz
            vec4  b;     // box: max.xyz | plane: distance in b.x
            ivec4 meta;  // meta.x = type (0 box, 1 plane), meta.y = material index
        };

        struct GpuMaterial
        {
            vec4 albedo;
            vec4 emissive;
        };

        // std430 SSBOs - 'binding' here must match GpuRaytracer.PrimitiveBinding/MaterialBinding.
        // The [] (unsized array) lets us use primitives.length() below instead of a separate count uniform.
        layout(std430, binding = 1) readonly buffer PrimitiveBuffer { GpuPrimitive primitives[]; };
        layout(std430, binding = 2) readonly buffer MaterialBuffer  { GpuMaterial  materials[];  };

        uniform vec3  uCamPos;
        uniform vec3  uCamForward;
        uniform vec3  uCamRight;
        uniform vec3  uCamUp;
        uniform float uAspect;
        uniform float uFovScale;

        uniform vec3  uSunToSun;      // direction FROM a surface TOWARD the sun (normalized)
        uniform vec3  uSunColor;
        uniform float uSunIntensity;

        struct HitInfo
        {
            float t;
            vec3  point;
            vec3  normal;
            int   materialIndex;
        };

        bool hitBox(GpuPrimitive p, vec3 ro, vec3 rd, float tMin, float tMax, out HitInfo hit)
        {
            float tEnter = tMin, tExit = tMax;
            vec3 normal = vec3(0.0);
            vec3 bmin = p.a.xyz, bmax = p.b.xyz;

            for (int i = 0; i < 3; ++i)
            {
                float o = ro[i], d = rd[i], mn = bmin[i], mx = bmax[i];
                float invD = 1.0 / d;
                float t0 = (mn - o) * invD;
                float t1 = (mx - o) * invD;
                if (invD < 0.0) { float tmp = t0; t0 = t1; t1 = tmp; }
                if (t0 > tEnter)
                {
                    tEnter = t0;
                    normal = vec3(0.0);
                    normal[i] = d < 0.0 ? 1.0 : -1.0;
                }
                if (t1 < tExit) tExit = t1;
                if (tEnter > tExit) return false;
            }
            if (tEnter < tMin || tEnter > tMax) return false;

            hit.t = tEnter;
            hit.point = ro + rd * tEnter;
            hit.normal = normal;
            hit.materialIndex = 0; // filled in by caller
            return true;
        }

        bool hitPlane(GpuPrimitive p, vec3 ro, vec3 rd, float tMin, float tMax, out HitInfo hit)
        {
            vec3 normal = p.a.xyz;
            float dist = p.b.x;
            float denom = dot(normal, rd);
            if (abs(denom) < 1e-6) { hit.t = 0.0; hit.point = vec3(0.0); hit.normal = vec3(0.0); hit.materialIndex = 0; return false; }

            float t = (dist - dot(normal, ro)) / denom;
            if (t < tMin || t > tMax) { hit.t = 0.0; hit.point = vec3(0.0); hit.normal = vec3(0.0); hit.materialIndex = 0; return false; }

            hit.t = t;
            hit.point = ro + rd * t;
            hit.normal = normal;
            hit.materialIndex = 0; // filled in by caller
            return true;
        }

        bool hitScene(vec3 ro, vec3 rd, float tMin, float tMax, out HitInfo hit)
        {
            bool hitAny = false;
            float closest = tMax;
            hit.t = 0.0; hit.point = vec3(0.0); hit.normal = vec3(0.0); hit.materialIndex = 0;

            int count = primitives.length();
            for (int i = 0; i < count; ++i)
            {
                GpuPrimitive p = primitives[i];
                HitInfo candidate;
                bool didHit = (p.meta.x == 0)
                    ? hitBox(p, ro, rd, tMin, closest, candidate)
                    : hitPlane(p, ro, rd, tMin, closest, candidate);

                if (didHit)
                {
                    hitAny = true;
                    closest = candidate.t;
                    candidate.materialIndex = p.meta.y;
                    hit = candidate;
                }
            }
            return hitAny;
        }

        vec3 skyColor(vec3 rd)
        {
            // A dot product of ~1.0 means the ray looks almost exactly at the sun - draw a sharp disc.
            float sunDot = dot(rd, uSunToSun);
            if (sunDot > 0.995) return vec3(10.0, 10.0, 8.0);

            float t = 0.5 * (rd.y + 1.0);
            return mix(vec3(1.0), vec3(0.5, 0.7, 1.0), t);
        }

        vec3 traceRay(vec3 ro, vec3 rd)
        {
            HitInfo hit;
            if (!hitScene(ro, rd, 0.001, 1e30, hit)) return skyColor(rd);

            HitInfo shadowHit;
            vec3 shadowOrigin = hit.point + hit.normal * 0.001;
            bool inShadow = hitScene(shadowOrigin, uSunToSun, 0.001, 1e30, shadowHit);

            float ambient = 0.1;
            float diffuse = inShadow ? 0.05 : max(0.0, dot(hit.normal, uSunToSun));
            vec3 albedo = materials[hit.materialIndex].albedo.rgb;

            return albedo * uSunColor * uSunIntensity * (diffuse + ambient);
        }

        void main()
        {
            ivec2 size = imageSize(uOutputImage);
            ivec2 pixel = ivec2(gl_GlobalInvocationID.xy);
            if (pixel.x >= size.x || pixel.y >= size.y) return;

            // CpuRaytracer writes scanline 'row' into buffer row (h-1-row) because CPU row 0 is the
            // top of the image but GL texture row 0 is the bottom. We reproduce that same flip here
            // by asking 'which CPU scanline would have ended up at this GPU texel row'.
            int rowCpu = size.y - 1 - pixel.y;

            float u = (2.0 * (pixel.x + 0.5) / float(size.x) - 1.0) * uAspect * uFovScale;
            float v = (2.0 * (rowCpu + 0.5) / float(size.y) - 1.0) * uFovScale;

            vec3 rd = normalize(uCamForward + u * uCamRight + v * uCamUp);
            vec3 color = clamp(traceRay(uCamPos, rd), 0.0, 1.0);

            imageStore(uOutputImage, pixel, vec4(color, 1.0));
        }";
}
