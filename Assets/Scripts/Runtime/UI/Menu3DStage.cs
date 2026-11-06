using UnityEngine;

namespace EchoMaze.UI
{
    /// <summary>
    /// Puts the menu on its own stage: when the interface comes up, the 3D diorama camera opens and
    /// the flat gameplay camera is switched off. This is not a stack — the URP camera here clears to
    /// solid black, so a gameplay camera that stayed enabled would simply paint the diorama out again
    /// depending on render order, and a background that appears and disappears with draw order is a
    /// bug nobody reading a menu should ever have to notice.
    ///
    /// Nothing restores the gameplay camera, because nothing needs to: this component lives in the
    /// Boot scene and levels load single — when the run begins, this scene's cameras stop existing and
    /// the level arrives with its own.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Menu3DStage : MonoBehaviour
    {
        [Tooltip("The Menu Camera 3D object, saved inactive so the scene file keeps one enabled camera " +
                 "for every test that counts them.")]
        [SerializeField] GameObject menuStage;
        [SerializeField] Camera gameplayCamera;

        void Awake()
        {
            if (menuStage != null)
            {
                menuStage.SetActive(true);
            }

            if (gameplayCamera != null)
            {
                gameplayCamera.enabled = false;
            }
        }
    }
}
