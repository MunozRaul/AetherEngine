using Aether.Core;
using Aether.Glaze;
using Aether.Ray;
using Aether.Ray.Shapes;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using SysVec3 = System.Numerics.Vector3;

namespace Aether.Realmheart;

/// <summary>
/// subclasses OpenTK.Windowing.Desktop.GameWindow
/// </summary>
public class GameApp : GameWindow
{
    // Scene objects
    private Scene _scene = new();
    private Camera _camera = new() { Position = new SysVec3(0, 1.7f, 5f) };
    private Transform _cubeTransform = new() { Position = new SysVec3(0, 1.5f, 0) };

    // Renderer objects
    private ShaderProgram _shader = null!;
    private Mesh _plane = null!;
    private Mesh _cube = null!;
    private GlTexture _raytracedTex = null!;
    private int _blitFbo;

    // Raytracer
    private GpuRaytracer _raytracer = null!;
    private float _raytraceCooldown = 0f;
    private const float RaytracePeriod = 0.016f;   // re-render every 16 ms (60 fps)

    // Input
    private InputHandler _input = null!;

    // Racetracer target tracker
    private SceneObject _raytracerCube = null!;

    public GameApp(GameWindowSettings gws, NativeWindowSettings nws) : base(gws, nws) { }

    protected override void OnLoad()
    {
        base.OnLoad();
        GL.ClearColor(0.1f, 0.1f, 0.15f, 1f);
        GL.Enable(EnableCap.DepthTest);
        CursorState = CursorState.Grabbed;

        // Build meshes
        _plane = MeshFactory.CreatePlane(20f);
        _cube = MeshFactory.CreateCube(0.5f);

        // Load shaders from disk (Shaders/scene.vert, Shaders/scene.frag)
        _shader = new ShaderProgram(
            ShaderSourceLoader.Load(@"Shaders\scene.vert"),
            ShaderSourceLoader.Load(@"Shaders\scene.frag"));

        // Build scene for raytracer
        _scene.Objects.Add(new SceneObject
        {
            Hittable = new InfinitePlane(),
            Material = new Material { Albedo = new SysVec3(0.3f, 0.6f, 0.3f) } // greenish floor
        });
        // Cube AABB — will update each frame to match animation
        _raytracerCube = new SceneObject
        {
            Hittable = new Box { Min = new SysVec3(-0.25f, 1.25f, -0.25f), Max = new SysVec3(0.25f, 1.75f, 0.25f) },
            Material = new Material { Albedo = new SysVec3(0.8f, 0.2f, 0.2f) } // reddish cube
        };
        _scene.Objects.Add(_raytracerCube);

        // Raytracer. Width/Height here are just RaytracerSettings' informational defaults.
        // The actual render target resolution now tracks the window size (see CreateRaytraceTarget).
        RaytracerSettings rtSettings = new RaytracerSettings();
        SunLight sun = new SunLight { Direction = SysVec3.Normalize(new SysVec3(-1f, -1.5f, -0.5f)) };
        _raytracer = new GpuRaytracer(rtSettings, sun);

        // The FBO handle itself is created once (only its texture attachment gets swapped out
        // on resize, see CreateRaytraceTarget), since only the attached texture ever needs replacing.
        _blitFbo = GL.GenFramebuffer();
        CreateRaytraceTarget(Size.X, Size.Y);

        _camera.Position = new SysVec3(0, 1.7f, 5f);
        _camera.Yaw = -90f; // Forces the camera orientation matrix to point at the scene center
        _camera.Pitch = 0f;  // Look straight across the horizon
        _camera.Update();
        _input = new InputHandler(_camera);
    }

    protected override void OnUpdateFrame(FrameEventArgs e)
    {
        base.OnUpdateFrame(e);
        float dt = (float)e.Time;

        if (KeyboardState.IsKeyDown(Keys.Escape))
        {
            Close();
        }

        // Move camera
        _input.Update(KeyboardState, MouseState, dt);
        _camera.Update();

        // Animate cube: spin + bob
        _cubeTransform.Rotation = new SysVec3(0, _cubeTransform.Rotation.Y + 45f * dt, 0);
        _cubeTransform.Position = new SysVec3(0, 1.5f + MathF.Sin((float)GLFW.GetTime()) * 0.3f, 0);

        // Swap out the entire Box instance to match the new position to synchronize raytracer
        // (this is assuming index 1 matches the box added in OnLoad)
        // NOTE: If swapping gets too inefficient, adding a mutation helper to Box is an alternative.
        if (_scene.Objects.Count > 1 && _scene.Objects[1].Hittable is Box)
        {
            float size = 0.5f; // Matches the MeshFactory size

            // Create a brand new immutable box at the updated position
            _raytracerCube.Hittable = new Box
            {
                Min = _cubeTransform.Position - new SysVec3(size / 2f),
                Max = _cubeTransform.Position + new SysVec3(size / 2f)
            };
        }

        // Re-raytrace periodically (not every frame, even on GPU this keeps CPU-side scene
        // upload overhead in check while we don't yet have dirty-tracking)
        _raytraceCooldown -= dt;
        if (_raytraceCooldown <= 0f)
        {
            _raytracer.Render(_scene, _camera, _raytracedTex);
            _raytraceCooldown = RaytracePeriod;
        }
    }

    protected override void OnRenderFrame(FrameEventArgs e)
    {
        base.OnRenderFrame(e);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        // _raytracedTex is now always sized to match the window (see CreateRaytraceTarget),
        // so this is a 1:1 copy, not a stretch which means no upscale blur/blockiness.
        GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, _blitFbo);

        // Copy the raytracer image straight onto the screen surface (Framebuffer 0)
        GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0);
        GL.BlitFramebuffer(0, 0, _raytracedTex.Width, _raytracedTex.Height,
                          0, 0, Size.X, Size.Y,
                          ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest);

        SwapBuffers();
    }

    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);
        GL.Viewport(0, 0, e.Width, e.Height);

        // Re-allocate the raytrace target to match the new window size (e.g. entering fullscreen).
        // Skip zero-sized events (window minimized) since TexStorage2D can't allocate empty storage,
        // and skip redundant recreations if the size didn't actually change.
        if (e.Width > 0 && e.Height > 0 && _raytracedTex != null &&
            (e.Width != _raytracedTex.Width || e.Height != _raytracedTex.Height))
        {
            CreateRaytraceTarget(e.Width, e.Height);
        }
    }

    // (Re)allocates _raytracedTex at the given size and re-points the (already-existing) blit FBOs
    // color attachment at it. TexStorage2D storage is immutable, so a resize means delete + recreate,
    // not an in-place update.
    private void CreateRaytraceTarget(int width, int height)
    {
        _raytracedTex?.Dispose();
        _raytracedTex = new GlTexture(width, height);

        GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, _blitFbo);
        GL.FramebufferTexture2D(FramebufferTarget.ReadFramebuffer, FramebufferAttachment.ColorAttachment0,
                                TextureTarget.Texture2D, _raytracedTex.Handle, 0);
        GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, 0);
    }

    protected override void OnUnload()
    {
        _shader.Dispose();
        _plane.Dispose();
        _cube.Dispose();
        _raytracedTex.Dispose();
        _raytracer.Dispose();
        GL.DeleteFramebuffer(_blitFbo);
        base.OnUnload();
    }
}
