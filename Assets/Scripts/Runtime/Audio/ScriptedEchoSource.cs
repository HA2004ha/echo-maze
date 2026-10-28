using EchoMaze.Data;
using UnityEngine;

namespace EchoMaze.Audio
{
    /// <summary>
    /// Fixed script of raw levels, one per Poll. The gate and the ping cadence are asserted through it
    /// without a microphone or a clock, and from week 4 it is the bot's mouth: the same script drives a
    /// headless route and makes the playtest reproducible.
    /// </summary>
    public sealed class ScriptedEchoSource : IEchoSource
    {
        readonly float[] script;
        readonly NoiseGate gate;
        int index;
        bool edge;

        public ScriptedEchoSource(float[] script, EchoTuningProfile profile)
        {
            this.script = script ?? new float[0];
            gate = new NoiseGate(profile.gateOpenRms, profile.gateCloseRms,
                                 profile.gateFullRms, profile.loudnessHold);
        }

        public bool Open(string device, int sampleRate) => true;

        public void Close() { }

        public void Poll()
        {
            float next = index < script.Length ? script[index] : 0f;
            if (index < script.Length)
            {
                index++;
            }

            edge = gate.Update(next, Time.deltaTime);
        }

        public float RawRms => gate.Raw;

        public float Loudness => gate.Loudness;

        public bool NewVocalEvent => edge;

        public string Status => "scripted";

        public bool Exhausted => index >= script.Length;
    }
}
