namespace EchoMaze.Gameplay
{
    /// <summary>
    /// The words of the chapter-one hints, in one place so the team can reword them without reading any
    /// logic. English only, lower-case, no full stops: the HUD is thin and dim, and a hint is a whisper.
    ///
    /// Every line is written to be read in one glance over a lit room while the player is still walking:
    /// the key first, then the consequence. The earlier wording shouted the key in capitals and spent a
    /// whole line explaining the mouse, which is the one control nobody needs telling twice.
    /// </summary>
    public static class HintTexts
    {
        public static string For(Hint hint)
        {
            switch (hint)
            {
                case Hint.Start:
                    return "space: hold to charge, aim, release";
                case Hint.AfterWave:
                    return "f: a quiet circle all around you";
                case Hint.FirstTouch:
                    return "walls you touch stay in your memory";
                case Hint.WalkNoise:
                    return "shift walks silently — steps make noise";
                case Hint.Door:
                    return "e: open or close a door";
                case Hint.Lantern:
                    return "q: put a lamp down — it stays and draws the walls";
                default:
                    return "";
            }
        }
    }
}
