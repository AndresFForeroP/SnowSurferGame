using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Procedural terrain generator:
/// - 600m undulating track with finish line
/// - Deterministic cliff gaps every ~70m (player must jump over)
/// - Trees and rocks sunk into terrain (only canopy visible)
/// - Static background clouds (created once, don't follow player)
/// - Flat terrain after finish for deceleration
/// - Speed boost powerups
/// </summary>
public class TerrainGenerator : MonoBehaviour
{
    [Header("Track Settings")]
    [SerializeField] private float segmentLength = 25f;
    [SerializeField] private int pointsPerSegment = 16;
    [SerializeField] private float trackTotalLength = 600f;

    [Header("Slope Shape")]
    [SerializeField] private float baseDescentRate = -0.15f;
    [SerializeField] private float waveAmplitude = 2.5f;
    [SerializeField] private float waveFrequency = 0.06f;
    [SerializeField] private float noiseAmplitude = 0.4f;

    [Header("Difficulty Ramp")]
    [SerializeField] private int flatStartSegments = 4;
    [SerializeField] private float maxWaveAmplitude = 4f;
    [SerializeField] private float difficultyRampRate = 0.0015f;

    [Header("Visual")]
    [SerializeField] private Color snowSurfaceColor = new Color(0.95f, 0.97f, 1f, 1f);
    [SerializeField] private Color snowFillColor = new Color(0.85f, 0.9f, 1f, 1f);
    [SerializeField] private float surfaceLineWidth = 0.4f;

    [Header("Obstacles")]
    [SerializeField] private GameObject[] obstaclePrefabs;
    [SerializeField] private float obstacleSpawnChance = 0.15f;
    [SerializeField] private float minObstacleSpacing = 15f;

    [Header("Cliff Gaps")]
    [SerializeField] private float cliffGapWidth = 9f;
    [SerializeField] private float cliffInterval = 70f;
    [SerializeField] private float firstCliffAtX = 100f;

    [Header("Decorations")]
    [SerializeField] private float treeSpawnChance = 0.25f;
    [SerializeField] private float rockSpawnChance = 0.15f;
    [SerializeField] private float minDecoSpacing = 8f;

    [Header("Clouds")]
    [SerializeField] private int cloudCount = 25;
    [SerializeField] private float cloudMinY = 8f;
    [SerializeField] private float cloudMaxY = 18f;

    [Header("Collectibles & Powerups")]
    [SerializeField] private float collectibleSpawnChance = 0.15f;
    [SerializeField] private float collectibleHeight = 1.5f;
    [SerializeField] private float speedBoostChance = 0.06f;
    [SerializeField] private float speedBoostHeight = 1.8f;

    [Header("Post-Finish")]
    [SerializeField] private float postFinishFlatLength = 80f;

    [Header("Ground Layer")]
    [SerializeField] private LayerMask groundLayerMask;

    private class SegmentData
    {
        public GameObject go;
        public float startX;
        public float endX;
    }

    private List<SegmentData> activeSegments = new List<SegmentData>();
    private float nextSegmentStartX;
    private float nextSegmentStartY;
    private float lastObstacleX = -100f;
    private float lastTreeX = -100f;
    private float lastRockX = -100f;
    private float lastSpeedBoostX = -200f;
    private Transform playerTransform;
    private int groundLayerIndex;
    private int segmentCount;
    private bool trackComplete;
    private bool finishLineSpawned;
    private bool cloudsSpawned;
    private List<float> cliffPositions = new List<float>();

    private Material surfaceLineMaterial;
    private Material fillMaterial;
    private Sprite[] treeSprites;
    private Sprite[] rockSprites;
    private Sprite[] cloudSprites;
    private Sprite giftBagSprite;
    private Sprite finishFlagSprite;

    public float FinishLineX => trackTotalLength;
    public float TrackLength => trackTotalLength;
    public System.Action OnFinishLineReached;

