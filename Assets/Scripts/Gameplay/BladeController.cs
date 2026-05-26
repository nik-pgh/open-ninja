using System.Collections.Generic;
using UnityEngine;

namespace OpenNinja
{
    /// <summary>
    /// Translates mouse input into a swipe in world space (on a fixed z plane),
    /// then slices any cube the swept sphere passes through. Drives the BladeTip
    /// transform that the TrailRenderer follows.
    /// </summary>
    public class BladeController : MonoBehaviour
    {
        [Header("Slice physics")]
        [SerializeField] private float bladeRadius = 0.25f;
        [SerializeField] private float minSliceSpeed = 4f;
        [SerializeField] private LayerMask cubeMask = ~0;
        [SerializeField] private float playPlaneZ = 0f;

        [Header("Refs")]
        [SerializeField] private Transform bladeTip;
        [SerializeField] private TrailRenderer bladeTrail;
        [SerializeField] private Camera gameCamera;

        private bool _isSwiping;
        private Vector3 _lastTipWorld;
        private readonly HashSet<Cube> _slicedThisSwipe = new();

        private void Awake()
        {
            if (gameCamera == null) gameCamera = Camera.main;
        }

        private void Update()
        {
            if (gameCamera == null) return;

            Vector3 worldNow = MouseToWorld();
            if (bladeTip != null) bladeTip.position = worldNow;

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
            // Cast a ray from the camera through the cursor and intersect the world
            // play plane (z = playPlaneZ). This handles angled cameras correctly,
            // unlike ScreenToWorldPoint which projects along the camera's local +Z.
            Ray ray = gameCamera.ScreenPointToRay(Input.mousePosition);
            if (Mathf.Approximately(ray.direction.z, 0f))
            {
                // Ray is parallel to the play plane; fall back to the cursor at z=plane.
                return new Vector3(ray.origin.x, ray.origin.y, playPlaneZ);
            }
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
