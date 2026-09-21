using System.Numerics;

namespace Aether.Core;

/// <summary>
/// position, yaw, pitch, view matrix
/// </summary>
public class Camera
{
    public Vector3 Position { get; set; }
    public float Yaw { get; set; }   // degrees, horizontal rotation
    public float Pitch { get; set; }   // degrees, clamped +-89

    public Vector3 Forward { get; private set; }
    public Vector3 Right { get; private set; }
    public Vector3 Up { get; private set; }

    public void Update()
    {
        // Recompute Forward and Right from Yaw/Pitch
        float yawRad = MathF.PI / 180f * Yaw;
        float pitchRad = MathF.PI / 180f * Pitch;
        Forward = Vector3.Normalize(new Vector3(
            MathF.Cos(pitchRad) * MathF.Cos(yawRad),
            MathF.Sin(pitchRad),
            MathF.Cos(pitchRad) * MathF.Sin(yawRad)
        ));
        Right = Vector3.Normalize(Vector3.Cross(Forward, Vector3.UnitY));
        Up = Vector3.Normalize(Vector3.Cross(Forward, Right));
    }

    // Returns a 4×4 view matrix (compatible with OpenTK Matrix4)
    public Matrix4x4 GetViewMatrix()
        => Matrix4x4.CreateLookAt(Position, Position + Forward, Vector3.UnitY);
}
