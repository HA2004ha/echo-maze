using System;
using UnityEngine;

namespace EchoMaze.Data
{
    /// <summary>
    /// What a collider *is*, not what it is made of: GDD 3.0 removed surface materials and brightness
    /// now comes from the curvature of the distance profile. The numbers are frozen because scenes and
    /// prefabs store them as ints — Metal (2) and Sponge (3) are retired, their slots stay empty.
    /// </summary>
    public enum SurfaceKind
    {
        None = 0,
        Plain = 1,
        Absorber = 4,
        Crawler = 5,
        Spike = 6
    }

    [CreateAssetMenu(menuName = "Echo Maze/Echo Tuning", fileName = "EchoTuning")]
    public sealed class EchoTuningProfile : ScriptableObject
    {
        [Header("Fan")]
        [Tooltip("Half of the cone angle in degrees. 32 gives the 64 degree cone from the GDD.")]
        [Range(5f, 90f)] public float coneHalfAngleDeg = 32f;
        [Tooltip("Rays at the quietest audible ping. Low quality tiers live here.")]
        [Range(16, 512)] public int minRays = 80;
        [Tooltip("Rays at a shout. Each ray is one Physics2D raycast, done once per ping.")]
        [Range(16, 512)] public int maxRays = 200;

        [Header("Reach")]
        [Tooltip("Wave radius at loudness 0, in world units.")]
        [Min(0.5f)] public float minRadius = 2f;
        [Tooltip("Wave radius at a shout. Keep it near the size of the room: a ring bigger than everything it can measure reads as a circle arriving from nowhere.")]
        [Min(1f)] public float maxRadius = 6.5f;
        [Tooltip("Below 1 so a whisper still shows something useful.")]
        [Range(0.1f, 3f)] public float radiusLoudnessExponent = 0.75f;

        [Header("Wave")]
        [Tooltip("How fast the arc travels toward the player, world units per second. Must stay above 1.6x max player speed or a returning arc can never overtake the player.")]
        [Min(1f)] public float waveSpeed = 7f;
        [Tooltip("Arc dies when its base radius falls to this.")]
        [Range(0.05f, 2f)] public float rKill = 0.35f;
        [Tooltip("Seconds the outward cue at the emitter is drawn. It is a plain cone outline with no geometry in it, so it reveals nothing; it only shows that the sound left the player in the aimed direction. 0 disables it.")]
        [Range(0f, 0.5f)] public float emissionPulseSeconds = 0.16f;

