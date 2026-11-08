using NUnit.Framework;
using EchoMaze.Gameplay;

namespace EchoMaze.Tests
{
    /// <summary>
    /// The chapter page's two provable halves: the prose a chapter carries, and the record of which chapters
    /// have been walked.
    ///
    /// The page itself is a canvas and needs a person to judge; what it reads is pure and is asserted here.
    /// The one failure these tests exist for is the silent one: a chapter added to the run with no words of
    /// its own would render as an empty row that still looks intentional.
    /// </summary>
    public class ChapterPageTests
    {
        /// <summary>A scene name no build contains, so the progress tests never touch a real save.</summary>
        const string Unwritten = "Test_Chapter_Not_In_The_Run";

        [TearDown]
        public void ForgetTheTestChapter()
        {
            ChapterProgress.Forget(Unwritten);
        }

        [Test]
        public void EveryChapterInTheCatalogue_HasWordsOfItsOwn()
        {
            Assert.Greater(ChapterCatalog.Count, 0, "the story has no chapters");

            for (int i = 0; i < ChapterCatalog.Count; i++)
            {
                ChapterStory chapter = ChapterCatalog.At(i);
                Assert.IsFalse(string.IsNullOrEmpty(chapter.Scene), "chapter " + i + " is not attached to a scene");
                Assert.IsFalse(string.IsNullOrEmpty(chapter.Numeral), chapter.Scene + " has no numeral");
                Assert.IsFalse(string.IsNullOrEmpty(chapter.Title), chapter.Scene + " has no title");
                Assert.IsFalse(string.IsNullOrEmpty(chapter.Subtitle), chapter.Scene + " has no subtitle");
                Assert.IsFalse(string.IsNullOrEmpty(chapter.Opening), chapter.Scene + " has no opening line");
                Assert.IsFalse(string.IsNullOrEmpty(chapter.Thread), chapter.Scene + " does not lead anywhere");
                Assert.IsFalse(string.IsNullOrEmpty(chapter.Debrief), chapter.Scene + " has nothing to leave behind");
            }
        }

        [Test]
        public void TheCatalogue_StartsAtTheMazeAndKeepsItsRoomsInOrder()
        {
            // The flow's own order is asserted against this catalogue in BootSceneWiringTests, where the
            // boot scene is open; here the catalogue alone has to be coherent.
            Assert.AreEqual(ChapterCatalog.MazeScene, ChapterCatalog.At(0).Scene);
            Assert.AreEqual("I", ChapterCatalog.At(0).Numeral);

            var seen = new System.Collections.Generic.HashSet<string>();
            for (int i = 0; i < ChapterCatalog.Count; i++)
            {
                Assert.IsTrue(seen.Add(ChapterCatalog.At(i).Scene),
                              ChapterCatalog.At(i).Scene + " appears twice in the story");
            }
        }

        [Test]
        public void ASceneTheCatalogueKnows_OpensWithItsOwnLine()
        {
            string opening = ChapterCatalog.OpeningForScene(ChapterCatalog.MazeScene);
            Assert.AreEqual(ChapterCatalog.At(0).Opening, opening,
                            "the level's opening caption is not the catalogue's line");
            Assert.IsEmpty(ChapterCatalog.OpeningForScene("Some_Scene_With_No_Story"),
                           "a room with no story must say nothing rather than something generic");
        }

        [Test]
        public void ASceneTheCatalogueDoesNotKnow_StillGetsAReadableNode()
        {
            ChapterStory chapter = ChapterCatalog.At(9, "Level09_Unknown");
            Assert.AreEqual("Level09_Unknown", chapter.Scene);
            Assert.IsFalse(string.IsNullOrEmpty(chapter.Title),
                           "a chapter added to the flow before the catalogue must not draw a blank row");
        }

        [Test]
        public void AChapter_IsNotWalkedUntilItIsMarked()
        {
            Assert.IsFalse(ChapterProgress.IsComplete(Unwritten));
            Assert.IsFalse(ChapterProgress.Read(Unwritten).Complete);

            ChapterProgress.MarkComplete(Unwritten, 12, 1, 95f);

            Assert.IsTrue(ChapterProgress.IsComplete(Unwritten));
            ChapterProgress.Record record = ChapterProgress.Read(Unwritten);
            Assert.IsTrue(record.Complete);
            Assert.IsTrue(record.HasBest);
            Assert.AreEqual(95f, record.Seconds, 0.001f);
            Assert.AreEqual(12, record.Waves);
            Assert.AreEqual(1, record.Retries);
        }

        [Test]
        public void MarkingAChapter_KeepsTheFastestRunRatherThanTheLatest()
        {
            ChapterProgress.MarkComplete(Unwritten, 12, 1, 95f);
            Assert.IsFalse(ChapterProgress.MarkComplete(Unwritten, 3, 0, 130f),
                           "a slower run was reported as a record");

            ChapterProgress.Record slower = ChapterProgress.Read(Unwritten);
            Assert.AreEqual(95f, slower.Seconds, 0.001f, "the later, slower run overwrote the record");
            Assert.AreEqual(12, slower.Waves);

            Assert.IsTrue(ChapterProgress.MarkComplete(Unwritten, 8, 0, 61f),
                          "a faster run was not reported as a record");
            ChapterProgress.Record faster = ChapterProgress.Read(Unwritten);
            Assert.AreEqual(61f, faster.Seconds, 0.001f);
            Assert.AreEqual(8, faster.Waves);
        }

        [Test]
        public void ForgettingAChapter_TakesItsRecordWithIt()
        {
            ChapterProgress.MarkComplete(Unwritten, 12, 1, 95f);
            ChapterProgress.Forget(Unwritten);

            Assert.IsFalse(ChapterProgress.IsComplete(Unwritten),
                           "a forgotten chapter still counts as walked, so it would never be replayable");
            ChapterProgress.Record record = ChapterProgress.Read(Unwritten);
            Assert.IsFalse(record.HasBest);
            Assert.AreEqual(0, record.Waves);
        }
    }
}
