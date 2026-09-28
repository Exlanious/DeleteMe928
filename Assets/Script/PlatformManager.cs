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

    private readonly Queue<GameObject> spawnedPlatforms = new Queue<GameObject>();
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
        Vector3 position = new Vector3(
            lastPlatformPosition.x + Random.Range(-horizontalSpawnRange, horizontalSpawnRange),
            height,
            lastPlatformPosition.z + Random.Range(-horizontalSpawnRange, horizontalSpawnRange));

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
}
