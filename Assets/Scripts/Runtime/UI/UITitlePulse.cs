using UnityEngine;
using UnityEngine.UI;

namespace EchoMaze.UI
{
    /// <summary>
    /// The title's rings breathe: three concentric circles drifting outward on offset phases, the way a
    /// sonar scope idles between pings. The generator used to paint them at fixed scales and fixed
    /// alphas, which on a menu that never moves read as furniture; this is the same geometry asking a
    /// question instead of answering one.
    ///
    /// Everything is driven from unscaled time in Update — one sine per ring, phase-shifted by its slot —
    /// because a menu is exactly the screen where the clock may be paused, rewound, or stepped by the
    /// debug window, and the room must keep breathing through all of it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UITitlePulse : MonoBehaviour
    {
        [Tooltip("Outermost first, the order the generator stacks them.")]
        [SerializeField] Image[] rings;
        [Tooltip("Optional: the title's Shadow component, whose glow breathes with the rings.")]
        [SerializeField] Shadow headingShadow;

        [SerializeField] float periodSeconds = 4f;
        [SerializeField] float minScale = 0.92f;
        [SerializeField] float maxScale = 1.25f;
        [Tooltip("The alpha swings between these fractions of whatever the generator painted on each " +
                 "ring, so the outer rings stay fainter than the inner ones through the whole breath.")]
        [SerializeField] float minAlphaFraction = 0.35f;
        [SerializeField] float maxAlphaFraction = 1.35f;
        [SerializeField] float shadowMinAlpha = 0.15f;
        [SerializeField] float shadowMaxAlpha = 0.55f;

        Vector3[] baseScales;
        Color[] baseColors;

        void Awake()
        {
            int count = rings != null ? rings.Length : 0;
            baseScales = new Vector3[count];
            baseColors = new Color[count];
            for (int i = 0; i < count; i++)
            {
                if (rings[i] != null)
                {
                    baseScales[i] = rings[i].transform.localScale;
                    baseColors[i] = rings[i].color;
                }
            }
        }

        void Update()
        {
            float phase = Time.unscaledTime / Mathf.Max(0.01f, periodSeconds) * Mathf.PI * 2f;

            if (rings != null)
            {
                for (int i = 0; i < rings.Length; i++)
                {
                    Image ring = rings[i];
                    if (ring == null)
                    {
                        continue;
                    }

                    // Each ring lags the previous one by a third of the sweep, so the crest travels
                    // outward instead of the whole set pumping together.
                    float wave = 0.5f + 0.5f * Mathf.Sin(phase - i * Mathf.PI / 3f);
                    ring.transform.localScale = baseScales[i] * Mathf.Lerp(minScale, maxScale, wave);
                    Color color = baseColors[i];
                    color.a = baseColors[i].a * Mathf.Lerp(minAlphaFraction, maxAlphaFraction, wave);
                    ring.color = color;
                }
            }

            if (headingShadow != null)
            {
                float breath = 0.5f + 0.5f * Mathf.Sin(phase * 0.5f);
                Color glow = headingShadow.effectColor;
                glow.a = Mathf.Lerp(shadowMinAlpha, shadowMaxAlpha, breath);
                headingShadow.effectColor = glow;
            }
        }
    }
}
