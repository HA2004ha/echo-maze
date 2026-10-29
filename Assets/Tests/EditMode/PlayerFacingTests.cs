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
        public void TheFourCardinals_OfTheAimVector_GetTheFourCardinalPoses()
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
        /// The reason the fan was widened (Docs/ISSUES.md #2): the echo cone is 64°, so a pose that can sit
        /// 45° off the heading is the worst available indicator of the thing the character is doing. A held
        /// WASD diagonal is exactly |vx| = |vy|, which used to be the boundary the resolver broke by hand
        /// every frame and is now the dead centre of its own sector.
        /// </summary>
        [Test]
        public void TheFourDiagonals_GetTheFourDiagonalPoses()
        {
            Assert.AreEqual(PlayerPose.DiagonalUpRight, Move(1f, 1f));
            Assert.AreEqual(PlayerPose.DiagonalUpLeft, Move(-1f, 1f));
            Assert.AreEqual(PlayerPose.DiagonalDownRight, Move(1f, -1f));
            Assert.AreEqual(PlayerPose.DiagonalDownLeft, Move(-1f, -1f));
        }

        /// <summary>
        /// A tenth of a degree on either side of four of the eight edges, which is the table the sectors have
        /// to match. If a future edit moves a boundary by a hair, this fails and the hair has to be argued
        /// for. The edges are all rotations of 22.5 and 67.5 — the sectors are equal and the map is built from
        /// |x| and |y| — so four of them, one per quadrant, is the whole set.
        /// </summary>
        [Test]
        public void TheSectorEdges_SitAtTwentyTwoAndSixtySevenDegrees()
        {
            float d = Mathf.Deg2Rad;
            Assert.AreEqual(PlayerPose.ProfileRight, Aim(22.4f * d), "just inside the right sector");
            Assert.AreEqual(PlayerPose.DiagonalUpRight, Aim(22.6f * d), "just past it, the diagonal has it");
            Assert.AreEqual(PlayerPose.DiagonalUpRight, Aim(67.4f * d), "the diagonal keeps its own edges");
            Assert.AreEqual(PlayerPose.Back, Aim(67.6f * d), "and 67.6 is the back sector");

            Assert.AreEqual(PlayerPose.Front, Aim(-112.4f * d), "the front sector reaches down to -112.5");
            Assert.AreEqual(PlayerPose.DiagonalDownLeft, Aim(-112.6f * d),
                "and a tenth of a degree past it is the down-left diagonal's");
            Assert.AreEqual(PlayerPose.DiagonalDownLeft, Aim(-157.4f * d),
                "the diagonal keeps its own far edge");
            Assert.AreEqual(PlayerPose.ProfileLeft, Aim(-157.6f * d), "and -157.6 is due left");
        }

        /// <summary>
        /// Which side an exactly-on-the-edge heading belongs to, asserted with the slope itself rather than
        /// with a trig value: cos and sin of 22.5° are not exactly the ratio the boundary is defined by, so
        /// an assertion written at "22.5 degrees" would be testing a float coin toss. The rule is that the
        /// cardinal takes it — the pose whose file was drawn straight-on rather than one that only exists
        /// mirrored.
        /// </summary>
        [Test]
        public void AnEdgeExactlyOnTheSlope_BelongsToTheCardinal()
        {
            float slope = PlayerFacing.CardinalSlopeLimit;

            Assert.AreEqual(PlayerPose.ProfileRight, PlayerFacing.ResolveDirection(new Vector2(1f, slope)),
                "on the line, the cardinal wins");
            Assert.AreEqual(PlayerPose.DiagonalUpRight,
                PlayerFacing.ResolveDirection(new Vector2(1f, slope * 1.001f)),
                "and one part in a thousand past it is properly the diagonal's");

            // The same edge approached from the other axis, where the cardinal is the tall vector rather than
            // the wide one. Written as (slope, 1) rather than as (1, 1/slope) because only this form is an
            // exact float comparison: the reciprocal of the slope is not representable, so an assertion at
            // "67.5 degrees" would be measuring a rounding error instead of a rule.
            Assert.AreEqual(PlayerPose.Back, PlayerFacing.ResolveDirection(new Vector2(slope, 1f)),
                "the rule is the same at both edges of a sector, not whichever one the code happens to test");
            Assert.AreEqual(PlayerPose.DiagonalUpRight,
                PlayerFacing.ResolveDirection(new Vector2(slope * 1.001f, 1f)));
        }

        /// <summary>
        /// Walks the whole circle and checks each angle against the sector it falls in, so the assertions
        /// above cannot be a lucky sample of a rule that is actually wrong somewhere else.
        /// </summary>
        [Test]
        public void EveryDegree_MatchesTheSectorTable()
        {
            for (int deg = -180; deg < 180; deg++)
            {
                PlayerPose expected;
                if (deg >= -157 && deg < -112)
                {
                    expected = PlayerPose.DiagonalDownLeft;
                }
                else if (deg >= -112 && deg < -67)
                {
                    expected = PlayerPose.Front;
                }
                else if (deg >= -67 && deg < -22)
                {
                    expected = PlayerPose.DiagonalDownRight;
                }
                else if (deg >= -22 && deg < 23)
                {
                    expected = PlayerPose.ProfileRight;
                }
                else if (deg >= 23 && deg < 68)
                {
                    expected = PlayerPose.DiagonalUpRight;
                }
                else if (deg >= 68 && deg < 113)
                {
                    expected = PlayerPose.Back;
                }
                else if (deg >= 113 && deg < 158)
                {
                    expected = PlayerPose.DiagonalUpLeft;
                }
                else
                {
                    expected = PlayerPose.ProfileLeft;
                }

                // A quarter of a degree inside each cell. The edges are at x.5 degrees, so the sweep has to
                // avoid them by construction rather than by rounding: at a half-degree offset the sample
                // would land exactly on every boundary in the set, and the test would be measuring which
                // side of a float comparison cos() happened to fall on.
                Assert.AreEqual(expected, Aim((deg + 0.25f) * Mathf.Deg2Rad),
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
            Assert.AreEqual(PlayerPose.DiagonalDownLeft, Aim(-2f),
                "115 degrees below screen right is down-left, which now has a pose of its own instead of " +
                "being given to the vertical");
        }

        /// <summary>
        /// Docs/ISSUES.md #6, in one assertion. The cursor sits a hair under due left and moves a hair above
        /// it: two degrees of mouse, and the pose must not move at all.
        ///
        /// It used to move six times. Player eased the aim with <c>Mathf.LerpAngle</c>, which is a degrees API
        /// (like DeltaAngle and SmoothDampAngle), while the aim is kept in radians — so the wrap correction
        /// that is supposed to fire when the two angles straddle the seam happens to sit at a difference of
        /// 180 <em>radians</em>, which no pair of Atan2 outputs ever reaches. A crossing of due left lerped the
        /// long way instead: from −179° through screen right, up, and back down to +179°.
        ///
        /// Both directions of the crossing are checked because the seam is the sign of π, and only one of
        /// them looks like a bug from the outside.
        /// </summary>
        [Test]
        public void ACursorCrossingDueLeft_KeepsFacingLeftAllTheWayAcross()
        {
            float d = Mathf.Deg2Rad;

            float forwards = -179f * d;
            for (int step = 0; step < 20; step++)
            {
                forwards = PlayerFacing.SmoothTowards(forwards, 179f * d, 0.3f);
                Assert.AreEqual(PlayerPose.ProfileLeft, Aim(forwards),
                    "easing across due left put the character at " + Aim(forwards) + " on step " + step +
                    ", aim " + (forwards * Mathf.Rad2Deg).ToString("F1") + " degrees");
            }

            float backwards = 179f * d;
            for (int step = 0; step < 20; step++)
            {
                backwards = PlayerFacing.SmoothTowards(backwards, -179f * d, 0.3f);
                Assert.AreEqual(PlayerPose.ProfileLeft, Aim(backwards),
                    "the same crossing the other way reached " + Aim(backwards) + " on step " + step);
            }
        }

        /// <summary>
        /// The same seam on a turn that is meant to be visible: due-left-ish to down-left-ish, forty degrees
        /// the short way. Everything in between has to stay within forty degrees of due left, or the aim took
        /// the long way round the compass — which is what the case above looks like in play.
        /// </summary>
        [Test]
        public void ATurnAcrossTheSeam_TakesTheShortWayRound()
        {
            float d = Mathf.Deg2Rad;
            float current = 170f * d;
            float target = -150f * d;

            for (int step = 0; step < 30; step++)
            {
                current = PlayerFacing.SmoothTowards(current, target, 0.3f);
                float away = Mathf.Abs(Mathf.DeltaAngle(180f, current * Mathf.Rad2Deg));
                Assert.LessOrEqual(away, 40f,
                    "step " + step + " of a 170 to -150 degree turn was " + away.ToString("F1") +
                    " degrees off due left. The turn is forty degrees and goes through 180, so nothing on " +
                    "this path may reach the right-hand side of the screen");
            }

            Assert.Less(Mathf.Abs(Mathf.DeltaAngle(-150f, current * Mathf.Rad2Deg)), 1f,
                "and it has to actually arrive");
        }

        /// <summary>
        /// The degrees round-trip must not have changed the case that already worked: a turn inside one half
        /// of the screen eases like a plain lerp. Without this, the two cases above would pass for any
        /// function that held the angle still.
        /// </summary>
        [Test]
        public void ATurnThatDoesNotCrossTheSeam_EasesLikeAPlainLerp()
        {
            float d = Mathf.Deg2Rad;
            Assert.AreEqual(30f * Mathf.Deg2Rad, PlayerFacing.SmoothTowards(0f, 60f * d, 0.5f), 1e-5f,
                "halfway between due right and up-right is thirty degrees");
            Assert.AreEqual(0f, PlayerFacing.SmoothTowards(0f, 60f * d, 0f), 1e-6f, "no ease, no move");
            Assert.AreEqual(60f * d, PlayerFacing.SmoothTowards(0f, 60f * d, 1f), 1e-5f, "a full ease arrives");
        }

        [Test]
        public void AStillPlayer_FacesItsAim()
        {
            Assert.AreEqual(PlayerPose.Back, PlayerFacing.Resolve(Vector2.zero, Mathf.PI * 0.5f),
                "standing still and looking up the screen is the pose a listening player should show");
        }

        /// <summary>
        /// Below the deadzone the stick is not being pushed, it is being released. A sub-deadzone vector has
        /// to fall back to the aim rather than turn the character by a sector because a key came up
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
        /// <summary>
        /// The acceptance case Docs/ISSUES.md #2 was written for, in the form the issue asked for it: a pure
        /// function over the compass. Every pose owns everything within <c>SectorHalfWidthDegrees</c> of its
        /// own centre and hands over outside it, which is the same statement as "the drawn face is never more
        /// than 22.5° off the axis the sound travels on" — against the 64° cone, the number that made four
        /// poses the wrong tool.
        /// </summary>
        [Test]
        public void EveryPose_OwnsTwentyTwoDegreesEitherSideOfItsOwnCentre()
        {
            foreach (PlayerPose pose in AllPoses)
            {
                Vector2 centre = PlayerFacing.PoseCentre(pose);

                Assert.AreEqual(pose, PlayerFacing.ResolveDirection(centre),
                    pose + " must be the heading its own pose is most right for, or the band is measured " +
                    "from somewhere else");
                Assert.AreEqual(pose, PlayerFacing.ResolveDirection(centre * 3.7f),
                    "a heading is a direction: scaling it cannot change the answer");
                Assert.AreEqual(pose, PlayerFacing.ResolveDirection(Rotate(centre, 22f)),
                    pose + " still owns 22 degrees off its own centre");
                Assert.AreEqual(pose, PlayerFacing.ResolveDirection(Rotate(centre, -22f)));
                Assert.AreNotEqual(pose, PlayerFacing.ResolveDirection(Rotate(centre, 23f)),
                    pose + " has to hand over at 23, which is SectorHalfWidthDegrees plus a degree");
                Assert.AreNotEqual(pose, PlayerFacing.ResolveDirection(Rotate(centre, -23f)));
            }
        }

        /// <summary>
        /// The eight centres have to be the eight compass points, not eight headings that happen to name
        /// poses the map agrees with. Consecutive centres 45° apart is what makes the fan even, and the
        /// distance from every centre to every other pose's answer being at least a sector is what makes
        /// "the nearest pose wins" true rather than merely untested.
        /// </summary>
        [Test]
        public void TheEightCentres_AreFortyFiveDegreesApartAndEachIsItsOwnNeighbour()
        {
            for (int i = 0; i < AllPoses.Length; i++)
            {
                Vector2 centre = PlayerFacing.PoseCentre(AllPoses[i]);

                for (int j = 0; j < AllPoses.Length; j++)
                {
                    float separation = Vector2.Angle(centre, PlayerFacing.PoseCentre(AllPoses[j]));
                    if (i == j)
                    {
                        Assert.AreEqual(0f, separation, 1e-3f, AllPoses[i] + " is not its own centre");
                        continue;
                    }

                    Assert.GreaterOrEqual(separation, 45f - 1e-3f,
                        AllPoses[i] + " and " + AllPoses[j] + " are " + separation +
                        " degrees apart: eight poses over 360 are one eighth of the circle apart, and a " +
                        "pair closer than that leaves part of the compass with no pose at all");
                }
            }
        }

        static readonly PlayerPose[] AllPoses =
        {
            PlayerPose.Front, PlayerPose.Back, PlayerPose.ProfileRight, PlayerPose.ProfileLeft,
            PlayerPose.DiagonalDownRight, PlayerPose.DiagonalUpRight,
            PlayerPose.DiagonalDownLeft, PlayerPose.DiagonalUpLeft
        };

        static Vector2 Rotate(Vector2 vector, float degrees)
        {
            return Quaternion.Euler(0f, 0f, degrees) * vector;
        }

        /// <summary>
        /// The band is expressed against <c>PoseCentre</c>, so the width it is added to has to be the real
        /// half-width of the sectors and not a number that happens to work while there are eight poses.
        /// </summary>
        [Test]
        public void TheSectorHalfWidth_IsWhatTheSectorsAreActuallyWorth()
        {
            Assert.AreEqual(360f / AllPoses.Length, PlayerFacing.SectorHalfWidthDegrees * 2f, 1e-4f,
                "eight poses, eight equal sectors of 45 degrees. A pose count that moves again moves this with " +
                "it, and PlayerVisual's boundaryDegrees has to halve or double in the same commit or the " +
                "total dead angle on the compass changes silently");
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
