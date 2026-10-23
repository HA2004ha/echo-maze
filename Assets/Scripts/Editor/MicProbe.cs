using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace EchoMaze.EditorTools
{
    /// <summary>
    /// Answers one question per machine: does Unity's Microphone class see anything here at all.
    /// The echo game cannot be judged without it, and the answer is not the same on Windows,
    /// macOS and Linux, so it is printed rather than assumed.
    /// </summary>
    public static class MicProbe
    {
        [MenuItem("Tools/Echo Maze/Probe Microphone")]
        public static void Probe() => EditorUtility.DisplayDialog("Microphone probe", ProbeToLog(), "Close");

        /// <summary>Entry point for -executeMethod, which needs a static void.</summary>
        public static void RunHeadless() => ProbeToLog();

        /// <summary>Safe from -batchmode too, which is how the team checks a machine they cannot see.</summary>
        public static string ProbeToLog()
        {
            var report = new StringBuilder();
            string[] devices = null;

            try
            {
                devices = Microphone.devices;
            }
            catch (System.Exception e)
            {
                report.AppendLine("THREW  Microphone.devices: " + e.GetType().Name + " " + e.Message);
            }

            report.AppendLine("platform      " + Application.platform + " on " + SystemInfo.operatingSystem);
            report.AppendLine("unity version " + Application.unityVersion);
            report.AppendLine("devices       " + (devices == null ? "unreadable" : devices.Length.ToString()));

            if (devices != null)
            {
                foreach (string device in devices)
                {
                    report.AppendLine("  · " + device);
                    try
                    {
                        Microphone.GetDeviceCaps(device, out int min, out int max);
                        report.AppendLine("      sample rate " + min + ".." + max +
                                          ", position " + Microphone.GetPosition(device));
                    }
                    catch (System.Exception e)
                    {
                        report.AppendLine("      caps failed: " + e.Message);
                    }
                }
            }

            report.AppendLine("output sample rate " + AudioSettings.outputSampleRate +
                              ", speaker mode " + AudioSettings.speakerMode);
            string text = report.ToString();
            Debug.Log("Echo Maze microphone probe\n" + text);
            return text;
        }
    }
}
