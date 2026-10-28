using EchoMaze.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace EchoMaze.Tests
{
    /// <summary>
    /// The pose rule, tested as the pure function it is. This is the one piece of the player's art that a
    /// reader cannot judge by eye: a wrong sector reads as a character that "sometimes turns the wrong way"
    /// in a playtest and is impossible to describe to whoever has to fix it, so every boundary is written
    /// down here instead.
    /// </summary>
    public class PlayerFacingTests
    {
        static PlayerPose Aim(float radians)
        {
            return PlayerFacing.Resolve(Vector2.zero, radians);
        }

        static PlayerPose Move(float x, float y)
        {
            return PlayerFacing.Resolve(new Vector2(x, y), 0f);
        }

        [Test]
        public void TheFourCardinals_OfTheAimVector_GetTheFourPoses()
        {
            Assert.AreEqual(PlayerPose.ProfileRight, Aim(0f),
                "0 is screen right: the canonical profile, unflipped");
            Assert.AreEqual(PlayerPose.Back, Aim(Mathf.PI * 0.5f),
                "up the screen is walking away from the camera");
            Assert.AreEqual(PlayerPose.ProfileLeft, Aim(Mathf.PI),
                "screen left must be the mirrored profile, not a fifth sprite");
            Assert.AreEqual(PlayerPose.Front, Aim(-Mathf.PI * 0.5f),
                "down the screen is walking into the camera");
        }

        /// <summary>
        /// WASD diagonals are exactly |x| == |y|, so which side of the tie the rule takes is the pose a
        /// player holding two keys actually sees. Vertical wins: whether you are coming or going carries
        /// more intent than which hand was raised first.
        /// </summary>
        [Test]
        public void ADiagonal_ResolvesToTheVerticalPose()
        {
            Assert.AreEqual(PlayerPose.Back, Move(1f, 1f));
            Assert.AreEqual(PlayerPose.Back, Move(-1f, 1f));
            Assert.AreEqual(PlayerPose.Front, Move(1f, -1f));
            Assert.AreEqual(PlayerPose.Front, Move(-1f, -1f));
        }

        /// <summary>
        /// One degree on either side of every boundary, which is the table the sectors have to match. If a
        /// future edit moves a boundary by a hair, this fails and the hair has to be argued for.
        /// </summary>
        [Test]
        public void TheSectorEdges_SitAtFortyFiveAndOneThirtyFiveDegrees()
        {
            float d = Mathf.Deg2Rad;
            Assert.AreEqual(PlayerPose.ProfileRight, Aim(44f * d), "just below the back sector");
            Assert.AreEqual(PlayerPose.Back, Aim(45f * d), "the edge belongs to the vertical pose");
            Assert.AreEqual(PlayerPose.Back, Aim(135f * d), "and to the left edge of the same sector");
            Assert.AreEqual(PlayerPose.ProfileLeft, Aim(136f * d), "just past it");
            Assert.AreEqual(PlayerPose.Front, Aim(-45f * d), "the front sector keeps its own edges");
            Assert.AreEqual(PlayerPose.ProfileRight, Aim(-44f * d), "and 44 degrees below right is still right");
            Assert.AreEqual(PlayerPose.Front, Aim(-135f * d));
            Assert.AreEqual(PlayerPose.ProfileLeft, Aim(-136f * d));
        }

        /// <summary>
        /// Walks the whole circle and checks each angle against the sector it falls in, so the four
        /// assertions above cannot be a lucky sample of a rule that is actually wrong somewhere else.
        /// </summary>
        [Test]
        public void EveryDegree_MatchesTheSectorTable()
        {
            for (int deg = -180; deg < 180; deg++)
            {
                PlayerPose expected;
                if (deg >= -135 && deg < -45)
                {
                    expected = PlayerPose.Front;
                }
                else if (deg >= -45 && deg < 45)
                {
                    expected = PlayerPose.ProfileRight;
                }
                else if (deg >= 45 && deg < 135)
                {
                    expected = PlayerPose.Back;
                }
                else
                {
                    expected = PlayerPose.ProfileLeft;
                }

                // Half a degree inside each cell: the edges are pinned by their own test, and a sweep that
                // straddles them would only be testing which side of an epsilon it rounded to.
                Assert.AreEqual(expected, Aim((deg + 0.5f) * Mathf.Deg2Rad),
                    deg + " degrees off screen right");
            }
        }

        [Test]
        public void BothEndsOfTheAtan2Range_AreTheSameHeading()
        {
            Assert.AreEqual(Aim(Mathf.PI), Aim(-Mathf.PI),
                "the two ends of the range are one direction; the left profile must not depend on how " +
                "Mathf.Atan2 happened to sign it");
            Assert.AreEqual(PlayerPose.ProfileLeft, Aim(3f), "3 rad is 172 degrees, just short of due left");
            Assert.AreEqual(PlayerPose.ProfileLeft, Aim(-3f), "and so is -3 rad");
            Assert.AreEqual(PlayerPose.Front, Aim(-2f), "115 degrees below screen right is down-left");
        }

        [Test]
        public void AStillPlayer_FacesItsAim()
        {
            Assert.AreEqual(PlayerPose.Back, PlayerFacing.Resolve(Vector2.zero, Mathf.PI * 0.5f),
                "standing still and looking up the screen is the pose a listening player should show");
        }

        /// <summary>
        /// Below the deadzone the stick is not being pushed, it is being released. A sub-deadzone vector has
        /// to fall back to the aim rather than turn the character a quarter-rotation because a key came up
        /// one frame early.
        /// </summary>
        [Test]
        public void AMovementInsideTheDeadzone_KeepsTheAimPose()
        {
            // Horizontal on purpose: if the dribble of movement counted, the answer would be a profile and
            // the test would pass for the wrong reason on a vertical one.
            Vector2 under = new Vector2(0.09f, 0f);
            Assert.Less(under.sqrMagnitude, PlayerFacing.MoveDeadzoneSqr,
                "the test vector has to be inside the deadzone for this to mean anything");
            Assert.AreEqual(PlayerPose.ProfileRight, PlayerFacing.Resolve(Vector2.zero, 0f),
                "and the aim has to disagree with it");
            Assert.AreEqual(PlayerPose.Back, PlayerFacing.Resolve(under, Mathf.PI * 0.5f),
                "a movement too small to count must not override where a still player is looking");
        }

        /// <summary>
        /// The decision from the art review, written down so nobody "fixes" it later: the body turns to
        /// where it is going even when that is the way the mouse is not pointing. Movement is the stronger
        /// signal because it is the thing that makes noise.
        /// </summary>
        [Test]
        public void WalkingAwayFromItsOwnAim_FacesWhereItIsGoing()
        {
            Assert.AreEqual(PlayerPose.Front, PlayerFacing.Resolve(new Vector2(0f, -2.5f), Mathf.PI * 0.5f),
                "aiming up while walking down must show the front, or the character reads as sliding " +
                "backwards");
            Assert.AreEqual(PlayerPose.ProfileRight,
                PlayerFacing.Resolve(new Vector2(2.5f, 0f), Mathf.PI),
                "same rule on the horizontal axis");
        }

        /// <summary>
        /// The deadzone is the number Player.UpdateFootsteps compares lastInput against. Two copies of a
        /// constant is a smell, but a shared one would put a gameplay threshold in a rendering file; the
        /// equality is what stops them from drifting apart unnoticed.
        /// </summary>
        [Test]
        public void TheMoveDeadzone_IsTheFootstepThreshold()
        {
            Assert.AreEqual(0.01f, PlayerFacing.MoveDeadzoneSqr,
                "Player.UpdateFootsteps uses a literal 0.01 for the same 'is the player actually walking' " +
                "question; change one and you must change this too, deliberately");
        }

        /// <summary>
        /// The two helpers the sticky layer in <c>PlayerVisual</c> is built on. <c>ResolveDirection</c> is the
        /// sector map with the movement/aim choice taken out of it, so the visual can ask about a heading it
        /// has already picked; <c>PoseCentre</c> is the heading each pose is most correct for, which is what
        /// "cleared the boundary by the band" gets measured from. They are pinned against the map they come
        /// from rather than against literals, so moving a sector edge moves the hysteresis with it instead of
        /// quietly desynchronising the two.
        /// </summary>
        [Test]
        public void EveryPose_SitsFortyFiveDegreesEitherSideOfItsOwnCentre()
        {
            foreach (PlayerPose pose in AllPoses)
            {
                Vector2 centre = PlayerFacing.PoseCentre(pose);

                Assert.AreEqual(pose, PlayerFacing.ResolveDirection(centre),
                    pose + " must be the heading its own pose is most right for, or the band is measured " +
                    "from somewhere else");
                Assert.AreEqual(pose, PlayerFacing.ResolveDirection(centre * 3.7f),
                    "a heading is a direction: scaling it cannot change the answer");
                Assert.AreEqual(pose, PlayerFacing.ResolveDirection(Rotate(centre, 44f)),
                    pose + " still owns 44 degrees off its own centre");
                Assert.AreEqual(pose, PlayerFacing.ResolveDirection(Rotate(centre, -44f)));
                Assert.AreNotEqual(pose, PlayerFacing.ResolveDirection(Rotate(centre, 46f)),
                    pose + " has to hand over at 46, which is SectorHalfWidthDegrees plus a degree");
                Assert.AreNotEqual(pose, PlayerFacing.ResolveDirection(Rotate(centre, -46f)));
            }
        }

        static readonly PlayerPose[] AllPoses =
        {
            PlayerPose.Front, PlayerPose.Back, PlayerPose.ProfileRight, PlayerPose.ProfileLeft
        };

        static Vector2 Rotate(Vector2 vector, float degrees)
        {
            return Quaternion.Euler(0f, 0f, degrees) * vector;
        }

        /// <summary>
        /// The band is expressed against <c>PoseCentre</c>, so the width it is added to has to be the real
        /// half-width of the sectors and not a number that happens to work while there are four poses.
        /// </summary>
        [Test]
        public void TheSectorHalfWidth_IsWhatTheSectorsAreActuallyWorth()
        {
            Assert.AreEqual(360f / AllPoses.Length, PlayerFacing.SectorHalfWidthDegrees * 2f, 1e-4f,
                "four poses, four equal quarters. Issue 2 in Docs/ISSUES.md doubles the pose count, which " +
                "halves this to 22.5 and halves the hysteresis band that sits on top of it");
        }

        [Test]
        public void AStepThatJustFired_HasZeroPhase()
        {
            Assert.AreEqual(0f, PlayerFacing.StepPhase(5f, 5f + 0.55f, 0.55f), 1e-4f,
                "the bounce starts at the ground, not somewhere in the middle of a stride");
        }

        [Test]
        public void Phase_RunsUpToButNeverPastOne()
        {
            Assert.AreEqual(0.5f, PlayerFacing.StepPhase(5f, 5f + 0.275f, 0.55f), 1e-3f,
                "midway to the next footstep is the top of the hop");
            Assert.AreEqual(1f, PlayerFacing.StepPhase(5f, 5f + 0.0001f, 0.55f), 1e-2f,
                "the frame before a step lands is the dip");
            Assert.AreEqual(0f, PlayerFacing.StepPhase(5.55f, 5.55f, 0.55f),
                "the step has landed: the bounce is back on the ground rather than off the end of the range");
            Assert.AreEqual(0f, PlayerFacing.StepPhase(5f, 5f + 2f, 0.55f),
                "a timer further off than one stride (an interval just retuned smaller) must clamp to the " +
                "start of a step, not run past the top of the range");
        }

        /// <summary>
        /// Both ways a stride cannot be running: the timer is in the past (the player stopped, and the
        /// stale nextStepTime is exactly what would otherwise keep the body bobbing during a stop-and-listen)
        /// and there is no rhythm to measure against at all (an untuned profile, or a Revive that zeroed it).
        /// </summary>
        [Test]
        public void AStaleOrMissingStepTimer_ParksTheBounceAtZero()
        {
            Assert.AreEqual(0f, PlayerFacing.StepPhase(5f, 4.9f, 0.55f),
                "a timer that was not reset when the player stopped must not keep the animation running");
            Assert.AreEqual(0f, PlayerFacing.StepPhase(5f, 0f, 0.55f),
                "nextStepTime 0 is the value Revive parks the clock at");
            Assert.AreEqual(0f, PlayerFacing.StepPhase(5f, 9f, 0f),
                "with no footstep interval there is no stride to lock to, so nothing bounces");
            Assert.AreEqual(0f, PlayerFacing.StepPhase(5f, 9f, -1f), "and a negative one is the same no-op");
        }
    }
}
