using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ConfigureGame
{
    public static object Main()
    {
        var player = Object.FindAnyObjectByType<PlayerMovement>();
        if (player.GetComponent<HeightScore>() == null) Undo.AddComponent<HeightScore>(player.gameObject);
        if (player.GetComponent<GameSession>() == null) Undo.AddComponent<GameSession>(player.gameObject);
        if (player.GetComponent<GameScreens>() == null) Undo.AddComponent<GameScreens>(player.gameObject);

        var prefab = PrefabUtility.LoadPrefabContents("Assets/Player/Prefabs/FirstPersonController.prefab");
        if (prefab.GetComponent<HeightScore>() == null) prefab.AddComponent<HeightScore>();
        PrefabUtility.SaveAsPrefabAsset(prefab, "Assets/Player/Prefabs/FirstPersonController.prefab");
        PrefabUtility.UnloadPrefabContents(prefab);

        var scenes = EditorBuildSettings.scenes.ToList();
        var main = scenes.First(s => s.path == "Assets/Scenes/PlatformerLevel.unity");
        scenes.Remove(main);
        main.enabled = true;
        scenes.Insert(0, main);
        EditorBuildSettings.scenes = scenes.ToArray();
        EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
        EditorSceneManager.SaveOpenScenes();
        var fontImporter = AssetImporter.GetAtPath("Assets/TextMesh Pro/Fonts/LiberationSans.ttf");
        if (fontImporter != null) fontImporter.SaveAndReimport();
        AssetDatabase.SaveAssets();
        return new { scene = player.gameObject.scene.path, state = "Configured", buildScene = EditorBuildSettings.scenes[0].path };
    }
}
