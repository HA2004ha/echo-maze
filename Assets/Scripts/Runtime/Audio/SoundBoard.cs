using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EchoMaze.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EchoMaze.Audio
{
    /// <summary>
    /// The one object that plays everything: the interface's ticks, the room's events, the music, and the
    /// heart in the player's ears when something is hunting them.
    ///
    /// One board per game, surviving scene loads, because music belongs to the game rather than to a room
    /// and a new scene must not restart the theme. The scene builder puts one in every scene it generates
    /// anyway — a chapter opened directly from the editor has to have sound too — so a duplicate finds the
    /// incumbent in <c>Awake</c> and removes itself, the same rule <see cref="GameFlow"/> uses.
    ///
    /// A sound is played by name (<see cref="SoundId"/>), never by handing around a clip. That is what lets
    /// this class own the fallback: a <c>wav</c> the sound designer dropped in
    /// <c>Assets/Audio/&lt;name&gt;.wav</c> is wired by the scene builder and wins, and anything they have not
    /// replaced yet is synthesised on first use. Nothing is ever silent while the palette is being collected.
    ///
    /// Batch mode has no audio device, so nothing is generated or played there — but the calls still count,
    /// which is what lets a PlayMode test assert that a door opening asked for the door sound.
    ///
    /// The board also owns the **ears**: a scene needs exactly one <see cref="AudioListener"/> and every
    /// level puts it on the player, so that a door has a distance. The menu has no player, and a scene with
    /// no listener in it is silent — music included. The board carries one and switches it off the moment a
    /// scene brings its own.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SoundBoard : MonoBehaviour
    {
        /// <summary>One designer clip replacing one synthesised sound. Built by the scene builder from Assets/Audio.</summary>
        [Serializable]
        public struct ClipSlot
        {
            public SoundId id;
            public AudioClip clip;
        }

        /// <summary>Which piece of music belongs to the scene that is open.</summary>
        public enum Cue
        {
            Silence,
            Menu,
            Chapter,
        }

        public static SoundBoard Instance { get; private set; }

        [Tooltip("A clip here replaces the synthesised sound of the same name. The scene builder fills this " +
                 "from Assets/Audio/<SoundId>.wav, so a designer's file needs no code change at all.")]
        [SerializeField] ClipSlot[] clips = new ClipSlot[0];

        [SerializeField, Range(0f, 1f)] float sfxVolume = 0.9f;
        [SerializeField, Range(0f, 1f)] float musicVolume = 0.5f;
        [Tooltip("Loudest the chase heartbeat gets. Under the roars on purpose: it is the player's body, " +
                 "not an enemy bearing down.")]
        [SerializeField, Range(0f, 1f)] float heartbeatVolume = 0.45f;
        [SerializeField, Range(0.2f, 6f)] float musicFadeSeconds = 1.6f;

        const int SfxVoices = 8;
        const int WorldVoices = 4;

        readonly Dictionary<SoundId, AudioClip> clipsById = new Dictionary<SoundId, AudioClip>();

        // Music is a minute of samples — about half a second of arithmetic — so it is computed on a worker
        // thread and turned into an AudioClip on the main thread when it lands. A menu that freezes for
        // half a second on open is a worse first impression than a theme that starts a beat late.
        readonly Dictionary<SoundId, Task<float[]>> generating = new Dictionary<SoundId, Task<float[]>>();
        SoundId wantedMusic = SoundId.MenuTheme;

        AudioSource[] voices = new AudioSource[0];
        AudioSource[] worldVoices = new AudioSource[0];
        AudioSource music;
        AudioSource heartbeat;
        AudioListener ears;

        Crawler[] crawlers = new Crawler[0];
        int nextVoice;
        int nextWorld;
        float musicTarget;
        float threat;
        float threatTarget;
        float nextThreatPoll;
        Cue cue = Cue.Silence;

        /// <summary>The last sound asked for. Read by tests; it is also the cheapest debug answer there is.</summary>
        public SoundId LastPlayed { get; private set; }

        /// <summary>How many sounds have been asked for, whether or not there was a device to hear them.</summary>
        public int PlayedCount { get; private set; }

        public Cue CurrentCue => cue;

        /// <summary>How loud the chase is in the player's ears right now, 0..1.</summary>
        public float Threat => threat;

        /// <summary>False where there is no audio device: batch mode, and therefore the test suites.</summary>
        public bool AudioEnabled => !Application.isBatchMode && Application.isPlaying;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                // A later scene's board: the game already has one and music must not restart per room.
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            BuildVoices();
            EnsureEars();
            foreach (ClipSlot slot in clips)
            {
                if (slot.clip != null)
                {
                    clipsById[slot.id] = slot.clip;
                }
            }

            if (AudioEnabled)
            {
                heartbeat.clip = Clip(SoundId.Heartbeat);
            }

            ChooseCueForScene();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                SceneManager.sceneLoaded -= OnSceneLoaded;
            }
        }

        void Update()
        {
            CollectFinishedMusic();

            if (music != null)
            {
                float step = Time.unscaledDeltaTime / Mathf.Max(0.1f, musicFadeSeconds) * Mathf.Max(0.05f, musicVolume);
                music.volume = Mathf.MoveTowards(music.volume, musicTarget, step);
            }

            if (Time.unscaledTime >= nextThreatPoll)
            {
                // Four times a second is enough for a body: the heartbeat follows the hunt, not a frame.
                nextThreatPoll = Time.unscaledTime + 0.25f;
                threatTarget = AnyoneHunting() ? 1f : 0f;
            }

            threat = Mathf.MoveTowards(threat, threatTarget, Time.unscaledDeltaTime * 1.5f);

            if (heartbeat != null && AudioEnabled)
            {
                heartbeat.volume = threat * heartbeatVolume;
                if (!heartbeat.isPlaying && heartbeat.clip != null)
                {
                    heartbeat.Play();
                }
            }
        }

        // ---------------------------------------------------------------------------------------
        // Static entry points: a call site says what happened, not how to find the board.
        // ---------------------------------------------------------------------------------------

        /// <summary>A sound that belongs to the player's own head: interface, footsteps, the wave returning.</summary>
        public static void Play(SoundId id, float volume = 1f, float pitch = 1f)
        {
            if (Instance != null)
            {
                Instance.PlayOne(id, volume, pitch);
            }
        }

        /// <summary>A sound that has a place in the room, so it reaches the player from where it happened.</summary>
        public static void PlayAt(SoundId id, Vector2 at, float volume = 1f)
        {
            if (Instance != null)
            {
                Instance.PlayOneAt(id, at, volume);
            }
        }

        public void PlayOne(SoundId id, float volume = 1f, float pitch = 1f)
        {
            Count(id);
            if (!AudioEnabled || voices.Length == 0)
            {
                return;
            }

            AudioClip clip = Clip(id);
            if (clip == null)
            {
                return;
            }

            AudioSource voice = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Length;
            // A pooled source keeps its pitch, so it is written on every play rather than once.
            voice.pitch = Mathf.Clamp(pitch, 0.25f, 2.5f);
            voice.PlayOneShot(clip, Mathf.Clamp01(volume * sfxVolume));
        }

        public void PlayOneAt(SoundId id, Vector2 at, float volume = 1f)
        {
            Count(id);
            if (!AudioEnabled || worldVoices.Length == 0)
            {
                return;
            }

            AudioClip clip = Clip(id);
            if (clip == null)
            {
                return;
            }

            AudioSource voice = worldVoices[nextWorld];
            nextWorld = (nextWorld + 1) % worldVoices.Length;
            voice.transform.position = new Vector3(at.x, at.y, 0f);
            voice.pitch = 1f;
            voice.PlayOneShot(clip, Mathf.Clamp01(volume * sfxVolume));
        }

        /// <summary>
        /// The room is finished: the sting, and the bed taken down under it so the two are not fighting.
        /// The next scene's <see cref="SetCue"/> brings the music back.
        /// </summary>
        public void Victory()
        {
            AudioClip sting = Clip(SoundId.VictorySting);
            if (sting != null)
            {
                PlayOne(SoundId.VictorySting, 1f);
            }
            else
            {
                RequestMusic(SoundId.VictorySting);
            }

            musicTarget = AudioEnabled ? musicVolume * 0.3f : 0f;
        }

        /// <summary>
        /// The clip for a sound: the designer's file if the builder found one, else synthesised now. Null
        /// for music that is still being computed — <see cref="SetCue"/> plays it as soon as it lands.
        /// </summary>
        public AudioClip Clip(SoundId id)
        {
            if (clipsById.TryGetValue(id, out AudioClip cached))
            {
                return cached;
            }

            if (!AudioEnabled)
            {
                return null;
            }

            if (SoundCatalog.IsMusic(id) || generating.ContainsKey(id))
            {
                return null;
            }

            AudioClip built = SoundSynth.Build(id);
            clipsById[id] = built;
            return built;
        }

        /// <summary>Starts computing a piece of music in the background, once.</summary>
        void RequestMusic(SoundId id)
        {
            if (!AudioEnabled || clipsById.ContainsKey(id) || generating.ContainsKey(id))
            {
                return;
            }

            // The sample buffer is a pure function of the id: no Unity call, no device, nothing that has to
            // be on the main thread. Only AudioClip.Create and SetData have to be, and they happen below.
            generating[id] = Task.Run(() => SoundSynth.SampleBuffer(id));
        }

        /// <summary>Turns finished buffers into clips and starts the one the current scene is waiting for.</summary>
        void CollectFinishedMusic()
        {
            if (generating.Count == 0)
            {
                return;
            }

            List<SoundId> done = null;
            foreach (KeyValuePair<SoundId, Task<float[]>> pair in generating)
            {
                if (pair.Value.IsCompleted)
                {
                    if (done == null)
                    {
                        done = new List<SoundId>();
                    }

                    done.Add(pair.Key);
                }
            }

            if (done == null)
            {
                return;
            }

            foreach (SoundId id in done)
            {
                float[] samples = generating[id].Result;
                generating.Remove(id);

                AudioClip clip = AudioClip.Create(id.ToString(), samples.Length, 1, SoundSynth.SampleRate, false);
                clip.SetData(samples, 0);
                clipsById[id] = clip;

                if (id == wantedMusic && cue != Cue.Silence && music != null)
                {
                    StartMusic(clip);
                }
            }
        }

        void Count(SoundId id)
        {
            PlayedCount++;
            LastPlayed = id;
        }

        // ---------------------------------------------------------------------------------------
        // Music.
        // ---------------------------------------------------------------------------------------

        void ChooseCueForScene()
        {
            // Before anything is cued: a scene loaded without ears is a scene nothing can be heard in.
            EnsureEars();

            bool inLevel = FindAnyObjectByType<LevelSession>() != null;
            crawlers = FindObjectsByType<Crawler>(FindObjectsInactive.Exclude);

            // The victory sting is generated while the room is being walked so it is never late: the one
            // moment a sound cannot be a beat behind is the moment the chapter ends.
            if (inLevel)
            {
                RequestMusic(SoundId.VictorySting);
            }

            SetCue(inLevel ? Cue.Chapter : Cue.Menu);
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode) => ChooseCueForScene();

        /// <summary>
        /// Switches the music, or restores its level after a victory ducked it. The clip is looked up before
        /// the source is stopped, so a missing clip leaves the current theme playing rather than silence.
        /// </summary>
        public void SetCue(Cue next)
        {
            EnsureEars();

            if (next != cue)
            {
                cue = next;
                if (music != null)
                {
                    if (next == Cue.Silence)
                    {
                        music.Stop();
                        musicTarget = 0f;
                        return;
                    }

                    wantedMusic = next == Cue.Menu ? SoundId.MenuTheme : SoundId.ChapterBed;
                    AudioClip clip = Clip(wantedMusic);
                    if (clip != null)
                    {
                        StartMusic(clip);
                    }
                    else
                    {
                        RequestMusic(wantedMusic);
                    }
                }
            }

            musicTarget = cue == Cue.Silence || !AudioEnabled ? 0f : musicVolume;
        }

        /// <summary>The music source is only ever started with a clip that exists, so a missing theme is silence, not an error.</summary>
        void StartMusic(AudioClip clip)
        {
            if (music == null)
            {
                return;
            }

            music.clip = clip;
            music.volume = 0f;
            music.Play();
            musicTarget = AudioEnabled ? musicVolume : 0f;
        }

        bool AnyoneHunting()
        {
            for (int i = 0; i < crawlers.Length; i++)
            {
                Crawler crawler = crawlers[i];
                if (crawler != null && crawler.IsHunting)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Guarantees the scene has exactly one listener: the player's if there is one, the board's if there
        /// is not. This is the menu's case — Boot is a camera and a canvas and no player, and it shipped
        /// once with no listener at all, which is a menu with a theme nobody could hear and buttons that
        /// made no sound when pressed.
        /// </summary>
        void EnsureEars()
        {
            if (ears == null)
            {
                ears = GetComponent<AudioListener>();
                if (ears == null)
                {
                    ears = gameObject.AddComponent<AudioListener>();
                }
            }

            // Someone else's ears win: in a level the listener is on the player, at the body's position, so
            // that a sound has a direction. Two enabled listeners is a warning every frame and one of them
            // ignored; a switched-off one is simply not there.
            bool sceneHasEars = false;
            foreach (AudioListener other in FindObjectsByType<AudioListener>(FindObjectsInactive.Include))
            {
                if (other != ears)
                {
                    sceneHasEars = true;
                    break;
                }
            }

            ears.enabled = !sceneHasEars;
        }

        // ---------------------------------------------------------------------------------------
        // Sources.
        // ---------------------------------------------------------------------------------------

        void BuildVoices()
        {
            voices = new AudioSource[SfxVoices];
            for (int i = 0; i < voices.Length; i++)
            {
                voices[i] = NewVoice("Voice " + (i + 1), spatial: false);
            }

            worldVoices = new AudioSource[WorldVoices];
            for (int i = 0; i < worldVoices.Length; i++)
            {
                worldVoices[i] = NewVoice("World voice " + (i + 1), spatial: true);
            }

            music = NewVoice("Music", spatial: false);
            music.loop = true;
            music.volume = 0f;

            heartbeat = NewVoice("Heartbeat", spatial: false);
            heartbeat.loop = true;
            heartbeat.volume = 0f;
        }

        AudioSource NewVoice(string name, bool spatial)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);

            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = spatial ? 1f : 0f;

            if (spatial)
            {
                // The listener is on the player, not the camera (the camera sits at z = -10 and would hear
                // the whole room), so a world sound is heard from where the body is. Linear rolloff keeps
                // the distance legible, which is the same choice the Crawler's roar makes.
                source.rolloffMode = AudioRolloffMode.Linear;
                source.minDistance = 2f;
                source.maxDistance = 18f;
            }

            return source;
        }
    }
}
