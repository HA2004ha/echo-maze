using EchoMaze.Core;
using EchoMaze.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace EchoMaze.UI
{
    /// <summary>
    /// The page the game opens on: start, the story's chapters, the field guide, calibrate, quit, and one
    /// line telling you what the machine thinks your voice is.
    ///
    /// The status line is the reason this class touches the director at all. On Linux the capture device is a
    /// different answer on every machine, and a player who cannot tell whether the game hears them has no way
    /// to decide whether to shout or to go to the calibration page. The wording comes from
    /// <see cref="EchoDirector.SourceStatus"/>, which is the same string the log prints, so the interface and
    /// the log can never disagree about whether there is a microphone.
    ///
    /// The first button says what it will do, not what it is: "begin" on a profile with nothing walked,
    /// "continue · chapter II" once the first room is behind you, and "walk it again" when there is nothing
    /// left but the last room. That word is read from <see cref="GameFlow"/> in <c>Start</c>, never in
    /// <c>Awake</c>: the flow is another object in the same scene and its own Awake may not have run yet.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MainMenu : MonoBehaviour
    {
        [SerializeField] EchoDirector director;
        [SerializeField] Button startButton;
        [Tooltip("Opens the chapter page: the story's rooms, what is walked, what is still sealed.")]
        [SerializeField] Button chaptersButton;
        [SerializeField] Button guideButton;
        [SerializeField] Button calibrateButton;
        [SerializeField] Button quitButton;
        [SerializeField] Text statusLabel;
        [Tooltip("The word on the start button, rewritten from the saved progress when the menu opens.")]
        [SerializeField] Text startLabel;
        [Tooltip("The page the calibrate button opens, so the menu does not have to know how calibration works.")]
        [SerializeField] GameObject calibrationPanel;
        [Tooltip("The field guide, for the same reason: the menu opens it and knows nothing about pages.")]
        [SerializeField] GuidePanel guidePanel;
        [Tooltip("The chapter page, likewise: this class opens it and does not know what a node is.")]
        [SerializeField] ChapterSelectPanel chapterPanel;

        void Awake()
        {
            if (startButton != null)
            {
                startButton.onClick.AddListener(BeginRun);
            }

            if (chaptersButton != null)
            {
                chaptersButton.onClick.AddListener(OpenChapters);
            }

            if (guideButton != null)
            {
                guideButton.onClick.AddListener(() => OpenGuide());
            }

            if (calibrateButton != null)
            {
                calibrateButton.onClick.AddListener(OpenCalibration);
            }

            if (quitButton != null)
            {
                quitButton.onClick.AddListener(() => Application.Quit());
            }

            if (calibrationPanel != null)
            {
                calibrationPanel.SetActive(false);
            }

            if (guidePanel != null)
            {
                guidePanel.gameObject.SetActive(false);
            }

            if (chapterPanel != null)
            {
                chapterPanel.gameObject.SetActive(false);
            }

            if (director != null)
            {
                // The menu is not a room. It listens to the microphone so the status line and the
                // calibration page can work, and so the gate opening on a door slam in the next room does
                // not fire a ping against a pose nobody has set yet.
                director.EmitEnabled = false;
            }
        }

        void Start()
        {
            WriteStartLabel();

            // Gamepad and keyboard navigation start from nothing, and an EventSystem with no selection
            // shows no highlight and submits no button — the first frame of a pad user on the menu is
            // otherwise a dead screen. The menu's own first entry point is the beginning of the run.
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null &&
                startButton != null)
            {
                EventSystem.current.SetSelectedGameObject(startButton.gameObject);
            }
        }

        void Update()
        {
            if (statusLabel != null && director != null)
            {
                statusLabel.text = director.SourceStatus;
            }
        }

        /// <summary>
        /// The run's entry point: the furthest room the player has earned. A fresh profile has only chapter
        /// one open, so this is "begin" for a new player and "continue" for a player who left mid-story,
        /// without the two being two different buttons that can disagree about where they go.
        /// </summary>
        public void BeginRun()
        {
            GameFlow.Active?.ContinueRun();
        }

        public void OpenChapters()
        {
            chapterPanel?.Open();
        }

        public void OpenCalibration()
        {
            if (calibrationPanel != null)
            {
                calibrationPanel.SetActive(true);
            }
        }

        /// <summary>Opens the field guide, optionally at a named page. The page argument exists for the
        /// terminal capture hook, which has to be able to photograph the fifth card without clicking four
        /// buttons it cannot reach from a command line.</summary>
        public void OpenGuide(int page = 0)
        {
            guidePanel?.Open(page);
        }

        void WriteStartLabel()
        {
            if (startLabel == null)
            {
                return;
            }

            GameFlow flow = GameFlow.Active;
            if (flow == null || flow.ChaptersWalked == 0)
            {
                startLabel.text = "begin";
                return;
            }

            int index = flow.ContinueIndex;
            string numeral = ChapterCatalog.Numeral(index, flow.ChapterName(index));
            startLabel.text = flow.IsChapterComplete(index)
                ? "walk it again  ·  chapter " + numeral
                : "continue  ·  chapter " + numeral;
        }
    }
}
