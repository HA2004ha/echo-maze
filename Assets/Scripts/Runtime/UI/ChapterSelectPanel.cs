using System.Collections;
using System.Collections.Generic;
using EchoMaze.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace EchoMaze.UI
{
    /// <summary>
    /// The story page: every chapter as a node on one descending thread, with the rooms already survived
    /// marked complete and their best run stamped beside them, the next room open, and everything past it
    /// sealed. The last node is the finale the build does not contain — drawn as part of the thread, never
    /// as a button, so the page never promises a room that does not exist.
    ///
    /// It reads <see cref="GameFlow"/> for the order and <see cref="ChapterProgress"/> for the record, and
    /// owns no state of its own beyond which fade is running: opening the page twice is the same page.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ChapterSelectPanel : MonoBehaviour
    {
        /// <summary>How many rows the generated page has. Chapters past this are not shown.</summary>
        public const int Capacity = 3;

        [SerializeField] CanvasGroup group;
        [Tooltip("The line under the heading: a fixed invitation, or the quiet line once everything is walked.")]
        [SerializeField] Text subtitleLabel;
        [Tooltip("\"2 of 2 chapters walked\" — the page's own scoreboard.")]
        [SerializeField] Text statusLabel;

        [Header("One entry per row, wired by the scene builder")]
        [SerializeField] Button[] rowButtons = new Button[0];
        [SerializeField] Text[] rowNumerals = new Text[0];
        [SerializeField] Text[] rowTitles = new Text[0];
        [SerializeField] Text[] rowSubtitles = new Text[0];
        [SerializeField] Text[] rowStates = new Text[0];
        [SerializeField] Text[] rowRecords = new Text[0];
        [SerializeField] Image[] rowMarkers = new Image[0];
        [SerializeField] Button backButton;
        [SerializeField] float fadeSeconds = 0.22f;

        // The three states, as colour: walked, waiting, and not this build's to give.
        static readonly Color OpenTint = new Color(0.85f, 0.97f, 1f, 1f);
        static readonly Color CompleteTint = new Color(0.50f, 0.91f, 0.94f, 1f);
        static readonly Color SealedTint = new Color(0.42f, 0.49f, 0.55f, 1f);

        Coroutine fadeRun;
        bool closing;
        int defaultRow;

        void Awake()
        {
            if (backButton != null)
            {
                backButton.onClick.AddListener(Close);
            }

            if (rowButtons != null)
            {
                for (int i = 0; i < rowButtons.Length; i++)
                {
                    if (rowButtons[i] == null)
                    {
                        continue;
                    }

                    // Copied per iteration: a listener that closes over the loop variable is the same
                    // listener on every row, and every row would then start the last chapter.
                    int index = i;
                    rowButtons[i].onClick.AddListener(() => Play(index));
                }
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
            FocusDefault();
        }

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                Close();
            }
            else if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame)
            {
                Step(-1);
            }
            else if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame)
            {
                Step(1);
            }
        }

        /// <summary>Opens the page. Called by the menu, which knows nothing about rows.</summary>
        public void Open()
        {
            if (gameObject.activeSelf)
            {
                Render();
                return;
            }

            gameObject.SetActive(true);
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

        /// <summary>Starts a row's chapter. Refuses sealed rows even if something calls it directly.</summary>
        public void Play(int index)
        {
            GameFlow flow = GameFlow.Active;
            if (flow == null || index < 0 || index >= flow.ChapterCount || !flow.IsUnlocked(index))
            {
                return;
            }

            closing = true;
            flow.StartChapter(index);
        }

        /// <summary>
        /// Writes every row from the flow and the saved progress. One pass, so the page can never show a
        /// node as open while its own button underneath still thinks it is sealed.
        /// </summary>
        public void Render()
        {
            GameFlow flow = GameFlow.Active;
            int chapters = flow != null ? flow.ChapterCount : 0;
            defaultRow = -1;
            bool openRowSeen = false;

            for (int i = 0; i < Capacity; i++)
            {
                if (rowButtons == null || i >= rowButtons.Length || rowButtons[i] == null)
                {
                    continue;
                }

                Button row = rowButtons[i];
                bool isChapter = i < chapters;
                bool isEpilogue = !isChapter && i == chapters;
                bool show = isChapter || isEpilogue;
                row.gameObject.SetActive(show);
                if (!show)
                {
                    continue;
                }

                string scene = isChapter ? flow.ChapterName(i) : "";
                ChapterStory story = ChapterCatalog.At(i, scene);
                ChapterProgress.Record record = isChapter ? ChapterProgress.Read(scene) : default;
                bool complete = isChapter && record.Complete;
                bool unlocked = isChapter && flow.IsUnlocked(i);
                bool playable = isChapter && unlocked;

                string numeral = isEpilogue ? ChapterCatalog.EpilogueNumeral : story.Numeral;
                string title = isEpilogue ? ChapterCatalog.EpilogueTitle : story.Title;
                string subtitle = isEpilogue ? ChapterCatalog.EpilogueSubtitle
                    : complete && !string.IsNullOrEmpty(story.Debrief) ? story.Debrief : story.Subtitle;

                Color tint;
                string state;
                string detail;
                if (isEpilogue)
                {
                    tint = SealedTint;
                    state = "SEALED";
                    detail = ChapterCatalog.EpilogueRecord;
                }
                else if (complete)
                {
                    tint = CompleteTint;
                    state = "COMPLETE";
                    detail = RecordLine(record);
                }
                else if (unlocked)
                {
                    tint = OpenTint;
                    state = "OPEN";
                    detail = "not yet walked";
                }
                else
                {
                    tint = SealedTint;
                    state = "SEALED";
                    detail = "finish chapter " + ChapterCatalog.Numeral(i - 1, flow.ChapterName(i - 1)) + " to open";
                }

                Set(rowNumerals, i, numeral, tint);
                Set(rowTitles, i, title, Color.Lerp(tint, Color.white, playable ? 0.25f : 0f));
                Set(rowSubtitles, i, subtitle, new Color(SealedTint.r, SealedTint.g, SealedTint.b, playable ? 0.85f : 0.55f));
                Set(rowStates, i, state, tint);
                Set(rowRecords, i, detail, new Color(tint.r, tint.g, tint.b, 0.62f));

                if (rowMarkers != null && i < rowMarkers.Length && rowMarkers[i] != null)
                {
                    rowMarkers[i].color = new Color(tint.r, tint.g, tint.b, playable || complete ? 0.9f : 0.45f);
                }

                row.interactable = playable;

                // The hover grow is a promise that the row does something; a sealed row must not make it.
                var feedback = row.GetComponent<UIButtonFeedback>();
                if (feedback != null)
                {
                    feedback.enabled = playable;
                }

                if (playable)
                {
                    if (!openRowSeen && !complete)
                    {
                        openRowSeen = true;
                        defaultRow = i;
                    }
                    else if (defaultRow < 0)
                    {
                        defaultRow = i;
                    }
                }
            }

            int walked = flow != null ? flow.ChaptersWalked : 0;
            if (statusLabel != null)
            {
                statusLabel.text = chapters == 0
                    ? "the story has not been loaded"
                    : walked + " of " + chapters + (chapters == 1 ? " chapter walked" : " chapters walked");
            }

            if (subtitleLabel != null)
            {
                subtitleLabel.text = chapters > 0 && walked >= chapters
                    ? "the facility is quiet. for now."
                    : "what you have walked, and what is still sealed";
            }
        }

        /// <summary>The record stamped on a walked node: a best, not a latest.</summary>
        static string RecordLine(ChapterProgress.Record record) =>
            record.HasBest
                ? VictoryPanel.FormatTime(record.Seconds) + "  ·  " + record.Waves + " waves  ·  " +
                  record.Retries + (record.Retries == 1 ? " retry" : " retries")
                : "walked";

        static void Set(Text[] labels, int index, string value, Color color)
        {
            if (labels == null || index >= labels.Length || labels[index] == null)
            {
                return;
            }

            labels[index].text = value;
            labels[index].color = color;
        }

        void FocusDefault()
        {
            if (rowButtons == null || defaultRow < 0 || defaultRow >= rowButtons.Length ||
                rowButtons[defaultRow] == null || EventSystem.current == null)
            {
                return;
            }

            EventSystem.current.SetSelectedGameObject(rowButtons[defaultRow].gameObject);
        }

        /// <summary>
        /// Up and down inside the thread, skipping sealed rows: a keyboard or pad user should never be able
        /// to land on a button that refuses them.
        /// </summary>
        void Step(int delta)
        {
            if (rowButtons == null)
            {
                return;
            }

            var playable = new List<int>();
            for (int i = 0; i < rowButtons.Length; i++)
            {
                if (rowButtons[i] != null && rowButtons[i].gameObject.activeSelf && rowButtons[i].interactable)
                {
                    playable.Add(i);
                }
            }

            if (playable.Count == 0 || EventSystem.current == null)
            {
                return;
            }

            GameObject selected = EventSystem.current.currentSelectedGameObject;
            int at = -1;
            for (int i = 0; i < playable.Count; i++)
            {
                if (rowButtons[playable[i]].gameObject == selected)
                {
                    at = i;
                }
            }

            int next = at < 0
                ? (delta > 0 ? 0 : playable.Count - 1)
                : ((at + delta) % playable.Count + playable.Count) % playable.Count;
            EventSystem.current.SetSelectedGameObject(rowButtons[playable[next]].gameObject);
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
