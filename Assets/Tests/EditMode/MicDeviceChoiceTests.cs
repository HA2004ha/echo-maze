using EchoMaze.Audio;
using EchoMaze.UI;
using NUnit.Framework;

namespace EchoMaze.Tests.EditMode
{
    /// <summary>
    /// The microphone choice, as a rule over a supplied list.
    ///
    /// This is the one part of the audio path that has never been about audio: the test matrix measured that
    /// the Linux player auto-selects the PipeWire alias <c>Default Input Device</c>, and a name is not a
    /// promise about what an alias points at. So the selection rules are pinned here, on lists written out
    /// in the test, on the machine that has the microphone problem and on the one that does not.
    /// </summary>
    public class MicDeviceChoiceTests
    {
        static readonly string[] RealMachine =
        {
            "Default Input Device",
            "Monitor of Family 17h/19h HD Audio Controller Analog Stereo",
            "Family 17h/19h HD Audio Controller Analog Stereo",
        };

        [Test]
        public void Auto_SkipsEveryMonitor()
        {
            Assert.AreEqual("Default Input Device", MicDeviceChoice.AutoSelect(RealMachine),
                            "a monitor is our own speakers, and the game would answer its own sound effects forever");
            Assert.AreEqual("Headset Microphone", MicDeviceChoice.AutoSelect(new[]
            {
                "Monitor of Headset", "Headset Microphone", "Monitor of Speakers"
            }), "the fallback is the first real input, not the first entry");
        }

        [Test]
        public void Auto_IsCaseInsensitive()
        {
            Assert.AreEqual("mic", MicDeviceChoice.AutoSelect(new[] { "monitor of mic", "mic" }),
                            "PipeWire capitalises, PulseAudio did not, and both mean the same thing");
        }

        [Test]
        public void AnEmptyMachine_YieldsNoDevice()
        {
            Assert.IsNull(MicDeviceChoice.AutoSelect(new string[0]));
            Assert.IsNull(MicDeviceChoice.AutoSelect(null));
            Assert.IsNull(MicDeviceChoice.Resolve(new string[0], "anything"),
                          "no device is the state the simulated fallback and the Space key are there for");
        }

        [Test]
        public void ASavedNameThatIsStillOnTheMachine_IsUsedExactly()
        {
            Assert.AreEqual(RealMachine[2], MicDeviceChoice.Resolve(RealMachine, RealMachine[2]),
                            "a pinned headset is honoured even though auto would have picked something else");
        }

        [Test]
        public void ASavedNameThatLeftTheMachine_FallsBackToAuto()
        {
            Assert.AreEqual("Default Input Device",
                            MicDeviceChoice.Resolve(RealMachine, "QCY-T1 ANC Microphone"),
                            "the headset went out of the room; that is not a reason to leave the player deaf");
        }

        [Test]
        public void AListThatIsAllMonitors_StillOpensSomething()
        {
            Assert.AreEqual("Monitor of Speakers",
                            MicDeviceChoice.AutoSelect(new[] { "Monitor of Speakers", "Monitor of HDMI" }),
                            "the page says what it landed on rather than pretending there is no input at all");
        }

        [Test]
        public void ThePickerCycle_VisitsAutoAndEveryDeviceOnce()
        {
            int count = 3;
            Assert.AreEqual(0, CalibrationPanel.NextChoiceIndex(-1, count),
                            "auto — or a saved name that has gone — steps to the first device");
            Assert.AreEqual(1, CalibrationPanel.NextChoiceIndex(0, count));
            Assert.AreEqual(2, CalibrationPanel.NextChoiceIndex(1, count));
            Assert.AreEqual(0, CalibrationPanel.NextChoiceIndex(2, count),
                            "and then back to auto, so the cycle is closed and cannot walk off the list");
        }
    }
}
