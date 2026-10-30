using EchoMaze.Gameplay;
using NUnit.Framework;

namespace EchoMaze.Tests
{
    /// <summary>The chapter-one hint rule: once each, in order, never two on the screen at the same time.</summary>
    public class HintQueueTests
    {
        const float Slot = 5f;

        [Test]
        public void AHint_IsAcceptedOnlyOnce()
        {
            var queue = new HintQueue(Slot);

            Assert.IsTrue(queue.Request(Hint.Start));
            Assert.IsFalse(queue.Request(Hint.Start), "a repeat was queued");
            Assert.IsTrue(queue.TryNext(0f, out Hint shown));
            Assert.AreEqual(Hint.Start, shown);
            Assert.IsFalse(queue.Request(Hint.Start), "a hint that was already shown came back");
        }

        [Test]
        public void TwoHints_NeverOverlap_AndComeOutInTheOrderAsked()
        {
            var queue = new HintQueue(Slot);
            queue.Request(Hint.Start);
            queue.Request(Hint.AfterWave);

            Assert.IsTrue(queue.TryNext(10f, out Hint first));
            Assert.AreEqual(Hint.Start, first);
            Assert.IsFalse(queue.TryNext(10f + Slot - 0.01f, out _), "second hint appeared on top of the first");
            Assert.IsTrue(queue.TryNext(10f + Slot, out Hint second));
            Assert.AreEqual(Hint.AfterWave, second);
            Assert.AreEqual(0, queue.Pending);
        }

        [Test]
        public void AnEmptyQueue_ShowsNothing()
        {
            Assert.IsFalse(new HintQueue(Slot).TryNext(100f, out _));
        }

        [Test]
        public void EveryHint_HasWording()
        {
            foreach (Hint hint in System.Enum.GetValues(typeof(Hint)))
            {
                Assert.IsNotEmpty(HintTexts.For(hint), hint + " has no text");
            }
        }
    }
}
