using System.Collections;
using EchoMaze.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace EchoMaze.Tests.PlayMode
{
    /// <summary>
    /// The Crawler has no pathfinding, so a straight walk into a wall used to wedge it for good (it sat in the
    /// hollow it had chased the player to). It now slides along the wall it meets.
    /// </summary>
    public class CrawlerSlideTests : BlockoutRig
    {
        [UnityTest]
        public IEnumerator ACrawlerWalkingIntoAWall_SlidesAlongItInsteadOfStopping()
        {
            var wall = new GameObject("Wall") { layer = LayerMask.NameToLayer("World") };
            wall.transform.position = new Vector2(0f, 1.5f);
            wall.AddComponent<BoxCollider2D>().size = new Vector2(8f, 0.4f);

            // Goal straight through the wall and a little to the right: head-on cannot progress, a slide can.
            Crawler crawler = NewCrawler(Vector2.zero, new[] { new Vector2(2f, 4f), new Vector2(2f, 4f) });
            Physics2D.SyncTransforms();

            Vector2 start = crawler.Position;
            yield return new WaitForSeconds(2f);

            Assert.Greater(Vector2.Distance(start, crawler.Position), 1.2f,
                           "the Crawler pressed against the wall and did not move");
            Object.Destroy(wall);
        }
    }
}
