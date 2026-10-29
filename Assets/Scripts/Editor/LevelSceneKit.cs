using UnityEditor;
using UnityEngine;
using EchoMaze.Core;
using EchoMaze.Gameplay;
using EchoMaze.UI;

namespace EchoMaze.EditorTools
{
    /// <summary>
    /// The parts every playable chapter needs and no chapter should each invent: an event system, a run
    /// controller, a session, a camera that follows, a HUD, a pause screen and a victory screen.
    ///
    /// Shared because the two levels are the same game with different geometry. A per-scene copy of the HUD
    /// is the reason a project ends up with three cooldown bars that disagree about what they measure.
    /// </summary>
    public static class LevelSceneKit
    {
        /// <summary>
        /// Builds the chrome around an already-placed player and returns the session the level's own zones
        /// hang off. Called by each scene setup after its geometry, because the camera clamp is the level's
        /// extent and only the level knows it.
        /// </summary>
        public static LevelSession BuildChrome(EchoDirector director, Player player,
                                               Vector2 cameraBoundsHalf, float cameraSize, string chapterTitle)
        {
            UiKit.EnsureEventSystem();
            GameObject notice = SceneKit.EnsureDeathNotice();
            RunController runner = SceneKit.EnsureRunController(player, notice);
            LevelSession session = SceneKit.EnsureLevelSession(player, runner, director, chapterTitle);
            SceneKit.SetReference(runner, "session", session);

            PlayerCameraRig rig = SceneKit.EnsureCameraRig(player, cameraBoundsHalf, cameraSize);
            UiKit.EnsureHud(director, player, session);
            UiKit.EnsurePausePanel(session);
            UiKit.EnsureVictoryPanel();
            return session;
        }
    }
}
