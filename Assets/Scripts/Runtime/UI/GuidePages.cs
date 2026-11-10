namespace EchoMaze.UI
{
    /// <summary>
    /// One page of the field guide: a heading and the body under it, with the two layout limits the
    /// generated card can actually hold.
    /// </summary>
    public sealed class GuidePage
    {
        public GuidePage(string title, string body)
        {
            Title = title;
            Body = body;
        }

        public string Title { get; }
        public string Body { get; }

        public int LineCount => Body.Split('\n').Length;

        public int LongestLine
        {
            get
            {
                int longest = 0;
                foreach (string line in Body.Split('\n'))
                {
                    if (line.Length > longest)
                    {
                        longest = line.Length;
                    }
                }

                return longest;
            }
        }
    }

    /// <summary>
    /// The words of the field guide, in one place and with no Unity in them, the way
    /// <see cref="EchoMaze.Gameplay.HintTexts"/> does the chapter-one hints: a reword is a change to this
    /// file and not a hunt through the generator.
    ///
    /// What is written here is what the code actually does today, plus the creatures the GDD names that are
    /// on their way — each of those marked as not being in the run yet. A guide that advertised Listen mode
    /// or the marble as if they were playable would be the menu's own version of the promise problem
    /// <c>Docs/STATUS.md</c> keeps a list of.
    ///
    /// The style is the HUD's: lower-case, no full stops where a line can end, ASCII only, because the
    /// interface font is a 95-glyph monospace and a box where a dash should be is not a typo the player
    /// can be expected to read through.
    /// </summary>
    public static class GuidePages
    {
        /// <summary>
        /// The body label is generated 1040 px wide and Share Tech Mono advances 0.6 em, so at the 22 pt the
        /// page is built at a line longer than this is clipped by the frame rather than wrapped by the
        /// label. <see cref="GuidePage.LongestLine"/> is asserted against it.
        /// </summary>
        public const int MaxLineChars = 78;

        /// <summary>
        /// Lines the body label has room for at the generated size and line spacing, with the prev/next row
        /// still clear. The longest card is exactly this, so a reword that adds a line fails the suite instead
        /// of quietly running under the buttons.
        /// </summary>
        public const int MaxLines = 16;

        static readonly GuidePage[] all =
        {
            new GuidePage("reading the dark",
                "the screen is black and you give off no light. the room is only ever\n" +
                "shown to you by sound: a noise leaves you, travels out, hits something\n" +
                "and comes back as a train of arcs that close on you.\n" +
                "\n" +
                "the shape of an arc is the shape of what it hit.\n" +
                "\n" +
                "  wall       a long flat line, broken into pieces\n" +
                "  pillar     a soft round curve\n" +
                "  corner     one bright point\n" +
                "  creature   a warm arc that beats like a heart\n" +
                "\n" +
                "you never see the whole of a thing, only the side that faces you. and\n" +
                "every sound you make is also heard by whatever shares the room."),

            new GuidePage("the waves you can make",
                "loudness is a dial, not a switch. a louder call reaches farther and\n" +
                "reads finer, costs a longer cooldown, and carries farther to anything\n" +
                "that is listening.\n" +
                "\n" +
                "  your voice   a shout paints a cone in the direction you aim: the\n" +
                "               farthest wave there is, and the one the microphone\n" +
                "               answers.\n" +
                "  space        the manual echo. hold to charge, release to send it.\n" +
                "               not a downgrade of your voice but the same wave at\n" +
                "               a set loudness, and it needs no microphone at all.\n" +
                "  f            the weak pulse. a full circle close around you, quiet,\n" +
                "               paying its own short cooldown instead of the loud one:\n" +
                "               the way to look behind you without telling the room."),

            new GuidePage("moving, and what it costs",
                "  wasd / arrows  move. every step is a sound.\n" +
                "  shift          the silent step, and a slower one. the only\n" +
                "                 cheap move here, and it works on glass too.\n" +
                "  mouse          aim. the echo cone follows it.\n" +
                "  e              open a door, or take the way out beside it.\n" +
                "  esc            pause.\n" +
                "\n" +
                "sounds you never chose to make:\n" +
                "  your steps     a small noise at your feet.\n" +
                "  a wall         touching it lights it for six seconds, makes a\n" +
                "                 smaller noise, then the dark takes the line back.\n" +
                "  yourself       faint warm dots of where you have walked, gone\n" +
                "                 again after about four seconds.\n" +
                "\n" +
                "listen mode and the decoy marble are the next milestone: not in\n" +
                "this build, and not on any menu."),

            new GuidePage("what lives in the dark",
                "you have no attack and one touch ends the run. nothing here is\n" +
                "fought; all of it is escaped. an enemy is introduced in three steps:\n" +
                "you hear it first, then you see its arc, then you learn what it is.\n" +
                "\n" +
                "  crawler   two round bumps with a gap between them. slower than\n" +
                "            you, hears about medium, cannot open a door.\n" +
                "  spike     a saw edge, and its arc is cut like one. faster than\n" +
                "            you, weak hearing, and it breaks doors.\n" +
                "  absorber  nothing comes back at all: a hole of black in the\n" +
                "            middle of the ring. slowest, hears furthest.\n" +
                "\n" +
                "a roar is a warm ring rather than a wave: where the creature is,\n" +
                "and nothing about the walls.\n" +
                "\n" +
                "the maze you start in has nothing in it. the crawler is chapter two,\n" +
                "and the rooms after it keep going down. all of it is in the build."),

            new GuidePage("the rest of the room",
                "  door         two line fragments with a slit between them.\n" +
                "               closed, it is a wall: to the wave, to your body,\n" +
                "               and from the next milestone, to hearing.\n" +
                "  the way out  it is not drawn. a gap in the far wall, then e\n" +
                "               beside it.\n" +
                "  sanctuary    the room that saves the run. walked into, it moves\n" +
                "               the checkpoint, and dying sends you back to it.\n" +
                "  hollow       a pocket in the wall. a quiet sound made inside\n" +
                "               barely carries; a shout is heard at full reach and\n" +
                "               breaks it. cover before you are found, not after.\n" +
                "  glass        a floor that answers a step. shift is still\n" +
                "               silent on it.\n" +
                "  death        black, the words YOU WERE HEARD, and the last\n" +
                "               sanctuary again inside two seconds with the room\n" +
                "               rebuilt."),

            new GuidePage("your voice",
                "the menu page \"tune your voice\" listens to your room for a\n" +
                "moment, puts the gate above your own noise floor and prints\n" +
                "the answer.\n" +
                "\n" +
                "  input      one button steps auto, then every device the\n" +
                "             machine has. a name starting \"Monitor of\" is your\n" +
                "             own speakers: the game skips it, or it would\n" +
                "             answer its own sound effects.\n" +
                "  no device  the game falls back and keeps playing on space. a\n" +
                "             missing microphone has never been a reason you\n" +
                "             could not play, and the menu line names which of\n" +
                "             the two it has.\n" +
                "\n" +
                "headphones and a microphone. shouting is a trade that goes both\n" +
                "ways: what you hear, the room hears too."),

            // Appended, not inserted: the diagram is drawn per page index, and putting this card before
            // "your voice" would hand the microphone diagram to the wrong card.
            new GuidePage("the descent",
                "the menu's chapters page is the whole story as one rail. each node is\n" +
                "a room; the room you have earned is open, the ones behind you carry\n" +
                "their best walk, and the ones ahead stay sealed until you have walked\n" +
                "the one before them.\n" +
                "\n" +
                "  choose     click a node, or up and down. the page reads it out on\n" +
                "             the right before you commit to it.\n" +
                "  start      the big button, or enter. the first line on the menu\n" +
                "             says continue once there is something to continue.\n" +
                "  records    waves, retries and time are kept per room, and only a\n" +
                "             faster walk replaces the number.\n" +
                "\n" +
                "ten rooms. the first has nothing in it that reacts to you. the last\n" +
                "of them is lit by something that is not a lantern."),

            new GuidePage("the lantern",
                "you carry two of them, three in the rooms that mean it. no light\n" +
                "comes from anywhere else: what you are given is not a torch but\n" +
                "a few lamps, and where they go is your decision.\n" +
                "\n" +
                "  q            set one down at your feet. it stays there: you cannot\n" +
                "               pick it up again, and it burns for the rest of the\n" +
                "               room.\n" +
                "  light        it draws every wall within about five metres, and\n" +
                "               keeps drawing them while it stands there.\n" +
                "  what it costs nothing. light makes no noise, and nothing in\n" +
                "               the dark can hear it: it is the one thing you can\n" +
                "               read for free.\n" +
                "  what it is not a map. the walls past its reach stay dark, and\n" +
                "               the light ends where the room does.\n" +
                "\n" +
                "so a lamp spends a room's dark on the part of it you chose to keep."),
        };

        public static GuidePage[] All => all;

        public static int Count => all.Length;

        public static GuidePage At(int index) => all[Clamp(index)];

        /// <summary>The page after the last is the first, and the page before the first is the last: the
        /// guide is a loop of cards, and a next button that stops working is read as a broken build.</summary>
        public static int Next(int index) => (Clamp(index) + 1) % all.Length;

        public static int Previous(int index) =>
            (Clamp(index) + all.Length - 1) % all.Length;

        /// <summary>Where a requested page index actually lands, for a terminal argument nobody checked.</summary>
        public static int Clamp(int index) =>
            index < 0 ? 0 : (index > all.Length - 1 ? all.Length - 1 : index);

        public static string Counter(int index) => (Clamp(index) + 1) + " / " + all.Length;
    }
}
