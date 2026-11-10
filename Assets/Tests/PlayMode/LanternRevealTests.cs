using System.Collections;
using EchoMaze.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace EchoMaze.Tests.PlayMode
{
    /// <summary>
    /// The lamp against real colliders: the reveal is a physics query, so the only honest test of it is one
    /// that has walls in the world. What is asserted is the rule the player feels — a lamp standing in a room
    /// draws what it can see, on its own, and never moves.
    /// </summary>
    public class LanternRevealTests : BlockoutRig
    {
        Lantern lantern;

        /// <summary>Builds a lamp at <paramref name="at"/> and, optionally, a wall a wall-away from it.</summary>
        Lantern BuildLamp(Vector2 at, bool withWall)
        {
            if (withWall)
            {
                GameObject wallObject = New("rig lantern wall");
                wallObject.transform.position = at + new Vector2(1.5f, 0f);
                wallObject.layer = LayerMask.NameToLayer("EchoTarget");
                BoxCollider2D wall = wallObject.AddComponent<BoxCollider2D>();
                wall.size = new Vector2(0.4f, 6f);
            }

            // Inactive first: Awake runs on activation, and the profile has to be there before it does.
            GameObject go = New("rig lantern", false);
            go.transform.position = at;

            lantern = go.AddComponent<Lantern>();
            Wire(lantern, "profile", profile);
            go.SetActive(true);
            return lantern;
        }

        [UnityTest]
        public IEnumerator ALamp_DrawsTheWallItCanSee_WithoutBeingAsked()
        {
            BuildLamp(Vector2.zero, withWall: true);

            yield return new WaitForFixedUpdate();
            yield return null;

            Assert.IsTrue(lantern.IsLit, "a lamp is lit by standing there; there is no key for it");
            Assert.Greater(lantern.RevealedSegments, 0, "a wall a metre and a half away was not drawn");

            int drawn = 0;
            foreach (SpriteRenderer line in lantern.GetComponentsInChildren<SpriteRenderer>(false))
            {
                drawn += line.enabled ? 1 : 0;
            }

            Assert.Greater(drawn, 1, "the store has segments but the pool drew none of them");
        }

        [UnityTest]
        public IEnumerator ALampInAnEmptyPartOfTheRoom_DrawsNothing()
        {
            BuildLamp(new Vector2(0f, -40f), withWall: false);

            yield return new WaitForFixedUpdate();
            yield return null;

            Assert.IsTrue(lantern.IsLit);
            Assert.AreEqual(0, lantern.RevealedSegments, "there is nothing within reach, so nothing is drawn");
        }

        [UnityTest]
        public IEnumerator ALamp_StaysWhereTheRoomPutIt()
        {
            Vector2 where = new Vector2(2.5f, -1.5f);
            BuildLamp(where, withWall: true);

            for (int frame = 0; frame < 10; frame++)
            {
                yield return null;
            }

            Assert.AreEqual(0f, ((Vector2)lantern.transform.position - where).magnitude, 1e-4f,
                            "a lamp is a place: nothing about it moves");
            Assert.IsNull(lantern.transform.parent, "and it is not carried by anything");
        }
    }
}
