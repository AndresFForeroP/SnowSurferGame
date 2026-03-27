using UnityEngine;

/// <summary>
/// Creates snow spray particles at the player's feet when grounded.
/// Particles simulate snow being kicked up by the snowboard dragging on the surface.
/// Also has an optional trail renderer for board track effect.
/// </summary>
public class SnowEffects : MonoBehaviour
{
    [Header("Snow Spray (at feet)")]
    [SerializeField] private int maxParticles = 150;
    [SerializeField] private Color snowParticleColor = new Color(0.92f, 0.96f, 1f, 0.9f);
    [SerializeField] private float baseEmissionRate = 40f;
    [SerializeField] private float maxEmissionRate = 120f;

    [Header("Trail")]
    [SerializeField] private bool enableTrail = true;
    [SerializeField] private float trailTime = 0.4f;
    [SerializeField] private float trailWidth = 0.25f;
    [SerializeField] private Color trailColor = new Color(0.85f, 0.92f, 1f, 0.4f);

    private SnowboarderController playerController;
    private ParticleSystem snowParticles;
    private ParticleSystem.EmissionModule emission;
    private TrailRenderer trail;

    private void Start()
    {
        playerController = GetComponent<SnowboarderController>();
        CreateSnowSprayAtFeet();

        if (enableTrail)
            CreateTrailRenderer();
    }

    private void Update()
    {
        if (playerController == null) return;

        bool shouldEmit = playerController.IsGrounded && playerController.IsAlive;

        if (shouldEmit)
        {
            float rate = Mathf.Lerp(baseEmissionRate, maxEmissionRate, playerController.NormalizedSpeed);
            emission.rateOverTime = rate;
        }
        else
        {
            emission.rateOverTime = 0f;
        }

        if (trail != null)
        {
            trail.emitting = shouldEmit;
        }
    }

    /// <summary>
    /// Create a particle system positioned at the player's feet.
    /// Snow sprays backward and upward to simulate dragging the board on snow.
    /// </summary>
    private void CreateSnowSprayAtFeet()
    {
        GameObject particleObj = new GameObject("SnowSprayFeet");
        particleObj.transform.SetParent(transform);
        // Position at the bottom of the player (feet level)
        particleObj.transform.localPosition = new Vector3(-0.3f, -0.5f, 0);

        snowParticles = particleObj.AddComponent<ParticleSystem>();

        // Main module
        var main = snowParticles.main;
        main.maxParticles = maxParticles;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.18f);
        main.startColor = snowParticleColor;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0.4f;

        // Emission
        emission = snowParticles.emission;
        emission.rateOverTime = 0f;

        // Shape: emit in a cone pointing backward-upward (simulates snow spray)
        var shape = snowParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 40f;
        shape.radius = 0.15f;
        // Rotate to spray backward and upward (behind the player, upward splash)
        shape.rotation = new Vector3(0, 0, 140f);

        // Size over lifetime: shrink out
        var sol = snowParticles.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0, 0.8f),
            new Keyframe(0.3f, 1f),
            new Keyframe(1, 0)
        ));

        // Color over lifetime: white → transparent
        var col = snowParticles.colorOverLifetime;
        col.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(Color.white, 0),
                new GradientColorKey(new Color(0.9f, 0.95f, 1f), 1)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0.9f, 0),
                new GradientAlphaKey(0.6f, 0.4f),
                new GradientAlphaKey(0f, 1)
            }
        );
        col.color = grad;

        // Velocity over lifetime: snow drifts upward and backward
        var vol = snowParticles.velocityOverLifetime;
        vol.enabled = true;
        vol.x = new ParticleSystem.MinMaxCurve(-2f, -0.5f); // drift backward
        vol.y = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);  // drift upward
        vol.z = new ParticleSystem.MinMaxCurve(0f, 0f);      // no Z movement

        // Renderer
        var renderer = snowParticles.GetComponent<ParticleSystemRenderer>();
        renderer.material = new Material(Shader.Find("Sprites/Default"));
        renderer.material.color = snowParticleColor;
        renderer.sortingOrder = 4;
    }

    /// <summary>
    /// Create a trail renderer at the board contact point.
    /// </summary>
    private void CreateTrailRenderer()
    {
        GameObject trailObj = new GameObject("BoardTrail");
        trailObj.transform.SetParent(transform);
        trailObj.transform.localPosition = new Vector3(0, -0.5f, 0);

        trail = trailObj.AddComponent<TrailRenderer>();
        trail.time = trailTime;
        trail.startWidth = trailWidth;
        trail.endWidth = 0.02f;
        trail.material = new Material(Shader.Find("Sprites/Default"));
        trail.startColor = trailColor;
        trail.endColor = new Color(trailColor.r, trailColor.g, trailColor.b, 0f);
        trail.sortingOrder = 1;
        trail.minVertexDistance = 0.1f;
    }
}
