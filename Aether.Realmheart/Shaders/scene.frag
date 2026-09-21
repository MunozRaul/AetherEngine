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
}
