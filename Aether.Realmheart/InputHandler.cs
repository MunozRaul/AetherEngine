using Aether.Core;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Aether.Realmheart;

/// <summary>
/// reads KeyboardState / MouseState, moves camera
/// </summary>
public class InputHandler
{
    private readonly Camera _camera;
    private float _lastMouseX, _lastMouseY;
    private bool _firstMove = true;
    private const float MoveSpeed = 5f;
    private const float MouseSensitivity = 0.1f;

    public InputHandler(Camera camera) => _camera = camera;

    public void Update(KeyboardState kb, MouseState mouse, float dt)
    {
        // WASD movement relative to camera look direction
        if (kb.IsKeyDown(Keys.W)) _camera.Position += _camera.Forward * MoveSpeed * dt;
        if (kb.IsKeyDown(Keys.S)) _camera.Position -= _camera.Forward * MoveSpeed * dt;
        if (kb.IsKeyDown(Keys.A)) _camera.Position -= _camera.Right * MoveSpeed * dt;
        if (kb.IsKeyDown(Keys.D)) _camera.Position += _camera.Right * MoveSpeed * dt;

        // Mouse look
        if (_firstMove) { _lastMouseX = mouse.X; _lastMouseY = mouse.Y; _firstMove = false; }
        float dx = mouse.X - _lastMouseX;
        float dy = mouse.Y - _lastMouseY;
        _lastMouseX = mouse.X;
        _lastMouseY = mouse.Y;

        _camera.Yaw += dx * MouseSensitivity;
        _camera.Pitch = Math.Clamp(_camera.Pitch - dy * MouseSensitivity, -89f, 89f);
    }
}
