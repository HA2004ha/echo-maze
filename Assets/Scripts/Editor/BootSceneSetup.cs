using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using EchoMaze.Core;
using EchoMaze.Data;
using EchoMaze.Gameplay;
using EchoMaze.UI;

namespace EchoMaze.EditorTools
{
    /// <summary>
    /// The page before the game: a title, three buttons, the machine's own voice status, and the flow object
    /// that survives every scene after it.
    ///
    /// This scene is also where the microphone is proved. It opens the capture on a black screen with a meter
    /// on it, which is the only honest way to answer "does the game hear me" before the player has spent a
    /// level assuming it does.
    /// </summary>
    public static class BootSceneSetup
    {
        const string ScenePath = "Assets/Scenes/Boot.unity";

        // The run is one chapter for this milestone: the maze is the level a player is meant to finish, and
        // finishing it must show the victory panel. The corridor stays in the build as the week-4 room and
        // is entered from the editor until its hunt systems make it a chapter rather than a preview.
        public static readonly string[] Chapters = { "Level01_Maze" };
        const string MenuScene = "Boot";

        /// <summary>
        /// Everything a player build needs, in load order: the menu, the two chapters, and the math
        /// playground last so it stays reachable without becoming what the game boots into.
        /// </summary>
        public static readonly string[] BuildScenes =
        {
            "Assets/Scenes/Boot.unity",
            "Assets/Scenes/Level01_Maze.unity",
            "Assets/Scenes/Level02_Corridor.unity",
            "Assets/Scenes/Sandbox.unity"
        };

        [MenuItem("Tools/Echo Maze/Build Boot Scene")]
        public static void Build()
        {
            AssetDatabase.Refresh();

            if (!System.IO.File.Exists(ScenePath))
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            }

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            EchoTuningProfile tuning = SceneKit.EnsureTuning();
            Material arcMaterial = SceneKit.EnsureMaterial();

            SceneKit.RemoveGlobalLights2D();
            // The menu is framed tight, because the title and the buttons are authored at the reference
            // resolution and a wide camera would only show more of the black.
            SceneKit.ConfigureCamera(5f);
            SceneKit.EnsureVolume(SceneKit.EnsureVolumeProfile());
            EchoRenderer field = SceneKit.EnsureEchoField(arcMaterial);

            // Microphone mode on a scene with no player in it: the source is what the status line and the
            // calibration page read, and the director emits nothing here (MainMenu switches it off).
            EchoDirector director = SceneKit.EnsureDirector(tuning, field,
                                                            EchoDirector.SourceMode.Microphone);
            VoiceCalibration calibration = SceneKit.EnsureVoiceCalibration(director);
            GameFlow flow = SceneKit.EnsureGameFlow(Chapters, MenuScene);

            UiKit.EnsureEventSystem();
            UiKit.EnsureMainMenu(director, calibration);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            Selection.activeGameObject = flow.gameObject;
        }
    }
}
