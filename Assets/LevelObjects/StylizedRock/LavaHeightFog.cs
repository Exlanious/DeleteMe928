using UnityEngine;

// Publishes the moving visual surface height; does not alter lava movement or collision.
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class LavaHeightFog : MonoBehaviour
{
    [SerializeField] private Renderer lavaSurface;
    [SerializeField] private Color fogColor = new Color(0.48f, 0.29f, 0.17f, 1f);
    [SerializeField, Min(0.1f)] private float fogHeight = 4f;
    [SerializeField, Range(0f, 1f)] private float density = 0.14f;
    [SerializeField, Range(0f, 1f)] private float contactSoftness = 0.65f;
    private static readonly int Parameters = Shader.PropertyToID("_LavaHeightFogParameters");
    private static readonly int FogColor = Shader.PropertyToID("_LavaHeightFogColor");

    private void OnEnable()
    {
        if (lavaSurface == null) lavaSurface = GetComponent<Renderer>();
        Publish();
    }

    // Runs after the lava's Update so the fog follows its current surface without a frame of lag.
    private void LateUpdate() { Publish(); }
    private void OnValidate() { if (isActiveAndEnabled) Publish(); }

    private void Publish()
    {
        if (lavaSurface == null) lavaSurface = GetComponent<Renderer>();
        float surface = lavaSurface != null ? lavaSurface.bounds.max.y : transform.position.y;
        Shader.SetGlobalVector(Parameters, new Vector4(surface, Mathf.Max(0.1f, fogHeight), density, contactSoftness));
        Shader.SetGlobalColor(FogColor, QualitySettings.activeColorSpace == ColorSpace.Linear ? fogColor.linear : fogColor);
    }

    private void OnDisable()
    {
        // Scene reloads and removal must not leave a stale fog layer in shader globals.
        Shader.SetGlobalVector(Parameters, new Vector4(0f, 1f, 0f, 0f));
    }
}
