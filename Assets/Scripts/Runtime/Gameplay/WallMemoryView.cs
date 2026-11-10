using EchoMaze.Core;
using EchoMaze.Data;
using UnityEngine;

namespace EchoMaze.Gameplay
{
    /// <summary>
    /// Owns the player's <see cref="WallMemory"/> and draws it: thin, dim neon lines that fade in over a
    /// quarter of a second, hold for six, and then go out.
    ///
    /// The lines used to stay for the rest of the level, which made a walked room a map the player owned
    /// outright and turned the last third of every chapter into reading it. They are a trace now: what the
    /// hand felt is bright while it is fresh and gone by the time the next corner needs thinking about, so
    /// the room has to be re-read rather than remembered twice.
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
        float aliveUntil;
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
                // As long as the newest line can still be seen, the mesh has to be rebuilt: the fade-out is
                // a change the store does not know about.
                aliveUntil = Time.time + profile.wallMemoryHoldSeconds + profile.wallMemoryFadeOutSeconds;
            }
        }

        static Vector2 Clamp(Vector2 v, Bounds box) => new Vector2(
            Mathf.Clamp(v.x, box.min.x, box.max.x), Mathf.Clamp(v.y, box.min.y, box.max.y));

        void LateUpdate()
        {
            if (memory == null || profile == null)
            {
                return;
            }

            // Two reasons to rebuild: the store changed, or a line is still getting brighter or going out.
            // The second one is a change the store never sees, which is why age is read here and not there.
            bool changed = memory.Version != drawnVersion;
            if (!changed && Time.time > aliveUntil && drawnAt >= aliveUntil)
            {
                return;
            }

            // One more rebuild lands at or after aliveUntil, which is what pins the final alpha at zero.
            drawnVersion = memory.Version;
            drawnAt = Time.time;
            Color color = profile.wallMemoryColor;
            float half = profile.baseThickness * profile.wallMemoryThicknessScale * 0.5f;
            float fadeIn = Mathf.Max(0.01f, profile.wallMemoryFadeInSeconds);
            float hold = Mathf.Max(0f, profile.wallMemoryHoldSeconds);
            float fadeOut = Mathf.Max(0.01f, profile.wallMemoryFadeOutSeconds);

            quads.Begin();
            for (int i = 0; i < memory.Count; i++)
            {
                memory.Get(i, out Vector2 a, out Vector2 b, out float born);
                float age = Time.time - born;
                float rise = Mathf.Clamp01(age / fadeIn);
                float fall = 1f - Mathf.Clamp01((age - hold) / fadeOut);
                color.a = profile.wallMemoryAlpha * rise * fall;
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
