using EchoMaze.Audio;
using NUnit.Framework;

namespace EchoMaze.Tests
{
    /// <summary>
    /// The gate is the only place the game decides that a sound happened, so its two failure modes are
    /// the two ways the week can be ruined: chatter (one word, four waves) and deadness (a voice that
    /// never opens it). Both are pure level arithmetic and belong in EditMode.
    /// </summary>
    public class NoiseGateTests
    {
        const float Open = 0.02f;
        const float Close = 0.012f;
        const float Full = 0.12f;
        const float Hold = 0.25f;
        const float Frame = 1f / 60f;

        static NoiseGate Gate() => new NoiseGate(Open, Close, Full, Hold);

        [Test]
        public void Silence_NeverOpens()
        {
            NoiseGate gate = Gate();
            for (int i = 0; i < 60; i++)
            {
                Assert.That(gate.Update(0f, Frame), Is.False);
            }

            Assert.That(gate.Loudness, Is.EqualTo(0f));
        }

        [Test]
        public void OneRise_MakesExactlyOneEvent()
        {
            NoiseGate gate = Gate();
            Assert.That(gate.Update(0.5f, Frame), Is.True, "the first frame above the threshold is the event");

            for (int i = 0; i < 60; i++)
            {
                Assert.That(gate.Update(0.5f, Frame), Is.False,
                    "a held voice must not re-arm: this is the barrage week 1 shipped with");
            }
        }

        [Test]
        public void DipInsideTheHysteresisBand_KeepsTheSameEvent()
        {
            NoiseGate gate = Gate();
            gate.Update(0.5f, Frame);

            // Between close and open the gate stays shut: this is the band a room tone or a breath sits in.
            Assert.That(gate.Update(0.016f, Frame), Is.False);
            Assert.That(gate.Update(0.016f, Frame), Is.False,
                    "levels in the band must not count as a new vocal event");
            Assert.That(gate.IsOpen, Is.True, "and the gate must not close inside the band either");
        }

        [Test]
        public void ASecondRise_AfterARealPause_IsASecondEvent()
        {
            NoiseGate gate = Gate();
            Assert.That(gate.Update(0.5f, Frame), Is.True);

            Assert.That(gate.Update(0f, Frame), Is.False, "falling out of the gate is not a vocal event");
            Assert.That(gate.IsOpen, Is.False, "the level dropped below the close threshold, so it shut");

            Assert.That(gate.Update(0.5f, Frame), Is.True, "and the next rise is a new word");
            Assert.That(gate.Update(0.5f, Frame), Is.False, "held again, so silent again");
        }

        [Test]
        public void LoudnessMapsWhisperToShout()
        {
            NoiseGate gate = Gate();
            gate.Update(0.04f, Frame);
            float quiet = gate.Loudness;
            Assert.That(quiet, Is.GreaterThan(0f).And.LessThan(1f),
                "a careful voice should sit in the middle of the range, not at either end");

            gate.Update(1f, Frame);
            Assert.That(gate.Loudness, Is.EqualTo(1f), "a shout must be able to saturate");
        }

        [Test]
        public void LoudnessIsHeldAcrossAShortGap()
        {
            NoiseGate gate = Gate();
            gate.Update(0.5f, Frame);
            Assert.That(gate.Loudness, Is.GreaterThan(0f));

            // One frame of nothing between two syllables of one word: the level stays up, so the
            // smoothing never turns a sentence into a strobe.
            gate.Update(0f, Frame);
            Assert.That(gate.Loudness, Is.GreaterThan(0f));

            for (int i = 0; i < (int)(Hold / Frame) + 2; i++)
            {
                gate.Update(0f, Frame);
            }

            Assert.That(gate.Loudness, Is.EqualTo(0f), "but the hold has to end eventually");
        }

        [Test]
        public void Constructor_FixesAnUnsortedThresholdPair()
        {
            // A calibration that puts close above open would otherwise latch the gate shut forever.
            NoiseGate gate = new NoiseGate(0.02f, 0.5f, 0.1f, Hold);
            gate.Update(0.5f, Frame);
            Assert.That(gate.IsOpen, Is.True);
            gate.Update(0.001f, Frame);
            Assert.That(gate.IsOpen, Is.False);
        }
    }
}
