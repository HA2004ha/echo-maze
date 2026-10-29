using System;
using EchoMaze.Core;
using EchoMaze.Data;
using UnityEngine;

namespace EchoMaze.Gameplay
{
    /// <summary>
    /// Glass shards, gravel, a puddle: a floor that answers a step. The GDD's rule is that the cost is paid
    /// for walking *normally* — Shift already makes no footstep noise anywhere, and it has to keep working
    /// here or the silent step stops being the one cheap move the game offers.
    ///
    /// Containment is measured against the zone's own rectangle rather than a trigger callback: the query
    /// does not depend on the collision matrix, does not need a collider on the body, and cannot miss an
    /// enter because of an execution order. The zone is invisible geometry on purpose — the player learns it
    /// is there by hearing it, and by the noise it makes on the enemy's side of the trade.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NoisyFloor : MonoBehaviour
    {
        EchoDirector director;
        Player player;
        Bounds area;
        float interval = 0.45f;
        float nextNoiseAt;
        bool inside;

        /// <summary>Raised every time the floor reports a step, for the HUD's "you are being loud" cue.</summary>
        public event Action Noised;

        public void Build(EchoDirector owningDirector, Player watched, Vector2 centre, Vector2 size,
                          float stepInterval)
        {
            director = owningDirector;
            player = watched;
            area = new Bounds(centre, size);
            interval = Mathf.Max(0.05f, stepInterval);
        }

        void Update()
        {
            if (director == null || player == null || !player.IsAlive)
            {
                return;
            }

            Vector2 position = player.Position;
            bool nowInside = area.Contains(position);
            inside = nowInside;

            if (!nowInside || !player.IsMoving || player.IsQuiet || Time.time < nextNoiseAt)
            {
                return;
            }

            EchoTuningProfile profile = director.Profile;
            if (profile == null)
            {
                return;
            }

            nextNoiseAt = Time.time + interval;
            director.ReportNoise(position, profile.noisyFloorLoudness);
            Noised?.Invoke();
        }

        /// <summary>True while the player is standing on it, for the HUD and for tests.</summary>
        public bool Contains(Vector2 position) => inside;
    }
}
