using UnityEngine;

namespace EchoMaze.Gameplay
{
    /// <summary>
    /// The camera follows the body. That is the whole class, and it is the difference between a maze you can
    /// get lost in and a screenshot of a wave.
    ///
    /// Clamped to the level rectangle rather than free, because the world outside a maze grid is empty and
    /// an unclamped rig at the corner of a 21x15 map shows the player a third of black nothing on the side
    /// they are pressed against — which reads as "the level ends here" when it does not.
    ///
    /// LateUpdate, like <c>AimCursor</c>: the body moves in <c>FixedUpdate</c> and the aim is settled in
    /// <c>Player.Update</c>, so a camera that ran in either of those would be one step behind on the frames
    /// where they disagree.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerCameraRig : MonoBehaviour
    {
        [SerializeField] Player player;
        [Tooltip("Fraction of the remaining gap closed per second of real time, so a pause does not stall " +
                 "the follow. 0 snaps, which is what the blockout scene does by having no rig at all.")]
        [Range(0f, 20f)] [SerializeField] float followPerSecond = 7f;
        [Tooltip("Half-extents of the rectangle the camera centre may not leave, in world units, measured " +
                 "from the level centre. Set by the level builder from the grid; negative means unclamped.")]
        [SerializeField] Vector2 boundsHalf = new Vector2(-1f, -1f);

        Camera cam;

        /// <summary>Where the rig wants to be, exposed so a test can assert the follow without a frame.</summary>
        public Vector2 Target => player != null ? player.Position : (Vector2)transform.position;

        /// <summary>
        /// Set by the level from the grid it just read. Half-extents rather than min/max because the grid is
        /// centred on the origin by construction, and a negative axis means that axis is not clamped at all.
        /// </summary>
        public void SetBounds(Vector2 half) => boundsHalf = half;

        void Awake()
        {
            cam = GetComponent<Camera>();
            if (cam == null)
            {
                enabled = false;
            }
        }

        void LateUpdate()
        {
            if (player == null)
            {
                return;
            }

            Vector3 to = Target;
            if (boundsHalf.x >= 0f)
            {
                Vector2 half = ScreenHalf();
                to.x = Clamp(to.x, -boundsHalf.x + half.x, boundsHalf.x - half.x);
                to.y = Clamp(to.y, -boundsHalf.y + half.y, boundsHalf.y - half.y);
            }

            float t = Mathf.Clamp01(followPerSecond * Time.unscaledDeltaTime);
            Vector3 from = transform.position;
            transform.position = new Vector3(Mathf.Lerp(from.x, to.x, t),
                                             Mathf.Lerp(from.y, to.y, t),
                                             from.z);
        }

        /// <summary>
        /// The visible half-size at the sprite plane, not at the screen plane: <c>pixelHeight</c> over the
        /// orthographic size is the world-per-pixel the whole project measures in, and a camera with a
        /// non-16:9 window would otherwise let the clamp cut off the side of a room the player can see.
        /// </summary>
        Vector2 ScreenHalf()
        {
            float height = cam.orthographicSize;
            float aspect = cam.aspect;
            return new Vector2(height * aspect, height);
        }

        static float Clamp(float value, float min, float max) =>
            max < min ? (min + max) * 0.5f : Mathf.Clamp(value, min, max);
    }
}
