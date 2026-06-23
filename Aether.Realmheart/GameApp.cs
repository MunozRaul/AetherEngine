using Aether.Core;
using Aether.Glaze;
using Aether.Ray;
using Aether.Ray.Shapes;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using SysVec3 = System.Numerics.Vector3;
using TKMatrix4 = OpenTK.Mathematics.Matrix4;
using TKVec3 = OpenTK.Mathematics.Vector3;

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
    private const float RaytracePeriod = 0.05f;   // re-render every 50 ms

    // Input
    private InputHandler _input = null!;

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
        Box cubeHittable = new Box { Min = new SysVec3(-0.5f, 1f, -0.5f), Max = new SysVec3(0.5f, 2f, 0.5f) };
        _scene.Objects.Add(new SceneObject { Hittable = cubeHittable });

        // Raytracer
        RaytracerSettings rtSettings = new RaytracerSettings { Width = 320, Height = 180 };   // low res for speed
        SunLight sun = new SunLight { Direction = SysVec3.Normalize(new SysVec3(-1f, -1.5f, -0.5f)) };
        _raytracer = new CpuRaytracer(rtSettings, sun);
        _raytracedTex = new GlTexture(rtSettings.Width, rtSettings.Height);

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

        _shader.Use();
        // setup global scene matrices
        TKMatrix4 proj = TKMatrix4.CreatePerspectiveFieldOfView(
            MathHelper.DegreesToRadians(75f),
            (float)Size.X / Size.Y, 0.1f, 200f);
        TKMatrix4 view = _camera.GetViewMatrix().ToOpenTK(); // convert System.Numerics → OpenTK

        _shader.SetMatrix4("uProjection", proj);
        _shader.SetMatrix4("uView", view);

        // Reset default object color to grey
        _shader.SetVector3("uAlbedo", new TKVec3(0.75f, 0.75f, 0.75f));

        // Draw plane
        _shader.SetMatrix4("uModel", TKMatrix4.Identity);
        _plane.Draw();

        // Draw cube with current animated transform
        _shader.SetMatrix4("uModel", _cubeTransform.GetModelMatrix().ToOpenTK());
        _cube.Draw();

        // Draw sun in the skybox
        SysVec3 sunDirFromOrigin = _raytracer.SunDirection;
        SysVec3 sunWorldPos = _camera.Position + (SysVec3.Normalize(sunDirFromOrigin) * 150f);
        TKMatrix4 sunModel = TKMatrix4.CreateScale(4f) * TKMatrix4.CreateTranslation(sunWorldPos.X, sunWorldPos.Y, sunWorldPos.Z);
        _shader.SetMatrix4("uModel", sunModel);
        // Inject a bright yellow color override into the shader
        _shader.SetVector3("uAlbedo", new TKVec3(2.0f, 2.0f, 1.2f)); // values > 1.0 look bright!
        _cube.Draw();

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

    // Minimal vertex shader source
    private const string SceneVert = @"
        #version 330 core
        layout(location=0) in vec3 aPos;
        layout(location=1) in vec3 aNormal;
        layout(location=2) in vec2 aUV;
        uniform mat4 uModel, uView, uProjection;
        out vec3 vNormal;
        out vec3 vFragPos;

        void main() {
            vec4 worldPos  = uModel * vec4(aPos, 1.0);
            vFragPos       = worldPos.xyz;
            vNormal        = mat3(transpose(inverse(uModel))) * aNormal;
            gl_Position    = uProjection * uView * worldPos;
        }";

    // Minimal fragment shader — directional sun + ambient
    private const string SceneFrag = @"
        #version 330 core
        in  vec3 vNormal;
        in  vec3 vFragPos;
        out vec4 FragColor;

        uniform vec3 uSunDir     = normalize(vec3(-1.0, -1.5, -0.5));
        uniform vec3 uSunColor   = vec3(1.0, 0.95, 0.85);
        uniform vec3 uAlbedo     = vec3(0.75, 0.75, 0.75);

        void main() {
            // If the color is set to be ultra-bright like the sun, make it emit rays since it can act as a light source
            if (uAlbedo.r > 1.5) {
                FragColor = vec4(uAlbedo, 1.0);
                return;
            }

            vec3  n       = normalize(vNormal);
            float diff    = max(dot(n, -uSunDir), 0.0);
            vec3  color   = uAlbedo * (uSunColor * diff + vec3(0.08));
            FragColor     = vec4(color, 1.0);
        }";
}
