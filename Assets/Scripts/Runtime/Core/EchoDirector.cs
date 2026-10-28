using System;
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
        /// values stored in scenes, so ManualOnly keeps 0 and the shipped Sandbox saves 2.
        /// </summary>
        public enum SourceMode
        {
            /// <summary>No sound source at all. Space and ForcePing are the only pings: the development path until the microphone lands.</summary>
            ManualOnly = 0,
            /// <summary>Constant loudness from the profile, fed through the automatic gate so the microphone path can be exercised without a microphone.</summary>
            Simulated = 1,
            /// <summary>The real capture device. The delivery path on Linux. Falls back to Simulated with a
            /// Console warning on a machine that has no usable input, which is the case worth handling.</summary>
            Microphone = 2
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
        float nextReadyTime;
        float lastWeakPulseTime = float.NegativeInfinity;
        bool poolEmptyWarned;
        int seedCounter = 0x5eed1;

        /// <summary>
        /// Raised once per measured ping. Week 2's Crawler listens to this; week 3 replaces it with the
        /// NoiseBoard, which is the same information with walls, doors and noise zones applied to it.
        /// </summary>
        public event Action<PingRecord> PingEmitted;

        /// <summary>
        /// Every sound the world makes, whether or not it draws a wave: a ping, a footstep, a wall bump.
        /// Position and loudness, nothing else. This is the seam week 3's NoiseBoard plugs into (walls,
        /// closed doors, noise zones and the suspicion meter all hang off it); until then the Crawler
        /// hears the straight-line rule in ArcMath.HearingRadius.
        /// </summary>
        public event Action<Vector2, float> NoiseEmitted;

        public IEchoSource Source => source;

        /// <summary>The tuning asset in force, so the debug window and tests read the same numbers the director does.</summary>
        public EchoTuningProfile Profile => profile;
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

        /// <summary>False while the recovery from the last ping is still being paid.</summary>
        public bool CanPing => Time.time >= nextReadyTime;

        /// <summary>Seconds of recovery left, for the HUD cooldown ring.</summary>
        public float CooldownRemaining => Mathf.Max(0f, nextReadyTime - Time.time);

        void Awake()
        {
            if (profile == null)
            {
                Debug.LogWarning("EchoDirector has no EchoTuningProfile assigned; using code defaults. " +
                                 "Create Assets/Data/Echo/EchoTuning.asset to tune the wave.");
                profile = ScriptableObject.CreateInstance<EchoTuningProfile>();
            }

            BuildRuntimeState();

            source = CreateSource();
            if (source != null && !source.Open(null, 0))
            {
                ReportSourceFailure(source);
            }

            if (fieldRenderer == null)
            {
                fieldRenderer = FindAnyObjectByType<EchoRenderer>();
            }
        }

        /// <summary>
        /// Everything sized from the tuning asset: the shared ray buffer and the ping pool. Split out of
        /// Awake so a swapped profile can rebuild it without a domain reload.
        /// </summary>
        void BuildRuntimeState()
        {
            rayBuffer = new RaycastHit2D[profile.CapacityRays];
            echoFilter = RayFan.BuildFilter(echoLayerMask);
            pool.Clear();
            for (int i = 0; i < Mathf.Max(1, profile.maxLivePings); i++)
            {
                pool.Add(PingRecord.CreatePooled(profile.CapacityRays));
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

            // One vocal event, one wave. The gate decides when a voice started; re-arming on a level
            // threshold every minPingInterval was week 1's defect 6, where one shouted word came back
            // as four overlapping arc fields and nothing could be read in any of them.
            if (!source.NewVocalEvent)
            {
                return;
            }

            float loudness = source.Loudness;
            if (loudness < profile.loudnessThreshold || !CanPing)
            {
                return;
            }

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

        /// <summary>
        /// A sound that draws no wave: a footstep, a wall bump, later the decoy marble and doors. It costs
        /// no cooldown and reveals nothing to its maker, it only travels.
        /// </summary>
        public void ReportNoise(Vector2 position, float loudness)
        {
            NoiseEmitted?.Invoke(position, Mathf.Clamp01(loudness));
        }

        void EmitPing(float loudness, Vector2 origin, float aim)
        {
            Emit(loudness, origin, aim, ArcMath.PingRadius(loudness, profile),
                 ArcMath.RayCount(loudness, profile), profile.ConeHalfRad, false,
                 profile.cooldownBase + profile.cooldownPerLoudness * loudness);
        }

        /// <summary>
        /// The weak pulse: a full circle measured close around the player, so the player can find out
        /// whether something is standing behind them without shouting and giving away their position.
        /// It pays its own short cooldown and none of the loud one, which is the whole reason to press F.
        /// </summary>
        public bool TryWeakPulse()
        {
            if (Time.time - lastWeakPulseTime < profile.weakPulseCooldown)
            {
                return false;
            }

            lastWeakPulseTime = Time.time;
            Emit(0f, pose, aimRad, profile.weakPulseRadius, profile.weakPulseRays | 1, Mathf.PI, true, 0f);
            return true;
        }

        /// <summary>Seconds until the weak pulse is available again, for the HUD.</summary>
        public float WeakPulseRemaining =>
            Mathf.Max(0f, lastWeakPulseTime + profile.weakPulseCooldown - Time.time);

        void Emit(float loudness, Vector2 origin, float aim, float radius, int rays, float coneHalf,
                 bool closedRing, float cooldown)
        {
            PingRecord record = AcquireRecord();
            if (record == null)
            {
                return;
            }

            record.Begin(origin, loudness, aim, coneHalf, radius, Time.time, rays, seedCounter++, closedRing);
            RayFan.Cast(record, echoFilter, rayBuffer);
            SurfaceRead.Compute(record, profile);
            LastRayCount = rays;

            // Every loud ping pays for itself, and the cost scales with the loudness: that is what makes
            // the signal costly, which is the whole game theory of being heard.
            nextReadyTime = Mathf.Max(nextReadyTime, Time.time + cooldown);

            PingEmitted?.Invoke(record);

            // A wave is always also a noise. The weak pulse is the quietest thing that can be emitted
            // (loudness 0, so it is heard only inside rho * minRadius), which is what "very low noise"
            // means in the GDD's table without adding a second scale.
            NoiseEmitted?.Invoke(origin, Mathf.Clamp01(loudness));
        }

        PingRecord AcquireRecord()
        {
            if (pool.Count == 0)
            {
                // Reachable only if the profile was edited to maxLivePings 0 before OnValidate ran, or
                // by a script that built the pool itself. Failing quiet beats throwing every frame.
                if (!poolEmptyWarned)
                {
                    Debug.LogWarning("EchoDirector has an empty ping pool: nothing will be drawn. " +
                                     "Check EchoTuningProfile.maxLivePings.", this);
                    poolEmptyWarned = true;
                }

                return null;
            }

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

            float arcLife = ArcMath.ArcLife(0f, 0f, 0f);
            foreach (PingRecord ping in pool)
            {
                if (!ping.alive)
                {
                    continue;
                }

                float age = Time.time - ping.emitTime;
                if (age > ping.TrainSeconds(profile))
                {
                    ping.alive = false;
                    continue;
                }

                arcLife = ArcMath.ArcLife(ping.radius, profile.waveSpeed, profile.rKill);
                for (int k = 0; k < profile.arcsPerPing; k++)
                {
                    float arcAge = ArcMath.ArcAge(age, k, profile.arcInterval);
                    if (arcAge < 0f)
                    {
                        // Later arcs have not been emitted yet, and they are all younger still.
                        break;
                    }

                    float baseRadius = ArcMath.BaseRadius(ping.radius, profile.waveSpeed, arcAge);

                    // Cannot break here: arc k is older than arc k+1, so a dead outer arc says nothing
                    // about the inner ones that were born after it.
                    if (!ArcMath.IsAlive(baseRadius, profile.rKill))
                    {
                        continue;
                    }

                    renderer?.AddArc(ping, k, baseRadius,
                                     ArcMath.Alpha(arcAge, arcLife, profile.alphaCurve), Time.time, profile);
                }

                if (ArcMath.HasEmissionPulse(age, profile))
                {
                    float carrier = ArcMath.BaseRadius(ping.radius, profile.waveSpeed, age);
                    renderer?.AddConeOutline(ping.origin, ping.aimAngleRad, ping.coneHalfRad,
                                             ArcMath.EmissionRadius(age, profile.waveSpeed, carrier),
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
                case SourceMode.Microphone:
                    return new MicrophoneEchoSource(profile);
                default:
                    return null;
            }
        }

        void ReportSourceFailure(IEchoSource opened)
        {
            if (sourceMode != SourceMode.Microphone)
            {
                return;
            }

            // A microphone that will not open is a machine without a usable input, or one whose default
            // input is routed to a monitor: say so once, name the device, and keep the game playable.
            Debug.LogWarning("Microphone unavailable (" + opened.Status + "). Falling back to the " +
                             "simulated source: the wave will fire on a timer instead of on your voice. " +
                             "Set EchoTuningProfile.microphoneDevice to the mic you want, or leave it " +
                             "empty to take the first non-monitor device.", this);
            source = new SimulatedEchoSource(profile);
            source.Open(null, 0);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Swap the tuning asset without a domain reload and rebuild everything it sizes. The debug
        /// window and the tests both need this: a profile is the only place the wave is configured.
        /// </summary>
        public void UseProfile(EchoTuningProfile next)
        {
            if (next == null || next == profile)
            {
                return;
            }

            profile = next;
            BuildRuntimeState();
        }

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
