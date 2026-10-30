using System.Diagnostics;
using EchoMaze.Core;
using NUnit.Framework;
using UnityEngine;

namespace EchoMaze.Tests
{
    /// <summary>The footprint ring: expiry, capacity, and the number behind "is erasing the trail heavy?".</summary>
    public class TrailBufferTests
    {
        const float Seconds = 4f;
        const float Walk = 2.5f;
        const float Step = 0.35f;

        [Test]
        public void Capacity_IsTheWalkingMaximumPlusSlack()
        {
            Assert.AreEqual(Mathf.CeilToInt(Seconds * Walk / Step) + TrailBuffer.Slack,
                            new TrailBuffer(Seconds, Walk, Step).Capacity);
        }

        [Test]
        public void PointsExpire_AfterTheirSeconds_EvenWhenStandingStill()
        {
            var trail = new TrailBuffer(Seconds, Walk, Step);
            for (int i = 0; i < 5; i++)
            {
                trail.Tick(new Vector2(i * 0.4f, 0), i * 0.1f);
            }

            Assert.AreEqual(5, trail.Count);

            trail.Tick(new Vector2(4 * 0.4f, 0), 3.9f);
            Assert.AreEqual(5, trail.Count, "nothing is four seconds old yet");

            trail.Tick(new Vector2(4 * 0.4f, 0), 4.05f);
            Assert.AreEqual(4, trail.Count, "the point born at 0 is gone");

            trail.Tick(new Vector2(4 * 0.4f, 0), 4.45f);
            Assert.AreEqual(0, trail.Count, "all gone with the player not having moved");
        }

        [Test]
        public void OnePoint_PerStepWalked_NotPerCall()
        {
            var trail = new TrailBuffer(Seconds, Walk, Step);
            for (int i = 0; i < 100; i++)
            {
                trail.Tick(new Vector2(i * 0.01f, 0), i * 0.01f);
            }

            // 0.99 m walked: the first point plus one per 0.35 m.
            Assert.AreEqual(3, trail.Count);
        }

        [Test]
        public void WalkingAtFullSpeedForever_NeverExceedsCapacity()
        {
            var trail = new TrailBuffer(Seconds, Walk, Step);
            int max = 0;
            for (int frame = 0; frame < 60 * 30; frame++)
            {
                float t = frame / 60f;
                trail.Tick(new Vector2(t * Walk, 0), t);
                max = Mathf.Max(max, trail.Count);
            }

            Assert.LessOrEqual(max, trail.Capacity);
            Assert.AreEqual(Mathf.CeilToInt(Seconds * Walk / Step), max, 2, "steady state is seconds x speed / step (a point lands a frame past each 0.35 m, so a couple fewer)");
        }

        [Test]
        public void AFullBuffer_DropsTheOldest_AndKeepsOrder()
        {
            var trail = new TrailBuffer(100f, Walk, Step);
            int total = trail.Capacity + 5;
            for (int i = 0; i < total; i++)
            {
                trail.Tick(new Vector2(i, 0), i * 0.01f);
            }

            Assert.AreEqual(trail.Capacity, trail.Count);
            trail.Get(0, out Vector2 oldest, out _);
            trail.Get(trail.Count - 1, out Vector2 newest, out _);
            Assert.AreEqual(5f, oldest.x);
            Assert.AreEqual(total - 1, newest.x);
        }

        [Test]
        public void Ticking_Allocates_Nothing()
        {
            var trail = new TrailBuffer(Seconds, Walk, Step);
            trail.Tick(Vector2.zero, 0f);
            long before = System.GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 2000; i++)
            {
                trail.Tick(new Vector2(i * 0.1f, 0), i * 0.02f);
            }

            Assert.AreEqual(0, System.GC.GetAllocatedBytesForCurrentThread() - before);
        }

        /// <summary>
        /// Not a pass/fail on speed so much as the way the number gets recorded: it logs the cost of a full
        /// rebuild (tick + write every dot into the mesh buffer + upload) so Docs/TUNING.md has a measured
        /// figure. The bound is two orders of magnitude over what it should cost, so only a real regression fails.
        /// </summary>
        [Test]
        public void RebuildingAFullTrail_CostMeasured()
        {
            var trail = new TrailBuffer(Seconds, Walk, Step);
            var quads = new QuadMeshBuffer(trail.Capacity, "TrailCost");
            Color color = new Color(0.95f, 0.88f, 0.72f, 0.35f);
            Vector2 half = new Vector2(0.045f, 0f);

            for (int i = 0; i < trail.Capacity; i++)
            {
                trail.Tick(new Vector2(i * Step, 0), i * 0.01f);
            }

            const int Rebuilds = 1000;
            var watch = Stopwatch.StartNew();
            for (int r = 0; r < Rebuilds; r++)
            {
                quads.Begin();
                for (int i = 0; i < trail.Count; i++)
                {
                    trail.Get(i, out Vector2 p, out _);
                    quads.AddSegment(p - half, p + half, 0.045f, color);
                }

                quads.End();
            }

            watch.Stop();
            double msPer = watch.Elapsed.TotalMilliseconds / Rebuilds;
            Object.DestroyImmediate(quads.Mesh);
            UnityEngine.Debug.Log("Footprint trail rebuild: " + msPer.ToString("F4") + " ms per frame for " +
                                  trail.Count + " dots (" + Rebuilds + " rebuilds)");
            Assert.Less(msPer, 1.0, "a full trail rebuild should be far under a millisecond");
        }
    }
}
