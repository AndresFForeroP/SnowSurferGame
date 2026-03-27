using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class DynamicCloudSystem : MonoBehaviour
{
    private Sprite[] cloudSprites;
    private float spawnTimer;

    private class CloudData
    {
        public Transform transform;
        public float localSpeed;
    }

    private List<CloudData> activeClouds = new List<CloudData>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        // Subscribe to scene loads so it sets up the clouds automatically
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (Camera.main != null)
        {
            // Only add if not already present
            if (Camera.main.GetComponentInChildren<DynamicCloudSystem>() == null)
            {
                GameObject cloudSystemObj = new GameObject("DynamicCloudSystem");
                cloudSystemObj.transform.SetParent(Camera.main.transform);
                cloudSystemObj.transform.localPosition = Vector3.zero;
                cloudSystemObj.AddComponent<DynamicCloudSystem>();
            }
        }
    }

    private void Start()
    {
        // Destroy old manual clouds from the scene to prevent weird overlapping behavior
        GameObject[] objects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        foreach (GameObject obj in objects)
        {
            // Only destroy if it belongs to the old system (not our new child clouds)
            if (obj.name.Contains("Cloud") && obj.transform.parent != this.transform)
            {
                Destroy(obj);
            }
        }

        // Load cloud sprites from Resources folder
        cloudSprites = new Sprite[3];
        cloudSprites[0] = Resources.Load<Sprite>("Cloud 1");
        cloudSprites[1] = Resources.Load<Sprite>("Cloud 2");
        cloudSprites[2] = Resources.Load<Sprite>("Cloud 3");

        // Pre-warm the sky with initial clouds
        for (int i = 0; i < 15; i++)
        {
            SpawnCloud(Random.Range(-25f, 25f));
        }
    }

    private void Update()
    {
        if (cloudSprites == null || cloudSprites[0] == null) return;

        // Spawning logic
        spawnTimer -= Time.deltaTime;
        if (spawnTimer <= 0f)
        {
            spawnTimer = Random.Range(1.5f, 3.5f);
            // Spawn just off-screen to the right
            SpawnCloud(25f);
        }

        // Movement logic
        for (int i = activeClouds.Count - 1; i >= 0; i--)
        {
            CloudData cloud = activeClouds[i];
            
            // Move cloud left in local space
            cloud.transform.localPosition += Vector3.left * cloud.localSpeed * Time.deltaTime;

            // Destroy if way off-screen to the left
            if (cloud.transform.localPosition.x < -25f)
            {
                Destroy(cloud.transform.gameObject);
                activeClouds.RemoveAt(i);
            }
        }
    }

    private void SpawnCloud(float startX)
    {
        // Safety check to ensure sprites are loaded
        if (cloudSprites[0] == null) return;

        GameObject cloudObj = new GameObject("DynamicCloud");
        cloudObj.transform.SetParent(this.transform);

        SpriteRenderer sr = cloudObj.AddComponent<SpriteRenderer>();
        // Assign a random cloud sprite
        sr.sprite = cloudSprites[Random.Range(0, cloudSprites.Length)];
        sr.sortingOrder = -20; // Ensure it stays behind game elements

        // Determine distance (Scale)
        // Smaller clouds = further away = slower local speed
        // Larger clouds = closer = faster local speed
        float distanceFactor = Random.Range(0.4f, 1.8f);
        
        cloudObj.transform.localScale = new Vector3(distanceFactor, distanceFactor, 1f);

        // Opacity adds depth
        sr.color = new Color(1f, 1f, 1f, Random.Range(0.5f, 0.9f));

        // Random height between -2 and 10 locally
        float startY = Random.Range(-2f, 12f);
        
        // Push z slightly back naturally
        cloudObj.transform.localPosition = new Vector3(startX, startY, 15f);

        // Parallax speed relative to distance factor
        // E.g., slow = 1 unit/sec, fast = 4 units/sec (moving left locally)
        float parallaxSpeed = Mathf.Lerp(0.5f, 4.0f, (distanceFactor - 0.4f) / 1.4f);

        activeClouds.Add(new CloudData()
        {
            transform = cloudObj.transform,
            localSpeed = parallaxSpeed
        });
    }
}