        [Header("Shape")]
        [Tooltip("A_0 in the GDD formula: how strongly hit distance deforms the arc. At the GDD's 0.28 a pillar only dents the ring by 0.12 world units, which is ten pixels and reads as a circle, so week 1 measures it at 1: the arc radius then mirrors the room's own distances.")]
        [Range(0f, 2f)] public float gain0 = 1f;
        [Tooltip("Keeps the returning-wave sign testable at runtime. +1 means nearer surfaces sit closer to the player.")]
        public float gainSign = 1f;
        [Tooltip("Per-arc deformation decay in a train: A_k = gain0 * innerDecay^k. The outer arc carries the measurement, the inner ones are its echo and should deform less or the field turns to mush.")]
        [Range(0.1f, 1f)] public float innerDecay = 0.72f;
        [Tooltip("Arc opacity over normalised age.")]
        public AnimationCurve alphaCurve =
            new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.65f, 0.75f), new Keyframe(1f, 0f));

        [Header("Draw")]
        [Tooltip("Ribbon width in world units. One world unit is about ninety pixels in the sandbox camera, so anything under 0.04 disappears.")]
        [Range(0.002f, 0.3f)] public float baseThickness = 0.05f;
        [Tooltip("Base line colour, before alpha and HDR.")]
        public Color arcColor = new Color(0.498f, 0.914f, 0.941f, 1f);

        [Header("Echo trigger")]
        [Tooltip("Gated loudness needed to emit a ping automatically.")]
        [Range(0f, 1f)] public float loudnessThreshold = 0.18f;
        [Tooltip("Seconds of recovery a player always pays after a ping. The GDD's cooldown is 0.6 + 1.4 * loudness, so a shout costs two seconds of silence and a whisper costs well under one. The old minPingInterval re-arm is gone: the gate fires once per vocal event and this is the only cost.")]
        [Range(0f, 3f)] public float cooldownBase = 0.6f;
        [Tooltip("Extra seconds of cooldown per unit of loudness. This is the cost that makes the signal costly, which is the whole game theory of it.")]
        [Range(0f, 4f)] public float cooldownPerLoudness = 1.4f;
        [Tooltip("Loudness of a quick Space tap. Holding Space charges up to a full shout over manualChargeSeconds.")]
        [Range(0f, 1f)] public float manualEchoLoudness = 0.65f;
        [Tooltip("Seconds of holding Space that take the manual echo from the tap loudness to 1. Long enough that a panic tap is not a shout, short enough that a deliberate charge fits inside one cooldown.")]
        [Range(0.2f, 4f)] public float manualChargeSeconds = 1.2f;
        [Range(1, 4)] public int maxLivePings = 3;

        [Header("Movement")]
        [Tooltip("Walk speed in world units per second. waveSpeed must stay above 1.6x this or a returning arc can never overtake the player (Docs/TUNING.md rule 1).")]
        [Range(0.5f, 8f)] public float walkSpeed = 2.5f;
        [Tooltip("Speed while Shift is held. Slow enough to read as deliberate, and it makes no footstep noise at all, which is the only reason to press it.")]
        [Range(0.1f, 8f)] public float silentSpeed = 1f;
        [Tooltip("Seconds between footsteps while walking normally. Silent step emits none.")]
        [Range(0.1f, 3f)] public float footstepInterval = 0.55f;
        [Tooltip("Loudness of one footstep. GDD calls walking normally a small, short range noise; through H = rho * R(L) this maps to a couple of metres.")]
        [Range(0f, 1f)] public float footstepLoudness = 0.12f;
        [Tooltip("Loudness of a wall bump. Very low: careless walking has a cost, but a bump is not a shout.")]
        [Range(0f, 1f)] public float bumpLoudness = 0.04f;

        [Header("Hearing")]
        [Tooltip("rho = enemy hearing radius / visible echo reach at the same loudness. The tuning knob from the GDD: too high and pinging is never worth it, too low and there is no tension.")]
        [Range(0.05f, 3f)] public float rho = 0.8f;
        [Tooltip("Crawler speed as a fraction of walk speed. Above 1 the creature cannot be outrun, which is the whole Crawler design.")]
        [Range(0.1f, 2f)] public float crawlerSpeedMultiplier = 0.65f;

        [Header("Arc train")]
        [Tooltip("Arcs emitted per ping. One arc per ping is a single ring and reads as a pulse; three read as a wave with a front.")]
        [Range(1, 4)] public int arcsPerPing = 3;
        [Tooltip("Seconds between the arcs of one train, so arc k is born k intervals after the carrier left.")]
        [Range(0f, 0.5f)] public float arcInterval = 0.12f;

        [Header("Brightness from geometry")]
        [Tooltip("Opacity of a sample whose profile is locally flat. A plane measures exactly 1.0 of normalised curvature, so the floor sits above it and only genuinely bent runs light up.")]
        [Range(0f, 1f)] public float brightnessBase = 0.4f;
        [Tooltip("Extra opacity a sample can earn from curvature, added to brightnessBase.")]
        [Range(0f, 1f)] public float brightnessGain = 0.6f;
        [Tooltip("Normalised curvature (|second difference| / distance) that counts as flat. A plane seen head-on is 1, the same plane at the edge of a 32 degree cone is about 1.8.")]
        [Range(0f, 8f)] public float curvatureFloor = 2f;
        [Tooltip("Normalised curvature that saturates brightness. floor + range is the line between architecture and feature: measured in the blockout, every flat wall run sits at 1.0-2.0 and the 0.6 m pillar at 5.7 m at ~10.")]
        [Range(0.5f, 40f)] public float curvatureRange = 6f;
        [Tooltip("A change of hit distance between neighbouring rays bigger than this fraction of the distance itself is not curvature: the fan stepped off one surface onto another. The line breaks there and neither side gets a brightness bonus, so a corner's kink can be the flare instead of every object's silhouette.")]
        [Range(0.02f, 0.6f)] public float edgeJumpFraction = 0.12f;

        [Header("Fracture")]
        [Tooltip("Fraction of the segments across a locally flat run that are left undrawn. The break pattern is drawn from the ping's own seed, so every arc of that ping shows the same gaps and the eye tracks one wave instead of static.")]
        [Range(0f, 0.6f)] public float flatDropChance = 0.18f;
        [Tooltip("Normalised curvature below which a sample is eligible to be dropped. Kept above the flat-plane range so edges and corners are never broken up.")]
        [Range(0f, 8f)] public float fractureFlatBelow = 2.4f;

        [Header("Living things")]
        [Tooltip("Blinks per second on the arc samples of a creature. Walls stay steady, so a breathing part of the wave is information without being labelled.")]
        [Range(0f, 4f)] public float heartbeatHz = 1.1f;
        [Tooltip("Fractional brightness swing of that blink.")]
        [Range(0f, 1f)] public float shimmer = 0.35f;

        [Header("Microphone")]
        [Tooltip("Empty uses the default recording device. Otherwise the name must match Microphone.devices exactly.")]
        public string microphoneDevice = "";
        [Tooltip("RMS at which the gate opens. Set from the calibration page, not here, on a real machine.")]
        [Range(0.001f, 0.2f)] public float gateOpenRms = 0.02f;
        [Tooltip("RMS at which it closes again. The gap between the two is the anti-chatter hysteresis.")]
        [Range(0.001f, 0.2f)] public float gateCloseRms = 0.012f;
        [Tooltip("RMS that maps to loudness 1, i.e. a shout.")]
        [Range(0.01f, 1f)] public float gateFullRms = 0.12f;
        [Tooltip("Seconds of near-silence that still count as the same vocal event, so a word with a dip in it makes one wave.")]
        [Range(0f, 2f)] public float loudnessHold = 0.25f;

        [Header("Simulated source (Linux development path)")]
        [Tooltip("Constant loudness used when no microphone is available.")]
        [Range(0f, 1f)] public float simulatedLoudness = 0.7f;
        [Tooltip("Seconds between the simulated source's vocal events. Without a microphone the gate would otherwise fire exactly once and the development path would look broken.")]
        [Range(0.2f, 8f)] public float simulatedEventInterval = 1.6f;

        [Header("Silent fallbacks")]
        [Tooltip("Reach of the F weak pulse: a 360 degree ring measured from the player, short enough to cost almost nothing to make.")]
        [Range(0.25f, 4f)] public float weakPulseRadius = 1.5f;
        [Tooltip("Rays in the weak pulse. It is a full circle, so this is the angular resolution of the whole ring.")]
        [Range(24, 512)] public int weakPulseRays = 96;
        [Tooltip("Seconds between weak pulses.")]
        [Range(0.1f, 5f)] public float weakPulseCooldown = 1.2f;

        void OnValidate()
        {
            coneHalfAngleDeg = Mathf.Clamp(coneHalfAngleDeg, 5f, 90f);
            minRays = Mathf.Clamp(minRays, 16, 512);
            maxRays = Mathf.Clamp(maxRays, minRays, 512);
            minRadius = Mathf.Max(0.5f, minRadius);
            maxRadius = Mathf.Max(minRadius + 0.5f, maxRadius);
            waveSpeed = Mathf.Max(1f, waveSpeed);
            maxLivePings = Mathf.Clamp(maxLivePings, 1, 4);
            arcsPerPing = Mathf.Clamp(arcsPerPing, 1, 4);
            arcInterval = Mathf.Clamp(arcInterval, 0f, 0.5f);
            brightnessBase = Mathf.Clamp(brightnessBase, 0f, 1f);
            brightnessGain = Mathf.Clamp(brightnessGain, 0f, 1f - brightnessBase);
            curvatureFloor = Mathf.Clamp(curvatureFloor, 0f, 8f);
            curvatureRange = Mathf.Max(0.5f, curvatureRange);
            edgeJumpFraction = Mathf.Clamp(edgeJumpFraction, 0.02f, 0.6f);
            flatDropChance = Mathf.Clamp(flatDropChance, 0f, 0.6f);
            heartbeatHz = Mathf.Clamp(heartbeatHz, 0f, 4f);
            shimmer = Mathf.Clamp(shimmer, 0f, 1f);
            weakPulseRadius = Mathf.Clamp(weakPulseRadius, 0.25f, 4f);
            weakPulseRays = Mathf.Clamp(weakPulseRays, 24, 512) | 1;
            weakPulseCooldown = Mathf.Max(0.1f, weakPulseCooldown);
            walkSpeed = Mathf.Max(0.5f, walkSpeed);
            silentSpeed = Mathf.Clamp(silentSpeed, 0.1f, walkSpeed);
            rho = Mathf.Max(0.05f, rho);
            crawlerSpeedMultiplier = Mathf.Clamp(crawlerSpeedMultiplier, 0.1f, 2f);
            // TUNING.md rule 1, enforced here so a designer raising walk speed cannot silently break
            // the one property that makes a returning arc look like it is returning.
            waveSpeed = Mathf.Max(waveSpeed, 1.6f * walkSpeed);
            if (alphaCurve == null || alphaCurve.length == 0)
            {
                alphaCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
            }
        }

        public float ConeHalfRad => coneHalfAngleDeg * Mathf.Deg2Rad;

        /// <summary>
        /// Odd, so that clamping a ray count to capacity can never land on an even number and drop the
        /// ray that points exactly at the aim direction. Also covers the weak pulse, which is the widest
        /// fan the game emits and shares the director's single ray buffer.
        /// </summary>
        public int CapacityRays => Mathf.Max(Mathf.Max(minRays, maxRays), weakPulseRays) | 1;
    }
}