    private void Start()
    {
        groundLayerIndex = LayerMask.NameToLayer("Default");
        for (int i = 0; i < 32; i++)
        {
            if ((groundLayerMask.value & (1 << i)) != 0) { groundLayerIndex = i; break; }
        }

        surfaceLineMaterial = new Material(Shader.Find("Sprites/Default"));
        surfaceLineMaterial.color = snowSurfaceColor;
        fillMaterial = new Material(Shader.Find("Sprites/Default"));
        fillMaterial.color = snowFillColor;

        LoadSprites();

        // Pre-calculate deterministic cliff positions
        for (float x = firstCliffAtX; x < trackTotalLength * 0.9f; x += cliffInterval + Random.Range(-10f, 10f))
        {
            cliffPositions.Add(x);
        }

        SnowboarderController player = FindFirstObjectByType<SnowboarderController>();
        if (player != null)
            playerTransform = player.transform;

        nextSegmentStartX = -25f;
        nextSegmentStartY = 3f;

        int initialCount = Mathf.Min(15, Mathf.CeilToInt((trackTotalLength + postFinishFlatLength) / segmentLength));
        for (int i = 0; i < initialCount; i++)
        {
            if (nextSegmentStartX >= trackTotalLength + postFinishFlatLength) { trackComplete = true; break; }
            SpawnSegment(i < flatStartSegments);
        }

        // Spawn clouds ONCE as static background
        SpawnStaticClouds();
    }

    private void LoadSprites()
    {
        List<Sprite> trees = new List<Sprite>();
        for (int i = 1; i <= 4; i++)
        {
            Sprite s = LoadSprite($"Assets/Snow+Surfer+Sprite+Assets+V1/Tree {i}.png");
            if (s != null) trees.Add(s);
        }
        treeSprites = trees.ToArray();

        List<Sprite> rocks = new List<Sprite>();
        for (int i = 1; i <= 2; i++)
        {
            Sprite s = LoadSprite($"Assets/Snow+Surfer+Sprite+Assets+V1/Rock {i}.png");
            if (s != null) rocks.Add(s);
        }
        rockSprites = rocks.ToArray();

        List<Sprite> clouds = new List<Sprite>();
        for (int i = 1; i <= 3; i++)
        {
            Sprite s = LoadSprite($"Assets/Snow+Surfer+Sprite+Assets+V1/Cloud {i}.png");
            if (s != null) clouds.Add(s);
        }
        cloudSprites = clouds.ToArray();

        giftBagSprite = LoadSprite("Assets/Snow+Surfer+Sprite+Assets+V1/Gift Bag.png");
        finishFlagSprite = LoadSprite("Assets/Snow+Surfer+Sprite+Assets+V1/Post 1.png");
    }

    private Sprite LoadSprite(string path)
    {
#if UNITY_EDITOR
        return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
#else
        return null;
#endif
    }

    private void Update()
    {
        if (playerTransform == null) return;

        if (!trackComplete)
        {
            float lookAheadDistance = segmentLength * 8;
            while (nextSegmentStartX < playerTransform.position.x + lookAheadDistance)
            {
                if (nextSegmentStartX >= trackTotalLength + postFinishFlatLength)
                {
                    trackComplete = true; break;
                }
                bool isPostFinish = nextSegmentStartX >= trackTotalLength;
                SpawnSegment(segmentCount < flatStartSegments || isPostFinish);
            }
        }

        if (!finishLineSpawned && playerTransform.position.x >= trackTotalLength - segmentLength * 4)
            SpawnFinishLine();

        if (finishLineSpawned && playerTransform.position.x >= trackTotalLength)
        {
            OnFinishLineReached?.Invoke();
            finishLineSpawned = false;
        }

        // Cleanup behind player
        float cleanupDistance = 60f;
        for (int i = activeSegments.Count - 1; i >= 0; i--)
        {
            var seg = activeSegments[i];
            if (seg.go == null) { activeSegments.RemoveAt(i); continue; }
            if (seg.endX < playerTransform.position.x - cleanupDistance)
            {
                Destroy(seg.go);
                activeSegments.RemoveAt(i);
            }
        }
    }

