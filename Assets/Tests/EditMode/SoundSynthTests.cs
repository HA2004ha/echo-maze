using EchoMaze.Audio;
using Stopwatch = System.Diagnostics.Stopwatch;
using NUnit.Framework;
using UnityEngine;

namespace EchoMaze.Tests
{
    /// <summary>
    /// The sound palette, checked by the only instrument a batch-mode suite has: the samples themselves.
    ///
    /// A person with headphones decides whether the wave coming back sounds like a wave coming back. A test
    /// can decide the things that are wrong regardless of taste: an event with no sound at all, a sound that
    /// is silence, a sample that is not a number, a loop that clicks at its seam, and a palette that takes
    /// longer to synthesise than the menu takes to open.
    /// </summary>
    public class SoundSynthTests
    {
        const float AudibleThreshold = 0.01f;

        [Test]
        public void EveryEvent_HasASoundThatCanBeHeard()
        {
            foreach (SoundId id in SoundCatalog.All)
            {
                float[] samples = SoundSynth.SampleBuffer(id);

                Assert.Greater(samples.Length / (float)SoundSynth.SampleRate, 0.05f,
                               id + " is too short to be heard as anything");

                float peak = 0f;
                foreach (float sample in samples)
                {
                    peak = Mathf.Max(peak, Mathf.Abs(sample));
                }

                Assert.Greater(peak, AudibleThreshold, id + " is silence");
            }
        }

        [Test]
        public void NoSound_IsAnOverloadOrANumberThatIsNotOne()
        {
            foreach (SoundId id in SoundCatalog.All)
            {
                foreach (float sample in SoundSynth.SampleBuffer(id))
                {
                    // A NaN sample is a silent clip and a screaming console; both have shipped in games.
                    Assert.IsFalse(float.IsNaN(sample), id + " contains a NaN");
                    Assert.LessOrEqual(Mathf.Abs(sample), 1f, id + " leaves the range a speaker can play");
                }
            }
        }

        [Test]
        public void ALoop_JoinsItsOwnEndWithoutAStep()
        {
            foreach (SoundId id in SoundCatalog.All)
            {
                if (!SoundCatalog.IsLoop(id))
                {
                    continue;
                }

                float[] samples = SoundSynth.SampleBuffer(id);
                float step = Mathf.Abs(samples[samples.Length - 1] - samples[0]);

                // One sample's difference between the end and the start is all a seam is allowed to be.
                Assert.Less(step, 0.1f, id + " clicks at its loop point");
            }
        }

        [Test]
        public void TheTheme_IsLongEnoughToBeMusic()
        {
            float menu = SoundSynth.SampleBuffer(SoundId.MenuTheme).Length / (float)SoundSynth.SampleRate;
            float room = SoundSynth.SampleBuffer(SoundId.ChapterBed).Length / (float)SoundSynth.SampleRate;

            Assert.Greater(menu, 20f, "a menu theme under twenty seconds is a jingle");
            Assert.Greater(room, 15f, "a room bed under fifteen seconds is heard looping while you stand still");
        }

        [Test]
        public void TheEcho_GrowsLouderAndFallsInPitchWithTheShout()
        {
            Assert.Less(SoundSynth.PingEchoVolume(0.2f), SoundSynth.PingEchoVolume(0.9f),
                        "a shout has to come back louder than a tap");
            Assert.Greater(SoundSynth.PingEchoPitch(0.2f), SoundSynth.PingEchoPitch(0.9f),
                           "a shout has to come back lower than a tap");
        }

        [Test]
        public void EverySound_IsNamedAfterTheFileThatWouldReplaceIt()
        {
            foreach (SoundId id in SoundCatalog.All)
            {
                Assert.AreEqual("Assets/Audio/" + id + ".wav", SoundCatalog.AssetPath(id));
            }
        }

        [Test]
        public void TheWholePalette_IsCheapEnoughToBuildWhileTheMenuOpens()
        {
            var watch = new Stopwatch();
            long eventMs = 0;
            long musicMs = 0;

            foreach (SoundId id in SoundCatalog.All)
            {
                watch.Restart();
                SoundSynth.SampleBuffer(id);
                watch.Stop();

                if (SoundCatalog.IsMusic(id))
                {
                    musicMs += watch.ElapsedMilliseconds;
                }
                else
                {
                    eventMs += watch.ElapsedMilliseconds;
                }
            }

            // Printed, not guessed: the split is the answer to "what has to be generated on the main thread",
            // because the music half is ~60 s of samples and the events are ~8 s. A test that only asserts
            // makes nobody read the number.
            Debug.Log("SoundSynth: " + SoundCatalog.All.Length + " sounds in " + (eventMs + musicMs) +
                      " ms (" + eventMs + " ms of events on the main thread, " + musicMs +
                      " ms of music generated off it)");

            Assert.Less(eventMs, 250,
                        "the event palette takes long enough to synthesise that a ping would hitch");
            Assert.Less(eventMs + musicMs, 3000, "synthesising the palette is slower than loading the game");
        }
    }
}
