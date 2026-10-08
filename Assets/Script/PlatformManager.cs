using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Generates an endless path of platforms that climbs upward. Every new platform is placed so it can be
/// reached from the previous one with either a double jump (short gap, big rise) or a dash (long gap, small rise),
/// with alternate routes between each pair of main platforms, following the inside of the boundary walls.
/// </summary>
public class PlatformManager : MonoBehaviour
{
    private class Platform
    {
        public GameObject gameObject; //null for platforms we didn't spawn (or the virtual start point)
        public Vector3 topCenter; //center of the platform's top surface
        public Vector2 halfSize; //half width on x and z
        public bool generated;
        public readonly List<Platform> branchesToNext = new List<Platform>();
    }

    [SerializeField] private Transform player;
    [SerializeField] private GameObject platformPrefab;
    [SerializeField] private Material platformMaterial;
    [SerializeField] private Vector2 platformDimensions = new Vector2(2.5f, 2.5f); //platform footprint on x and z (also used as the prefab's footprint)
    [SerializeField] private float platformThickness = 0.5f;

    [Header("Spawning")]
    [SerializeField, Min(5)] private int platformsAhead = 10; //main platforms kept in front of the one the player is standing on
    [SerializeField, Min(0)] private int platformsKeptBehind = 20; //older platforms past this count get destroyed
    [SerializeField, Min(1)] private int branchPlatformsPerStep = 2;

    //Keep the rise small and use wider edge gaps to make the route favor sideways movement.
    [Header("Double Jump Steps (x = min, y = max)")]
    [SerializeField] private Vector2 doubleJumpGap = new Vector2(2.5f, 4.5f); //edge to edge distance
    [SerializeField] private Vector2 doubleJumpRise = new Vector2(0.5f, 1.25f); //how much higher the next top is

    [Header("Dash Steps (x = min, y = max)")]
    [SerializeField, Range(0f, 1f)] private float dashStepChance = 0.35f;
    [SerializeField] private Vector2 dashGap = new Vector2(4f, 6.5f);
    [SerializeField] private Vector2 dashRise = new Vector2(0.25f, 0.8f);

    [Header("Path Shape")]
    [SerializeField] private float maxTurnAngle = 60f; //how far the path can turn each step
    [SerializeField] private float verticalClearance = 4f; //platforms this close vertically must not overlap from above
    [SerializeField] private float minClearance = 2f; //minimum horizontal space between platforms at similar heights
    [SerializeField] private int placementAttempts = 16;

    [Header("Level Bounds")]
    [SerializeField] private Vector2 levelBoundsCenter = new Vector2(9.5f, 29f);
    [SerializeField] private Vector2 levelBoundsSize = new Vector2(26f, 60f);
    [SerializeField] private float wallBaseHeight;
    [SerializeField] private float wallTopPadding = 2f;
    [SerializeField] private float wallThickness = 1f;
    [SerializeField] private Material wallMaterial;

    private readonly List<Platform> path = new List<Platform>(); //platforms the player climbs, in order
    private readonly List<Platform> obstacles = new List<Platform>(); //hand placed platforms that new ones must not overlap
    private readonly GameObject[] boundaryWalls = new GameObject[4];
    private CharacterController playerController;
    private int currentIndex; //index in path of the platform the player most recently stood on
    private int orbitDirection = 1;
    private bool hasLoggedPlacementFailure;
    private bool hasLoggedBranchFailure;

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
        orbitDirection = Random.value < 0.5f ? -1 : 1;
        StartPathFromExistingPlatforms();

