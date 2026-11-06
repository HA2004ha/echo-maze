using EchoMaze.Core;
using NUnit.Framework;
using UnityEngine;

namespace EchoMaze.Tests
{
    /// <summary>The pure half of the wall memory: the merge rule and the ring. No scene, no colliders.</summary>
    public class WallMemoryTests
    {
        const float Merge = 0.15f;

        [Test]
        public void OverlappingCollinearSegments_MergeIntoTheirSpan()
        {
            bool merged = WallMemory.TryMerge(new Vector2(0, 0), new Vector2(1, 0),
                                              new Vector2(0.5f, 0.05f), new Vector2(1.5f, 0.05f), Merge,
                                              out Vector2 a, out Vector2 b);
            Assert.IsTrue(merged);
            Assert.AreEqual(new Vector2(0, 0), a);
            Assert.AreEqual(new Vector2(1.5f, 0.05f), b);
        }

        [Test]
        public void AdjacentSegments_WithinTheGap_Merge()
        {
            Assert.IsTrue(WallMemory.TryMerge(new Vector2(0, 0), new Vector2(1, 0),
                                              new Vector2(1.1f, 0), new Vector2(2, 0), Merge, out _, out Vector2 b));
            Assert.AreEqual(new Vector2(2, 0), b);
        }

        [Test]
        public void ANewSegmentInsideTheOldOne_ChangesNothing()
        {
            Assert.IsTrue(WallMemory.TryMerge(new Vector2(0, 0), new Vector2(2, 0),
                                              new Vector2(0.5f, 0), new Vector2(1.5f, 0), Merge,
                                              out Vector2 a, out Vector2 b));
            Assert.AreEqual(new Vector2(0, 0), a);
            Assert.AreEqual(new Vector2(2, 0), b);
        }

        [Test]
        public void ReversedDirection_StillMerges()
        {
            Assert.IsTrue(WallMemory.TryMerge(new Vector2(0, 0), new Vector2(1, 0),
                                              new Vector2(-0.5f, 0), new Vector2(0.5f, 0), Merge,
                                              out Vector2 a, out Vector2 b));
            Assert.AreEqual(new Vector2(-0.5f, 0), a);
            Assert.AreEqual(new Vector2(1, 0), b);
        }

        [Test]
        public void FarParallel_Perpendicular_AndDistantAlongTheWall_AreRefused()
        {
            var a0 = new Vector2(0, 0);
            var b0 = new Vector2(1, 0);
            Assert.IsFalse(WallMemory.TryMerge(a0, b0, new Vector2(0, 0.5f), new Vector2(1, 0.5f), Merge, out _, out _),
                           "a parallel wall half a metre over");
            Assert.IsFalse(WallMemory.TryMerge(a0, b0, new Vector2(1, 0), new Vector2(1, 1), Merge, out _, out _),
                           "the perpendicular wall at a corner");
            Assert.IsFalse(WallMemory.TryMerge(a0, b0, new Vector2(2, 0), new Vector2(3, 0), Merge, out _, out _),
                           "the same wall, a metre further on");
        }

        [Test]
        public void BumpingTheSameSpotManyTimes_SpendsOneSlot()
        {
            var memory = new WallMemory(10, Merge);
            for (int i = 0; i < 100; i++)
            {
                memory.Add(new Vector2(0, -0.5f), new Vector2(0, 0.5f));
            }

            Assert.AreEqual(1, memory.Count);
        }

        [Test]
        public void SlidingAlongAWall_BuildsOneGrowingLine()
        {
            var memory = new WallMemory(10, Merge);
            for (int i = 0; i < 40; i++)
            {
                float y = i * 0.1f;
                memory.Add(new Vector2(0, y - 0.5f), new Vector2(0, y + 0.5f));
            }

            Assert.AreEqual(1, memory.Count);
            memory.Get(0, out Vector2 a, out Vector2 b, out _);
            Assert.AreEqual(-0.5f, Mathf.Min(a.y, b.y), 1e-4f);
            Assert.AreEqual(4.4f, Mathf.Max(a.y, b.y), 1e-4f);
        }

        [Test]
        public void AFullStore_OverwritesTheOldestAndCountsIt()
        {
            var memory = new WallMemory(3, Merge);
            for (int i = 0; i < 5; i++)
            {
                // Spaced a metre apart so nothing merges.
                memory.Add(new Vector2(i * 3f, 0), new Vector2(i * 3f + 1f, 0), i);
            }

            Assert.AreEqual(3, memory.Count);
            Assert.AreEqual(2, memory.OverflowCount);
            float oldest = float.MaxValue;
            for (int i = 0; i < memory.Count; i++)
            {
                memory.Get(i, out _, out _, out float born);
                oldest = Mathf.Min(oldest, born);
            }

            Assert.AreEqual(2f, oldest, "segments 0 and 1 are the ones overwritten");
        }

        [Test]
        public void AChange_BumpsTheVersion_AndANoOpDoesNot()
        {
            var memory = new WallMemory(4, Merge);
            memory.Add(new Vector2(0, 0), new Vector2(1, 0));
            int version = memory.Version;
            memory.Add(new Vector2(0.2f, 0), new Vector2(0.8f, 0));
            Assert.AreEqual(version, memory.Version);
            memory.Add(new Vector2(0.5f, 0), new Vector2(1.5f, 0));
            Assert.Greater(memory.Version, version);
        }

        [Test]
        public void Clear_EmptiesTheStore()
        {
            var memory = new WallMemory(4, Merge);
            memory.Add(new Vector2(0, 0), new Vector2(1, 0));
            memory.Clear();
            Assert.AreEqual(0, memory.Count);
            Assert.AreEqual(0, memory.OverflowCount);
        }

        [Test]
        public void AddingAfterConstruction_Allocates_Nothing()
        {
            var memory = new WallMemory(64, Merge);
            memory.Add(new Vector2(0, 0), new Vector2(1, 0)); // warm up JIT
            long before = System.GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 500; i++)
            {
                memory.Add(new Vector2(i * 3f, 0), new Vector2(i * 3f + 1f, 0), i);
            }

            Assert.AreEqual(0, System.GC.GetAllocatedBytesForCurrentThread() - before);
        }
    }
}
