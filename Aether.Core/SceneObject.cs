namespace Aether.Core
{
    /// <summary>
    /// base: holds IHittable + material info
    /// </summary>
    public class SceneObject
    {
        public required IHittable Hittable { get; set; }

        // TODO: add material property
    }
}