        //walls are children too, so create them after the existing platforms have been read
        CreateBoundaryWalls();
        UpdateBoundaryWalls(path[path.Count - 1].topCenter.y + wallTopPadding);
        SpawnPlatformsAhead();
    }

    private void Update()
    {
        UpdatePlayerProgress();
        SpawnPlatformsAhead();
        DestroyPlatformsBehind();
    }

    /// <summary>
    /// Start generating from the highest hand placed platform.
    /// </summary>
    private void StartPathFromExistingPlatforms()
    {
        Platform highest = null;
        for (int index = 0; index < transform.childCount; index++)
        {
            Platform existing = PlatformFromTransform(transform.GetChild(index));
            obstacles.Add(existing);
            if (highest == null || existing.topCenter.y > highest.topCenter.y)
            {
                highest = existing;
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
            int previousCount = path.Count;
            SpawnNextPlatform();
            if (path.Count == previousCount)
            {
                return;
            }
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
            foreach (Platform branch in oldest.branchesToNext)
            {
                if (branch.gameObject != null)
                {
                    Destroy(branch.gameObject);
                }
                obstacles.Remove(branch);
            }
            path.RemoveAt(0);
            currentIndex--;
        }
    }

    /// <summary>
    /// Pick a double jump or dash step, then try a few directions that leave room around older platforms.
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
        float preferredHeading = PreferredHeadingAlongBounds(previous);
        float bestScore = float.NegativeInfinity;
        int attempts = Mathf.Max(1, placementAttempts);
        for (int attempt = 0; attempt <= attempts; attempt++)
        {
            float candidateHeading;
            if (attempt < attempts)
            {
                //Widen the search around the route direction to find a clear in-bounds step.
                float turnRange = Mathf.Min(180f, maxTurnAngle * (1f + 2f * attempt / attempts));
                candidateHeading = preferredHeading + Random.Range(-turnRange, turnRange);
            }
            else
            {
                Vector2 towardCenter = levelBoundsCenter - new Vector2(previous.topCenter.x, previous.topCenter.z);
                candidateHeading = towardCenter.sqrMagnitude > 0.001f
                    ? Mathf.Atan2(towardCenter.x, towardCenter.y) * Mathf.Rad2Deg
                    : preferredHeading;
            }

            Vector3 candidate = PositionAfterGap(previous, halfSize, candidateHeading, gap);
            candidate.y = previous.topCenter.y + rise;

            float edgeDistance = DistanceToBoundsEdge(candidate, halfSize);
            if (edgeDistance < 0f)
            {
                continue;
            }

            float clearance = Mathf.Min(ClearanceFromOlderPlatforms(candidate, halfSize), 1000f);
            if (clearance < minClearance)
            {
                continue;
            }

            float turnFromPreferred = Mathf.Abs(Mathf.DeltaAngle(preferredHeading, candidateHeading));
            float score = clearance - edgeDistance - turnFromPreferred * 0.1f;
            if (score > bestScore)
            {
                bestScore = score;
                bestPosition = candidate;
            }
        }

        if (bestScore == float.NegativeInfinity)
        {
            if (!hasLoggedPlacementFailure)
            {
                Debug.LogError("PlatformManager could not find an in-bounds platform position with the configured spacing. Check the level bounds and platform dimensions.", this);
                hasLoggedPlacementFailure = true;
            }
            return;
        }

        hasLoggedPlacementFailure = false;
        Platform next = CreatePlatform(bestPosition, halfSize);
        path.Add(next);
        UpdateBoundaryWalls(bestPosition.y + wallTopPadding);

        Vector3 direction = (bestPosition - previous.topCenter).normalized;
        Vector3 perpendicular = new Vector3(direction.z, 0f, -direction.x);
        Vector3 segmentCenter = (previous.topCenter + bestPosition) * 0.5f;
        Vector3 branchTowardCenter = new Vector3(levelBoundsCenter.x - segmentCenter.x, 0f, levelBoundsCenter.y - segmentCenter.z);
        int innerSide = Vector3.Dot(perpendicular, branchTowardCenter) >= 0f ? 1 : -1;
        int spawnedBranches = 0;
        for (int branchIndex = 0; branchIndex < Mathf.Max(1, branchPlatformsPerStep); branchIndex++)
        {
            Vector3 bestBranchPosition = Vector3.zero;
            float bestBranchScore = float.NegativeInfinity;
            int side = branchIndex % 2 == 0 ? innerSide : -innerSide;
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                float branchProgress = Random.Range(0.4f, 0.6f);
                float branchOffset = Mathf.Max(platformDimensions.x, platformDimensions.y)
                    + minClearance + Random.Range(0f, 1f);
                Vector3 branchPosition = Vector3.Lerp(previous.topCenter, bestPosition, branchProgress)
                    + perpendicular * side * branchOffset;
                branchPosition.y = Mathf.Lerp(previous.topCenter.y, bestPosition.y, branchProgress);

                if (DistanceToBoundsEdge(branchPosition, halfSize) < 0f)
                {
                    continue;
                }

                float clearance = Mathf.Min(ClearanceFromOlderPlatforms(branchPosition, halfSize),
                    ClearanceFrom(next, branchPosition, halfSize));
                if (clearance < minClearance)
                {
                    continue;
                }

                float score = clearance - Mathf.Abs(branchProgress - 0.5f);
                if (score > bestBranchScore)
                {
                    bestBranchScore = score;
                    bestBranchPosition = branchPosition;
                }
            }

            if (bestBranchScore == float.NegativeInfinity)
            {
                continue;
            }

            Platform branch = CreatePlatform(bestBranchPosition, halfSize);
            branch.gameObject.name = "Generated Branch Platform";
            previous.branchesToNext.Add(branch);
            obstacles.Add(branch);
            spawnedBranches++;
        }

        if (spawnedBranches == 0 && !hasLoggedBranchFailure)
        {
            Debug.LogWarning("PlatformManager could not place a branch platform with the current spacing and level bounds.", this);
            hasLoggedBranchFailure = true;
        }
    }

    private float PreferredHeadingAlongBounds(Platform previous)
    {
        float halfWidth = Mathf.Max(0f, levelBoundsSize.x * 0.5f - previous.halfSize.x);
        float halfDepth = Mathf.Max(0f, levelBoundsSize.y * 0.5f - previous.halfSize.y);
        float offsetX = previous.topCenter.x - levelBoundsCenter.x;
        float offsetZ = previous.topCenter.z - levelBoundsCenter.y;
        float distanceToXEdge = halfWidth - Mathf.Abs(offsetX);
        float distanceToZEdge = halfDepth - Mathf.Abs(offsetZ);
        Vector3 direction;

        if (Mathf.Min(distanceToXEdge, distanceToZEdge) > Mathf.Max(dashGap.y, doubleJumpGap.y) + 1f)
        {
            direction = distanceToXEdge <= distanceToZEdge
                ? new Vector3(offsetX == 0f ? orbitDirection : Mathf.Sign(offsetX), 0f, 0f)
                : new Vector3(0f, 0f, offsetZ == 0f ? orbitDirection : Mathf.Sign(offsetZ));
        }
        else if (distanceToXEdge <= distanceToZEdge)
        {
            direction = new Vector3(0f, 0f, orbitDirection * (offsetX >= 0f ? 1f : -1f));
        }
        else
        {
            direction = new Vector3(orbitDirection * (offsetZ >= 0f ? -1f : 1f), 0f, 0f);
        }

        return Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
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
    /// Signed distance to the nearest edge of the level bounds (negative outside).
    /// </summary>
    private float DistanceToBoundsEdge(Vector3 candidate, Vector2 halfSize)
    {
        float roomX = Mathf.Max(0f, levelBoundsSize.x * 0.5f - halfSize.x);
        float roomZ = Mathf.Max(0f, levelBoundsSize.y * 0.5f - halfSize.y);
        float distanceX = roomX - Mathf.Abs(candidate.x - levelBoundsCenter.x);
        float distanceZ = roomZ - Mathf.Abs(candidate.z - levelBoundsCenter.y);
        return Mathf.Min(distanceX, distanceZ);
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

    private void OnDrawGizmosSelected()
    {
        //show the climbing path in the scene view
        Gizmos.color = Color.yellow;
        for (int index = 1; index < path.Count; index++)
        {
            Gizmos.DrawLine(path[index - 1].topCenter, path[index].topCenter);
            foreach (Platform branch in path[index - 1].branchesToNext)
            {
                Gizmos.DrawLine(path[index - 1].topCenter, branch.topCenter);
                Gizmos.DrawLine(branch.topCenter, path[index].topCenter);
            }
        }
    }
}
