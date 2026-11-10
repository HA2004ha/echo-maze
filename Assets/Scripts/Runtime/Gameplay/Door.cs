using EchoMaze.Audio;
using EchoMaze.Core;
using EchoMaze.Data;
using UnityEngine;

namespace EchoMaze.Gameplay
{
    /// <summary>
    /// A door: one leaf, hinged at one end, standing in the mouth of a corridor.
    ///
    /// The leaf is placed on the boundary between the corridor the player walks down and the cell beyond
    /// it, not in the middle of the cell. That is what makes a doorway read as a doorway: the leaf spans
    /// the corridor exactly, end to end, flush with the two wall faces that flank the opening, so a shut
    /// door is a wall across the corridor and a swing of 90 degrees clears the whole opening. A leaf at
    /// the cell centre leaves half a cell of extra corridor in front of it, and any stub added to fill
    /// that gap is a little wall standing in a corridor — which is the thing this geometry exists to
    /// avoid.
    ///
    /// The door is the one object the game *draws*: one leaf seen from above, hinged at one end. The
    /// sprite is not always visible, because the black screen is the design's premise — it is shown only
    /// once the player has earned it with one of the game's two senses: a wave that reached the leaf, or
    /// a body close enough to use the door. The drawn leaf and the leaf's collider are the same box in
    /// the same place, so the picture and the thing the wave measures cannot disagree.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Door : MonoBehaviour, IInteractable
    {
        /// <summary>Depth of the leaf along the passage, as a fraction of the cell. Thin: it is a door, not a wall.</summary>
        const float ThicknessCells = 0.1f;

        [Header("Leaf")]
        [Tooltip("Colour of the drawn leaf. Amber is the colour the victory map already uses for doors, so the " +
                 "object and its map symbol agree; it is deliberately neither the cyan of the wave nor the " +
                 "orange of a living thing.")]
        [SerializeField] Color leafColor = new Color(0.85f, 0.6f, 0.22f, 1f);
        [Tooltip("Sorting order on the Default layer. -1 keeps the leaf behind the player; the Echo layer sorts " +
                 "above Default and is for things meant to read over the body (touch marks, arcs).")]
        [SerializeField] int leafSortingOrder = -1;

        [Header("Motion")]
        [Tooltip("Seconds the leaf takes to swing 90 degrees about its hinge.")]
        [SerializeField, Range(0.05f, 1.5f)] float swingSeconds = 0.4f;

        [Header("Reveal")]
        [Tooltip("The leaf is drawn while the player is this close, in world units. About three times " +
                 "interactionRange, so the door is fully drawn before the open prompt appears.")]
        [SerializeField, Min(0.5f)] float revealDistance = 3.5f;
        [Tooltip("Seconds the leaf stays drawn after a ping reached it. Also the hold used when the player " +
                 "opens or closes it, so the swing is seen even if they step back.")]
        [SerializeField, Min(0f)] float pingRevealSeconds = 4f;
        [Tooltip("Seconds the leaf takes to fade in or out. It is disabled entirely once invisible, so an " +
                 "undiscovered door costs no draw call.")]
        [SerializeField, Range(0.02f, 1f)] float fadeSeconds = 0.25f;

        const int LeafTextureEdge = 4;

        EchoDirector director;
        Player player;
        Collider2D leafCollider;
        bool startClosed = true;

        Transform hinge;
        SpriteRenderer leaf;
        Sprite leafSprite;
        Vector2 leafPosition;
        float leafLength = 2f;
        float leafThickness = 0.2f;
        float closedAngle;
        float swingSign = 1f;
        float shown;
        float swung;
        float revealedUntil = float.NegativeInfinity;
        bool built;

        ContactFilter2D revealFilter;
        readonly RaycastHit2D[] revealHits = new RaycastHit2D[4];

        public bool IsClosed { get; private set; }

        /// <summary>
        /// Set by whoever builds the door — the maze builder at runtime, or a test rig. The door owns all of
        /// its own geometry: a door cell is a door, so the leaf, its collider and the drawn leaf are created
        /// here rather than by the level.
        /// </summary>
        /// <param name="corridorDirection">Unit direction of travel from the corridor the player approaches
        /// down, into the door's cell. The leaf is placed on the boundary behind the cell, so the corridor
        /// ends at the door and everything past the leaf is the new corridor.</param>
        /// <param name="cellSize">The grid cell the door occupies, which is the width of the corridor and
        /// therefore the length of the leaf.</param>
        public void Build(EchoDirector owningDirector, Player owningPlayer, bool closedAtStart,
                          Vector2 corridorDirection, float cellSize)
        {
            director = owningDirector;
            player = owningPlayer;
            startClosed = closedAtStart;
            IsClosed = closedAtStart;

            Vector2 intoCell = corridorDirection.sqrMagnitude > 1e-6f
                ? corridorDirection.normalized
                : Vector2.up;

            // Across the corridor, i.e. along the leaf. The hinge is the -across end: it is the corner of
            // the wall face the leaf closes against, and the swing therefore goes into the new cell, away
            // from the player still standing in the corridor.
            Vector2 across = new Vector2(-intoCell.y, intoCell.x);
            leafPosition = (Vector2)transform.position - intoCell * (cellSize * 0.5f);
            closedAngle = Mathf.Atan2(across.y, across.x) * Mathf.Rad2Deg;
            swingSign = Mathf.Sign(across.x * intoCell.y - across.y * intoCell.x);
            if (Mathf.Approximately(swingSign, 0f))
            {
                swingSign = 1f;
            }

            leafLength = Mathf.Max(0.2f, cellSize);
            leafThickness = Mathf.Max(0.05f, cellSize * ThicknessCells);

            BuildHinge(across);
            BuildCollider();
            ApplyBlocks();
            ApplyLeaf();

            if (director != null)
            {
                director.PingEmitted += OnPing;
                revealFilter = RayFan.BuildFilter(director.EchoLayerMask);
                built = true;
            }
        }

        public string Prompt => IsClosed ? "E   OPEN THE DOOR" : "E   CLOSE THE DOOR";

        public bool CanInteract(Player player) => leafCollider != null;

        public void Interact(Player player)
        {
            IsClosed = !IsClosed;
            ApplyBlocks();

            // The player is standing at the door, but they may walk away before the swing finishes: hold the
            // leaf on screen for the reveal window so the animation is always seen by the person who caused it.
            Reveal();

            // Opening and closing are different noises in the GDD's table (low / medium): the first is the
            // wake trigger of every chase space, the second is the move the player makes while being hunted.
            EchoTuningProfile profile = director != null ? director.Profile : null;
            if (director != null && profile != null)
            {
                Vector2 here = transform.position;
                director.ReportNoise(here, IsClosed ? profile.doorCloseLoudness : profile.doorOpenLoudness);
            }

            // Heard from the doorway rather than from the player's head: which side of the room a door is
            // on is information, and a flat sound would throw it away.
            SoundBoard.PlayAt(IsClosed ? SoundId.DoorClose : SoundId.DoorOpen, transform.position);
        }

        /// <summary>
        /// Back to the state the map authored. Called on a checkpoint restart, because a door the player
        /// opened on the run that got them killed has to be shut again: an open door is the one piece of
        /// information a corpse cannot have given away, and leaving it open lets a retry win the room with
        /// the noise it paid for the first time.
        ///
        /// The leaf snaps rather than animating: a restart is a state restore, not an event, and nobody is
        /// watching the doorway while the death screen is up.
        /// </summary>
        public void ResetToStart()
        {
            IsClosed = startClosed;
            ApplyBlocks();

            swung = IsClosed ? 0f : 1f;
            shown = 0f;
            revealedUntil = float.NegativeInfinity;
            ApplyLeaf();
        }

        void Update()
        {
            if (!built)
            {
                return;
            }

            if (player != null &&
                ((Vector2)player.Position - leafPosition).sqrMagnitude <= revealDistance * revealDistance)
            {
                revealedUntil = Mathf.Max(revealedUntil, Time.time + fadeSeconds);
            }

            float targetAlpha = Time.time < revealedUntil ? 1f : 0f;
            float targetSwing = IsClosed ? 0f : 1f;
            bool changed = false;

            if (!Mathf.Approximately(shown, targetAlpha))
            {
                shown = Mathf.MoveTowards(shown, targetAlpha, Time.deltaTime / Mathf.Max(0.02f, fadeSeconds));
                changed = true;
            }

            if (!Mathf.Approximately(swung, targetSwing))
            {
                swung = Mathf.MoveTowards(swung, targetSwing, Time.deltaTime / Mathf.Max(0.05f, swingSeconds));
                changed = true;
            }

            if (changed)
            {
                ApplyLeaf();
            }
        }

        /// <summary>
        /// A ping reveals the door only if its fan actually reached the leaf: same cone, same reach and same
        /// echo mask the wave used, and the nearest thing on the ray has to be the leaf itself. An open leaf
        /// is switched off as a collider, so an open doorway does not echo; a door the player has left open
        /// stays findable by walking up to it.
        /// </summary>
        void OnPing(PingRecord ping)
        {
            if (!built || ping == null || !ping.Allocated || leafCollider == null || !leafCollider.enabled)
            {
                return;
            }

            Vector2 toDoor = leafPosition - ping.origin;
            float distance = toDoor.magnitude;
            if (distance > ping.radius)
            {
                return;
            }

            if (!ping.fullCircle && distance > 1e-4f)
            {
                Vector2 aim = new Vector2(Mathf.Cos(ping.aimAngleRad), Mathf.Sin(ping.aimAngleRad));
                if (Vector2.Angle(toDoor, aim) > ping.coneHalfRad)
                {
                    return;
                }
            }

            if (Peek(ping.origin, leafPosition))
            {
                Reveal();
            }
        }

        /// <summary>True when a ray from the ping origin reaches the leaf and meets nothing else first.</summary>
        bool Peek(Vector2 origin, Vector2 target)
        {
            Vector2 toTarget = target - origin;
            float distance = toTarget.magnitude;
            if (distance <= 1e-4f)
            {
                return true;
            }

            int count = Physics2D.Raycast(origin, toTarget / distance, revealFilter, revealHits, distance + 0.05f);
            int nearest = -1;
            float nearestDistance = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                RaycastHit2D hit = revealHits[i];
                if (hit.collider != null && hit.distance > 0f && hit.distance < nearestDistance)
                {
                    nearestDistance = hit.distance;
                    nearest = i;
                }
            }

            return nearest >= 0 && revealHits[nearest].collider == leafCollider;
        }

