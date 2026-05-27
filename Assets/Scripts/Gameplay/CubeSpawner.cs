using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OpenNinja
{
    /// <summary>
    /// Spawns cubes from a horizontal line at the bottom of the play area.
    /// The cube material is chosen by a weighted roll over a configured list,
    /// where each entry's weight is an AnimationCurve evaluated at elapsed
    /// run time. Each material can also override the launch impulse range.
    /// </summary>
    public class CubeSpawner : MonoBehaviour
    {
        [Serializable]
        public struct MaterialEntry
        {
            public CubeMaterial material;
            public AnimationCurve weightOverTime;
        }

        [Header("Prefab")]
        [SerializeField] private Cube cubePrefab;

        [Header("Spawn line")]
        [SerializeField] private Transform spawnLineLeft;
        [SerializeField] private Transform spawnLineRight;

        [Header("Difficulty")]
        [SerializeField] private AnimationCurve spawnIntervalOverTime =
            AnimationCurve.Linear(0f, 0.7f, 60f, 0.3f);
        [SerializeField] private List<MaterialEntry> entries = new();

        [Header("Launch impulse (defaults; per-material override wins if set)")]
        [SerializeField] private Vector2 launchImpulseRange = new(5f, 14f);
        [SerializeField] private Vector2 sideImpulseRange = new(0f, 4f);
        [SerializeField] private float maxUpwardVelocity = 25f;

        private float _runStartTime;

        private void OnEnable()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnGameOver += HandleGameOver;
        }

        private void OnDisable()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnGameOver -= HandleGameOver;
        }

        private void Start()
        {
            _runStartTime = Time.time;
            StartCoroutine(SpawnLoop());
        }

        private void HandleGameOver(int _) { /* loop self-pauses via IsGameOver guard */ }

        public void NotifyRunRestarted()
        {
            _runStartTime = Time.time;
        }

        private IEnumerator SpawnLoop()
        {
            while (true)
            {
                if (GameManager.Instance != null && GameManager.Instance.IsGameOver)
                {
                    yield return new WaitForSeconds(0.25f);
                    continue;
                }

                float elapsed = Time.time - _runStartTime;
                float interval = Mathf.Max(0.05f, spawnIntervalOverTime.Evaluate(elapsed));
                yield return new WaitForSeconds(interval);

                if (GameManager.Instance != null && GameManager.Instance.IsGameOver) continue;
                SpawnOne(elapsed);
            }
        }

        /// <summary>Picks a material by weight at the current elapsed time. Public for tests.</summary>
        public CubeMaterial PickMaterial(float elapsed)
        {
            if (entries == null || entries.Count == 0) return null;

            float total = 0f;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].material == null) continue;
                float w = Mathf.Max(0f, entries[i].weightOverTime?.Evaluate(elapsed) ?? 0f);
                total += w;
            }
            if (total <= 0f) return entries[0].material;

            float roll = UnityEngine.Random.value * total;
            float running = 0f;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].material == null) continue;
                float w = Mathf.Max(0f, entries[i].weightOverTime?.Evaluate(elapsed) ?? 0f);
                running += w;
                if (roll <= running) return entries[i].material;
            }
            return entries[entries.Count - 1].material;
        }

        private void SpawnOne(float elapsed)
        {
            if (cubePrefab == null || spawnLineLeft == null || spawnLineRight == null) return;
            CubeMaterial material = PickMaterial(elapsed);
            if (material == null) return;

            float x = UnityEngine.Random.Range(spawnLineLeft.position.x, spawnLineRight.position.x);
            Vector3 pos = new Vector3(x, spawnLineLeft.position.y, spawnLineLeft.position.z);
            Quaternion rot = UnityEngine.Random.rotation;

            Cube cube = Instantiate(cubePrefab, pos, rot);
            cube.Initialize(material);

            Rigidbody rb = cube.GetComponent<Rigidbody>();
            if (rb == null) return;

            Vector2 upRange = material.HasLaunchOverride ? material.launchImpulseOverride : launchImpulseRange;
            float up = UnityEngine.Random.Range(upRange.x, upRange.y);
            float side = UnityEngine.Random.Range(sideImpulseRange.x, sideImpulseRange.y);
            side *= UnityEngine.Random.value < 0.5f ? -1f : 1f;
            rb.AddForce(new Vector3(side, up, 0f), ForceMode.Impulse);

            // Cap upward velocity so lightest cubes don't escape the play area instantly.
            if (rb.linearVelocity.y > maxUpwardVelocity)
                rb.linearVelocity = new Vector3(rb.linearVelocity.x, maxUpwardVelocity, rb.linearVelocity.z);
        }

        /// <summary>EditMode tests use this to inject a synthetic entries list.</summary>
        public void SetEntriesForTest(List<MaterialEntry> testEntries)
        {
            entries = testEntries;
        }
    }
}
