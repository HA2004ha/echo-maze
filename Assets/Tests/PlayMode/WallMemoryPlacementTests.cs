using System.Collections;
using EchoMaze.Core;
using EchoMaze.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace EchoMaze.Tests.PlayMode
{
    /// <summary>
    /// The wall memory fed by a real contact: the player is teleported into a wall (same technique as
    /// TouchMarkPlacementTests, for the same reason: Player.FixedUpdate overwrites any velocity).
    /// </summary>
    public class WallMemoryPlacementTests : BlockoutRig
    {
        const float WallThickness = 0.4f;
        const float WallHeight = 5.8f;

        Rigidbody2D subjectBody;
        WallMemoryView view;
        BoxCollider2D wall;
        float wallFaceX;

        [SetUp]
        public void BuildMemory()
        {
            subjectBody = player.GetComponent<Rigidbody2D>();

            GameObject go = New("rig wall memory", false);
            view = go.AddComponent<WallMemoryView>();
            Wire(view, "profile", profile);
            go.SetActive(true);
            Wire(marks, "memory", view);

            Vector2 spawn = player.transform.position;
            wallFaceX = spawn.x - 1f;
            GameObject wallObject = New("rig wall west");
            wallObject.transform.position = new Vector2(wallFaceX - WallThickness * 0.5f, spawn.y);
            wall = wallObject.AddComponent<BoxCollider2D>();
            wall.size = new Vector2(WallThickness, WallHeight);
        }

        IEnumerator PressAgainstTheWall(float y)
        {
            float x = wallFaceX + 0.25f - 0.03f;
            for (float px = wallFaceX + 0.6f; px > x - 0.05f; px -= 0.05f)
            {
                subjectBody.position = new Vector2(px, y);
                yield return new WaitForFixedUpdate();
                if (view.Memory.Count > 0)
                {
                    break;
                }
            }

            yield return new WaitForFixedUpdate();
        }

        [UnityTest]
        public IEnumerator TouchingTheWall_RemembersALineAlongIt_ThatOutlivesTheCollider()
        {
            yield return PressAgainstTheWall(0f);

            Assert.GreaterOrEqual(view.Memory.Count, 1, "no segment: the test measured no collision");
            view.Memory.Get(0, out Vector2 a, out Vector2 b, out _);

            Vector2 mid = (a + b) * 0.5f;
            Assert.AreEqual(wallFaceX, mid.x, 0.1f, "the line sits on the wall face");
            Assert.AreEqual(0f, mid.y, 0.2f, "and near the contact");
            Assert.Less(Mathf.Abs(a.x - b.x), 0.15f, "along the wall (vertical), not across it");
            Assert.AreEqual(profile.wallMemorySegmentLength, Vector2.Distance(a, b), 0.25f);

            // A door opening: the collider goes, the memory must not.
            wall.enabled = false;
            yield return new WaitForFixedUpdate();
            yield return null;

            Assert.GreaterOrEqual(view.Memory.Count, 1);
            view.Memory.Get(0, out Vector2 a2, out Vector2 b2, out _);
            Assert.AreEqual(a, a2);
            Assert.AreEqual(b, b2);
        }

        [UnityTest]
        public IEnumerator BumpingTheSameSpotTenTimes_DoesNotGrowTheMemory()
        {
            yield return PressAgainstTheWall(0f);
            int first = view.Memory.Count;
            Assert.GreaterOrEqual(first, 1);

            for (int i = 0; i < 10; i++)
            {
                subjectBody.position = new Vector2(wallFaceX + 1f, 0f);
                yield return new WaitForFixedUpdate();
                yield return PressAgainstTheWall(0f);
            }

            Assert.LessOrEqual(view.Memory.Count, first + 2);
        }

        [Test]
        public void AnEnemyContact_IsNotRemembered()
        {
            GameObject creature = New("rig creature");
            var collider = creature.AddComponent<CircleCollider2D>();
            creature.AddComponent<EchoSurface>().kind = EchoMaze.Data.SurfaceKind.Crawler;

            marks.Remember(collider, Vector2.zero, Vector2.right);
            Assert.AreEqual(0, view.Memory.Count);

            marks.Remember(wall, Vector2.zero, Vector2.right);
            Assert.AreEqual(1, view.Memory.Count, "while a wall at the same spot is");
        }
    }
}
