using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Process = System.Diagnostics.Process;
using ProcessStartInfo = System.Diagnostics.ProcessStartInfo;

namespace EchoMaze.EditorTools
{
    /// <summary>
    /// Checks the two things that silently break a Unity repo on a fresh machine: the YAML smart
    /// merge driver that .gitattributes demands but git cannot find, and LFS actually running for
    /// binaries. Both failures are invisible until a scene merge explodes or a 40 MB png lands raw
    /// in history, which is what happened on the first commit of this project.
    /// </summary>
    public static class GitSetupValidator
    {
        const string DriverName = "Unity Smart Merge";
        const string DriverKey = "merge.unityyamlmerge.driver";
        const string RecursiveKey = "merge.unityyamlmerge.recursive";

        [MenuItem("Tools/Echo Maze/Validate Git Setup")]
        public static void Validate()
        {
            var report = new StringBuilder();
            bool ok = true;

            string driver = GitConfig(DriverKey);
            if (string.IsNullOrEmpty(driver))
            {
                ok = false;
                report.AppendLine("FAIL  " + DriverKey + " is not registered. .gitattributes asks for " +
                                  "merge=unityyamlmerge, so every scene and prefab merge falls back to a text " +
                                  "merge. Run Tools > Echo Maze > Register Unity Merge Driver.");
            }
            else
            {
                report.AppendLine("PASS  " + DriverKey + " = " + driver);
                if (!driver.Contains("UnityYAMLMerge"))
                {
                    ok = false;
                    report.AppendLine("FAIL  the driver does not point at UnityYAMLMerge. The path is " +
                                      "per-machine: fix it in Docs/TEAM_SETUP.md and re-register.");
                }
            }

            string recursive = GitConfig(RecursiveKey);
            report.AppendLine(string.IsNullOrEmpty(recursive)
                ? "WARN  " + RecursiveKey + " is unset; nested scene merges may not resolve cleanly."
                : "PASS  " + RecursiveKey + " = " + recursive);

            string lfsVersion = Git("lfs", "version");
            if (lfsVersion.StartsWith("#error") || lfsVersion.Length == 0)
            {
                ok = false;
                report.AppendLine("FAIL  git lfs is not runnable: " + lfsVersion);
            }
            else
            {
                report.AppendLine("PASS  " + lfsVersion);

                string tracked = Git("lfs", "ls-files", "--name-only");
                report.AppendLine(string.IsNullOrWhiteSpace(tracked)
                    ? "WARN  nothing is tracked by LFS yet. Correct while the repo has no binaries; wrong the " +
                        "moment a png/ttf/wav is committed raw."
                    : "PASS  LFS tracks: " + tracked.Replace('\n', ' ').Trim());

                string filters = Git("config", "--get-regexp", "^filter\\.lfs");
                report.AppendLine(string.IsNullOrEmpty(filters)
                    ? "FAIL  no filter.lfs config; run git lfs install --local"
                    : "PASS  filter.lfs configured");
            }

            string editorVersion = Application.unityVersion;
            report.AppendLine("INFO  editor " + editorVersion + " · project " + ProjectRoot);

            Debug.Log("Echo Maze git setup\n" + report +
                      (ok ? "\nall good." : "\nneeds fixing before the team clones."));
            EditorUtility.DisplayDialog(ok ? "Git setup OK" : "Git setup needs work", report.ToString(), "Close");
        }

        [MenuItem("Tools/Echo Maze/Register Unity Merge Driver")]
        public static void RegisterDriver()
        {
            string tool = Path.GetFullPath(EditorYamlMergeCandidate());
            if (!File.Exists(tool))
            {
                EditorUtility.DisplayDialog("UnityYAMLMerge not found",
                    "Expected it at:\n" + tool +
                    "\n\nThis path is per-machine. Find UnityYAMLMerge under your editor install and write the " +
                    "three git config lines yourself, as documented in Docs/TEAM_SETUP.md.", "OK");
                return;
            }

            Git("config", "merge.unityyamlmerge.name", DriverName);
            Git("config", DriverKey, tool + " merge -p %O %A %B %P");
            Git("config", RecursiveKey, "binary");
            Validate();
        }

        /// <summary>
        /// Application.dataPath of an opened project is &lt;install&gt;/.../&lt;project&gt;/Assets, and the
        /// editor binary sits four levels above the project folder in a Hub install.
        /// </summary>
        static string EditorYamlMergeCandidate()
        {
            string projectRoot = ProjectRoot;
            string fromHub = Path.Combine(EditorApplication.applicationContentsPath, "Data/Tools/UnityYAMLMerge");
            if (File.Exists(fromHub))
            {
                return fromHub;
            }

            Debug.LogWarning("UnityYAMLMerge not found at " + fromHub + " (project " + projectRoot + ")");
            return fromHub;
        }

        static string GitConfig(string key)
        {
            string value = Git("config", "--get", key);
            return value.StartsWith("#error") ? string.Empty : value;
        }

        /// <summary>
        /// Every argument is quoted for the shell here. The merge driver value contains spaces and the
        /// %O/%A/%B/%P tokens, and git must receive it as a single string, so the quoting has to be
        /// explicit rather than left to however Unity splits ProcessStartInfo.Arguments.
        /// </summary>
        static string Git(params string[] args)
        {
            var command = new StringBuilder("git");
            foreach (string arg in args)
            {
                command.Append(' ').Append(ShellQuote(arg));
            }

            try
            {
                var start = new ProcessStartInfo
                {
                    FileName = "/bin/sh",
                    Arguments = "-c \"" + command + "\"",
                    WorkingDirectory = ProjectRoot,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using Process process = Process.Start(start);
                if (process == null)
                {
                    return "#error process not started";
                }

                string output = process.StandardOutput.ReadToEnd();
                process.StandardError.ReadToEnd();
                return output.Trim();
            }
            catch (Exception e)
            {
                return "#error " + e.Message;
            }
        }

        static string ShellQuote(string value) => "'" + value.Replace("'", "'\\''") + "'";

        static string ProjectRoot => Path.GetFullPath(Application.dataPath + "/..");
    }
}
