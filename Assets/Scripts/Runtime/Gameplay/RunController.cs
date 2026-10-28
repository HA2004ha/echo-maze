using UnityEngine;

namespace EchoMaze.Gameplay
{
    /// <summary>
    /// The lose-and-retry loop of the blockout: one hit kills, the screen goes black with the GDD's
    /// "YOU WERE HEARD", and the run is back at the room start in about a second.
    ///
    /// Week 3 moves the restart point from the room start to the last sanctuary and adds the section state
    /// reset that comes with it; that is why the spawn is captured at Awake instead of being a scene asset.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RunController : MonoBehaviour
    {
        [SerializeField] Player player;
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
