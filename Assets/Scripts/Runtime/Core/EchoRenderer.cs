using EchoMaze.Data;
using UnityEngine;
using UnityEngine.Rendering;

namespace EchoMaze.Core
{
    /// <summary>
    /// Draws the whole wave field as one mesh in one draw call, with per-sample colour and
    /// thickness. Chosen over LineRenderer (which cannot vary width per sample and multiplies
    /// components on every break) and over Sprite Shape (built for filled splines).
    ///
    /// Unity 6000.6 has no Mesh.SetIndexCount, so the index buffer is written once and unused
    /// quads collapse to a degenerate point with zero colour instead of shrinking the buffer.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class EchoRenderer : MonoBehaviour, IEchoRenderer
    {
        public const int MaxQuads = 2400;
        const int VertsPerQuad = 4;
        const int EmissionSegments = 40;

        Mesh mesh;
        Vector3[] verts;
        Color[] colors;
        Vector2[] uvs;
        Vector2[] emissionPath;

        MeshFilter meshFilter;
        int cursor;
        int lastVertCount;
        int vertCount;

        public int LastQuadCount { get; private set; }
        public int OverflowCount { get; private set; }

        void Awake()
        {
            BuildBuffers();
        }

        /// <summary>
        /// Buffers are built lazily as well as in Awake: a component added by a script in EditMode never
        /// receives Awake, and the readability tests draw arcs headless.
        /// </summary>
        void BuildBuffers()
        {
            if (verts != null)
            {
                return;
            }

            if (meshFilter == null)
            {
                meshFilter = GetComponent<MeshFilter>();
            }

            var meshRenderer = GetComponent<MeshRenderer>();

            vertCount = MaxQuads * VertsPerQuad;
            verts = new Vector3[vertCount];
            colors = new Color[vertCount];
            uvs = new Vector2[vertCount];
            emissionPath = new Vector2[EmissionSegments + 1];

            var indices = new int[MaxQuads * 6];
            for (int q = 0; q < MaxQuads; q++)
            {
                int v = q * VertsPerQuad;
                int o = q * 6;
                indices[o] = v;
                indices[o + 1] = v + 1;
                indices[o + 2] = v + 2;
                indices[o + 3] = v;
                indices[o + 4] = v + 2;
                indices[o + 5] = v + 3;
            }

            for (int i = 0; i < vertCount; i++)
            {
                uvs[i] = new Vector2(i % VertsPerQuad < 2 ? 0f : 1f, i % VertsPerQuad == 1 || i % VertsPerQuad == 2 ? 1f : 0f);
            }

            mesh = new Mesh { name = "EchoField" };
            mesh.MarkDynamic();
            mesh.SetVertices(verts, 0, vertCount, MeshUpdateFlags.DontRecalculateBounds);
            mesh.SetColors(colors, 0, vertCount, MeshUpdateFlags.DontRecalculateBounds);
            mesh.SetUVs(0, uvs, 0, vertCount, MeshUpdateFlags.DontRecalculateBounds);
            mesh.SetIndices(indices, MeshTopology.Triangles, 0, false);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 64f);

            meshFilter.mesh = mesh;
            if (meshRenderer != null)
            {
                meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
                meshRenderer.receiveShadows = false;
            }
        }

        public void BeginFrame()
        {
            BuildBuffers();
            cursor = 0;
            OverflowCount = 0;

            // The index buffer always references all MaxQuads quads, so the mesh vertex count never
            // changes. Shrinking the uploads to the used prefix instead makes Unity reject every
            // frame with "vertices is too small / colors out of bounds", so unused quads are cleared
            // in place: collapsed onto the origin they cover no pixel, and a zero colour additive-blends
            // to nothing.
            for (int i = 0; i < lastVertCount; i++)
            {
                verts[i] = Vector3.zero;
                colors[i] = Color.clear;
            }
        }

