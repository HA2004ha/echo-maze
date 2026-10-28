using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using EchoMaze.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace EchoMaze.Tests.PlayMode
{
    /// <summary>
    /// Touch marks: the wall you brushed stays drawn for a moment after the wave that would have shown it
    /// has gone. They are the only free information in the game, so they have to appear on contact and
    /// disappear on schedule — a mark that never fades turns the dark into a map.
    /// </summary>
    public class TouchMarkTests
    {
        readonly List<GameObject> built = new List<GameObject>();
        TouchMarks marks;

        [SetUp]
        public void SetUp()
        {
            var go = new GameObject("test marks");
            built.Add(go);
            marks = go.AddComponent<TouchMarks>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in built)
            {
                Object.Destroy(go);
            }

            built.Clear();
        }

        [UnityTest]
        public IEnumerator AContact_LeavesOneMarkThatFadesAway()
        {
            float fade = (float)Field(marks, "fadeSeconds");

            marks.Flash(new Vector2(1f, 2f), Vector2.up);
            Assert.AreEqual(1, marks.ActiveCount, "the touch mark did not appear on contact");

            for (int frame = 0; frame < 4; frame++)
            {
                yield return null;
            }

            Assert.AreEqual(1, marks.ActiveCount, "a mark has to outlive the frame it was made in");

            yield return new WaitForSeconds(fade + 0.2f);

            Assert.AreEqual(0, marks.ActiveCount,
                            "the mark is still on the wall after " + fade + " s: the room gets mapped for free");
        }

        /// <summary>
        /// The pool is finite and ring-buffered, so dragging along a corridor recycles old marks instead of
        /// either leaking renderers or silently dropping the newest touch.
        /// </summary>
        [UnityTest]
        public IEnumerator MoreContactsThanThePool_RecycleTheOldestMark()
        {
            float fade = (float)Field(marks, "fadeSeconds");
            int pool = (int)Field(marks, "poolSize");

            for (int i = 0; i < pool + 6; i++)
            {
                marks.Flash(new Vector2(i * 0.2f, 0f), Vector2.up);
            }

            Assert.AreEqual(pool, marks.ActiveCount,
                            "the pool stopped taking new touches instead of recycling");

            yield return new WaitForSeconds(fade + 0.2f);

            Assert.AreEqual(0, marks.ActiveCount);
        }

        /// <summary>
        /// A mark is laid along the surface, not across it: the same contact on a wall that runs north-south
        /// has to read as a vertical dash. This is the whole reason Flash takes a normal.
        /// </summary>
        [Test]
        public void AMark_LiesAlongTheSurfaceItTouched()
        {
            marks.Flash(Vector2.zero, Vector2.up);
            float alongAWorizontalWall = LastMarkZ();

            marks.Flash(Vector2.zero, Vector2.right);
            float alongAVerticalWall = LastMarkZ();

            float difference = Mathf.Abs(Mathf.DeltaAngle(alongAWorizontalWall, alongAVerticalWall));
            Assert.Greater(difference, 45f,
                           "both marks were laid out the same way whatever they touched");
        }

        /// <summary>The renderer the pool wrote to last: the cursor has already moved past it.</summary>
        float LastMarkZ()
        {
            var pool = (SpriteRenderer[])Field(marks, "pool");
            int cursor = (int)Field(marks, "cursor");
            return pool[(cursor + pool.Length - 1) % pool.Length].transform.eulerAngles.z;
        }

        static object Field(object target, string name)
        {
            return target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);
        }
    }
}
