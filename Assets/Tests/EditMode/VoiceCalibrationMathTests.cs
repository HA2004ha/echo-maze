using EchoMaze.Audio;
using EchoMaze.Data;
using NUnit.Framework;
using UnityEngine;

namespace EchoMaze.Tests
{
    /// <summary>
    /// The arithmetic behind the calibration page. It is the only part of the microphone path that can be
    /// proven without a microphone: a threshold set wrong by a factor of ten is the difference between a game
    /// that answers the player and one that pings at the fridge, and neither is visible in a log line.
    /// </summary>
    public class VoiceCalibrationMathTests
    {
        EchoTuningProfile profile;

        [SetUp]
        public void Build()
        {
            // The shipped defaults, not a hand-tuned profile: the question this suite asks is whether the
            // arithmetic is right for the numbers the game actually starts with.
            profile = ScriptableObject.CreateInstance<EchoTuningProfile>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(profile);

        [Test]
        public void ARoomThatWasNeverspokenIn_IsRefused()
        {
            GateCalibration result = VoiceCalibrationMath.FromPeak(profile.calibrationFloorRms * 0.5f, profile);

            Assert.That(result.IsValid, Is.False,
                        "a gate tuned to a silent room hears a fridge as a shout");
            Assert.That(result.OpenRms, Is.EqualTo(0f));
        }

        [Test]
        public void ACallThatIsRealButTiny_IsStillRefusedRatherThanGivenAGateItCannotOpen()
        {
            // The floor on `open` exists to stop a silent apartment tuning the gate to a fridge, and it is
            // allowed to outbid a very quiet call. What must never happen is a "valid" calibration whose own
            // voice cannot trigger it: that is a page that says "done" and a game that stays deaf.
            float peak = profile.calibrationMinOpenRms * 0.75f;

            Assert.That(peak, Is.GreaterThanOrEqualTo(profile.calibrationFloorRms),
                        "this case is only interesting above the refusal floor");
            Assert.That(VoiceCalibrationMath.FromPeak(peak, profile).IsValid, Is.False);
        }

        [Test]
        public void TheCalibratedCallOpensTheGateItProduced()
        {
            GateCalibration result = VoiceCalibrationMath.FromPeak(0.05f, profile);

            Assert.That(result.IsValid, Is.True);

            var gate = new NoiseGate(result.OpenRms, result.CloseRms, result.FullRms, profile.loudnessHold);
            Assert.That(gate.Update(0.05f, 1f / 60f), Is.True,
                        "the sound the player just made has to be the sound that opens the gate");
        }

        [Test]
        public void TheCalibratedCallReadsTheTargetLoudness()
        {
            GateCalibration result = VoiceCalibrationMath.FromPeak(0.05f, profile);
            var gate = new NoiseGate(result.OpenRms, result.CloseRms, result.FullRms, profile.loudnessHold);
            gate.Update(0.05f, 1f / 60f);

            // The whole promise of the page: the voice you calibrated with is mid-loud, so a shout above it
            // is still headroom rather than a clipped 1.0 from the first syllable.
            Assert.That(gate.Loudness, Is.EqualTo(profile.calibrationTargetLoudness).Within(0.001f));
        }

        [Test]
        public void AShoutAboveTheCalibratedCallGivesMoreReachButNotMoreThanOne()
        {
            GateCalibration result = VoiceCalibrationMath.FromPeak(0.05f, profile);
            var gate = new NoiseGate(result.OpenRms, result.CloseRms, result.FullRms, profile.loudnessHold);
            gate.Update(0.05f * 4f, 1f / 60f);

            Assert.That(gate.Loudness, Is.GreaterThan(profile.calibrationTargetLoudness));
            Assert.That(gate.Loudness, Is.LessThanOrEqualTo(1f));
        }

        [Test]
        public void TheCloseThresholdIsAlwaysBelowTheOpenOne()
        {
            // Hysteresis is the only reason one word is one ping, and a calibration that inverts the band
            // would chatter. The peaks start where the floor on `open` stops outbidding them.
            foreach (float peak in new[] { 0.01f, 0.02f, 0.05f, 0.12f, 0.4f })
            {
                GateCalibration result = VoiceCalibrationMath.FromPeak(peak, profile);
                Assert.That(result.IsValid, Is.True, "peak " + peak);
                Assert.That(result.CloseRms, Is.LessThan(result.OpenRms), "peak " + peak);
                Assert.That(result.FullRms, Is.GreaterThan(result.OpenRms), "peak " + peak);
            }
        }

        [Test]
        public void TheOpenThresholdStaysInsideItsOwnBand()
        {
            GateCalibration quiet = VoiceCalibrationMath.FromPeak(0.01f, profile);
            GateCalibration loud = VoiceCalibrationMath.FromPeak(0.4f, profile);

            Assert.That(quiet.OpenRms, Is.GreaterThanOrEqualTo(profile.calibrationMinOpenRms));
            Assert.That(loud.OpenRms, Is.LessThanOrEqualTo(profile.calibrationMaxOpenRms),
                        "a gate that needs a scream is a gate the player will not use");
        }

        [Test]
        public void RunningPeakKeepsTheLoudestWindow()
        {
            float peak = 0f;
            foreach (float sample in new[] { 0.01f, 0.06f, 0.02f, 0.03f })
            {
                peak = VoiceCalibrationMath.RunningPeak(peak, sample);
            }

            Assert.That(peak, Is.EqualTo(0.06f),
                        "the gate is set from the loudest window, not the average of how long the word was");
        }

        [Test]
        public void NoProfileRefused()
        {
            Assert.That(VoiceCalibrationMath.FromPeak(0.05f, null).IsValid, Is.False);
        }
    }
}
