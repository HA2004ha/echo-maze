using System.Collections;
using EchoMaze.Audio;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace EchoMaze.Tests.PlayMode
{
    /// <summary>
    /// Every level keeps the ears on the player, so a door has a distance; Boot has no player in it, and it
    /// shipped once with no <see cref="AudioListener"/> at all — a menu whose theme and button presses were
    /// inaudible while the levels were fine. The board carries the listener and gives it up to a scene that
    /// brings its own, and this is what holds that: exactly one listener, always switched on.
    /// </summary>
    public class SoundBoardEarsTests
    {
        [UnityTest]
        public IEnumerator TheBoard_HearsInASceneThatHasNoPlayer()
        {
            RemoveEveryListener();
            yield return null;

            SoundBoard board = new GameObject("Sound Board").AddComponent<SoundBoard>();
            yield return null;

            AudioListener[] ears = Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include);
            Assert.AreEqual(1, ears.Length, "the menu has no player, so the board has to be its ears");
            Assert.IsTrue(ears[0].enabled, "the only listener in the scene is switched off: nothing can be heard");
            Assert.AreSame(board.gameObject, ears[0].gameObject,
                           "the ears must not sit on a camera the menu stage switches off");

            Object.Destroy(board.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheBoard_GivesTheEarsToAPlayerThatHasItsOwn()
        {
            RemoveEveryListener();
            yield return null;

            SoundBoard board = new GameObject("Sound Board").AddComponent<SoundBoard>();
            var player = new GameObject("Player");
            player.AddComponent<AudioListener>();
            yield return null;

            // What a level does when it loads: the cue follows the scene, and the board checks who is listening.
            SoundBoard.Instance.SetCue(SoundBoard.Cue.Chapter);

            Assert.IsTrue(player.GetComponent<AudioListener>().enabled, "the player's listener is the one honoured in a level");
            Assert.IsFalse(board.GetComponent<AudioListener>().enabled,
                           "two enabled listeners is a warning every frame and one of them ignored");

            Object.Destroy(player);
            Object.Destroy(board.gameObject);
            yield return null;
        }

        static void RemoveEveryListener()
        {
            foreach (AudioListener stray in Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include))
            {
                Object.Destroy(stray);
            }
        }
    }
}
