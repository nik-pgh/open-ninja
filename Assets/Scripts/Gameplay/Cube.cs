using UnityEngine;

namespace OpenNinja
{
    /// <summary>
    /// Per-cube router. Holds a CubeMaterial reference at runtime; applies its
    /// physics/visual settings on Initialize. Routes slice and fall-off events
    /// into GameManager. On wall collision plays bounce SFX and applies the
    /// material's bounciness multiplier to the rigidbody velocity.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public class Cube : MonoBehaviour
    {
        [SerializeField] private CubeMaterial material;
        [SerializeField] private ParticleSystem burstPrefab;

        private const string WallTag = "Wall";
        private const float MinBounceAudioInterval = 0.05f;

        private Rigidbody _rb;
        private MeshRenderer _renderer;
        private bool _consumed;
        private float _lastAudioTime;

        public CubeMaterial Material => material;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _renderer = GetComponentInChildren<MeshRenderer>();
        }

        /// <summary>Configure the cube before adding force. Idempotent.</summary>
        public void Initialize(CubeMaterial mat)
        {
            material = mat;
            if (material == null) return;

            if (_rb == null) _rb = GetComponent<Rigidbody>();
            if (_renderer == null) _renderer = GetComponentInChildren<MeshRenderer>();

            _rb.mass = material.mass;
            _rb.drag = 0.05f;
            _rb.angularDrag = 0.1f;
            transform.localScale = Vector3.one * material.displayScale;

            if (_renderer != null && material.renderMaterial != null)
                _renderer.sharedMaterial = material.renderMaterial;

            gameObject.layer = LayerMask.NameToLayer("Cube");
            _consumed = false;
            _lastAudioTime = -1f;
        }

        public void HandleSlice(Vector3 slicePoint)
        {
            if (_consumed) return;
            _consumed = true;

            SpawnBurst(slicePoint);

            var gm = GameManager.Instance;
            if (gm != null && material != null)
            {
                if (material.role == CubeRole.Danger) gm.RegisterDangerClick(slicePoint);
                else                                  gm.RegisterHit(material.basePoints, slicePoint);
            }

            // Sense-of-mass feedback. Both helpers no-op when the runtime
            // pieces (singletons, scene) aren't present (e.g. in unit tests).
            if (material != null)
            {
                HitStopController.Apply(material.mass);
                BladeController.Instance?.ApplySliceDrag(material.mass);
            }

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
            if (gm != null && material != null && material.role != CubeRole.Danger)
                gm.RegisterMiss();

            if (Application.isPlaying) Destroy(gameObject);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_consumed) return;
            if (material == null) return;
            if (!collision.gameObject.CompareTag(WallTag)) return;

            // Rate-limit so two segment hits in a single physics step don't double-play.
            float now = Time.time;
            if (now - _lastAudioTime >= MinBounceAudioInterval)
            {
                _lastAudioTime = now;
                if (material.impactClip != null)
                {
                    Vector3 contactPoint = collision.GetContact(0).point;
                    float pitch = material.audioPitchAtMassOne *
                                  Mathf.Lerp(1.6f, 0.5f,
                                      Mathf.InverseLerp(0.3f, 3f, material.mass));
                    AudioOneShot.Play(material.impactClip, contactPoint, pitch, material.audioVolume);
                }
            }

            if (!Mathf.Approximately(material.bouncinessMultiplier, 1f) && _rb != null)
            {
                _rb.linearVelocity *= material.bouncinessMultiplier;
                _rb.angularVelocity *= material.bouncinessMultiplier;
            }
        }

        private void SpawnBurst(Vector3 worldPos)
        {
            if (burstPrefab == null) return;
            var burst = Instantiate(burstPrefab, worldPos, Quaternion.identity);
            var main = burst.main;
            if (material != null) main.startColor = material.burstTint;
            burst.Play();
            Destroy(burst.gameObject, main.duration + main.startLifetime.constantMax);
        }
    }
}
