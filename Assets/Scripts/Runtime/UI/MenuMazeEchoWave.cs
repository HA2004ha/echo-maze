using System.Collections;
using UnityEngine;

namespace EchoMaze.UI
{
    /// <summary>
    /// The menu's echo, expressed the only way a model can echo: every few seconds a wave of light
    /// starts at a random point of the diorama and washes outward, each wall block brightening as the
    /// band passes it and settling back to slate behind it. No rings, no rays, no geometry is drawn —
    /// the phase-1 version of this idea painted arc ribbons across the screen and the user was right
    /// to hate them; the wave should be something you watch the walls do, not something on top of
    /// the picture.
    ///
    /// Intensity goes through a <see cref="MaterialPropertyBlock"/> on MazeBlock's _Wave, past 1 where
    /// the band crests, so Bloom — not a brighter flat cyan — is what sells the moment a wall is lit.
    /// The block is built once and reused; a pulse allocates nothing.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MenuMazeEchoWave : MonoBehaviour
    {
        [Tooltip("The container whose wall renderers the wave washes. The base plate and the trim sit " +
                 "outside it on purpose: the frame hums from its own material and only walls echo.")]
        [SerializeField] Transform mazeWallsRoot;
        [SerializeField] float intervalSeconds = 4.2f;
        [Tooltip("How long one wave takes to cross the model. Shorter reads as a flicker, longer as a " +
                 "searchlight; this is the speed of the thing arriving, not of the thing sweeping.")]
        [SerializeField] float waveSeconds = 1.6f;
        [Tooltip("Distance the crest travels in one wave — past the far corner of the model, so no " +
                 "wall is exempt.")]
        [SerializeField] float reach = 13f;
        [Tooltip("Radial width of the bright band. Wide is a tide, narrow is a drum skin.")]
        [SerializeField] float bandWidth = 1.7f;
        [Tooltip("The wave's colour at crest, HDR. Only the rgb matters; the shader multiplies by the " +
                 "band's intensity itself. Deliberately short of the old 2.2 green: the crest washes " +
                 "over the panel behind the menu text, and Bloom turns a brighter wave into a screen " +
                 "you cannot read.")]
        [SerializeField] Color waveColor = new Color(0.22f, 1.5f, 2.1f);
        [Tooltip("Half-width of the square the origin is picked in, in model units.")]
        [SerializeField] float originHalfExtent = 4.4f;

        MeshRenderer[] walls;
        MaterialPropertyBlock block;

        void Awake()
        {
            if (mazeWallsRoot != null)
            {
                walls = mazeWallsRoot.GetComponentsInChildren<MeshRenderer>(false);
            }

            block = new MaterialPropertyBlock();
        }

        void OnEnable()
        {
            StartCoroutine(Waves());
        }

        IEnumerator Waves()
        {
            while (true)
            {
                yield return new WaitForSecondsRealtime(
                    Mathf.Max(0.05f, intervalSeconds - waveSeconds));

                if (walls == null || walls.Length == 0 || mazeWallsRoot == null)
                {
                    continue;
                }

                yield return OneWave();
            }
        }

        IEnumerator OneWave()
        {
            Vector3 centre = mazeWallsRoot.position;
            Vector2 origin = centre + new Vector3(Random.Range(-originHalfExtent, originHalfExtent), 0f,
                                                  Random.Range(-originHalfExtent, originHalfExtent));

            float elapsed = 0f;
            while (elapsed < waveSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float crest = (elapsed / waveSeconds) * reach;

                for (int i = 0; i < walls.Length; i++)
                {
                    if (walls[i] == null)
                    {
                        continue;
                    }

                    Vector3 offset = walls[i].transform.position - new Vector3(origin.x, 0f, origin.y);
                    float distance = Mathf.Sqrt(offset.x * offset.x + offset.z * offset.z);
                    float d = (distance - crest) / Mathf.Max(0.01f, bandWidth);
                    float intensity = Mathf.Exp(-d * d);

                    block.SetVector("_Wave", new Vector4(waveColor.r, waveColor.g, waveColor.b,
                                                         intensity));
                    walls[i].SetPropertyBlock(block);
                }

                yield return null;
            }

            // Settled, not switched off: the last frame of the band already left the far walls dark,
            // but the near ones were still mid-tail, and a hard zero would blink them.
            for (int i = 0; i < walls.Length; i++)
            {
                if (walls[i] != null)
                {
                    block.SetVector("_Wave", Vector4.zero);
                    walls[i].SetPropertyBlock(block);
                }
            }
        }
    }
}
