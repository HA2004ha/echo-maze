using UnityEngine;

namespace EchoMaze.Audio
{
    /// <summary>
    /// The game's music, written as note data and rendered by <see cref="SoundSynth"/> like every other
    /// sound. Three pieces, because the game has three places to be: the menu, a room, and the end of a room.
    ///
    /// The menu theme is in D minor at 60 BPM, eight bars in two four-bar phrases: a phrase that climbs to
    /// the fifth, and a phrase that answers it and lands on the tonic over the dominant chord, so the loop
    /// point is a cadence rather than a seam. Three layers hold it — a pad, a bass, and a bell that carries
    /// the tune — and then the tune is played **again, twice**, once a beat and a half late and an octave
    /// down, once three beats late and barely there. Those copies are the whole idea: a sound that comes
    /// back changed is the game's own subject, and a menu about listening should be the first room that
    /// answers. They wrap past the end of the loop, so the last note of the tune is answered after the loop
    /// point, in the first bar.
    ///
    /// A room's bed is the same key with no motif: two low drones, air swelling and falling twice a loop,
    /// and a bell every other bar somewhere far away. It is deliberately boring. A chase adds the heartbeat
    /// from <see cref="SoundBoard"/>, and the music must not already be tense when nothing is happening.
    ///
    /// Everything loops. The pads' release tails are wrapped to the top of the buffer, so a chord that runs
    /// over a bar line decays into the next loop instead of stopping at it.
    /// </summary>
    public static class MusicScore
    {
        public const float MenuBpm = 60f;
        public const float ChapterBpm = 52f;
        const int MenuBars = 8;
        const int ChapterBars = 6;

        /// <summary>
        /// One note of the theme, counted in **beats from the top of the loop** rather than in seconds, so the
        /// tempo is one number and the tune cannot drift away from the harmony it is written against.
        /// </summary>
        readonly struct Step
        {
            public readonly float Beat;
            public readonly float Beats;
            public readonly int Pitch;

            public Step(float beat, float beats, int pitch)
            {
                Beat = beat;
                Beats = beats;
                Pitch = pitch;
            }
        }

        /// <summary>
        /// The menu: Dm – Bb – F – Gm – A. Eight bars at 60 BPM, a four-bar question and a four-bar answer,
        /// ending on the tonic over the dominant so the loop point is a cadence. Three layers: a pad and a
        /// bass that hold the harmony, the tune on a bell, and the tune twice more behind it.
        /// </summary>
        public static float[] MenuTheme()
        {
            float beat = 60f / MenuBpm;
            float barLength = 4f * beat;
            float length = MenuBars * barLength;
            float[] buffer = SoundSynth.Buffer(length);

            // One chord per bar: two of Dm, two of Bb, then F, Gm and two of the dominant A. The bass sits
            // an octave below the pad's root, which is the register the arc's hum occupies in a room.
            int[] roots = { 38, 38, 34, 34, 41, 43, 45, 45 };
            int[][] chords =
            {
                new[] { 57, 62, 65 }, // Dm
                new[] { 57, 62, 65 },
                new[] { 58, 62, 65 }, // Bb
                new[] { 58, 62, 65 },
                new[] { 57, 60, 65 }, // F
                new[] { 55, 58, 62 }, // Gm
                new[] { 57, 61, 64 }, // A
                new[] { 57, 61, 64 },
            };

            for (int bar = 0; bar < MenuBars; bar++)
            {
                float start = bar * barLength;

                foreach (int note in chords[bar])
                {
                    // A long attack and a long release: the pad is air, not a chord being struck. It runs
                    // past the bar line and wraps at the loop point, so the harmony never stops breathing.
                    SoundSynth.Tone(buffer, start, barLength * 0.92f, SoundSynth.Midi(note),
                                    SoundSynth.Midi(note), 0.07f, 1.1f, 1.5f, SoundSynth.Wave.Sine,
                                    wrap: true, vibrato: 0.0015f);
                }

                SoundSynth.Tone(buffer, start, barLength * 0.8f, SoundSynth.Midi(roots[bar]),
                                SoundSynth.Midi(roots[bar]), 0.12f, 0.03f, 1.1f, SoundSynth.Wave.Triangle,
                                wrap: true);
            }

            // The tune. Phrase one climbs D5–F5–A5 and settles on G5; phrase two answers it from F5 down to
            // Bb4, then the third phrase turns the same shape minor under F and Gm, and the last bar leads
            // C#5 – E5 – D5 across the loop point. It is meant to be hummable after one pass, which is the
            // only test a menu theme has.
            Step[] tune =
            {
                new Step(0f, 2f, 74),  // D5
                new Step(2f, 2f, 77),  // F5
                new Step(4f, 3f, 81),  // A5
                new Step(7f, 1f, 79),  // G5

                new Step(8f, 2f, 77),  // F5
                new Step(10f, 2f, 74), // D5
                new Step(12f, 3f, 72), // C5
                new Step(15f, 1f, 70), // Bb4

                new Step(16f, 2f, 77), // F5
                new Step(18f, 2f, 81), // A5
                new Step(20f, 2f, 79), // G5
                new Step(22f, 2f, 74), // D5

                new Step(24f, 3f, 73), // C#5 — the leading tone, on the dominant
                new Step(27f, 1f, 76), // E5
                new Step(28f, 4f, 74), // D5 — the tonic, caught by the next loop
            };

            for (int i = 0; i < tune.Length; i++)
            {
                Step note = tune[i];
                float at = note.Beat * beat;

                // Capped: a bell's decay is written as a fraction of its duration, so a five-second ring is a
                // long ring *and* four times the arithmetic. Three and a half seconds is already longer than
                // the next note is away at 60 BPM, and the theme is generated while the menu is opening.
                float ring = Mathf.Min(note.Beats * beat + 1.4f, 3.6f);

                // A little more on a downbeat, so the tune breathes in bars instead of ticking.
                bool downbeat = note.Beat % 4f < 0.001f;
                SoundSynth.Bell(buffer, at, ring, note.Pitch, downbeat ? 0.12f : 0.095f, wrap: true);

                // The room's first answer: the same note a beat and a half late, an octave down, a third as
                // loud, with a slow attack — an answer rather than a repeat, which is why it is a sine and
                // not the bell. The last note answers after the loop point and lands in bar one.
                SoundSynth.Tone(buffer, at + 1.5f * beat, Mathf.Min(ring + 1f, 4.2f), SoundSynth.Midi(note.Pitch - 12),
                                SoundSynth.Midi(note.Pitch - 12), 0.034f, 0.06f, 1.6f, SoundSynth.Wave.Sine,
                                wrap: true, vibrato: 0.002f);

                // The far wall: the same answer again three beats late and a whisper. Two reflections are
                // what turn a copy of the tune into a room with depth.
                SoundSynth.Tone(buffer, at + 3f * beat, Mathf.Min(ring + 1.6f, 4.4f), SoundSynth.Midi(note.Pitch - 12),
                                SoundSynth.Midi(note.Pitch - 12), 0.012f, 0.1f, 2f, SoundSynth.Wave.Sine,
                                wrap: true);
            }

            // A D1 under the first six bars: the register the arc's hum lives in, and the reason the menu
            // and a chapter sound like the same game. It stops for the dominant, so the cadence lifts.
            SoundSynth.Tone(buffer, 0f, 6f * barLength, SoundSynth.Midi(26), SoundSynth.Midi(26), 0.05f, 2.5f,
                            2.5f, SoundSynth.Wave.Sine, wrap: true);

            // Air under everything, so the loop is a place rather than a chord progression on a synth.
            uint seed = 0x1B0DEu;
            SoundSynth.Noise(buffer, 0f, length, 0.022f, 320f, 900f, 3f, 3f, ref seed, wrap: true);

            return SoundSynth.Finish(SoundSynth.CloseSeam(buffer, 0.06f));
        }

