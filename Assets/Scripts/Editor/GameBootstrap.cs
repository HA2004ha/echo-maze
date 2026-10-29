using System.IO;
using UnityEditor;
using UnityEngine;

namespace EchoMaze.EditorTools
{
    /// <summary>
    /// One menu item that makes the whole game: the project settings, the four scenes, the build list and the
    /// player settings.
    ///
    /// It exists because a clone of this repository is only a game once every generated object in it has been
    /// generated, and a new team member should not have to know the order. Everything it calls is already
    /// idempotent, which is what makes "run the bootstrap again" a safe instruction to put in a README.
    /// </summary>
    public static class GameBootstrap
    {
        [MenuItem("Tools/Echo Maze/Build Everything")]
        public static void BuildEverything()
        {
            SandboxSceneSetup.ApplyProjectSettings();

            BootSceneSetup.Build();
            Level01MazeSceneSetup.BuildLevel();
            Level02SceneSetup.BuildLevel();
            SandboxSceneSetup.BuildSandbox();

            ApplyBuildScenes();
            ApplyPlayerSettings();

            AssetDatabase.SaveAssets();
            Debug.Log("Echo Maze: bootstrap done. Open Assets/Scenes/Boot.unity and press Play — that is the " +
                      "menu the game starts on.");
        }

        /// <summary>
        /// The load order is the run order: a build boots into the menu and the two chapters follow it. The
        /// sandbox stays last so it remains reachable from the editor without being what anyone plays.
        /// </summary>
        [MenuItem("Tools/Echo Maze/Apply Build Scenes")]
        public static void ApplyBuildScenes()
        {
            string[] paths = BootSceneSetup.BuildScenes;
            var scenes = new EditorBuildSettingsScene[paths.Length];
            for (int i = 0; i < scenes.Length; i++)
            {
                if (!File.Exists(paths[i]))
                {
                    Debug.LogError("Build scene " + paths[i] + " does not exist yet: run " +
                                   "Tools/Echo Maze/Build Everything, which creates it.");
                }

                scenes[i] = new EditorBuildSettingsScene(paths[i], true);
            }

            EditorBuildSettings.scenes = scenes;
            Debug.Log("Echo Maze: build scenes are " + string.Join(", ", paths) + ".");
        }

        /// <summary>
        /// The two player settings this week is judged by. Written here rather than left to whoever last
        /// opened Build Settings, because a build that comes up in a 960x600 window makes the aim reticle
        /// smaller than its own pixels — and the cursor art is authored one texel to one screen pixel.
        /// </summary>
        public static void ApplyPlayerSettings()
        {
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.runInBackground = false;
        }
    }
}
