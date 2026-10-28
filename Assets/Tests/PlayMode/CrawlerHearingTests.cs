using System.Collections;
using EchoMaze.Core;
using EchoMaze.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace EchoMaze.Tests.PlayMode
{
    /// <summary>
    /// Week 2's hearing stub: a Crawler walks towards any noise whose hearing radius H = rho * R(L) reaches
    /// it, and towards nothing else. The rule is the one the GDD's table gives, and it is the same R(L) the
    /// wave is drawn with, so "louder reaches further" is one number for both seeing and being heard.
    /// </summary>
    public class CrawlerHearingTests : BlockoutRig
    {
        Crawler crawler;

        [SetUp]
        public void BuildCrawler()
        {
            crawler = NewCrawler(Vector2.zero);
        }

        [Test]
        public void ANoiseWithinItsHearingRadius_IsWalkedTowards()
        {
            director.ReportNoise(new Vector2(0f, 3f), 1f);

            Assert.IsTrue(crawler.IsHunting,
                          "a shout 3 m away is inside " + ArcMath.HearingRadius(1f, profile).ToString("F1") +
                          " m of hearing and the Crawler did not turn");
        }

        [Test]
        public void ANoiseBeyondItsHearingRadius_IsIgnored()
        {
            float reach = ArcMath.HearingRadius(0f, profile);
            Assert.Less(reach, 3f, "the test needs the quietest sound to fall short of the noise it places");

            director.ReportNoise(new Vector2(0f, 3f), 0f);

            Assert.IsFalse(crawler.IsHunting,
                           "a footstep heard at " + reach.ToString("F1") + " m woke a Crawler 3 m away: " +
                           "loudness has stopped meaning anything to the enemy");
        }

        /// <summary>
        /// The creature emits its own footsteps at its own position. Without this guard it hunts its own
        /// walking forever and never patrols, which is the difference between an enemy and a statue.
        /// </summary>
        [Test]
        public void ItsOwnFootsteps_DoNotStartleIt()
        {
            director.ReportNoise(crawler.Position, 1f);

            Assert.IsFalse(crawler.IsHunting, "the Crawler started hunting its own footsteps");
        }

        /// <summary>
        /// The stub has to be temporary in a way a reader can see: a hunt is abandoned rather than
        /// resolved, because with no pathfinding the creature is pressed against a wall by now.
        /// </summary>
        [UnityTest]
        public IEnumerator AHunt_IsGivenUpWhenNothingWasFound()
        {
            Wire(crawler, "huntSeconds", 0.3f);
            var from = crawler.Position;
            director.ReportNoise(new Vector2(0f, 3f), 1f);
            Assert.IsTrue(crawler.IsHunting);

            // Timed in seconds, not frames: the test runner's frames are far shorter than a game frame,
            // so a frame count here is a race with the physics step rather than a measure of anything.
            yield return new WaitForSeconds(0.6f);
            yield return null;

            Assert.AreNotEqual(from, crawler.Position,
                               "the physics loop never ran, so nothing here can be timed");
            Assert.IsFalse(crawler.IsHunting,
                           "the hunt never ends, so a Crawler that cannot reach the noise never goes back " +
                           "to its route. mode=" + Value(crawler, "mode") + " at " + crawler.Position);
        }
    }
}
