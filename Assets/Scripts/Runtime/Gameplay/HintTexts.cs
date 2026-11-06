namespace EchoMaze.Gameplay
{
    /// <summary>
    /// The words of the chapter-one hints, in one place so the team can reword them without reading any
    /// logic. English only, lower-case, no full stops: the HUD is thin and dim, and a hint is a whisper.
    /// </summary>
    public static class HintTexts
    {
        public static string For(Hint hint)
        {
            switch (hint)
            {
                case Hint.Start:
                    return "SPACE: hold, aim with the mouse, release to send a wave in that direction";
                case Hint.AfterWave:
                    return "F: a small circle all around you — quiet, short range";
                case Hint.FirstTouch:
                    return "walls you touch stay marked in your memory";
                case Hint.WalkNoise:
                    return "SHIFT: walk silently — walking makes noise";
                case Hint.Door:
                    return "E: open or close a door";
                default:
                    return "";
            }
        }
    }
}
