using EchoMaze.Audio;
using EchoMaze.Data;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EchoMaze.Gameplay
{
    /// <summary>
    /// The lamps the player is given for one chapter, and the only thing that spends them.
    ///
    /// The room does not place any light for you: two of them are in the pack, three in the rooms that mean
    /// it, and where they go is the player's decision. <b>Q</b> sets one down where the body is standing, and
    /// that is the whole interface — nothing is carried, nothing is picked back up, and a lamp placed badly
    /// stays placed badly for the rest of the run.
    ///
    /// That is on purpose and follows from what a lamp is here: light makes no noise, so it is the one thing
    /// in a room you can read for free, and the price of it is that it cannot move. The chapter's difficulty
    /// is then partly a question of how much dark you are allowed to answer.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LanternKit : MonoBehaviour
    {
        [SerializeField] EchoTuningProfile profile;
        [SerializeField] Player player;
        [SerializeField] LevelSession session;

        [Tooltip("Lamps this chapter gives. Two is the floor, three for the hard and brutal rooms; the level " +
                 "builder writes it from the chapter's grade. Zero is a room walked blind.")]
        [SerializeField, Range(0, 4)] int capacity = 2;

        readonly LanternStock stock = new LanternStock(0);

        public int Capacity => stock.Capacity;

        /// <summary>Lamps still in the pack. The HUD prints this and nothing else reads it.</summary>
        public int Remaining => stock.Remaining;

        void Awake()
        {
            // A scene-built kit starts here; one made by a test is wired and built instead.
            stock.Reset(capacity);
        }

        /// <summary>Wired by the level builder, which knows the chapter's grade.</summary>
        public void Build(EchoTuningProfile tuning, Player owner, LevelSession levelSession, int lamps)
        {
            profile = tuning;
            player = owner;
            session = levelSession;
            capacity = Mathf.Clamp(lamps, 0, 4);
            stock.Reset(capacity);
        }

        void Update()
        {
            if (player == null || profile == null || session != null && session.IsPaused || !player.IsAlive)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.qKey.wasPressedThisFrame)
            {
                PlaceHere();
            }
        }

        /// <summary>Sets a lamp down where the player is standing.</summary>
        public Lantern PlaceHere() =>
            PlaceAt(player != null ? player.Position : (Vector2)transform.position);

        /// <summary>
        /// Sets a lamp down at <paramref name="at"/>, or returns null when the pack is empty. The lamp is
        /// parented to the kit only so that it is cleaned up with the level object; it does not move.
        /// </summary>
        public Lantern PlaceAt(Vector2 at)
        {
            if (profile == null || !stock.TryTake())
            {
                if (session != null)
                {
                    session.Notice("no lamps left");
                }

                return null;
            }

            var go = new GameObject("Lantern");
            go.transform.SetParent(transform, true);
            go.transform.position = at;

            var lamp = go.AddComponent<Lantern>();
            lamp.Build(profile);

            // Light makes no noise the director hears; the hand that sets it down does make a sound.
            SoundBoard.PlayAt(SoundId.LampPlace, at, 0.8f);

            if (session != null)
            {
                session.Notice(Remaining > 0
                    ? "a lamp is down — " + Remaining + " left"
                    : "the last lamp is down");
            }

            return lamp;
        }
    }
}
