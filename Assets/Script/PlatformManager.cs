using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Generates an endless path of platforms that climbs upward. Every new platform is placed so it can be
/// reached from the previous one with either a double jump (short gap, big rise) or a dash (long gap, small rise),
/// and there are always at least platformsAhead platforms in front of the one the player is standing on.
/// </summary>
public class PlatformManager : MonoBehaviour
{
    private class Platform
    {
        public GameObject gameObject; //null for platforms we didn't spawn (or the virtual start point)
        public Vector3 topCenter; //center of the platform's top surface
        public Vector2 halfSize; //half width on x and z
        public bool generated;
    }

    [SerializeField] private Transform player;
    [SerializeField] private GameObject platformPrefab;
    [SerializeField] private Material platformMaterial;
    [SerializeField] private Vector2 platformDimensions = new Vector2(2.5f, 2.5f); //platform footprint on x and z (also used as the prefab's footprint)
    [SerializeField] private float platformThickness = 0.5f;

    [Header("Spawning")]
    [SerializeField, Min(5)] private int platformsAhead = 5; //platforms always kept in front of the one the player is standing on
    [SerializeField, Min(0)] private int platformsKeptBehind = 20; //older platforms past this count get destroyed

    //These limits are based on PlayerMovement's defaults (moveSpeed 5, jump 6, gravity 25, dash 25):
    //a double jump reaches about 4.4 units high, a jump + dash covers about 9 units across, so these leave a safety margin.
    [Header("Double Jump Steps (x = min, y = max)")]
    [SerializeField] private Vector2 doubleJumpGap = new Vector2(1f, 3f); //edge to edge distance
    [SerializeField] private Vector2 doubleJumpRise = new Vector2(1.5f, 3f); //how much higher the next top is

    [Header("Dash Steps (x = min, y = max)")]
    [SerializeField, Range(0f, 1f)] private float dashStepChance = 0.35f;
    [SerializeField] private Vector2 dashGap = new Vector2(3.5f, 6f);
    [SerializeField] private Vector2 dashRise = new Vector2(0.25f, 1.5f);

    [Header("Path Shape")]
    [SerializeField] private float maxTurnAngle = 60f; //how far the path can turn each step
    [SerializeField] private float verticalClearance = 4f; //platforms this close vertically must not overlap from above
    [SerializeField] private float minClearance = 1f; //minimum horizontal space between a new platform and older ones
    [SerializeField] private int placementAttempts = 16;

    private readonly List<Platform> path = new List<Platform>(); //platforms the player climbs, in order
    private readonly List<Platform> obstacles = new List<Platform>(); //hand placed platforms that new ones must not overlap
    private CharacterController playerController;
    private int currentIndex; //index in path of the platform the player most recently stood on
    private float heading; //direction the path is traveling, in degrees around y

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

