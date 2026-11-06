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
    /// with a patrolling Crawler in it, a death, and a way out. Built with editor APIs like every other
    /// scene here, so the geometry, the references and the layers are produced by Unity and can be
    /// regenerated on a fresh clone with one menu item.
    ///
    /// The room is still a blockout — flat invisible walls whose only job is to be measurable — but it is a
    /// chapter now: it has the HUD, pause and exit the maze has, so the two rooms can be played back to back
    /// and the build is a game rather than a demo reel.
    /// </summary>
    public static class Level02SceneSetup
    {
        const string LevelPath = "Assets/Scenes/Level02_Corridor.unity";

        // Interior 7 x 5, which is the GDD's 4-8 m room at the top of the range, with an alcove hanging off
        // the south wall. The wave's shout reach is 6.5, so the far wall is still measurable from anywhere
        // inside, and orthographicSize 6 keeps the whole room on one screen — which is also why the camera
        // rig's clamp below is the room itself: the follow has nowhere to go, and the blockout framing is
        // preserved by the same numbers that let the maze move.
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

            GameObject exit = BuildRoom();

            GameObject playerObject = SceneKit.EnsurePlayer(PlayerSpawn);
            TouchMarks marks = SceneKit.EnsureTouchMarks();
            EchoDirector director = SceneKit.EnsureDirector(tuning, field);
            Player player = playerObject.GetComponent<Player>();
            SceneKit.SetReference(player, "director", director);
            SceneKit.SetReference(player, "marks", marks);
            SceneKit.EnsureAimCursor(playerObject);

            SceneKit.EnsureCrawler(CrawlerStart, Patrol, director, player);

            // The interior half-extents, not the wall rectangle: at this camera size the clamp range inverts
            // and the rig pins itself to the room centre, which is the fixed framing the blockout was built
            // with. The rig exists anyway so that both chapters run the same camera code.
            LevelSession session = LevelSceneKit.BuildChrome(director, player, new Vector2(3.5f, 2.5f),
                                                             6f, "chapter two  ·  the corridor");
            SceneKit.EnsureLevelExit(exit, session);

            // The two pockets in the wall. Their boxes are the inside of each alcove only (1.2 x 1.2 m, the
            // 0.5 m body fits with 0.35 m to spare on each side), so standing in the room's doorway of the
            // alcove is not cover. The first one is the spawn.
            LevelSceneKit.EnsureHollow("Hollow_South", new Vector2(2f, -3.1f), new Vector2(1.2f, 1.2f), player, session);
            LevelSceneKit.EnsureHollow("Hollow_North", new Vector2(-2.5f, 3.1f), new Vector2(1.2f, 1.2f), player, session);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), LevelPath);
            Selection.activeGameObject = playerObject;
        }

        /// <summary>
        /// Four walls, one pillar, one box corner, one alcove and the exit tile it returns. Every number here
        /// is chosen so the Crawler's patrol legs miss the furniture by more than its own body width: a
        /// patrolling thing that jams on a blockout prop teaches the tester nothing.
        /// </summary>
        static GameObject BuildRoom()
        {
            // The single north slab of earlier builds would close the new opening, and EnsureWall never deletes.
            GameObject oldNorth = GameObject.Find("Wall_North");
            if (oldNorth != null)
            {
                Object.DestroyImmediate(oldNorth);
            }

            // The north wall is split like the south one, leaving the second pocket at the far side of the room
            // from the spawn: opening x -3.1..-1.9, inside y 2.5..3.7. It clears the pillar's left edge (-2.1)
            // by the body's width at y = 1.3 and is 2.9 m from the Crawler's far patrol point.
            SceneKit.EnsureWall("Wall_NorthWest", new Vector2(-3.5f, 2.7f), new Vector2(0.8f, 0.4f));
            SceneKit.EnsureWall("Wall_NorthEast", new Vector2(1f, 2.7f), new Vector2(5.8f, 0.4f));
            SceneKit.EnsureWall("Alcove2_West", new Vector2(-3.3f, 3.3f), new Vector2(0.4f, 1.4f));
            SceneKit.EnsureWall("Alcove2_East", new Vector2(-1.7f, 3.3f), new Vector2(0.4f, 1.4f));
            SceneKit.EnsureWall("Alcove2_Back", new Vector2(-2.5f, 3.9f), new Vector2(2f, 0.4f));
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

            // The exit is a trigger volume and nothing more: the wave must not measure it (triggers are
            // excluded from the fan), it draws no glow (the GDD wants the way out found by sound), and the
            // LevelExit component put on it in BuildLevel is what ends the chapter when the player stands
            // beside it and presses E.
            return SceneKit.EnsureShape("Exit_Tile", new Vector2(-3.3f, 0.2f),
                                        SceneKit.Shape.TriggerBox(0.7f, 1.8f), SurfaceKind.Plain);
        }
    }
}
