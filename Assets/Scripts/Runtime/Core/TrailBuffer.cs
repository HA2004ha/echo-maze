using UnityEngine;

namespace EchoMaze.Core
{
    /// <summary>
    /// The player's footprints: a ring of (position, birth time) with one point per
    /// <c>stepDistance</c> walked, dropped from the tail once older than <c>seconds</c>.
    ///
    /// Erasing every trail after a few seconds is cheap by construction: the buffer is sized once from how
    /// many points a player at full walk speed can leave in that time, expiry only ever pops the oldest
    /// (O(1) per point), and nothing allocates after construction. Index 0 is the oldest point.
    /// </summary>
    public sealed class TrailBuffer
    {
        /// <summary>Spare slots over the walking maximum, for a body shoved faster than it walks.</summary>
        public const int Slack = 4;

        readonly Vector2[] positions;
        readonly float[] born;
        readonly float seconds;
        readonly float stepSqr;
        int tail;
        Vector2 lastPoint;
        bool hasLast;

        public TrailBuffer(float seconds, float walkSpeed, float stepDistance)
        {
            this.seconds = seconds;
            stepSqr = stepDistance * stepDistance;
            int capacity = CapacityFor(seconds, walkSpeed, stepDistance);
            positions = new Vector2[capacity];
            born = new float[capacity];
        }

        public static int CapacityFor(float seconds, float walkSpeed, float stepDistance) =>
            Mathf.CeilToInt(seconds * walkSpeed / stepDistance) + Slack;

        public int Capacity => positions.Length;
        public int Count { get; private set; }

        /// <summary>Drops what has expired, then leaves a point if the body moved a full step.</summary>
        public void Tick(Vector2 position, float time)
        {
            while (Count > 0 && time - born[tail] >= seconds)
            {
                tail = (tail + 1) % positions.Length;
                Count--;
            }

            if (hasLast && (position - lastPoint).sqrMagnitude < stepSqr)
            {
                return;
            }

            if (Count == positions.Length)
            {
                tail = (tail + 1) % positions.Length;
                Count--;
            }

            int slot = (tail + Count) % positions.Length;
            positions[slot] = position;
            born[slot] = time;
            Count++;
            lastPoint = position;
            hasLast = true;
        }

        public void Get(int index, out Vector2 position, out float bornAt)
        {
            int slot = (tail + index) % positions.Length;
            position = positions[slot];
            bornAt = born[slot];
        }

        public void Clear()
        {
            Count = 0;
            tail = 0;
            hasLast = false;
        }
    }
}
