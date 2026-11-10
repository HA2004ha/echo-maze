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

        // The run is the catalogue's order, not a second list that has to be kept in step: a chapter added
        // to ChapterCatalog is in the flow, in the build list and on the story page by the same edit. The
        // corridor is chapter two and is hand-built, so it is in here without a grid file.
        public static string[] Chapters => ChapterCatalog.SceneNames;
        const string MenuScene = "Boot";

        /// <summary>
        /// Everything a player build needs, in load order: the menu, every chapter, and the math playground
        /// last so it stays reachable without becoming what the game boots into.
        /// </summary>
        public static string[] BuildScenes
        {
            get
            {
                var paths = new System.Collections.Generic.List<string> { "Assets/Scenes/Boot.unity" };
                foreach (string scene in Chapters)
                {
                    paths.Add("Assets/Scenes/" + scene + ".unity");
                }

                paths.Add("Assets/Scenes/Sandbox.unity");
                return paths.ToArray();
            }
        }

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

            // The menu is where the theme is first heard: the board opens on it because Boot has no level
            // session in it, and keeps playing through every chapter after that.
            SceneKit.EnsureSoundBoard();

            // The rejected phase-1 backdrop: collider-only 2D walls that were invisible between waves.
            // Gone on rebuild rather than left dormant, so nobody can mistake its objects for the stage.
            SceneKit.RemoveLegacyMenuStage();

            // GDD: "Background is an echo arc drifting slowly" — here a real 3D maze model on a table,
            // turning under its own camera, its walls washed by a wave of light that draws no lines.
            // The flat orthographic camera keeps its tag and its tests; Menu3DStage switches the view
            // at runtime, and levels never see any of this because they load single.
            GameObject maze = SceneKit.EnsureMenuMaze3D();
            GameObject menuCamera = SceneKit.EnsureMenuCamera3D(maze);

            UiKit.EnsureEventSystem();
            MainMenu menu = UiKit.EnsureMainMenu(director, calibration);
            SceneKit.EnsureMenuStage(menu.gameObject, menuCamera);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            Selection.activeGameObject = flow.gameObject;
        }
    }
}
