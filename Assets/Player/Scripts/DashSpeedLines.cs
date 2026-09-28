using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Streaks that rush past the camera while dashing. Builds its own particle system at runtime,
/// and gets stronger or weaker with the current dash speed.
/// </summary>
public class DashSpeedLines : MonoBehaviour
{
    [SerializeField] private Color lineColor = new Color(1f, 1f, 1f, 0.6f);
    [SerializeField] private float maxEmissionRate = 250f; //lines per second at the very start of a dash
    [SerializeField] private float lineSpeed = 60f; //how fast the lines fly past the camera
    [SerializeField] private float lineWidth = 0.03f;
    [SerializeField] private float lineStretch = 0.04f; //line length = speed * this
    [SerializeField] private float spawnDistance = 12f; //how far ahead of the camera the lines start
    [SerializeField] private float spawnRadius = 2.5f; //size of the ring the lines spawn in
    [SerializeField, Range(0f, 1f)] private float ringThickness = 0.5f; //0 = lines only on the outer edge, 1 = lines can go through the center
    [SerializeField, Range(0f, 1f)] private float minIntensity = 0.05f; //below this fraction of dash speed, no lines spawn
    [SerializeField] private Material lineMaterial; //optional, a soft additive material is made if this is empty

    private PlayerMovement movement;
    private Transform cameraTransform;
    private ParticleSystem lines;
    private Vector3 lastDashDirection = Vector3.forward;

    void Start()
    {
        //get the movement script and the camera
        movement = GetComponent<PlayerMovement>();
        cameraTransform = GetComponentInChildren<Camera>().transform;

        lines = CreateParticleSystem();
    }

    void LateUpdate()
    {
        //how strong the effect should be, 1 at the start of a dash fading to 0 as it slows down
        Vector3 dashVelocity = movement.DashVelocity;
        float intensity = movement.DashStartSpeed > 0 ? Mathf.Clamp01(dashVelocity.magnitude / movement.DashStartSpeed) : 0f;

        //remember the dash direction so the lines don't snap around when the dash stops
        if (dashVelocity.sqrMagnitude > 0.01f) {
            lastDashDirection = dashVelocity.normalized;
        }

        //place the emitter ahead of the camera in the dash direction, pointing back at the camera
        lines.transform.SetPositionAndRotation(cameraTransform.position + lastDashDirection * spawnDistance, Quaternion.LookRotation(-lastDashDirection));

        //spawn more lines the faster we're dashing
        ParticleSystem.EmissionModule emission = lines.emission;
        emission.rateOverTime = intensity > minIntensity ? maxEmissionRate * intensity : 0f;

        //newly spawned lines get fainter as the dash slows down
        ParticleSystem.MainModule main = lines.main;
        main.startColor = new Color(lineColor.r, lineColor.g, lineColor.b, lineColor.a * intensity);
    }

    /// <summary>
    /// Build the particle system that draws the speed lines
    /// </summary>
    ParticleSystem CreateParticleSystem() {
        GameObject linesObject = new GameObject("DashSpeedLines");
        linesObject.transform.SetParent(transform, false);
        ParticleSystem system = linesObject.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); //stop so the settings below can be changed

        //lines fly from the spawn ring to just behind the camera, then disappear
        ParticleSystem.MainModule main = system.main;
        main.loop = true;
        main.playOnAwake = false;
        main.startLifetime = (spawnDistance + 2f) / lineSpeed;
        main.startSpeed = lineSpeed;
        main.startSize = lineWidth;
        main.maxParticles = 1000;
        main.simulationSpace = ParticleSystemSimulationSpace.Local; //lines move with the player so they always rush past the camera

        //spawn in a ring and fly straight toward the camera
        ParticleSystem.ShapeModule shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 0f;
        shape.radius = spawnRadius;
        shape.radiusThickness = ringThickness;

        //nothing spawns until we dash
        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = 0f;

        //fade each line in and out so they don't pop
        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = system.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient fade = new Gradient();
        fade.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = fade;

        //stretch each particle along its velocity so it looks like a line
        ParticleSystemRenderer particleRenderer = linesObject.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Stretch;
        particleRenderer.velocityScale = lineStretch;
        particleRenderer.lengthScale = 1f;
        particleRenderer.shadowCastingMode = ShadowCastingMode.Off;
        particleRenderer.receiveShadows = false;
        particleRenderer.material = lineMaterial != null ? lineMaterial : CreateLineMaterial();

        system.Play();
        return system;
    }

    /// <summary>
    /// Make a soft, additive URP particle material for the lines
    /// </summary>
    Material CreateLineMaterial() {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) {
            Debug.LogWarning("DashSpeedLines: URP particle shader not found, assign a Line Material in the Inspector.");
            return null;
        }

        //set the material up as transparent with additive blending
        Material material = new Material(shader);
        material.SetFloat("_Surface", 1f); //transparent
        material.SetFloat("_Blend", 2f); //additive
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)BlendMode.One);
        material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        material.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
        material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;
        material.SetTexture("_BaseMap", CreateLineTexture());
        return material;
    }

    /// <summary>
    /// Make a small texture that is bright in the middle and fades out to every edge, so lines have soft tapered ends
    /// </summary>
    Texture2D CreateLineTexture() {
        const int size = 32;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        for (int x = 0; x < size; x++) {
            for (int y = 0; y < size; y++) {
                float u = 1f - Mathf.Abs((x + 0.5f) / size * 2f - 1f); //1 in the middle, 0 at the edges
                float v = 1f - Mathf.Abs((y + 0.5f) / size * 2f - 1f);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, u) * Mathf.SmoothStep(0f, 1f, v)));
            }
        }
        texture.Apply();
        return texture;
    }
}
