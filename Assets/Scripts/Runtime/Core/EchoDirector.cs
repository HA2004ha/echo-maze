using System.Collections.Generic;
using EchoMaze.Audio;
using EchoMaze.Data;
using UnityEngine;

namespace EchoMaze.Core
{
    /// <summary>
    /// Owns the ping pool: polls the sound source, runs the ray fan once per emitted sound and
    /// advances the arc train every frame. Everything is origin-anchored in world space, so the
    /// player transform is never needed here beyond the pose reported by SetPose.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EchoDirector : MonoBehaviour
    {
        /// <summary>
        /// Where the loudness that drives the automatic gate comes from. The numbers below are the
        /// values stored in scenes, so ManualOnly keeps 0: Sandbox.unity already saves 0 and needs
        /// no edit.
        /// </summary>
        public enum SourceMode
        {
            /// <summary>No sound source at all. Space and ForcePing are the only pings: the development path until the microphone lands.</summary>
            ManualOnly = 0,
            /// <summary>Constant loudness from the profile, fed through the automatic gate so the microphone path can be exercised without a microphone.</summary>
            Simulated = 1
        }

        [SerializeField] EchoTuningProfile profile;
        [SerializeField] SourceMode sourceMode = SourceMode.ManualOnly;
        [SerializeField] LayerMask echoLayerMask = ~0;
        [SerializeField] EchoRenderer fieldRenderer;

        readonly List<PingRecord> pool = new List<PingRecord>(4);

        IEchoSource source;
        ContactFilter2D echoFilter;
        RaycastHit2D[] rayBuffer;
        Vector2 pose;
        float aimRad;
        float lastAutoPingTime = float.NegativeInfinity;
        int seedCounter = 0x5eed1;

        public IEchoSource Source => source;
        public string SourceStatus => source != null ? source.Status : "none";
        public float CurrentLoudness => source != null ? source.Loudness : 0f;
        public float RawLoudness => source != null ? source.RawRms : 0f;
        public int LivePingCount { get; private set; }
        public int LastRayCount { get; private set; }

        /// <summary>
        /// Loudness of the Space fallback, read from the profile so the tuning asset stays the only
        /// source of truth for how loud a manual echo is.
        /// </summary>
        public float ManualEchoLoudness => profile.manualEchoLoudness;

        void Awake()
        {
            if (profile == null)
            {
                Debug.LogWarning("EchoDirector has no EchoTuningProfile assigned; using code defaults. " +
                                 "Create Assets/Data/Echo/EchoTuning.asset to tune the wave.");
                profile = ScriptableObject.CreateInstance<EchoTuningProfile>();
            }

            rayBuffer = new RaycastHit2D[profile.CapacityRays];
            echoFilter = RayFan.BuildFilter(echoLayerMask);
            for (int i = 0; i < profile.maxLivePings; i++)
            {
                pool.Add(PingRecord.CreatePooled(profile.CapacityRays));
            }

            source = CreateSource();
            source?.Open(null, 0);

            if (fieldRenderer == null)
            {
                fieldRenderer = FindAnyObjectByType<EchoRenderer>();
            }
        }

        void OnDestroy()
        {
            source?.Close();
        }

        public void SetPose(Vector2 worldOrigin, float aimRadians)
        {
            pose = worldOrigin;
            aimRad = aimRadians;
        }

        void Update()
        {
            source?.Poll();
            DriveAutoPing();
            AdvanceArcs();
        }

        void DriveAutoPing()
        {
            if (source == null || profile.loudnessThreshold <= 0f)
            {
                return;
            }

            float loudness = source.Loudness;
            if (loudness < profile.loudnessThreshold)
            {
                return;
            }

            if (Time.time - lastAutoPingTime < profile.minPingInterval)
            {
                return;
            }

            lastAutoPingTime = Time.time;
            EmitPing(loudness, pose, aimRad);
        }

        /// <summary>
        /// The single entry point for every non-microphone sound: Space, the decoy marble, the
        /// weak pulse and the scream on damage all come through here, so they share one code path
        /// and one set of readability rules.
        /// </summary>
        public void ForcePing(float loudness) => ForcePing(loudness, pose, aimRad);

        public void ForcePing(float loudness, Vector2 origin, float aim)
        {
            EmitPing(Mathf.Clamp01(loudness), origin, aim);
        }

        void EmitPing(float loudness, Vector2 origin, float aim)
        {
            PingRecord record = AcquireRecord();
            float radius = ArcMath.PingRadius(loudness, profile);
            int rays = ArcMath.RayCount(loudness, profile);

            record.Begin(origin, loudness, aim, profile.ConeHalfRad, radius, Time.time, rays, seedCounter++);
            RayFan.Cast(record, echoFilter, rayBuffer);
            LastRayCount = rays;
        }

        PingRecord AcquireRecord()
        {
            foreach (PingRecord candidate in pool)
            {
                if (!candidate.alive)
                {
                    return candidate;
                }
            }

            // Pool full: the oldest ping is dropped rather than growing the pool, because three
            // overlapping wave fields is already more than a player can read.
            PingRecord oldest = pool[0];
            foreach (PingRecord candidate in pool)
            {
                if (candidate.emitTime < oldest.emitTime)
                {
                    oldest = candidate;
                }
            }

            return oldest;
        }

        void AdvanceArcs()
        {
            IEchoRenderer renderer = fieldRenderer;
            LivePingCount = 0;

            if (renderer != null)
            {
                renderer.BeginFrame();
            }

            foreach (PingRecord ping in pool)
            {
                if (!ping.alive)
                {
                    continue;
                }

                float age = Time.time - ping.emitTime;
                float baseRadius = ArcMath.BaseRadius(ping.radius, profile.waveSpeed, age);

                if (!ArcMath.IsAlive(baseRadius, profile.rKill))
                {
                    ping.alive = false;
                    continue;
                }

                float alpha = ArcMath.Alpha(age, ping.LifeSeconds(profile.waveSpeed, profile.rKill),
                                           profile.alphaCurve);
                renderer?.AddArc(ping, 0, baseRadius, alpha, profile);

                if (ArcMath.HasEmissionPulse(age, profile))
                {
                    renderer?.AddConeOutline(ping.origin, ping.aimAngleRad, ping.coneHalfRad,
                                             ArcMath.EmissionRadius(age, profile.waveSpeed, baseRadius),
                                             ArcMath.EmissionAlpha(age, profile), profile);
                }

                LivePingCount++;
            }

            renderer?.EndFrame();
        }

        IEchoSource CreateSource()
        {
            switch (sourceMode)
            {
                case SourceMode.Simulated:
                    return new SimulatedEchoSource(profile);
                default:
                    return null;
            }
        }

#if UNITY_EDITOR
        /// <summary>Used by the debug window to rebind without a domain reload.</summary>
        public void UseSource(IEchoSource next)
        {
            source?.Close();
            source = next;
            source?.Open(null, 0);
        }
#endif
    }
}
