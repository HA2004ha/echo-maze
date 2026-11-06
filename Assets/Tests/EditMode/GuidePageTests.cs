using EchoMaze.UI;
using NUnit.Framework;

namespace EchoMaze.Tests
{
    /// <summary>
    /// The field guide is copy, and copy is the part of a UI that rots quietly: a page can be reworded until
    /// it no longer fits the card, and a build ships it clipped by the frame with every test still green.
    /// Both are arithmetic on a string, so both are asserted here rather than looked for in a screenshot.
    /// </summary>
    public class GuidePageTests
    {
        [Test]
        public void EveryPage_HasATitleAndABody()
        {
            Assert.IsNotEmpty(GuidePages.All, "the guide has no pages at all");

            for (int i = 0; i < GuidePages.Count; i++)
            {
                GuidePage page = GuidePages.All[i];
                Assert.IsNotEmpty(page.Title, "page " + i + " has no title");
                Assert.IsNotEmpty(page.Body, "page " + i + " has no body");
            }
        }

        [Test]
        public void EveryPage_FitsTheGeneratedCard()
        {
            for (int i = 0; i < GuidePages.Count; i++)
            {
                GuidePage page = GuidePages.All[i];
                Assert.LessOrEqual(page.LongestLine, GuidePages.MaxLineChars,
                                  "page " + i + " (" + page.Title + ") has a " + page.LongestLine +
                                  " character line; the text column is " + GuidePages.MaxLineChars +
                                  " wide and the label does not wrap, so it would be cut by the frame");
                Assert.LessOrEqual(page.LineCount, GuidePages.MaxLines,
                                  "page " + i + " (" + page.Title + ") is " + page.LineCount +
                                  " lines tall and the body label holds " + GuidePages.MaxLines);
            }
        }

        [Test]
        public void TheGuide_NamesTheThingsAPlayerMeets()
        {
            // Not a style test: the reason this page exists is that nothing else in the game says what a
            // wave, a creature or a hiding place is. A rename in the code that drops one of these words
            // from the guide is the failure this pins.
            string everything = Body();

            Assert.That(everything, Does.Contain("manual echo"));
            Assert.That(everything, Does.Contain("weak pulse"));
            Assert.That(everything, Does.Contain("shift"));
            Assert.That(everything, Does.Contain("crawler"));
            Assert.That(everything, Does.Contain("spike"));
            Assert.That(everything, Does.Contain("absorber"));
            Assert.That(everything, Does.Contain("sanctuary"));
            Assert.That(everything, Does.Contain("hollow"));
            Assert.That(everything, Does.Contain("door"));
            Assert.That(everything, Does.Contain("microphone"));
        }

        [Test]
        public void ThePager_WrapsAtBothEnds()
        {
            Assert.AreEqual(1, GuidePages.Next(0));
            Assert.AreEqual(0, GuidePages.Next(GuidePages.Count - 1), "the last page has no way forward");
            Assert.AreEqual(GuidePages.Count - 1, GuidePages.Previous(0), "the first page has no way back");
            Assert.AreEqual(GuidePages.Count - 2, GuidePages.Previous(GuidePages.Count - 1));
        }

        [Test]
        public void AnOutsideIndex_ClampsIntoTheGuide()
        {
            Assert.AreEqual(0, GuidePages.Clamp(-7));
            Assert.AreEqual(GuidePages.Count - 1, GuidePages.Clamp(GuidePages.Count + 3));
            Assert.AreEqual("1 / " + GuidePages.Count, GuidePages.Counter(-1));
            Assert.IsInstanceOf<GuidePage>(GuidePages.At(9999));
        }

        static string Body()
        {
            var text = new System.Text.StringBuilder();
            foreach (GuidePage page in GuidePages.All)
            {
                text.Append(page.Title).Append('\n').Append(page.Body).Append('\n');
            }

            return text.ToString().ToLowerInvariant();
        }
    }
}
