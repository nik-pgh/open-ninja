using System.Collections.Generic;
using UnityEngine;

namespace OpenNinja
{
    /// <summary>
    /// Translates mouse input into a swipe in world space (on a fixed z plane),
    /// then slices any cube the swept sphere passes through. Drives the BladeTip
    /// transform that the TrailRenderer follows. Exposes ApplySliceDrag so cubes
    /// can "drag" the blade tip after a heavy slice.
    /// </summary>
    public class BladeController : MonoBehaviour
    {
        public static BladeController Instance { get; private set; }

        [Header("Slice physics")]
        [SerializeField] private float bladeRadius = 0.25f;
        [SerializeField] private float minSliceSpeed = 4f;
        [SerializeField] private LayerMask cubeMask = ~0;
        [SerializeField] private float playPlaneZ = 0f;

        [Header("Refs")]
        [SerializeField] private Transform bladeTip;
        [SerializeField] private TrailRenderer bladeTrail;
        [SerializeField] private Camera gameCamera;

        private const float SliceDragMassMin = 0.3f;
        private const float SliceDragMassMax = 3.0f;
        private const float DragFactorMin = 0.1f;
        private const float DragFactorMax = 0.6f;
        private const float DragDurationMin = 0.04f;
        private const float DragDurationMax = 0.12f;

        private bool _isSwiping;
        private Vector3 _lastTipWorld;
        private float _dragUntil;
        private float _dragFactor;
        private readonly HashSet<Cube> _slicedThisSwipe = new();

        private void Awake()
        {
            Instance = this;
            if (gameCamera == null) gameCamera = Camera.main;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void ApplySliceDrag(float mass)
        {
            float t = Mathf.InverseLerp(SliceDragMassMin, SliceDragMassMax, mass);
            _dragFactor = Mathf.Lerp(DragFactorMin, DragFactorMax, t);
            _dragUntil = Time.unscaledTime + Mathf.Lerp(DragDurationMin, DragDurationMax, t);
        }

        private void Update()
        {
            if (gameCamera == null) return;

            Vector3 worldNow = MouseToWorld();
            if (bladeTip != null)
            {
                if (_dragUntil > Time.unscaledTime)
                    bladeTip.position = Vector3.Lerp(bladeTip.position, worldNow, 1f - _dragFactor);
                else
                    bladeTip.position = worldNow;
            }

            if (Input.GetMouseButtonDown(0))
            {
                _isSwiping = true;
                _lastTipWorld = worldNow;
                _slicedThisSwipe.Clear();
                if (bladeTrail != null) bladeTrail.Clear();
            }

            if (Input.GetMouseButtonUp(0))
            {
                _isSwiping = false;
                _slicedThisSwipe.Clear();
            }

            if (_isSwiping)
            {
                if (GameManager.Instance == null || !GameManager.Instance.IsGameOver)
                {
                    TrySlice(_lastTipWorld, worldNow);
                }
                _lastTipWorld = worldNow;
            }
        }

        private Vector3 MouseToWorld()
        {
            Ray ray = gameCamera.ScreenPointToRay(Input.mousePosition);
            if (Mathf.Approximately(ray.direction.z, 0f))
                return new Vector3(ray.origin.x, ray.origin.y, playPlaneZ);
            float t = (playPlaneZ - ray.origin.z) / ray.direction.z;
            Vector3 hit = ray.origin + ray.direction * t;
            hit.z = playPlaneZ;
            return hit;
        }

        private void TrySlice(Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            float distance = delta.magnitude;
            if (distance <= Mathf.Epsilon) return;

            float speed = distance / Time.deltaTime;
            if (speed < minSliceSpeed) return;

            var hits = Physics.SphereCastAll(
                from, bladeRadius, delta.normalized, distance, cubeMask, QueryTriggerInteraction.Ignore);

            foreach (var hit in hits)
            {
                var cube = hit.collider.GetComponent<Cube>() ?? hit.collider.GetComponentInParent<Cube>();
                if (cube == null) continue;
                if (!_slicedThisSwipe.Add(cube)) continue;
                cube.HandleSlice(hit.point);
            }
        }
    }
}
