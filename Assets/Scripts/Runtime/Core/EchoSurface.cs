using EchoMaze.Data;
using UnityEngine;

namespace EchoMaze.Core
{
    [DisallowMultipleComponent]
    public sealed class EchoSurface : MonoBehaviour
    {
        [Tooltip("How much of the wave comes back from this collider. The Absorber uses 0, which is the only place amplitude is assigned per object; everything else is read from geometry.")]
        [Range(0f, 1f)] public float reflectivity = 1f;
        public SurfaceKind kind = SurfaceKind.Plain;
    }
}
