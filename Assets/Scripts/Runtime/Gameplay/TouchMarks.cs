using UnityEngine;

namespace EchoMaze.Gameplay
{
    /// <summary>
    /// Touch marks: when the body meets a wall, a dim short segment of that wall flashes at the contact
    /// point and fades in about half a second (GDD 3.0: "the player must not be forced to ping for every
    /// step").
    ///
    /// Drawn from a small pool of sprites rather than from the wave mesh: a mark belongs to a place on a
    /// wall, not to the ping that happened to be in the air, so it has to outlive that ping and stay put
    /// while the arcs collapse past it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TouchMarks : MonoBehaviour
    {
        [Tooltip("Seconds a mark takes to fade. The GDD's number.")]
        [Range(0.1f, 2f)] [SerializeField] float fadeSeconds = 0.5f;
        [Tooltip("World size of the dash: long along the wall, thin into it. One world unit is about ninety " +
                 "pixels in the blockout camera, so this is roughly 63 by 8 pixels.")]
        [SerializeField] Vector2 dashSize = new Vector2(0.7f, 0.09f);
        [Tooltip("Marks sit below the wave in brightness: they tell you where a wall is, not what it is.")]
        [SerializeField] Color markColor = new Color(0.498f, 0.914f, 0.941f, 0.5f);

        [Tooltip("Marks kept on the wall at once. A bump is one mark, so this only has to survive a player " +
                 "dragging themselves along a corridor.")]
        [SerializeField, Range(2, 24)] int poolSize = 10;

        const int SpriteEdge = 32;

        SpriteRenderer[] pool;
        float[] expiresAt;
        float[] startAlpha;
        Sprite dash;
        int cursor;

        void Awake()
        {
            dash = BuildDash();

            pool = new SpriteRenderer[Mathf.Max(2, poolSize)];
            expiresAt = new float[pool.Length];
            startAlpha = new float[pool.Length];

            for (int i = 0; i < pool.Length; i++)
            {
                var child = new GameObject("Mark_" + i);
                child.transform.SetParent(transform, false);
                var renderer = child.AddComponent<SpriteRenderer>();
                renderer.sprite = dash;
                renderer.sortingLayerName = "Echo";
                renderer.enabled = false;
                pool[i] = renderer;
            }
        }

        void Update()
        {
            for (int i = 0; i < pool.Length; i++)
            {
                SpriteRenderer renderer = pool[i];
                if (!renderer.enabled)
                {
                    continue;
                }

                float remaining = (expiresAt[i] - Time.time) / Mathf.Max(0.01f, fadeSeconds);
                if (remaining <= 0f)
                {
                    renderer.enabled = false;
                    continue;
                }

                Color color = markColor;
                color.a = startAlpha[i] * remaining;
                renderer.color = color;
            }
        }

        /// <summary>
        /// Flashes a mark at a world contact point, laid out along the surface rather than across it.
        /// </summary>
        public void Flash(Vector2 point, Vector2 surfaceNormal)
        {
            if (surfaceNormal.sqrMagnitude < 1e-6f)
            {
                return;
            }

            int index = cursor;
            cursor = (cursor + 1) % pool.Length;
            SpriteRenderer renderer = pool[index];

            renderer.enabled = true;
            renderer.transform.position = new Vector3(point.x, point.y, 0f);
            renderer.transform.rotation = Quaternion.Euler(0f, 0f,
                Mathf.Atan2(surfaceNormal.y, surfaceNormal.x) * Mathf.Rad2Deg - 90f);
            renderer.transform.localScale = new Vector3(dashSize.x, dashSize.y, 1f);
            startAlpha[index] = markColor.a;
            expiresAt[index] = Time.time + fadeSeconds;
        }

        /// <summary>Marks still on the wall. Read by the tests and by the debug window.</summary>
        public int ActiveCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < pool.Length; i++)
                {
                    if (pool[i].enabled)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        /// <summary>
        /// Generated, not imported: this is a line, not art, and the alternative is a scene reference to a
        /// texture that has to be shipped and re-imported for the sake of one soft rectangle. (A runtime
        /// sprite could never be *saved* into a scene, which is the week-1 lesson this does not violate:
        /// nothing here is serialised.)
        /// </summary>
        static Sprite BuildDash()
        {
            var texture = new Texture2D(SpriteEdge, SpriteEdge, TextureFormat.RGBA32, false);
            float centre = (SpriteEdge - 1) * 0.5f;
            for (int y = 0; y < SpriteEdge; y++)
            {
                for (int x = 0; x < SpriteEdge; x++)
                {
                    float along = Mathf.Clamp01(1f - Mathf.Abs(x - centre) / centre);
                    float across = Mathf.Clamp01(1f - Mathf.Abs(y - centre) / (SpriteEdge * 0.22f));
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, along * along * across * across));
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, SpriteEdge, SpriteEdge),
                                 new Vector2(0.5f, 0.5f), SpriteEdge);
        }
    }
}