        playerController = player.GetComponent<CharacterController>();
        StartPathFromExistingPlatforms();
        SpawnPlatformsAhead();
    }

    private void Update()
    {
        UpdatePlayerProgress();
        SpawnPlatformsAhead();
        DestroyPlatformsBehind();
    }

    /// <summary>
    /// Start generating from the highest hand placed platform, heading away from the one before it
    /// </summary>
    private void StartPathFromExistingPlatforms()
    {
        Platform highest = null;
        Platform secondHighest = null;
        for (int index = 0; index < transform.childCount; index++)
        {
            Platform existing = PlatformFromTransform(transform.GetChild(index));
            obstacles.Add(existing);
            if (highest == null || existing.topCenter.y > highest.topCenter.y)
            {
                secondHighest = highest;
                highest = existing;
            }
            else if (secondHighest == null || existing.topCenter.y > secondHighest.topCenter.y)
            {
                secondHighest = existing;
            }
        }

        if (highest == null)
        {
            //no hand placed platforms, so start from wherever the player is standing
            highest = new Platform
            {
                topCenter = new Vector3(player.position.x, PlayerFeetHeight(), player.position.z),
                halfSize = platformDimensions * 0.5f,
            };
        }
        else
        {
            obstacles.Remove(highest);
        }
        path.Add(highest);
        currentIndex = 0;

        Vector3 startDirection = secondHighest != null ? highest.topCenter - secondHighest.topCenter : player.forward;
        startDirection.y = 0;
        heading = startDirection.sqrMagnitude > 0.001f ? Mathf.Atan2(startDirection.x, startDirection.z) * Mathf.Rad2Deg : 0f;
    }

    private Platform PlatformFromTransform(Transform platformTransform)
    {
        Collider platformCollider = platformTransform.GetComponentInChildren<Collider>();
        Bounds bounds = platformCollider != null
            ? platformCollider.bounds
            : new Bounds(platformTransform.position, platformTransform.lossyScale);
        return new Platform
        {
            gameObject = platformTransform.gameObject,
            topCenter = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z),
            halfSize = new Vector2(bounds.extents.x, bounds.extents.z),
        };
    }

    /// <summary>
    /// Move currentIndex forward to the furthest platform the player is standing on
    /// </summary>
    private void UpdatePlayerProgress()
    {
        float feetHeight = PlayerFeetHeight();
        float playerRadius = playerController != null ? playerController.radius : 0.5f;
        for (int index = path.Count - 1; index > currentIndex; index--)
        {
            Platform platform = path[index];
            bool feetOnTop = feetHeight >= platform.topCenter.y - 0.25f && feetHeight <= platform.topCenter.y + 0.5f;
            bool insideX = Mathf.Abs(player.position.x - platform.topCenter.x) <= platform.halfSize.x + playerRadius;
            bool insideZ = Mathf.Abs(player.position.z - platform.topCenter.z) <= platform.halfSize.y + playerRadius;
            if (feetOnTop && insideX && insideZ)
            {
                currentIndex = index;
                return;
            }
        }
    }

    private float PlayerFeetHeight()
    {
        return playerController != null ? playerController.bounds.min.y : player.position.y - 1f;
    }

    private void SpawnPlatformsAhead()
    {
        while (path.Count - 1 - currentIndex < platformsAhead)
        {
            SpawnNextPlatform();
        }
    }

    private void DestroyPlatformsBehind()
    {
        while (currentIndex > platformsKeptBehind)
        {
            Platform oldest = path[0];
            if (oldest.generated && oldest.gameObject != null)
            {
                Destroy(oldest.gameObject);
            }
            path.RemoveAt(0);
            currentIndex--;
        }
    }

    /// <summary>
    /// Pick a double jump or dash step, then try a few directions until the new platform doesn't crowd older ones
    /// </summary>
    private void SpawnNextPlatform()
    {
        Platform previous = path[path.Count - 1];
        Vector2 halfSize = platformDimensions * 0.5f;

        bool dashStep = Random.value < dashStepChance;
        Vector2 gapRange = dashStep ? dashGap : doubleJumpGap;
        Vector2 riseRange = dashStep ? dashRise : doubleJumpRise;
        float gap = Random.Range(gapRange.x, gapRange.y);
        float rise = Random.Range(riseRange.x, riseRange.y);

        Vector3 bestPosition = Vector3.zero;
        float bestHeading = heading;
        float bestClearance = float.NegativeInfinity;
        int attempts = Mathf.Max(1, placementAttempts);
        for (int attempt = 0; attempt < attempts; attempt++)
        {
            //widen the allowed turn on later attempts so the path can steer out of tight spots
            float turnRange = Mathf.Min(180f, maxTurnAngle * (1f + 2f * attempt / attempts));
            float candidateHeading = heading + Random.Range(-turnRange, turnRange);
            Vector3 candidate = PositionAfterGap(previous, halfSize, candidateHeading, gap);
            candidate.y = previous.topCenter.y + rise;

            float clearance = ClearanceFromOlderPlatforms(candidate, halfSize);
            if (clearance > bestClearance)
            {
                bestClearance = clearance;
                bestPosition = candidate;
                bestHeading = candidateHeading;
            }
            if (clearance >= minClearance)
            {
                break;
            }
        }

        heading = bestHeading;
        path.Add(CreatePlatform(bestPosition, halfSize));
    }

    /// <summary>
    /// Find the new platform's center so that, traveling along the heading, there is exactly gap between the two edges
    /// </summary>
    private Vector3 PositionAfterGap(Platform previous, Vector2 halfSize, float headingDegrees, float gap)
    {
        float radians = headingDegrees * Mathf.Deg2Rad;
        Vector3 direction = new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians));

        //distance along the direction where the two platforms stop overlapping on x or z
        float separateX = Mathf.Abs(direction.x) > 0.0001f ? (previous.halfSize.x + halfSize.x) / Mathf.Abs(direction.x) : float.PositiveInfinity;
        float separateZ = Mathf.Abs(direction.z) > 0.0001f ? (previous.halfSize.y + halfSize.y) / Mathf.Abs(direction.z) : float.PositiveInfinity;
        float centerDistance = Mathf.Min(separateX, separateZ) + gap;

        return previous.topCenter + direction * centerDistance;
    }

    /// <summary>
    /// Smallest horizontal gap between the candidate and any older platform close enough vertically to get in the way.
    /// The platform being jumped from is skipped since its spacing is set by the step.
    /// </summary>
    private float ClearanceFromOlderPlatforms(Vector3 candidate, Vector2 halfSize)
    {
        float clearance = float.PositiveInfinity;
        for (int index = 0; index < path.Count - 1; index++)
        {
            clearance = Mathf.Min(clearance, ClearanceFrom(path[index], candidate, halfSize));
        }
        foreach (Platform obstacle in obstacles)
        {
            clearance = Mathf.Min(clearance, ClearanceFrom(obstacle, candidate, halfSize));
        }
        return clearance;
    }

    private float ClearanceFrom(Platform other, Vector3 candidate, Vector2 halfSize)
    {
        if (Mathf.Abs(other.topCenter.y - candidate.y) >= verticalClearance)
        {
            return float.PositiveInfinity;
        }
        float gapX = Mathf.Max(0f, Mathf.Abs(other.topCenter.x - candidate.x) - (other.halfSize.x + halfSize.x));
        float gapZ = Mathf.Max(0f, Mathf.Abs(other.topCenter.z - candidate.z) - (other.halfSize.y + halfSize.y));
        return Mathf.Sqrt(gapX * gapX + gapZ * gapZ);
    }

    private Platform CreatePlatform(Vector3 topCenter, Vector2 halfSize)
    {
        Vector3 position = topCenter - Vector3.up * (platformThickness * 0.5f);

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
            platform.transform.localScale = new Vector3(platformDimensions.x, platformThickness, platformDimensions.y);
        }

        platform.name = "Generated Platform";
        Renderer platformRenderer = platform.GetComponentInChildren<Renderer>();
        if (platformRenderer != null && platformMaterial != null)
        {
            platformRenderer.sharedMaterial = platformMaterial;
        }

        return new Platform
        {
            gameObject = platform,
            topCenter = topCenter,
            halfSize = halfSize,
            generated = true,
        };
    }

    private void OnDrawGizmosSelected()
    {
        //show the climbing path in the scene view
        Gizmos.color = Color.yellow;
        for (int index = 1; index < path.Count; index++)
        {
            Gizmos.DrawLine(path[index - 1].topCenter, path[index].topCenter);
        }
    }
}
