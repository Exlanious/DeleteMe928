using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Independent visual layer: never changes wall colliders or the platform generator.
public sealed class RockWallBlocks : MonoBehaviour
{
    [SerializeField] private Material stoneMaterial;
    [SerializeField, Min(0.5f)] private float stoneWidth = 2.4f;
    [SerializeField, Min(0.5f)] private float rowHeight = 1.35f;
    private Transform player;
    private readonly Dictionary<Transform, WallVisual> visuals = new Dictionary<Transform, WallVisual>();
    private sealed class WallVisual
    {
        public GameObject gameObject;
        public Mesh mesh;
        public Vector3 scale;
        public int band = int.MinValue;
    }

    private void Start()
    {
        var movement = FindAnyObjectByType<PlayerMovement>();
        if (movement != null) player = movement.transform;
    }

    private void LateUpdate()
    {
        Vector3 center = Vector3.zero;
        int count = 0;
        foreach (Transform child in transform)
            if (child.name == "Level Boundary Wall") { center += child.position; count++; }
        if (count == 0) return;
        center /= count;
        foreach (Transform wall in transform)
        {
            if (wall.name != "Level Boundary Wall") continue;
            if (!visuals.TryGetValue(wall, out WallVisual visual))
            {
                visual = new WallVisual { gameObject = new GameObject("Rock block facing (visual only)") };
                visual.gameObject.transform.SetParent(wall, false);
                visual.gameObject.AddComponent<MeshFilter>();
                visual.gameObject.AddComponent<MeshRenderer>().sharedMaterial = stoneMaterial;
                visuals.Add(wall, visual);
            }
            int band = Mathf.FloorToInt((player != null ? player.position.y : center.y) / rowHeight);
            if (visual.scale == wall.lossyScale && visual.band == band) continue;
            Build(wall, visual, center, band);
        }
    }

    private void Build(Transform wall, WallVisual visual, Vector3 center, int band)
    {
        if (visual.mesh != null) Destroy(visual.mesh);
        Vector3 size = wall.lossyScale;
        bool alongZ = size.z > size.x;
        Vector3 across = alongZ ? Vector3.forward : Vector3.right;
        Vector3 normal = alongZ ? Vector3.right : Vector3.forward;
        if (Vector3.Dot(normal, center - wall.position) < 0) normal = -normal;
        float length = alongZ ? size.z : size.x;
        float thickness = alongZ ? size.x : size.z;
        float bottom = wall.position.y - size.y * 0.5f;
        int totalRows = Mathf.CeilToInt(size.y / rowHeight) + 1;
        // Bound visual complexity in an endless level; retain stone around and above the player.
        int firstRow = Mathf.Max(0, Mathf.FloorToInt((band * rowHeight - bottom) / rowHeight) - 12);
        int lastRow = Mathf.Min(totalRows, firstRow + 40);
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        int wallSeed = Mathf.RoundToInt(wall.position.x * 137f + wall.position.z * 271f);
        int columns = Mathf.CeilToInt(length / stoneWidth);
        // Voronoi cells have oblique shared edges: no horizontal courses or rectangular tiles.
        for (int row = firstRow - 2; row <= lastRow + 1; row++)
        {
            for (int col = -1; col <= columns; col++)
            {
                Vector2 site = Site(col, row, wallSeed, length);
                var polygon = new List<Vector2> {
                    new Vector2(-length * 0.5f, bottom), new Vector2(length * 0.5f, bottom),
                    new Vector2(length * 0.5f, bottom + size.y), new Vector2(-length * 0.5f, bottom + size.y)
                };
                site.y += bottom;
                for (int dy = -3; dy <= 3 && polygon.Count >= 3; dy++)
                {
                    for (int dx = -3; dx <= 3 && polygon.Count >= 3; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        Vector2 other = Site(col + dx, row + dy, wallSeed, length);
                        other.y += bottom;
                        Vector2 planeNormal = other - site;
                        float planeDistance = Vector2.Dot(planeNormal, (site + other) * 0.5f);
                        polygon = Clip(polygon, planeNormal, planeDistance);
                    }
                }
                if (polygon.Count < 3) continue;
                float seed = Noise(col, row, wallSeed + 47);
                EmitPolygon(wall, across, normal, thickness, polygon, seed, vertices, triangles);
            }
        }
        visual.mesh = new Mesh { name = "Solid beveled wall stones", indexFormat = IndexFormat.UInt32 };
        visual.mesh.SetVertices(vertices);
        visual.mesh.SetTriangles(triangles, 0);
        visual.mesh.RecalculateNormals();
        visual.mesh.RecalculateBounds();
        visual.gameObject.GetComponent<MeshFilter>().sharedMesh = visual.mesh;
        visual.scale = size;
        visual.band = band;
    }

