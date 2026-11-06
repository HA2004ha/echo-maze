using EchoMaze.Data;
using EchoMaze.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace EchoMaze.Tests
{
    /// <summary>The hollow rule as a table: cover helps against a quiet sound and an enemy that is not yet chasing.</summary>
    public class HollowScaleTests
    {
        EchoTuningProfile profile;

        [SetUp]
        public void SetUp() => profile = ScriptableObject.CreateInstance<EchoTuningProfile>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(profile);

        [Test]
        public void QuietSoundInCover_AgainstAnIdleEnemy_IsAttenuated()
        {
            Assert.AreEqual(profile.hollowHearingMultiplier,
                            Hollow.HearingScale(true, profile.footstepLoudness, false, profile));
        }

        [Test]
        public void OutsideCover_NothingIsAttenuated()
        {
            Assert.AreEqual(1f, Hollow.HearingScale(false, profile.footstepLoudness, false, profile));
        }

        [Test]
        public void ALoudSoundInCover_BreaksIt()
        {
            Assert.AreEqual(1f, Hollow.HearingScale(true, profile.manualEchoLoudness, false, profile));
        }

        [Test]
        public void AChasingEnemy_IgnoresCover()
        {
            Assert.AreEqual(1f, Hollow.HearingScale(true, profile.footstepLoudness, true, profile));
        }

        [Test]
        public void TheShippedLoudnesses_FallOnTheRightSideOfTheThreshold()
        {
            Assert.Less(profile.footstepLoudness, profile.hollowBreaksAbove, "footsteps would break cover");
            Assert.Less(profile.bumpLoudness, profile.hollowBreaksAbove);
            Assert.Greater(profile.manualEchoLoudness, profile.hollowBreaksAbove, "a ping would not break cover");
        }
    }
}
