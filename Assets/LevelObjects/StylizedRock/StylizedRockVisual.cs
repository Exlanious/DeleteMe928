using System.Collections.Generic;
using UnityEngine;

// Visual geometry only. Collider, placement, and player movement are owned elsewhere.
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public sealed class StylizedRockVisual : MonoBehaviour
{
    private Mesh ownedMesh;

    private void OnEnable()
    {
        var vertices = new List<Vector3>();
        var top = new List<int>();
        var sides = new List<int>();
        // Position-derived variation never consumes the generator's UnityEngine.Random state.
        float seed = transform.position.x * 0.73f + transform.position.z * 1.17f + transform.position.y * 0.41f;
        Vector2[] outline = {
            new Vector2(-1.13f, -1.25f), new Vector2(-0.35f, -1.25f),
            new Vector2(0.62f, -1.25f), new Vector2(1.16f, -1.25f),
            new Vector2(1.25f, -1.12f), new Vector2(1.25f, -0.24f),
            new Vector2(1.25f, 0.67f), new Vector2(1.25f, 1.14f),
            new Vector2(1.13f, 1.25f), new Vector2(0.32f, 1.25f),
            new Vector2(-0.68f, 1.25f), new Vector2(-1.15f, 1.25f),
            new Vector2(-1.25f, 1.13f), new Vector2(-1.25f, 0.27f),
            new Vector2(-1.25f, -0.61f), new Vector2(-1.25f, -1.14f)
        };
        int count = outline.Length;
        var upper = new Vector3[count];
        var middle = new Vector3[count];
        var lower = new Vector3[count];
        for (int i = 0; i < count; i++)
        {
            float a = Mathf.Sin(seed + i * 2.73f) * 0.5f + 0.5f;
            float b = Mathf.Sin(seed * 1.31f + i * 4.19f) * 0.5f + 0.5f;
            upper[i] = new Vector3(outline[i].x, 0.25f, outline[i].y);
            float bulge = 0.91f + a * 0.13f;
            middle[i] = new Vector3(outline[i].x * bulge, 0.08f - b * 0.25f, outline[i].y * bulge);
            float taper = 0.67f + b * 0.27f;
            lower[i] = new Vector3(outline[i].x * taper + (a - 0.5f) * 0.1f,
                -0.25f - a * 0.24f, outline[i].y * taper);
        }
        for (int i = 0; i < count; i++)
        {
            int j = (i + 1) % count;
            Triangle(vertices, top, new Vector3(0, 0.25f, 0), upper[j], upper[i]);
            Triangle(vertices, sides, upper[i], upper[j], middle[i]);
            Triangle(vertices, sides, upper[j], middle[j], middle[i]);
            Triangle(vertices, sides, middle[i], middle[j], lower[j]);
            Triangle(vertices, sides, middle[i], lower[j], lower[i]);
            Triangle(vertices, sides, new Vector3(0, -0.4f, 0), lower[i], lower[j]);
        }
        // Fit the visual to the existing local collider without touching its transform or size.
        var box = GetComponent<BoxCollider>();
        if (box != null)
        {
            Vector3 fit = new Vector3(box.size.x / 2.5f, box.size.y / 0.5f, box.size.z / 2.5f);
            for (int i = 0; i < vertices.Count; i++)
                vertices[i] = Vector3.Scale(vertices[i], fit) + box.center;
        }
        ownedMesh = new Mesh { name = "Stylized Rock (visual only)", hideFlags = HideFlags.HideAndDontSave };
        ownedMesh.SetVertices(vertices);
        ownedMesh.subMeshCount = 2;
        ownedMesh.SetTriangles(top, 0);
        ownedMesh.SetTriangles(sides, 1);
        ownedMesh.RecalculateNormals();
        ownedMesh.RecalculateBounds();
        GetComponent<MeshFilter>().sharedMesh = ownedMesh;
    }

    private static void Triangle(List<Vector3> vertices, List<int> triangles, Vector3 a, Vector3 b, Vector3 c)
    {
        int start = vertices.Count;
        vertices.Add(a); vertices.Add(b); vertices.Add(c);
        triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
    }

    private void OnDisable()
    {
        if (ownedMesh == null) return;
        if (GetComponent<MeshFilter>().sharedMesh == ownedMesh)
            GetComponent<MeshFilter>().sharedMesh = null;
        if (Application.isPlaying) Destroy(ownedMesh);
        else DestroyImmediate(ownedMesh);
        ownedMesh = null;
    }
}
