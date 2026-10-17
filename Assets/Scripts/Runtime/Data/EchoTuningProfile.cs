using System;
using UnityEngine;

namespace EchoMaze.Data
{
    public enum SurfaceKind
    {
        None = 0,
        Concrete = 1,
        Metal = 2,
        Sponge = 3,
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
        [Min(0.5f)] public float minRadius = 2.5f;
        [Min(1f)] public float maxRadius = 9f;
        [Tooltip("Below 1 so a whisper still shows something useful.")]
        [Range(0.1f, 3f)] public float radiusLoudnessExponent = 0.75f;

        [Header("Wave")]
        [Tooltip("How fast the arc travels toward the player, world units per second. Must stay above 1.6x max player speed or a returning arc can never overtake the player.")]
        [Min(1f)] public float waveSpeed = 7f;
        [Tooltip("Arc dies when its base radius falls to this.")]
        [Range(0.05f, 2f)] public float rKill = 0.35f;

        [Header("Shape")]
        [Tooltip("A_0 in the GDD formula: how strongly hit distance deforms the arc.")]
        [Range(0f, 2f)] public float gain0 = 0.28f;
        [Tooltip("Keeps the returning-wave sign testable at runtime. +1 means nearer surfaces sit closer to the player.")]
        public float gainSign = 1f;
        [Tooltip("Per-arc deformation decay in a train. 1 in week 1 because a ping emits a single arc.")]
        [Range(0.1f, 1f)] public float innerDecay = 1f;
        [Tooltip("Arc opacity over normalised age.")]
        public AnimationCurve alphaCurve =
            new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.65f, 0.75f), new Keyframe(1f, 0f));

        [Header("Draw")]
        [Tooltip("Ribbon width in world units.")]
        [Range(0.002f, 0.3f)] public float baseThickness = 0.022f;
        [Tooltip("Base line colour, before alpha and HDR.")]
        public Color arcColor = new Color(0.498f, 0.914f, 0.941f, 1f);

        [Header("Echo trigger")]
        [Tooltip("Gated loudness needed to emit a ping automatically.")]
        [Range(0f, 1f)] public float loudnessThreshold = 0.18f;
        [Tooltip("Seconds between automatic pings, so shouting does not spam the arc field.")]
        [Min(0.05f)] public float minPingInterval = 0.35f;
        [Tooltip("Loudness of the Space fallback echo.")]
        [Range(0f, 1f)] public float manualEchoLoudness = 0.65f;
        [Range(1, 4)] public int maxLivePings = 3;

        [Header("Simulated source (Linux development path)")]
        [Tooltip("Constant loudness used when no microphone is available.")]
        [Range(0f, 1f)] public float simulatedLoudness = 0.7f;

        void OnValidate()
        {
            coneHalfAngleDeg = Mathf.Clamp(coneHalfAngleDeg, 5f, 90f);
            minRays = Mathf.Clamp(minRays, 16, 512);
            maxRays = Mathf.Clamp(maxRays, minRays, 512);
            minRadius = Mathf.Max(0.5f, minRadius);
            maxRadius = Mathf.Max(minRadius + 0.5f, maxRadius);
            waveSpeed = Mathf.Max(1f, waveSpeed);
            maxLivePings = Mathf.Clamp(maxLivePings, 1, 4);
            if (alphaCurve == null || alphaCurve.length == 0)
            {
                alphaCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
            }
        }

        public float ConeHalfRad => coneHalfAngleDeg * Mathf.Deg2Rad;

        /// <summary>
        /// Odd, so that clamping a ray count to capacity can never land on an even number and drop the
        /// ray that points exactly at the aim direction.
        /// </summary>
        public int CapacityRays => Mathf.Max(minRays, maxRays) | 1;
    }
}
