using UnityEngine;
using System.Collections.Generic;

public class PlatformManager : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private GameObject platformPrefab;
    [SerializeField] private Material platformMaterial;
    [SerializeField] private Vector2 platformDimensions = new Vector2(2.5f, 2.5f);
    [SerializeField] private float verticalSpacing = 2.5f;
    [SerializeField] private float horizontalSpawnRange = 2f;
    [SerializeField] private float spawnAheadDistance = 12.5f;
    [SerializeField] private int maxSpawnedPlatforms = 50;
    [SerializeField] private Vector2 levelBoundsCenter = new Vector2(9.5f, 29f);
    [SerializeField] private Vector2 levelBoundsSize = new Vector2(26f, 60f);
    [SerializeField] private float wallBaseHeight;
    [SerializeField] private float wallTopPadding = 2f;
    [SerializeField] private float wallThickness = 1f;
    [SerializeField] private Material wallMaterial;

    private readonly Queue<GameObject> spawnedPlatforms = new Queue<GameObject>();
    private readonly GameObject[] boundaryWalls = new GameObject[4];
    private float nextPlatformHeight;
    private Vector3 lastPlatformPosition;

    private void Start()
    {
        if (player == null)
        {
            PlayerMovement playerMovement = FindAnyObjectByType<PlayerMovement>();
            if (playerMovement != null)
            {
                player = playerMovement.transform;
            }
        }

        if (player == null)
        {
            Debug.LogError("PlatformManager requires a player Transform or a PlayerMovement in the scene.", this);
            enabled = false;
            return;
        }

        lastPlatformPosition = player.position;
        nextPlatformHeight = player.position.y;
        for (int index = 0; index < transform.childCount; index++)
        {
            Transform existingPlatform = transform.GetChild(index);
            if (existingPlatform.position.y > nextPlatformHeight)
            {
                nextPlatformHeight = existingPlatform.position.y;
                lastPlatformPosition = existingPlatform.position;
            }
        }

        CreateBoundaryWalls();
        UpdateBoundaryWalls(nextPlatformHeight + wallTopPadding);
        SpawnPlatformsAhead();
    }

    private void Update()
    {
        SpawnPlatformsAhead();
    }

    private void SpawnPlatformsAhead()
    {
        float spacing = Mathf.Max(0.1f, verticalSpacing);
        float targetHeight = player.position.y + Mathf.Max(0f, spawnAheadDistance);
        while (nextPlatformHeight + spacing <= targetHeight)
        {
            nextPlatformHeight += spacing;
            SpawnPlatform(nextPlatformHeight);
        }
    }

    private void SpawnPlatform(float height)
    {
        float halfPlatformWidth = Mathf.Max(0f, levelBoundsSize.x * 0.5f - platformDimensions.x * 0.5f);
        float halfPlatformDepth = Mathf.Max(0f, levelBoundsSize.y * 0.5f - platformDimensions.y * 0.5f);
        Vector3 position = new Vector3(
            Mathf.Clamp(lastPlatformPosition.x + Random.Range(-horizontalSpawnRange, horizontalSpawnRange),
                levelBoundsCenter.x - halfPlatformWidth, levelBoundsCenter.x + halfPlatformWidth),
            height,
            Mathf.Clamp(lastPlatformPosition.z + Random.Range(-horizontalSpawnRange, horizontalSpawnRange),
                levelBoundsCenter.y - halfPlatformDepth, levelBoundsCenter.y + halfPlatformDepth));

        GameObject platform;
        if (platformPrefab != null)
        {
            platform = Instantiate(platformPrefab, position, Quaternion.identity, transform);
        }
        else
        {
            platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
            platform.transform.SetParent(transform, true);
            platform.transform.position = position;
            platform.transform.localScale = new Vector3(platformDimensions.x, 0.5f, platformDimensions.y);
        }

        platform.name = "Generated Platform";
        lastPlatformPosition = position;
        UpdateBoundaryWalls(height + wallTopPadding);
        Renderer platformRenderer = platform.GetComponentInChildren<Renderer>();
        if (platformRenderer != null && platformMaterial != null)
        {
            platformRenderer.sharedMaterial = platformMaterial;
        }

        spawnedPlatforms.Enqueue(platform);
        while (spawnedPlatforms.Count > Mathf.Max(1, maxSpawnedPlatforms))
        {
            Destroy(spawnedPlatforms.Dequeue());
        }
    }

    private void CreateBoundaryWalls()
    {
        for (int index = 0; index < boundaryWalls.Length; index++)
        {
            boundaryWalls[index] = GameObject.CreatePrimitive(PrimitiveType.Cube);
            boundaryWalls[index].name = "Level Boundary Wall";
            boundaryWalls[index].transform.SetParent(transform, true);

            Renderer wallRenderer = boundaryWalls[index].GetComponent<Renderer>();
            Material material = wallMaterial != null ? wallMaterial : platformMaterial;
            if (wallRenderer != null && material != null)
            {
                wallRenderer.sharedMaterial = material;
            }
        }
    }

    private void UpdateBoundaryWalls(float topHeight)
    {
        float thickness = Mathf.Max(0.1f, wallThickness);
        float height = Mathf.Max(1f, topHeight - wallBaseHeight);
        float centerHeight = wallBaseHeight + height * 0.5f;
        float halfWidth = Mathf.Max(0.1f, levelBoundsSize.x * 0.5f);
        float halfDepth = Mathf.Max(0.1f, levelBoundsSize.y * 0.5f);

        SetBoundaryWall(0, new Vector3(levelBoundsCenter.x - halfWidth - thickness * 0.5f, centerHeight, levelBoundsCenter.y),
            new Vector3(thickness, height, levelBoundsSize.y + thickness * 2f));
        SetBoundaryWall(1, new Vector3(levelBoundsCenter.x + halfWidth + thickness * 0.5f, centerHeight, levelBoundsCenter.y),
            new Vector3(thickness, height, levelBoundsSize.y + thickness * 2f));
        SetBoundaryWall(2, new Vector3(levelBoundsCenter.x, centerHeight, levelBoundsCenter.y - halfDepth - thickness * 0.5f),
            new Vector3(levelBoundsSize.x, height, thickness));
        SetBoundaryWall(3, new Vector3(levelBoundsCenter.x, centerHeight, levelBoundsCenter.y + halfDepth + thickness * 0.5f),
            new Vector3(levelBoundsSize.x, height, thickness));
    }

    private void SetBoundaryWall(int index, Vector3 position, Vector3 scale)
    {
        boundaryWalls[index].transform.position = position;
        boundaryWalls[index].transform.localScale = scale;
    }
}