        void Reveal() => revealedUntil = Mathf.Max(revealedUntil, Time.time + pingRevealSeconds);

        void BuildHinge(Vector2 across)
        {
            var hingeObject = new GameObject(name + " hinge");
            hingeObject.transform.SetParent(transform, false);
            hingeObject.transform.position = leafPosition - across * (leafLength * 0.5f);
            hingeObject.transform.rotation = Quaternion.Euler(0f, 0f, closedAngle);
            hinge = hingeObject.transform;

            var leafObject = new GameObject(name + " leaf");
            leafObject.transform.SetParent(hinge, false);
            leafObject.transform.localPosition = new Vector3(leafLength * 0.5f, 0f, 0f);
            leafObject.transform.localScale = new Vector3(leafLength, leafThickness, 1f);

            leafSprite = BuildLeafSprite();
            leaf = leafObject.AddComponent<SpriteRenderer>();
            leaf.sprite = leafSprite;
            leaf.sortingOrder = leafSortingOrder;
            leaf.enabled = false;
        }

        /// <summary>
        /// The door's body in the world: one thin leaf across the corridor mouth, an <see cref="EchoSurface"/>
        /// of kind <see cref="SurfaceKind.Plain"/> on `EchoTarget`, i.e. a thing the rays read. It is the only
        /// collider the door owns — a closed leaf is the wall the corridor ends at, and once it swings away
        /// the opening is clear, with no stub left standing in the passage.
        /// </summary>
        void BuildCollider()
        {
            var go = new GameObject(name + " leaf blocker");
            go.transform.SetParent(transform, false);
            go.transform.position = leafPosition;
            go.transform.rotation = Quaternion.Euler(0f, 0f, closedAngle);
            go.layer = LayerMask.NameToLayer("EchoTarget");

            var collider = go.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(leafLength, leafThickness);

            var surface = go.AddComponent<EchoSurface>();
            surface.kind = SurfaceKind.Plain;
            surface.reflectivity = 1f;
            leafCollider = collider;
        }

