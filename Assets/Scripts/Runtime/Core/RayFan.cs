using EchoMaze.Data;
using UnityEngine;

namespace EchoMaze.Core
{
    /// <summary>
    /// Casts the fan for one ping and writes the distance profile into a PingRecord. Runs only at
    /// emit time, never per frame: the arc train re-reads the stored profile, which is what keeps
    /// 200 rays affordable without jobs or Burst.
    ///
    /// Unity 6 removed the Physics2D NonAlloc family in favour of the SceneQuery API, so the
    /// allocation-free path is the ContactFilter2D overload that fills a caller-owned buffer.
    /// </summary>
    public static class RayFan
    {
        /// <summary>
        /// Triggers are excluded on purpose: the Absorber is a trigger so the fan can pass through it
        /// and dim the wall behind it instead of erasing the whole cone.
        /// </summary>
        public static ContactFilter2D BuildFilter(int layerMask)
        {
            var filter = ContactFilter2D.noFilter;
            filter.useLayerMask = true;
            filter.layerMask = layerMask;
            filter.useTriggers = false;
            return filter;
        }

        public static void Cast(PingRecord ping, ContactFilter2D filter, RaycastHit2D[] results)
        {
            if (results == null || results.Length == 0)
            {
                Debug.LogError("RayFan needs a preallocated RaycastHit2D buffer.");
                return;
            }

            int hits = 0;
            for (int i = 0; i < ping.rayCount; i++)
            {
                float angle = ArcMath.SampleAngle(ping, i);
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

                int n = Physics2D.Raycast(ping.origin, dir, filter, results, ping.radius);
                int nearest = Nearest(results, n);

                if (nearest >= 0)
                {
                    hits++;
                    ping.WriteSample(i, results[nearest].distance, ReflectivityOf(results[nearest].collider),
                        KindOf(results[nearest].collider), true);
                }
                else
                {
                    ping.WriteSample(i, float.PositiveInfinity, 0f, SurfaceKind.None, false);
                }
            }

            if (hits == 0)
            {
                ping.dMin = float.PositiveInfinity;
            }
        }

        /// <summary>
        /// The SceneQuery buffer is not documented as distance-sorted, and the arc is a *surface*, so
        /// picking results[0] blindly would let one collider's interior face cut a notch in a wall.
        /// </summary>
        static int Nearest(RaycastHit2D[] results, int count)
        {
            int best = -1;
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                RaycastHit2D hit = results[i];
                if (hit.collider == null || hit.distance <= 0f)
                {
                    continue;
                }

                if (hit.distance < bestDistance)
                {
                    bestDistance = hit.distance;
                    best = i;
                }
            }

            return best;
        }

        static float ReflectivityOf(Collider2D hit)
        {
            var surface = hit != null ? hit.GetComponent<EchoSurface>() : null;
            return surface != null ? surface.reflectivity : 1f;
        }

        static SurfaceKind KindOf(Collider2D hit)
        {
            var surface = hit != null ? hit.GetComponent<EchoSurface>() : null;
            return surface != null ? surface.kind : SurfaceKind.Plain;
        }
    }
}
