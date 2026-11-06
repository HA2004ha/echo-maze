using EchoMaze.Core;
using EchoMaze.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace EchoMaze.Tests.PlayMode
{
    /// <summary>
    /// The first real safe spot, against the real Crawler stub. The footstep is placed at half its own
    /// hearing radius: outside a hollow that is heard, inside one it is not (0.15 of the radius), and the
    /// distance comes from the profile so a retune moves the test with it. (A footstep carries only about
    /// 2.3 m with the shipped numbers, so 3 m would be silent outside as well and prove nothing.)
    /// </summary>
    public class HollowTests : BlockoutRig
    {
        static readonly Vector2 Cover = new Vector2(10f, 0f);

        Crawler crawler;
        float footstepAt;

        [SetUp]
        public void BuildHollowAndCrawler()
        {
            // The hollow is built far from the player and the Crawler, which each test moves next to it.
            crawler = NewCrawler(Vector2.zero);
            footstepAt = ArcMath.HearingRadius(profile.footstepLoudness, profile) * 0.5f;

            GameObject go = New("rig hollow", false);
            var hollow = go.AddComponent<Hollow>();
            Wire(hollow, "centre", Cover);
            Wire(hollow, "size", new Vector2(1.2f, 1.2f));
            go.SetActive(true);
        }

        // Hearing only looks at the distance, so the Crawler is placed instead of walked.
        void CrawlerAt(Vector2 position)
        {
            crawler.GetComponent<Rigidbody2D>().position = position;
            crawler.transform.position = position;
            Physics2D.SyncTransforms();
        }

        [Test]
        public void AFootstepOutsideAHollow_IsHeard()
        {
            CrawlerAt(Cover + Vector2.up * 5f);
            director.ReportNoise(Cover + Vector2.up * (5f - footstepAt), profile.footstepLoudness);

            Assert.IsTrue(crawler.IsHunting, "the control: the same footstep in the open must be heard");
        }

        [Test]
        public void AFootstepInsideAHollow_IsNotHeard()
        {
            CrawlerAt(Cover + Vector2.up * footstepAt);
            director.ReportNoise(Cover, profile.footstepLoudness);

            Assert.IsFalse(crawler.IsHunting, "a footstep made in cover turned the Crawler");
        }

        [Test]
        public void APingInsideAHollow_BreaksCover()
        {
            CrawlerAt(Cover + Vector2.up * footstepAt);
            director.ReportNoise(Cover, profile.manualEchoLoudness);

            Assert.IsTrue(crawler.IsHunting, "pinging from the hollow did not break cover");
        }

        [Test]
        public void ACrawlerAlreadyHunting_StillHearsAFootstepInsideAHollow()
        {
            CrawlerAt(Cover + Vector2.up * footstepAt);
            Vector2 elsewhere = Cover + Vector2.up * (footstepAt + 1f);
            director.ReportNoise(elsewhere, profile.manualEchoLoudness);
            Assert.IsTrue(crawler.IsHunting);

            director.ReportNoise(Cover, profile.footstepLoudness);

            Assert.AreEqual(Cover, (Vector2)Value(crawler, "huntTarget"),
                            "a chasing Crawler ignored the footstep because it was made in cover");
        }
    }
}
