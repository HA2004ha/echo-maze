using System.Collections;
using System.Collections.Generic;
using EchoMaze.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace EchoMaze.Tests.PlayMode
{
    /// <summary>
    /// The Crawler's two gaits as the product owner asked for them: a slow patrol with one stomp every
    /// 1.5 s, and a hunt that is faster but still slower than the player's walk and steps faster with it.
    /// The production numbers are used as they are; the rig's profile is already its own instance.
    /// </summary>
    public class CrawlerGaitTests : BlockoutRig
    {
        Crawler crawler;
        readonly List<float> steps = new List<float>();

        [SetUp]
        public void BuildCrawler()
        {
            steps.Clear();

            // Long legs, so the first leg (10 m) is still being walked when the test ends and no waypoint
            // pause cuts into the rhythm. Four metres from the rig player, so contact cannot end the run.
            Vector2 start = new Vector2(0f, 4f);
            crawler = NewCrawler(start, new[] { start + Vector2.right * 10f, start - Vector2.right * 10f });

            // Its own footsteps are the only noises reported at its own position.
            director.NoiseEmitted += (position, loudness) =>
            {
                if (Vector2.Distance(position, crawler.Position) < 0.01f)
                {
                    steps.Add(Time.time);
                }
            };
        }

        /// <summary>
        /// Starts the clock on the first step rather than on the test, so a long first frame in the runner
        /// cannot push the last expected step past the end of the wait.
        /// </summary>
        IEnumerator WaitFromFirstStep(float seconds)
        {
            float deadline = Time.time + 1f;
            while (steps.Count == 0 && Time.time < deadline)
            {
                yield return null;
            }

            Assert.AreEqual(1, steps.Count, "the Crawler never took its first step");
            yield return new WaitForSeconds(seconds);
        }

        void AssertSpacing(float expected)
        {
            for (int i = 1; i < steps.Count; i++)
            {
                float gap = steps[i] - steps[i - 1];
                // Two physics steps of slack: steps are taken in FixedUpdate, and Time.time + interval can land
                // a hair past a 0.02 s tick, so a 0.75 s beat is measured as 0.76 or 0.78 s.
                Assert.That(gap, Is.InRange(expected - 1e-3f, expected + 2f * Time.fixedDeltaTime + 1e-3f),
                            "step " + i + " came " + gap.ToString("F3") + " s after the previous one");
            }
        }

        [UnityTest]
        public IEnumerator APatrol_StompsOnceEveryPatrolInterval()
        {
            yield return WaitFromFirstStep(3.2f);

            Assert.IsFalse(crawler.IsHunting, "something woke the Crawler and the patrol was never measured");
            Assert.AreEqual(3, steps.Count, "3.2 s of patrol should be steps at 0, 1.5 and 3.0 s");
            AssertSpacing(profile.crawlerStepInterval);
        }

        [UnityTest]
        public IEnumerator AHunt_IsFasterThanThePatrolButSlowerThanThePlayer_AndStepsFaster()
        {
            // 5 m straight up: just inside a shout's 5.2 m hearing radius, and far enough that the 2 m/s walk
            // (about 2.4 s) is still going when the measurement ends. Reported before the first physics step,
            // so every step measured is a hunting one.
            director.ReportNoise(crawler.Position + Vector2.up * 5f, 1f);
            Assert.IsTrue(crawler.IsHunting);

            // Between the third step (1.5 s) and the fourth (2.25 s).
            yield return WaitFromFirstStep(1.6f);

            Assert.IsTrue(crawler.IsHunting, "the hunt ended before it could be measured");
            float speed = crawler.GetComponent<Rigidbody2D>().linearVelocity.magnitude;
            Assert.AreEqual(profile.walkSpeed * profile.crawlerChaseSpeedMultiplier, speed, 0.05f,
                            "the hunt is not walked at the chase speed");
            Assert.Less(speed, profile.walkSpeed, "a hunting Crawler the player cannot outwalk");

            float chaseInterval = Crawler.StepInterval(profile.crawlerStepInterval,
                                                       profile.crawlerPatrolSpeedMultiplier,
                                                       profile.crawlerChaseSpeedMultiplier);
            Assert.AreEqual(3, steps.Count, "1.6 s of hunting should be steps at 0, 0.75 and 1.5 s");
            AssertSpacing(chaseInterval);
        }
    }
}
