using System.Collections.Generic;
using System.Reflection;
using EchoMaze.Core;
using EchoMaze.Data;
using EchoMaze.Gameplay;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace EchoMaze.Tests.PlayMode
{
    /// <summary>
    /// A wired blockout world for the PlayMode tests: a director, a profile, a player, touch marks and a
    /// crawler, built the way the scene builder wires them but without touching the scene.
    ///
    /// The references on these components are private and set by the editor builder through
    /// SerializedObject, which the PlayMode assembly cannot use. Reflection is the honest way to put a
    /// component into the state the builder puts it in, and it keeps the tests off the inspector.
    /// </summary>
    public abstract class BlockoutRig
    {
        protected readonly List<GameObject> built = new List<GameObject>();

        protected EchoDirector director;
        protected EchoTuningProfile profile;
        protected Player player;
        protected TouchMarks marks;

        [SetUp]
        public void BuildDirector()
        {
            profile = ScriptableObject.CreateInstance<EchoTuningProfile>();
            profile.cooldownBase = 0f;
            profile.cooldownPerLoudness = 0f;

            director = New("rig director").AddComponent<EchoDirector>();
            director.UseProfile(profile);

            marks = New("rig marks").AddComponent<TouchMarks>();
            player = NewPlayer(Vector2.zero);
        }

        [TearDown]
        public void TearDownRig()
        {
            foreach (GameObject go in built)
            {
                // Switched off before the deferred Destroy, so a later test's FindObjectsByType cannot
                // pick up an object that is still in the scene for one more frame. That is how a rig
                // leaks into its neighbour's assertions.
                go.SetActive(false);
                Object.Destroy(go);
            }

            Object.Destroy(profile);
            built.Clear();
        }

        /// <summary>
        /// Components are added to an inactive GameObject because Awake runs on activation, and both Player
        /// and Crawler log an error about the references they have not been given yet. An error in a test
        /// fails it, so the wiring has to happen before the object wakes up.
        ///
        /// The colliders and the body settings mirror what the scene builder puts on them, because the
        /// rules under test include the ones that only exist in a collision.
        /// </summary>
        protected Player NewPlayer(Vector2 spawn)
        {
            GameObject go = New("rig player", false);
            go.transform.position = spawn;

            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            go.AddComponent<CircleCollider2D>().radius = 0.25f;

            var component = go.AddComponent<Player>();
            Wire(component, "director", director);
            Wire(component, "marks", marks);
            go.SetActive(true);
            return component;
        }

        protected Crawler NewCrawler(Vector2 start, Vector2[] patrol)
        {
            GameObject go = New("rig crawler", false);
            go.transform.position = start;

            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            go.AddComponent<CircleCollider2D>().radius = 0.32f;

            var component = go.AddComponent<Crawler>();
            Wire(component, "director", director);
            Wire(component, "player", player);
            Wire(component, "patrolPoints", patrol);
            go.SetActive(true);
            return component;
        }

        protected Crawler NewCrawler(Vector2 start)
        {
            return NewCrawler(start, new[] { start + Vector2.right * 2f, start - Vector2.right * 2f });
        }

        /// <summary>An object the rig will destroy at the end of the test.</summary>
        protected GameObject New(string name, bool active = true)
        {
            var go = new GameObject(name);
            go.SetActive(active);
            built.Add(go);
            return go;
        }

        protected static void Wire(Component target, string field, object value)
        {
            Field(target, field).SetValue(target, value);
        }

        protected static object Value(object target, string name)
        {
            return Field(target, name).GetValue(target);
        }

        static FieldInfo Field(object target, string name)
        {
            return target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
        }
    }
}
