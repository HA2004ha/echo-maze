using EchoMaze.Core;
using EchoMaze.Data;
using EchoMaze.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace EchoMaze.Tests
{
    /// <summary>
    /// The rules week 2's gameplay stands on, all of them pure: how far a noise carries compared with how
    /// far its wave reaches, how the Space charge maps to loudness, and the two invariants the tuning asset
    /// refuses to let a designer break.
    /// </summary>
    public class MovementAndHearingTests
    {
        EchoTuningProfile profile;

        [SetUp]
        public void SetUp()
        {
            profile = ScriptableObject.CreateInstance<EchoTuningProfile>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(profile);
        }

        [Test]
        public void HearingRadius_IsRhoTimesTheVisibleReach()
        {
            profile.maxRadius = 6.5f;
            profile.rho = 0.8f;
            Assert.AreEqual(5.2f, ArcMath.HearingRadius(1f, profile), 1e-4f,
                "rho is defined as hearing over reach at the same loudness; if it is not exactly that, " +
                "the knob does not mean what the GDD says it means");
        }

        [Test]
        public void HearingRadius_GrowsWithLoudness()
        {
            Assert.Greater(ArcMath.HearingRadius(1f, profile), ArcMath.HearingRadius(0.3f, profile),
                "a shout has to be heard from further away than a whisper, or loudness costs nothing");
        }

        [Test]
        public void WhileRhoIsBelowOne_PingingAlwaysRevealsLessThanItShows()
        {
            profile.rho = 0.8f;
            for (int i = 0; i <= 10; i++)
            {
                float loudness = i / 10f;
                Assert.LessOrEqual(ArcMath.HearingRadius(loudness, profile),
                                   ArcMath.PingRadius(loudness, profile),
                                   "at loudness " + loudness + " the enemy would hear further than the wave " +
                                   "shows, which is the game theory inverted");
            }
        }

        [Test]
        public void ManualEcho_AQuickTapIsQuieterThanAFullCharge()
        {
            profile.manualEchoLoudness = 0.65f;
            profile.manualChargeSeconds = 1.2f;
            Assert.AreEqual(0.65f, ArcMath.ManualLoudness(0f, profile), 1e-4f);
            Assert.AreEqual(1f, ArcMath.ManualLoudness(1.2f, profile), 1e-4f,
                "holding the key for the whole charge window has to reach a shout, or the charge is decoration");
            Assert.AreEqual(1f, ArcMath.ManualLoudness(9f, profile), 1e-4f,
                "holding past the window must not make the ping louder than a shout");
        }

        [Test]
        public void ManualEcho_ChargeIsMonotonicInHoldTime()
        {
            float previous = -1f;
            for (int i = 0; i <= 20; i++)
            {
                float loudness = ArcMath.ManualLoudness(i * 0.1f, profile);
                Assert.GreaterOrEqual(loudness, previous, "a longer hold returned a quieter echo at " + i * 0.1f + "s");
                previous = loudness;
            }
        }

        [Test]
        public void OnValidate_KeepsTheWaveFasterThanThePlayerCanRun()
        {
            // TUNING.md rule 1. A player faster than 1.6x the wave can outrun its own returning arc, and
            // the feeling that the wave comes back dies without anyone understanding why.
            profile.walkSpeed = 9f;
            Validate(profile);

            Assert.GreaterOrEqual(profile.waveSpeed, 1.6f * profile.walkSpeed - 1e-4f,
                                  "waveSpeed " + profile.waveSpeed + " against walkSpeed " + profile.walkSpeed);
        }

        [Test]
        public void OnValidate_KeepsTheSilentStepSlowerThanWalking()
        {
            profile.walkSpeed = 2.5f;
            profile.silentSpeed = 8f;
            Validate(profile);

            Assert.LessOrEqual(profile.silentSpeed, profile.walkSpeed,
                "a step that is quieter and faster than walking removes the trade-off entirely");
        }

        [Test]
        public void Defaults_KeepTheCrawlerOutrunnable()
        {
            // The chase is the Crawler's fastest gait, so it is the one that has to stay under the player.
            Assert.Less(profile.walkSpeed * profile.crawlerChaseSpeedMultiplier, profile.walkSpeed,
                "the Crawler is defined as something you can outrun; the level escape rules in the GDD " +
                "cannot be met by an enemy that is faster than the player");
        }

        [Test]
        public void OnValidate_KeepsTheChaseUnderWalkingAndThePatrolUnderTheChase()
        {
            profile.crawlerChaseSpeedMultiplier = 1.5f;
            profile.crawlerPatrolSpeedMultiplier = 2f;
            Validate(profile);

            Assert.Less(profile.walkSpeed * profile.crawlerChaseSpeedMultiplier, profile.walkSpeed,
                "a hunting Crawler that matches the player's walk can never be escaped");
            Assert.LessOrEqual(profile.crawlerPatrolSpeedMultiplier, profile.crawlerChaseSpeedMultiplier,
                "a Crawler that slows down when it hears you tells the player the opposite of the truth");
        }

        [Test]
        public void StepInterval_IsThePatrolBeatOnPatrolAndShorterOnAHunt()
        {
            float patrol = profile.crawlerPatrolSpeedMultiplier;
            float chase = profile.crawlerChaseSpeedMultiplier;
            float onPatrol = Crawler.StepInterval(profile.crawlerStepInterval, patrol, patrol);
            float onHunt = Crawler.StepInterval(profile.crawlerStepInterval, patrol, chase);

            Assert.AreEqual(1.5f, onPatrol, 1e-5f, "the product owner's patrol beat is one step every 1.5 s");
            Assert.AreEqual(0.75f, onHunt, 1e-5f, "same stride at twice the speed is half the interval");
            Assert.Less(onHunt, onPatrol, "a hunting Crawler must be heard stepping faster");
        }

        /// <summary>
        /// CreateInstance does not run OnValidate, and these tests are about what OnValidate refuses to
        /// leave in a shipped asset, so it has to be called by hand.
        /// </summary>
        static void Validate(EchoTuningProfile target)
        {
            typeof(EchoTuningProfile)
                .GetMethod("OnValidate", System.Reflection.BindingFlags.Instance |
                                         System.Reflection.BindingFlags.NonPublic)
                .Invoke(target, null);
        }
    }
}
