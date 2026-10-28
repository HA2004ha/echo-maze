using System.Collections;
using EchoMaze.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace EchoMaze.Tests.PlayMode
{
    /// <summary>
    /// The loop the blockout exists for: the Crawler's body touching yours ends the run, the screen says so,
    /// and you are back at the room start with the creature back on its route. One hit, no health bar, and a
    /// retry the GDD promises in under two seconds.
    /// </summary>
    public class RunControllerTests : BlockoutRig
    {
        static readonly Vector2 RoomStart = new Vector2(2f, -3.15f);
        static readonly Vector2 CrawlerSpawn = new Vector2(0f, -0.6f);

        RunController controller;
        Crawler crawler;
        GameObject deathNotice;

        [SetUp]
        public void BuildRun()
        {
            player.transform.position = RoomStart;
            crawler = NewCrawler(CrawlerSpawn);

            deathNotice = New("rig death notice");
            var host = New("rig run controller", false);
            controller = host.AddComponent<RunController>();
            Wire(controller, "player", player);
            Wire(controller, "deathNotice", deathNotice);
            host.SetActive(true);
        }

        [UnityTest]
        public IEnumerator TheCrawlerTouchingYou_EndsTheRun()
        {
            crawler.transform.position = player.transform.position;

            for (int frame = 0; frame < 120 && player.IsAlive; frame++)
            {
                yield return null;
            }

            Assert.IsFalse(player.IsAlive, "two bodies in the same place and nobody died");
            Assert.IsTrue(deathNotice.activeSelf, "the run ended without the screen saying why");
        }

        [UnityTest]
        public IEnumerator OneHit_PutsYouBackAtTheRoomStartUnderTwoSeconds()
        {
            float start = Time.unscaledTime;
            player.Die();
            Assert.IsTrue(controller.IsDead);

            // Dragged away, so that passing the respawn assertions means something came back rather than
            // nothing having moved.
            player.transform.position = new Vector2(-3f, 2f);
            crawler.transform.position = new Vector2(1.5f, 2f);

            // The retry is timed on the unscaled clock on purpose (a death screen that freezes time must
            // still hand the run back), and the test harness runs its frames faster than the clock: 600
            // yielded frames here are milliseconds of real time. So the wait polls the real clock.
            while (Time.unscaledTime - start < 3f && !player.IsAlive)
            {
                yield return null;
            }

            Assert.IsTrue(player.IsAlive, "the retry never came");
            Assert.LessOrEqual(Time.unscaledTime - start, 2f,
                               "the GDD's promise is an instant retry; a dead player watching a black " +
                               "screen for longer than two seconds is a quit, not a lesson");
            Assert.Less(Vector2.Distance(player.transform.position, RoomStart), 0.2f,
                        "you did not come back where the room started");
            Assert.Less(Vector2.Distance(crawler.Position, CrawlerSpawn), 0.5f,
                        "the Crawler stayed where the kill happened, so the retry is a different room");
            Assert.IsFalse(deathNotice.activeSelf, "the screen stayed black after the retry");
        }

        /// <summary>
        /// The kill comes from a collision callback and the retry from a timer, so a second touch in the
        /// same instant must not schedule a second retry or the player respawns twice.
        /// </summary>
        [Test]
        public void DyingTwice_IsOneDeath()
        {
            int deaths = 0;
            player.Died += () => deaths++;

            player.Die();
            player.Die();

            Assert.AreEqual(1, deaths, "a single death was announced twice");
        }
    }
}
