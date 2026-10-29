using EchoMaze.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EchoMaze.UI
{
    /// <summary>
    /// The pointer, drawn by the game instead of by the operating system: a ring with a dot on the exact
    /// aim point, and a head on the ring that turns to the angle the character and the echo cone are using.
    ///
    /// Three things, in three places, used to disagree. The OS arrow said where the hand was, the face said
    /// one of eight directions, and the cone said a ninth until the aim finished easing. The head draws
    /// <c>Player.AimRadians</c> — the eased angle <c>director.SetPose</c> hands to the cone — so the reticle
    /// and the shot are always the same statement, and the ~0.1 s the ease costs is visible as the head
    /// catching up rather than hidden as a mismatch. Nothing here re-eases and nothing here snaps to a
    /// sector: a second ease would be a lag nobody asked for, and a sector-snapped head would sit up to
    /// 22.5 degrees off the cone and reintroduce the class of defect <c>Docs/ISSUES.md</c> #6 just closed.
    ///
    /// The position is the raw pointer and is not clamped to the screen. A reticle that stopped short of an
    /// edge would lie about the one coordinate that is definitely correct, and the aim genuinely uses it.
    ///
    /// Optional by design, like <c>PlayerVisual</c>: the PlayMode rigs never build a cursor, <c>Player</c>
    /// never looks at this, and an incomplete setup disables itself without a word rather than logging an
    /// error during activation, which would fail every test in the suite. Un-ticking it in the inspector is
    /// the escape hatch — <c>OnDisable</c> hands the system cursor back.
    ///
    /// Not sealed: the one line that cannot be tested without hardware is the one line a test needs to
    /// override, and a subclass is the only way to hand it a screen point without pretending the runner may
    /// move a mouse.
    /// </summary>
    [DisallowMultipleComponent]
    public class AimCursor : MonoBehaviour
    {
        [Header("Wired by the scene builder")]
        [Tooltip("The player whose aim this reticle draws. Not found by search: the builder promises the " +
                 "reference, and a game with no player in the scene has no business drawing an aim.")]
        [SerializeField] Player player;
        [Tooltip("The ring. 48 texels at PPU 90, so one texel is one screen pixel at the 1080p target.")]
        [SerializeField] Sprite ringSprite;
        [Tooltip("The centre dot: the exact aim point, and the only element whose job is precision.")]
        [SerializeField] Sprite dotSprite;
        [Tooltip("The direction head, authored pointing along +x so the rotation onto the aim angle is the " +
                 "identity and not an offset somebody has to remember.")]
        [SerializeField] Sprite headSprite;

        [Header("Colour")]
        [Tooltip("#7fe9f0 — the interface cyan every other element in the game already uses, so there is one " +
                 "contrast number to tune in TUNING.md rather than one per sprite. The alpha is the design: " +
                 "this cyan lands at 0.827 times alpha over black, and AimCursorMath.AlphaCeiling is the " +
                 "brightest any element may be without crossing Bloom and turning the interface into a light " +
                 "source.")]
        [SerializeField] Color cursorColor = new Color(0.498f, 0.914f, 0.941f, 1f);
        [Tooltip("The ring is the frame, not the point, so it stays the dimmest thing on screen.")]
        [Range(0f, 1f)] [SerializeField] float ringAlpha = 0.34f;
        [Tooltip("The dot marks where the aim is measured from and is allowed to be as bright as a touch " +
                 "mark, which is the game's existing unit for \"this is real but it is not a wave\".")]
        [Range(0f, 1f)] [SerializeField] float dotAlpha = 0.5f;

        [Header("Head states")]
        [Tooltip("Steady brightness while the aim is being believed.")]
        [Range(0f, 1f)] [SerializeField] float headAlpha = 0.4f;
        [Tooltip("The direction head, slack. Below the park radius the pointer is on the character's own body, " +
                 "the angle is meaningless and PlayerVisual holds the pose instead of resolving it " +
                 "(Docs/ISSUES.md #3's third gate) — a rule that is otherwise invisible, and parking the " +
                 "cursor on yourself is the normal resting state rather than an edge case. The ring and the " +
                 "dot do not dim: the point is still exactly where you put it.")]
        [Range(0f, 1f)] [SerializeField] float parkedAlpha = 0.16f;
        [Tooltip("A full manual charge. Brighter means louder is this game's own language, and the hold is " +
                 "the only sound the player sizes by hand, so the head walks up to here while Space is down " +
                 "and peaks on the frame ManualLoudness does. Still under the ceiling: the wave has to " +
                 "outshine its own aiming tool.")]
        [Range(0f, 1f)] [SerializeField] float chargedAlpha = 0.62f;
        [Tooltip("Pointer-to-body separation, in world units, at which the head goes slack. One number with " +
                 "PlayerVisual.aimNoiseRadius, asserted equal rather than shared, and 0.27 m is the same " +
                 "24 pixels at the 1080p target.")]
        [Range(0f, 1f)] [SerializeField] float parkRadius = AimCursorMath.ParkedRadiusDefault;

        Transform head;
        SpriteRenderer ringRenderer;
        SpriteRenderer dotRenderer;
        SpriteRenderer headRenderer;
        bool hasPointer;

        void Awake()
        {
            Transform ring = transform.Find("Ring");
            Transform dot = transform.Find("Dot");
            head = transform.Find("Head");
            ringRenderer = ring != null ? ring.GetComponent<SpriteRenderer>() : null;
            dotRenderer = dot != null ? dot.GetComponent<SpriteRenderer>() : null;
            headRenderer = head != null ? head.GetComponent<SpriteRenderer>() : null;

            if (player == null || ringRenderer == null || dotRenderer == null || headRenderer == null ||
                ringSprite == null || dotSprite == null || headSprite == null)
            {
                // Silent, because the scene builder is the thing that promises this exists and it has
                // already warned. An error here would also fail every PlayMode test that activates a rig.
                enabled = false;
                return;
            }

            ringRenderer.sprite = ringSprite;
            dotRenderer.sprite = dotSprite;
            headRenderer.sprite = headSprite;

            // HUD is the layer the project provisioned and never used: Default (the body) < Echo (the wave)
            // < HUD (the interface). The three children share one layer and one z, so their relative order
            // is only defined by sortingOrder.
            ringRenderer.sortingLayerName = "HUD";
            dotRenderer.sortingLayerName = "HUD";
            headRenderer.sortingLayerName = "HUD";
            ringRenderer.sortingOrder = 0;
            dotRenderer.sortingOrder = 1;
            headRenderer.sortingOrder = 2;
        }

        void OnEnable()
        {
            Cursor.visible = false;
        }

        void OnDisable()
        {
            // The restore is the point of putting it here rather than in Start: leaving play mode disables
            // components, and an unrepaired Cursor.visible strands the editor without a pointer.
            Cursor.visible = true;
        }

        void LateUpdate()
        {
            Vector2 screen;
            Camera camera;
            if (!TryReadPointer(out screen, out camera))
            {
                // Never had a device to read: give up, hand the system cursor back, and let the machine that
                // has no mouse keep the pointer it does have.
                if (!hasPointer)
                {
                    enabled = false;
                }

                return;
            }

            hasPointer = true;

            Vector3 world = camera.ScreenToWorldPoint(screen);

            // The camera sits at z -10, so the plain conversion comes back at the screen plane, not at the
            // sprite plane. Player only ever reads x and y out of the same call; a cursor that assigned the
            // whole vector would inherit -10, against the explicit z the touch marks already force
            // (TouchMarks.Flash).
            transform.position = new Vector3(world.x, world.y, 0f);

            head.localRotation = Quaternion.Euler(0f, 0f, player.AimRadians * Mathf.Rad2Deg);

            float progress = AimCursorMath.ChargeProgress(player.ChargeSeconds, player.ManualChargeSeconds);
            bool parked = AimCursorMath.IsParked(player.AimDistance, parkRadius);

            ringRenderer.color = Tint(ringAlpha);
            dotRenderer.color = Tint(dotAlpha);
            headRenderer.color = Tint(AimCursorMath.HeadAlpha(progress, parked,
                                                              headAlpha, parkedAlpha, chargedAlpha));
        }

        /// <summary>
        /// The cyan, with the brightness carried entirely by alpha, and the ceiling enforced rather than only
        /// tuned: somebody nudging a slider to 1.0 in the inspector should make the reticle dimmer than the
        /// wave, not brighter, and that should be true without them having to know why.
        /// </summary>
        Color Tint(float alpha)
        {
            Color color = cursorColor;
            color.a = Mathf.Min(alpha, AimCursorMath.AlphaCeiling);
            return color;
        }

        /// <summary>
        /// The only line in this file that cannot be tested: the hardware. <c>Player</c> reads
        /// <c>Pointer.current</c> directly for the same reason the <c>.inputactions</c> rewrite is still
        /// ahead of it (Docs/DESIGN_DECISIONS.md), and the aim cursor inherits both the read and its null
        /// behaviour. Virtual rather than inline so a PlayMode test can hand it a screen point without
        /// pretending the runner is allowed to move a mouse — the policy <c>PlayerVisualTests</c> states
        /// outright: simulated keys are less trustworthy than setting what the component would have read.
        /// </summary>
        protected virtual bool TryReadPointer(out Vector2 screen, out Camera camera)
        {
            Pointer pointer = Pointer.current;
            camera = Camera.main;

            if (pointer == null || camera == null)
            {
                screen = Vector2.zero;
                return false;
            }

            screen = pointer.position.ReadValue();
            return true;
        }
    }
}
