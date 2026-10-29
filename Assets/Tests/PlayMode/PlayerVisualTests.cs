using System.Collections;
using System.Collections.Generic;
using EchoMaze.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace EchoMaze.Tests.PlayMode
{
    /// <summary>
    /// The character's pose and its motions, tested on a wired rig rather than in the blockout scene.
    ///
    /// The Player component is switched off in these tests and the three things it would have driven are
    /// set directly: the body's velocity, the aim angle and the footstep clock. Simulated keys would be
    /// more faithful to the hand, and less trustworthy here: the runner's own input state is not something
    /// a test should depend on, and a keyboard is not guaranteed to exist on the machine running it. What
    /// PlayerVisual reads is what Player writes, so setting those three is setting Player's output.
    ///
    /// The sprites are made with Sprite.Create instead of loaded from Assets/Art. The rule that art has to
    /// be a real asset is about what a scene can serialise; a component field in a test only has to be a
    /// sprite, and comparing by reference proves which pose was picked without reading pixels.
    /// </summary>
    public class PlayerVisualTests : BlockoutRig
    {
        const float Far = 30f;

        Player subject;
        PlayerVisual visual;
        Rigidbody2D subjectBody;
        CircleCollider2D subjectCollider;
        Transform visuals;
        SpriteRenderer sprite;

        Sprite frontPose;
        Sprite backPose;
        Sprite sidePose;
        Sprite diagonalDownPose;
        Sprite diagonalUpPose;
        readonly List<Object> assets = new List<Object>();

        float Stride => profile.footstepInterval;
        float Walk => profile.walkSpeed;

        [SetUp]
        public void BuildVisualPlayer()
        {
            frontPose = MakePose("test front");
            backPose = MakePose("test back");
            sidePose = MakePose("test side");
            diagonalDownPose = MakePose("test 34 front");
            diagonalUpPose = MakePose("test 34 back");

            GameObject go = New("rig visual player", false);
            go.transform.position = new Vector2(Far, Far);

            subjectBody = go.AddComponent<Rigidbody2D>();
            subjectBody.gravityScale = 0f;
            subjectBody.freezeRotation = true;
            subjectCollider = go.AddComponent<CircleCollider2D>();
            subjectCollider.radius = 0.25f;

            subject = go.AddComponent<Player>();
            Wire(subject, "director", director);
            Wire(subject, "marks", marks);

            visual = go.AddComponent<PlayerVisual>();
            Wire(visual, "frontSprite", frontPose);
            Wire(visual, "backSprite", backPose);
            Wire(visual, "sideSprite", sidePose);
            Wire(visual, "diagonalDownSprite", diagonalDownPose);
            Wire(visual, "diagonalUpSprite", diagonalUpPose);

            // Built before the activation, because PlayerVisual resolves the Visuals child in Awake and a
            // component that misses it there never gets a second chance.
            GameObject child = new GameObject("Visuals");
            child.transform.SetParent(go.transform, false);
            sprite = child.AddComponent<SpriteRenderer>();
            visuals = child.transform;

            go.SetActive(true);
            subject.enabled = false;
        }

        [TearDown]
        public void TearDownPoses()
        {
            foreach (Object asset in assets)
            {
                Object.Destroy(asset);
            }

            assets.Clear();
        }

        Sprite MakePose(string name)
        {
            var texture = new Texture2D(4, 4);
            var pose = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 100f);
            pose.name = name;
            assets.Add(texture);
            assets.Add(pose);
            return pose;
        }

        void WalkIn(Vector2 direction)
        {
            subjectBody.linearVelocity = direction * Walk;
        }

        void LookAt(float radians)
        {
            Wire(subject, "aimRad", radians);
        }

        /// <summary>
        /// The state a held Shift puts Player in. IsQuiet has a private setter that only Player.Update can
        /// reach, and Update is what these tests switch off, so the field behind the property is the honest
        /// place to put it.
        /// </summary>
        void HoldShift(bool held)
        {
            Wire(subject, "<IsQuiet>k__BackingField", held);
        }

        [UnityTest]
        public IEnumerator WalkingRight_ShowsTheSidePoseUnflipped()
        {
            WalkIn(Vector2.right);
            yield return null;

            Assert.AreEqual(PlayerPose.ProfileRight, visual.CurrentPose);
            Assert.AreEqual(sidePose, sprite.sprite, "the canonical profile faces screen right");
            Assert.IsFalse(sprite.flipX);
        }

        /// <summary>
        /// Facing left is the one texture mirrored. This is only free because the two profile files are
        /// bit-exact mirrors — re-measured on the 4096 delivery, after one export arrived lit from the wrong
        /// side and went back (Docs/ISSUES.md #5). If the art team ever draws the two sides differently, a
        /// shield, a lantern, a fixed light, the mirror stops reproducing the drawing and this case is where
        /// the change has to be made.
        /// </summary>
        [UnityTest]
        public IEnumerator WalkingLeft_ReusesTheSameTextureFlipped()
        {
            WalkIn(Vector2.left);
            yield return null;

            Assert.AreEqual(PlayerPose.ProfileLeft, visual.CurrentPose);
            Assert.AreEqual(sidePose, sprite.sprite,
                "a second left-profile sprite file would be 2.7 MB of texture memory (1024 squared, DXT5, " +
                "with mip levels) to redraw a picture the art is proved to be a mirror of");
            Assert.IsTrue(sprite.flipX);
        }

        [UnityTest]
        public IEnumerator WalkingUpscreen_ShowsTheBackPose()
        {
            WalkIn(Vector2.up);
            yield return null;

            Assert.AreEqual(PlayerPose.Back, visual.CurrentPose);
            Assert.AreEqual(backPose, sprite.sprite);
        }

        [UnityTest]
        public IEnumerator WalkingDownscreen_ShowsTheFrontPose()
        {
            WalkIn(Vector2.down);
            yield return null;

            Assert.AreEqual(PlayerPose.Front, visual.CurrentPose);
            Assert.AreEqual(frontPose, sprite.sprite);
        }

        /// <summary>
        /// The four diagonals are the whole point of Docs/ISSUES.md #2: the echo cone is 64° wide and steered
        /// by the same number that picks the pose, so a character that can sit 45° off the axis it is shouting
        /// along is the worst available indicator of its own aim. Each pair below is one texture.
        ///
        /// Which member of the pair is the mirror is <em>not</em> the same question as which member faces left,
        /// and conflating them is the bug these four cases exist to keep out: the profile is authored facing
        /// screen right, the two 3/4 stills are authored facing screen left (their cyan mass sits 121 px left of
        /// their own centre against +197 px in the profile, and the light in this set is on the side the
        /// character faces). So the flip is the disagreement between the two, and the unflipped diagonal is the
        /// leftward one.
        /// </summary>
        [UnityTest]
        public IEnumerator WalkingDownAndLeft_ShowsTheAuthoredTurnedFront()
        {
            WalkIn(new Vector2(-1f, -1f).normalized);
            yield return null;

            Assert.AreEqual(PlayerPose.DiagonalDownLeft, visual.CurrentPose);
            Assert.AreEqual(diagonalDownPose, sprite.sprite);
            Assert.IsFalse(sprite.flipX, "Player_34_front is drawn facing screen left, so the leftward " +
                                         "diagonal needs no mirror");
        }

        [UnityTest]
        public IEnumerator WalkingDownAndRight_TurnsTheSameFrontAround()
        {
            WalkIn(new Vector2(1f, -1f).normalized);
            yield return null;

            Assert.AreEqual(PlayerPose.DiagonalDownRight, visual.CurrentPose);
            Assert.AreEqual(diagonalDownPose, sprite.sprite,
                "a third 3/4 file would be 2.7 MB of texture memory for a picture this one mirrored");
            Assert.IsTrue(sprite.flipX);
        }

        [UnityTest]
        public IEnumerator WalkingUpAndLeft_ShowsTheAuthoredTurnedBack()
        {
            WalkIn(new Vector2(-1f, 1f).normalized);
            yield return null;

            Assert.AreEqual(PlayerPose.DiagonalUpLeft, visual.CurrentPose);
            Assert.AreEqual(diagonalUpPose, sprite.sprite);
            Assert.IsFalse(sprite.flipX);
        }

        [UnityTest]
        public IEnumerator WalkingUpAndRight_TurnsTheSameBackAround()
        {
            WalkIn(new Vector2(1f, 1f).normalized);
            yield return null;

            Assert.AreEqual(PlayerPose.DiagonalUpRight, visual.CurrentPose);
            Assert.AreEqual(diagonalUpPose, sprite.sprite);
            Assert.IsTrue(sprite.flipX);
        }

        [UnityTest]
        public IEnumerator StandingStill_FacesWhereThePlayerIsLooking()
        {
            subjectBody.linearVelocity = Vector2.zero;
            LookAt(Mathf.PI * 0.5f);
            yield return null;

            Assert.AreEqual(backPose, sprite.sprite,
                "a player that has stopped to listen should be facing its aim, or the mouse means nothing " +
                "while standing");

            // The dwell gate is 100 ms of wall clock between two pose changes, so the second aim has to be
            // given its turn. Waiting rather than wiring the dwell to zero keeps this test answering the
            // question it was written for (does the aim drive the pose) without stepping over a gate that
            // exists on purpose; Dwell_BlocksASecondPoseInsideItsWindow is the case for the gate itself.
            yield return new WaitForSecondsRealtime(0.15f);

            LookAt(-Mathf.PI * 0.5f);
            yield return null;

            Assert.AreEqual(frontPose, sprite.sprite);
        }

        /// <summary>
        /// The acceptance case Docs/ISSUES.md #3 asked for.
        ///
        /// A heading parked on a sector boundary is the worst case the resolver used to have: it is 22.5
        /// degrees off the pose being drawn, so the raw map wants a neighbour every frame and any shake of the
        /// aim turns the character. The jitter is the largest per-pixel noise that can reach the band at all —
        /// one pixel of cursor at <c>aimNoiseRadius</c> (24 px, where the aim stops being trusted entirely),
        /// which is 2.4 degrees — and "at most once" is the wording the issue chose, because a pose that
        /// settles onto the other side once and then stops is not the defect being described.
        /// </summary>
        [UnityTest]
        public IEnumerator ParkedOnASectorBoundary_CursorJitterForThirtyFrames_ChangesThePoseAtMostOnce()
        {
            subjectBody.linearVelocity = Vector2.zero;
            LookAt(Mathf.PI * 0.125f);

            int changes = 0;
            PlayerPose previous = visual.CurrentPose;

            for (int frame = 0; frame < 30; frame++)
            {
                // Alternating either side of the boundary, which is the case that used to alternate poses.
                LookAt(Mathf.PI * 0.125f + (frame % 2 == 0 ? 1f : -1f) * 2.4f * Mathf.Deg2Rad);
                yield return null;

                if (visual.CurrentPose != previous)
                {
                    changes++;
                    previous = visual.CurrentPose;
                }
            }

            Assert.LessOrEqual(changes, 1,
                "thirty frames of one-pixel cursor noise on a boundary produced " + changes +
                " turns. The band is SectorHalfWidthDegrees + boundaryDegrees = 27.5 degrees off the pose " +
                "being drawn, so anything inside it has to hold still");
        }

        /// <summary>
        /// The other half: the band must not have become "never turn". A heading that has properly left its
        /// pose — here a full half-turn, well past 22.5 + 5 degrees — is taken on the next frame after the
        /// dwell window, and without this case the test above would pass for a character that is frozen.
        /// </summary>
        [UnityTest]
        public IEnumerator AHeadingThatReallyLeftItsSector_TakesTheNewPose()
        {
            subjectBody.linearVelocity = Vector2.zero;
            LookAt(0f);
            yield return null;
            Assert.AreEqual(PlayerPose.ProfileRight, visual.CurrentPose);

            yield return new WaitForSecondsRealtime(0.15f);
            LookAt(Mathf.PI);
            yield return null;

            Assert.AreEqual(PlayerPose.ProfileLeft, visual.CurrentPose,
                "180 degrees off the pose being drawn is not a boundary case, it is a turn");
        }

        /// <summary>
        /// A held WASD diagonal is |vx| = |vy| forever. Under four poses that was the boundary the resolver
        /// broke by hand every single frame, and the reason issue 3 existed; under eight it is the dead centre
        /// of a sector, so the two seconds of it this case runs has nothing left to argue about. Kept because
        /// it is the case that fails the day somebody moves a sector edge onto the keyboard: the pose has to
        /// be the diagonal, and it has to be that pose on every frame after the first.
        /// </summary>
        [UnityTest]
        public IEnumerator WalkingASteadyDiagonal_ForTwoSeconds_HoldsOnePose()
        {
            WalkIn(new Vector2(1f, 1f).normalized);

            yield return null;
            PlayerPose settled = visual.CurrentPose;

            Assert.AreEqual(PlayerPose.DiagonalUpRight, settled,
                "a perfect diagonal is 45 degrees, which is the centre of its own sector now that the fan " +
                "has eight of them");

            for (int frame = 0; frame < 120; frame++)
            {
                WalkIn(new Vector2(1f, 1f).normalized);
                yield return null;

                Assert.AreEqual(settled, visual.CurrentPose,
                    "a perfect diagonal changed the pose on frame " + frame + ": the tie is the common case " +
                    "of this input, not an edge of the number line");
            }
        }

        /// <summary>
        /// The dwell is the gate that covers what hysteresis cannot see: a movement/aim handover, where the
        /// velocity crosses the deadzone and the *source* changes without any heading crossing a boundary.
        /// Two poses requested inside one window get one draw.
        /// </summary>
        [UnityTest]
        public IEnumerator Dwell_BlocksASecondPoseInsideItsWindow()
        {
            subjectBody.linearVelocity = Vector2.zero;
            LookAt(0f);
            yield return null;
            Assert.AreEqual(PlayerPose.ProfileRight, visual.CurrentPose);

            // Half-turn out of the band, so the only thing that can be refusing the next change is the dwell.
            LookAt(Mathf.PI);
            yield return null;
            Assert.AreEqual(PlayerPose.ProfileLeft, visual.CurrentPose,
                "the first turn is allowed, or the gate would be a freeze");

            // The window is widened rather than the frame duration being trusted: two frames on a loaded
            // runner can be further apart than 100 ms, and a case that only fails on a slow machine is worse
            // than the flicker it is here to catch. What 0.1 s is the number TUNING.md prices.
            Wire(visual, "dwellSeconds", 5f);

            LookAt(0f);
            yield return null;

            Assert.AreEqual(PlayerPose.ProfileLeft, visual.CurrentPose,
                "inside its window the second pose is not drawn, even though the band is cleared twice over");
        }

        /// <summary>
        /// Cause 3 of the issue: standing still with the pointer resting on the character. The angle from
        /// body to cursor is real and it is meaningless, and before this gate the pose was driven by
        /// sub-pixel noise. Inside the radius the aim stops being an input at all.
        /// </summary>
        [UnityTest]
        public IEnumerator CursorParkedOnTheBody_StopsDraggingThePose()
        {
            subjectBody.linearVelocity = Vector2.zero;
            LookAt(Mathf.PI * 0.5f);
            yield return null;
            Assert.AreEqual(PlayerPose.Back, visual.CurrentPose);

            Wire(subject, "aimDistance", 0.1f);
            LookAt(-Mathf.PI * 0.5f);

            for (int frame = 0; frame < 10; frame++)
            {
                yield return null;
            }

            Assert.AreEqual(PlayerPose.Back, visual.CurrentPose,
                "the pointer is 0.1 m off the body, inside aimNoiseRadius, so a half-turn of aim is noise " +
                "and the character keeps facing where it last meant something");
        }

        /// <summary>
        /// The decision taken at the art review, here so that nobody "improves" it later: the body turns to
        /// where it is going even when that is opposite to the aim, because movement is the thing that makes
        /// the noise the game is built on.
        /// </summary>
        [UnityTest]
        public IEnumerator WalkingAgainstItsOwnAim_FacesWhereTheBodyIsGoing()
        {
            LookAt(Mathf.PI * 0.5f);
            WalkIn(Vector2.down);
            yield return null;

            Assert.AreEqual(frontPose, sprite.sprite,
                "aimed up and walking down: showing the back would read as sliding backwards");
        }

        [UnityTest]
        public IEnumerator AStepMidway_RaisesTheBodyAndLeansItIntoTheWalk()
        {
            WalkIn(Vector2.right);
            Wire(subject, "nextStepTime", Time.time + Stride * 0.5f);
            yield return null;

            Assert.Greater(visual.Phase, 0.3f,
                "half a stride after a footstep should be the top of the hop, given the frame budget of " +
                "the test runner");
            Assert.Less(visual.Phase, 1f, "phase is a fraction of a stride and must stay inside one");
            Assert.Greater(visuals.localPosition.y, 0.01f,
                "the hop is 2.7 pixels at the play resolution: felt rather than seen, but not zero");
            Assert.Greater(Mathf.Abs(visuals.localRotation.z), 0.01f,
                "the body tips into the direction it walks");
        }

        /// <summary>
        /// Silent walking has to look like something, because it is the one move the game teaches rather
        /// than the one it punishes. calm is one multiplier on every motion term, so this asserts that all
        /// three of them land on zero together.
        /// </summary>
        [UnityTest]
        public IEnumerator HoldingShift_FlattensEveryMotionWithinTheCalmWindow()
        {
            WalkIn(Vector2.right);
            Wire(subject, "nextStepTime", Time.time + Stride * 0.5f);
            yield return null;
            Assert.Greater(visuals.localPosition.y, 0.01f, "the same frame before Shift has motion to flatten");

            HoldShift(true);

            // Real time, because the ease runs on unscaledDeltaTime like the rest of the quiet UI does.
            yield return new WaitForSecondsRealtime(0.3f);
            yield return null;

            Assert.AreEqual(0f, visual.Calm, "the window is shorter than the wait, so it has to bottom out");
            Assert.AreEqual(0f, visuals.localPosition.y, 1e-6f, "the hop is gone");
            Assert.AreEqual(0f, visuals.localRotation.z, 1e-6f, "the lean is gone");
            Assert.AreEqual(1f, visuals.localScale.x, 1e-6f, "the squash is gone");
            Assert.AreEqual(1f, visuals.localScale.y, 1e-6f);
            Assert.AreEqual(sidePose, sprite.sprite,
                "going quiet stops the motion, not the walking: the pose still follows the body");
        }

        [UnityTest]
        public IEnumerator NotHoldingShift_KeepsEveryMotionRunning()
        {
            WalkIn(Vector2.right);
            Wire(subject, "nextStepTime", Time.time + Stride * 0.5f);
            HoldShift(false);

            yield return new WaitForSecondsRealtime(0.3f);
            yield return null;

            Assert.Greater(visual.Calm, 0.9f);
            Assert.Greater(Mathf.Abs(visuals.localRotation.z), 0.01f,
                "the counter-case to the flattening test: if the lean were always zero, quiet would prove " +
                "nothing");
        }

        /// <summary>
        /// The bounce is locked to Player's own footstep clock rather than a timer of its own, and this is
        /// the pair of moments that proves it. A re-derived timer keeps bouncing after you stop (its clock
        /// goes stale) and lands out of step when you start again (the first step after a resume fires
        /// immediately). Both of those are the sounds the game teaches with.
        /// </summary>
        [UnityTest]
        public IEnumerator TheBounce_IsLockedToTheFootstepClock()
        {
            WalkIn(Vector2.right);

            // A stale clock: the footstep that was due has long since passed, so the body is on the ground.
            Wire(subject, "nextStepTime", Time.time - 1f);
            yield return null;
            Assert.AreEqual(0f, visual.Phase, 1e-6f, "a stale clock must park the bounce, not run it");
            Assert.AreEqual(0f, visuals.localPosition.y, 1e-6f);
            Assert.Less(visuals.localScale.y, 1f,
                "the frame a footstep lands is a dip: impact squash with no hop over it");

            // The next step is now due a full stride away, which is the frame after a real footstep fires.
            Wire(subject, "nextStepTime", Time.time + Stride);
            yield return null;
            Assert.Less(visual.Phase, 0.35f,
                "the bounce restarts near the ground on the step itself, not somewhere into the stride. The " +
                "bound is a frame and a half of the runner, not a stride fraction: this is the test that " +
                "would fail if the visual carried its own timer instead");

            yield return new WaitForSecondsRealtime(Stride * 0.5f);
            yield return null;
            Assert.Greater(visual.Phase, 0.4f, "half a stride later it is near the top of the hop");
            Assert.Less(visual.Phase, 1f);
            Assert.Greater(visuals.localPosition.y, 0.01f);
        }

        [UnityTest]
        public IEnumerator Dying_HoldsTheLastPoseStandingStraightAndStill()
        {
            WalkIn(Vector2.up);
            yield return null;
            Assert.AreEqual(backPose, sprite.sprite, "dying should not itself change the pose");

            subject.Die();

            // A dead body still has a velocity here: Player.FixedUpdate is off with the component, so the
            // visual has to decide what to draw from IsAlive rather than from the motion it can see.
            WalkIn(Vector2.right);
            yield return null;
            yield return null;

            Assert.AreEqual(backPose, sprite.sprite,
                "the last pose is held: a corpse that turns to face its fall is a different statement");
            Assert.AreEqual(0f, visual.Phase, 1e-6f);
            Assert.AreEqual(0f, visuals.localPosition.y, 1e-6f, "standing up straight");
            Assert.AreEqual(0f, visuals.localRotation.z, 1e-6f);
            Assert.AreEqual(1f, visuals.localScale.y, 1e-6f,
                "and still: a body that keeps breathing after it has died is the animation saying the " +
                "player is not dead");
        }

        /// <summary>
        /// Everything the animation writes goes to the Visuals child. The root is what Rigidbody2D and the
        /// collider live on, so a squash that reached it would resize what hits walls and fight the body's
        /// interpolation. This is the guard that keeps them apart.
        /// </summary>
        [UnityTest]
        public IEnumerator NothingTheVisualDoes_TouchesTheBodyThatCollides()
        {
            // This case is about the visual and the collider layout, not about input. On a headless runner
            // there is no Keyboard.current, so Player.FixedUpdate would zero the velocity the test just
            // wrote and the body would never move. The visual does not need Player.Update to be running:
            // it reads the body, which is still simulated.
            subject.enabled = false;

            // A paused clock from an earlier scene would also stop the solver; the headless runner is the
            // place where that is easy to inherit and hard to see.
            Time.timeScale = 1f;
            subjectBody.simulated = true;
            subjectBody.WakeUp();

            Vector2 start = subjectBody.position;
            WalkIn(Vector2.right);

            for (int frame = 0; frame < 5; frame++)
            {
                // WaitForFixedUpdate rather than one rendered frame: in batchmode a frame can pass without
                // the physics tick this test is measuring, and then "did not move" means "was not stepped".
                yield return new WaitForFixedUpdate();
            }

            Assert.AreNotEqual(start, subjectBody.position,
                "the body travelled, so the assertions below are about a running simulation");
            Assert.AreEqual(Vector3.one, subject.transform.localScale,
                "a scaled root scales the collider: walls would stop the player at the wrong distance");
            Assert.AreEqual(0f, subject.transform.rotation.z, 1e-6f,
                "a rotated root is a rotation the body's interpolation has to smooth");
            Assert.AreEqual(0.25f, subjectCollider.radius, 1e-6f);
            Assert.Greater(Mathf.Abs(visuals.localRotation.z), 0.01f,
                "and the child did move, so the three checks above are not both vacuous");
        }

        /// <summary>
        /// The visual is optional, and the two halves of that promise are tested apart. This one is the
        /// rig the week-2 tests already use: no PlayerVisual, no Visuals child, and a full frame of it
        /// running. The runner fails any test that logs an error, so surviving this frame is the proof that
        /// nothing in Player now depends on the art being there.
        /// </summary>
        [UnityTest]
        public IEnumerator TheWeek2RigPlayer_NeedsNoArtAtAll()
        {
            Assert.IsNull(player.GetComponent<PlayerVisual>());
            Assert.IsNull(player.transform.Find("Visuals"));

            yield return null;
            yield return null;

            Assert.IsTrue(player.IsAlive, "the week-2 rig still plays with no art on it");
        }

        /// <summary>
        /// The other half: a PlayerVisual that is present but unwired disables itself without a word,
        /// rather than failing every frame on a missing sprite. A scene built before the art landed, or a
        /// teammate who has not run the bootstrap since a rename, gets a dot and no red console.
        /// </summary>
        [UnityTest]
        public IEnumerator APlayerVisualWithoutItsSprites_TurnsItselfOffSilently()
        {
            GameObject go = New("rig unwired visual", false);
            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;

            var unwiredPlayer = go.AddComponent<Player>();
            Wire(unwiredPlayer, "director", director);
            Wire(unwiredPlayer, "marks", marks);

            var unwired = go.AddComponent<PlayerVisual>();

            go.SetActive(true);

            Assert.IsFalse(unwired.enabled,
                "an incomplete setup has to opt out quietly: the builder already warned about the art, and " +
                "an error here would fail every PlayMode test that logs");

            yield return null;
            yield return null;

            Assert.IsFalse(unwired.enabled, "and it stays off rather than being re-enabled by anything");
        }

        /// <summary>
        /// The half-wired case the eight-pose change actually creates: a scene generated while the fan had
        /// four poses has the three cardinal sprites and nothing else, and a visual that drew cardinals and
        /// then went blank on the first diagonal would be worse than one that opted out. The builder is what
        /// promises the art, and it now requires all five files or falls back to the dot, so this is the guard
        /// that the promise is kept in both directions.
        /// </summary>
        [UnityTest]
        public IEnumerator APlayerVisualFromTheFourPoseEra_TurnsItselfOffSilently()
        {
            GameObject go = New("rig four pose visual", false);
            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;

            var oldPlayer = go.AddComponent<Player>();
            Wire(oldPlayer, "director", director);
            Wire(oldPlayer, "marks", marks);

            var old = go.AddComponent<PlayerVisual>();
            Wire(old, "frontSprite", frontPose);
            Wire(old, "backSprite", backPose);
            Wire(old, "sideSprite", sidePose);

            GameObject child = new GameObject("Visuals");
            child.transform.SetParent(go.transform, false);
            child.AddComponent<SpriteRenderer>();

            go.SetActive(true);

            Assert.IsFalse(old.enabled,
                "three of five sprites is an incomplete setup: it has to opt out like the empty one does, " +
                "not stand on a diagonal with no picture to draw");

            yield return null;
            yield return null;

            Assert.IsFalse(old.enabled);
        }
    }
}
