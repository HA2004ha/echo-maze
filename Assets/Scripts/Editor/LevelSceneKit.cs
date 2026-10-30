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
            EnsureDeathSnarl(player, director);
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

        /// <summary>
        /// The death sound on the player's own object. Missing clip is a warning, not an error: the death
        /// is still counted and the black screen still comes.
        /// </summary>
        public static DeathSnarl EnsureDeathSnarl(Player player, EchoDirector director)
        {
            var snarl = player.GetComponent<DeathSnarl>() ?? player.gameObject.AddComponent<DeathSnarl>();
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Death_snarl.wav");
            if (clip == null)
            {
                Debug.LogWarning("Death clip Assets/Audio/Death_snarl.wav is not imported: dying will be silent.",
                                 player);
            }

            SceneKit.SetReference(snarl, "director", director);
            SceneKit.SetReference(snarl, "clip", clip);
            return snarl;
        }

        /// <summary>A hollow over <paramref name="size"/> of floor at <paramref name="centre"/>; one object per name.</summary>
        public static Hollow EnsureHollow(string name, Vector2 centre, Vector2 size, Player player,
                                          LevelSession session)
        {
            GameObject go = GameObject.Find(name) ?? new GameObject(name);
            var hollow = go.GetComponent<Hollow>() ?? go.AddComponent<Hollow>();
            SceneKit.SetReference(hollow, "player", player);
            SceneKit.SetReference(hollow, "session", session);
            SceneKit.SetVector2(hollow, "centre", centre);
            SceneKit.SetVector2(hollow, "size", size);
            return hollow;
        }

        /// <summary>The chapter-one hints, switched on. Chapters that want none simply do not call this.</summary>
        public static LevelHints EnsureHints(EchoDirector director, Player player, TouchMarks marks,
                                             LevelSession session)
        {
            GameObject go = GameObject.Find("Level Hints") ?? new GameObject("Level Hints");
            var hints = go.GetComponent<LevelHints>() ?? go.AddComponent<LevelHints>();
            SceneKit.SetReference(hints, "director", director);
            SceneKit.SetReference(hints, "player", player);
            SceneKit.SetReference(hints, "marks", marks);
            SceneKit.SetReference(hints, "session", session);
            var serialized = new SerializedObject(hints);
            serialized.FindProperty("hintsEnabled").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return hints;
        }
    }
}
