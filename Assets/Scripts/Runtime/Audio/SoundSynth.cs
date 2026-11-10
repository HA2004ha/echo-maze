using UnityEngine;

namespace EchoMaze.Audio
{
    /// <summary>
    /// The whole non-musical palette, synthesised from arithmetic instead of shipped as audio files.
    ///
    /// Three reasons this is code and not a folder of wavs. The game has no art department and no budget
    /// for a sound library; the sounds it needs are mostly *information* (a door, a footstep, a floor of
    /// glass) rather than performances; and a palette in code is reviewable, diffable and testable in batch
    /// mode, where there is no audio device to listen with. The sound designer's clips take precedence the
    /// moment they exist: <see cref="SoundBoard"/> plays a file dropped at
    /// <c>Assets/Audio/&lt;SoundId&gt;.wav</c> instead of the recipe here (see <see cref="SoundCatalog.AssetPath"/>).
    ///
    /// Everything is mono, 44.1 kHz and in [-1, 1]. Sample generation is a pure function of the id — no
    /// Unity object, no device, no clock — so the EditMode suite can read the samples and assert that a
    /// sound is audible, finite, and short, which is the only part of "does it sound right" a machine can
    /// answer. Whether it *sounds* right is still a person with headphones, and the GDD says so.
    /// </summary>
    public static class SoundSynth
    {
        public const int SampleRate = 44100;

        /// <summary>The waves a voice can be built from. Additive enough for a game about echoes.</summary>
        internal enum Wave
        {
            Sine,
            Saw,
            Triangle,
        }

        /// <summary>
        /// The samples for one sound, before Unity sees them. The length of the array is the sound's length,
        /// so duration is never a second constant that can disagree with the buffer.
        /// </summary>
        public static float[] SampleBuffer(SoundId id)
        {
            switch (id)
            {
                // -- the interface ---------------------------------------------------------------
                case SoundId.UiHover: return UiHover();
                case SoundId.UiClick: return UiClick();
                case SoundId.UiBack: return UiBack();

                // -- the body and the floor ------------------------------------------------------
                case SoundId.Footstep: return Footstep();
                case SoundId.GlassStep: return GlassStep();
                case SoundId.Hint: return Hint();

                // -- the wave and what it finds ---------------------------------------------------
                case SoundId.PingEcho: return PingEcho();
                case SoundId.DoorOpen: return DoorOpen();
                case SoundId.DoorClose: return DoorClose();
                case SoundId.LampPlace: return LampPlace();
                case SoundId.Sanctuary: return Sanctuary();

                // -- what is hunting you ----------------------------------------------------------
                case SoundId.Alert: return Alert();
                case SoundId.GiveUp: return GiveUp();
                case SoundId.Heartbeat: return Heartbeat();

                // -- music ------------------------------------------------------------------------
                case SoundId.MenuTheme: return MusicScore.MenuTheme();
                case SoundId.ChapterBed: return MusicScore.ChapterBed();
                case SoundId.VictorySting: return MusicScore.VictorySting();

                default:
                    // A new SoundId with no recipe is silent, which is a bug the test suite reports rather
                    // than a crash in a level nobody was watching.
                    return Buffer(0.05f);
            }
        }