    // ===================== STATIC BACKGROUND CLOUDS =====================

    private void SpawnStaticClouds()
    {
        if (cloudsSpawned || cloudSprites == null || cloudSprites.Length == 0) return;
        cloudsSpawned = true;

        // Create a parent that does NOT get cleaned up
        GameObject cloudParent = new GameObject("BackgroundClouds");
        // NOT parented to terrain — stays in scene root

        float trackLen = trackTotalLength + postFinishFlatLength;
        float spacing = trackLen / cloudCount;

        for (int i = 0; i < cloudCount; i++)
        {
            float x = -20f + spacing * i + Random.Range(-10f, 10f);
            float y = Random.Range(cloudMinY, cloudMaxY);

            Sprite sprite = cloudSprites[Random.Range(0, cloudSprites.Length)];
            float scale = Random.Range(0.7f, 1.5f);

            GameObject cloud = new GameObject($"Cloud_{i}");
            cloud.transform.parent = cloudParent.transform;
            cloud.transform.position = new Vector3(x, y, 0);
            cloud.transform.localScale = Vector3.one * scale;

            SpriteRenderer sr = cloud.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = -5;
            sr.color = new Color(1f, 1f, 1f, Random.Range(0.75f, 1f));
            if (Random.value > 0.5f) sr.flipX = true;
        }
    }

    // ===================== SEGMENT SPAWNING =====================

    private void SpawnSegment(bool isFlat)
    {
        segmentCount++;

        float endOfTrack = trackTotalLength + postFinishFlatLength;
        float actualLength = segmentLength;
        if (nextSegmentStartX + actualLength > endOfTrack + 10f)
        {
            actualLength = (endOfTrack + 10f) - nextSegmentStartX;
            if (actualLength < 5f) return;
        }

        // Is this segment post-finish? Generate flat terrain
        bool isPostFinish = nextSegmentStartX >= trackTotalLength - 10f;

        // Check if a cliff should appear in this segment
        bool hasCliff = false;
        float cliffCenterX = 0;

        if (!isFlat && !isPostFinish)
        {
            foreach (float cx in cliffPositions)
            {
                if (cx >= nextSegmentStartX && cx < nextSegmentStartX + actualLength)
                {
                    hasCliff = true;
                    cliffCenterX = cx;
                    break;
                }
            }
        }

        Vector2[] allPoints = GenerateTerrainPoints(isFlat || isPostFinish, actualLength);

        if (hasCliff)
        {
            float cliffStart = cliffCenterX - cliffGapWidth * 0.5f;
            float cliffEnd = cliffCenterX + cliffGapWidth * 0.5f;

            List<Vector2> before = new List<Vector2>();
            List<Vector2> after = new List<Vector2>();
            float cliffY = 0;

            for (int i = 0; i < allPoints.Length; i++)
            {
                if (allPoints[i].x < cliffStart)
                    before.Add(allPoints[i]);
                else if (allPoints[i].x > cliffEnd)
                    after.Add(allPoints[i]);
                else if (cliffY == 0)
                    cliffY = allPoints[i].y;
            }

            if (cliffY == 0 && allPoints.Length > 0)
                cliffY = allPoints[allPoints.Length / 2].y;

            // Add edge points right at cliff edges for crisp gap
            if (before.Count > 0)
            {
                float edgeY = before[before.Count - 1].y;
                before.Add(new Vector2(cliffStart, edgeY));
            }
            if (after.Count > 0)
            {
                float edgeY = after[0].y;
                after.Insert(0, new Vector2(cliffEnd, edgeY));
            }

            if (before.Count >= 2)
            {
                GameObject seg1 = CreateTerrainSegmentObject($"Terrain_{segmentCount}a", before.ToArray());
                activeSegments.Add(new SegmentData { go = seg1, startX = before[0].x, endX = before[before.Count - 1].x });
            }

            if (after.Count >= 2)
            {
                GameObject seg2 = CreateTerrainSegmentObject($"Terrain_{segmentCount}b", after.ToArray());
                activeSegments.Add(new SegmentData { go = seg2, startX = after[0].x, endX = after[after.Count - 1].x });
            }

            CreateCliffKillZone(cliffStart, cliffEnd, cliffY);
        }
        else
        {
            GameObject segment = CreateTerrainSegmentObject($"Terrain_{segmentCount}", allPoints);
            activeSegments.Add(new SegmentData { go = segment, startX = allPoints[0].x, endX = allPoints[allPoints.Length - 1].x });
        }

        float distRatio = nextSegmentStartX / trackTotalLength;

        // Obstacles (not near cliffs, not post-finish)
        if (!isFlat && !isPostFinish && segmentCount > flatStartSegments + 2 && distRatio < 0.95f)
            SpawnObstaclesOnSegment(allPoints);

        // Decorations (trees, rocks)
        if (segmentCount > 1 && !isPostFinish)
        {
            SpawnTreesOnSegment(allPoints);
            SpawnRocksOnSegment(allPoints);
        }

        // Boosts
        if (!isFlat && !isPostFinish && segmentCount > flatStartSegments)
        {
            SpawnSpeedBoostsOnSegment(allPoints);
        }

        nextSegmentStartX = allPoints[allPoints.Length - 1].x;
        nextSegmentStartY = allPoints[allPoints.Length - 1].y;
    }

