using UnityEngine;
using UnityEngine.UI;

namespace EchoMaze.UI
{
    /// <summary>
    /// The victory emote: the celebration the run ends on. Six layers of one painting — the swirl of light,
    /// the confetti, the crown and the word, the pedestal, the ghost — arriving in the order that makes the
    /// picture, then breathing while the summary is read.
    ///
    /// It is a UI animation and not a spawned effect because the panel it belongs to is a canvas the
    /// generator builds, and the emote has to survive the same rebuild: every layer is a serialized
    /// <see cref="Image"/>, so a repair pass re-asserts the art instead of stacking a second copy.
    ///
    /// Two properties of this clock are load-bearing. It runs on <c>unscaled</c> time, because the run is
    /// over and <c>LevelSession</c> has already stopped the world at <c>timeScale = 0</c> — a scaled emote
    /// would be a still frame. And it is driven from <c>Update</c> on the canvas, not from the fading child,
    /// so a panel hidden mid-entrance can be shown again without the animation being stuck at whatever
    /// fraction it had reached.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VictoryEmote : MonoBehaviour
    {
        [Tooltip("The pop the emote opens with. Additive, over everything else.")]
        [SerializeField] Image flash;

        [Tooltip("The light the ghost stands in — additive, painted on black.")]
        [SerializeField] Image swirls;
        [SerializeField] Image confetti;

        [Tooltip("The crown and the word, additive like the swirls.")]
        [SerializeField] Image title;

        [Tooltip("The two matted pieces. Opaque ink, so they are drawn with the canvas's own material.")]
        [SerializeField] Image pedestal;
        [SerializeField] Image ghost;

        /// <summary>When the entrance has finished and the loop begins.</summary>
        public const float EntranceSeconds = 1.25f;

        float elapsed = -1f;

        public bool IsPlaying => elapsed >= 0f;

        /// <summary>Seconds since <see cref="Play"/>, on the same unscaled clock the pose is computed from.</summary>
        public float Elapsed => Mathf.Max(0f, elapsed);

        void Awake() => Stop();

        /// <summary>Run the entrance from the top. Safe to call on an emote that is mid-way through one.</summary>
        public void Play()
        {
            elapsed = 0f;
            Pose(0f);
        }

        /// <summary>Clear every layer back to its resting place and stop the clock.</summary>
        public void Stop()
        {
            elapsed = -1f;
            Pose(flash, 0f, Vector2.zero, Vector2.one, 0f);
            Pose(swirls, 0f, Vector2.zero, Vector2.one, 0f);
            Pose(confetti, 0f, Vector2.zero, Vector2.one, 0f);
            Pose(title, 0f, Vector2.zero, Vector2.one, 0f);
            Pose(pedestal, 0f, Vector2.zero, Vector2.one, 0f);
            Pose(ghost, 0f, Vector2.zero, Vector2.one, 0f);
        }

        void Update()
        {
            if (elapsed < 0f)
            {
                return;
            }

            elapsed += Time.unscaledDeltaTime;
            Pose(elapsed);
        }

        void Pose(float t)
        {
            Pose(flash, FlashAlpha(t), Vector2.zero,
                 Vector2.one * Mathf.Lerp(0.45f, 1.75f, EaseOutCubic(Window(t, 0f, 0.55f))), 0f);

            // The swirl is the light the whole picture stands in: it opens first and widens under the
            // other layers, so the ghost is never standing on nothing.
            float swirlIn = EaseOutCubic(Window(t, 0f, 0.85f));
            float swirlAlpha = Mathf.SmoothStep(0f, 1f, Window(t, 0f, 0.5f));
            float swirlRotation = Mathf.Lerp(-16f, 3f, swirlIn);
            float swirlScale = Mathf.Lerp(0.84f, 1f, swirlIn);

            // Confetti falls into frame a beat later and settles, then keeps drifting.
            float confettiIn = EaseOutCubic(Window(t, 0.06f, 0.9f));
            float confettiOffsetY = Mathf.Lerp(42f, 0f, confettiIn);

            // The title drops in from above and overshoots slightly, which is the difference between a
            // card that appeared and a page that was stamped.
            float titleIn = EaseOutBack(Window(t, 0.12f, 0.42f));
            float titleOffsetY = Mathf.Lerp(96f, 0f, titleIn);

            // The pedestal rises out of the dark first, then the ghost falls onto it.
            float pedestalIn = EaseOutCubic(Window(t, 0f, 0.62f));
            float pedestalOffsetY = Mathf.Lerp(-300f, 0f, pedestalIn);

            float drop = Window(t, 0.36f, 0.34f);
            float ghostOffsetY = Mathf.Lerp(180f, 0f, drop * drop);
            float squash = 1f - Window(t, 0.70f, 0.36f);
            squash *= squash;
            float ghostScaleX = 1f + 0.16f * squash;
            float ghostScaleY = 1f - 0.14f * squash;
            ghostOffsetY -= 4f * squash;

            if (t > EntranceSeconds)
            {
                // The loop: everything at rest still moves, because a static victory screen reads as a
                // screenshot and the run is supposed to be over, not crashed.
                float u = t - EntranceSeconds;
                swirlRotation += 2.2f * Mathf.Sin(u * 0.6f);
                swirlAlpha *= 0.9f + 0.1f * (0.5f + 0.5f * Mathf.Sin(u * 0.9f));
                swirlScale += 0.01f * Mathf.Sin(u * 0.8f);

                confettiOffsetY += 7f * Mathf.Sin(u * 0.7f);

                titleOffsetY += 2f * Mathf.Sin(u * 0.9f);

                ghostOffsetY += 3.5f * Mathf.Sin(u * 1.6f);
            }

            Pose(swirls, swirlAlpha, Vector2.zero, Vector2.one * swirlScale, swirlRotation);
            Pose(confetti, Mathf.SmoothStep(0f, 1f, Window(t, 0.06f, 0.45f)), new Vector2(0f, confettiOffsetY),
                 Vector2.one, Mathf.Lerp(-2.5f, 0f, confettiIn));
            Pose(title, Mathf.SmoothStep(0f, 1f, Window(t, 0.12f, 0.34f)), new Vector2(0f, titleOffsetY),
                 Vector2.one * Mathf.Lerp(1.12f, 1f, titleIn), 0f);
            Pose(pedestal, Mathf.SmoothStep(0f, 1f, Window(t, 0f, 0.30f)),
                 new Vector2(0f, pedestalOffsetY), Vector2.one * Mathf.Lerp(1.03f, 1f, pedestalIn), 0f);
            Pose(ghost, Mathf.SmoothStep(0f, 1f, Window(t, 0.36f, 0.16f)),
                 new Vector2(0f, ghostOffsetY), new Vector2(ghostScaleX, ghostScaleY),
                 Mathf.Sin(t * 2.2f) * 0.8f * (1f - Window(t, 0.36f, 0.34f)));
        }

        static float FlashAlpha(float t) => 1f - Mathf.SmoothStep(0f, 1f, Window(t, 0.02f, 0.5f));

        static float Window(float t, float start, float duration) =>
            Mathf.Clamp01((t - start) / Mathf.Max(0.0001f, duration));

        static float EaseOutCubic(float x)
        {
            x = Mathf.Clamp01(x);
            float inverse = 1f - x;
            return 1f - inverse * inverse * inverse;
        }

        /// <summary>Overshoot then settle — the only curve here that passes its own destination.</summary>
        static float EaseOutBack(float x)
        {
            x = Mathf.Clamp01(x);
            const float c1 = 1.70158f;
            float inverse = x - 1f;
            return 1f + (c1 + 1f) * inverse * inverse * inverse + c1 * inverse * inverse;
        }

        static void Pose(Image layer, float alpha, Vector2 offset, Vector2 scale, float rotation)
        {
            if (layer == null)
            {
                return;
            }

            Color tint = layer.color;
            layer.color = new Color(tint.r, tint.g, tint.b, Mathf.Clamp01(alpha));

            RectTransform rect = layer.rectTransform;
            rect.localPosition = new Vector3(offset.x, offset.y, 0f);
            rect.localRotation = Quaternion.Euler(0f, 0f, rotation);
            rect.localScale = new Vector3(scale.x, scale.y, 1f);
        }
    }
}
