using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using EchoMaze.Core;
using EchoMaze.Data;
using EchoMaze.Gameplay;

namespace EchoMaze.EditorTools
{
    /// <summary>
    /// Chapter one: the maze drawn from <c>Assets/Data/Levels/Level01_Maze.txt</c>, with nothing in it that
    /// can kill the player.
    ///
    /// It is the GDD's tutorial space and it earns its place by being the only room where the two mechanics
    /// the game runs on can be learned without a cost: a door that has to be opened and heard, and a floor of
    /// glass that answers a step. An enemy here would teach the player to stop experimenting, which is the
    /// opposite of what the first room is for.
    /// </summary>
    public static class Level01MazeSceneSetup
    {
        const string LevelPath = "Assets/Scenes/Level01_Maze.unity";
        const string GridPath = "Assets/Data/Levels/Level01_Maze.txt";

        /// <summary>World units per grid cell — the same number the level file's legend documents.</summary>
        const float CellSize = 2f;

        // 21 x 15 cells at two metres is 42 x 30 world units. The camera shows 12 units of height at
        // orthographicSize 6, so the maze is roughly two and a half screens tall: close enough that a single
        // ping's arcs fill the frame, far enough that the room beyond the next corner stays unknown.
        const float CameraSize = 6f;

        [MenuItem("Tools/Echo Maze/Build Level01 Maze")]
        public static void BuildLevel()
        {
            AssetDatabase.Refresh();

            if (!System.IO.File.Exists(LevelPath))
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), LevelPath);
            }

            EditorSceneManager.OpenScene(LevelPath, OpenSceneMode.Single);

            EchoTuningProfile tuning = SceneKit.EnsureTuning();
            Material arcMaterial = SceneKit.EnsureMaterial();

            SceneKit.RemoveGlobalLights2D();
            SceneKit.EnsureVolume(SceneKit.EnsureVolumeProfile());
            EchoRenderer field = SceneKit.EnsureEchoField(arcMaterial);

            // The body is parked at the origin and the grid's own 'P' cell moves it during MazeLevel.Awake,
            // which runs before any Update: the spawn is authored in the map, not here.
            GameObject playerObject = SceneKit.EnsurePlayer(Vector2.zero);
            TouchMarks marks = SceneKit.EnsureTouchMarks();
            EchoDirector director = SceneKit.EnsureDirector(tuning, field);
            Player player = playerObject.GetComponent<Player>();
            SceneKit.SetReference(player, "director", director);
            SceneKit.SetReference(player, "marks", marks);
            SceneKit.EnsureAimCursor(playerObject);

            LevelSession session = LevelSceneKit.BuildChrome(director, player,
                                                             new Vector2(21f * CellSize * 0.5f,
                                                                         15f * CellSize * 0.5f),
                                                             CameraSize, "chapter one  ·  the maze");

            // The tutorial space is the only one that teaches the controls on screen.
            LevelSceneKit.EnsureHints(director, player, marks, session);

            // The rig's clamp is overwritten by the level from the grid it actually read; what is wired here
            // is the object, so the camera follows from the first frame rather than after the first ping.
            var rig = UnityEngine.Object.FindAnyObjectByType<PlayerCameraRig>();
            SceneKit.EnsureMazeLevel(GridPath, CellSize, director, player, session, rig);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), LevelPath);
            Selection.activeGameObject = playerObject;
        }
    }
}