    private GameObject CreateTerrainSegmentObject(string name, Vector2[] points)
    {
        GameObject segment = new GameObject(name);
        segment.layer = groundLayerIndex;
        segment.transform.parent = transform;

        EdgeCollider2D ec = segment.AddComponent<EdgeCollider2D>();
        ec.points = points;

        CreateSurfaceLine(segment, points);
        CreateTerrainFill(segment, points);
        return segment;
    }

    // ===================== TERRAIN GENERATION =====================

    private Vector2[] GenerateTerrainPoints(bool isFlat, float length)
    {
        Vector2[] points = new Vector2[pointsPerSegment];
        float sX = nextSegmentStartX;
        float sY = nextSegmentStartY;
        float stepX = length / (pointsPerSegment - 1);

        float progress = Mathf.Clamp01(nextSegmentStartX / trackTotalLength);
        float difficultyMult = 1f + (segmentCount * difficultyRampRate);
        float currentAmplitude = Mathf.Min(waveAmplitude * difficultyMult, maxWaveAmplitude);

        if (progress > 0.85f)
            currentAmplitude *= (1f - Mathf.InverseLerp(0.85f, 1f, progress) * 0.9f);

        for (int i = 0; i < pointsPerSegment; i++)
        {
            float x = sX + stepX * i;
            float relX = stepX * i;
            float y;

            if (isFlat)
                y = sY + Mathf.Sin(x * 0.03f) * 0.05f; // nearly flat
            else
            {
                float descent = relX * baseDescentRate;
                float wave = Mathf.Sin(x * waveFrequency) * currentAmplitude;
                float noise = (Mathf.PerlinNoise(x * 0.03f, 0.5f) - 0.5f) * noiseAmplitude;
                y = sY + descent + wave + noise;
            }
            points[i] = new Vector2(x, y);
        }

        // Ensure start continuity
        points[0] = new Vector2(sX, sY);
        for (int i = 1; i < Mathf.Min(3, pointsPerSegment); i++)
        {
            float blend = (float)i / 3f;
            points[i] = new Vector2(points[i].x, Mathf.Lerp(sY, points[i].y, blend));
        }

        return points;
    }

    // ===================== CLIFF HAZARDS =====================

    private void CreateCliffKillZone(float startX, float endX, float y)
    {
        GameObject kz = new GameObject("CliffKillZone");
        kz.transform.parent = transform;
        kz.transform.position = new Vector3((startX + endX) / 2f, y - 12f, 0);
        kz.tag = "Obstacle";

        BoxCollider2D col = kz.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(endX - startX + 2f, 18f);
    }

