namespace EchoMaze.Audio
{
    /// <summary>
    /// Every moment in Echo Maze that makes a sound, named once so gameplay never carries a string, a path
    /// or a clip reference around. The palette is part of the game's rule that every sound is a trade: a
    /// sound that does not tell the player something, or tell the dark where they are, is decoration.
    ///
    /// One entry per event rather than one per clip: the last item of a list like this is always the one
    /// somebody forgets, so <c>SoundSynthTests</c> walks the whole enum and fails if an entry has no sound.
    /// </summary>
    public enum SoundId
    {
        // The interface: the game's own hand, never a thing in the dark.
        UiHover,
        UiClick,
        UiBack,

        // The body and the floor it is on.
        Footstep,
        GlassStep,
        Hint,

        // The two senses the game is built on: the wave coming back, and the things it finds.
        PingEcho,
        DoorOpen,
        DoorClose,
        LampPlace,
        Sanctuary,

        // What is hunting you.
        Alert,
        GiveUp,
        Heartbeat,

        // Music: the menu, a room, and the end of a room.
        MenuTheme,
        ChapterBed,
        VictorySting,
    }

    /// <summary>
    /// Facts about the catalogue that more than one class needs, kept next to the enum so a new sound is
    /// added in one place instead of in a switch in every consumer.
    /// </summary>
    public static class SoundCatalog
    {
        /// <summary>
        /// Clips meant to be played on a looping <c>AudioSource</c>. They are rendered to join themselves
        /// (see <c>SoundSynth.CloseSeam</c>), so a loop point is a bar line rather than a click.
        /// </summary>
        public static bool IsLoop(SoundId id) =>
            id == SoundId.Heartbeat || id == SoundId.MenuTheme || id == SoundId.ChapterBed;

        /// <summary>Clips that are music rather than an event: they get the music bus and the long fades.</summary>
        public static bool IsMusic(SoundId id) =>
            id == SoundId.MenuTheme || id == SoundId.ChapterBed || id == SoundId.VictorySting;

        /// <summary>
        /// Where the sound designer's clip for this sound goes. The name is the enum's own name, so the
        /// rule is one sentence: drop <c>Footstep.wav</c> in <c>Assets/Audio</c> and the footsteps are yours.
        /// </summary>
        public static string AssetPath(SoundId id) => "Assets/Audio/" + id + ".wav";

        /// <summary>Every value of the enum, allocated once, in declaration order.</summary>
        public static readonly SoundId[] All = (SoundId[])System.Enum.GetValues(typeof(SoundId));
    }
}
