using System.Collections;
using System.Collections.Generic;
using EchoMaze.Audio;
using EchoMaze.Core;
using EchoMaze.Data;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace EchoMaze.Tests.PlayMode
{
    /// <summary>
    /// Week 1's defect 6 in test form: one shouted word came back as four overlapping wave fields,
    /// because the director re-armed on a level threshold every tick. These run the real director for
    /// real frames on a scripted level, which is also the interface week 4's bot will drive.
    /// </summary>
    public class VocalEventPingTests
    {
        const float Loud = 0.5f;

        /// <summary>Above the gate and its threshold, well below a shout: the level a careful whisper is at.</summary>
        const float Whisper = 0.04f;

        EchoDirector director;
        EchoTuningProfile profile;
        readonly List<PingRecord> emitted = new List<PingRecord>();

        [SetUp]
        public void SetUp()
        {
            profile = ScriptableObject.CreateInstance<EchoTuningProfile>();
            // Isolate the gate from the cost; the cooldown has its own test below.
            profile.cooldownBase = 0f;
            profile.cooldownPerLoudness = 0f;

            var body = new GameObject("test director");
            director = body.AddComponent<EchoDirector>();
            director.UseProfile(profile);
            director.PingEmitted += emitted.Add;
        }

        [TearDown]
        public void TearDown()
        {
            director.PingEmitted -= emitted.Add;
            Object.Destroy(director.gameObject);
            Object.Destroy(profile);
            emitted.Clear();
        }

        /// <summary>
        /// A level that stays up for half a second is one thing said, not several. This is the regression:
        /// the old gate pinged on every interval while the level sat above the threshold.
        /// </summary>
        [UnityTest]
        public IEnumerator ContinuousLoudness_MakesExactlyOnePing()
        {
            director.UseSource(new ScriptedEchoSource(
                Script(new[] { Loud, 0f }, new[] { 30, 20 }), profile));

            for (int frame = 0; frame < 46; frame++)
            {
                yield return null;
            }

            Assert.AreEqual(1, emitted.Count,
                "one continuous vocal event made " + emitted.Count + " pings: the wave field is unreadable");
        }

        [UnityTest]
        public IEnumerator TwoSeparateEvents_MakeTwoPings()
        {
            director.UseSource(new ScriptedEchoSource(
                Script(new[] { Loud, 0f, Loud, 0f }, new[] { 3, 6, 3, 6 }), profile));

            for (int frame = 0; frame < 20; frame++)
            {
                yield return null;
            }

            Assert.AreEqual(2, emitted.Count, "a pause long enough to be a new word must start a new wave");
        }

        [UnityTest]
        public IEnumerator AWhisperAndAShout_PingWithDifferentReach()
        {
            director.UseSource(new ScriptedEchoSource(
                Script(new[] { Whisper, 0f, Loud, 0f }, new[] { 3, 6, 3, 6 }), profile));

            for (int frame = 0; frame < 20; frame++)
            {
                yield return null;
            }

            Assert.AreEqual(2, emitted.Count, "expected one whisper and one shout");
            Assert.Greater(emitted[1].radius, emitted[0].radius,
                "a shout has to reach further than a whisper, or loudness is decoration");
            Assert.Greater(emitted[1].rayCount, emitted[0].rayCount,
                "and a shout has to resolve finer than a whisper");
        }

        /// <summary>
        /// The cost side of the bargain: while the recovery from a ping is being paid, a new vocal event
        /// buys nothing. That cost is what makes being heard a decision rather than a reflex.
        /// </summary>
        [UnityTest]
        public IEnumerator ASecondEventInsideTheCooldown_MakesNoPing()
        {
            profile.cooldownBase = 1f;
            director.UseSource(new ScriptedEchoSource(
                Script(new[] { Loud, 0f, Loud, 0f }, new[] { 3, 6, 3, 6 }), profile));

            for (int frame = 0; frame < 20; frame++)
            {
                yield return null;
            }

            Assert.AreEqual(1, emitted.Count,
                "the recovery time was not paid, so the player can spam echoes for free");
            Assert.Greater(director.CooldownRemaining, 0f);
        }

        /// <summary>One Poll per entry, so a script is a timeline measured in frames.</summary>
        static float[] Script(float[] levels, int[] frames)
        {
            var values = new List<float>();
            for (int block = 0; block < levels.Length; block++)
            {
                for (int i = 0; i < frames[block]; i++)
                {
                    values.Add(levels[block]);
                }
            }

            return values.ToArray();
        }
    }
}
