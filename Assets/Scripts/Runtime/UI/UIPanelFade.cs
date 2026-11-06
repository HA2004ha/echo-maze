using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace EchoMaze.UI
{
    /// <summary>
    /// Fades a panel's <see cref="CanvasGroup"/> in and out instead of switching it on and off, so a
    /// page arriving is a page you watch arrive. The component lives on the always-active canvas object,
    /// never on the fading child: a fade that runs inside the thing it is fading dies halfway when the
    /// generator later deactivates that child, and a half-faded panel that never finished disappearing
    /// still swallows clicks.
    ///
    /// While the panel is hidden the group is left interactable but invisible only for the duration of
    /// the fade-out; at rest both alpha and raycasts are at zero, so an off-screen pause page cannot be
    /// clicked through its ghost.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UIPanelFade : MonoBehaviour
    {
        [SerializeField] CanvasGroup group;
        [SerializeField] float inSeconds = 0.32f;
        [SerializeField] float outSeconds = 0.2f;
        [Tooltip("For screens like the death notice that are switched on wholesale rather than driven: " +
                 "arriving enabled means arriving by fade.")]
        [SerializeField] bool playOnEnable;
        [Tooltip("Ease curve exponent. One is linear; two takes the long way in and the short way out, " +
                 "which reads as settling rather than as a timer.")]
        [SerializeField] float ease = 2f;

        Coroutine running;

        void OnEnable()
        {
            if (playOnEnable && group != null)
            {
                group.alpha = 0f;
                Show();
            }
        }

        public void Attach(CanvasGroup target)
        {
            group = target;
        }

        /// <summary>Show the panel, ramping from wherever it currently is.</summary>
        public void Show()
        {
            if (group == null)
            {
                return;
            }

            if (running != null)
            {
                StopCoroutine(running);
            }

            group.alpha = Mathf.Clamp01(group.alpha);
            group.interactable = true;
            group.blocksRaycasts = true;
            // Duration scales with the distance left to travel, so an interrupted fade-out that is
            // shown again resumes at the right speed instead of crawling the last few percent.
            running = StartCoroutine(Ramp(group.alpha, 1f,
                                          Mathf.Max(0.01f, inSeconds * (1f - group.alpha))));
        }

        /// <summary>Hide the panel. The object stays active; only the group goes to zero.</summary>
        public void Hide()
        {
            if (group == null)
            {
                return;
            }

            if (running != null)
            {
                StopCoroutine(running);
            }

            running = StartCoroutine(Ramp(group.alpha, 0f, Mathf.Max(0.01f, outSeconds * group.alpha)));
        }

        /// <summary>
        /// Hidden at once, no ramp: the state a panel is born in. A level that loaded with the pause page
        /// fading out behind it is a level showing a panel nobody asked for.
        /// </summary>
        public void HideImmediate()
        {
            if (group == null)
            {
                return;
            }

            if (running != null)
            {
                StopCoroutine(running);
                running = null;
            }

            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }

        IEnumerator Ramp(float from, float to, float seconds)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / seconds);
                float eased = to > from ? 1f - Mathf.Pow(1f - t, ease) : Mathf.Pow(t, ease);
                group.alpha = Mathf.Lerp(from, to, eased);
                yield return null;
            }

            group.alpha = to;
            if (to <= 0f)
            {
                group.interactable = false;
                group.blocksRaycasts = false;
            }

            running = null;
        }
    }
}
