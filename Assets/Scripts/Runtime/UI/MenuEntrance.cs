using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace EchoMaze.UI
{
    /// <summary>
    /// The menu arriving rather than being there: each element listed here fades in and slides the last
    /// few dozen pixels into place, on a stagger, once at Start. Elements are ordered the way the eye
    /// should read them — title, subtitle, then the buttons top to bottom — so the page assembles itself
    /// in the order you would have written it.
    ///
    /// Every element gets its own CanvasGroup, added here if it has none. The generators re-assert those
    /// groups at alpha one on every rebuild, so a scene regenerated from this code is never left holding
    /// an invisible menu by a fade that had not finished when the editor saved.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MenuEntrance : MonoBehaviour
    {
        [Tooltip("Top to bottom, the order the page writes itself in.")]
        [SerializeField] RectTransform[] elements;
        [SerializeField] float firstDelaySeconds = 0.15f;
        [SerializeField] float perItemDelay = 0.12f;
        [SerializeField] float itemSeconds = 0.5f;
        [Tooltip("How far below its resting place each element starts, in its own local units.")]
        [SerializeField] float slideDistance = 40f;

        readonly List<CanvasGroup> groups = new List<CanvasGroup>();
        readonly List<Vector3> restPositions = new List<Vector3>();

        void Start()
        {
            if (elements == null || elements.Length == 0)
            {
                enabled = false;
                return;
            }

            groups.Clear();
            restPositions.Clear();
            foreach (RectTransform element in elements)
            {
                if (element == null)
                {
                    groups.Add(null);
                    restPositions.Add(Vector3.zero);
                    continue;
                }

                CanvasGroup group = element.GetComponent<CanvasGroup>();
                if (group == null)
                {
                    group = element.gameObject.AddComponent<CanvasGroup>();
                }

                group.alpha = 0f;
                group.interactable = false;
                group.blocksRaycasts = false;
                groups.Add(group);
                restPositions.Add(element.localPosition);
            }

            StartCoroutine(Reveal());
        }

        IEnumerator Reveal()
        {
            // Staggered starts, overlapping ramps: a sequential reveal of seven items takes seconds and
            // the page trickles in instead of arriving. Each element begins its own ramp a beat after the
            // last, so the whole entrance is over in firstDelay + n*perItemDelay + itemSeconds.
            for (int i = 0; i < groups.Count; i++)
            {
                if (groups[i] != null)
                {
                    StartCoroutine(Item(i, firstDelaySeconds + i * perItemDelay));
                }
            }

            yield break;
        }

        IEnumerator Item(int index, float startDelay)
        {
            if (startDelay > 0f)
            {
                yield return new WaitForSecondsRealtime(startDelay);
            }

            CanvasGroup group = groups[index];
            RectTransform element = elements[index];
            Vector3 rest = restPositions[index];
            float elapsed = 0f;
            Vector3 down = rest - new Vector3(0f, slideDistance, 0f);

            while (elapsed < itemSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / itemSeconds);
                float eased = 1f - Mathf.Pow(1f - t, 2f);
                group.alpha = eased;
                element.localPosition = Vector3.Lerp(down, rest, eased);
                yield return null;
            }

            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
            element.localPosition = rest;
        }
    }
}
