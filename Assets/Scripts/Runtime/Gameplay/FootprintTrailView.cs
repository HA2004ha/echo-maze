using EchoMaze.Core;
using EchoMaze.Data;
using UnityEngine;

namespace EchoMaze.Gameplay
{
    /// <summary>
    /// The player's own footprints, so they can tell whether they have been here before. Faint warm dots,
    /// one per <c>trailStepDistance</c> walked, fading linearly to nothing over <c>trailSeconds</c>
    /// (<see cref="TrailBuffer"/> does the bookkeeping). Shift leaves prints too: the owner did not ask
    /// for a silent step to be untraceable, so the simplest rule wins. The Crawler leaves none.
    ///
    /// Drawn on the Echo layer, which sorts over the body, so a print inside <see cref="BodyClearance"/>
    /// of the player is skipped rather than drawn across the torso. One mesh rebuild per frame is the whole
    /// cost (at most ~33 quads); see Docs/TUNING.md for the measured number.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class FootprintTrailView : MonoBehaviour
    {
        /// <summary>The drawn figure reaches 0.447 m from its centre (see TouchMarks), so 0.4 m is on the torso.</summary>
        public const float BodyClearance = 0.4f;

        [SerializeField] EchoTuningProfile profile;
        [Tooltip("The player whose steps are recorded.")]
        [SerializeField] Transform target;

        TrailBuffer trail;
        QuadMeshBuffer quads;
        int lastDrawn;

        public TrailBuffer Trail => trail;

        void Awake()
        {
            if (profile == null || target == null)
            {
                Debug.LogError(name + " has no profile or target: no footprints will be drawn. " +
                               "Rebuild the scene with Tools/Echo Maze.", this);
                enabled = false;
                return;
            }

            trail = new TrailBuffer(profile.trailSeconds, profile.walkSpeed, profile.trailStepDistance);
            quads = new QuadMeshBuffer(trail.Capacity, "Footprints");
            GetComponent<MeshFilter>().mesh = quads.Mesh;
        }

        void LateUpdate()
        {
            Vector2 here = target.position;
            trail.Tick(here, Time.time);
            if (trail.Count == 0 && lastDrawn == 0)
            {
                return;
            }

            Color color = profile.trailColor;
            float half = profile.trailDotSize * 0.5f;
            Vector2 along = new Vector2(half, 0f);

            quads.Begin();
            for (int i = 0; i < trail.Count; i++)
            {
                trail.Get(i, out Vector2 p, out float born);
                if ((p - here).sqrMagnitude < BodyClearance * BodyClearance)
                {
                    continue;
                }

                color.a = profile.trailMaxAlpha * Mathf.Clamp01(1f - (Time.time - born) / profile.trailSeconds);
                quads.AddSegment(p - along, p + along, half, color);
            }

            quads.End();
            lastDrawn = quads.Count;
        }

        void OnDestroy()
        {
            if (quads != null)
            {
                Destroy(quads.Mesh);
            }
        }
    }
}
