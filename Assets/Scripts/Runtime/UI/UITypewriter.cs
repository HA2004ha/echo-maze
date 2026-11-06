using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace EchoMaze.UI
{
    /// <summary>
    /// Types a line out instead of printing it, used on the victory summary: a page of results arriving
    /// one character at a time reads as the machine reporting to you, which is the fiction the whole
    /// interface is built on. Speed is in characters per second on unscaled time, because the moment it
    /// runs is the moment the game clock stopped.
    ///
    /// The label must use the project's monospace font: proportional fonts make every keystroke shift
    /// the following characters, which turns a typewriter into a seizure.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UITypewriter : MonoBehaviour
    {
        [SerializeField] Text label;
        [SerializeField] float charactersPerSecond = 38f;

        Coroutine running;

        public void Attach(Text target)
        {
            label = target;
        }

        /// <summary>Restart the reveal with new text; calling it again mid-line simply retypes.</summary>
        public void Play(string text)
        {
            if (label == null)
            {
                return;
            }

            if (running != null)
            {
                StopCoroutine(running);
            }

            running = StartCoroutine(Type(text ?? string.Empty));
        }

        /// <summary>Stop typing and put the whole line up at once, for callers that cannot wait.</summary>
        public void Complete(string text)
        {
            if (running != null)
            {
                StopCoroutine(running);
                running = null;
            }

            if (label != null)
            {
                label.text = text ?? string.Empty;
            }
        }

        IEnumerator Type(string text)
        {
            float perCharacter = 1f / Mathf.Max(1f, charactersPerSecond);
            float accumulator = 0f;
            int shown = 0;
            label.text = string.Empty;

            while (shown < text.Length)
            {
                accumulator += Time.unscaledDeltaTime;
                while (accumulator >= perCharacter && shown < text.Length)
                {
                    accumulator -= perCharacter;
                    shown++;
                }

                label.text = text.Substring(0, shown);
                yield return null;
            }

            running = null;
        }
    }
}
