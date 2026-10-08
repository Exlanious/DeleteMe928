using UnityEngine;
using UnityEngine.Rendering;

public class LavaEmbers : MonoBehaviour
{
    [SerializeField] Material emberMaterial;

    [Header("Adjust")]
    [Tooltip("Emission rate. 1 matches the current density.")]
    [SerializeField, Min(0f)] float density = 1f;
    [Tooltip("Particle size. 1 matches the current size.")]
    [SerializeField, Min(0f)] float size = 1f;
    [Tooltip("HDR color multiplier. 1 matches the current brightness.")]
    [SerializeField, Min(0f)] float brightness = 1f;
    [Tooltip("How high particles rise. 1 matches the current height.")]
    [SerializeField, Min(0f)] float popHeight = 1f;

    Material runtimeMaterial;

    void Awake()
    {
        FitAboveLava();
        runtimeMaterial = CreateRuntimeMaterial();
        Build("Embers", true);
        Build("Sparks", false);
    }

    void OnValidate()
    {
        if (!Application.isPlaying || runtimeMaterial == null)
            return;

        Build("Embers", true);
        Build("Sparks", false);
    }

    void OnDestroy()
    {
        if (runtimeMaterial != null)
            Destroy(runtimeMaterial);
    }

    // The lava object is a scaled cube. Counter-scale this child so particle
    // sizes stay in meters, then sit the emitter on the top face.
    void FitAboveLava()
    {
        Transform lava = transform.parent;
        if (lava == null)
            return;

        Vector3 scale = lava.localScale;
        transform.localRotation = Quaternion.identity;
        transform.localScale = new Vector3(
            scale.x != 0f ? 1f / scale.x : 1f,
            scale.y != 0f ? 1f / scale.y : 1f,
            scale.z != 0f ? 1f / scale.z : 1f);
        transform.localPosition = new Vector3(0f, 0.58f, 0f);
    }

    void Build(string objectName, bool embers)
    {
        Transform existing = transform.Find(objectName);
        GameObject go = existing != null ? existing.gameObject : new GameObject(objectName);
        if (existing == null)
            go.transform.SetParent(transform, false);

        ParticleSystem particles = go.GetComponent<ParticleSystem>();
        if (particles == null)
            particles = go.AddComponent<ParticleSystem>();

        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (embers)
            ConfigureEmbers(particles);
        else
            ConfigureSparks(particles);

        ApplySharedSettings(particles);
        particles.Play();
    }

    void ConfigureEmbers(ParticleSystem particles)
    {
        ParticleSystem.MainModule main = particles.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.8f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.14f * size, 0.42f * size);
        main.startColor = new ParticleSystem.MinMaxGradient(
            ScaleBrightness(new Color(1.8f, 0.28f, 0.02f, 1f)),
            ScaleBrightness(new Color(3.2f, 1.05f, 0.2f, 1f)));
        main.gravityModifier = 0.18f;
        main.maxParticles = ScaledCount(400);

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 100f * density;

        ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(0.05f, 0.45f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.55f * PopScale, 1.5f * PopScale);
        velocity.z = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = EmberFade();

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.35f),
            new Keyframe(0.18f, 1f),
            new Keyframe(1f, 0.15f)));

        ParticleSystem.RotationOverLifetimeModule rotation = particles.rotationOverLifetime;
        rotation.enabled = true;
        rotation.separateAxes = false;
        rotation.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);

        ParticleSystem.NoiseModule noise = particles.noise;
        noise.enabled = true;
        noise.strength = 0.3f;
        noise.frequency = 0.35f;
        noise.scrollSpeed = 0.25f;
        noise.damping = true;
        noise.octaveCount = 1;
    }

    void ConfigureSparks(ParticleSystem particles)
    {
        ParticleSystem.MainModule main = particles.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.95f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f * size, 0.16f * size);
        main.startColor = new ParticleSystem.MinMaxGradient(
            ScaleBrightness(new Color(3.2f, 0.55f, 0.05f, 1f)),
            ScaleBrightness(new Color(5f, 2.1f, 0.45f, 1f)));
        main.gravityModifier = 1.15f;
        main.maxParticles = ScaledCount(60);

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 14f * density;

        ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.3f, 0.9f);
        velocity.y = new ParticleSystem.MinMaxCurve(2.4f * PopScale, 4.6f * PopScale);
        velocity.z = new ParticleSystem.MinMaxCurve(-0.45f, 0.45f);

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = SparkFade();

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(1f, 0.05f)));

        ParticleSystem.NoiseModule noise = particles.noise;
        noise.enabled = false;
        ParticleSystem.RotationOverLifetimeModule rotation = particles.rotationOverLifetime;
        rotation.enabled = false;
    }

    void ApplySharedSettings(ParticleSystem particles)
    {
        ParticleSystem.MainModule main = particles.main;
        main.loop = true;
        main.playOnAwake = true;
        main.duration = 5f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

        Vector3 lavaSize = transform.parent != null ? transform.parent.localScale : new Vector3(60f, 1f, 85f);
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(Mathf.Abs(lavaSize.x), 0.12f, Mathf.Abs(lavaSize.z));

        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = runtimeMaterial;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    Material CreateRuntimeMaterial()
    {
        Material source = emberMaterial;
        if (source == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            source = new Material(shader);
        }

        Material material = new Material(source);
        material.name = "Lava Ember Runtime";
        return material;
    }

    // Height grows with the square of upward speed, so scale speed by the square root.
    float PopScale => Mathf.Sqrt(popHeight);

    int ScaledCount(int baseCount)
    {
        return Mathf.Max(1, Mathf.RoundToInt(baseCount * density));
    }

    Color ScaleBrightness(Color color)
    {
        return new Color(color.r * brightness, color.g * brightness, color.b * brightness, color.a);
    }

    static ParticleSystem.MinMaxGradient EmberFade()
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.92f, 0.55f), 0f),
                new GradientColorKey(new Color(1f, 0.38f, 0.04f), 0.4f),
                new GradientColorKey(new Color(0.45f, 0.04f, 0.01f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.08f),
                new GradientAlphaKey(0.7f, 0.55f),
                new GradientAlphaKey(0f, 1f)
            });
        return gradient;
    }

    static ParticleSystem.MinMaxGradient SparkFade()
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.95f, 0.7f), 0f),
                new GradientColorKey(new Color(1f, 0.28f, 0.03f), 0.35f),
                new GradientColorKey(new Color(0.35f, 0.02f, 0f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 0.2f),
                new GradientAlphaKey(0f, 1f)
            });
        return gradient;
    }
}
