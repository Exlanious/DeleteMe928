# Lava Climb verification

Implemented and checked in Unity 6000.6.0f1 through Unity MCP and its C# script runner.

- Custom `Skybox/Height Gradient` shader: supported, no shader messages. Live height blend reached 0.999999 at 100 units above the start and returned below 0.00005 on descent.
- Height score: one point per full Unity unit above the run's starting Y. The score holds the greatest height reached; falling and reclimbing below that height keep the score. A separate best score persists through PlayerPrefs.
- UGUI / TextMeshPro: the HUD displayed 25 in its probe, and a later live climb/fall probe held 32. All HUD text uses TMP with a valid LiberationSans SDF font.
- Start screen: movement and camera control wait for Start. Lava remained at Y = -3.5 while the start screen was open and after entering Ready.
- First movement: keyboard movement started the run. A first-jump check captured Ready -> Playing, with lava beginning to rise to Y = -3.49324.
- Death: lava contact opened Game Over, froze movement and score tracking, and released the cursor.
- Retry: the button and Input System pointer events reloaded the level, reset the run score to zero, reset movement, retained the best score, and left lava waiting for movement.
- Platform generation: 20 deterministic seeds, 100 steps each, 2,000 total platforms. No dead ends, out-of-bounds placement, or rises above the calculated double-jump limit. Recovery steps stayed below the 4.44-unit maximum jump rise.
- Final checks completed with zero Unity console errors and warnings. Artificial test scores were removed. Temporary input-routing changes used for background editor tests were restored.

`Assets/Scenes/PlatformerLevel.unity` is the first enabled build scene. Press Play, then use the Start button or Enter. Move with WASD/arrows/left stick, jump with Space/south button, and dash with Left Shift/right shoulder.

The HeightSkybox component exposes the transition height (100 units) and smoothing speed (3). HeightScore exposes points per unit (1). Edit the HeightSkybox material to adjust the two sky palettes.

Reusable checks live in `AgentScripts/RegressionChecks.cs`. Run `RegressionChecks.Generation` in Play Mode (pausing first is convenient), and `RegressionChecks.Score` through Unity MCP `run_script`. Screenshots in this directory include test-score probes.

## Open sky and expanded lava

- Added two tile rings around the original 120 x 120 lava block: 25 tiles form a 600 x 600 surface. They share the existing world-space magma material, one rising controller, and one 600 x 600 hazard trigger. The original trigger depth and surface height remain unchanged.
- Removed boundary-wall creation and resizing from PlatformManager. Play Mode and retry both have zero boundary walls.
- Checked trigger coverage over the outer tiles, shared materials, aligned tile heights during rising, and player-following movement. Falling outside the platform generation area still opens Game Over. Retry restores Ready and lava Y = -3.5.
- Rechecked 2,000 platform-generation steps across 20 seeds with no failures; Unity reported zero console errors and warnings.
- The algorithm streams an endless upward platform path, spawning ahead and trimming behind. Its horizontal generation area remains 26 x 60 units; it does not stream infinite X/Z terrain. The finite lava surface follows the player horizontally.
- `open-sky-level.png` shows the unobstructed sky and tiled lava from the starting pad. `AgentScripts/OpenSkyLevel.cs` provides the idempotent Unity MCP construction and surface inspection calls.
