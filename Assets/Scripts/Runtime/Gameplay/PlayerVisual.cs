using UnityEngine;

namespace EchoMaze.Gameplay
{
    /// <summary>
    /// The character: which of the four poses to show, and the small motions that make it a living thing
    /// rather than a sticker. Deliberately not an Animator — there is one still per pose, so the only
    /// motion available is the kind you compute, and a state machine would add the project's first
    /// .anim/.controller assets to get there.
    ///
    /// Everything it writes goes to the <c>Visuals</c> child transform, never to this GameObject. The
    /// root carries the Rigidbody2D and the collider, so a squash on the root would resize what collides
    /// with walls and fight the body's interpolation.
    ///
    /// The component is optional by design. <c>Player</c> never looks at it, the PlayMode rigs never
    /// create it, and a game with no visual on the player behaves exactly as it did before this file
    /// existed. When its setup is incomplete it disables itself without a word: the scene builder is the
    /// thing that promises the sprites exist, and it has already warned if they do not.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerVisual : MonoBehaviour
    {
        [Header("Poses (wired by the scene builder)")]
        [Tooltip("Facing the camera, i.e. walking down the screen.")]
        [SerializeField] Sprite frontSprite;
        [Tooltip("Facing away, walking up the screen. The pose a retreating player sees.")]
        [SerializeField] Sprite backSprite;
        [Tooltip("The canonical side view, drawn facing screen right. Facing left is the same texture " +
                 "flipped: the two profile files are bit-exact mirrors (measured 2026-10-29 on the 4096 " +
                 "delivery: 0 differing pixels), so a second sprite would be 2.7 MB of memory (1024 squared, " +
                 "DXT5, with mip levels) to redraw nothing. If the art team ever lights the two sides " +
                 "differently, this stops being free — see Docs/ISSUES.md #5.")]
        [SerializeField] Sprite sideSprite;

        [Header("Idle breath")]
        [Tooltip("Fractional vertical scale swing while standing still. 0.012 of an 89 pixel body is about " +
                 "one pixel: enough to be alive, not enough to draw the eye off the wave.")]
        [Range(0f, 0.05f)] [SerializeField] float breatheAmplitude = 0.012f;
        [Range(0.1f, 2f)] [SerializeField] float breatheHz = 0.4f;

        [Header("Step")]
        [Tooltip("Hop height at the top of a stride, world units. 0.03 is 2.7 pixels: felt, not seen.")]
        [Range(0f, 0.1f)] [SerializeField] float bobHeight = 0.03f;
        [Tooltip("How far the body dips on the frame a footstep lands, as a fraction of its height.")]
        [Range(0f, 0.1f)] [SerializeField] float landSquash = 0.02f;

        [Header("Lean")]
        [Tooltip("Degrees the body tips into the direction it is moving, at full walk speed.")]
        [Range(0f, 10f)] [SerializeField] float leanDegrees = 3f;
        [Tooltip("The walkSpeed this lean was tuned against; it saturates here so a future speed change " +
                 "cannot make the character tip further. Re-tune if TUNING.md's walkSpeed moves.")]
        [Min(0.1f)] [SerializeField] float leanReferenceSpeed = 2.5f;

        [Tooltip("Seconds of holding Shift for all motion to flatten to nothing. Fast enough that the " +
                 "animation itself visibly goes quiet the moment you decide to be silent, slow enough " +
                 "that it does not snap.")]
        [Range(0.05f, 1f)] [SerializeField] float calmSeconds = 0.2f;

        [Header("Pose stability")]
        [Tooltip("Degrees past a sector boundary the heading has to clear before a neighbour is allowed. " +
                 "A WASD diagonal is an exact |vx| = |vy| tie by construction, and a cursor 10 px off the " +
                 "body turns one pixel of shake into ~5.7 degrees of aim, so without a band the pose flips " +
                 "as often as those numbers wobble. 10 degrees on a 45-degree boundary still leaves 35 " +
                 "degrees of responsive angle either side of a switch; issue 2 in Docs/ISSUES.md halves both " +
                 "numbers when the fan widens, because eight boundaries are twice as many places to stick.")]
        [Range(0f, 20f)] [SerializeField] float boundaryDegrees = 10f;
        [Tooltip("Shortest interval between two pose changes. This is the gate that kills the class " +
                 "wholesale, including the handover when a key is released, where the velocity crosses the " +
                 "movement deadzone constantly and hysteresis cannot see it because the direction is what " +
                 "changed, not its sector. Costs 100 ms of response, which is less than one stride.")]
        [Range(0f, 0.5f)] [SerializeField] float dwellSeconds = 0.1f;
        [Tooltip("Pointer-to-body separation below which the aim is noise: the angle exists but nothing " +
                 "about it is stable, so the current pose is held instead of resolved. 0.27 m is ~24 px at " +
                 "the 1080p target, about a quarter of the character's own width, and parking the cursor on " +
                 "the body is the normal resting state rather than an edge case.")]
        [Range(0f, 1f)] [SerializeField] float aimNoiseRadius = 0.27f;

        /// <summary>How far a stride has run since the last footstep landed. 0 when not walking.</summary>
        public float Phase { get; private set; }

        /// <summary>1 walking, 0 fully quieted by Shift. Every motion term is multiplied by it.</summary>
        public float Calm => calm;

        public PlayerPose CurrentPose { get; private set; } = PlayerPose.Front;

        Player player;
        Rigidbody2D body;
        Transform visuals;
        SpriteRenderer sprite;
        float calm = 1f;
        float lastPoseChangeTime = float.NegativeInfinity;

        void Awake()
        {
            player = GetComponent<Player>();
            body = GetComponent<Rigidbody2D>();
            visuals = transform.Find("Visuals");
            sprite = visuals != null ? visuals.GetComponent<SpriteRenderer>() : null;

            if (player == null || body == null || sprite == null ||
                frontSprite == null || backSprite == null || sideSprite == null)
            {
                enabled = false;
                return;
            }

            ApplyPose(PlayerFacing.Resolve(body.linearVelocity, player.AimRadians));

            // The first answer is not a change, and the dwell is only meant to space real ones out.
            lastPoseChangeTime = float.NegativeInfinity;
        }

        void Update()
        {
            Vector2 velocity = body.linearVelocity;

            // Unscaled, because the retry after a death runs on unscaled time: the character should not
            // take an extra second to go quiet just because the world stopped.
            calm = Mathf.MoveTowards(calm, player.IsQuiet ? 0f : 1f,
                                     Time.unscaledDeltaTime / Mathf.Max(0.01f, calmSeconds));

            if (!player.IsAlive)
            {
                // Dead: hold the last pose and stand the body up straight rather than crumpling it, so
                // the black screen is the feedback and not the sprite.
                Phase = 0f;
                ApplyMotion(Vector2.zero, 0f, 0f);
                return;
            }

            PlayerPose pose = ResolveSticky(velocity);
            if (pose != CurrentPose)
            {
                ApplyPose(pose);
            }

            Phase = PlayerFacing.StepPhase(Time.time, player.NextStepTime, player.FootstepPeriod);
            ApplyMotion(velocity, Phase, Mathf.Clamp01(velocity.magnitude / 0.3f));
        }

        /// <summary>
        /// Which pose to show, with the three gates <c>Docs/ISSUES.md</c> #3 asks for, none of which belong
        /// in the pure resolver: the sector map stays boundary-deterministic so it stays testable, and the
        /// stickiness is a property of drawing, not of aiming.
        ///
        /// The order matters. The cursor radius is checked before anything is resolved, because a heading
        /// read off a pointer sitting on the character is not a measurement; the dwell is checked before the
        /// band, because it is the cheaper test and it also has to gate the movement/aim handover, where the
        /// direction changes without crossing anybody's boundary.
        /// </summary>
        PlayerPose ResolveSticky(Vector2 velocity)
        {
            bool walking = velocity.sqrMagnitude >= PlayerFacing.MoveDeadzoneSqr;

            if (!walking && player.AimDistance <= aimNoiseRadius)
            {
                return CurrentPose;
            }

            Vector2 heading = walking
                ? velocity
                : new Vector2(Mathf.Cos(player.AimRadians), Mathf.Sin(player.AimRadians));

            PlayerPose asked = PlayerFacing.ResolveDirection(heading);
            if (asked == CurrentPose)
            {
                return CurrentPose;
            }

            if (Time.unscaledTime - lastPoseChangeTime < dwellSeconds)
            {
                return CurrentPose;
            }

            // How far the heading has left the pose we are drawing. Vector2.Angle rather than arithmetic on
            // the aim, so the wrap at ±π stays somebody else's problem.
            if (Vector2.Angle(heading, PlayerFacing.PoseCentre(CurrentPose))
                <= PlayerFacing.SectorHalfWidthDegrees + boundaryDegrees)
            {
                return CurrentPose;
            }

            return asked;
        }

        void ApplyPose(PlayerPose pose)
        {
            CurrentPose = pose;
            lastPoseChangeTime = Time.unscaledTime;

            switch (pose)
            {
                case PlayerPose.Front:
                    sprite.sprite = frontSprite;
                    sprite.flipX = false;
                    break;
                case PlayerPose.Back:
                    sprite.sprite = backSprite;
                    sprite.flipX = false;
                    break;
                case PlayerPose.ProfileRight:
                    sprite.sprite = sideSprite;
                    sprite.flipX = false;
                    break;
                default:
                    sprite.sprite = sideSprite;
                    sprite.flipX = true;
                    break;
            }
        }

        /// <summary>
        /// Four independent knobs, one write each. The breath only runs when still and the step only runs
        /// when moving, so the two never fight; <c>calm</c> multiplies the lot, which is what makes
        /// silent walking look like something rather than just a slower number.
        /// </summary>
        void ApplyMotion(Vector2 velocity, float phase, float moving)
        {
            // The breath is gated on being alive as well as on being still: without the first half of that
            // condition, the pose a dead body is held in rises and falls once every two and a half seconds.
            float breathe = player.IsAlive ? Mathf.Sin(Time.time * Mathf.PI * 2f * breatheHz) * (1f - moving) : 0f;
            float stride = Mathf.Sin(Mathf.PI * phase);
            float impact = Mathf.Clamp01(1f - phase * 4f) * moving;
            float lean = leanDegrees * Mathf.Clamp(velocity.x / leanReferenceSpeed, -1f, 1f);

            float k = calm;
            float stretch = (breatheAmplitude * breathe - landSquash * impact) * k;

            visuals.localPosition = new Vector3(0f, bobHeight * stride * moving * k, 0f);
            visuals.localScale = new Vector3(1f - stretch * 0.5f, 1f + stretch, 1f);
            visuals.localRotation = Quaternion.Euler(0f, 0f, -lean * k);
        }
    }
}
