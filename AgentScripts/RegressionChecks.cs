using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class RegressionChecks
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    public static object Generation(int seedCount = 20, int stepsPerSeed = 100)
    {
        if (!Application.isPlaying) throw new Exception("Run generation checks in Play Mode so platform cleanup uses runtime destruction.");
        var template = Object.FindAnyObjectByType<PlatformManager>();
        var type = typeof(PlatformManager);
        var start = type.GetMethod("Start", PrivateInstance);
        var spawn = type.GetMethod("SpawnNextPlatform", PrivateInstance);
        var trim = type.GetMethod("DestroyPlatformsBehind", PrivateInstance);
        var pathField = type.GetField("path", PrivateInstance);
        var indexField = type.GetField("currentIndex", PrivateInstance);
        var center = (Vector2)type.GetField("levelBoundsCenter", PrivateInstance).GetValue(template);
        var size = (Vector2)type.GetField("levelBoundsSize", PrivateInstance).GetValue(template);
        var footprint = (Vector2)type.GetField("platformDimensions", PrivateInstance).GetValue(template);
        var player = Object.FindAnyObjectByType<PlayerMovement>();
        float maxRise = player.MaximumDoubleJumpRise;
        var randomState = UnityEngine.Random.state;
        int generated = 0;
        int recoverySteps = 0;
        try
        {
            for (int seed = 0; seed < seedCount; seed++)
            {
                GameObject clone = Object.Instantiate(template.gameObject);
                clone.name = "Temporary Generation Check";
                try
                {
                    // The live template already contains runtime walls/platforms; each seed starts from the authored level.
                    for (int child = clone.transform.childCount - 1; child >= 0; child--)
                    {
                        var obj = clone.transform.GetChild(child).gameObject;
                        if (obj.name.StartsWith("Generated ") || obj.name == "Level Boundary Wall") Object.DestroyImmediate(obj);
                    }
                    UnityEngine.Random.InitState(seed);
                    Physics.SyncTransforms();
                    var manager = clone.GetComponent<PlatformManager>();
                    start.Invoke(manager, null);
                    var path = (IList)pathField.GetValue(manager);
                    if (path.Count < 11) throw new Exception($"Initial route failed at seed {seed}.");
                    for (int step = 0; step < stepsPerSeed; step++)
                    {
                        indexField.SetValue(manager, path.Count - 1);
                        trim.Invoke(manager, null);
                        int before = path.Count;
                        var previous = Top(path[before - 1]);
                        spawn.Invoke(manager, null);
                        if (path.Count != before + 1) throw new Exception($"Route stopped at seed {seed}, step {step}.");
                        var next = Top(path[path.Count - 1]);
                        float rise = next.y - previous.y;
                        if (rise <= 0 || rise > maxRise - 0.25f) throw new Exception($"Unreachable rise {rise}, seed {seed}.");
                        if (Mathf.Abs(next.x - center.x) + footprint.x * .5f > size.x * .5f + .001f
                            || Mathf.Abs(next.z - center.y) + footprint.y * .5f > size.y * .5f + .001f)
                            throw new Exception($"Platform outside bounds, seed {seed}.");
                        if (rise > 1.25f) recoverySteps++;
                        generated++;
                    }
                }
                finally { Object.DestroyImmediate(clone); }
            }
        }
        finally { UnityEngine.Random.state = randomState; }
        return new { check = "Platform generation", seedCount, stepsPerSeed, generated, recoverySteps, maxRise };
    }

    public static object Score()
    {
        GameObject probe = new GameObject("Temporary Score Check");
        try
        {
            var score = probe.AddComponent<HeightScore>();
            score.ResetRun(3f);
            score.RecordHeight(15.9f);
            int peak = score.Score;
            score.RecordHeight(-10f);
            int fall = score.Score;
            score.RecordHeight(14f);
            int reclimb = score.Score;
            score.RecordHeight(20.2f);
            int newPeak = score.Score;
            score.SetTracking(false);
            score.RecordHeight(100f);
            int frozen = score.Score;
            score.ResetRun(3f);
            if (peak != 12 || fall != 12 || reclimb != 12 || newPeak != 17 || frozen != 17 || score.Score != 0)
                throw new Exception("Height score regression");
            return new { check = "Height score", peak, fall, reclimb, newPeak, frozen, reset = score.Score };
        }
        finally { Object.DestroyImmediate(probe); }
    }

    private static Vector3 Top(object platform)
    {
        return (Vector3)platform.GetType().GetField("topCenter").GetValue(platform);
    }
}
