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
        /// <summary>Where a calibrated gate survives, per machine and per install.</summary>
        const string GateOpenKey = "echo.gate.open";
        const string GateCloseKey = "echo.gate.close";
        const string GateFullKey = "echo.gate.full";

        /// <summary>
        /// Where the chosen microphone survives. A preference and not a scene value for the same reason the
        /// gate is: the headset belongs to the machine, and a scene that remembers it would be wrong on
        /// every other laptop the team opens the project with.
        /// </summary>
        const string DeviceKey = "echo.mic.device";

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

        /// <summary>
        /// Creature ripples: an origin and a birth time on <see cref="Clock"/>, NaN when the slot is free. Fixed and tiny: a roar is rare, and
        /// a fifth simultaneous one would only replace the oldest, which has nearly faded anyway.
        /// </summary>
        readonly Vector2[] rippleOrigins = new Vector2[4];
        readonly float[] rippleBirths = { float.NaN, float.NaN, float.NaN, float.NaN };

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

        /// <summary>Creature ripples drawn this frame. Separate from LivePingCount: a roar is not a ping.</summary>
        public int LiveRippleCount { get; private set; }
        public int LastRayCount { get; private set; }

        /// <summary>
        /// Loudness of the Space fallback, read from the profile so the tuning asset stays the only
        /// source of truth for how loud a manual echo is.
        /// </summary>
        public float ManualEchoLoudness => profile.manualEchoLoudness;

        /// <summary>False while the last ping's recovery is still being paid.</summary>
        public bool CanPing => EmitEnabled && Time.time >= nextReadyTime;

        /// <summary>
        /// Off while a screen is paused and while the calibration page is listening. The pause is not
        /// cosmetic: <c>Time.time</c> stands still at timeScale 0, so a charge released during a pause would
        /// pay a cooldown that can never expire and leave the next room silent. The source keeps polling
        /// with this off — the meter has to read the room — only the emission stops.
        /// </summary>
        public bool EmitEnabled { get; set; } = true;

        /// <summary>Seconds of recovery left, for the HUD cooldown ring.</summary>
        public float CooldownRemaining => Mathf.Max(0f, nextReadyTime - Time.time);

        /// <summary>
        /// The cooldown the last ping actually cost. The ring is a fraction of the player's own debt, so it
        /// has to be divided by the number that was paid and not by the one a whisper would pay — otherwise a
        /// shout empties the ring in a fifth of its sweep and the interface lies about the loudest thing in
        /// the game.
        /// </summary>
        public float LastCooldownSeconds { get; private set; }

        /// <summary>0 when ready, 1 at the moment of the ping, and the sweep between them for the ring.</summary>
        public float CooldownFraction =>
            LastCooldownSeconds <= 0f ? 0f : Mathf.Clamp01(CooldownRemaining / LastCooldownSeconds);

        void Awake()
        {
            if (profile == null)
            {
                Debug.LogWarning("EchoDirector has no EchoTuningProfile assigned; using code defaults. " +
                                 "Create Assets/Data/Echo/EchoTuning.asset to tune the wave.");
                profile = ScriptableObject.CreateInstance<EchoTuningProfile>();
            }

            BuildRuntimeState();

            ApplyStoredGate();
            source = CreateSource();
            if (source != null && !source.Open(StoredDeviceName, 0))
            {
                ReportSourceFailure(source);
            }
            else if (sourceMode == SourceMode.Microphone && source != null)
            {
                ReportSourceOpened(source);
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

        /// <summary>
        /// The clock the arcs are advanced on. Identical to <c>Time.time</c> outside the editor and while
        /// unfrozen; the debug window holds it still (and can step it) so a wave can be measured and
        /// screenshotted instead of arguing with a frame rate.
        /// </summary>
        float Clock =>
#if UNITY_EDITOR
            frozenAt >= 0f ? frozenAt :
#endif
            Time.time;

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
            if (!EmitEnabled)
            {
                return;
            }

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

        /// <summary>
        /// A creature's call made visible: a warm ring growing out of <paramref name="origin"/> and fading.
        /// Deliberately not a ping — it casts no rays, so it says where the creature is and nothing about the
        /// walls; it takes no slot in the player's pool and no cooldown; and it is not a noise, so nothing
        /// that listens to <see cref="NoiseEmitted"/> hears it.
        /// </summary>
        public void EmitRipple(Vector2 origin)
        {
            int slot = 0;
            for (int i = 0; i < rippleBirths.Length; i++)
            {
                if (float.IsNaN(rippleBirths[i]))
                {
                    slot = i;
                    break;
                }

                if (rippleBirths[i] < rippleBirths[slot])
                {
                    slot = i;
                }
            }

            rippleOrigins[slot] = origin;
            rippleBirths[slot] = Clock;
        }

        void AdvanceRipples(IEchoRenderer renderer)
        {
            LiveRippleCount = 0;
            float life = Mathf.Max(0.01f, profile.roarWaveSeconds);
            for (int i = 0; i < rippleBirths.Length; i++)
            {
                if (float.IsNaN(rippleBirths[i]))
                {
                    continue;
                }

                float t = (Clock - rippleBirths[i]) / life;
                if (t >= 1f)
                {
                    rippleBirths[i] = float.NaN;
                    continue;
                }

                LiveRippleCount++;
                renderer?.AddConeOutline(rippleOrigins[i], 0f, Mathf.PI, profile.roarWaveRadius * t, 1f - t,
                                         profile.creatureArcColor, profile);
            }
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
            if (!EmitEnabled || Time.time - lastWeakPulseTime < profile.weakPulseCooldown)
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

            // Clock, not Time.time: a ping emitted while the debug window holds the arcs still must be
            // born at the frozen instant, or it would start its life already dead.
            record.Begin(origin, loudness, aim, coneHalf, radius, Clock, rays, seedCounter++, closedRing);
            RayFan.Cast(record, echoFilter, rayBuffer);
            SurfaceRead.Compute(record, profile);
            LastRayCount = rays;

            // Every loud ping pays for itself, and the cost scales with the loudness: that is what makes
            // the signal costly, which is the whole game theory of being heard.
            float until = Mathf.Max(nextReadyTime, Time.time + cooldown);

            // Measured against the debt that is actually in force, which is the longer of the new one and
            // whatever the previous ping was still paying.
            LastCooldownSeconds = Mathf.Max(0.0001f, until - Time.time);
            nextReadyTime = until;

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

                float age = Clock - ping.emitTime;
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
                                     ArcMath.Alpha(arcAge, arcLife, profile.alphaCurve), Clock, profile);
                }

                if (ArcMath.HasEmissionPulse(age, profile))
                {
                    float carrier = ArcMath.BaseRadius(ping.radius, profile.waveSpeed, age);
                    renderer?.AddConeOutline(ping.origin, ping.aimAngleRad, ping.coneHalfRad,
                                             ArcMath.EmissionRadius(age, profile.waveSpeed, carrier),
                                             ArcMath.EmissionAlpha(age, profile), profile.arcColor, profile);
                }

                LivePingCount++;
            }

            AdvanceRipples(renderer);
            renderer?.EndFrame();
        }

        /// <summary>
        /// A gate is a property of the machine and the headset, not of the level, so the calibration the
        /// player did once is read back before the capture opens. Applied to the profile rather than kept
        /// beside it, because the profile is the only place the source and the debug window read these
        /// numbers from and a second copy would be a second truth.
        /// </summary>
        void ApplyStoredGate()
        {
            if (sourceMode != SourceMode.Microphone || !PlayerPrefs.HasKey(GateOpenKey))
            {
                return;
            }

            profile.gateOpenRms = PlayerPrefs.GetFloat(GateOpenKey, profile.gateOpenRms);
            profile.gateCloseRms = PlayerPrefs.GetFloat(GateCloseKey, profile.gateCloseRms);
            profile.gateFullRms = PlayerPrefs.GetFloat(GateFullKey, profile.gateFullRms);
        }

        /// <summary>
        /// Re-thresholds a live capture without closing and reopening it. The scene is rebuilt from a menu
        /// after calibration, and a mic that restarts mid-session is a mic that drops a word; replacing the
        /// gate keeps the ring.
        ///
        /// Returns false when there is no gate to move — ManualOnly and the simulated source have no
        /// threshold at all — so the calibration page can say "no microphone here, the game runs on Space"
        /// instead of reporting a threshold nobody listens to.
        /// </summary>
        public bool ApplyGateThresholds(float openRms, float closeRms, float fullRms)
        {
            profile.gateOpenRms = openRms;
            profile.gateCloseRms = closeRms;
            profile.gateFullRms = fullRms;
            PlayerPrefs.SetFloat(GateOpenKey, openRms);
            PlayerPrefs.SetFloat(GateCloseKey, closeRms);
            PlayerPrefs.SetFloat(GateFullKey, fullRms);
            PlayerPrefs.Save();

            if (source is MicrophoneEchoSource mic)
            {
                mic.ApplyGate(openRms, closeRms, fullRms);
                return true;
            }

            return false;
        }

        /// <summary>
        /// The captures this process can see right now. Read on every call rather than cached: the page that
        /// offers them is the page a player opens after plugging a headset in, and a stale list would be a
        /// reason to restart the game for no reason.
        /// </summary>
        public static string[] AvailableDevices() => Microphone.devices ?? Array.Empty<string>();

        /// <summary>The saved choice; <see cref="MicDeviceChoice.Auto"/> when nobody ever picked one.</summary>
        public static string StoredDeviceName => PlayerPrefs.GetString(DeviceKey, MicDeviceChoice.Auto);

        /// <summary>The capture the game is actually listening to, or null when none is open.</summary>
        public string ActiveDeviceName => source is MicrophoneEchoSource mic ? mic.DeviceName : null;

        /// <summary>
        /// Change the input without restarting the game: store the name, close the old clip, open the new
        /// one. Empty means Auto — the first input that is not a monitor.
        ///
        /// A machine whose microphone failed to open at all is exactly the machine this button is for, so
        /// the attempt rebuilds the real source rather than refusing because the director is currently
        /// holding the simulated one. It returns false when the capture still will not open, which is the
        /// page's cue to say so and point at <c>Space</c>.
        /// </summary>
        public bool UseDevice(string name)
        {
            string choice = name ?? MicDeviceChoice.Auto;
            if (choice.Length == 0)
            {
                PlayerPrefs.DeleteKey(DeviceKey);
            }
            else
            {
                PlayerPrefs.SetString(DeviceKey, choice);
            }

            PlayerPrefs.Save();

            if (sourceMode != SourceMode.Microphone)
            {
                return false;
            }

            source?.Close();
            source = new MicrophoneEchoSource(profile);
            if (!source.Open(choice, 0))
            {
                ReportSourceFailure(source);
                return false;
            }

            ReportSourceOpened(source);
            return true;
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

        /// <summary>
        /// A player build's audio backend is a different question from the editor's, and the only evidence
        /// for it is this line in `Player.log`: an absence of warnings cannot be told apart from a mic that
        /// was never asked for. It names every device the process can see, because device names differ per
        /// distro, per audio server and per headset, and a teammate has to be able to paste it back.
        /// </summary>
        void ReportSourceOpened(IEchoSource opened)
        {
            string[] devices = Microphone.devices ?? Array.Empty<string>();
            Debug.Log("Microphone open in " + (Application.isEditor ? "the editor" : "the player") + ": " +
                      opened.Status + ". " + devices.Length + " capture device(s): " +
                      string.Join(", ", devices) + ".", this);
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
        float frozenAt = -1f;

        /// <summary>Read by the debug window: a negative value means the arcs run on the real clock.</summary>
        public bool IsFrozen => frozenAt >= 0f;

        /// <summary>
        /// The clock the arcs are actually being advanced on, so the debug window can print an age the
        /// wave itself agrees with instead of a second reading that only matches when unfrozen.
        /// </summary>
        public float ArcClock => Clock;

        /// <summary>Stops the arc clock where it is. Cooldowns and the microphone keep running on real time.</summary>
        public void FreezeArcs()
        {
            frozenAt = Time.time;
        }

        public void UnfreezeArcs()
        {
            frozenAt = -1f;
        }

        /// <summary>
        /// Moves the frozen clock by a delta, so a held wave can be stepped through instead of only
        /// photographed at one instant. Ignored while unfrozen.
        /// </summary>
        public void StepFrozenArcs(float seconds)
        {
            if (frozenAt >= 0f)
            {
                frozenAt += seconds;
            }
        }

        /// <summary>
        /// The pool itself, dead records included, so the debug window can plot a ping's samples after its
        /// arcs have gone. Editor-only: nothing at runtime has any business reading another ping's buffer.
        /// </summary>
        public System.Collections.Generic.IReadOnlyList<PingRecord> PoolForDebugger => pool;

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
