using EchoMaze.Core;
using EchoMaze.Data;
using UnityEngine;

namespace EchoMaze.Gameplay
{
    /// <summary>
    /// The sound that finishes the roar: a shorter, lower snarl of the same family, played flat (2D) the
    /// instant the player dies so it is at full volume wherever the body fell.
    ///
    /// It listens to <see cref="Player.Died"/> instead of being called from Player: <c>Die()</c> already
    /// refuses a second call, so one death is one event, and a <c>Revive</c> raises nothing. Lives on the
    /// player's object, next to the <c>AudioListener</c>, so the player is never out of earshot of it.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    [DisallowMultipleComponent]
    public sealed class DeathSnarl : MonoBehaviour
    {
        [SerializeField] EchoDirector director;
        [Tooltip("Death_snarl.wav. Empty means the death is silent; the black screen still happens.")]
        [SerializeField] AudioClip clip;

        AudioSource voice;
        Player player;

        /// <summary>Deaths announced so far. Counted even without a clip, because batch mode has no audio device.</summary>
        public int PlayCount { get; private set; }

        void Awake()
        {
            voice = GetComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.loop = false;
            voice.spatialBlend = 0f;
            player = GetComponent<Player>();
        }

        void OnEnable()
        {
            if (player != null)
            {
                player.Died += OnDied;
            }
        }

        void OnDisable()
        {
            if (player != null)
            {
                player.Died -= OnDied;
            }
        }

        void OnDied()
        {
            PlayCount++;
            if (clip == null)
            {
                return;
            }

            EchoTuningProfile profile = director != null ? director.Profile : null;
            voice.PlayOneShot(clip, profile != null ? profile.deathSoundVolume : 1f);
        }
    }
}
