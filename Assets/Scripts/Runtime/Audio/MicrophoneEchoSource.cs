using System;
using EchoMaze.Data;
using UnityEngine;

namespace EchoMaze.Audio
{
    /// <summary>
    /// The real microphone. This whole team is on Linux, and the Linux editor was measured on
    /// 2026-10-28 to expose capture devices and return live samples, so this is the primary path.
    /// If a machine genuinely has no input device, EchoDirector falls back to the simulated source
    /// instead of crashing, and ManualOnly keeps working everywhere.
    ///
    /// One RMS window per Poll and one ping per rising edge: the game never needs pitch, phonemes or
    /// speech recognition, only how loud the player was and when they started.
    /// </summary>
    public sealed class MicrophoneEchoSource : IEchoSource
    {
        /// <summary>Seconds of capture Unity keeps in the looped ring. Far more than one window; it is
        /// the margin for a frame that arrives late.</summary>
        const int RingSeconds = 5;

        /// <summary>Length of the analysis window in seconds: about a rendered frame of audio at
        /// 48 kHz, short enough that a shout registers immediately.</summary>
        const float WindowSeconds = 0.05f;

        /// <summary>Seconds of capture to throw away after Open. Measured on Linux: the ring is
        /// uninitialised memory until the capture thread has written past it, and the first window read
        /// back 0.10 RMS of pure noise against a real ambient floor of 0.003. That is loud enough to
        /// open the gate, so the game would ping once at startup before the player made a sound.</summary>
        const float WarmupSeconds = 0.25f;

        readonly EchoTuningProfile profile;
        readonly NoiseGate gate;

        AudioClip clip;
        float[] window;
        string deviceName;
        int ringSamples;
        int lastPosition;
        float warmUntil;
        bool recording;
        bool edge;
        string failure;

        public MicrophoneEchoSource(EchoTuningProfile profile)
        {
            this.profile = profile;
            gate = new NoiseGate(profile.gateOpenRms, profile.gateCloseRms,
                                 profile.gateFullRms, profile.loudnessHold);
        }

        public bool Open(string device, int sampleRate)
        {
            string[] devices = Microphone.devices;
            if (devices == null || devices.Length == 0)
            {
                failure = "no capture device";
                return false;
            }

            string requested = string.IsNullOrEmpty(device) ? profile.microphoneDevice : device;
            deviceName = string.IsNullOrEmpty(requested) ? AutoSelect(devices) : FindDevice(devices, requested);
            if (deviceName == null)
            {
                // Listing what the machine actually has is the whole message a teammate needs: device
                // names differ per distro, per audio server and per headset.
                failure = "device not found: " + requested + " (available: " + string.Join(", ", devices) + ")";
                return false;
            }

            int rate = sampleRate > 0 ? sampleRate : 44100;

            try
            {
                // Looped, so the capture never stops and GetPosition keeps wrapping over one ring.
                // Unity 6 hands back the AudioClip it is recording into; its sample buffer is the ring.
                Microphone.End(deviceName);
                clip = Microphone.Start(deviceName, true, RingSeconds, rate);
                recording = clip != null && Microphone.IsRecording(deviceName);
            }
            catch (System.Exception e)
            {
                recording = false;
                failure = e.Message;
            }

            if (!recording && string.IsNullOrEmpty(failure))
            {
                failure = "did not start";
            }

            ringSamples = recording ? Mathf.Max(16, clip.samples) : 0;
            int channels = recording ? Mathf.Max(1, clip.channels) : 1;

            // Size the window from the rate Unity actually delivered, not the one we asked for: the
            // capture driver is free to answer at the device's native rate, and a window sized against a
            // wrong rate is a wrong number of milliseconds of audio.
            int actualRate = recording ? Mathf.Max(8000, clip.frequency) : rate;
            window = new float[Mathf.Max(16, Mathf.CeilToInt(WindowSeconds * actualRate) * channels)];
            lastPosition = recording ? Microphone.GetPosition(deviceName) : 0;
            warmUntil = Time.time + WarmupSeconds;
            return recording;
        }

        public void Close()
        {
            if (deviceName != null)
            {
                Microphone.End(deviceName);
            }

            clip = null;
            recording = false;
        }

        public void Poll()
        {
            if (!recording)
            {
                return;
            }

            // Cleared every frame: the director reads NewVocalEvent as "this frame only", and a stale
            // true would emit a second wave for one word.
            edge = false;

            if (!Microphone.IsRecording(deviceName) || clip == null)
            {
                // A capture that died mid-session reads to the player as "the game ignores my voice",
                // so say it in the status line instead of waiting for them to notice.
                recording = false;
                failure = "capture stopped";
                gate.Reset();
                return;
            }

            int position = Microphone.GetPosition(deviceName);
            int fresh = position - lastPosition;
            if (fresh < 0)
            {
                fresh += ringSamples;
            }

            // The capture thread advances the ring position in blocks, so a rendered frame can legally
            // see no new audio. Feeding the gate a zero then would be a fake silence: it closes the gate
            // mid-shout, and the player's one vocal event comes back as several pings or none.
            if (fresh <= 0)
            {
                return;
            }

            lastPosition = position;

            if (Time.time < warmUntil)
            {
                return;
            }

            // Never read more than one window: after a pause the ring still holds old audio, and
            // measuring it would report a shout the player stopped seconds ago.
            int take = Mathf.Min(fresh, window.Length);

            int start = position - take;
            while (start < 0)
            {
                start += ringSamples;
            }

            int first = Mathf.Min(take, ringSamples - start);
            clip.GetData(new Span<float>(window, 0, first), start);
            if (take > first)
            {
                // The window crossed the end of the ring: contiguous in time, not in memory.
                clip.GetData(new Span<float>(window, first, take - first), 0);
            }

            edge = gate.Update(Rms(window, take), Time.deltaTime);
        }

        static float Rms(float[] samples, int count)
        {
            if (count <= 0)
            {
                return 0f;
            }

            float sum = 0f;
            for (int i = 0; i < count; i++)
            {
                sum += samples[i] * samples[i];
            }

            return Mathf.Sqrt(sum / count);
        }

        static string FindDevice(string[] devices, string requested)
        {
            foreach (string candidate in devices)
            {
                if (candidate == requested)
                {
                    return candidate;
                }
            }

            return null;
        }

        /// <summary>
        /// The first entry that is not a monitor. A monitor is our own speaker output looped back into
        /// the input, which would make the game answer its own sound effects with waves forever.
        /// Pin the real device with EchoTuningProfile.microphoneDevice when the audio server routes
        /// the default input somewhere else.
        /// </summary>
        static string AutoSelect(string[] devices)
        {
            foreach (string candidate in devices)
            {
                if (candidate.IndexOf("Monitor", System.StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return candidate;
                }
            }

            return devices[0];
        }

        /// <summary>Set once per Poll and read by the director in the same frame.</summary>
        public bool NewVocalEvent => edge;

        public float RawRms => gate.Raw;

        public float Loudness => gate.Loudness;

        public string Status => recording
            ? "mic:" + ShortName(deviceName)
            : "mic offline (" + (failure ?? "not opened") + ")";

        static string ShortName(string name) =>
            string.IsNullOrEmpty(name) ? "?"
            : name.Length <= 18 ? name : name.Substring(0, 18) + "...";
    }
}
