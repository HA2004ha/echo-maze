using EchoMaze.Gameplay;
using NUnit.Framework;

namespace EchoMaze.Tests.PlayMode
{
    /// <summary>One death, one snarl: a repeated Die() does not repeat it, and a Revive makes none.</summary>
    public class DeathSnarlTests : BlockoutRig
    {
        DeathSnarl snarl;

        [SetUp]
        public void AddSnarl()
        {
            // Without a clip: batch mode has no audio device, and what is under test is when it is asked for.
            snarl = player.gameObject.AddComponent<DeathSnarl>();
        }

        [Test]
        public void DyingTwice_PlaysOnce()
        {
            player.Die();
            player.Die();

            Assert.AreEqual(1, snarl.PlayCount);
        }

        [Test]
        public void Reviving_PlaysNothing_AndTheNextDeathPlaysAgain()
        {
            player.Die();
            player.Revive(UnityEngine.Vector2.zero);
            Assert.AreEqual(1, snarl.PlayCount, "the revive made the sound");

            player.Die();
            Assert.AreEqual(2, snarl.PlayCount);
        }
    }
}
