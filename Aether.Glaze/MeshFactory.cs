namespace Aether.Glaze;

/// <summary>
/// Geometry helpers
/// </summary>
public static class MeshFactory
{
    // A large flat quad at Y=0 for the floor
    public static Mesh CreatePlane(float halfSize = 20f)
    {
        float[] verts =
        {
        //  x          y   z          nx  ny  nz   u   v
           -halfSize,  0, -halfSize,  0,  1,  0,   0,  0,
            halfSize,  0, -halfSize,  0,  1,  0,   1,  0,
            halfSize,  0,  halfSize,  0,  1,  0,   1,  1,
           -halfSize,  0,  halfSize,  0,  1,  0,   0,  1,
        };
        uint[] idx = { 0, 1, 2, 2, 3, 0 };
        return new Mesh(verts, idx);
    }

    // Unit cube centred at the origin
    public static Mesh CreateCube(float halfSize = 0.5f)
    {
        float h = halfSize;
        // 24 unique vertices (4 per face, so normals are per-face)
        float[] verts =
        {
        //  pos          normal       uv
            // +Z face
           -h, -h,  h,   0,  0,  1,   0, 0,
            h, -h,  h,   0,  0,  1,   1, 0,
            h,  h,  h,   0,  0,  1,   1, 1,
           -h,  h,  h,   0,  0,  1,   0, 1,
            // -Z face
            h, -h, -h,   0,  0, -1,   0, 0,
           -h, -h, -h,   0,  0, -1,   1, 0,
           -h,  h, -h,   0,  0, -1,   1, 1,
            h,  h, -h,   0,  0, -1,   0, 1,
            // +X face
            h, -h,  h,   1,  0,  0,   0, 0,
            h, -h, -h,   1,  0,  0,   1, 0,
            h,  h, -h,   1,  0,  0,   1, 1,
            h,  h,  h,   1,  0,  0,   0, 1,
            // -X face
           -h, -h, -h,  -1,  0,  0,   0, 0,
           -h, -h,  h,  -1,  0,  0,   1, 0,
           -h,  h,  h,  -1,  0,  0,   1, 1,
           -h,  h, -h,  -1,  0,  0,   0, 1,
            // +Y face
           -h,  h,  h,   0,  1,  0,   0, 0,
            h,  h,  h,   0,  1,  0,   1, 0,
            h,  h, -h,   0,  1,  0,   1, 1,
           -h,  h, -h,   0,  1,  0,   0, 1,
            // -Y face
           -h, -h, -h,   0, -1,  0,   0, 0,
            h, -h, -h,   0, -1,  0,   1, 0,
            h, -h,  h,   0, -1,  0,   1, 1,
           -h, -h,  h,   0, -1,  0,   0, 1,
        };
        uint[] idx = new uint[36];
        for (uint face = 0; face < 6; face++)
        {
            uint b = face * 4;
            uint i = face * 6;
            idx[i + 0] = b; idx[i + 1] = b + 1; idx[i + 2] = b + 2;
            idx[i + 3] = b + 2; idx[i + 4] = b + 3; idx[i + 5] = b;
        }
        return new Mesh(verts, idx);
    }
}
