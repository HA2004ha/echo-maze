using EchoMaze.Data;
using UnityEngine;

namespace EchoMaze.Audio
{
    /// <summary>
    /// What one calibration produced: three thresholds, or a refusal. A struct rather than an out-parameter
    /// triple because "the room was too quiet for that to have been a voice" is a result the page has to
    /// render, not an exception on the game's main thread.
    /// </summary>
    public readonly struct GateCalibration
    {
        public readonly bool IsValid;
        public readonly float PeakRms;
        public readonly float OpenRms;
        public readonly float CloseRms;
        public readonly float FullRms;

        GateCalibration(bool valid, float peak, float open, float close, float full)
        {
            IsValid = valid;
            PeakRms = peak;
            OpenRms = open;
            CloseRms = close;
            FullRms = full;
        }

        public static GateCalibration Refused(float peak) => new GateCalibration(false, peak, 0f, 0f, 0f);

        public static GateCalibration Accepted(float peak, float open, float close, float full) =>
            new GateCalibration(true, peak, open, close, full);
    }

    /// <summary>
    /// The arithmetic of the calibration page, kept out of the component so an EditMode test can hand it a
    /// peak and assert the thresholds without a microphone, a frame clock or a scene.
    /// </summary>
    public static class VoiceCalibrationMath
    {
        /// <summary>
        /// The loudest window heard so far. A peak and not an average, because the player is asked to make
        /// the sound they intend to play with, and the gate has to open for the quietest of those — the
        /// average of a window with one word in it is a function of how long the word was.
        /// </summary>
        public static float RunningPeak(float peak, float sample) => Mathf.Max(peak, sample);

        /// <summary>
        /// Thresholds for a measured peak.
        ///
        /// The open line lands under the calibrated call by <see cref="EchoTuningProfile.calibrationOpenFraction"/>,
        /// so the voice that was just measured opens the gate with margin; the full line is then placed so
        /// that same call reads <see cref="EchoTuningProfile.calibrationTargetLoudness"/>, which puts a shout
        /// near 1 without needing a second calibration step.
        ///
        /// A peak under the floor is refused rather than clamped: shipping a gate opened by a fridge would
        /// make the game emit waves the player never made, which is worse than an uncalibrated default.
        /// </summary>
        public static GateCalibration FromPeak(float peakRms, EchoTuningProfile profile)
        {
            if (profile == null)
            {
                return GateCalibration.Refused(peakRms);
            }

            if (peakRms < profile.calibrationFloorRms)
            {
                return GateCalibration.Refused(peakRms);
            }

            float open = Mathf.Clamp(peakRms * profile.calibrationOpenFraction,
                                     profile.calibrationMinOpenRms, profile.calibrationMaxOpenRms);

            // The floor on `open` exists to stop a silent apartment tuning the gate to a fridge, and it is
            // allowed to outbid a very quiet call: a gate the calibrated voice cannot open is not a refusal
            // of the room, it is a refusal of that attempt, and the page says so. Passing it through would
            // hand back thresholds that look valid and a game that never answers the player.
            if (open >= peakRms)
            {
                return GateCalibration.Refused(peakRms);
            }

            float close = open * profile.calibrationCloseRatio;

            // peak = close + target * (full - close), solved for full. Inverting the gate's own ramp is what
            // makes "the call you just made reads 0.6" a promise rather than a hope.
            float target = Mathf.Clamp(profile.calibrationTargetLoudness, 0.2f, 0.95f);
            float full = close + (peakRms - close) / target;

            // The gate enforces full >= open * 1.5 itself; keeping that rule visible here means the number
            // the page prints is the number the gate ends up with.
            full = Mathf.Max(full, open * 1.5f);

            return GateCalibration.Accepted(peakRms, open, close, full);
        }
    }
}
