namespace EchoMaze.Audio
{
    /// <summary>
    /// Everything the echo system knows about sound. Swapping the real microphone for the simulated
    /// or scripted source is what lets the wave be tuned and tested without a capture device.
    /// </summary>
    public interface IEchoSource
    {
        bool Open(string device, int sampleRate);

        void Close();

        /// <summary>Called once per frame from the director.</summary>
        void Poll();

        /// <summary>Un-gated level, for the calibration waveform.</summary>
        float RawRms { get; }

        /// <summary>Gated and smoothed 0..1 loudness that decides whether a ping fires.</summary>
        float Loudness { get; }

        /// <summary>
        /// True on exactly one Poll per vocal event: the moment the voice rises through the gate. The
        /// director pings on this and not on a level threshold, because a level threshold re-arms every
        /// cooldown and one shouted word comes back as four overlapping wave fields.
        /// </summary>
        bool NewVocalEvent { get; }

        /// <summary>Human readable state for the HUD and the debug window, e.g. "mic:default".</summary>
        string Status { get; }
    }
}
