using System;

namespace EchoMaze.Audio
{
    /// <summary>
    /// Which capture device the game listens to, as a rule over a supplied list rather than as a call into
    /// Unity's audio backend.
    ///
    /// The rule has to be testable without a microphone: this project's own test matrix records that the
    /// Linux player auto-selects the PipeWire alias <c>Default Input Device</c>, and no code that reads a
    /// <em>name</em> can tell what an alias currently points at. A machine whose default input is a monitor
    /// would hear its own speakers, so the fallback behaviour when a saved name has gone away is the thing
    /// being specified here, and it is specified in a file that an EditMode test can drive with a list.
    /// </summary>
    public static class MicDeviceChoice
    {
        /// <summary>
        /// "Whatever the machine has". Empty rather than a sentinel word, so a preference key that was never
        /// written and a preference deliberately set back to Auto are the same state.
        /// </summary>
        public const string Auto = "";

        /// <summary>A monitor is our own speaker output looped back into the input.</summary>
        public static bool IsMonitor(string name) =>
            name != null && name.IndexOf("Monitor", StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>
        /// The first entry that is not a monitor. When every input on the machine <em>is</em> a monitor, that
        /// is still the only thing to open — the calibration page then says what it landed on rather than
        /// pretending there is no microphone at all.
        /// </summary>
        public static string AutoSelect(string[] devices)
        {
            if (devices == null || devices.Length == 0)
            {
                return null;
            }

            foreach (string candidate in devices)
            {
                if (!IsMonitor(candidate))
                {
                    return candidate;
                }
            }

            return devices[0];
        }

        /// <summary>
        /// Resolve a saved preference against the list the machine answers with right now. A name that is no
        /// longer in the list is not an error, it is a headset that left the room: the game falls back to
        /// <see cref="AutoSelect"/> and keeps playing, which is the one behaviour that cannot strand a player
        /// with a black screen and a wave that never moves.
        /// </summary>
        public static string Resolve(string[] devices, string preferred)
        {
            if (devices == null || devices.Length == 0)
            {
                return null;
            }

            if (!string.IsNullOrEmpty(preferred))
            {
                foreach (string candidate in devices)
                {
                    if (candidate == preferred)
                    {
                        return candidate;
                    }
                }
            }

            return AutoSelect(devices);
        }
    }
}
