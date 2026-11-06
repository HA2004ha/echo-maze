using System.Collections;
using EchoMaze.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace EchoMaze.Tests.PlayMode
{
    /// <summary>
    /// Docs/ISSUES.md #9: dying before any sanctuary used to respawn the player where they died, because
    /// <see cref="LevelSession.Checkpoint"/> fell back to the player's *current* position. Both restart paths
    /// are held to the same rule here: no checkpoint taken means the spawn.
    /// </summary>
    public class RespawnTests : BlockoutRig
    {
        static readonly Vector2 Spawn = new Vector2(2f, -3.15f);
        static readonly Vector2 DeathPlace = new Vector2(-3f, 2f);

        [UnityTest]
        public IEnumerator WithASession_DyingBeforeAnySanctuary_RespawnsAtTheSpawn()
        {
            player.PlaceAt(Spawn);

            // Created after the player stands on the spawn, which is the order MazeLevel (-20) gives the
            // real scene: the session's Awake captures the spawn from where the player is then.
            GameObject host = New("rig run + session", false);
            var runner = host.AddComponent<RunController>();
            var session = host.AddComponent<LevelSession>();
            Wire(runner, "player", player);
            Wire(runner, "session", session);
            Wire(runner, "retryDelay", 0.2f);
            Wire(session, "player", player);
            Wire(session, "runner", runner);
            Wire(session, "director", director);
            host.SetActive(true);

            yield return DieElsewhereAndWait();

            Assert.Less(Vector2.Distance(player.Position, Spawn), 0.05f,
                        "the retry put the player at " + player.Position + " instead of the spawn " + Spawn);
        }

        [UnityTest]
        public IEnumerator WithoutASession_DyingBeforeAnySanctuary_RespawnsAtTheSpawn()
        {
            player.PlaceAt(Spawn);

            GameObject host = New("rig run controller", false);
            var runner = host.AddComponent<RunController>();
            Wire(runner, "player", player);
            Wire(runner, "retryDelay", 0.2f);
            host.SetActive(true);

            yield return DieElsewhereAndWait();

            Assert.Less(Vector2.Distance(player.Position, Spawn), 0.05f,
                        "the retry put the player at " + player.Position + " instead of the spawn " + Spawn);
        }

        IEnumerator DieElsewhereAndWait()
        {
            player.PlaceAt(DeathPlace);
            player.Die();

            // The retry runs on the unscaled clock and the runner's frames are far shorter than a real
            // frame, so the wait polls the real clock (see RunControllerTests).
            float start = Time.unscaledTime;
            while (Time.unscaledTime - start < 3f && !player.IsAlive)
            {
                yield return null;
            }

            Assert.IsTrue(player.IsAlive, "the retry never came");
        }
    }
}
