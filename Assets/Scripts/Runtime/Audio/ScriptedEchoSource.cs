namespace EchoMaze.Audio
{
    /// <summary>
    /// Fixed script of loudness samples, one per Poll. Used by EditMode tests so the gate and
    /// the ping cadence can be asserted without a clock or a microphone.
    /// </summary>
    public sealed class ScriptedEchoSource : IEchoSource
    {
        readonly float[] script;
        int index;
        float current;

        public ScriptedEchoSource(float[] script)
        {
            this.script = script ?? new float[0];
        }

        public bool Open(string device, int sampleRate) => true;

        public void Close() { }

        public void Poll()
        {
            if (index < script.Length)
            {
                current = script[index];
                index++;
            }
            else
            {
                current = 0f;
            }
        }

        public float RawRms => current;

        public float Loudness => current;

        public string Status => "scripted";

        public bool Exhausted => index >= script.Length;
    }
}
