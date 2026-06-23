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

    // Raytracer
    private CpuRaytracer _raytracer = null!;
    private float _raytraceCooldown = 0f;
    private const float RaytracePeriod = 0.016f;   // re-render every 16 ms (60 fps)
    private const int WidthPixels = 640;
    private const int HeightPixels = 360;

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

        // Load shaders (see shader source below)
        _shader = new ShaderProgram(SceneVert, SceneFrag);

        // Build scene for raytracer
        _scene.Objects.Add(new SceneObject { Hittable = new InfinitePlane() });
        // Cube AABB — will update each frame to match animation
        _raytracerCube = new SceneObject
        {
            Hittable = new Box { Min = new SysVec3(-0.25f, 1.25f, -0.25f), Max = new SysVec3(0.25f, 1.75f, 0.25f) }
        };
        _scene.Objects.Add(_raytracerCube);

        // Raytracer
        RaytracerSettings rtSettings = new RaytracerSettings { Width = WidthPixels, Height = HeightPixels };   // low res for speed
        SunLight sun = new SunLight { Direction = SysVec3.Normalize(new SysVec3(-1f, -1.5f, -0.5f)) };
        _raytracer = new CpuRaytracer(rtSettings, sun);
        _raytracedTex = new GlTexture(rtSettings.Width, rtSettings.Height);

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

        // Re-raytrace periodically (not every frame — it's slow)
        _raytraceCooldown -= dt;
        if (_raytraceCooldown <= 0f)
        {
            byte[] pixels = _raytracer.Render(_scene, _camera);
            _raytracedTex.Upload(pixels);
            _raytraceCooldown = RaytracePeriod;
        }
    }

    protected override void OnRenderFrame(FrameEventArgs e)
    {
        base.OnRenderFrame(e);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        // This stretches my 320x180 raytraced image across my full screen window
        GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, 0);

        // Create an internal framebuffer to hold the texture for copying
        int fbo = GL.GenFramebuffer();
        GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, fbo);
        GL.FramebufferTexture2D(FramebufferTarget.ReadFramebuffer, FramebufferAttachment.ColorAttachment0,
                                TextureTarget.Texture2D, _raytracedTex.Handle, 0);

        // Copy the raytracer image straight onto the screen surface (Framebuffer 0)
        GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0);
        GL.BlitFramebuffer(0, 0, WidthPixels, HeightPixels,
                          0, 0, Size.X, Size.Y,
                          ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest);

        GL.DeleteFramebuffer(fbo);

        SwapBuffers();
    }

    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);
        GL.Viewport(0, 0, e.Width, e.Height);
    }

    protected override void OnUnload()
    {
        _shader.Dispose();
        _plane.Dispose();
        _cube.Dispose();
        _raytracedTex.Dispose();
        base.OnUnload();
    }

    // Modified vertex shader to pass UV coordinates to the fragment shader
    private const string SceneVert = @"
        #version 330 core
        layout(location=0) in vec3 aPos;
        layout(location=1) in vec3 aNormal;
        layout(location=2) in vec2 aUV;
        
        uniform mat4 uModel, uView, uProjection;
        
        out vec3 vNormal;
        out vec3 vFragPos;
        out vec2 vUV;

        void main() {
            vec4 worldPos  = uModel * vec4(aPos, 1.0);
            vFragPos       = worldPos.xyz;
            vNormal        = mat3(transpose(inverse(uModel))) * aNormal;
            vUV            = aUV; // Pass texture coordinates down
            gl_Position    = uProjection * uView * worldPos;
        }";

    // Hybrid fragment shader that can toggle between standard lighting and raytracer texture
    private const string SceneFrag = @"
        #version 330 core
        in  vec3 vNormal;
        in  vec3 vFragPos;
        in  vec2 vUV;
        out vec4 FragColor;

        uniform vec3 uSunDir     = normalize(vec3(-1.0, -1.5, -0.5));
        uniform vec3 uSunColor   = vec3(1.0, 0.95, 0.85);
        uniform vec3 uAlbedo     = vec3(0.75, 0.75, 0.75);

        uniform sampler2D uRaytraceTexture;
        uniform bool uUseTexture = false;

        void main() {
            // Emissive shortcut for our sun object
            if (uAlbedo.r > 1.5) {
                FragColor = vec4(uAlbedo, 1.0);
                return;
            }

            // If toggled, map our raytracer pixels straight onto the surface geometry
            if (uUseTexture) {
                FragColor = texture(uRaytraceTexture, vUV);
                return;
            }

            // Fallback standard rasterization shader
            vec3  n       = normalize(vNormal);
            float diff    = max(dot(n, -uSunDir), 0.0);
            vec3  color   = uAlbedo * (uSunColor * diff + vec3(0.08));
            FragColor     = vec4(color, 1.0);
        }";
}