        /// <summary>
        /// The clip the game plays for this sound. Built on demand and cached by <see cref="SoundBoard"/>:
        /// the menu theme is about 32 seconds of audio, and generating it in <c>Awake</c> for a test that
        /// never plays anything would be a stall for nothing.
        /// </summary>
        public static AudioClip Build(SoundId id)
        {
            float[] samples = SampleBuffer(id);
            AudioClip clip = AudioClip.Create(id.ToString(), samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        // ---------------------------------------------------------------------------------------
        // How loud and how high the wave comes back. Loudness is the GDD's trade made audible: a tap
        // costs little and returns a small sound, a shout fills the room and comes back lower and louder.
        // ---------------------------------------------------------------------------------------

        /// <summary>Volume of the returning echo, from the ping's own loudness.</summary>
        public static float PingEchoVolume(float loudness) =>
            Mathf.Lerp(0.28f, 0.85f, Mathf.Clamp01((loudness - 0.15f) / 0.85f));

        /// <summary>
        /// Pitch of the returning echo: a quiet tap comes back bright and high, a shout comes back low and
        /// heavy. Falling pitch with rising loudness is the opposite of what a UI would do, and it is what
        /// makes a shout feel physically expensive.
        /// </summary>
        public static float PingEchoPitch(float loudness) =>
            Mathf.Lerp(1.2f, 0.85f, Mathf.Clamp01((loudness - 0.15f) / 0.85f));

        // ---------------------------------------------------------------------------------------
        // Recipes. Each one is a few voices summed; the comment says what the player is meant to
        // understand from it, because that is the only reason it exists.
        // ---------------------------------------------------------------------------------------

        /// <summary>A cursor crossing a line: the smallest possible confirmation, gone in 70 ms.</summary>
        static float[] UiHover()
        {
            float[] buffer = Buffer(0.08f);
            Tone(buffer, 0f, 0.075f, 1350f, 1650f, 0.16f, 0.004f, 0.05f);
            return Finish(buffer);
        }

        /// <summary>A button taking the press: a tick with a little air behind it, so it cuts through a room.</summary>
        static float[] UiClick()
        {
            float[] buffer = Buffer(0.13f);
            Tone(buffer, 0f, 0.06f, 760f, 620f, 0.3f, 0.002f, 0.05f);
            uint seed = 0x51CEu;
            Noise(buffer, 0f, 0.05f, 0.22f, 5200f, 2600f, 0.001f, 0.045f, ref seed);
            return Finish(buffer);
        }

        /// <summary>Going back a page: the same tick, falling.</summary>
        static float[] UiBack()
        {
            float[] buffer = Buffer(0.2f);
            Tone(buffer, 0f, 0.19f, 560f, 280f, 0.26f, 0.004f, 0.15f);
            return Finish(buffer);
        }

        /// <summary>
        /// One step: mostly the floor, a little of the body. Quiet on purpose — the step is the price of
        /// moving, and it is already paid in loudness to the director; this is the receipt.
        /// </summary>
        static float[] Footstep()
        {
            float[] buffer = Buffer(0.15f);
            uint seed = 0x2F11u;
            Noise(buffer, 0f, 0.13f, 0.5f, 320f, 180f, 0.002f, 0.12f, ref seed);
            Tone(buffer, 0f, 0.09f, 86f, 62f, 0.22f, 0.002f, 0.08f);
            return Finish(buffer);
        }

        /// <summary>Glass: the same step, but the floor answers with splinters and everyone hears it.</summary>
        static float[] GlassStep()
        {
            float[] buffer = Buffer(0.26f);
            uint seed = 0x77AA13u;
            Noise(buffer, 0f, 0.2f, 0.34f, 7200f, 4200f, 0.001f, 0.19f, ref seed);
            Tone(buffer, 0.01f, 0.08f, 2650f, 2400f, 0.06f, 0.001f, 0.07f);
            Tone(buffer, 0.05f, 0.09f, 3100f, 2900f, 0.045f, 0.001f, 0.08f);
            return Finish(buffer);
        }

        /// <summary>
        /// A hint arriving on the HUD. Two soft bell notes a fourth apart, neither of them urgent: the
        /// tutorial line is help, not an alarm, and the game already has an alarm.
        /// </summary>
        static float[] Hint()
        {
            float[] buffer = Buffer(0.5f);
            Bell(buffer, 0f, 0.45f, 76, 0.16f);
            Bell(buffer, 0.14f, 0.4f, 83, 0.11f);
            return Finish(buffer);
        }

        /// <summary>
        /// The wave coming back. A falling sweep is the arc train's arrival in the ear, and the noise tail
        /// behind it is the room: a hard room answers with more of it than a soft one will once surfaces
        /// drive this (the profile is not consulted here yet).
        /// </summary>
        static float[] PingEcho()
        {
            float[] buffer = Buffer(0.85f);
            Tone(buffer, 0f, 0.5f, 980f, 430f, 0.3f, 0.006f, 0.45f);
            Bell(buffer, 0.06f, 0.7f, 69, 0.06f);
            uint seed = 0x19C3u;
            Noise(buffer, 0.12f, 0.6f, 0.1f, 2200f, 900f, 0.05f, 0.5f, ref seed);
            return Finish(buffer);
        }

        /// <summary>An opening door: a long creak that ends in the leaf arriving, which is the loud half.</summary>
        static float[] DoorOpen()
        {
            float[] buffer = Buffer(1.1f);
            uint seed = 0x4B3Fu;
            Noise(buffer, 0f, 0.95f, 0.3f, 700f, 240f, 0.08f, 0.7f, ref seed);
            Tone(buffer, 0.72f, 0.3f, 74f, 52f, 0.26f, 0.004f, 0.26f);
            return Finish(buffer);
        }

        /// <summary>
        /// A closing door: one heavy hit. It is the louder of the two by design — closing is the move the
        /// player makes while being hunted, and the profile charges it more loudness for the same reason.
        /// </summary>
        static float[] DoorClose()
        {
            float[] buffer = Buffer(0.6f);
            Tone(buffer, 0f, 0.4f, 68f, 44f, 0.48f, 0.002f, 0.36f);
            uint seed = 0x9D17u;
            Noise(buffer, 0f, 0.35f, 0.28f, 1600f, 500f, 0.001f, 0.3f, ref seed);
            return Finish(buffer);
        }

        /// <summary>A lamp set down: a warm bell and a small breath of dust. Light makes no noise — this is the hand.</summary>
        static float[] LampPlace()
        {
            float[] buffer = Buffer(0.6f);
            Bell(buffer, 0f, 0.55f, 74, 0.18f);
            Bell(buffer, 0.02f, 0.5f, 81, 0.1f);
            uint seed = 0x3A51u;
            Noise(buffer, 0f, 0.16f, 0.1f, 1400f, 600f, 0.002f, 0.14f, ref seed);
            return Finish(buffer);
        }

        /// <summary>A checkpoint taken: a rising fifth that resolves rather than alerts. Safe is not quiet.</summary>
        static float[] Sanctuary()
        {
            float[] buffer = Buffer(1.4f);
            Tone(buffer, 0f, 1.3f, 392f, 440f, 0.14f, 0.25f, 0.9f);
            Tone(buffer, 0.08f, 1.25f, 587f, 660f, 0.1f, 0.3f, 0.9f);
            Bell(buffer, 0.3f, 1f, 81, 0.06f);
            return Finish(buffer);
        }

        /// <summary>
        /// Something found you. The GDD asks for half a second of telegraph before a chase: this is that
        /// half second, and it is the only deliberately ugly sound in the palette.
        /// </summary>
        static float[] Alert()
        {
            float[] buffer = Buffer(0.95f);
            Tone(buffer, 0f, 0.32f, 190f, 260f, 0.24f, 0.01f, 0.12f, Wave.Saw);
            Tone(buffer, 0.22f, 0.6f, 300f, 470f, 0.26f, 0.02f, 0.5f, Wave.Saw);
            uint seed = 0x2B73u;
            Noise(buffer, 0f, 0.5f, 0.1f, 2600f, 1200f, 0.02f, 0.45f, ref seed);
            return Finish(buffer);
        }

        /// <summary>It lost you: the alert falling away instead of stopping dead, so the relief is audible.</summary>
        static float[] GiveUp()
        {
            float[] buffer = Buffer(0.95f);
            Tone(buffer, 0f, 0.85f, 320f, 110f, 0.24f, 0.02f, 0.7f, Wave.Saw);
            return Finish(buffer);
        }

        /// <summary>
        /// Two beats of a heart, in 1.3 s, quiet at both ends so the loop point is in silence. Driven by
        /// <see cref="SoundBoard"/>: the nearer and longer the hunt, the louder it gets.
        /// </summary>
        static float[] Heartbeat()
        {
            float[] buffer = Buffer(1.3f);
            Thump(buffer, 0.02f, 0.16f, 62f, 0.5f);
            Thump(buffer, 0.3f, 0.2f, 54f, 0.36f);
            return Finish(buffer);
        }

        // ---------------------------------------------------------------------------------------
        // Voices. Every writer adds into a buffer, so two sounds can be summed without a mixer.
        // ---------------------------------------------------------------------------------------

        /// <summary>A buffer of the given length, silent.</summary>
        internal static float[] Buffer(float seconds) =>
            new float[Mathf.Max(1, Mathf.RoundToInt(seconds * SampleRate))];

        /// <summary>Equal temperament from A4 = 440, so the music can be written as note numbers.</summary>
        internal static float Midi(int note) => 440f * Mathf.Pow(2f, (note - 69) / 12f);

        /// <summary>
        /// An oscillator with a frequency sweep and a linear attack/release. <paramref name="wrap"/> writes
        /// past the end back to the start, which is how a release tail that outlives a bar stays inside a
        /// looping buffer instead of being cut at the loop point.
        /// </summary>
        internal static void Tone(float[] buffer, float start, float duration, float fromHz, float toHz,
                                  float amplitude, float attack, float release, Wave wave = Wave.Sine,
                                  bool wrap = false, float vibrato = 0f)
        {
            int first = Mathf.RoundToInt(start * SampleRate);
            int count = Mathf.RoundToInt(duration * SampleRate);
            float phase = 0f;

            for (int i = 0; i < count; i++)
            {
                float u = count <= 1 ? 0f : (float)i / (count - 1);
                float hz = Mathf.Lerp(fromHz, toHz, u);
                if (vibrato > 0f)
                {
                    hz *= 1f + vibrato * Mathf.Sin(2f * Mathf.PI * 5.2f * i / SampleRate);
                }

                phase += hz / SampleRate;
                phase -= Mathf.Floor(phase);

                float envelope = Envelope(i, count, attack, release);
                Add(buffer, first + i, amplitude * envelope * Osc(wave, phase), wrap);
            }
        }

        /// <summary>
        /// A struck metal tone: three partials with an exponential decay. Used for anything the player reads
        /// as a *thing* rather than a voice — hints, lamps, a checkpoint, the way out.
        /// </summary>
        internal static void Bell(float[] buffer, float start, float duration, int note, float amplitude,
                                  bool wrap = false)
        {
            int first = Mathf.RoundToInt(start * SampleRate);
            int count = Mathf.RoundToInt(duration * SampleRate);
            float hz = Midi(note);

            for (int i = 0; i < count; i++)
            {
                float t = (float)i / SampleRate;
                float envelope = Mathf.Exp(-4.2f * t / Mathf.Max(0.05f, duration)) *
                                 Mathf.Min(1f, t / 0.004f);
                float value = Mathf.Sin(2f * Mathf.PI * hz * t)
                              + 0.45f * Mathf.Sin(2f * Mathf.PI * 2.01f * hz * t)
                              + 0.22f * Mathf.Sin(2f * Mathf.PI * 3.02f * hz * t);
                Add(buffer, first + i, amplitude * envelope * value, wrap);
            }
        }

        /// <summary>A heart thump: a low sine that dies faster than the ear expects, which is what makes it a body.</summary>
        internal static void Thump(float[] buffer, float start, float duration, float hz, float amplitude)
        {
            int first = Mathf.RoundToInt(start * SampleRate);
            int count = Mathf.RoundToInt(duration * SampleRate);

            for (int i = 0; i < count; i++)
            {
                float t = (float)i / SampleRate;
                float envelope = Mathf.Exp(-18f * t) * Mathf.Min(1f, t / 0.004f);
                Add(buffer, first + i, amplitude * envelope * Mathf.Sin(2f * Mathf.PI * hz * t), false);
            }
        }

        /// <summary>
        /// White noise through a one-pole low-pass whose cutoff sweeps; the workhorse of creaks, steps,
        /// glass and air. <paramref name="seed"/> is threaded through so a loop rendered twice is identical,
        /// which is what lets the seam be a deliberate crossfade rather than a different noise every run.
        /// </summary>
        internal static void Noise(float[] buffer, float start, float duration, float amplitude,
                                   float cutoffFrom, float cutoffTo, float attack, float release,
                                   ref uint seed, bool wrap = false)
        {
            int first = Mathf.RoundToInt(start * SampleRate);
            int count = Mathf.RoundToInt(duration * SampleRate);
            float lowPass = 0f;

            for (int i = 0; i < count; i++)
            {
                float u = count <= 1 ? 0f : (float)i / (count - 1);
                float cutoff = Mathf.Clamp(Mathf.Lerp(cutoffFrom, cutoffTo, u), 20f, SampleRate * 0.45f);
                float coefficient = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / SampleRate);

                float white = NextFloat(ref seed) * 2f - 1f;
                lowPass += coefficient * (white - lowPass);

                float envelope = Envelope(i, count, attack, release);
                Add(buffer, first + i, amplitude * envelope * lowPass, wrap);
            }
        }

        /// <summary>
        /// Adds past the end wrap back to the start when <paramref name="wrap"/> is set, and are dropped
        /// when it is not, which is what keeps a one-shot from being heard again at the beginning.
        /// </summary>
        internal static void Add(float[] buffer, int index, float value, bool wrap)
        {
            if (wrap)
            {
                index %= buffer.Length;
                if (index < 0)
                {
                    index += buffer.Length;
                }
            }
            else if (index < 0 || index >= buffer.Length)
            {
                return;
            }

            buffer[index] += value;
        }

        /// <summary>
        /// Makes a rendered loop join itself: the samples just past the loop point are crossfaded into the
        /// head and dropped. Without this, the noise in a room's bed clicks once every loop — one sample's
        /// jump is a transient, and a transient in a quiet room is the loudest thing in it.
        /// </summary>
        internal static float[] CloseSeam(float[] buffer, float seconds)
        {
            int fade = Mathf.Clamp(Mathf.RoundToInt(seconds * SampleRate), 1, buffer.Length / 3);
            int keep = buffer.Length - fade;
            var closed = new float[keep];

            for (int i = 0; i < keep; i++)
            {
                closed[i] = buffer[i];
            }

            for (int i = 0; i < fade; i++)
            {
                float u = (float)i / fade;
                closed[i] = buffer[i] * u + buffer[keep + i] * (1f - u);
            }

            return closed;
        }

        /// <summary>The last thing every recipe does: keep the samples inside what a speaker can play.</summary>
        internal static float[] Finish(float[] buffer)
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] = Mathf.Clamp(buffer[i], -1f, 1f);
            }

            return buffer;
        }

        static float Envelope(int index, int count, float attack, float release)
        {
            float attackSamples = Mathf.Max(1f, attack * SampleRate);
            float releaseSamples = Mathf.Max(1f, release * SampleRate);

            float value = 1f;
            if (index < attackSamples)
            {
                value = index / attackSamples;
            }

            float fromEnd = count - 1 - index;
            if (fromEnd < releaseSamples)
            {
                value = Mathf.Min(value, fromEnd / releaseSamples);
            }

            return Mathf.Clamp01(value);
        }

        static float Osc(Wave wave, float phase)
        {
            switch (wave)
            {
                case Wave.Saw: return 2f * phase - 1f;
                case Wave.Triangle: return phase < 0.5f ? 4f * phase - 1f : 3f - 4f * phase;
                default: return Mathf.Sin(2f * Mathf.PI * phase);
            }
        }

        /// <summary>xorshift32, so the noise is a pure function of the seed and not of wall-clock state.</summary>
        static float NextFloat(ref uint seed)
        {
            seed ^= seed << 13;
            seed ^= seed >> 17;
            seed ^= seed << 5;
            return (seed & 0xFFFFFFu) / 16777215f;
        }
    }
}
