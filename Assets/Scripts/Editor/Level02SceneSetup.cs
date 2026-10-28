using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using EchoMaze.Core;
using EchoMaze.Data;
using EchoMaze.Gameplay;

namespace EchoMaze.EditorTools
{
    /// <summary>
    /// The first screen that is supposed to look like the game: one enclosed room you can walk through,
    /// with a patrolling Crawler in it and a death. Built with editor APIs like every other scene here, so
    /// the geometry, the references and the layers are produced by Unity and can be regenerated on a fresh
    /// clone with one menu item.
    ///
    /// The room is a blockout, not a level: flat invisible walls whose only job is to be measurable. The
    /// Corridor proper (doors, hollows, a sanctuary, landmark audio) is week 4.
    /// </summary>
    public static class Level02SceneSetup
    {
        const string LevelPath = "Assets/Scenes/Level02_Corridor.unity";

        // Interior 7 x 5, which is the GDD's 4-8 m room at the top of the range, with an alcove hanging off
        // the south wall. The wave's shout reach is 6.5, so the far wall is still measurable from anywhere
        // inside, and orthographicSize 6 keeps the whole room on one screen (the camera starts following the
        // player when the maze does, in week 3).
        static readonly Vector2 PlayerSpawn = new Vector2(2f, -3.15f);

        // The creature walks a vertical leg through the north middle band, and the run starts at its far end:
        // 5.4 m from the player at spawn, 4.0 m at the closest point of the leg. The first route anyone played
        // was a horizontal leg at y = -0.6 straight over the alcove mouth 2.55 m above the spawn, which made
        // the room unsurvivable — a shout is heard across a room this small by design (rho * R covers it), so
        // the distance is the only thing between the first ping and a hunt arriving. The leg is placed by the
        // furniture, not near it: at x = 0.2 its two bumps (r 0.32 at +-0.42) clear Pillar by 0.36 m and
        // Box_Corner by 0.86 m, and 0.28 m below Wall_North.
        static readonly Vector2 CrawlerStart = new Vector2(0.2f, 1.9f);
        static readonly Vector2[] Patrol = { new Vector2(0.2f, 0.4f), new Vector2(0.2f, 1.9f) };

        [MenuItem("Tools/Echo Maze/Week 2 Bootstrap")]
        public static void Bootstrap()
        {
            SandboxSceneSetup.ApplyProjectSettings();

            // Both scenes share the tuning asset, the volume profile and the arc material, and every builder
            // is idempotent, so the week 2 bootstrap also brings the math playground up to date: it has to
            // swap PlayerMock for Player, or it would ship a missing script.
            SandboxSceneSetup.BuildSandbox();
            BuildLevel();
            Debug.Log("Echo Maze: week 2 bootstrap done. Open " + LevelPath + " and press Play.");
        }

        [MenuItem("Tools/Echo Maze/Build Level02 Corridor")]
        public static void BuildLevel()
        {
            AssetDatabase.Refresh();

            // A new scene is created through the editor API and saved before it is opened, so the file Unity
            // writes is the one the clone gets; hand-copied YAML from Sandbox is how a scene ends up with a
            // serialization version this editor does not understand.
            if (!System.IO.File.Exists(LevelPath))
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), LevelPath);
            }

            EditorSceneManager.OpenScene(LevelPath, OpenSceneMode.Single);

            // After OpenScene, never before: a scene load unloads assets that have no scene reference, and
            // a local variable pointing at one of them comes back destroyed.
            EchoTuningProfile tuning = SceneKit.EnsureTuning();
            Material arcMaterial = SceneKit.EnsureMaterial();

            SceneKit.RemoveGlobalLights2D();
            SceneKit.ConfigureCamera();
            SceneKit.EnsureVolume(SceneKit.EnsureVolumeProfile());
            EchoRenderer field = SceneKit.EnsureEchoField(arcMaterial);

            BuildRoom();

            GameObject playerObject = SceneKit.EnsurePlayer(PlayerSpawn);
            TouchMarks marks = SceneKit.EnsureTouchMarks();
            EchoDirector director = SceneKit.EnsureDirector(tuning, field);
            Player player = playerObject.GetComponent<Player>();
            SceneKit.SetReference(player, "director", director);
            SceneKit.SetReference(player, "marks", marks);

            SceneKit.EnsureCrawler(CrawlerStart, Patrol, director, player);
            SceneKit.EnsureRunController(player, SceneKit.EnsureDeathNotice());

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), LevelPath);
            Selection.activeGameObject = playerObject;
        }

        /// <summary>
        /// Four walls, one pillar, one box corner, one alcove and one exit. Every number here is chosen so
        /// the Crawler's patrol legs miss the furniture by more than its own body width: a patrolling thing
        /// that jams on a blockout prop teaches the tester nothing.
        /// </summary>
        static void BuildRoom()
        {
            SceneKit.EnsureWall("Wall_North", new Vector2(0f, 2.7f), new Vector2(7.8f, 0.4f));
            SceneKit.EnsureWall("Wall_West", new Vector2(-3.7f, 0f), new Vector2(0.4f, 5.8f));
            SceneKit.EnsureWall("Wall_East", new Vector2(3.7f, 0f), new Vector2(0.4f, 5.8f));

            // The south wall is split to leave the alcove opening, so the nock is a real pocket in the wave
            // and not a box standing in front of a wall.
            SceneKit.EnsureWall("Wall_SouthWest", new Vector2(-1.45f, -2.7f), new Vector2(4.9f, 0.4f));
            SceneKit.EnsureWall("Wall_SouthEast", new Vector2(3.45f, -2.7f), new Vector2(0.9f, 0.4f));
            SceneKit.EnsureWall("Alcove_West", new Vector2(1.2f, -3.3f), new Vector2(0.4f, 1.4f));
            SceneKit.EnsureWall("Alcove_East", new Vector2(2.8f, -3.3f), new Vector2(0.4f, 1.4f));
            SceneKit.EnsureWall("Alcove_Back", new Vector2(2f, -3.9f), new Vector2(2f, 0.4f));

            SceneKit.EnsureShape("Pillar", new Vector2(-1.5f, 1.3f), SceneKit.Shape.Circle(0.6f),
                                 SurfaceKind.Plain);
            SceneKit.EnsureShape("Box_Corner", new Vector2(2.4f, 1.5f), SceneKit.Shape.Box(1.2f, 1.2f),
                                 SurfaceKind.Plain);

            // The exit is a trigger volume: the wave must not measure it (triggers are excluded from the
            // fan), and walking into it is the level-complete rule, which lands with the level flow in
            // week 4. It exists now so the room has a goal the designer can see and move.
            SceneKit.EnsureShape("Exit_Tile", new Vector2(-3.3f, 0.2f), SceneKit.Shape.TriggerBox(0.7f, 1.8f),
                                 SurfaceKind.Plain);
        }
    }
}
