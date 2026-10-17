using EchoMaze.Data;
using UnityEngine;

namespace EchoMaze.Audio
{
    /// <summary>
    /// Loudness from a tunable curve instead of a microphone. This is the normal development
    /// path on Linux, where Unity editor microphone support cannot be relied on.
    /// </summary>
    public sealed class SimulatedEchoSource : IEchoSource
    {
        readonly EchoTuningProfile profile;
        readonly AnimationCurve curve;
        float clock;

        public SimulatedEchoSource(EchoTuningProfile profile, AnimationCurve curve = null)
        {
            this.profile = profile;
            this.curve = curve;
        }

        public bool Open(string device, int sampleRate) => true;

        public void Close() { }

        public void Poll() => clock += Time.deltaTime;

        public float RawRms => Effective();

        public float Loudness => Effective();

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
