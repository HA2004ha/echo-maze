using EchoMaze.Gameplay;
using NUnit.Framework;

namespace EchoMaze.Tests
{
    /// <summary>
    /// The pack of lamps: how many a chapter gives, and that spending one is spending it. No scene, no
    /// keyboard — the part of "the player places the light" that can be settled with arithmetic.
    /// </summary>
    public class LanternStockTests
    {
        [Test]
        public void ANewPack_IsFull()
        {
            var stock = new LanternStock(2);

            Assert.AreEqual(2, stock.Capacity);
            Assert.AreEqual(2, stock.Remaining);
            Assert.IsFalse(stock.IsEmpty);
        }

        [Test]
        public void TakingASpare_LeavesOneFewer_AndThePackRunsOut()
        {
            var stock = new LanternStock(2);

            Assert.IsTrue(stock.TryTake());
            Assert.IsTrue(stock.TryTake());
            Assert.AreEqual(0, stock.Remaining);
            Assert.IsTrue(stock.IsEmpty);
            Assert.IsFalse(stock.TryTake(), "a third lamp is a room lit for free");
            Assert.AreEqual(0, stock.Remaining, "a refused take does not go negative");
        }

        [Test]
        public void ANewChapter_RefillsThePack()
        {
            var stock = new LanternStock(3);
            stock.TryTake();
            stock.TryTake();

            stock.Reset(3);

            Assert.AreEqual(3, stock.Remaining);
        }

        [Test]
        public void AChapterWithNoLamps_GivesNothing()
        {
            var stock = new LanternStock(0);

            Assert.IsTrue(stock.IsEmpty);
            Assert.IsFalse(stock.TryTake());
        }

        [Test]
        public void ANegativeCount_IsAnEmptyPack_NotARecipeForNegativeLamps()
        {
            var stock = new LanternStock(-4);

            Assert.AreEqual(0, stock.Capacity);
            Assert.AreEqual(0, stock.Remaining);
            Assert.IsFalse(stock.TryTake());
        }
    }
}
