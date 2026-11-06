using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using EchoMaze.Gameplay;
using EchoMaze.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace EchoMaze.Tests.PlayMode
{
    public class VictoryMapRevealTests : BlockoutRig
    {
        LevelSession session;
        GameFlow flow;
        float previousTimeScale;
        bool previousCursorVisible;
        CursorLockMode previousCursorLock;
        Camera existingCamera;
        Vector3 previousCameraPosition;
        float previousCameraSize;
        readonly Dictionary<Behaviour, bool> previousEnabled = new Dictionary<Behaviour, bool>();

        [SetUp]
        public void BuildSession()
        {
            previousTimeScale = Time.timeScale;
            previousCursorVisible = Cursor.visible;
            previousCursorLock = Cursor.lockState;
            Time.timeScale = 1f;
            existingCamera = Camera.main;
            if (existingCamera != null)
            {
                previousCameraPosition = existingCamera.transform.position;
                previousCameraSize = existingCamera.orthographicSize;
            }
            RememberEnabled(Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None));
            RememberEnabled(Object.FindObjectsByType<PlayerCameraRig>(FindObjectsSortMode.None));
            RememberEnabled(Object.FindObjectsByType<AimCursor>(FindObjectsSortMode.None));

            Assert.IsNull(GameFlow.Active, "this fixture needs an isolated test scene, not a running game");
            Assert.IsNull(Object.FindAnyObjectByType<LevelSession>(), "an unrelated session would affect GameFlow lookup");
            GameObject host = New("rig victory session", false);
            session = host.AddComponent<LevelSession>();
            Wire(session, "player", player);
            Wire(session, "director", director);
            Wire(session, "chapterTitle", "Test chapter");
            host.SetActive(true);
        }

        [TearDown]
        public void RestorePresentationState()
        {
            // Clear the persistent singleton synchronously, before the next fixture can create a flow.
            if (flow != null)
            {
                built.Remove(flow.gameObject);
                Object.DestroyImmediate(flow.gameObject);
                flow = null;
            }
            // Include-inactive reveal checks must not see a previous test's deferred destruction.
            if (session != null)
            {
                built.Remove(session.gameObject);
                Object.DestroyImmediate(session.gameObject);
                session = null;
            }
            foreach (var entry in previousEnabled)
                if (entry.Key != null) entry.Key.enabled = entry.Value;
            previousEnabled.Clear();
            if (existingCamera != null)
            {
                existingCamera.transform.position = previousCameraPosition;
                existingCamera.orthographicSize = previousCameraSize;
            }
            Time.timeScale = previousTimeScale;
            Cursor.lockState = previousCursorLock;
            Cursor.visible = previousCursorVisible;
        }

        void RememberEnabled<T>(T[] components) where T : Behaviour
        {
            foreach (T component in components) previousEnabled[component] = component.enabled;
        }

        [UnityTest]
        public IEnumerator NormalPlayPauseAndDeath_DoNotRevealTheMap()
        {
            yield return null;
            AssertNoReveal();

            session.SetPaused(true);
            yield return null;
            Assert.IsTrue(session.IsPaused);
            Assert.AreEqual(0f, Time.timeScale);
            AssertNoReveal();

            session.SetPaused(false);
            player.Die();
            yield return null;
            Assert.IsFalse(player.IsAlive);
            AssertNoReveal();
        }

        [Test]
        public void Complete_CreatesExactlyOneViewAndFreezesThePlayer()
        {
            int completions = 0;
            session.Completed += () => completions++;

            session.Complete();
            VictoryMapView view = session.GetComponent<VictoryMapView>();
            Assert.IsNotNull(view);
            Assert.IsNotNull(view.GetComponentInChildren<CanvasGroup>(), "completion must show, not only attach, the view");
            Assert.IsTrue(session.IsComplete);
            Assert.IsTrue(session.IsPaused);
            Assert.AreEqual(0f, Time.timeScale);
            Assert.IsFalse(player.enabled);
            Assert.IsFalse(director.EmitEnabled);
            Assert.IsFalse(view.IsReady, "the reveal must finish before continuation is offered");

            session.Complete();
            session.Complete();
            Assert.AreEqual(1, completions);
            Assert.AreEqual(1, Object.FindObjectsByType<VictoryMapView>(FindObjectsSortMode.None).Length);
            Assert.AreSame(view, session.GetComponent<VictoryMapView>());
            Assert.AreEqual(1, view.GetComponentsInChildren<Canvas>().Length);
        }

        [Test]
        public void CompletedSession_CannotBeUnpaused()
        {
            session.Complete();
            int pauseChanges = 0;
            session.PauseToggled += _ => pauseChanges++;

            session.SetPaused(false);
            session.SetPaused(true);
            session.SetPaused(false);

            Assert.IsTrue(session.IsComplete);
            Assert.IsTrue(session.IsPaused);
            Assert.AreEqual(0f, Time.timeScale);
            Assert.IsFalse(player.enabled);
            Assert.IsFalse(director.EmitEnabled);
            Assert.AreEqual(0, pauseChanges, "completed sessions must not reopen the gameplay/pause UI");
        }

        [Test]
        public void DeadPlayer_CannotCompleteOrRevealTheMap()
        {
            int completions = 0;
            session.Completed += () => completions++;
            player.Die();

            session.Complete();
            session.Complete();

            Assert.IsFalse(player.IsAlive);
            AssertNoReveal();
            Assert.IsFalse(session.IsPaused);
            Assert.AreEqual(1f, Time.timeScale);
            Assert.AreEqual(0, completions);
        }

        [UnityTest]
        public IEnumerator Reveal_BecomesReadyWhileTimeScaleIsZero()
        {
            session.Complete();
            var view = session.GetComponent<VictoryMapView>();
            var group = view.GetComponentInChildren<CanvasGroup>();
            float elapsedAtCompletion = session.ElapsedSeconds;
            Assert.IsFalse(view.IsReady);
            Assert.IsFalse(group.interactable);

            yield return WaitForReveal(view);

            Assert.IsTrue(group.interactable);
            Assert.AreEqual(1f, group.alpha, 0.001f);
            Assert.AreEqual(0f, Time.timeScale);
            Assert.AreEqual(elapsedAtCompletion, session.ElapsedSeconds,
                "watching the victory map must not add to chapter play time");
            Assert.IsFalse(player.enabled);
        }

        [UnityTest]
        public IEnumerator GameFlow_CountsOnceAndWaitsForExplicitContinue()
        {
            BuildFlowWithoutLoadingScene();
            int runCompletions = 0;
            flow.RunCompleted += () => runCompletions++;
            Scene originalScene = SceneManager.GetActiveScene();

            director.ForcePing(0.5f);
            director.ForcePing(0.5f);
            player.Die();
            session.RestartFromCheckpoint();
            yield return null;
            Assert.AreEqual(2, session.PingCount);
            Assert.AreEqual(1, session.DeathCount);
            Assert.Greater(session.ElapsedSeconds, 0f);
            float playedSeconds = session.ElapsedSeconds;

            session.Complete();
            session.Complete();
            var view = session.GetComponent<VictoryMapView>();
            view.Continue(); // Too early: neither summary nor scene navigation is allowed yet.
            Assert.AreEqual(0, flow.ChapterIndex);
            Assert.AreEqual(0, runCompletions);
            yield return WaitForReveal(view);
            yield return null; // Readiness itself must not advance the run.
            session.Complete();

            Assert.IsTrue(flow.IsRunning);
            Assert.AreEqual(0, flow.ChapterIndex);
            Assert.AreEqual(originalScene.handle, SceneManager.GetActiveScene().handle);
            Assert.AreEqual(0, runCompletions);
            Assert.AreEqual(2, flow.TotalPings);
            Assert.AreEqual(1, flow.TotalDeaths);
            Assert.AreEqual(playedSeconds, flow.TotalSeconds);
            Assert.AreEqual(0f, Time.timeScale);

            view.Continue();
            view.Continue();
            Assert.IsFalse(flow.IsRunning);
            Assert.AreEqual(1, runCompletions, "only explicit continuation may finish the run, and only once");
            Assert.AreEqual(2, flow.TotalPings);
            Assert.AreEqual(1, flow.TotalDeaths);
            Assert.AreEqual(playedSeconds, flow.TotalSeconds);
        }

        void BuildFlowWithoutLoadingScene()
        {
            // Keep this fixture in its isolated scene; the scene-flow fixture covers actual loads.
            GameObject host = New("rig victory flow", false);
            flow = host.AddComponent<GameFlow>();
            Wire(flow, "chapterScenes", new[] { "Test chapter" });
            Wire(flow, "chapterIndex", 0);
            host.SetActive(true);
            Assert.AreSame(flow, GameFlow.Active);
            MethodInfo bind = typeof(GameFlow).GetMethod("OnSceneLoaded", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(bind, "update this fixture if GameFlow's scene-binding entry point changes");
            bind.Invoke(flow, new object[] { SceneManager.GetActiveScene(), LoadSceneMode.Single });
        }

        static IEnumerator WaitForReveal(VictoryMapView view)
        {
            // Real-time deadline: scaled waits never finish after completion freezes the world.
            float deadline = Time.realtimeSinceStartup + 5f;
            while (!view.IsReady && Time.realtimeSinceStartup < deadline)
            {
                Assert.AreEqual(0f, Time.timeScale);
                yield return null;
            }
            Assert.IsTrue(view.IsReady, "the transition must use unscaled time");
        }

        void AssertNoReveal()
        {
            Assert.IsFalse(session.IsComplete);
            Assert.AreEqual(0, Object.FindObjectsByType<VictoryMapView>(FindObjectsInactive.Include,
                FindObjectsSortMode.None).Length, "the full map must exist only after a win");
        }
    }

    public class VictoryMapSceneFlowTests
    {
        [UnityTest]
        public IEnumerator BothChapters_RevealBeforeNextSceneOrSummary()
        {
            Time.timeScale = 1f;
            yield return SceneManager.LoadSceneAsync("Boot");
            GameFlow flow = GameFlow.Active;
            Assert.IsNotNull(flow);
            int finished = 0;
            flow.RunCompleted += () => finished++;
            flow.StartRun();
            yield return null;

            for (int chapter = 0; chapter < flow.ChapterCount; chapter++)
            {
                Assert.AreEqual(chapter, flow.ChapterIndex);
                Assert.AreEqual(flow.ChapterName(chapter), SceneManager.GetActiveScene().name);
                Assert.AreEqual(1f, Time.timeScale);
                Assert.IsNull(Object.FindAnyObjectByType<VictoryMapView>());
                var session = Object.FindAnyObjectByType<LevelSession>();
                var player = Object.FindAnyObjectByType<Player>();
                var exit = Object.FindAnyObjectByType<LevelExit>();
                Assert.IsTrue(player.enabled);
                player.PlaceAt(exit.transform.position);
                exit.Interact(player);
                var view = Object.FindAnyObjectByType<VictoryMapView>();
                Assert.IsNotNull(view);
                Assert.Greater(view.Map.ShapeCount, 20);
                float deadline = Time.realtimeSinceStartup + 5f;
                while (!view.IsReady && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.IsTrue(view.IsReady);
                Assert.AreEqual(chapter, flow.ChapterIndex);
                Assert.AreEqual(0, finished);
                Assert.IsTrue(session.IsComplete);
                Assert.AreEqual(0f, Time.timeScale);
                Camera camera = Camera.main;
                Rect viewport = VictoryMapView.MapViewport;
                Vector3 min = camera.WorldToViewportPoint(view.Map.MapBounds.min);
                Vector3 max = camera.WorldToViewportPoint(view.Map.MapBounds.max);
                Assert.GreaterOrEqual(min.x, viewport.xMin);
                Assert.GreaterOrEqual(min.y, viewport.yMin);
                Assert.LessOrEqual(max.x, viewport.xMax);
                Assert.LessOrEqual(max.y, viewport.yMax);
                var button = view.GetComponentInChildren<UnityEngine.UI.Button>();
                Assert.IsTrue(button.interactable);
                button.onClick.Invoke();
                yield return null;
            }

            Assert.AreEqual(1, finished);
            Assert.IsFalse(flow.IsRunning);
            yield return new WaitForSecondsRealtime(0.5f);
            var summary = Object.FindAnyObjectByType<VictoryPanel>();
            Assert.IsNotNull(summary);
            Assert.IsTrue(summary.GetComponent<Canvas>().enabled);
            Assert.Greater(summary.GetComponentInChildren<CanvasGroup>().alpha, 0f);
            flow.ReturnToMenu();
            yield return null;
            Assert.AreEqual("Boot", SceneManager.GetActiveScene().name);
            Assert.AreEqual(1f, Time.timeScale);
            Assert.IsNull(Object.FindAnyObjectByType<VictoryMapView>());
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.timeScale = 1f;
            Cursor.visible = true;
            if (GameFlow.Active != null) Object.DestroyImmediate(GameFlow.Active.gameObject);
            Scene previous = SceneManager.GetActiveScene();
            Scene clean = SceneManager.CreateScene("Victory map test cleanup");
            SceneManager.SetActiveScene(clean);
            yield return SceneManager.UnloadSceneAsync(previous);
        }
    }
}
