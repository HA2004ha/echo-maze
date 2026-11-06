using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace EchoMaze.UI
{
    /// <summary>
    /// The field guide: the page that answers "how do I play this" without anybody reading a document
    /// first. Six cards, one per subject, stepped through with two buttons or the arrow keys.
    ///
    /// The words are <see cref="GuidePages"/> and this class only moves pixels, the same split the
    /// calibration page keeps: what a page says and what fits on it are both provable without a canvas.
    ///
    /// It is a child of the menu canvas and it switches itself off when it closes, which is the one panel
    /// pattern allowed to animate its own death: nothing subscribes from a page that only exists while you
    /// are reading it. The rest of the panels live on the canvas and switch a child instead, because they
    /// subscribe to the event that would switch them back on.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GuidePanel : MonoBehaviour
    {
        [Tooltip("Faded on open and on close; the page is only switched off once the fade has landed, the " +
                 "same way the calibration page does it.")]
        [SerializeField] CanvasGroup group;
        [SerializeField] Text titleLabel;
        [SerializeField] Text bodyLabel;
        [SerializeField] Text counterLabel;
        [SerializeField] GuideDiagram diagram;
        [SerializeField] Button prevButton;
        [SerializeField] Button nextButton;
        [SerializeField] Button closeButton;
        [SerializeField] float fadeSeconds = 0.22f;

        Coroutine fadeRun;
        bool closing;
        int page;

        void Awake()
        {
            if (prevButton != null)
            {
                prevButton.onClick.AddListener(Previous);
            }

            if (nextButton != null)
            {
                nextButton.onClick.AddListener(Next);
            }

            if (closeButton != null)
            {
                closeButton.onClick.AddListener(Close);
            }
        }

        void OnEnable()
        {
            closing = false;
            Render();

            if (group == null)
            {
                return;
            }

            if (fadeRun != null)
            {
                StopCoroutine(fadeRun);
            }

            group.alpha = 0f;
            group.interactable = true;
            group.blocksRaycasts = true;
            fadeRun = StartCoroutine(Fade(0f, 1f, false));
        }

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame)
            {
                Previous();
            }
            else if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame)
            {
                Next();
            }
            else if (keyboard.escapeKey.wasPressedThisFrame)
            {
                Close();
            }
        }

        /// <summary>Open at a named page. Used by the menu button and by the terminal capture hook.</summary>
        public void Open(int index = 0)
        {
            page = GuidePages.Clamp(index);
            if (!gameObject.activeSelf)
            {
                // OnEnable renders, so a page the caller asked for has to be stored before the wake-up.
                gameObject.SetActive(true);
                return;
            }

            Render();
        }

        public void Next()
        {
            page = GuidePages.Next(page);
            Render();
        }

        public void Previous()
        {
            page = GuidePages.Previous(page);
            Render();
        }

        void Render()
        {
            GuidePage current = GuidePages.At(page);
            if (diagram != null) diagram.ShowPage(page);
            if (titleLabel != null)
            {
                titleLabel.text = current.Title;
            }

            if (bodyLabel != null)
            {
                bodyLabel.text = current.Body;
            }

            if (counterLabel != null)
            {
                counterLabel.text = GuidePages.Counter(page);
            }
        }

        public void Close()
        {
            if (group == null || closing)
            {
                gameObject.SetActive(false);
                return;
            }

            closing = true;
            group.blocksRaycasts = false;
            group.interactable = false;
            fadeRun = StartCoroutine(Fade(group.alpha, 0f, true));
        }

        IEnumerator Fade(float from, float to, bool disableAtEnd)
        {
            float seconds = Mathf.Max(0.05f, fadeSeconds);
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / seconds;
                group.alpha = Mathf.Lerp(from, to, to > from ? 1f - (1f - t) * (1f - t) : t * t);
                yield return null;
            }

            group.alpha = to;
            if (disableAtEnd)
            {
                gameObject.SetActive(false);
            }
        }
    }
}