        void ApplyLeaf()
        {
            if (leaf == null || hinge == null)
            {
                return;
            }

            float swing = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(swung));
            hinge.localRotation = Quaternion.Euler(0f, 0f, closedAngle + 90f * swingSign * swing);

            Color color = leafColor;
            color.a *= Mathf.Clamp01(shown);
            leaf.color = color;
            leaf.enabled = shown > 0.002f;
        }

        void ApplyBlocks()
        {
            if (leafCollider != null)
            {
                leafCollider.enabled = IsClosed;
            }
        }

        /// <summary>
        /// Generated, not imported: the leaf is a bar, not art, and every scene with doors is built from code,
        /// so a sprite asset would have to be committed and referenced for one rectangle. Not serialised, so
        /// this does not break the week-1 rule about runtime sprites in scenes.
        /// </summary>
        static Sprite BuildLeafSprite()
        {
            var texture = new Texture2D(LeafTextureEdge, LeafTextureEdge, TextureFormat.RGBA32, false);
            var white = new Color[LeafTextureEdge * LeafTextureEdge];
            for (int i = 0; i < white.Length; i++)
            {
                white[i] = Color.white;
            }

            texture.SetPixels(white);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, LeafTextureEdge, LeafTextureEdge),
                                 new Vector2(0.5f, 0.5f), LeafTextureEdge);
        }

        void OnDestroy()
        {
            if (director != null)
            {
                director.PingEmitted -= OnPing;
            }

            if (leafSprite != null)
            {
                if (leafSprite.texture != null)
                {
                    Destroy(leafSprite.texture);
                }

                Destroy(leafSprite);
            }
        }
    }
}
