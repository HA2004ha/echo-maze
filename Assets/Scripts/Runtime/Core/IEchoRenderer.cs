using EchoMaze.Data;
using UnityEngine;

namespace EchoMaze.Core
{
    public interface IEchoRenderer
    {
        void BeginFrame();

        void AddArc(PingRecord ping, int arcIndex, float baseRadius, float alpha, EchoTuningProfile profile);

        void EndFrame();
    }
}
