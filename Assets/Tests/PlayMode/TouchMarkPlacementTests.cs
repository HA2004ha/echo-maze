using System.Collections;
using EchoMaze.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace EchoMaze.Tests.PlayMode
{
    /// <summary>
    /// Where a touch mark ends up when the *player* is the thing that touched the wall.
    ///
    /// TouchMarkTests drives TouchMarks.Flash by hand, which is the right way to test a pool and the wrong
    /// way to test a placement rule: the rule lives in the pair (Player.OnCollisionEnter2D, Flash), and it
    /// only breaks where the number physics reports for a real contact meets the number the art is drawn at.
    /// That is the whole of issue 1 in Docs/ISSUES.md, so this file builds a wall and a collision instead of
    /// calling Flash.
    ///
    /// The player is moved by teleporting the body, not by pushing on it: Player.FixedUpdate overwrites the
    /// body's velocity from input every fixed step, so a test that set a velocity would be overruled a frame
    /// later. A teleport still creates a real contact pair, which is what the code under test subscribes to.
    /// </summary>
    public class TouchMarkPlacementTests : BlockoutRig
    {
        // Mirrors Level02SceneSetup's Wall_West: 0.4 thick, 5.8 tall, face a metre west of the spawn.
        const float WallThickness = 0.4f;
        const float WallHeight = 5.8f;

        // The drawn figure is 2289 px wide on a 4096 canvas at PPU 2560, so its edge sits 0.447 m from the
        // body centre while the hitbox edge sits 0.25 m. Anything under this and the dash is on the torso.
        const float SilhouetteReach = 0.44f;

        Rigidbody2D subjectBody;

        [SetUp]
        public void FindSubjectBody()
        {
            subjectBody = player.GetComponent<Rigidbody2D>();
        }

        [UnityTest]
        public IEnumerator APlayerTouchingAWall_FlashesClearOfItsOwnBody()
        {
            Vector2 spawn = player.transform.position;
            float wallFaceX = spawn.x - 1f;
            BuildWall(new Vector2(wallFaceX - WallThickness * 0.5f, spawn.y));

            // Step in until the circle overlaps the face by 3 cm: past that the contact is unambiguous, and
            // going deeper only moves the reported point, it does not change the rule.
            float targetX = wallFaceX + 0.25f - 0.03f;
            for (float x = spawn.x; x > targetX - 0.05f; x -= 0.05f)
            {
                subjectBody.position = new Vector2(x, spawn.y);
                yield return new WaitForFixedUpdate();

                if (marks.ActiveCount > 0)
                {
                    break;
                }
            }

            for (int settle = 0; settle < 3 && marks.ActiveCount == 0; settle++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.Greater(marks.ActiveCount, 0,
                           "no touch mark at all, so this test measured no collision: the body stopped at x=" +
                           subjectBody.position.x.ToString("F3") + " with the wall face at x=" +
                           wallFaceX.ToString("F3"));

            Vector2 mark = LastMarkPosition();
            Vector2 body = subjectBody.position;
            float distanceFromTheBody = Vector2.Distance(mark, body);

            Assert.GreaterOrEqual(distanceFromTheBody, SilhouetteReach,
                                  "the dash lands inside the character: it is " +
                                  distanceFromTheBody.ToString("F3") + " m from the body centre and the drawn " +
                                  "figure reaches " + SilhouetteReach + " m. A mark on the torso says nothing " +
                                  "about the wall, which is the only job a mark has.");

            Assert.Less(mark.x, body.x, "the mark was flashed on the wrong side of the body");

            // Clearing the body is only useful if the dash still speaks about the wall: it has to stay inside
            // the band the wall collider occupies, or it hangs in the dark past the surface.
            Assert.GreaterOrEqual(mark.x, wallFaceX - WallThickness,
                                  "the dash is drawn outside the wall it touched: the wall is only " +
                                  WallThickness + " m thick, so the clearance cannot be more than half that");
        }

        void BuildWall(Vector2 centre)
        {
            GameObject wall = New("rig wall west", true);
            wall.transform.position = centre;

            var box = wall.AddComponent<BoxCollider2D>();
            box.size = new Vector2(WallThickness, WallHeight);
        }

        /// <summary>The renderer the pool wrote to last: the cursor has already moved past it.</summary>
        Vector2 LastMarkPosition()
        {
            var pool = (SpriteRenderer[])Value(marks, "pool");
            int cursor = (int)Value(marks, "cursor");
            return pool[(cursor + pool.Length - 1) % pool.Length].transform.position;
        }
    }
}
