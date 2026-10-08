using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class OpenSkyLevel
{
    public static object TileLava()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Tile the authored scene outside Play Mode.");
        var lava = Object.FindAnyObjectByType<Lava>();
        var mesh = lava.GetComponent<MeshFilter>().sharedMesh;
        var sourceRenderer = lava.GetComponent<MeshRenderer>();
        const int rings = 2;
        var existing = lava.transform.Find("Lava Tiles");
        if (existing != null) Undo.DestroyObjectImmediate(existing.gameObject);

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Extend lava by two tile rings");
        var group = new GameObject("Lava Tiles");
        Undo.RegisterCreatedObjectUndo(group, "Create lava tiles");
        group.transform.SetParent(lava.transform, false);
        for (int x = -rings; x <= rings; x++)
        {
            for (int z = -rings; z <= rings; z++)
            {
                if (x == 0 && z == 0) continue;
                var tile = new GameObject($"Lava Tile ({x}, {z})", typeof(MeshFilter), typeof(MeshRenderer));
                Undo.RegisterCreatedObjectUndo(tile, "Create lava tile");
                tile.layer = lava.gameObject.layer;
                tile.transform.SetParent(group.transform, false);
                tile.transform.localPosition = new Vector3(x, 0f, z);
                tile.GetComponent<MeshFilter>().sharedMesh = mesh;
                EditorUtility.CopySerialized(sourceRenderer, tile.GetComponent<MeshRenderer>());
            }
        }
        // One shared trigger and rising controller cover the entire tiled surface.
        var trigger = lava.GetComponent<BoxCollider>();
        Undo.RecordObject(trigger, "Extend lava trigger");
        trigger.size = new Vector3(rings * 2 + 1, trigger.size.y, rings * 2 + 1);
        Physics.SyncTransforms();
        EditorSceneManager.MarkSceneDirty(lava.gameObject.scene);
        EditorSceneManager.SaveScene(lava.gameObject.scene);
        return Inspect();
    }

    public static object Inspect()
    {
        var lava = Object.FindAnyObjectByType<Lava>();
        var renderers = lava.GetComponentsInChildren<MeshRenderer>();
        Bounds surface = renderers[0].bounds;
        foreach (var renderer in renderers) surface.Encapsulate(renderer.bounds);
        var trigger = lava.GetComponent<BoxCollider>();
        var outsideOldSurface = lava.transform.position + new Vector3(180f, 10f, 180f);
        bool extensionCovered = Physics.Raycast(outsideOldSurface, Vector3.down, out RaycastHit hit, 30f,
            ~0, QueryTriggerInteraction.Collide) && hit.collider == trigger;
        return new
        {
            playing = Application.isPlaying,
            scene = lava.gameObject.scene.path,
            surfaceTiles = renderers.Length,
            surfaceSize = surface.size.ToString(),
            triggerSize = trigger.bounds.size.ToString(),
            extensionCovered,
            sameMaterial = renderers.All(r => r.sharedMaterial == renderers[0].sharedMaterial),
            lavaControllers = lava.GetComponentsInChildren<Lava>().Length,
            hazardColliders = lava.GetComponentsInChildren<Collider>().Length,
            boundaryWalls = Object.FindObjectsByType<Transform>()
                .Count(t => t.name == "Level Boundary Wall"),
            lavaHeight = lava.transform.position.y,
            state = Application.isPlaying ? Object.FindAnyObjectByType<GameSession>().State.ToString() : "Edit Mode"
        };
    }
}