        /// <summary>A room: no motif, two drones, two swells of air, and a bell every other bar.</summary>
        public static float[] ChapterBed()
        {
            float barLength = 4f * 60f / ChapterBpm;
            float length = ChapterBars * barLength;
            float[] buffer = SoundSynth.Buffer(length);

            // D1, A1 and D2. Tuned to the buffer's own period by the wrap, so the drone is a hum and not a
            // chord: no third, because a third would make every room sound sad before anything happened.
            SoundSynth.Tone(buffer, 0f, length, SoundSynth.Midi(26), SoundSynth.Midi(26), 0.075f, 3f, 3f,
                            SoundSynth.Wave.Sine, wrap: true);
            SoundSynth.Tone(buffer, 0f, length, SoundSynth.Midi(33), SoundSynth.Midi(33), 0.05f, 4f, 4f,
                            SoundSynth.Wave.Sine, wrap: true);
            SoundSynth.Tone(buffer, 0f, length, SoundSynth.Midi(38), SoundSynth.Midi(38), 0.03f, 5f, 5f,
                            SoundSynth.Wave.Triangle, wrap: true, vibrato: 0.004f);

            // Two swells of filtered air, half a loop apart: the room breathing in and out.
            uint seed = 0x77E1u;
            SoundSynth.Noise(buffer, 0f, length * 0.5f, 0.045f, 300f, 900f, 4f, 6f, ref seed);
            SoundSynth.Noise(buffer, length * 0.5f, length * 0.5f, 0.04f, 700f, 260f, 5f, 7f, ref seed);

            // Something metal, far away, every other bar. Not a landmark yet: the landmark system is M2,
            // and this is the room saying it is not empty.
            for (int bar = 0; bar < ChapterBars; bar += 2)
            {
                SoundSynth.Bell(buffer, bar * barLength + 0.4f, 2.2f, bar % 4 == 0 ? 57 : 60, 0.045f, wrap: true);
            }

            return SoundSynth.Finish(SoundSynth.CloseSeam(buffer, 0.06f));
        }

        /// <summary>
        /// The end of a room: D4 F4 A4 D5, struck upward, with a low D under it. Four seconds, because a
        /// victory screen is read and a two-second sting leaves the player sitting in silence while a map
        /// draws itself.
        /// </summary>
        public static float[] VictorySting()
        {
            float[] buffer = SoundSynth.Buffer(4.2f);
            int[] notes = { 62, 65, 69, 74 };

            for (int i = 0; i < notes.Length; i++)
            {
                SoundSynth.Bell(buffer, i * 0.18f, 3.8f - i * 0.4f, notes[i], 0.15f);
            }

            SoundSynth.Tone(buffer, 0f, 3.6f, SoundSynth.Midi(38), SoundSynth.Midi(38), 0.1f, 0.05f, 3f);

            uint seed = 0x51A9u;
            SoundSynth.Noise(buffer, 0f, 3.4f, 0.035f, 4000f, 900f, 0.3f, 2.6f, ref seed);

            return SoundSynth.Finish(buffer);
        }
    }
}
