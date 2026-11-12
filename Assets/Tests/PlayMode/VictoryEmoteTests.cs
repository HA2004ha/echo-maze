using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using EchoMaze.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace EchoMaze.Tests.PlayMode
{
    /// <summary>
    /// The victory emote, on the clock it will actually run on.
    ///
    /// Everything here happens at <c>timeScale = 0</c> on purpose: the moment this animation plays is the
    /// moment the run has ended and <see cref="EchoMaze.Gameplay.LevelSession"/> has stopped the world. An
    /// emote that pumped <c>Time.deltaTime</c> would pass a test written at normal speed and be a still
    /// frame in the game, which is the exact failure this fixture exists to catch.
    /// </summary>
    public class VictoryEmoteTests
    {
        readonly List<GameObject> built = new List<GameObject>();
        float previousTimeScale;

        [SetUp]
        public void FreezeTheWorld()
        {
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }

        [TearDown]
        public void ThawAndCleanUp()
        {
            Time.timeScale = previousTimeScale;
            foreach (GameObject go in built)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }

            built.Clear();
        }

        /// <summary>
        /// A canvas-free stand-in for the generated panel: six Images under a root, wired the way
        /// <c>UiKit.EnsureVictoryEmote</c> wires them, because the fields are private and the builder sets
        /// them through the serialized object, which a PlayMode test cannot use.
        /// </summary>
        VictoryEmote BuildEmote(out Image[] layers)
        {
            // Inactive first: Awake runs on activation and clears every layer, so a rig that is wired
            // afterwards would be a rig whose emote never had anything to clear — the one state the
            // generated scene is never in.
            var root = new GameObject("rig emote", typeof(RectTransform));
            root.SetActive(false);
            built.Add(root);
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(860f, 860f);

            string[] names = { "Flash", "Swirls", "Confetti", "Title", "Pedestal", "Ghost" };
            layers = new Image[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                var go = new GameObject(names[i], typeof(RectTransform), typeof(Image));
                go.transform.SetParent(root.transform, false);
                go.GetComponent<RectTransform>().sizeDelta = new Vector2(860f, 860f);
                layers[i] = go.GetComponent<Image>();
                layers[i].color = Color.white;
            }

            var emote = root.AddComponent<VictoryEmote>();
            Wire(emote, "flash", layers[0]);
            Wire(emote, "swirls", layers[1]);
            Wire(emote, "confetti", layers[2]);
            Wire(emote, "title", layers[3]);
            Wire(emote, "pedestal", layers[4]);
            Wire(emote, "ghost", layers[5]);
            root.SetActive(true);
            return emote;
        }

        [UnityTest]
        public IEnumerator Play_RaisesEveryLayer_WhileTheWorldIsStopped()
        {
            VictoryEmote emote = BuildEmote(out Image[] layers);

            Assert.IsFalse(emote.IsPlaying, "a rebuilt panel must be born quiet, not mid-celebration");
            Assert.AreEqual(0f, layers[5].color.a, "the ghost is on screen before the run is won");

            emote.Play();
            Assert.IsTrue(emote.IsPlaying);

            // Unscaled frames, not scaled ones: the run is over and time is at zero.
            float deadline = Time.realtimeSinceStartup + VictoryEmote.EntranceSeconds + 1.5f;
            while (emote.Elapsed < VictoryEmote.EntranceSeconds + 0.15f && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.GreaterOrEqual(emote.Elapsed, VictoryEmote.EntranceSeconds,
                                  "the emote's clock did not advance while timeScale was zero");

            // The pop is the one layer that is meant to be gone: it is the flash of the arrival, not part
            // of the picture. Everything else has to be standing.
            Assert.AreEqual(0f, layers[0].color.a, "the opening pop is still burning after the entrance");
            for (int i = 1; i < layers.Length; i++)
            {
                Assert.Greater(layers[i].color.a, 0.9f, layers[i].name + " is still transparent after the entrance");
            }
        }

        [UnityTest]
        public IEnumerator TheEntrance_IsStaged_NotAllAtOnce()
        {
            VictoryEmote emote = BuildEmote(out Image[] layers);
            emote.Play();
            yield return null;

            // One frame in, the pop has started but the ghost has not been dropped yet: an entrance where
            // every layer arrives on frame one is not an entrance.
            Assert.Greater(layers[0].color.a, 0f, "the opening pop never fired");
            Assert.AreEqual(0f, layers[5].color.a, 0f, "the ghost arrived before the pedestal it lands on");
            Assert.IsTrue(emote.Elapsed > 0f, "the clock is not running on unscaled time");
        }

        [UnityTest]
        public IEnumerator Stop_PutsEveryLayerBackToNothing()
        {
            VictoryEmote emote = BuildEmote(out Image[] layers);
            emote.Play();

            float deadline = Time.realtimeSinceStartup + VictoryEmote.EntranceSeconds + 1.5f;
            while (emote.Elapsed < VictoryEmote.EntranceSeconds + 0.1f && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            emote.Stop();

            Assert.IsFalse(emote.IsPlaying);
            foreach (Image layer in layers)
            {
                Assert.AreEqual(0f, layer.color.a, layer.name + " was left visible by Stop");
                Assert.AreEqual(Vector3.zero, layer.rectTransform.localPosition, layer.name + " was left off its rest");
                Assert.AreEqual(Vector3.one, layer.rectTransform.localScale, layer.name + " was left scaled");
            }
        }

        [Test]
        public void TheSummaryPanel_StartsTheEmoteWhenItShowsTheRun()
        {
            GameObject canvas = new GameObject("rig victory canvas");
            built.Add(canvas);

            VictoryEmote emote = BuildEmote(out _);
            var panel = canvas.AddComponent<VictoryPanel>();
            Wire(panel, "emote", emote);

            MethodInfo show = typeof(VictoryPanel).GetMethod("Show", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(show, "update this fixture if VictoryPanel's show path is renamed");
            show.Invoke(panel, null);

            Assert.IsTrue(emote.IsPlaying, "the run ended and the emote did not start: the panel is wired to nothing");
        }

        static void Wire(Component target, string field, object value)
        {
            FieldInfo info = target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(info, target.GetType().Name + " has no serialized field named " + field);
            info.SetValue(target, value);
        }
    }
}
