# AetherEngine

A work-in-progress hybrid rendering engine built on .NET 10 and OpenTK, combining
traditional GPU rasterization with a raytracer. The long-term goal is to evolve the
raytracing side from a CPU prototype into a real-time GPU pipeline that follows the
same concepts as modern engine solutions like Unreal Engine's Lumen — not a pixel-perfect
clone, just the same architectural ideas at hobby scale.

## Project Layout

| Project | Responsibility |
|---|---|
| `Aether.Core` | Math primitives, `Camera`, `Transform`, `IHittable`, `Scene`, `Ray`/`HitRecord` |
| `Aether.Ray` | CPU raytracer, shapes (`Box`, `InfinitePlane`), `SunLight` |
| `Aether.Glaze` | OpenGL wrapper (OpenTK): meshes, shaders, textures |
| `Aether.Realmheart` | The actual game/app window tying rasterization + raytracing together |

## Long-Term Vision

Today, `Aether.Ray` is a CPU-bound Whitted-style raytracer (`Parallel.For` per pixel)
that renders a low-resolution image and blits it over the screen alongside a
conventional rasterized scene. That's a good proof of concept, but it doesn't scale:
no acceleration structure, no real materials, no GPU parallelism, and no denoising.

The vision is to move toward a **hybrid renderer**, the same core idea behind Lumen:

- Rasterize a G-buffer (position, normal, albedo, roughness) with the existing GL pipeline.
- Use GPU compute-shader ray tracing only for what rasterization is bad at: shadows,
  ambient occlusion, reflections, and global illumination.
- Avoid brute-force path tracing every pixel every frame — instead amortize GI cost
  over time using temporal accumulation and probe-based irradiance caching (DDGI-style),
  much like Lumen's surface cache and radiance probes.
- Denoise aggressively, since a handful of rays per pixel is the realistic real-time budget.

Hardware ray tracing (DXR / `VK_KHR_ray_tracing_pipeline`) is treated as an optional
stretch goal, not a prerequisite — Lumen itself defaults to software ray tracing against
distance fields, and OpenGL compute shaders can get us most of the way there without
switching graphics APIs.

## Roadmap

### Foundations
- [x] Introduce a proper `Material` system (albedo, roughness, metallic, emissive) on `SceneObject`
- [ ] Unify the scene representation used by the rasterizer and raytracer (currently duplicated —
      `Scene.Objects` vs. `Mesh`/`Transform` are kept in sync manually)
- [ ] Add triangle mesh support to the raytracer (currently only `Box` and `InfinitePlane`)
- [ ] Build a BVH (simple median-split builder is fine to start) for scene intersection instead of
      the current O(n) linear scan in `Scene.HitAnything`
- [x] Remove/replace stub placeholders (`FullscreenQuad.cs`, `MathHelper.cs`) once their real use lands
- [x] Cache the blit framebuffer instead of creating/deleting an FBO every frame in `OnRenderFrame`

### GPU Migration
- [x] Port `CpuRaytracer` logic into a GLSL compute shader (scene data as SSBOs: primitives, materials)
- [x] Write compute shader output directly to a storage texture/image for compositing
- [ ] Iterative (stack-based) BVH traversal in the compute shader for triangle meshes

### Hybrid Rendering (the "Lumen concept")
- [ ] Split rendering into a rasterized G-buffer pass + targeted ray passes (shadows, AO, reflections)
      instead of tracing the full image every frame
- [ ] Single shadow ray per pixel against the sun, replacing/augmenting the current shadow-ray logic
- [ ] Reflection rays limited to glossy/mirror-like surfaces only

### Temporal Stability
- [ ] Reprojection using camera motion + a velocity buffer
- [ ] Temporal accumulation (exponential history blending) before any spatial denoising
- [ ] Basic spatial denoiser (SVGF-lite or bilateral filter) for low sample counts

### Global Illumination
- [ ] Probe-based diffuse GI (DDGI-style): a world-space grid of irradiance probes,
      updated a handful per frame, sampled instead of recursive bounce tracing
- [ ] Octahedral encoding for probe storage

### Stretch Goals
- [ ] Evaluate hardware ray tracing via Vulkan (e.g. Silk.NET bindings) for BVH traversal acceleration
- [ ] Importance sampling / multiple light types beyond a single directional sun
