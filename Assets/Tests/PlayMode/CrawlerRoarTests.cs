using System.Collections;
using EchoMaze.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace EchoMaze.Tests.PlayMode
{
    /// <summary>
    /// The Crawler's roar: a rare, seeded call that is drawn as a warm ripple and is not a ping. The interval
    /// is shortened here only so the suite does not sit through 10-15 s of silence; the rule is the same.
    /// </summary>
    public class CrawlerRoarTests : BlockoutRig
    {
        const float Min = 0.3f;
        const float Max = 0.6f;

        Crawler crawler;
        float bornAt;

        [SetUp]
        public void BuildCrawler()
        {
            profile.roarIntervalMin = Min;
            profile.roarIntervalMax = Max;

            // A non-zero cost, so a roar that went through Emit would show up as a cooldown.
            profile.cooldownBase = 1f;

            // Away from the rig player at the origin, so contact cannot end the run mid-test.
            crawler = NewCrawler(new Vector2(0f, 4f));
            bornAt = Time.time;
        }

        IEnumerator WaitForRipple()
        {
            float deadline = Time.time + Max + 1f;
            while (director.LiveRippleCount == 0 && Time.time < deadline)
            {
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator TheFirstRoar_ComesInsideTheInterval()
        {
            float delay = crawler.NextRoarTime - bornAt;
            Assert.That(delay, Is.InRange(Min, Max), "the first roar was scheduled outside [min, max]");

            yield return WaitForRipple();

            float heardAt = Time.time - bornAt;
            Assert.AreEqual(1, director.LiveRippleCount, "no ripple by " + heardAt.ToString("F2") + " s");
            Assert.That(heardAt, Is.InRange(Min, Max + 0.1f), "the roar did not land on its schedule");
        }

        [UnityTest]
        public IEnumerator ARoar_IsOneRippleAndCostsThePlayerNothing()
        {
            Assert.AreEqual(0, director.LivePingCount);
            Assert.AreEqual(0f, director.CooldownRemaining);

            yield return WaitForRipple();

            Assert.AreEqual(1, director.LiveRippleCount, "a roar should draw exactly one ripple");
            Assert.AreEqual(0, director.LivePingCount, "a roar took a slot in the player's ping pool");
            Assert.AreEqual(0f, director.CooldownRemaining, "a roar charged the player a cooldown");
            Assert.IsTrue(director.CanPing);
        }

        [UnityTest]
        public IEnumerator ARetry_RoarsOnTheSameSchedule()
        {
            float firstDelay = crawler.NextRoarTime - bornAt;

            // Let the roar clock draw at least once more, so a reset that forgot to reseed would differ.
            yield return WaitForRipple();
            yield return null;

            crawler.ResetRun();
            float replayDelay = crawler.NextRoarTime - Time.time;

            Assert.AreEqual(firstDelay, replayDelay, 1e-4f, "the retry's first roar comes after a different wait");
        }
    }
}
