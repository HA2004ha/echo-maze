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
        Sprite sideLeftPose;
        readonly List<Object> assets = new List<Object>();

        float Stride => profile.footstepInterval;
        float Walk => profile.walkSpeed;

        [SetUp]
        public void BuildVisualPlayer()
        {
            frontPose = MakePose("test front");
            backPose = MakePose("test back");
            sidePose = MakePose("test side");
            sideLeftPose = MakePose("test side left");

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
            Wire(visual, "sideLeftSprite", sideLeftPose);

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
        /// The left profile is a drawing, not a flip. The 1024 originals were bit-exact mirrors, which is
        /// what the first version of this component assumed; the 4096 re-delivery keeps its rim light, eye
        /// and speculars on a fixed side, so mirroring the right profile draws a character lit the wrong
        /// way. Docs/ISSUES.md #5 carries the measurement.
        /// </summary>
        [UnityTest]
        public IEnumerator WalkingLeft_DrawsTheDeliveredLeftProfile()
        {
            WalkIn(Vector2.left);
            yield return null;

            Assert.AreEqual(PlayerPose.ProfileLeft, visual.CurrentPose);
            Assert.AreEqual(sideLeftPose, sprite.sprite,
                "facing left has to show the file the art team drew for it, not the right profile mirrored");
            Assert.IsFalse(sprite.flipX,
                "a flipped left profile puts the light on the side the delivered art does not light");
        }

        /// <summary>
        /// The fallback that keeps the component optional: a scene wired with three poses and no left file
        /// still shows a left-facing character, the way it did before the delivered profile existed.
        /// </summary>
        [UnityTest]
        public IEnumerator WithoutTheLeftProfile_FacingLeft_FallsBackToTheFlippedSide()
        {
            Wire(visual, "sideLeftSprite", null);

            WalkIn(Vector2.right);
            yield return null;
            WalkIn(Vector2.left);
            yield return null;

            Assert.AreEqual(PlayerPose.ProfileLeft, visual.CurrentPose);
            Assert.AreEqual(sidePose, sprite.sprite, "a scene without the left file must still have a body");
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

        [UnityTest]
        public IEnumerator StandingStill_FacesWhereThePlayerIsLooking()
        {
            subjectBody.linearVelocity = Vector2.zero;
            LookAt(Mathf.PI * 0.5f);
            yield return null;

            Assert.AreEqual(backPose, sprite.sprite,
                "a player that has stopped to listen should be facing its aim, or the mouse means nothing " +
                "while standing");

            LookAt(-Mathf.PI * 0.5f);
            yield return null;

            Assert.AreEqual(frontPose, sprite.sprite);
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
            Vector2 start = subjectBody.position;
            WalkIn(Vector2.right);

            for (int frame = 0; frame < 5; frame++)
            {
                yield return null;
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
    }
}
