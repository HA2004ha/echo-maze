using System;
using EchoMaze.Audio;
using EchoMaze.Core;
using EchoMaze.Data;
using UnityEngine;

namespace EchoMaze.UI
{
    /// <summary>
    /// The calibration page's engine: listen for a few seconds while the player makes the sound they intend
    /// to play with, and put the gate where that sound clears it.
    ///
    /// It lives in the menu rather than only inside the first level because a gate that is wrong is wrong
    /// before the player has ever seen a room, and because the answer to "the game ignores my voice" has to
    /// be a page they can reach again without restarting.
    ///
    /// Nothing here draws. The meter and the copy belong to <see cref="CalibrationPanel"/>, which reads
    /// <see cref="LiveRms"/>, <see cref="PeakRms"/> and <see cref="Progress01"/> and renders them; that split
    /// is what makes the arithmetic testable in EditMode without a canvas.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VoiceCalibration : MonoBehaviour
    {
        [SerializeField] EchoDirector director;

        bool measuring;
        bool emitBefore;
        float peak;
        float startedAt;

        /// <summary>Raised once per run, with the result: accepted thresholds or a refusal naming the peak.</summary>
        public event Action<GateCalibration> Finished;

        public bool IsMeasuring => measuring;

        /// <summary>The thing the page switches the microphone on when the player picks a different input.
        /// Read-only: the panel moves pixels and asks, it does not own the capture.</summary>
        public EchoDirector Director => director;

        /// <summary>
        /// False when the machine has no capture to calibrate. The page says so and offers the Space path
        /// rather than measuring a silence and calling it a voice.
        /// </summary>
        public bool HasMicrophone => director != null && director.Source is MicrophoneEchoSource;

        public float PeakRms => peak;

        public float LiveRms => director != null ? director.RawLoudness : 0f;

        /// <summary>0..1 through the listening window, for the progress bar.</summary>
        public float Progress01
        {
            get
            {
                EchoTuningProfile profile = director != null ? director.Profile : null;
                if (!measuring || profile == null || profile.calibrationSeconds <= 0f)
                {
                    return 0f;
                }

                return Mathf.Clamp01((Time.unscaledTime - startedAt) / profile.calibrationSeconds);
            }
        }

        /// <summary>
        /// Start listening. Emission is switched off for the window: a page that fired a wave for every
        /// attempt at being quiet would teach the player that the tool they are tuning is the thing
        /// bothering them, and the cooldown it pays would still be running when the level opened.
        /// </summary>
        public void Begin()
        {
            if (director == null)
            {
                return;
            }

            if (!HasMicrophone)
            {
                Finished?.Invoke(GateCalibration.Refused(0f));
                return;
            }

            measuring = true;
            emitBefore = director.EmitEnabled;
            peak = 0f;
            startedAt = Time.unscaledTime;
            director.EmitEnabled = false;
        }

        /// <summary>Leave without applying anything; the thresholds the player had stay the thresholds they have.</summary>
        public void Cancel()
        {
            if (!measuring)
            {
                return;
            }

            measuring = false;
            director.EmitEnabled = emitBefore;
        }

        void Update()
        {
            if (!measuring)
            {
                return;
            }

            peak = VoiceCalibrationMath.RunningPeak(peak, LiveRms);

            EchoTuningProfile profile = director.Profile;
            if (Time.unscaledTime - startedAt < profile.calibrationSeconds)
            {
                return;
            }

            measuring = false;
            GateCalibration result = VoiceCalibrationMath.FromPeak(peak, profile);

            if (result.IsValid)
            {
                director.ApplyGateThresholds(result.OpenRms, result.CloseRms, result.FullRms);
            }

            director.EmitEnabled = emitBefore;
            Finished?.Invoke(result);
        }
    }
}
