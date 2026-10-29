using EchoMaze.Core;
using EchoMaze.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace EchoMaze.UI
{
    /// <summary>
    /// The page the game opens on: start, calibrate, quit, and one line telling you what the machine thinks
    /// your voice is.
    ///
    /// The status line is the reason this class touches the director at all. On Linux the capture device is a
    /// different answer on every machine, and a player who cannot tell whether the game hears them has no way
    /// to decide whether to shout or to go to the calibration page. The wording comes from
    /// <see cref="EchoDirector.SourceStatus"/>, which is the same string the log prints, so the interface and
    /// the log can never disagree about whether there is a microphone.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MainMenu : MonoBehaviour
    {
        [SerializeField] EchoDirector director;
        [SerializeField] Button startButton;
        [SerializeField] Button calibrateButton;
        [SerializeField] Button quitButton;
        [SerializeField] Text statusLabel;
        [Tooltip("The page the calibrate button opens, so the menu does not have to know how calibration works.")]
        [SerializeField] GameObject calibrationPanel;

        void Awake()
        {
            if (startButton != null)
            {
                startButton.onClick.AddListener(BeginRun);
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

            if (director != null)
            {
                // The menu is not a room. It listens to the microphone so the status line and the
                // calibration page can work, and so the gate opening on a door slam in the next room does
                // not fire a ping against a pose nobody has set yet.
                director.EmitEnabled = false;
            }
        }

        void Update()
        {
            if (statusLabel != null && director != null)
            {
                statusLabel.text = director.SourceStatus;
            }
        }

        /// <summary>Named rather than called <c>Start</c>, which is a Unity message and would run itself on
        /// scene load, straight out of the menu and into chapter one.</summary>
        public void BeginRun()
        {
            GameFlow.Active?.StartRun();
        }

        public void OpenCalibration()
        {
            if (calibrationPanel != null)
            {
                calibrationPanel.SetActive(true);
            }
        }
    }
}
