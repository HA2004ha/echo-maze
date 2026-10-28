using UnityEngine;

namespace EchoMaze.Audio
{
    /// <summary>
    /// Turns a raw RMS level into the two things the game actually acts on: a gated 0..1 loudness, and
    /// the single frame in which a voice started. Written as a plain class so an EditMode test can feed
    /// it a level script and assert the event count without a microphone, a clock, or Play Mode.
    ///
    /// Hysteresis rather than one threshold, because a voice level hovering at the threshold otherwise
    /// chatters: open, ping, dip, close, open, ping, and the player sees three waves for one syllable.
    /// </summary>
    public sealed class NoiseGate
    {
        readonly float openRms;
        readonly float closeRms;
        readonly float fullRms;
        readonly float holdSeconds;

        float level;
        float hold;

        public NoiseGate(float openRms, float closeRms, float fullRms, float holdSeconds)
        {
            this.openRms = Mathf.Max(0.0001f, openRms);
            // Close below the open threshold or the gate never latches; full above both or a shout
            // cannot reach 1.
            this.closeRms = Mathf.Min(closeRms, this.openRms * 0.85f);
            this.fullRms = Mathf.Max(fullRms, this.openRms * 1.5f);
            this.holdSeconds = Mathf.Max(0f, holdSeconds);
        }

        public bool IsOpen { get; private set; }

        /// <summary>Un-gated level of the last window, for the calibration waveform.</summary>
        public float Raw { get; private set; }

        /// <summary>Gated and held loudness: what the reach and the ray count are driven by.</summary>
        public float Loudness => level;

        /// <summary>
        /// Feed one measured window. Returns true on the rising edge only, so a two second shout that
        /// stays above the threshold is one vocal event, not one per frame.
        /// </summary>
        public bool Update(float rms, float dt)
        {
            Raw = rms;
            bool started = false;

            if (!IsOpen && rms >= openRms)
            {
                IsOpen = true;
                started = true;
            }
            else if (IsOpen && rms < closeRms)
            {
                IsOpen = false;
            }

            if (IsOpen)
            {
                // Attack is immediate: a wave that fades in would lag the sound the player made.
                level = Mathf.Clamp01((rms - closeRms) / (fullRms - closeRms));
                hold = holdSeconds;
            }
            else if (hold > 0f)
            {
                // Release is delayed, so "psst" between two syllables of one word does not re-arm the gate.
                hold -= Mathf.Max(dt, 1e-5f);
            }
            else
            {
                level = 0f;
            }

            return started;
        }

        public void Reset()
        {
            IsOpen = false;
            level = 0f;
            hold = 0f;
            Raw = 0f;
        }
    }
}
