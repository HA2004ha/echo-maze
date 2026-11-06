using EchoMaze.Data;
using UnityEngine;

namespace EchoMaze.Core
{
    public interface IEchoRenderer
    {
        void BeginFrame();

        /// <summary>
        /// Draw one arc of a ping's train. time is the clock the creature shimmer reads, so the renderer
        /// never needs a MonoBehaviour of its own to animate.
        /// </summary>
        void AddArc(PingRecord ping, int arcIndex, float baseRadius, float alpha, float time,
                    EchoTuningProfile profile);

        /// <summary>
        /// Draw a plain arc across a cone in one colour. Used for the emission cue and the Crawler's roar
        /// ripple, neither of which carries measurements; the colour is what tells "your sound" from "theirs".
        /// </summary>
        void AddConeOutline(Vector2 origin, float aimRad, float coneHalfRad, float radius,
                            float alpha, Color color, EchoTuningProfile profile);

        void EndFrame();
    }
}
