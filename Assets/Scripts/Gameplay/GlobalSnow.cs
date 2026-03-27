using UnityEngine;

/// <summary>
/// Automatically creates a global falling snow particle system 
/// attached to the Main Camera.
/// </summary>
public class GlobalSnow : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        // Subscribe to scene loaded so we can find camera when it's ready
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            if (mainCam.GetComponentInChildren<GlobalSnow>() == null)
            {
                GameObject snowObj = new GameObject("GlobalSnowParticles");
                snowObj.transform.SetParent(mainCam.transform);
                snowObj.transform.localPosition = new Vector3(0, 10f, 10f); // Above and a bit forward
                snowObj.AddComponent<GlobalSnow>();
                
                CreateSnowParticleSystem(snowObj);
            }
        }
    }

    private static void CreateSnowParticleSystem(GameObject obj)
    {
        ParticleSystem ps = obj.AddComponent<ParticleSystem>();

        // Main
        var main = ps.main;
        main.maxParticles = 800;
        main.startLifetime = new ParticleSystem.MinMaxCurve(8f, 12f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
        main.startColor = new Color(1f, 1f, 1f, 0.8f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0.5f; // Fall down

        // Emission
        var emission = ps.emission;
        emission.rateOverTime = 120f;

        // Shape: A wide box above the screen
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(40f, 2f, 20f);
        // Point downwards with slight wind angle
        shape.rotation = new Vector3(80f, 10f, 0f);

        // Velocity (Wind)
        var vol = ps.velocityOverLifetime;
        vol.enabled = true;
        vol.x = new ParticleSystem.MinMaxCurve(-3f, -1f);
        vol.y = new ParticleSystem.MinMaxCurve(-2f, -1f);
        vol.z = new ParticleSystem.MinMaxCurve(0f, 0f); // Prevents "Curve must be in the same mode" Unity error

        var renderer = obj.GetComponent<ParticleSystemRenderer>();
        renderer.material = new Material(Shader.Find("Sprites/Default"));
        renderer.sortingOrder = 10; // In front of most things but back enough
    }
}
