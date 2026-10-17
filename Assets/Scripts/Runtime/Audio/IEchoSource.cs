namespace EchoMaze.Audio
{
    /// <summary>
    /// Everything the echo system knows about sound. Swapping the real microphone for the
    /// simulated source is the only difference between a Windows build and Linux development.
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

        /// <summary>Human readable state for the HUD and the debug window, e.g. "mic:default".</summary>
        string Status { get; }
    }
}