        public void AddArc(PingRecord ping, int arcIndex, float baseRadius, float alpha, float time,
                          EchoTuningProfile profile)
        {
            if (ping == null || alpha <= 0f)
            {
                return;
            }

            float gain = ArcMath.Deformation(arcIndex, profile);
            float half = profile.baseThickness * 0.5f;
            float gainSpan = Mathf.Max(1e-5f, profile.brightnessGain);
            int n = ping.rayCount;

            // A closed ring is drawn as one extra segment back to the first sample; a cone is not, so
            // the fan's edges stay edges and the eye can tell "I am surrounded" from "ahead of me".
            int limit = ping.fullCircle ? n + 1 : n;

            bool hasPrevious = false;
            Vector2 previous = Vector2.zero;
            int previousIndex = -1;

            for (int i = 0; i < limit; i++)
            {
                int index = ping.fullCircle && i == n ? 0 : i;

                // A missing reflection is a hole in the arc, never a long line to nowhere.
                if (!ArcMath.HasReflection(ping, index))
                {
                    hasPrevious = false;
                    continue;
                }

                float r = ArcMath.SampleRadius(baseRadius, gain, ping.d[index], ping.dMin);
                if (r <= 0f || float.IsNaN(r))
                {
                    hasPrevious = false;
                    continue;
                }

                Vector2 current = ArcMath.Polar(ping.origin, ArcMath.SampleAngle(ping, index), r);
                if (hasPrevious && !ping.broken[previousIndex])
                {
                    float brightness = ping.brightness[previousIndex];
                    Color tint = profile.arcColor;
                    if (SurfaceRead.IsCreature(ping.kind[previousIndex]))
                    {
                        brightness *= SurfaceRead.Shimmer(previousIndex, time, profile);

                        // The GDD's second tone: a segment that bounced off something alive is warm, so a
                        // player never has to decode a shape to know part of the wave came back alive.
                        tint = profile.creatureArcColor;
                    }

                    float bend = Mathf.Clamp01((brightness - profile.brightnessBase) / gainSpan);
                    AddSegment(previous, current, half * Mathf.Lerp(0.7f, 1.5f, bend), tint,
                               ping.amp[previousIndex] * brightness * alpha);
                }

                previous = current;
                previousIndex = index;
                hasPrevious = true;
            }
        }

        public void AddConeOutline(Vector2 origin, float aimRad, float coneHalfRad, float radius,
                                   float alpha, EchoTuningProfile profile)
        {
            if (alpha <= 0f || radius <= 0f)
            {
                return;
            }

            float half = profile.baseThickness * 0.5f;
            float span = 2f * coneHalfRad;
            for (int i = 0; i <= EmissionSegments; i++)
            {
                emissionPath[i] = ArcMath.Polar(origin, aimRad - coneHalfRad + span * (i / (float)EmissionSegments), radius);
            }

            for (int i = 1; i <= EmissionSegments; i++)
            {
                AddSegment(emissionPath[i - 1], emissionPath[i], half, profile.arcColor, alpha);
            }
        }

        public void EndFrame()
        {
            int count = cursor * VertsPerQuad;
            if (count > 0 || lastVertCount > 0)
            {
                mesh.SetVertices(verts, 0, vertCount, MeshUpdateFlags.DontRecalculateBounds);
                mesh.SetColors(colors, 0, vertCount, MeshUpdateFlags.DontRecalculateBounds);
            }

            lastVertCount = count;
            LastQuadCount = cursor;
        }

        void AddSegment(Vector2 a, Vector2 b, float halfWidth, Color baseColor, float brightness)
        {
            if (cursor >= MaxQuads || brightness <= 0f)
            {
                OverflowCount++;
                return;
            }

            Vector2 dir = b - a;
            float len = dir.magnitude;
            if (len <= 1e-6f)
            {
                return;
            }

            dir /= len;
            Vector2 side = new Vector2(-dir.y, dir.x) * halfWidth;
            Color c = new Color(baseColor.r, baseColor.g, baseColor.b, Mathf.Clamp01(baseColor.a * brightness));

            int v = cursor * VertsPerQuad;
            verts[v] = a - side;
            verts[v + 1] = a + side;
            verts[v + 2] = b + side;
            verts[v + 3] = b - side;

            colors[v] = c;
            colors[v + 1] = c;
            colors[v + 2] = c;
            colors[v + 3] = c;

            cursor++;
        }
    }
}
