using EchoMaze.Core;
using EchoMaze.Data;
using UnityEngine;

namespace EchoMaze.Gameplay
{
    /// <summary>
    /// Owns the player's <see cref="WallMemory"/> and draws it: thin, dim neon lines that fade in over a
    /// quarter of a second and then stay for the rest of the level.
    ///
    /// Lives on a scene object (not DontDestroyOnLoad), so a scene load is what clears it: loading a
    /// chapter builds a fresh store in Awake, while dying and retrying inside the scene keeps it, because
    /// the player remembers. It has its own mesh and renderer rather than a share of
    /// <see cref="EchoRenderer.MaxQuads"/>, which the live arcs need all of. Rebuilt only when the store
    /// changed or a line is still fading in.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class WallMemoryView : MonoBehaviour
    {
        [SerializeField] EchoTuningProfile profile;

        WallMemory memory;
        QuadMeshBuffer quads;
        int drawnVersion = -1;
        float fadingUntil;
        float drawnAt;

        public WallMemory Memory
        {
            get
            {
                Build();
                return memory;
            }
        }

        void Awake()
        {
            Build();
        }

        /// <summary>
        /// Built lazily as well: a test that adds this component to an inactive object and wires the
        /// profile afterwards must not hit a null store.
        /// </summary>
        void Build()
        {
            if (memory != null)
            {
                return;
            }

            if (profile == null)
            {
                Debug.LogError(name + " has no EchoTuningProfile reference: walls will not be remembered. " +
                               "Rebuild the scene with Tools/Echo Maze.", this);
                return;
            }

            memory = new WallMemory(profile.wallMemoryCapacity, profile.wallMemoryMergeDistance);
            quads = new QuadMeshBuffer(profile.wallMemoryCapacity, "WallMemory");
            GetComponent<MeshFilter>().mesh = quads.Mesh;
        }

        /// <summary>Remembers <c>wallMemorySegmentLength</c> metres of wall centred on a contact point.</summary>
        public void Remember(Vector2 point, Vector2 tangent) => Remember(point, tangent, null);

        /// <summary>
        /// As above, but the line is clipped to <paramref name="within"/> (the touched box's bounds), so a
        /// contact near a corner or an opening never draws wall across the gap beside it.
        /// </summary>
        public void Remember(Vector2 point, Vector2 tangent, Bounds? within)
        {
            Build();
            if (memory == null || tangent.sqrMagnitude < 1e-6f)
            {
                return;
            }

            Vector2 half = tangent.normalized * (profile.wallMemorySegmentLength * 0.5f);
            int before = memory.Version;
            Vector2 a = point - half;
            Vector2 b = point + half;
            if (within.HasValue)
            {
                Bounds box = within.Value;
                box.Expand(0.02f);
                a = Clamp(a, box);
                b = Clamp(b, box);
            }

            memory.Add(a, b, Time.time);
            if (memory.Version != before)
            {
                fadingUntil = Time.time + profile.wallMemoryFadeInSeconds;
            }
        }

        static Vector2 Clamp(Vector2 v, Bounds box) => new Vector2(
            Mathf.Clamp(v.x, box.min.x, box.max.x), Mathf.Clamp(v.y, box.min.y, box.max.y));

        void LateUpdate()
        {
            if (memory == null || (memory.Version == drawnVersion && drawnAt >= fadingUntil))
            {
                return;
            }

            // One more rebuild lands at or after fadingUntil, which is what pins the final alpha.
            drawnVersion = memory.Version;
            drawnAt = Time.time;
            Color color = profile.wallMemoryColor;
            float half = profile.baseThickness * profile.wallMemoryThicknessScale * 0.5f;
            float fade = Mathf.Max(0.01f, profile.wallMemoryFadeInSeconds);

            quads.Begin();
            for (int i = 0; i < memory.Count; i++)
            {
                memory.Get(i, out Vector2 a, out Vector2 b, out float born);
                color.a = profile.wallMemoryAlpha * Mathf.Clamp01((Time.time - born) / fade);
                quads.AddSegment(a, b, half, color);
            }

            quads.End();
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
