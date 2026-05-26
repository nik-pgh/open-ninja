using System.Collections;
using UnityEngine;

namespace OpenNinja
{
    /// <summary>
    /// Spawns cubes from a horizontal line at the bottom of the play area, with
    /// difficulty curves driving the spawn interval and danger-cube probability.
    /// </summary>
    public class CubeSpawner : MonoBehaviour
    {
        [Header("Prefabs")]
        [SerializeField] private Cube greenPrefab;
        [SerializeField] private Cube redPrefab;
        [SerializeField] private Cube blackPrefab;

        [Header("Spawn line")]
        [SerializeField] private Transform spawnLineLeft;
        [SerializeField] private Transform spawnLineRight;

        [Header("Difficulty")]
        [SerializeField] private AnimationCurve spawnIntervalOverTime =
            AnimationCurve.Linear(0f, 0.7f, 60f, 0.3f);
        [SerializeField] private AnimationCurve dangerProbabilityOverTime =
            AnimationCurve.Linear(0f, 0.05f, 60f, 0.15f);
        [SerializeField, Range(0f, 1f)] private float redWeightOfNonDanger = 0.25f;

        [Header("Launch impulse")]
        [SerializeField] private Vector2 launchImpulseRange = new(7f, 11f);
        [SerializeField] private Vector2 sideImpulseRange = new(0f, 2f);

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

        private void SpawnOne(float elapsed)
        {
            if (spawnLineLeft == null || spawnLineRight == null) return;

            float dangerP = Mathf.Clamp01(dangerProbabilityOverTime.Evaluate(elapsed));
            Cube prefab = ChoosePrefab(dangerP);
            if (prefab == null) return;

            float x = Random.Range(spawnLineLeft.position.x, spawnLineRight.position.x);
            Vector3 pos = new Vector3(x, spawnLineLeft.position.y, spawnLineLeft.position.z);
            Quaternion rot = Random.rotation;

            Cube cube = Instantiate(prefab, pos, rot);
            Rigidbody rb = cube.GetComponent<Rigidbody>();
            if (rb != null)
            {
                float side = Random.Range(sideImpulseRange.x, sideImpulseRange.y);
                side *= Random.value < 0.5f ? -1f : 1f;
                float up = Random.Range(launchImpulseRange.x, launchImpulseRange.y);
                rb.AddForce(new Vector3(side, up, 0f), ForceMode.Impulse);
            }
        }

        private Cube ChoosePrefab(float dangerP)
        {
            float roll = Random.value;
            if (roll < dangerP) return blackPrefab;
            float redP = redWeightOfNonDanger * (1f - dangerP);
            if (roll < dangerP + redP) return redPrefab;
            return greenPrefab;
        }
    }
}
