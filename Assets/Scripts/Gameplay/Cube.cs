using UnityEngine;

namespace OpenNinja
{
    /// <summary>
    /// Attached to every cube prefab. Holds type/visual data and routes slice / fall-off
    /// events to the GameManager. Does no scoring math itself.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public class Cube : MonoBehaviour
    {
        [SerializeField] private CubeType type;
        [SerializeField] private Color burstTint = Color.white;
        [SerializeField] private ParticleSystem burstPrefab;

        private bool _consumed;

        public CubeType Type => type;

        // Test seam — set type without an Inspector field assignment.
        public void ConfigureForTests(CubeType cubeType)
        {
            type = cubeType;
        }

        public void HandleSlice(Vector3 slicePoint)
        {
            if (_consumed) return;
            _consumed = true;

            SpawnBurst(slicePoint);

            var gm = GameManager.Instance;
            if (gm == null) return; // not in a real scene (e.g. unit test) — fine

            if (type == CubeType.Black) gm.RegisterDangerClick(slicePoint);
            else                       gm.RegisterHit(type, slicePoint);

            if (Application.isPlaying) Destroy(gameObject);
        }

        public void HandleFellOff()
        {
            if (_consumed)
            {
                if (Application.isPlaying) Destroy(gameObject);
                return;
            }
            _consumed = true;

            var gm = GameManager.Instance;
            if (gm != null && type != CubeType.Black) gm.RegisterMiss();

            if (Application.isPlaying) Destroy(gameObject);
        }

        private void SpawnBurst(Vector3 worldPos)
        {
            if (burstPrefab == null) return;
            var burst = Instantiate(burstPrefab, worldPos, Quaternion.identity);
            var main = burst.main;
            main.startColor = burstTint;
            burst.Play();
            Destroy(burst.gameObject, main.duration + main.startLifetime.constantMax);
        }
    }
}
