using System;
using EchoMaze.Audio;
using EchoMaze.Core;
using UnityEngine;
using UnityEngine.UI;

namespace EchoMaze.UI
{
    /// <summary>
    /// The page that tunes the gate: a meter that answers your voice, one button to start listening, the
    /// thresholds it landed on, and the input it is listening through.
    ///
    /// Deliberately thin — the arithmetic is <see cref="VoiceCalibrationMath"/>, the listening is
    /// <see cref="VoiceCalibration"/>, and this class only moves pixels. That split is what lets an EditMode
    /// test prove that a given peak produces a given gate without a microphone, a canvas or a scene.
    ///
    /// The input picker lives here because this page already owns the question the list answers — "is the
    /// game hearing me?" — and because on Linux the answer has never been a name the code could trust: the
    /// player build auto-selects the PipeWire alias <c>Default Input Device</c>, and an alias can point at a
    /// monitor, which is the game answering its own speakers. A picker is the only way out that does not
    /// involve editing a ScriptableObject in a terminal.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CalibrationPanel : MonoBehaviour
    {
        /// <summary>The choice that means "let the game decide", i.e. index 0 of the cycle.</summary>
        const string AutoCaption = "auto";

        [SerializeField] VoiceCalibration calibration;
        [SerializeField] Button startButton;
        [SerializeField] Button closeButton;
        [SerializeField] Image meter;
        [SerializeField, Range(0.01f, 0.2f)] float meterFullRms = 0.08f;
        [SerializeField] Text instructionLabel;
        [SerializeField] Text resultLabel;
        [SerializeField] Button deviceButton;
        [SerializeField] Text deviceLabel;
        [SerializeField, Range(0.5f, 10f)] float deviceRescanSeconds = 2f;

        string instruction = "make the sound you want to play with";
        string[] devices = Array.Empty<string>();
        float nextRescanAt;

        void Awake()
        {
            if (startButton != null)
            {
                startButton.onClick.AddListener(Begin);
            }

            if (closeButton != null)
            {
                closeButton.onClick.AddListener(Close);
            }

            if (deviceButton != null)
            {
                deviceButton.onClick.AddListener(CycleDevice);
            }

            if (calibration != null)
            {
                calibration.Finished += OnFinished;
            }

            if (instructionLabel != null)
            {
                instructionLabel.text = instruction;
            }
        }

        void OnEnable()
        {
            // The page is where a headset gets plugged in, so the list is rebuilt every time it opens rather
            // than once at startup.
            RefreshDevices();
        }

        void OnDestroy()
        {
            if (calibration != null)
            {
                calibration.Finished -= OnFinished;
            }
        }

        void Update()
        {
            if (meter == null || calibration == null)
            {
                return;
            }

            // The live bar and the peak it is measured against, both against the same scale: the player has
            // to be able to see how far above their own voice the gate will sit, or calibration is a leap of
            // faith with a progress bar attached.
            meter.fillAmount = Mathf.Clamp01(calibration.LiveRms / meterFullRms);

            if (Time.unscaledTime >= nextRescanAt && DevicesChanged())
            {
                RefreshDevices();
            }
        }

        /// <summary>
        /// Re-read the machine and put the answer on the page. A saved name whose device has left the room is
        /// not shown as the choice: the capture is running on the fallback, and the page says what the game
        /// actually hears instead of what somebody once picked.
        /// </summary>
        public void RefreshDevices()
        {
            nextRescanAt = Time.unscaledTime + deviceRescanSeconds;
            devices = EchoDirector.AvailableDevices();

            string stored = EchoDirector.StoredDeviceName;
            if (deviceButton != null)
            {
                deviceButton.GetComponentInChildren<Text>().text =
                    stored.Length == 0 ? "input: " + AutoCaption : "input: " + Short(stored);
                deviceButton.interactable = devices.Length > 0;
            }

            ReportDevice();
        }

        /// <summary>
        /// Step the choice: auto, then every device the machine lists, then auto again. Applied the moment it
        /// is chosen, so the line under the button is the answer and not a preview of one.
        /// </summary>
        public void CycleDevice()
        {
            if (calibration?.Director == null || devices.Length == 0)
            {
                return;
            }

            int next = NextChoiceIndex(Array.IndexOf(devices, EchoDirector.StoredDeviceName), devices.Length);
            calibration.Director.UseDevice(next == 0 ? MicDeviceChoice.Auto : devices[next - 1]);
            RefreshDevices();
        }

        /// <summary>
        /// The pure half of the picker: where the cycle goes next. Public and static because the rule is a
        /// number line and not a widget, and the test that pins it should not need a canvas.
        /// </summary>
        public static int NextChoiceIndex(int storedIndex, int deviceCount)
        {
            // -1 is "auto", or a saved name that is no longer on the machine; both step to the first device.
            int current = storedIndex < 0 ? -1 : storedIndex;
            int next = current + 1;
            return next > deviceCount - 1 ? 0 : next;
        }

        /// <summary>The capture the game is listening to right now, or null when none is open.</summary>
        public string ActiveDeviceName => calibration != null && calibration.Director != null
            ? calibration.Director.ActiveDeviceName
            : null;

        void ReportDevice()
        {
            if (deviceLabel == null)
            {
                return;
            }

            string active = ActiveDeviceName;
            deviceLabel.text = devices.Length == 0
                ? "no capture device on this machine — the game plays on Space"
                : active == null
                    ? "nothing is open yet — the game plays on Space"
                    : "listening to: " + Short(active);
        }

        static string Short(string name) =>
            name.Length <= 34 ? name : name.Substring(0, 31) + "…";

        bool DevicesChanged()
        {
            string[] now = EchoDirector.AvailableDevices();
            if (now.Length != devices.Length)
            {
                return true;
            }

            for (int i = 0; i < now.Length; i++)
            {
                if (now[i] != devices[i])
                {
                    return true;
                }
            }

            return false;
        }

        public void Begin()
        {
            if (calibration == null)
            {
                return;
            }

            if (startButton != null)
            {
                startButton.interactable = false;
            }

            if (resultLabel != null)
            {
                resultLabel.text = "listening…";
            }

            calibration.Begin();
        }

        public void Close()
        {
            calibration?.Cancel();
            gameObject.SetActive(false);
        }

        void OnFinished(GateCalibration result)
        {
            if (startButton != null)
            {
                startButton.interactable = true;
            }

            if (resultLabel == null)
            {
                return;
            }

            resultLabel.text = result.IsValid
                ? "gate opens at " + Millivolts(result.OpenRms) + ", a call reads mid-loud"
                : "nothing worth hearing (" + Millivolts(result.PeakRms) + ") — the game plays on Space";
        }

        /// <summary>
        /// RMS printed as millivolts. A raw 0.023 is not a number a person has any intuition for, and the
        /// whole point of this page is that the player can tell whether the answer looks like their machine.
        /// </summary>
        static string Millivolts(float rms) => Mathf.RoundToInt(rms * 1000f) + " mV";
    }
}
