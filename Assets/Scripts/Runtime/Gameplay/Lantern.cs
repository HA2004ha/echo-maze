using EchoMaze.Core;
using EchoMaze.Data;
using UnityEngine;

namespace EchoMaze.Gameplay
{
    /// <summary>
    /// A lamp standing where the room put it: the game's only light that is not a sound.
    ///
    /// The room is drawn by echoes, and an echo is gone as soon as it has arrived — which is the point of the
    /// game and also the reason a player can spend a whole chapter lost. A lantern is the answer that does not
    /// break the rule: it draws nothing the room has not already shown, it only draws the walls within its
    /// reach and keeps drawing them for as long as it stands there. Light makes no noise, so walking into a
    /// lamp's circle is the one way to read part of a room without paying for it.
    ///
    /// It is fixed on purpose. It is not carried, not picked up and not moved by any key: a lamp is a place,
    /// and the thing a place is good for is knowing where you are in the dark. What it lights, it lights
    /// because you walked there.
    ///
    /// Drawn with sprites from a fixed pool, like <see cref="TouchMarks"/> and for the same reason: the echo
    /// field's mesh buffer belongs to the live waves, and a reveal that shared it would blink out whenever the
    /// fan needed the vertices.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Lantern : MonoBehaviour
    {
        /// <summary>Rays of one reveal. Dense enough that two adjacent hits on a wall a room away merge.</summary>
        const int Rays = 192;

        /// <summary>Wall segments one reveal can draw. Six metres of radius is about thirty per face.</summary>
        const int LinePool = 220;

        /// <summary>Above the remembered walls (-10) and the footprints (-8), below the live arcs (0).</summary>
        const int SortingOrder = -6;

        [SerializeField] EchoTuningProfile profile;

        SpriteRenderer glow;
        SpriteRenderer core;
        SpriteRenderer[] lines;
        WallMemory reveal;
        int drawn;
        float retryUntil;

        public bool IsLit { get; private set; }

        /// <summary>Segments the reveal is drawing. Read by the debug window and by the tests.</summary>
        public int RevealedSegments => reveal != null ? reveal.Count : 0;

        /// <summary>Wired by <see cref="EchoMaze.Levels.MazeLevel"/>, which builds the lamps from the map.</summary>
        public void Build(EchoTuningProfile tuning)
        {
            profile = tuning;
            BuildVisuals();
            Lit();
        }

        void Awake()
        {
            // A lamp saved into a scene with its profile already set builds here; the ones the level creates
            // at runtime are wired first and built by Build. A missing profile is an error and not a warning:
            // an invisible lamp is a room that says it has light and does not.
            if (profile != null)
            {
                BuildVisuals();
            }
        }

        void Start() => Lit();

        void Update()
        {
            // The first reveal can land before the physics world has the walls that were made in the same
            // Awake (the level builds geometry, then lamps). Retrying for a second costs nothing and turns a
            // silently unlit lamp into one that lights on the next frame.
            if (retryUntil <= 0f || Time.time > retryUntil)
            {
                return;
            }

            Light(transform.position);
            if (drawn > 0)
            {
                retryUntil = 0f;
            }
        }

        void Lit()
        {
            if (profile == null || lines == null)
            {
                return;
            }

            IsLit = true;
            Dress();
            Light(transform.position);
            if (drawn == 0)
            {
                retryUntil = Time.time + 1f;
            }
        }

        // ---------------------------------------------------------------- light

        /// <summary>
        /// Draws every wall within reach of <paramref name="origin"/>. One ray per degree-ish, each hit
        /// remembered as a short segment along the face it landed on — the same shape
        /// <see cref="TouchMarks"/> remembers, so the lamp and the hand draw the room the same way.
        /// </summary>
        void Light(Vector2 origin)
        {
            if (profile == null || reveal == null || lines == null)
            {
                return;
            }

            reveal.Clear();
            float radius = profile.lanternRevealRadius;
            int mask = LayerMask.GetMask("EchoTarget");

            // A little longer than one memory segment so two neighbouring hits overlap rather than leaving a
            // hairline of dark between them along a wall.
            float half = profile.wallMemorySegmentLength * 0.5f + 0.1f;

            for (int i = 0; i < Rays; i++)
            {
                float angle = i * Mathf.PI * 2f / Rays;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                RaycastHit2D hit = Physics2D.Raycast(origin, direction, radius, mask);
                if (hit.collider == null || hit.distance <= 0.01f)
                {
                    continue;
                }

                Vector2 normal = hit.normal;
                if (normal.sqrMagnitude < 1e-6f)
                {
                    continue;
                }

                // Walls are axis-aligned boxes, so the face is snapped and clipped to the box exactly as a
                // touch is: a diagonal normal at a corner must not draw a line out into the room.
                Vector2 point = hit.point;
                Vector2 along = new Vector2(-normal.y, normal.x);

                if (hit.collider is BoxCollider2D box)
                {
                    Bounds bounds = box.bounds;
                    bool verticalFace = Mathf.Abs(normal.x) > Mathf.Abs(normal.y);
                    if (verticalFace)
                    {
                        point.x = Mathf.Abs(point.x - bounds.min.x) < Mathf.Abs(point.x - bounds.max.x)
                            ? bounds.min.x : bounds.max.x;
                        along = Vector2.up;
                    }
                    else
                    {
                        point.y = Mathf.Abs(point.y - bounds.min.y) < Mathf.Abs(point.y - bounds.max.y)
                            ? bounds.min.y : bounds.max.y;
                        along = Vector2.right;
                    }

                    reveal.Add(Clamp(point - along * half, bounds), Clamp(point + along * half, bounds));
                }
                else
                {
                    // A pillar is a circle: the chord follows the tangent it was hit on, and standing a little
                    // proud of the surface is the price of drawing a curve with one straight line.
                    reveal.Add(point - along * half, point + along * half);
                }
            }

            Draw();
        }

        static Vector2 Clamp(Vector2 v, Bounds box) => new Vector2(
            Mathf.Clamp(v.x, box.min.x, box.max.x), Mathf.Clamp(v.y, box.min.y, box.max.y));

        void Draw()
        {
            drawn = 0;
            if (reveal == null || profile == null)
            {
                return;
            }

            float thickness = profile.baseThickness * profile.wallMemoryThicknessScale * 0.9f;
            Color color = profile.lanternColor;
            color.a = profile.lanternRevealAlpha;

            for (int i = 0; i < reveal.Count && drawn < lines.Length; i++)
            {
                reveal.Get(i, out Vector2 a, out Vector2 b, out _);
                Vector2 delta = b - a;
                float length = delta.magnitude;
                if (length < 0.01f)
                {
                    continue;
                }

                SpriteRenderer line = lines[drawn++];
                line.enabled = true;
                line.transform.position = (a + b) * 0.5f;
                line.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
                line.transform.localScale = new Vector3(length, thickness, 1f);
                line.color = color;
            }

            for (int i = drawn; i < lines.Length; i++)
            {
                lines[i].enabled = false;
            }
        }

        /// <summary>
        /// The lamp's own light: a soft warm halo whose visible fall-off ends at the radius it draws, so the
        /// circle the player sees is the circle the walls are drawn in, and a brighter core that makes the
        /// lamp findable from across a dark room. Warm on purpose — every other cyan in the game is a wall,
        /// an echo or the player.
        /// </summary>
        void Dress()
        {
            if (glow == null || profile == null)
            {
                return;
            }

            float radius = profile.lanternRevealRadius;

            // The halo sprite is 2.2 world units across at scale 1, so this makes its edge the radius.
            glow.transform.localPosition = Vector3.zero;
            glow.transform.localScale = Vector3.one * (radius * 1.1f / 2.2f);

            Color halo = profile.lanternColor;
            halo.a = profile.lanternGlowAlpha;
            glow.color = halo;

            if (core != null)
            {
                core.color = profile.lanternColor;
            }
        }

        // ---------------------------------------------------------------- visuals

        void BuildVisuals()
        {
            if (glow != null)
            {
                return;
            }

            if (profile == null)
            {
                Debug.LogError(name + " has no EchoTuningProfile: it would be an invisible lamp. Rebuild the " +
                               "scene with Tools/Echo Maze.", this);
                return;
            }

            reveal = new WallMemory(LinePool, profile.wallMemoryMergeDistance);

            glow = EnsureSprite("Glow", SortingOrder - 1);
            glow.sprite = BuildRadialSprite(64, 2.2f);
            glow.color = profile.lanternColor;

            core = EnsureSprite("Core", SortingOrder + 1);
            core.sprite = BuildRadialSprite(16, 14f);
            core.color = profile.lanternColor;

            lines = new SpriteRenderer[LinePool];
            Sprite lineSprite = BuildLineSprite();
            for (int i = 0; i < lines.Length; i++)
            {
                SpriteRenderer line = EnsureSprite("Line_" + i, SortingOrder);
                line.sprite = lineSprite;
                line.enabled = false;
                lines[i] = line;
            }
        }

        SpriteRenderer EnsureSprite(string name, int order)
        {
            Transform child = transform.Find(name);
            if (child == null)
            {
                var go = new GameObject(name);
                go.transform.SetParent(transform, false);
                child = go.transform;
            }

            // Not `GetComponent() ?? AddComponent()`: on a fresh child the getter returns Unity's fake-null,
            // which the C# null-coalescing operator does not see, so the add is skipped and the next line
            // throws MissingComponentException on a component that was never there. Same defect as the death
            // notice, same fix: ask, then add, then use what the object has.
            SpriteRenderer renderer = child.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                renderer = child.gameObject.AddComponent<SpriteRenderer>();
            }

            renderer.sortingLayerName = "Echo";
            renderer.sortingOrder = order;
            return renderer;
        }

        /// <summary>A white line, one world unit long: the pool scales it to the segment it draws.</summary>
        static Sprite BuildLineSprite()
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            for (int y = 0; y < 2; y++)
            {
                for (int x = 0; x < 2; x++)
                {
                    texture.SetPixel(x, y, Color.white);
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);
        }

        /// <summary>
        /// A round fall-off. The exponent is what makes it read as light rather than as a circle: a linear
        /// ramp has a visible edge, and a fifth-power one has a centre and a dark.
        /// </summary>
        static Sprite BuildRadialSprite(int edge, float falloff)
        {
            var texture = new Texture2D(edge, edge, TextureFormat.RGBA32, false);
            float centre = (edge - 1) * 0.5f;
            for (int y = 0; y < edge; y++)
            {
                for (int x = 0; x < edge; x++)
                {
                    float dx = (x - centre) / centre;
                    float dy = (y - centre) / centre;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Pow(Mathf.Clamp01(1f - r), falloff);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, edge, edge), new Vector2(0.5f, 0.5f), edge);
        }
    }
}
