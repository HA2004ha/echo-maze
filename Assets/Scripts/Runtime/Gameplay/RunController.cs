using UnityEngine;

namespace EchoMaze.Gameplay
{
    /// <summary>
    /// The lose-and-retry loop: one hit kills, the screen goes black with the GDD's "YOU WERE HEARD", and the
    /// run is back under two seconds.
    ///
    /// With a <see cref="LevelSession"/> wired the restart goes to the last sanctuary and the section's
    /// authored state is rebuilt; without one — week 1's blockout and every PlayMode rig built before the
    /// levels existed — it goes back to the room start captured at Awake. Both are the same rule with a
    /// different restart point, which is why one class owns the timing.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RunController : MonoBehaviour
    {
        [SerializeField] Player player;
        [SerializeField] LevelSession session;
        [SerializeField] GameObject deathNotice;
        [Tooltip("Seconds the black screen stays up. The GDD asks for an instant retry under two seconds; " +
                 "longer and the death becomes a punishment instead of a lesson.")]
        [SerializeField, Range(0.2f, 2f)] float retryDelay = 1f;

        Vector2 roomStart;
        float retryAt = -1f;

        public bool IsDead => retryAt >= 0f;

        void Awake()
        {
            roomStart = player.transform.position;
            player.Died += OnPlayerDeath;

            if (deathNotice != null)
            {
                deathNotice.SetActive(false);
            }
        }

        /// <summary>The black screen, from the session as well as from here: the panel outlives the retry.</summary>
        public void ShowDeathNotice(bool visible)
        {
            if (deathNotice != null)
            {
                deathNotice.SetActive(visible);
            }
        }

        void OnPlayerDeath()
        {
            if (deathNotice != null)
            {
                deathNotice.SetActive(true);
            }

            // Unscaled, so the retry really is under two seconds even if a future change freezes time on
            // the death screen (which is the plan for the chase music sting in week 4).
            retryAt = Time.unscaledTime + retryDelay;
        }

        void Update()
        {
            if (retryAt < 0f || Time.unscaledTime < retryAt)
            {
                return;
            }

            retryAt = -1f;
            if (session != null)
            {
                // The session owns the checkpoint and the counters, and hides the notice itself: the death
                // and its retry are one transaction, not two halves of it split across classes.
                session.RestartFromCheckpoint();
                return;
            }

            if (deathNotice != null)
            {
                deathNotice.SetActive(false);
            }

            player.Revive(roomStart);

            // Looked up here rather than cached at Awake: week 3's section reset destroys and respawns
            // enemies, and a cached array would either miss the new ones or reach into destroyed ones.
            foreach (Crawler crawler in FindObjectsByType<Crawler>(FindObjectsInactive.Exclude))
            {
                crawler.ResetRun();
            }
        }
    }
}
