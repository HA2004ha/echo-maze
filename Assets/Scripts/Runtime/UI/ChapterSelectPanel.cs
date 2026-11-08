using System.Collections;
using EchoMaze.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace EchoMaze.UI
{
    /// <summary>
    /// The story page: the whole run as one rail of chapter nodes, and the selected node read out beside it
    /// — its name, how hard it is, what shares it, the sentence that leads into it, and the record of the
    /// best time it has been walked in.
    ///
    /// A rail rather than a list because the story is ten rooms long: ten rows would not fit on one page, and
    /// a scrolling page would break the one thing this screen is for, which is seeing the whole descent at
    /// once. Sealed nodes can be selected and read — the story is a reason to keep playing — but only an
    /// open one can be started.
    ///
    /// It owns no state beyond the selection: the order comes from <see cref="GameFlow"/>, the prose and the
    /// difficulty from <see cref="ChapterCatalog"/>, and the record from <see cref="ChapterProgress"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ChapterSelectPanel : MonoBehaviour
    {
        [SerializeField] CanvasGroup group;
        [Tooltip("The line under the heading: a fixed invitation, or the quiet line once everything is walked.")]
        [SerializeField] Text subtitleLabel;
        [Tooltip("\"3 of 10 chapters walked\" — the page's own scoreboard.")]
        [SerializeField] Text statusLabel;

        [Header("The rail, one node per chapter, wired by the scene builder")]
        [SerializeField] Button[] railButtons = new Button[0];
        [SerializeField] Text[] railNumerals = new Text[0];
        [SerializeField] Image[] railMarkers = new Image[0];

        [Header("The dossier of the selected node")]
        [SerializeField] CanvasGroup dossierGroup;
        [SerializeField] Text dossierNumeral;
        [SerializeField] Text dossierTitle;
        [SerializeField] Text dossierGrade;
        [SerializeField] Text dossierBody;
        [SerializeField] Text dossierThread;
        [SerializeField] Text dossierRecord;
        [SerializeField] Button playButton;
        [SerializeField] Text playLabel;
        [SerializeField] Button backButton;
        [SerializeField] ChapterRailPulse pulse;

        [SerializeField] float fadeSeconds = 0.22f;
        [SerializeField] float dossierFadeSeconds = 0.14f;

        // The three states, as colour: walked, waiting, and not yet earned.
        static readonly Color OpenTint = new Color(0.85f, 0.97f, 1f, 1f);
        static readonly Color CompleteTint = new Color(0.50f, 0.91f, 0.94f, 1f);
        static readonly Color SealedTint = new Color(0.42f, 0.49f, 0.55f, 1f);
        static readonly Color BodyTint = new Color(0.72f, 0.84f, 0.88f, 1f);
        static readonly Color QuoteTint = new Color(0.62f, 0.72f, 0.77f, 1f);

        Coroutine fadeRun;
        Coroutine dossierRun;
        bool closing;
        int selection;

        /// <summary>The node the page is reading out, exposed so a test can assert the page moves.</summary>
        public int Selection => selection;

        void Awake()
        {
            if (backButton != null)
            {
                backButton.onClick.AddListener(Close);
            }

            if (playButton != null)
            {
                playButton.onClick.AddListener(Play);
            }

            if (railButtons != null)
            {
                for (int i = 0; i < railButtons.Length; i++)
                {
                    if (railButtons[i] == null)
                    {
                        continue;
                    }

                    // Copied per iteration: a listener closing over the loop variable is the same listener
                    // on every node, and every node would then open the last chapter.
                    int index = i;
                    railButtons[i].onClick.AddListener(() => Select(index, true));
                }
            }
        }

        void OnEnable()
        {
            closing = false;
            selection = OpeningNode();
            Render();

            if (group != null)
            {
                if (fadeRun != null)
                {
                    StopCoroutine(fadeRun);
                }

                group.alpha = 0f;
                group.interactable = true;
                group.blocksRaycasts = true;
                fadeRun = StartCoroutine(Fade(0f, 1f, false));
            }

            Focus();
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
                Select(selection - 1, true);
            }
            else if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame)
            {
                Select(selection + 1, true);
            }
            else if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
            {
                Play();
            }
        }

        /// <summary>Opens the page. Called by the menu, which knows nothing about nodes.</summary>
        public void Open()
        {
            if (gameObject.activeSelf)
            {
                selection = OpeningNode();
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

        /// <summary>Moves the page to a node and walks the pulse down the rail to it.</summary>
        public void Select(int index, bool animate)
        {
            int count = railButtons != null ? railButtons.Length : 0;
            if (count == 0)
            {
                return;
            }

            int next = ((index % count) + count) % count;
            int previous = selection;
            selection = next;

            RenderRail();
            RenderDossier();
            Focus();

            if (pulse != null)
            {
                if (animate && previous != next)
                {
                    pulse.Play(RailY(previous), RailY(next));
                }
                else
                {
                    pulse.ResetTo(RailY(next));
                }
            }

            FadeDossier();
        }

        /// <summary>Starts the selected chapter. Refuses a sealed node even if something calls it directly.</summary>
        public void Play()
        {
            GameFlow flow = GameFlow.Active;
            if (flow == null || selection < 0 || selection >= flow.ChapterCount || !flow.IsUnlocked(selection))
            {
                return;
            }

            closing = true;
            flow.StartChapter(selection);
        }

        /// <summary>
        /// Writes the rail and the dossier from the flow and the saved progress. One pass, so the page can
        /// never show a node as open while the button beside it still thinks it is sealed.
        /// </summary>
        public void Render()
        {
            RenderRail();
            RenderDossier();
            if (pulse != null)
            {
                pulse.ResetTo(RailY(selection));
            }

            Focus();
        }

        void RenderRail()
        {
            GameFlow flow = GameFlow.Active;
            if (railButtons == null)
            {
                return;
            }

            for (int i = 0; i < railButtons.Length; i++)
            {
                if (railButtons[i] == null)
                {
                    continue;
                }

                bool exists = flow != null && i < flow.ChapterCount;
                railButtons[i].gameObject.SetActive(exists);
                if (!exists)
                {
                    continue;
                }

                bool complete = flow.IsChapterComplete(i);
                bool unlocked = flow.IsUnlocked(i);
                Color tint = complete ? CompleteTint : unlocked ? OpenTint : SealedTint;
                bool chosen = i == selection;

                Color numeral = chosen ? Color.Lerp(tint, Color.white, 0.45f) : tint;
                Set(railNumerals, i, ChapterCatalog.Numeral(i, flow.ChapterName(i)), numeral);

                if (railMarkers != null && i < railMarkers.Length && railMarkers[i] != null)
                {
                    float alpha = chosen ? 1f : complete || unlocked ? 0.55f : 0.28f;
                    railMarkers[i].color = new Color(tint.r, tint.g, tint.b, alpha);
                }
            }
        }

        void RenderDossier()
        {
            GameFlow flow = GameFlow.Active;
            int count = railButtons != null ? railButtons.Length : 0;
            if (flow == null || count == 0)
            {
                return;
            }

            int index = Mathf.Clamp(selection, 0, Mathf.Min(count, flow.ChapterCount) - 1);
            string scene = flow.ChapterName(index);
            ChapterStory story = ChapterCatalog.At(index, scene);
            ChapterProgress.Record record = ChapterProgress.Read(scene);

            bool complete = record.Complete;
            bool unlocked = flow.IsUnlocked(index);
            bool last = index == flow.ChapterCount - 1;
            Color tint = complete ? CompleteTint : unlocked ? OpenTint : SealedTint;

            if (dossierNumeral != null)
            {
                dossierNumeral.text = "chapter " + ChapterCatalog.Numeral(index, scene).ToLowerInvariant();
                dossierNumeral.color = new Color(tint.r, tint.g, tint.b, 0.85f);
            }

            if (dossierTitle != null)
            {
                dossierTitle.text = story.Title;
                dossierTitle.color = Color.Lerp(tint, Color.white, unlocked ? 0.35f : 0.12f);
            }

            if (dossierGrade != null)
            {
                string threat = string.IsNullOrEmpty(story.Threat) ? "" : "  ·  " + story.Threat;
                dossierGrade.text = story.Grade.ToUpperInvariant() + threat;
                dossierGrade.color = new Color(tint.r, tint.g, tint.b, unlocked ? 0.8f : 0.55f);
            }

            if (dossierBody != null)
            {
                dossierBody.text = complete && !string.IsNullOrEmpty(story.Debrief) ? story.Debrief : story.Subtitle;
                dossierBody.color = BodyTint;
            }

            if (dossierThread != null)
            {
                dossierThread.text = story.Thread;
                dossierThread.color = QuoteTint;
            }

            if (dossierRecord != null)
            {
                dossierRecord.text = RecordLine(record, unlocked, index, flow, last);
                dossierRecord.color = new Color(tint.r, tint.g, tint.b, 0.7f);
            }

            if (playButton != null)
            {
                playButton.interactable = unlocked;
                var feedback = playButton.GetComponent<UIButtonFeedback>();
                if (feedback != null)
                {
                    feedback.enabled = unlocked;
                }
            }

            if (playLabel != null)
            {
                playLabel.text = unlocked ? (complete ? "walk it again" : "walk this room") : "sealed";
            }

            int walked = flow.ChaptersWalked;
            if (subtitleLabel != null)
            {
                subtitleLabel.text = walked >= flow.ChapterCount
                    ? ChapterCatalog.FinalWord
                    : "what you have walked, and what is still sealed";
            }

            if (statusLabel != null)
            {
                statusLabel.text = walked + " of " + flow.ChapterCount +
                                   (flow.ChapterCount == 1 ? " chapter walked" : " chapters walked") +
                                   (last && walked >= flow.ChapterCount ? "  ·  the facility ends here" : "");
            }
        }

        /// <summary>The record stamped beside a node: a best, not a latest.</summary>
        static string RecordLine(ChapterProgress.Record record, bool unlocked, int index, GameFlow flow, bool last)
        {
            if (record.Complete)
            {
                if (!record.HasBest)
                {
                    return "walked";
                }

                return "best  " + VictoryPanel.FormatTime(record.Seconds) + "   ·   " + record.Waves + " waves" +
                       "   ·   " + record.Retries + (record.Retries == 1 ? " retry" : " retries");
            }

            if (unlocked)
            {
                return "not yet walked";
            }

            string gate = index > 0
                ? "finish chapter " + ChapterCatalog.Numeral(index - 1, flow.ChapterName(index - 1)) + " to open"
                : "sealed";
            return gate + (last ? "  ·  the last room in the story" : "");
        }

        static void Set(Text[] labels, int index, string value, Color color)
        {
            if (labels == null || index >= labels.Length || labels[index] == null)
            {
                return;
            }

            labels[index].text = value;
            labels[index].color = color;
        }

        /// <summary>The node the page should start on: the room earned and not yet walked.</summary>
        int OpeningNode()
        {
            GameFlow flow = GameFlow.Active;
            if (flow == null || railButtons == null)
            {
                return 0;
            }

            for (int i = 0; i < railButtons.Length && i < flow.ChapterCount; i++)
            {
                if (flow.IsUnlocked(i) && !flow.IsChapterComplete(i))
                {
                    return i;
                }
            }

            for (int i = Mathf.Min(railButtons.Length, flow.ChapterCount) - 1; i >= 0; i--)
            {
                if (flow.IsUnlocked(i))
                {
                    return i;
                }
            }

            return 0;
        }

        float RailY(int index)
        {
            if (railButtons == null || index < 0 || index >= railButtons.Length || railButtons[index] == null)
            {
                return 0f;
            }

            return ((RectTransform)railButtons[index].transform).anchoredPosition.y;
        }

        void Focus()
        {
            if (railButtons == null || selection < 0 || selection >= railButtons.Length ||
                railButtons[selection] == null || !railButtons[selection].gameObject.activeSelf ||
                EventSystem.current == null)
            {
                return;
            }

            EventSystem.current.SetSelectedGameObject(railButtons[selection].gameObject);
        }

        /// <summary>The dossier arrives with every choice, so a page that changes under the hand says so.</summary>
        void FadeDossier()
        {
            if (dossierGroup == null || !isActiveAndEnabled)
            {
                return;
            }

            if (dossierRun != null)
            {
                StopCoroutine(dossierRun);
            }

            dossierGroup.alpha = 0f;
            dossierRun = StartCoroutine(FadeDossierIn());
        }

        IEnumerator FadeDossierIn()
        {
            float seconds = Mathf.Max(0.02f, dossierFadeSeconds);
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / seconds);
                dossierGroup.alpha = 1f - (1f - t) * (1f - t);
                yield return null;
            }

            dossierGroup.alpha = 1f;
            dossierRun = null;
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
