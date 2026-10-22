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
        const string DriverScript = "Tools/unity-yaml-merge.sh";

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
                report.AppendLine("INFO  " + DriverKey + " = " + driver);

                // Git hands a merge driver extension-less temp files (.merge_file_XXXXXX), and
                // UnityYAMLMerge chooses its parser from the extension. A driver pointed straight at
                // the binary therefore registers cleanly, passes every config check, and then fails
                // every real merge, leaving the scene with only our side and no conflict markers.
                // Tools/unity-yaml-merge.sh puts the extension back.
                bool wrapped = driver.Contains("unity-yaml-merge");
                if (!wrapped && !driver.Contains("UnityYAMLMerge") && !driver.Contains("UnitySmartMerge"))
                {
                    ok = false;
                    report.AppendLine("FAIL  the driver points at neither " + DriverScript + " nor a Unity merge tool.");
                }
                else if (!wrapped && !IsWindows)
                {
                    ok = false;
                    report.AppendLine("FAIL  the driver calls the merge binary directly, which cannot resolve " +
                                      "anything on this platform. Re-run Tools > Echo Maze > Register Unity Merge Driver.");
                }
                else
                {
                    string probe = ProbeMerge(driver);
                    if (probe.StartsWith("FAIL"))
                    {
                        ok = false;
                    }

                    report.AppendLine(probe);
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
            string script = Path.GetFullPath(Path.Combine(ProjectRoot, DriverScript));
            if (!File.Exists(tool))
            {
                EditorUtility.DisplayDialog("UnityYAMLMerge not found",
                    "Expected it at:\n" + tool +
                    "\n\nThis path is per-machine. Find UnityYAMLMerge under your editor install and write the " +
                    "three git config lines yourself, as documented in Docs/TEAM_SETUP.md.", "OK");
                return;
            }

            if (!IsWindows && !File.Exists(script))
            {
                EditorUtility.DisplayDialog("Merge wrapper missing",
                    script + "\n\nGit passes the merge tool extension-less temp files, so the binary alone " +
                    "resolves nothing and this platform needs the wrapper. Restore it from git.", "OK");
                return;
            }

            Git("config", "merge.unityyamlmerge.name", DriverName);
            if (IsWindows)
            {
                // Not verified from a Linux machine: the Windows build may detect the type itself.
                // Validate immediately after registering on that platform.
                Git("config", DriverKey, "\"" + tool + "\" merge -p %O %A %B %P");
            }
            else
            {
                Git("config", DriverKey, "UNITY_YAML_MERGE='" + tool + "' '" + script + "' %O %A %B %P");
            }

            Git("config", RecursiveKey, "binary");
            Validate();
        }

        /// <summary>
        /// A registered driver is not a working driver: the bug this project shipped with was a config
        /// line that looked correct and merged nothing. So perform one real three-way merge on a copy
        /// of the sandbox scene, with the two sides editing adjacent lines of the same GameObject — the
        /// exact case a text merge conflicts on — and require both changes in the result.
        /// </summary>
        static string ProbeMerge(string driver)
        {
            if (IsWindows)
            {
                return "SKIP  the merge probe shells out to /bin/sh; validate scene merging on Windows by hand.";
            }

            string scene = Path.Combine(ProjectRoot, "Assets/Scenes/Sandbox.unity");
            if (!File.Exists(scene))
            {
                return "SKIP  " + scene + " is missing, so there is nothing to probe.";
            }

            string[] lines = File.ReadAllLines(scene);
            int target = -1;
            for (int i = 0; i + 1 < lines.Length; i++)
            {
                if (lines[i] == "  m_Name: Pillar_Concrete" && lines[i + 1].StartsWith("  m_TagString:"))
                {
                    target = i;
                    break;
                }
            }

            if (target < 0)
            {
                return "SKIP  Pillar_Concrete with its tag line not found in Sandbox.unity; probe fixture is stale.";
            }

            string dir = Path.Combine(Path.GetTempPath(), "echo-maze-merge-probe-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(dir);
                string baseFile = Path.Combine(dir, "base.unity");
                string oursFile = Path.Combine(dir, "ours.unity");
                string theirsFile = Path.Combine(dir, "theirs.unity");
                string probePath = Path.Combine(dir, "probe.unity");

                File.WriteAllLines(baseFile, lines);

                string[] ours = (string[])lines.Clone();
                ours[target] = "  m_Name: ProbeLeft";
                File.WriteAllLines(oursFile, ours);

                string[] theirs = (string[])lines.Clone();
                theirs[target + 1] = "  m_TagString: ProbeRight";
                File.WriteAllLines(theirsFile, theirs);

                // Build the command exactly the way git expands a merge driver. Only the extension of
                // %P matters to the wrapper, and nothing here may touch the real scene.
                string command = driver
                    .Replace("%O", "'" + baseFile + "'")
                    .Replace("%A", "'" + oursFile + "'")
                    .Replace("%B", "'" + theirsFile + "'")
                    .Replace("%P", "'" + probePath + "'");

                int exit = Sh(command, out string output);
                string merged = File.Exists(oursFile) ? File.ReadAllText(oursFile) : string.Empty;
                bool both = merged.Contains("  m_Name: ProbeLeft") && merged.Contains("  m_TagString: ProbeRight");

                return both && exit == 0
                    ? "PASS  probe merge resolved both sides of a real scene (exit 0)."
                    : "FAIL  probe merge kept " + (merged.Contains("ProbeLeft") ? "ours" : "neither") +
                      (merged.Contains("ProbeRight") ? " and theirs" : " but not theirs") +
                      ", exit " + exit + ". " + output;
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        static bool IsWindows => Environment.OSVersion.Platform == PlatformID.Win32NT;

        /// <summary>
        /// Runs a shell command and reports its exit code, which Git() throws away.
        /// </summary>
        static int Sh(string command, out string output)
        {
            output = string.Empty;
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
                    output = "process not started";
                    return -1;
                }

                string stdout = process.StandardOutput.ReadToEnd();
                string stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();
                output = (stdout + stderr).Trim();
                return process.ExitCode;
            }
            catch (Exception e)
            {
                output = e.Message;
                return -1;
            }
        }

        /// <summary>
        /// EditorApplication.applicationContentsPath already ends in the editor's Data folder, so the
        /// tool sits directly under it. The name differs by platform: UnityYAMLMerge on Linux and the
        /// macOS bundle, UnitySmartMerge.exe on Windows. Older layouts put Tools one level higher.
        /// </summary>
        static string EditorYamlMergeCandidate()
        {
            string contents = EditorApplication.applicationContentsPath;
            string[] relative =
            {
                "Tools/UnityYAMLMerge",
                "Tools/UnitySmartMerge.exe",
                "Tools/UnitySmartMerge",
                "../Tools/UnityYAMLMerge",
                "Data/Tools/UnityYAMLMerge"
            };

            foreach (string candidate in relative)
            {
                string full = Path.GetFullPath(Path.Combine(contents, candidate));
                if (File.Exists(full))
                {
                    return full;
                }
            }

            string expected = Path.GetFullPath(Path.Combine(contents, relative[0]));
            Debug.LogWarning("UnityYAMLMerge not found under " + contents + " (project " + ProjectRoot +
                             "). Tried " + string.Join(", ", relative) + ". Reporting " + expected);
            return expected;
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