    private void CreateCliffWarning(float x, float y)
    {
        // Removed warning triangles per user request
    }

    // ===================== TREES (dynamic height + forests) =====================

    private void SpawnTreesOnSegment(Vector2[] points)
    {
        if (treeSprites == null || treeSprites.Length == 0) return;

        bool isForest = Random.value > 0.7f; // 30% chance this segment is a dense forest
        float localChance = isForest ? 0.85f : treeSpawnChance;
        float spacingMultiplier = isForest ? 0.4f : 1f;

        for (int i = 1; i < points.Length - 1; i += (isForest ? 1 : 2))
        {
            float x = points[i].x;
            if (x - lastTreeX < (minDecoSpacing * spacingMultiplier)) continue;
            if (Random.value > localChance) continue;

            // Skip if near a cliff
            bool nearCliff = false;
            foreach (float cx in cliffPositions)
                if (Mathf.Abs(x - cx) < 15f) { nearCliff = true; break; }
            if (nearCliff) continue;

            float terrainY = points[i].y;
            Sprite treeSprite = treeSprites[Random.Range(0, treeSprites.Length)];
            float scale = Random.Range(0.5f, 0.9f);

            GameObject tree = new GameObject("Tree");
            tree.transform.parent = transform;
            
            // Calculate absolute bottom of the sprite to guarantee it sits on the snow
            float pivotOffset = treeSprite.bounds.min.y;
            tree.transform.position = new Vector3(
                x + Random.Range(-0.5f, 0.5f),
                terrainY - (pivotOffset * scale) - 0.2f, // -0.2f sinks the base slightly into the snow
                0
            );
            tree.transform.localScale = Vector3.one * scale;

            SpriteRenderer sr = tree.AddComponent<SpriteRenderer>();
            sr.sprite = treeSprite;
            sr.sortingOrder = -1; // behind player, behind terrain surface
            if (Random.value > 0.5f) sr.flipX = true;

            lastTreeX = x;
        }
    }

    // ===================== ROCKS =====================

    private void SpawnRocksOnSegment(Vector2[] points)
    {
        if (rockSprites == null || rockSprites.Length == 0) return;

        for (int i = 2; i < points.Length - 1; i += 4)
        {
            float x = points[i].x;
            if (x - lastRockX < minDecoSpacing * 2f) continue;
            if (Random.value > rockSpawnChance) continue;

            // Skip if near a cliff
            bool nearCliff = false;
            foreach (float cx in cliffPositions)
                if (Mathf.Abs(x - cx) < 15f) { nearCliff = true; break; }
            if (nearCliff) continue;

            float terrainY = points[i].y;
            Sprite rockSprite = rockSprites[Random.Range(0, rockSprites.Length)];
            float scale = Random.Range(0.2f, 0.4f);

            GameObject rock = new GameObject("Rock");
            rock.transform.parent = transform;
            
            float pivotOffset = rockSprite.bounds.min.y;
            rock.transform.position = new Vector3(
                x + Random.Range(-0.3f, 0.3f),
                terrainY - (pivotOffset * scale) - 0.1f, // sunk slightly
                0
            );
            rock.transform.localScale = Vector3.one * scale;

            SpriteRenderer sr = rock.AddComponent<SpriteRenderer>();
            sr.sprite = rockSprite;
            sr.sortingOrder = -1;
            if (Random.value > 0.5f) sr.flipX = true;

            lastRockX = x;
        }
    }

    // ===================== SPEED BOOSTS =====================

    private void SpawnSpeedBoostsOnSegment(Vector2[] points)
    {
        for (int i = 3; i < points.Length - 2; i += 5)
        {
            float x = points[i].x;
            if (x - lastSpeedBoostX < 80f) continue;
            if (Random.value > speedBoostChance) continue;

            // Skip if near a cliff
            bool nearCliff = false;
            foreach (float cx in cliffPositions)
                if (Mathf.Abs(x - cx) < 15f) { nearCliff = true; break; }
            if (nearCliff) continue;

            float y = points[i].y + speedBoostHeight;
            CreateSpeedBoost(new Vector3(x, y, 0));
            lastSpeedBoostX = x;
        }
    }

