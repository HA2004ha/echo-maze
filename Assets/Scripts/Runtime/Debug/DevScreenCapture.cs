using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using EchoMaze.Core;
using EchoMaze.Gameplay;
using EchoMaze.UI;

namespace EchoMaze.DebugTools
{
    /// <summary>
    /// A screenshot button for a game that is verified from a terminal.
    ///
    /// Every visual claim in this project — the glow, the arc density, the size of the reticle — has to be
    /// checked in the <b>player build</b>, because URP strips and screen-space decisions only exist there, and
    /// a headless editor run renders nothing. This hook lets one command produce the PNG that a human then
    /// looks at: <c>-echo-shot file.png</c> grabs the screen, <c>-echo-scene Level01_Maze</c> skips the menu
    /// clicks nobody is making, <c>-echo-ping</c> emits one echo so the frame being captured has something
    /// in it, and <c>-echo-pause</c>, <c>-echo-calibrate</c> and <c>-echo-guide-page 4</c> open the panels
    /// that only a click would otherwise ever show. <c>-echo-complete</c> finishes the current room and stops
    /// at its atlas; <c>-echo-victory</c> walks every remaining room so the capture lands on the victory
    /// panel at the end of the run. Without the first argument it does nothing at all, so it is inert in
    /// normal play.
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
            public bool victory;
            public bool pause;
            public bool calibrate;
            public bool chapters;
            public bool toLantern;
            public int guidePage = -1;
            public float completeLeadSeconds = 0.4f;
            public float pingLeadSeconds = 0.45f;
            public float panelLeadSeconds = 0.25f;
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

                    // A single load is applied at the end of the frame, so the room's player and its lamp kit
                    // do not exist yet on this line: everything below that reaches for them waits one frame.
                    yield return null;
                }

                // The lamps are the player's to place, so a capture of one takes the same two calls a Q
                // press makes: spend one from the pack beside the body, then step back into the light.
                if (toLantern)
                {
                    Player body = FindFirstObjectByType<Player>();
                    LanternKit kit = FindFirstObjectByType<LanternKit>();
                    if (body != null && kit != null)
                    {
                        Vector2 at = body.Position;
                        kit.PlaceAt(at + Vector2.right * 1.2f);
                        body.PlaceAt(at + Vector2.left * 1.4f);
                    }
                }

                // Realtime waits throughout: the victory panel pauses the game with Time.timeScale = 0,
                // and a scaled coroutine would sit there forever before taking the picture.
                yield return new WaitForSecondsRealtime(delaySeconds);

                // The panels a mouse user opens with clicks the terminal cannot make: Esc and the
                // calibrate button. Both go through the public surface, not a private field, so the
                // picture shows the same state a click would.
                if (pause)
                {
                    LevelSession paused = FindAnyObjectByType<LevelSession>();
                    if (paused != null)
                    {
                        paused.SetPaused(true);
                    }

                    yield return new WaitForSecondsRealtime(panelLeadSeconds);
                }

                if (calibrate)
                {
                    MainMenu menu = FindAnyObjectByType<MainMenu>();
                    if (menu != null)
                    {
                        menu.OpenCalibration();
                    }

                    yield return new WaitForSecondsRealtime(panelLeadSeconds);
                }

                // The guide is six cards, and a single capture of the first one proves only that the page
                // opens. The page number lets one command photograph any card in the build.
                if (guidePage >= 0)
                {
                    MainMenu guideMenu = FindAnyObjectByType<MainMenu>();
                    if (guideMenu != null)
                    {
                        guideMenu.OpenGuide(guidePage);
                    }

                    yield return new WaitForSecondsRealtime(panelLeadSeconds);
                }

                // The story page, which is opened by a menu button a terminal cannot click. It is last of
                // the panels so a capture of it cannot be a capture of the guide on top of it.
                if (chapters)
                {
                    MainMenu chapterMenu = FindAnyObjectByType<MainMenu>();
                    if (chapterMenu != null)
                    {
                        chapterMenu.OpenChapters();
                    }

                    yield return new WaitForSecondsRealtime(panelLeadSeconds);
                }

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

                // The victory panel is the end of the *run*, not of a room: it is raised when the last
                // chapter's atlas is dismissed, so one completion photograph would always be a photograph
                // of the atlas. This walks the whole list instead — complete, dismiss, repeat — and then
                // lets the capture fall on the panel itself, completeLead after it goes up.
                if (victory)
                {
                    for (int guard = 0; guard < 32 && GameFlow.Active != null && GameFlow.Active.IsRunning;
                         guard++)
                    {
                        LevelSession session = FindAnyObjectByType<LevelSession>();
                        if (session == null)
                        {
                            break;
                        }

                        session.Complete();

                        // Through the atlas's own button: it hides the page and then moves the flow on, and
                        // the terminal has to do both halves or the panel opens underneath the atlas.
                        VictoryMapView atlas = FindAnyObjectByType<VictoryMapView>();
                        for (float waited = 0f; atlas != null && !atlas.IsReady && waited < 5f; waited += Time.unscaledDeltaTime)
                        {
                            yield return null;
                            atlas = FindAnyObjectByType<VictoryMapView>();
                        }

                        if (atlas == null)
                        {
                            break;
                        }

                        atlas.Continue();

                        // A single load is applied at the end of the frame, so the session the next turn
                        // completes is the room that was loaded here, not the one being left.
                        yield return null;
                        yield return new WaitForSecondsRealtime(0.35f);
                    }

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

            // The victory summary now types itself out, and the picture is taken completeLead after the
            // run ends: 0.4 s catches eighteen characters, and a settled page needs the writer's own
            // runtime as the lead. Worth naming from the terminal rather than fixing in code.
            string completeLead = Value("-echo-complete-lead");
            if (completeLead != null)
            {
                runner.completeLeadSeconds = float.Parse(completeLead, CultureInfo.InvariantCulture);
            }

            string panelLead = Value("-echo-panel-lead");
            if (float.TryParse(panelLead, NumberStyles.Float, CultureInfo.InvariantCulture, out float seconds)
                && !float.IsNaN(seconds) && !float.IsInfinity(seconds) && seconds >= 0f)
            {
                runner.panelLeadSeconds = seconds;
            }

            string[] args = System.Environment.GetCommandLineArgs();
            runner.ping = System.Array.IndexOf(args, "-echo-ping") >= 0;
            runner.quitAfter = System.Array.IndexOf(args, "-echo-quit") >= 0;
            runner.complete = System.Array.IndexOf(args, "-echo-complete") >= 0;
            runner.victory = System.Array.IndexOf(args, "-echo-victory") >= 0;
            runner.pause = System.Array.IndexOf(args, "-echo-pause") >= 0;
            runner.calibrate = System.Array.IndexOf(args, "-echo-calibrate") >= 0;
            runner.chapters = System.Array.IndexOf(args, "-echo-chapters") >= 0;
            runner.toLantern = System.Array.IndexOf(args, "-echo-to-lantern") >= 0;
            string guide = Value("-echo-guide-page");
            if (guide != null)
            {
                runner.guidePage = int.TryParse(guide, NumberStyles.Integer, CultureInfo.InvariantCulture,
                                                 out int page) ? page : 0;
            }
            runner.startRun = System.Array.IndexOf(args, "-echo-start-run") >= 0 ||
                              ((runner.complete || runner.victory) && string.IsNullOrEmpty(runner.scene));
        }
    }
}
