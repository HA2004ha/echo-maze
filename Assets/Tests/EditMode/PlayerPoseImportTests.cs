using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace EchoMaze.Tests
{
    /// <summary>
    /// The player art imports at the size the balance was tuned at.
    ///
    /// A sprite's world footprint is set by its *source* resolution divided by its PPU, and nothing in the
    /// project says so out loud: <c>maxTextureSize</c> rescales the uploaded texture without resizing the
    /// sprite, so an importer can look right in the inspector and be four times too big in the room. When
    /// the art team re-delivers at a different canvas size that becomes a silent change to how much of the
    /// screen the character covers — which is a readability and a collision question, not an art question.
    /// So the footprint is asserted here, against the asset as it is actually imported.
    /// </summary>
    public class PlayerPoseImportTests
    {
        // 1.6 world units of canvas, which is what 4096 source pixels at PPU 2560 gives, and what the 0.25 m
        // hitbox and the 0.447 m silhouette reach in Docs/ISSUES.md are all measured against.
        const float CanvasWorldSize = 1.6f;

        // All five files the scene builder wires: three cardinals and the two 3/4 stills that carry the four
        // diagonals (Docs/ISSUES.md #2). The three leftward poses are mirrors, so eight poses ship as five
        // assets, and every one of them is load-bearing.
        static readonly string[] Poses =
        {
            "Assets/Art/Sprites/Player/Player_front.png",
            "Assets/Art/Sprites/Player/Player_back.png",
            "Assets/Art/Sprites/Player/Player_side.png",
            "Assets/Art/Sprites/Player/Player_34_front.png",
            "Assets/Art/Sprites/Player/Player_34_back.png"
        };

        [TestCaseSource(nameof(WiredPoses))]
        public void AWiredPose_LandsAtTheFootprintTheRoomWasTunedFor(string path)
        {
            var pose = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            Assert.AreNotEqual(null, pose,
                path + " is not imported as a Single sprite, so the scene that references it is " +
                "referencing a texture");

            Assert.AreEqual(CanvasWorldSize, pose.bounds.size.x, 0.02f,
                            path + " draws " + pose.bounds.size.x + " units wide on a " + pose.rect.width +
                            " pixel canvas at PPU " + pose.pixelsPerUnit + ": the character no longer has " +
                            "the silhouette the hitbox and the touch-mark clearance were measured against");
            Assert.AreEqual(CanvasWorldSize, pose.bounds.size.y, 0.02f, path + " is not square");
        }

        static string[] WiredPoses() => Poses;

        [Test]
        public void PlaceholderDot_StillFitsTheSameFootprint()
        {
            var dot = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Sprites/Player_dot.png");
            Assert.AreNotEqual(null, dot, "the fallback sprite the bootstrap draws when the art is missing");

            // 16 source pixels at PPU 16: one unit, the size the character had before any art landed.
            Assert.AreEqual(1f, dot.bounds.size.x, 0.02f);
            Assert.AreEqual(1f, dot.bounds.size.y, 0.02f);
        }
    }
}
