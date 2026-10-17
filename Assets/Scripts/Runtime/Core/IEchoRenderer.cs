using EchoMaze.Data;
using UnityEngine;

namespace EchoMaze.Core
{
    public interface IEchoRenderer
    {
        void BeginFrame();

        void AddArc(PingRecord ping, int arcIndex, float baseRadius, float alpha, EchoTuningProfile profile);

        /// <summary>Draw a plain arc across a cone. Used for the emission cue, which carries no measurements.</summary>
        void AddConeOutline(Vector2 origin, float aimRad, float coneHalfRad, float radius,
                            float alpha, EchoTuningProfile profile);

        void EndFrame();
    }
}