    private void CreateSpeedBoost(Vector3 pos)
    {
        GameObject boost = new GameObject("SpeedBoost");
        boost.transform.position = pos;
        boost.transform.parent = transform;

        SpriteRenderer sr = boost.AddComponent<SpriteRenderer>();
        sr.sprite = giftBagSprite != null ? giftBagSprite : CreateCircleSprite();
        if (giftBagSprite == null) sr.color = new Color(0.2f, 1f, 0.4f, 1f);
        sr.sortingOrder = 5;
        boost.transform.localScale = Vector3.one * 1.5f; // Made much bigger as requested

        CircleCollider2D col = boost.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.6f;

        boost.AddComponent<SpeedBoost>();
    }

    // ===================== OBSTACLES =====================

    private void SpawnObstaclesOnSegment(Vector2[] points)
    {
        if (obstaclePrefabs == null || obstaclePrefabs.Length == 0) return;

        for (int i = 3; i < points.Length - 1; i++)
        {
            float x = points[i].x;
            if (x - lastObstacleX < minObstacleSpacing) continue;

            // Skip if near a cliff
            bool nearCliff = false;
            foreach (float cx in cliffPositions)
                if (Mathf.Abs(x - cx) < 15f) { nearCliff = true; break; }
            if (nearCliff) continue;

            float currentChance = Mathf.Min(obstacleSpawnChance * (1f + segmentCount * 0.001f), 0.3f);
            if (Random.value > currentChance) continue;

            float y = points[i].y + 0.5f;
            GameObject prefab = obstaclePrefabs[Random.Range(0, obstaclePrefabs.Length)];
            GameObject obstacle = Instantiate(prefab, new Vector3(x, y, 0), Quaternion.identity, transform);
            obstacle.tag = "Obstacle";
            lastObstacleX = x;
        }
    }

    // ===================== FINISH LINE =====================

    private void SpawnFinishLine()
    {
        finishLineSpawned = true;
        float finishY = nextSegmentStartY;

        GameObject finishLine = new GameObject("FinishLine");
        finishLine.transform.parent = transform;
        
        // Use the "Post 1.png" (o "Post 2.png") asset as the finish banner/flag!
        if (finishFlagSprite != null)
        {
            // Center the sprite at the end of the track. If the pivot is centered, sink it slightly.
            float pivotOffset = finishFlagSprite.bounds.min.y;
            finishLine.transform.position = new Vector3(trackTotalLength, finishY - pivotOffset - 0.2f, 0);
            
            SpriteRenderer sr = finishLine.AddComponent<SpriteRenderer>();
            sr.sprite = finishFlagSprite;
            sr.sortingOrder = 10;
            // Scale it to look impressive
            finishLine.transform.localScale = Vector3.one * 1.5f;
            
            // Still add FINISH text floating above it
            GameObject textObj = new GameObject("FinishText");
            textObj.transform.parent = finishLine.transform;
            textObj.transform.position = new Vector3(trackTotalLength, finishLine.transform.position.y + finishFlagSprite.bounds.size.y * 1.5f + 1f, 0);
            TextMesh tm = textObj.AddComponent<TextMesh>();
            tm.text = "FINISH";
            tm.fontSize = 48; tm.characterSize = 0.15f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.color = Color.yellow;
            textObj.GetComponent<MeshRenderer>().sortingOrder = 11;
        }
        else
        {
            // Fallback primitive
            finishLine.transform.position = new Vector3(trackTotalLength, finishY, 0);
            LineRenderer lr = finishLine.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.startWidth = 0.4f; lr.endWidth = 0.4f;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = Color.red; lr.endColor = Color.red;
            lr.sortingOrder = 10; lr.useWorldSpace = true;
            lr.SetPosition(0, new Vector3(trackTotalLength, finishY - 1f, 0));
            lr.SetPosition(1, new Vector3(trackTotalLength, finishY + 10f, 0));
        }
    }

