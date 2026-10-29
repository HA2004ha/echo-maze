using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace EchoMaze.EditorTools
{
    /// <summary>
    /// The Linux player, built from the code rather than from whatever happens to be in Build Settings.
    ///
    /// It rebuilds the scenes first: a build of stale YAML is a build of a game nobody wrote, and the
    /// difference between the two is a bug report that cannot be reproduced. Everything it calls is
    /// idempotent, so running it from a terminal is the same instruction as clicking the menu item.
    /// </summary>
    public static class PlayerBuild
    {
        /// <summary>Where the player lands, relative to the project root. Kept out of version control.</summary>
        public const string OutputFolder = "Builds";

        /// <summary>
        /// The leaf of the build path is the name of the executable Unity writes, and on Linux it gets no
        /// extension: this project's player is <c>Builds/echo-maze</c>, not <c>Builds/Linux</c> and not
        /// anything named after <c>PlayerSettings.productName</c>.
        /// </summary>
        public const string ExecutableName = "echo-maze";

        /// <summary>
        /// A development build, because this artifact is what the team tests with: it writes the log this
        /// project's verification reads, and it shows a dialog instead of vanishing when a scene is broken.
        /// Flip to false for a release candidate.
        /// </summary>
        const bool Development = true;

        [MenuItem("Tools/Echo Maze/Build Player")]
        public static void Build()
        {
            GameBootstrap.BuildEverything();

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = BootSceneSetup.BuildScenes,
                locationPathName = Path.Combine(OutputFolder, ExecutableName),
                target = BuildTarget.StandaloneLinux64,
                options = Development ? BuildOptions.Development : BuildOptions.None,
            };

            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone,
                                                                 BuildTarget.StandaloneLinux64))
            {
                Debug.LogError("Could not switch to StandaloneLinux64 — nothing was built.");
                return;
            }

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            string message = "Echo Maze: " + summary.result + " in " + summary.totalTime + " (" +
                             (summary.totalSize / (1024 * 1024)) + " MB) → " + ExecutablePath();

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log(message);
            }
            else
            {
                Debug.LogError(message);
            }

            // A batchmode run reports through its exit code; a menu click in the editor should not close it.
            if (Application.isBatchMode && summary.result != BuildResult.Succeeded)
            {
                EditorApplication.Exit(1);
            }
        }

        /// <summary>The file to launch after <see cref="Build"/> — the same path the build was asked for.</summary>
        public static string ExecutablePath() => Path.Combine(OutputFolder, ExecutableName);
    }
}
