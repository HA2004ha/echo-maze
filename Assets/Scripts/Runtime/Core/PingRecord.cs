using EchoMaze.Data;
using UnityEngine;

namespace EchoMaze.Core
{
    /// <summary>
    /// One emitted sound and the distance profile the fan measured around it.
    /// Pooled and reused: allocated once per capacity and refilled per ping, so a frame with
    /// three live pings and 200 rays produces no garbage.
    ///
    /// Origin is a world position frozen at emit time, not the player transform, which is what
    /// lets a decoy marble ping from the far side of the room and keeps a moving player from
    /// smearing arcs that were measured from a different place.
    /// </summary>
    public sealed class PingRecord
    {
        public Vector2 origin;
        public float loudness;
        public float aimAngleRad;
        public float coneHalfRad;
        public float radius;
        public float emitTime;
        public int rayCount;
        public bool alive;

        public float[] d;
        public float[] amp;
        public SurfaceKind[] kind;

        public float dMin;
        public float dMax;
        public int seed;

        public int ValidSampleCount { get; private set; }

        public bool Allocated => d != null;

        public void Allocate(int maxRays)
        {
            if (d != null && d.Length >= maxRays)
            {
                return;
            }

            d = new float[maxRays];
            amp = new float[maxRays];
            kind = new SurfaceKind[maxRays];
        }

        public void Begin(Vector2 pingOrigin, float pingLoudness, float pingAim, float pingConeHalf,
                          float pingRadius, float time, int rays, int pingSeed)
        {
            Allocate(rays);
            origin = pingOrigin;
            loudness = pingLoudness;
            aimAngleRad = pingAim;
            coneHalfRad = pingConeHalf;
            radius = pingRadius;
            emitTime = time;
            rayCount = rays;
            seed = pingSeed;
            alive = true;
            ValidSampleCount = 0;
            dMin = float.PositiveInfinity;
            dMax = 0f;
        }

        /// <summary>Time at which the first arc of this ping dies.</summary>
        public float LifeSeconds(float waveSpeed, float rKill) =>
            waveSpeed > 0f ? Mathf.Max(0f, (radius - rKill) / waveSpeed) : 0f;

        public void WriteSample(int i, float distance, float reflectivity, SurfaceKind surfaceKind, bool valid)
        {
            if (!valid || float.IsPositiveInfinity(distance))
            {
                d[i] = float.PositiveInfinity;
                amp[i] = 0f;
                kind[i] = SurfaceKind.None;
                return;
            }

            d[i] = distance;
            amp[i] = reflectivity;
            kind[i] = surfaceKind;

            if (distance < dMin || ValidSampleCount == 0)
            {
                dMin = distance;
            }

            if (distance > dMax)
            {
                dMax = distance;
            }

            ValidSampleCount++;
        }

        public static PingRecord CreatePooled(int maxRays)
        {
            var record = new PingRecord();
            record.Allocate(maxRays);
            return record;
        }
    }
}
