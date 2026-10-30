using UnityEngine;

namespace EchoMaze.Core
{
    /// <summary>
    /// What the player remembers of the walls they touched: world-space segments captured at touch time and
    /// never read from a collider again.
    ///
    /// That is the point of it, not a shortcut. A door opens and its colliders switch off; the geometry now
    /// has an entrance, but the player's memory still has a wall there, so the way through is only found by
    /// feeling for it. Re-deriving the lines from colliders would quietly redraw the world as it is.
    ///
    /// Fixed capacity, flat arrays, no allocation after construction. A full store overwrites its oldest
    /// segment and counts it in <see cref="OverflowCount"/>. A touch next to an existing line extends that
    /// line (<see cref="TryMerge"/>) so leaning on one wall spends one slot, not one per physics step.
    /// </summary>
    public sealed class WallMemory
    {
        readonly Vector2[] starts;
        readonly Vector2[] ends;
        readonly float[] born;
        readonly float mergeDistance;
        int next;

        public WallMemory(int capacity, float mergeDistance)
        {
            capacity = Mathf.Max(1, capacity);
            starts = new Vector2[capacity];
            ends = new Vector2[capacity];
            born = new float[capacity];
            this.mergeDistance = mergeDistance;
        }

        public int Capacity => starts.Length;
        public int Count { get; private set; }

        /// <summary>Segments overwritten because the store was full. A debug number, never a gameplay one.</summary>
        public int OverflowCount { get; private set; }

        /// <summary>Bumped on every change, so a renderer rebuilds its mesh only when there is something new.</summary>
        public int Version { get; private set; }

        public void Get(int index, out Vector2 a, out Vector2 b, out float bornAt)
        {
            a = starts[index];
            b = ends[index];
            bornAt = born[index];
        }

        public void Clear()
        {
            Count = 0;
            next = 0;
            OverflowCount = 0;
            Version++;
        }

        /// <summary>Remembers a segment, or extends an existing line that it lies on.</summary>
        public void Add(Vector2 a, Vector2 b, float time = 0f)
        {
            if (a == b)
            {
                return;
            }

            // ponytail: linear scan of Count <= capacity entries per touch; a grid if capacity goes to the tens of thousands.
            for (int i = 0; i < Count; i++)
            {
                if (TryMerge(starts[i], ends[i], a, b, mergeDistance, out Vector2 ma, out Vector2 mb))
                {
                    if (ma != starts[i] || mb != ends[i])
                    {
                        starts[i] = ma;
                        ends[i] = mb;
                        Version++;
                    }

                    return;
                }
            }

            starts[next] = a;
            ends[next] = b;
            born[next] = time;
            next = (next + 1) % starts.Length;
            if (Count < starts.Length)
            {
                Count++;
            }
            else
            {
                OverflowCount++;
            }

            Version++;
        }

        /// <summary>
        /// Merges two segments into the one that spans both, if they are the same line: both ends of the
        /// new one within <paramref name="mergeDistance"/> of the old one's line (so "nearly the same place
        /// and roughly collinear" is one test), and their extents along it overlapping or no more than
        /// <paramref name="mergeDistance"/> apart. Refuses a parallel wall a metre over, a perpendicular
        /// wall at a corner, and a bump further along the same wall.
        ///
        /// The result's ends are two of the four input points, never an average, so merging a hundred
        /// times cannot smear a line off the wall.
        /// </summary>
        public static bool TryMerge(Vector2 a1, Vector2 b1, Vector2 a2, Vector2 b2, float mergeDistance,
                                    out Vector2 mergedA, out Vector2 mergedB)
        {
            mergedA = a1;
            mergedB = b1;

            Vector2 axis = b1 - a1;
            float length = axis.magnitude;
            if (length < 1e-6f)
            {
                return false;
            }

            axis /= length;
            Vector2 normal = new Vector2(-axis.y, axis.x);
            if (Mathf.Abs(Vector2.Dot(a2 - a1, normal)) > mergeDistance ||
                Mathf.Abs(Vector2.Dot(b2 - a1, normal)) > mergeDistance)
            {
                return false;
            }

            float t1a = 0f;
            float t1b = length;
            float t2a = Vector2.Dot(a2 - a1, axis);
            float t2b = Vector2.Dot(b2 - a1, axis);

            float lo1 = Mathf.Min(t1a, t1b), hi1 = Mathf.Max(t1a, t1b);
            float lo2 = Mathf.Min(t2a, t2b), hi2 = Mathf.Max(t2a, t2b);
            if (Mathf.Max(lo1, lo2) - Mathf.Min(hi1, hi2) > mergeDistance)
            {
                return false;
            }

            // Old segment is a1->b1 with t1a = 0 < t1b, so its own ends are the lo/hi unless the new one
            // reaches past them.
            mergedA = lo2 < 0f ? (t2a <= t2b ? a2 : b2) : a1;
            mergedB = hi2 > length ? (t2a >= t2b ? a2 : b2) : b1;
            return true;
        }
    }
}
