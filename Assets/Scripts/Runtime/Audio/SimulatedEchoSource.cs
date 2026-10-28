using EchoMaze.Data;
using UnityEngine;

namespace EchoMaze.Audio
{
    /// <summary>
    /// Loudness from a tunable curve instead of a microphone. A development and test aid: it drives the
    /// automatic gate on a timer so the ping path can be exercised on a machine with no capture device,
    /// and it is what EchoDirector falls back to when the microphone refuses to open.
    /// </summary>
    public sealed class SimulatedEchoSource : IEchoSource
    {
        readonly EchoTuningProfile profile;
        readonly AnimationCurve curve;
        float clock;
        float lastEvent = float.NegativeInfinity;
        bool edge;

        public SimulatedEchoSource(EchoTuningProfile profile, AnimationCurve curve = null)
        {
            this.profile = profile;
            this.curve = curve;
        }

        public bool Open(string device, int sampleRate) => true;

        public void Close() { }

        public void Poll()
        {
            clock += Time.deltaTime;

            // The simulated source has no voice to gate, so it invents the one thing the director
            // actually consumes: a discrete event every simulatedEventInterval seconds while the
            // profile's constant loudness stays above the threshold. Without it the development path
            // would either ping every frame or, once the director stops re-arming on level, never ping.
            float level = Effective();
            edge = level >= profile.loudnessThreshold &&
                   clock - lastEvent >= profile.simulatedEventInterval;
            if (edge)
            {
                lastEvent = clock;
            }
        }

        public float RawRms => Effective();

        public float Loudness => Effective();

        public bool NewVocalEvent => edge;

        public string Status => "simulated";

        float Effective()
        {
            if (curve != null && curve.length > 1)
            {
                float span = curve[curve.length - 1].time;
                float t = span > 0f ? clock % span : 0f;
                return Mathf.Clamp01(curve.Evaluate(t));
            }

            return Mathf.Clamp01(profile.simulatedLoudness);
        }
    }
}
