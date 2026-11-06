using System.Collections.Generic;

namespace EchoMaze.Gameplay
{
    /// <summary>The tutorial lines of chapter one. Wording lives in <see cref="HintTexts"/>.</summary>
    public enum Hint
    {
        Start,
        AfterWave,
        FirstTouch,
        WalkNoise,
        Door
    }

    /// <summary>
    /// The rule behind the chapter-one hints, with no Unity in it: each hint is accepted once, accepted
    /// hints wait their turn, and the next one is released only after the previous one has had its slot,
    /// so two lines never share the HUD. The component that watches the player lives in
    /// <see cref="LevelHints"/>; this class only answers "may a line be shown now, and which?".
    /// </summary>
    public sealed class HintQueue
    {
        readonly HashSet<Hint> requested = new HashSet<Hint>();
        readonly Queue<Hint> pending = new Queue<Hint>();
        readonly float slotSeconds;
        float busyUntil = float.NegativeInfinity;

        /// <param name="slotSeconds">How long one hint owns the screen, fade included.</param>
        public HintQueue(float slotSeconds)
        {
            this.slotSeconds = slotSeconds;
        }

        public int Pending => pending.Count;

        public bool WasRequested(Hint hint) => requested.Contains(hint);

        /// <summary>Queues the hint unless it was ever requested before. False means it was a repeat.</summary>
        public bool Request(Hint hint)
        {
            if (!requested.Add(hint))
            {
                return false;
            }

            pending.Enqueue(hint);
            return true;
        }

        /// <summary>The next hint to show at <paramref name="now"/>, or false while one is still on screen.</summary>
        public bool TryNext(float now, out Hint hint)
        {
            if (pending.Count == 0 || now < busyUntil)
            {
                hint = default;
                return false;
            }

            hint = pending.Dequeue();
            busyUntil = now + slotSeconds;
            return true;
        }
    }
}