    private Vector2 Site(int col, int row, int seed, float length)
    {
        // Large independent offsets break the underlying sampling grid; shared cells remain stable as walls grow.
        return new Vector2(-length * 0.5f + (col + 0.5f + (Noise(col, row, seed) - 0.5f) * 0.94f) * stoneWidth,
            (row + 0.5f + (Noise(col, row, seed + 19) - 0.5f) * 0.94f) * rowHeight);
    }

    private static float Noise(int col, int row, int seed)
    {
        unchecked
        {
            uint value = (uint)(col * 73856093 ^ row * 19349663 ^ seed * 83492791);
            value ^= value >> 16; value *= 0x7feb352du;
            value ^= value >> 15; value *= 0x846ca68bu;
            value ^= value >> 16;
            return (value & 0x00ffffffu) / 16777215f;
        }
    }

    private static List<Vector2> Clip(List<Vector2> polygon, Vector2 normal, float distance)
    {
        var result = new List<Vector2>(polygon.Count + 1);
        Vector2 previous = polygon[polygon.Count - 1];
        float previousDistance = Vector2.Dot(previous, normal) - distance;
        foreach (Vector2 current in polygon)
        {
            float currentDistance = Vector2.Dot(current, normal) - distance;
            if ((currentDistance <= 0) != (previousDistance <= 0))
                result.Add(Vector2.Lerp(previous, current, previousDistance / (previousDistance - currentDistance)));
            if (currentDistance <= 0) result.Add(current);
            previous = current;
            previousDistance = currentDistance;
        }
        return result;
    }

    private static void EmitPolygon(Transform wall, Vector3 across, Vector3 normal, float thickness,
        List<Vector2> polygon, float variation, List<Vector3> vertices, List<int> triangles)
    {
        Vector2 center = Vector2.zero;
        foreach (Vector2 point in polygon) center += point;
        center /= polygon.Count;
        float area = 0;
        for (int i = 0; i < polygon.Count; i++)
        {
            Vector2 a = polygon[i] - center, b = polygon[(i + 1) % polygon.Count] - center;
            area += Mathf.Abs(a.x * b.y - a.y * b.x) * 0.5f;
        }
        if (area < 0.015f) return;
        Vector3 origin = wall.position + across * center.x
            + Vector3.up * (center.y - wall.position.y) + normal * (thickness * 0.5f);
        var back = new Vector3[polygon.Count];
        var front = new Vector3[polygon.Count];
        for (int i = 0; i < polygon.Count; i++)
        {
            Vector2 delta = polygon[i] - center;
            // A narrow gap exposes the dark backing, with each cell beveled independently.
            float inset = Mathf.Max(0.80f, 1f - 0.022f / Mathf.Max(delta.magnitude, 0.05f));
            back[i] = origin + across * (delta.x * inset) + Vector3.up * (delta.y * inset);
            float depth = 0.10f + variation * 0.22f + Mathf.Sin(variation * 29f + i * 2.4f) * 0.035f;
            front[i] = origin + across * (delta.x * inset * 0.88f)
                + Vector3.up * (delta.y * inset * 0.88f) + normal * depth;
        }
        Vector3 peak = origin + normal * (0.17f + variation * 0.24f);
        for (int i = 0; i < polygon.Count; i++)
        {
            int j = (i + 1) % polygon.Count;
            Add(vertices, triangles, wall, normal, peak, front[i], front[j]);
            Add(vertices, triangles, wall, normal, front[i], back[i], back[j]);
            Add(vertices, triangles, wall, normal, front[i], back[j], front[j]);
        }
    }

    private static void Add(List<Vector3> vertices, List<int> triangles, Transform wall,
        Vector3 outward, Vector3 a, Vector3 b, Vector3 c)
    {
        if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0) { Vector3 swap = b; b = c; c = swap; }
        int i = vertices.Count;
        vertices.Add(wall.InverseTransformPoint(a));
        vertices.Add(wall.InverseTransformPoint(b));
        vertices.Add(wall.InverseTransformPoint(c));
        triangles.Add(i); triangles.Add(i + 1); triangles.Add(i + 2);
    }

    private void OnDestroy()
    {
        foreach (WallVisual visual in visuals.Values)
        {
            if (visual.mesh != null) Destroy(visual.mesh);
            if (visual.gameObject != null) Destroy(visual.gameObject);
        }
    }
}
