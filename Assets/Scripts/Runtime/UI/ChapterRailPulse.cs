using UnityEngine;
using UnityEngine.UI;

namespace EchoMaze.UI
{
    /// <summary>
    /// The story page's one movement: a pulse of light that walks down the rail to the chapter you chose,
    /// and a ring that opens where it lands.
    ///
    /// It is the game's own vocabulary — a wave leaving a point, hitting something and coming back as a
    /// ring — used as a selection cue instead of an animation bolted on. Between choices the rail breathes
    /// very slowly at the selected node, so the page is never completely still without ever moving enough
    /// to read as loading.
    ///
    /// Everything runs on unscaled time: the page can be opened from a paused game and nothing here waits
    /// on a clock that is stopped.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ChapterRailPulse : MonoBehaviour
    {
        [Tooltip("Screen x of the rail, in the panel's own anchored coordinates. The dot travels down it.")]
        [SerializeField] float railX = -520f;
        [SerializeField] Image travelling;
        [Tooltip("The ring that opens where the pulse lands, and that breathes at rest.")]
        [SerializeField] Image halo;
        [SerializeField] float travelSeconds = 0.4f;
        [SerializeField] float popSeconds = 0.55f;
        [SerializeField] float breathSeconds = 3.2f;

        enum Phase
        {
            Rest,
            Travelling,
            Popping
        }

        Phase phase = Phase.Rest;
        float fromY;
        float toY;
        float elapsed;

        public bool IsTravelling => phase == Phase.Travelling;

        /// <summary>Puts the pulse straight at a node, with no travel: the state the page opens in.</summary>
        public void ResetTo(float y)
        {
            fromY = y;
            toY = y;
            elapsed = 0f;
            phase = Phase.Rest;
            if (travelling != null)
            {
                Color c = travelling.color;
                c.a = 0f;
                travelling.color = c;
            }

            if (halo != null)
            {
                halo.rectTransform.anchoredPosition = new Vector2(railX, y);
            }
        }

        /// <summary>Sends the light from one node to another.</summary>
        public void Play(float startY, float endY)
        {
            fromY = startY;
            toY = endY;
            elapsed = 0f;
            phase = Phase.Travelling;

            if (travelling != null)
            {
                travelling.rectTransform.anchoredPosition = new Vector2(railX, startY);
                Color c = travelling.color;
                c.a = 0f;
                travelling.color = c;
            }

            if (halo != null)
            {
                Color c = halo.color;
                c.a = 0f;
                halo.color = c;
            }
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;

            if (travelling != null && phase != Phase.Travelling)
            {
                Color dim = travelling.color;
                dim.a = 0f;
                travelling.color = dim;
            }

            if (phase == Phase.Travelling)
            {
                elapsed += dt;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.05f, travelSeconds));
                // Cubic out: it leaves the old node slowly and arrives at the new one still moving, which is
                // what a wave does. A linear dot reads as a progress bar.
                float eased = 1f - Mathf.Pow(1f - t, 3f);

                if (travelling != null)
                {
                    travelling.rectTransform.anchoredPosition =
                        new Vector2(railX, Mathf.Lerp(fromY, toY, eased));
                    Color c = travelling.color;
                    c.a = t < 0.15f ? t / 0.15f : t > 0.85f ? (1f - t) / 0.15f : 1f;
                    travelling.color = c;
                }

                if (t >= 1f)
                {
                    phase = Phase.Popping;
                    elapsed = 0f;
                }

                return;
            }

            if (halo == null)
            {
                return;
            }

            if (phase == Phase.Popping)
            {
                elapsed += dt;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.05f, popSeconds));
                halo.rectTransform.anchoredPosition = new Vector2(railX, toY);
                halo.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.7f, 1.8f, t);
                Color c = halo.color;
                c.a = (1f - t) * 0.8f;
                halo.color = c;

                if (t >= 1f)
                {
                    phase = Phase.Rest;
                    elapsed = 0f;
                }

                return;
            }

            // At rest: one slow breath on the node that is chosen, and nothing else on the page moves.
            halo.rectTransform.anchoredPosition = new Vector2(railX, toY);
            float phaseAngle = Time.unscaledTime * (Mathf.PI * 2f / Mathf.Max(0.5f, breathSeconds));
            float breath = 0.5f + 0.5f * Mathf.Sin(phaseAngle);
            halo.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, 1.18f, breath);
            Color resting = halo.color;
            resting.a = Mathf.Lerp(0.05f, 0.13f, breath);
            halo.color = resting;
        }
    }
}
