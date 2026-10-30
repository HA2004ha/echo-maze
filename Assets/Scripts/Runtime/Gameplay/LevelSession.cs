using System;
using System.Collections.Generic;
using EchoMaze.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EchoMaze.Gameplay
{
    /// <summary>
    /// The state of one chapter: where the last sanctuary put the checkpoint, how many waves this run has
    /// spent, how many times the player has been heard, whether the level is paused or finished.
    ///
    /// One per level scene, owned by nobody else. <see cref="GameFlow"/> is the chapter *list* and lives
    /// across scenes, which is why the two are separate classes: a scene-to-scene reference cannot be
    /// serialized, so the level asks the flow for one call when it ends and never holds it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LevelSession : MonoBehaviour
    {
        [SerializeField] Player player;
        [SerializeField] RunController runner;
        [SerializeField] EchoDirector director;
        [Tooltip("Shown once on the HUD when the chapter opens, so the player knows which room they are in.")]
        [SerializeField] string chapterTitle = "";

        readonly List<Action> resetHooks = new List<Action>();

        Vector2 checkpoint;
        bool hasCheckpoint;
        bool paused;

        /// <summary>
        /// One line of copy for the HUD, from whoever in the level has something to say: a checkpoint taken,
        /// a floor that answered. The level reports and the interface renders, so neither one knows the
        /// other's business and there is exactly one place a sentence can come from.
        /// </summary>
        public event Action<string> Noticed;

        /// <summary>
        /// A <see cref="Noticed"/> that asks to stay up for a given number of seconds: the chapter-one hints,
        /// which are longer than a checkpoint line. Same HUD label, same listener.
        /// </summary>
        public event Action<string, float> Hinted;

        /// <summary>Raised on pause and on resume: the panel is the only listener and it needs both edges.</summary>
        public event Action<bool> PauseToggled;

        /// <summary>Raised when the exit is reached. Only GameFlow listens.</summary>
        public event Action Completed;

        /// <summary>
        /// Where a death sends the player: the last sanctuary, or until there is one the spawn captured in
        /// <c>Awake</c>. It used to fall back to the player's *current* position, which is where they had just
        /// died (Docs/ISSUES.md #9).
        /// </summary>
        public Vector2 Checkpoint => checkpoint;
        public string ChapterTitle => chapterTitle;
        public bool IsPaused => paused;
        public bool IsComplete { get; private set; }

        /// <summary>Waves emitted since the chapter opened, including the ones the microphone made.</summary>
        public int PingCount { get; private set; }

        /// <summary>Deaths since the chapter opened. The GDD's retry is a lesson, so the game counts them.</summary>
        public int DeathCount { get; private set; }

        /// <summary>Seconds of unscaled play since the chapter opened, for the end-of-run panel.</summary>
        public float ElapsedSeconds { get; private set; }

        void Awake()
        {
            // The spawn: MazeLevel (execution order -20) has already placed the player on the 'P' cell, and
            // Level02's builder serialises the player on its spawn.
            checkpoint = player != null ? player.Position : Vector2.zero;

            if (director != null)
            {
                director.PingEmitted += OnPing;
            }
        }

        void OnDestroy()
        {
            if (director != null)
            {
                director.PingEmitted -= OnPing;
            }
        }

        void Update()
        {
            if (IsComplete || player == null)
            {
                return;
            }

            // Unscaled from the first frame: the pause freezes the clock this accumulates on, so the run's
            // time is the time spent playing rather than the time spent staring at a panel.
            ElapsedSeconds += Time.unscaledDeltaTime;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                SetPaused(!paused);
            }
        }

        /// <summary>
        /// Freezes the world and keeps the interface alive. <c>Time.timeScale</c> rather than disabling
        /// components: every clock in the game runs on <c>Time.time</c>, so one switch stops the arcs, the
        /// body, the cooldowns and the enemies together, and nothing has to be un-frozen in the right order.
        /// The microphone is muted separately because <c>Time.time</c> standing still would leave a shout
        /// that pays no cooldown.
        /// </summary>
        public void SetPaused(bool value)
        {
            if (paused == value)
            {
                return;
            }

            paused = value;
            Time.timeScale = value ? 0f : 1f;
            if (director != null)
            {
                director.EmitEnabled = !value;
            }

            PauseToggled?.Invoke(value);
        }

        public void SetCheckpoint(Vector2 position)
        {
            // A sanctuary re-entered is the same checkpoint; the notice only fires when the run really moved.
            if (hasCheckpoint && Vector2.Distance(position, checkpoint) < 0.01f)
            {
                return;
            }

            checkpoint = position;
            hasCheckpoint = true;
            Notice("the room remembers you");
        }

        /// <summary>A line for the HUD that stays up for <paramref name="seconds"/> instead of the default.</summary>
        public void Hint(string text, float seconds)
        {
            Hinted?.Invoke(text, seconds);
        }

        /// <summary>One line for the HUD. A level says things; only the interface has to know how.</summary>
        public void Notice(string text)
        {
            Noticed?.Invoke(text);
        }

        /// <summary>
        /// Called by <see cref="RunController"/> instead of its own restart routine when a level has a
        /// session: the body goes back to the last sanctuary, the section's authored state is rebuilt, and
        /// the death is counted. Week 4's full section reset (enemies respawned at their patrol anchors)
        /// plugs into the hooks registered here.
        /// </summary>
        public void RestartFromCheckpoint()
        {
            DeathCount++;
            player.Revive(Checkpoint);

            foreach (Crawler crawler in FindObjectsByType<Crawler>(FindObjectsInactive.Exclude))
            {
                crawler.ResetRun();
            }

            foreach (Action hook in resetHooks)
            {
                hook();
            }

            if (runner != null)
            {
                runner.ShowDeathNotice(false);
            }
        }

        /// <summary>
        /// A reset that the level author needs but the session cannot know about — re-closing a door,
        /// emptying a hollow. Registered by whoever builds the object, run in registration order.
        /// </summary>
        public void RegisterResetHook(Action hook)
        {
            if (hook != null)
            {
                resetHooks.Add(hook);
            }
        }

        /// <summary>The exit was reached: the world stops, the counters freeze, and the flow is told.</summary>
        public void Complete()
        {
            if (IsComplete)
            {
                return;
            }

            // A scene played straight from the editor has no GameFlow, so nothing would show the victory
            // panel and the game would just freeze. Say so and keep playing.
            if (GameFlow.Active == null)
            {
                Hint("You found the way out. (Start from Boot to play the whole run.)", 5f);
                return;
            }

            IsComplete = true;
            SetPaused(true);
            Completed?.Invoke();
        }

        void OnPing(PingRecord ping) => PingCount++;
    }
}
