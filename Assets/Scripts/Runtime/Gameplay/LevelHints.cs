using EchoMaze.Core;
using UnityEngine;

namespace EchoMaze.Gameplay
{
    /// <summary>
    /// Watches the player in chapter one and hands <see cref="HintQueue"/> the moment each control starts
    /// to matter: the first wave, the first wall touched, three seconds of noisy walking, a door in reach.
    /// The lines go out through <see cref="LevelSession.Hint"/>, i.e. the HUD's one notice label, so there
    /// is no second text system to keep in step with it.
    ///
    /// Off unless <see cref="HintsEnabled"/>: only the maze builder turns it on, because the Corridor is a
    /// room with a Crawler in it and a line of instruction there is a line of noise.
    ///
    /// The wall touch is read from <see cref="TouchMarks.ActiveCount"/> rather than a new event, so it fires
    /// on the mark the player already sees and needs nothing from the wall-memory work.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LevelHints : MonoBehaviour
    {
        /// <summary>Seconds a hint is on screen before it starts to fade.</summary>
        const float ShowSeconds = 4f;

        /// <summary>The fade the HUD adds after <see cref="ShowSeconds"/> (HudView.titleFadeSeconds).</summary>
        const float FadeSeconds = 1f;

        [SerializeField] EchoDirector director;
        [SerializeField] Player player;
        [SerializeField] TouchMarks marks;
        [SerializeField] LevelSession session;
        [Tooltip("Chapter one only. The maze builder turns it on; the Corridor leaves it off.")]
        [SerializeField] bool hintsEnabled;
        [Tooltip("Seconds of walking without Shift before the silent-step hint appears.")]
        [SerializeField, Range(1f, 10f)] float noisyWalkSeconds = 3f;

        readonly HintQueue queue = new HintQueue(ShowSeconds + FadeSeconds);
        float noisyWalked;

        public bool HintsEnabled => hintsEnabled;

        void OnEnable()
        {
            if (director != null)
            {
                director.PingEmitted += OnPing;
            }
        }

        void OnDisable()
        {
            if (director != null)
            {
                director.PingEmitted -= OnPing;
            }
        }

        void Start()
        {
            if (hintsEnabled)
            {
                queue.Request(Hint.Start);
            }
        }

        void OnPing(PingRecord ping)
        {
            // The F pulse is a full circle; "after the first wave" is about Space, which F is the answer to.
            if (hintsEnabled && !ping.fullCircle)
            {
                queue.Request(Hint.AfterWave);
            }
        }

        void Update()
        {
            if (!hintsEnabled || player == null || session == null || !player.IsAlive || session.IsPaused)
            {
                return;
            }

            if (!queue.WasRequested(Hint.FirstTouch) && marks != null && marks.ActiveCount > 0)
            {
                queue.Request(Hint.FirstTouch);
            }

            if (player.IsMoving && !player.IsQuiet)
            {
                noisyWalked += Time.unscaledDeltaTime;
                if (noisyWalked >= noisyWalkSeconds)
                {
                    queue.Request(Hint.WalkNoise);
                }
            }
            else
            {
                noisyWalked = 0f;
            }

            if (player.Nearby is Door)
            {
                queue.Request(Hint.Door);
            }

            if (queue.TryNext(Time.unscaledTime, out Hint next))
            {
                session.Hint(HintTexts.For(next), ShowSeconds);
            }
        }
    }
}
