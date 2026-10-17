using EchoMaze.Data;
using UnityEngine;

namespace EchoMaze.Core
{
    [DisallowMultipleComponent]
    public sealed class EchoSurface : MonoBehaviour
    {
        [Tooltip("How much of the wave comes back from this collider. Absorbers use 0.")]
        [Range(0f, 1f)] public float reflectivity = 1f;
        public SurfaceKind kind = SurfaceKind.Concrete;
    }
}
