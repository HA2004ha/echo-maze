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

        /// <summary>Draw a plain arc across a cone. Used for the emission cue, which carries no measurements.</summary>
        void AddConeOutline(Vector2 origin, float aimRad, float coneHalfRad, float radius,
                            float alpha, EchoTuningProfile profile);

        void EndFrame();
    }
}
