using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using EchoMaze.Core;
using EchoMaze.Gameplay;

namespace EchoMaze.DebugTools
{
    /// <summary>
    /// A screenshot button for a game that is verified from a terminal.
    ///
    /// Every visual claim in this project — the glow, the arc density, the size of the reticle — has to be
    /// checked in the <b>player build</b>, because URP strips and screen-space decisions only exist there, and
    /// a headless editor run renders nothing. This hook lets one command produce the PNG that a human then
    /// looks at: <c>-echo-shot file.png</c> grabs the screen, <c>-echo-scene Level01_Maze</c> skips the menu
    /// clicks nobody is making, and <c>-echo-ping</c> emits one echo so the frame being captured has something
    /// in it. Without the first argument it does nothing at all, so it is inert in normal play.
    /// </summary>
    public static class DevScreenCapture
    {
        class Runner : MonoBehaviour
        {
            public string path;
            public string scene;
            public float delaySeconds = 2f;
            public bool ping;
            public bool startRun;
            public bool complete;
            public float completeLeadSeconds = 0.4f;
            public float pingLeadSeconds = 0.45f;
            public bool quitAfter;

            System.Collections.IEnumerator Run()
            {
                // A screenshot of the real flow needs the real entry point: GameFlow.StartRun loads the
                // chapter and subscribes to its session, which is what makes the victory panel appear. A
                // scene loaded directly is a room with no run around it and would hide that wiring.
                if (startRun && GameFlow.Active != null)
                {
                    GameFlow.Active.StartRun();
                }
                else if (!string.IsNullOrEmpty(scene) &&
                         SceneManager.GetActiveScene().name != scene)
                {
                    SceneManager.LoadScene(scene, LoadSceneMode.Single);
                }

                // Realtime waits throughout: the victory panel pauses the game with Time.timeScale = 0,
                // and a scaled coroutine would sit there forever before taking the picture.
                yield return new WaitForSecondsRealtime(delaySeconds);

                if (complete)
                {
                    LevelSession session = FindAnyObjectByType<LevelSession>();
                    if (session != null)
                    {
                        session.Complete();
                    }

                    // The panel is activated by the flow's RunCompleted event after the session pauses.
                    yield return new WaitForSecondsRealtime(completeLeadSeconds);
                }

                if (ping)
                {
                    EchoDirector director = FindFirstObjectByType<EchoDirector>();
                    if (director != null)
                    {
                        director.ForcePing(director.ManualEchoLoudness);
                    }
                }

                yield return new WaitForSecondsRealtime(ping ? pingLeadSeconds : 0f);
                ScreenCapture.CaptureScreenshot(path);

                // The write is asynchronous and lands after the frame; quitting on the next line loses it.
                yield return new WaitForSecondsRealtime(1f);
                if (quitAfter)
                {
                    Application.Quit();
                }
            }

            void Start() => StartCoroutine(Run());
        }

        static string Value(string flag)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == flag)
                {
                    return args[i + 1];
                }
            }

            return null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            string path = Value("-echo-shot");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            // ScreenCapture resolves a relative path against Application.dataPath (…_Data/), not the
            // directory the player was launched from. The terminal recipe in Docs/STATUS.md means cwd.
            string full = Path.GetFullPath(path);
            string folder = Path.GetDirectoryName(full);
            if (!string.IsNullOrEmpty(folder))
            {
                Directory.CreateDirectory(folder);
            }

            var go = new GameObject("Dev Screen Capture");
            Object.DontDestroyOnLoad(go);
            var runner = go.AddComponent<Runner>();
            runner.path = full;
            runner.scene = Value("-echo-scene");
            string delay = Value("-echo-shot-at");
            if (delay != null)
            {
                runner.delaySeconds = float.Parse(delay, CultureInfo.InvariantCulture);
            }

            string[] args = System.Environment.GetCommandLineArgs();
            runner.ping = System.Array.IndexOf(args, "-echo-ping") >= 0;
            runner.quitAfter = System.Array.IndexOf(args, "-echo-quit") >= 0;
            runner.complete = System.Array.IndexOf(args, "-echo-complete") >= 0;
            runner.startRun = System.Array.IndexOf(args, "-echo-start-run") >= 0 ||
                              (runner.complete && string.IsNullOrEmpty(runner.scene));
        }
    }
}
