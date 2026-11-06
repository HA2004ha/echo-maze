using System.Collections;
using UnityEngine;

namespace EchoMaze.UI
{
    /// <summary>
    /// Keeps the menu's 3D diorama alive while nobody is playing: the maze model turns at a
    /// walk-around pace and the camera breathes a few centimetres, so the backdrop is a model on a
    /// table someone is slowly circling rather than a picture. Unscaled time, because a paused game
    /// still shows a menu and a menu that froze reads as a crash, not as a pause.
    ///
    /// The turn lives on the maze pivot and the bob on this camera: rotating the camera instead would
    /// slide the light-in-the-frame and the wave colours around the screen while the UI sits still,
    /// which makes the buttons hard to read.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MenuMazeDrift : MonoBehaviour
    {
        [Tooltip("The maze model itself, spun about its own vertical axis.")]
        [SerializeField] Transform mazePivot;
        [SerializeField] float degreesPerSecond = 1.4f;
        [Tooltip("Half-height of the camera's vertical breath, in world units. Small, because the " +
                 "frame already moves from the rotation and two motions add up to nausea.")]
        [SerializeField] float bobAmplitude = 0.18f;
        [SerializeField] float bobPeriodSeconds = 9f;

        void OnEnable()
        {
            StartCoroutine(Drift());
        }

        IEnumerator Drift()
        {
            Vector3 home = transform.localPosition;
            float elapsed = 0f;

            while (true)
            {
                float dt = Time.unscaledDeltaTime;
                elapsed += dt;

                if (mazePivot != null)
                {
                    mazePivot.Rotate(0f, degreesPerSecond * dt, 0f, Space.Self);
                }

                float phase = elapsed / Mathf.Max(0.01f, bobPeriodSeconds) * 2f * Mathf.PI;
                Vector3 bob = home;
                bob.y += Mathf.Sin(phase) * bobAmplitude;
                transform.localPosition = bob;

                yield return null;
            }
        }
    }
}
