using EchoMaze.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EchoMaze.Gameplay
{
    /// <summary>
    /// Temporary player: enough movement and aiming to judge whether the wave is readable.
    /// Deliberately reads Keyboard.current / Pointer.current directly instead of going through
    /// GameInput, because the .inputactions rewrite is a week-3 task. See Docs/DESIGN_DECISIONS.md.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class PlayerMock : MonoBehaviour
    {
        [SerializeField] EchoDirector director;
        [SerializeField] float speed = 3.2f;
        [SerializeField] float quietMultiplier = 0.42f;
        [SerializeField] float aimSmoothing = 18f;

        Rigidbody2D body;
        float aimRad;
        bool hasAim;

        public bool IsQuiet { get; private set; }
        public float AimRadians => aimRad;

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            if (director == null)
            {
                director = FindAnyObjectByType<EchoDirector>();
            }
        }

        void Update()
        {
            if (director == null)
            {
                return;
            }

            IsQuiet = Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;
            UpdateAim();
            director.SetPose(transform.position, aimRad);

            // The edge is read here, not in FixedUpdate: wasPressedThisFrame is per-frame state, so a
            // physics loop that ran zero times drops the press and one that ran twice pings twice.
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
            {
                director.ForcePing(director.ManualEchoLoudness);
            }
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
            if (dir.sqrMagnitude < 1e-6f)
            {
                return;
            }

            float target = Mathf.Atan2(dir.y, dir.x);
            aimRad = hasAim
                ? Mathf.LerpAngle(aimRad, target, Mathf.Clamp01(aimSmoothing * Time.unscaledDeltaTime))
                : target;
            hasAim = true;
        }

        void FixedUpdate()
        {
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

            float multiplier = IsQuiet ? quietMultiplier : 1f;
            body.linearVelocity = input.normalized * (speed * multiplier);
        }
    }
}
