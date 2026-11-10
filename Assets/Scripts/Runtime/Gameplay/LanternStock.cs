using System;

namespace EchoMaze.Gameplay
{
    /// <summary>
    /// How many lamps the player still has, with no Unity in it: the arithmetic behind "two or three a
    /// chapter" that an EditMode test can hold to a rule without a scene.
    ///
    /// A lamp, once spent, is spent. The pack does not refill on a death, because the room is meant to be
    /// solved with what is in it — a retry that came back with the lamps it had already placed would make the
    /// dark optional.
    /// </summary>
    public sealed class LanternStock
    {
        int capacity;
        int remaining;

        public LanternStock(int capacity)
        {
            Reset(capacity);
        }

        /// <summary>Lamps the chapter gave. Zero for a room that is meant to be walked blind.</summary>
        public int Capacity => capacity;

        public int Remaining => remaining;

        public bool IsEmpty => remaining <= 0;

        /// <summary>Starts a chapter: the pack is full again, because it is a new room.</summary>
        public void Reset(int newCapacity)
        {
            capacity = Math.Max(0, newCapacity);
            remaining = capacity;
        }

        /// <summary>Takes one lamp. False when the pack is empty and nothing should be set down.</summary>
        public bool TryTake()
        {
            if (remaining <= 0)
            {
                return false;
            }

            remaining--;
            return true;
        }
    }
}
