using System.Collections.Generic;
using EchoMaze.Core;
using EchoMaze.Data;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace EchoMaze.Tests.PlayMode
{
    /// <summary>
    /// The noise stream: every sound the game makes is reported with where and how loud it was, and
    /// nothing else about it. Week 2 only has the player's own sounds on this stream, but it is the seam
    /// week 3's NoiseBoard and the enemies hang off, so it is pinned here while it is still small.
    /// </summary>
    public class NoiseStreamTests
    {
        EchoDirector director;
        EchoTuningProfile profile;
        readonly List<Vector2> at = new List<Vector2>();
        readonly List<float> level = new List<float>();

        [SetUp]
        public void SetUp()
        {
            profile = ScriptableObject.CreateInstance<EchoTuningProfile>();
            var body = new GameObject("noise director");
            director = body.AddComponent<EchoDirector>();
            director.UseProfile(profile);
            director.NoiseEmitted += OnNoise;
        }

        [TearDown]
        public void TearDown()
        {
            director.NoiseEmitted -= OnNoise;
            Object.Destroy(director.gameObject);
            Object.Destroy(profile);
            at.Clear();
            level.Clear();
        }

        void OnNoise(Vector2 position, float loudness)
        {
            at.Add(position);
            level.Add(loudness);
        }

        /// <summary>
        /// A wave you can see is a wave every enemy can hear. The two are one event, not two systems
        /// that have to be kept in sync, which is why this is asserted instead of assumed.
        /// </summary>
        [Test]
        public void AVisiblePing_IsAlsoANoiseAtItsOwnOrigin()
        {
            var origin = new Vector2(1.5f, -2f);
            director.ForcePing(0.7f, origin, 0f);

            Assert.AreEqual(1, at.Count, "the ping made no noise: enemies will never hear a shout");
            Assert.AreEqual(origin, at[0]);
            Assert.AreEqual(0.7f, level[0], 0.001f, "the noise must carry the loudness the wave was drawn with");
        }

        /// <summary>
        /// Footsteps, wall bumps and later the decoy marble: they travel, they cost nothing, and they
        /// draw no wave. If a bare noise ever started charging the cooldown, the quietest game state
        /// would be the one that punishes you most.
        /// </summary>
        [Test]
        public void ABareNoise_IsHeardButDrawsNothingAndCostsNothing()
        {
            director.ReportNoise(new Vector2(-3f, 1f), 0.4f);

            Assert.AreEqual(1, at.Count);
            Assert.AreEqual(-3f, at[0].x, 0.001f);
            Assert.AreEqual(0.4f, level[0], 0.001f);
            Assert.AreEqual(0, director.LivePingCount, "a footstep must not draw a wave field");
            Assert.IsTrue(director.CanPing, "a footstep must not pay a ping's cooldown");
        }

        /// <summary>
        /// The 360 degree pulse is the get-out-of-the-room tool, and the GDD's price for it is that it is
        /// nearly silent. Loudness 0 is what makes it nearly silent: it is heard only inside rho *
        /// minRadius, so the pulse cannot be the thing that kills you.
        /// </summary>
        [Test]
        public void TheWeakPulse_IsTheQuietestEventOnTheStream()
        {
            Assert.IsTrue(director.TryWeakPulse(), "the first pulse should be free");
            Assert.IsFalse(director.TryWeakPulse(), "the pulse has its own cooldown and must respect it");
            Assert.Greater(director.WeakPulseRemaining, 0f);
            Assert.AreEqual(0f, director.CooldownRemaining,
                            "the pulse draws a wave but pays no shout's cost");

            director.ForcePing(1f, Vector2.zero, 0f);

            Assert.AreEqual(2, at.Count);
            Assert.AreEqual(0f, level[0], 0.001f, "the pulse must be the floor of the noise scale");
            Assert.Greater(level[1], level[0]);
        }

        /// <summary>
        /// The order the whole stealth loop rests on: walking is quieter than the tap that shows you the
        /// room, and the tap is quieter than the shout. Pinned on the tuning asset rather than on a play
        /// session, because a designer inverting these two numbers cannot be caught by any amount of
        /// looking at the wave.
        /// </summary>
        [Test]
        public void TheNoisesThePlayerMakes_AreOrderedFromQuietToLoud()
        {
            Assert.Less(profile.bumpLoudness, profile.footstepLoudness,
                        "a wall bump has to be the smaller of the two accidental sounds");
            Assert.Less(profile.footstepLoudness, profile.manualEchoLoudness,
                        "walking has to be quieter than a tap, or the silent step is pointless");
            Assert.Less(profile.manualEchoLoudness, 1f, "a tap must not already be a shout");
        }
    }
}
