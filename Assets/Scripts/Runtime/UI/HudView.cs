using EchoMaze.Core;
using EchoMaze.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace EchoMaze.UI
{
    /// <summary>
    /// What the game tells the player about their own sound: how long the last wave still costs, what E
    /// would do where they are standing, which chapter they are in, and the two moments worth interrupting
    /// for — a sanctuary taking the run into memory, and a floor answering a step.
    ///
    /// It reads, it never decides. Every number here is already the game's own: the cooldown fraction comes
    /// from the director that pays it, the prompt comes from the interactable the player is next to. Nothing
    /// in this class is a second copy of a rule, which is the only way an interface like this stays true when
    /// the tuning changes.
    ///
    /// All labels fade on unscaled time, so the chapter title does not survive a pause as a frozen banner.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HudView : MonoBehaviour
    {
        [SerializeField] EchoDirector director;
        [SerializeField] Player player;
        [SerializeField] LevelSession session;

        [Header("Wired by the scene builder")]
        [Tooltip("The sweep that shows the recovery still being paid: Image Type Filled, Fill Method " +
                 "Vertical, Fill Origin Bottom — a bar that empties upward as the debt is paid off. A radial " +
                 "fill on a solid sprite draws a pie, and a pie reads as a clock rather than as a cost.")]
        [SerializeField] Image cooldownMeter;
        [SerializeField] Text promptLabel;
        [SerializeField] Text titleLabel;
        [SerializeField] Text noticeLabel;

        [Tooltip("Seconds the chapter title stays up before it fades out. Long enough to read a name, short " +
                 "enough that it is gone before the first ping.")]
        [SerializeField, Range(1f, 12f)] float titleSeconds = 4f;
        [SerializeField, Range(0.2f, 4f)] float titleFadeSeconds = 1f;
        [Tooltip("Seconds a notice — a checkpoint, a crying floor — stays on screen.")]
        [SerializeField, Range(0.5f, 8f)] float noticeSeconds = 2.5f;

        float titleUntil = float.NegativeInfinity;
        float noticeUntil = float.NegativeInfinity;
        string lastPrompt = "";

        /// <summary>The fraction the meter is drawn at, exposed so a test can assert it without a canvas.</summary>
        public float MeterFill => director != null ? director.CooldownFraction : 0f;

        /// <summary>The line E answers, or empty when there is nothing to touch here.</summary>
        public string PromptText => player != null && player.Nearby != null ? player.Nearby.Prompt : "";

        void Awake()
        {
            if (titleLabel != null && session != null)
            {
                titleLabel.text = session.ChapterTitle;
            }

            titleUntil = Time.unscaledTime + titleSeconds + titleFadeSeconds;

            if (session != null)
            {
                session.Noticed += Notice;
            }

            if (director == null)
            {
                // The HUD is decoration: a scene without a director is broken, but it is broken by the
                // director's own error and not by a second one from the interface.
                enabled = false;
            }
        }

        void Update()
        {
            if (cooldownMeter != null)
            {
                cooldownMeter.fillAmount = MeterFill;
            }

            string prompt = PromptText;
            if (promptLabel != null && prompt != lastPrompt)
            {
                promptLabel.text = prompt;
                lastPrompt = prompt;
            }

            if (titleLabel != null)
            {
                titleLabel.color = Fade(titleUntil, titleLabel.color);
            }

            if (noticeLabel != null)
            {
                noticeLabel.color = Fade(noticeUntil, noticeLabel.color);
            }
        }

        /// <summary>Show a line for a few seconds. The level's own events call this; nothing else needs to.</summary>
        public void Notice(string text)
        {
            if (noticeLabel == null)
            {
                return;
            }

            noticeLabel.text = text;
            noticeUntil = Time.unscaledTime + noticeSeconds + titleFadeSeconds;
            Color color = noticeLabel.color;
            color.a = 1f;
            noticeLabel.color = color;
        }

        /// <summary>
        /// The pause panel is a full-screen overlay above this, so the HUD does not have to hide itself:
        /// deactivating the object here would also unsubscribe it from the resume, which is how a HUD
        /// disappears for good.
        /// </summary>
        Color Fade(float until, Color color)
        {
            float remaining = until - Time.unscaledTime;
            color.a = remaining <= 0f ? 0f
                : Mathf.Clamp01(remaining / Mathf.Max(0.01f, titleFadeSeconds));
            return color;
        }
    }
}