    // ===================== VISUAL HELPERS =====================

    private void CreateSurfaceLine(GameObject parent, Vector2[] points)
    {
        LineRenderer lr = parent.AddComponent<LineRenderer>();
        lr.positionCount = points.Length;
        lr.startWidth = surfaceLineWidth; lr.endWidth = surfaceLineWidth;
        lr.material = surfaceLineMaterial;
        lr.startColor = snowSurfaceColor; lr.endColor = snowSurfaceColor;
        lr.sortingOrder = 2; lr.useWorldSpace = true;

        Vector3[] pos = new Vector3[points.Length];
        for (int i = 0; i < points.Length; i++)
            pos[i] = new Vector3(points[i].x, points[i].y, 0);
        lr.SetPositions(pos);
    }

    private void CreateTerrainFill(GameObject parent, Vector2[] surfacePoints)
    {
        GameObject fill = new GameObject("Fill");
        fill.transform.parent = parent.transform;
        fill.layer = parent.layer;

        MeshFilter mf = fill.AddComponent<MeshFilter>();
        MeshRenderer mr = fill.AddComponent<MeshRenderer>();
        mr.material = fillMaterial;
        mr.sortingOrder = 0;

        float minY = float.MaxValue;
        foreach (var p in surfacePoints) minY = Mathf.Min(minY, p.y);
        float bottomY = minY - 50f;

        Vector3[] verts = new Vector3[surfacePoints.Length * 2];
        int[] tris = new int[(surfacePoints.Length - 1) * 6];

        for (int i = 0; i < surfacePoints.Length; i++)
        {
            verts[i * 2] = new Vector3(surfacePoints[i].x, surfacePoints[i].y, 0);
            verts[i * 2 + 1] = new Vector3(surfacePoints[i].x, bottomY, 0);
        }

        for (int i = 0; i < surfacePoints.Length - 1; i++)
        {
            int ti = i * 6, vi = i * 2;
            tris[ti] = vi; tris[ti + 1] = vi + 2; tris[ti + 2] = vi + 1;
            tris[ti + 3] = vi + 1; tris[ti + 4] = vi + 2; tris[ti + 5] = vi + 3;
        }

        Mesh mesh = new Mesh();
        mesh.vertices = verts; mesh.triangles = tris;
        mesh.RecalculateNormals();
        mf.mesh = mesh;
    }

    // ===================== SPRITE HELPERS =====================

    private Sprite _circle;
    private Sprite CreateCircleSprite()
    {
        if (_circle != null) return _circle;
        int s = 32; Texture2D t = new Texture2D(s, s, TextureFormat.RGBA32, false);
        float c = s / 2f, r = c - 1;
        for (int x = 0; x < s; x++)
            for (int y = 0; y < s; y++)
                t.SetPixel(x, y, Vector2.Distance(new Vector2(x, y), new Vector2(c, c)) <= r ? Color.white : Color.clear);
        t.Apply();
        _circle = Sprite.Create(t, new Rect(0, 0, s, s), Vector2.one * 0.5f, s);
        return _circle;
    }

    private Sprite _triangle;
    private Sprite CreateTriangleSprite()
    {
        if (_triangle != null) return _triangle;
        int s = 32; Texture2D t = new Texture2D(s, s, TextureFormat.RGBA32, false);
        for (int x = 0; x < s; x++)
            for (int y = 0; y < s; y++)
            {
                float nX = (float)x / s, nY = (float)y / s;
                t.SetPixel(x, y, (nY < 1f && nX > 0.5f - nY * 0.5f && nX < 0.5f + nY * 0.5f) ? Color.white : Color.clear);
            }
        t.Apply();
        _triangle = Sprite.Create(t, new Rect(0, 0, s, s), Vector2.one * 0.5f, s);
        return _triangle;
    }
}
