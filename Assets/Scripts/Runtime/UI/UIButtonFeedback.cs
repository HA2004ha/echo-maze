using EchoMaze.Audio;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace EchoMaze.UI
{
    /// <summary>
    /// What a button does with its hands: grows a little and lights its frame when the pointer is over
    /// it or when the keyboard/gamepad navigation lands on it. The colour swap the generator already
    /// applies is a <see cref="Button.ButtonColorBlock"/>, which only speaks while the pointer is
    /// pressed; this covers the moment before the press, which is the moment a player is actually
    /// deciding.
    ///
    /// It sits on the same object as the Button rather than reaching for one, and it writes scale and
    /// frame alpha only — never colour — so the two effects stack instead of fighting over the same
    /// pixels.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UIButtonFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
                                                    ISelectHandler, IDeselectHandler,
                                                    IPointerDownHandler, ISubmitHandler
    {
        [Tooltip("The frame to light. Left empty, Awake takes the first Image on this object, which is " +
                 "what every generated button has.")]
        [SerializeField] Image frame;

        [SerializeField] float hoverScale = 1.04f;
        [SerializeField] float settleSeconds = 0.09f;
        [Tooltip("The colour the frame slides toward when it is lit. Adding alpha instead would do " +
                 "nothing: the idle frame is already at full alpha, so the only brightening a 9-slice " +
                 "line frame can take is toward a whiter cyan.")]
        [SerializeField] Color glowColor = new Color(0.85f, 1f, 1f, 1f);
        [SerializeField, Range(0f, 1f)] float glowStrength = 0.8f;

        Vector3 baseScale;
        Color baseColor;
        float value;
        bool hovered;
        bool selected;
        bool tracking;
        float lastHoverSound;

        void Awake()
        {
            if (frame == null)
            {
                frame = GetComponent<Image>();
            }

            baseScale = transform.localScale;
            if (frame != null)
            {
                // The idle colour of the button's own ColorBlock, not the frame's current colour: the
                // Selectable writes that block into the same Image at every state change, and the two
                // writers only agree if this one brightens the same value the other one rests on.
                Button button = GetComponent<Button>();
                baseColor = button != null ? button.colors.normalColor : frame.color;
            }
        }

        void Update()
        {
            if (!tracking)
            {
                return;
            }

            float target = (hovered || selected) ? 1f : 0f;
            float step = Time.unscaledDeltaTime / Mathf.Max(0.01f, settleSeconds);
            value = value < target ? Mathf.Min(target, value + step) : Mathf.Max(target, value - step);

            transform.localScale = Vector3.Lerp(baseScale, baseScale * hoverScale, value);
            if (frame != null)
            {
                frame.color = Color.Lerp(baseColor, glowColor, value * glowStrength);
            }

            if (value == target)
            {
                tracking = false;
                if (value == 0f)
                {
                    transform.localScale = baseScale;
                    if (frame != null)
                    {
                        frame.color = baseColor;
                    }
                }
            }
        }

        void BeginTracking()
        {
            // The value is continuous and both effects are lerps against it, so an interrupt mid-ramp
            // simply reverses the same value — the resting scale from Awake stays the base forever.
            tracking = true;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            HoverSound();
            hovered = true;
            BeginTracking();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovered = false;
            BeginTracking();
        }

        public void OnSelect(BaseEventData eventData)
        {
            HoverSound();
            selected = true;
            BeginTracking();
        }

        public void OnPointerDown(PointerEventData eventData) => SoundBoard.Play(SoundId.UiClick, 0.7f);

        public void OnSubmit(BaseEventData eventData) => SoundBoard.Play(SoundId.UiClick, 0.7f);

        /// <summary>
        /// A pointer entering a selected button fires both handlers for one arrival, so the sound is rate
        /// limited rather than left to double up. 80 ms is under the ear's ability to hear two ticks.
        /// </summary>
        void HoverSound()
        {
            if (Time.unscaledTime - lastHoverSound < 0.08f)
            {
                return;
            }

            lastHoverSound = Time.unscaledTime;
            SoundBoard.Play(SoundId.UiHover, 0.45f);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            selected = false;
            BeginTracking();
        }
    }
}
