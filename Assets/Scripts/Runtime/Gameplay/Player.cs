using System;
using EchoMaze.Core;
using EchoMaze.Data;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EchoMaze.Gameplay
{
    /// <summary>
    /// The player: WASD/arrows to move, the mouse to aim, Shift for a step that makes no footstep noise,
    /// Space held to charge the manual echo, F for the 360 degree weak pulse, and a touch mark plus a very
    /// small noise wherever the body meets a wall.
    ///
    /// Reads Keyboard.current / Pointer.current directly instead of going through GameInput: the
    /// .inputactions rewrite is deliberately a week-3 task. See Docs/DESIGN_DECISIONS.md.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class Player : MonoBehaviour
    {
        [SerializeField] EchoDirector director;
        [SerializeField] TouchMarks marks;

        Rigidbody2D body;
        Vector2 lastInput;
        float aimRad;
        float aimDistance = float.PositiveInfinity;
        bool hasAim;
        float chargeStart = -1f;
        float nextStepTime;
        readonly Collider2D[] interactHits = new Collider2D[16];
        ContactFilter2D interactFilter;
        IInteractable nearby;

        /// <summary>False after the Crawler touched us and before the retry put us back at the room start.</summary>
        public bool IsAlive { get; private set; } = true;

        /// <summary>True while Shift is held: the silent step, which is the only cheap move the game offers.</summary>
        public bool IsQuiet { get; private set; }

        /// <summary>
        /// Where the body is, on the physics clock rather than the transform's. The noise zones and the
        /// sanctuaries ask this instead of reading <c>transform.position</c> so that a query made from
        /// <c>Update</c> cannot disagree with where the collider actually is.
        /// </summary>
        public Vector2 Position => body != null ? body.position : (Vector2)transform.position;

        /// <summary>
        /// True while a movement key is held. Not "while the position changes": walking into a wall is a
        /// footstep, and a floor of glass has to hear it, because the GDD's cost is for the noise you make
        /// rather than the distance you cover.
        /// </summary>
        public bool IsMoving => lastInput.sqrMagnitude >= 0.01f;

        /// <summary>
        /// The thing E would act on right now — the nearest door or exit within the profile's interaction
        /// range — or null when there is nothing to touch. The HUD reads it for the prompt line; nobody
        /// else decides what E does.
        /// </summary>
        public IInteractable Nearby => nearby;

        public float AimRadians => aimRad;

        /// <summary>
        /// When the next footstep is due. Read-only and dumb on purpose: the step animation is the only
        /// consumer and it needs the clock, not a copy of it. <c>Player</c> never looks at the visual, so
        /// the dependency runs one way.
        /// </summary>
        public float NextStepTime => nextStepTime;

        /// <summary>
        /// How far the pointer is from the body, in world units, on the frame the aim was last refreshed;
        /// <see cref="float.PositiveInfinity"/> until there has been one. The visual needs the distance and
        /// not just the angle, because an aim read three pixels off the character is the same information
        /// as noise: the direction is real but nothing about it is stable (Docs/ISSUES.md #3, cause 3).
        /// </summary>
        public float AimDistance => aimDistance;

        /// <summary>
        /// The stride the bounce is measured against. Zero when the profile is missing, which parks the
        /// animation instead of inventing a rhythm the footsteps do not keep.
        /// </summary>
        public float FootstepPeriod => DirectorProfile != null ? DirectorProfile.footstepInterval : 0f;

        /// <summary>
        /// How long Space has been held toward a manual shout, or -1 when no charge is in progress. The only
        /// consumer is the aim cursor, which walks its head brightness up with this and has to peak on the
        /// frame <see cref="ArcMath.ManualLoudness"/> does. Negative rather than zero, because "a tap that
        /// has not been held at all" is a state the cursor draws differently from "no charge at all".
        /// </summary>
        public float ChargeSeconds => chargeStart >= 0f ? Time.time - chargeStart : -1f;

        /// <summary>
        /// Seconds of hold that take the manual echo from a tap to a full shout. Zero when the profile is
        /// missing, which flattens the cursor's charge lift instead of dividing by nothing.
        /// </summary>
        public float ManualChargeSeconds => DirectorProfile != null ? DirectorProfile.manualChargeSeconds : 0f;

        public event Action Died;

        EchoTuningProfile DirectorProfile => director != null ? director.Profile : null;

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();

            // No FindAnyObjectByType fallback here on purpose. Week 1's defect 2 was a scene reference
            // that was never wired, kept alive by a fallback that hid it; the builder wires it, and if it
            // does not, that is a bug that has to be loud.
            if (director == null)
            {
                Debug.LogError(name + " has no EchoDirector reference: no sound will ever be emitted. " +
                               "Rebuild the scene with Tools/Echo Maze.", this);
            }

            if (marks == null)
            {
                Debug.LogError(name + " has no TouchMarks reference: bumping into walls will be silent and " +
                               "invisible. Rebuild the scene with Tools/Echo Maze.", this);
            }

            // The door and exit probes are triggers on the Trigger layer, which is what an interaction is
            // allowed to reach: asking for that layer rather than "everything" keeps the per-frame query to
            // the handful of volumes that can answer E, and useTriggers is set explicitly rather than left
            // to the project-wide query default, which the echo fan's settings also read.
            interactFilter = new ContactFilter2D { useTriggers = true };
            interactFilter.SetLayerMask(LayerMask.GetMask("Trigger"));
        }

        void Update()
        {
            if (!IsAlive)
            {
                nearby = null;
                return;
            }

            if (director == null)
            {
                return;
            }

            IsQuiet = Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;
            UpdateAim();
            director.SetPose(transform.position, aimRad);
            UpdateEchoControls();
            UpdateInteraction();
            UpdateFootsteps();
        }

        void UpdateAim()
        {
            Pointer pointer = Pointer.current;
            Camera camera = Camera.main;
            if (pointer == null || camera == null)
            {
                return;
            }

            Vector2 screen = pointer.position.ReadValue();
            Vector2 world = camera.ScreenToWorldPoint(screen);
            Vector2 dir = world - (Vector2)transform.position;

            // Recorded before the guard below, because "the pointer is on top of me" is the answer the
            // visual needs, and it is exactly the case that has no usable angle.
            aimDistance = dir.magnitude;

            if (dir.sqrMagnitude < 1e-6f)
            {
                return;
            }

            float smoothing = Mathf.Clamp01(18f * Time.unscaledDeltaTime);
            aimRad = hasAim
                ? PlayerFacing.SmoothTowards(aimRad, Mathf.Atan2(dir.y, dir.x), smoothing)
                : Mathf.Atan2(dir.y, dir.x);
            hasAim = true;
        }

        void UpdateEchoControls()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            // The edges are read here, not in FixedUpdate: wasPressedThisFrame is per-frame state, so a
            // physics loop that ran zero times drops the press and one that ran twice pings twice.
            if (keyboard.fKey.wasPressedThisFrame)
            {
                director.TryWeakPulse();
            }

            if (keyboard.spaceKey.wasPressedThisFrame)
            {
                // Starting a charge while the last ping is still being paid would let the hold finish
                // without the cooldown that loudness is supposed to cost.
                if (director.CanPing)
                {
                    chargeStart = Time.time;
                }
            }
            else if (keyboard.spaceKey.wasReleasedThisFrame && chargeStart >= 0f)
            {
                float loudness = ArcMath.ManualLoudness(Time.time - chargeStart, director.Profile);
                chargeStart = -1f;

                // A shout that was charged while the microphone made a ping in the middle is dropped
                // rather than queued: the cooldown is the cost, and it is paid by whoever made the sound.
                if (director.CanPing)
                {
                    director.ForcePing(loudness);
                }
            }
        }

        /// <summary>
        /// E on the thing the body is standing next to. The probe is a circle query around the body rather
        /// than a trigger callback: the same reason the noise zones use containment — no collision-matrix
        /// entry and no execution order can make a door that is right there unreachable, and the HUD needs
        /// "what is near me" every frame anyway, which a trigger enter/exit pair cannot answer.
        /// </summary>
        void UpdateInteraction()
        {
            EchoTuningProfile profile = DirectorProfile;
            Keyboard keyboard = Keyboard.current;
            if (profile == null || keyboard == null)
            {
                return;
            }

            nearby = FindNearby(profile.interactionRange);

            if (keyboard.eKey.wasPressedThisFrame && nearby != null && nearby.CanInteract(this))
            {
                nearby.Interact(this);
            }
        }

        IInteractable FindNearby(float range)
        {
            int count = Physics2D.OverlapCircle(Position, range, interactFilter, interactHits);
            IInteractable best = null;
            float bestSqr = float.PositiveInfinity;

            for (int i = 0; i < count; i++)
            {
                Collider2D hit = interactHits[i];
                IInteractable candidate = hit.GetComponent<IInteractable>();
                if (candidate == null)
                {
                    continue;
                }

                float sqr = (hit.ClosestPoint(Position) - Position).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = candidate;
                }
            }

            return best;
        }

        void UpdateFootsteps()
        {
            EchoTuningProfile profile = DirectorProfile;
            if (profile == null || IsQuiet || lastInput.sqrMagnitude < 0.01f || Time.time < nextStepTime)
            {
                return;
            }

            nextStepTime = Time.time + profile.footstepInterval;
            director.ReportNoise(Position, profile.footstepLoudness);
        }

        void FixedUpdate()
        {
            if (!IsAlive)
            {
                body.linearVelocity = Vector2.zero;
                return;
            }

            EchoTuningProfile profile = DirectorProfile;
            if (profile == null)
            {
                return;
            }

            Vector2 input = Vector2.zero;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
                {
                    input.y += 1f;
                }

                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
                {
                    input.y -= 1f;
                }

                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
                {
                    input.x -= 1f;
                }

                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
                {
                    input.x += 1f;
                }
            }

            lastInput = input;
            float speed = IsQuiet ? profile.silentSpeed : profile.walkSpeed;
            body.linearVelocity = input.normalized * speed;
        }

        void OnCollisionEnter2D(Collision2D collision)
        {
            if (!IsAlive || collision.contactCount == 0)
            {
                return;
            }

            ContactPoint2D contact = collision.GetContact(0);
            if (marks != null)
            {
                // The body centre travels with the mark: Flash needs it to tell which side of the surface
                // the character is on, because the dash has to be pushed away from the body rather than
                // sitting on the hitbox surface, which the drawn figure already covers.
                marks.Flash(contact.point, contact.normal, body.position);
            }

            RememberContacts(collision);

            // Careless walking has a cost, but a body resting on a wall while listening does not: the
            // bump is only a noise if there was real speed behind it.
            if (director != null && collision.relativeVelocity.sqrMagnitude > 0.25f)
            {
                director.ReportNoise(contact.point, director.Profile.bumpLoudness);
            }
        }

        /// <summary>Sliding along a wall is a series of stays, not enters: this is what lets the memory follow a hand.</summary>
        void OnCollisionStay2D(Collision2D collision)
        {
            if (IsAlive)
            {
                RememberContacts(collision);
            }
        }

        void RememberContacts(Collision2D collision)
        {
            if (marks == null)
            {
                return;
            }

            for (int i = 0; i < collision.contactCount; i++)
            {
                ContactPoint2D contact = collision.GetContact(i);
                marks.Remember(collision.collider, contact.point, contact.normal);
            }
        }

        /// <summary>
        /// One hit kills (GDD): no health bar and no knockback. The black screen and the retry belong to
        /// RunController, so this only stops the body and says that it happened.
        /// </summary>
        public void Die()
        {
            if (!IsAlive)
            {
                return;
            }

            IsAlive = false;
            chargeStart = -1f;
            body.linearVelocity = Vector2.zero;
            Died?.Invoke();
        }

        /// <summary>
        /// Moves the body and its physics position together, so a placement is true on the same frame and
        /// not after the next step. The level builder puts the player on the spawn cell and the checkpoint
        /// restart puts them back in the sanctuary; either way the containment queries read
        /// <see cref="Position"/> immediately, and a transform that has not been picked up by the body yet
        /// would put the player one step behind where they are standing.
        /// </summary>
        public void PlaceAt(Vector2 position)
        {
            transform.position = position;
            if (body != null)
            {
                body.position = position;
                body.linearVelocity = Vector2.zero;
            }
        }

        public void Revive(Vector2 roomStart)
        {
            PlaceAt(roomStart);
            nextStepTime = 0f;
            IsAlive = true;
        }
    }
}
