using System.Numerics;

namespace Aether.Core
{
    /// <summary>
    /// position, rotation, scale → model matrix
    /// </summary>
    public class Transform
    {
        public Vector3 Position { get; set; } = Vector3.Zero;
        public Vector3 Rotation { get; set; } = Vector3.Zero;  // Euler angles, degrees
        public Vector3 Scale { get; set; } = Vector3.One;

        public Matrix4x4 GetModelMatrix()
        {
            Matrix4x4 t = Matrix4x4.CreateTranslation(Position);
            Matrix4x4 rx = Matrix4x4.CreateRotationX(MathF.PI / 180f * Rotation.X);
            Matrix4x4 ry = Matrix4x4.CreateRotationY(MathF.PI / 180f * Rotation.Y);
            Matrix4x4 rz = Matrix4x4.CreateRotationZ(MathF.PI / 180f * Rotation.Z);
            Matrix4x4 s = Matrix4x4.CreateScale(Scale);
            return s * rx * ry * rz * t;
        }
    }
}
