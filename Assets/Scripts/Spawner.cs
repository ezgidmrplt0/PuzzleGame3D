using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Spawner : MonoBehaviour
{
    // ✅ SlotManager bunu dinler (spawn edilen gerçek objeyi verir)
    public static event Action<FallingPiece> OnPieceSpawned;

    [Header("Piece Prefabs (tip tespiti için şart)")]
    [SerializeField] private GameObject cubePrefab;
    [SerializeField] private GameObject spherePrefab;

    [Header("Spawn Point")]
    [SerializeField] private Transform spawnPoint;

    [Header("Destroy Zone (Is Trigger = true)")]
    [SerializeField] private Collider destroyZone;

    [Header("Level Configs (0 = Level 1)")]
    [SerializeField] private List<LevelConfig> levels = new List<LevelConfig>();

    [Header("Runtime")]
    [Min(1)]
    [SerializeField] private int currentLevel = 1;

    [Header("Spawn Timing")]
    [SerializeField] private bool spawnOnStart = true;
    [SerializeField] private float spawnInterval = 0.35f;
    [SerializeField] private bool shuffleOrder = true;

    [Header("Random Spawn Offset")]
    [SerializeField] private float randomX = 0.2f;
    [SerializeField] private float randomY = 0.2f;
    [SerializeField] private float randomZ = 0.6f;

    [Header("No Overlap Settings")]
    [SerializeField] private float noOverlapRadius = 0.6f;
    [SerializeField] private int maxSpawnTries = 25;
    [SerializeField] private LayerMask overlapMask = ~0;

    [Header("Push Force (+X Direction)")]
    [SerializeField] private float forwardForceMin = 6f;
    [SerializeField] private float forwardForceMax = 10f;
    [SerializeField] private float sideForceZ = 1.5f;
    [SerializeField] private float upForce = 0f;

    [Header("Rigidbody Defaults")]
    [SerializeField] private bool useGravity = true;
    [SerializeField] private float drag = 0f;
    [SerializeField] private float angularDrag = 0.05f;

    private Coroutine spawnRoutine;

    [Serializable]
    public class LevelConfig
    {
        public List<SpawnEntry> spawns = new List<SpawnEntry>();
    }

    [Serializable]
    public class SpawnEntry
    {
        public GameObject prefab;
        [Min(0)] public int count = 1;
    }

    private void Start()
    {
        if (spawnOnStart)
            SpawnForLevel(1);
    }

    public void SpawnForLevel(int level)
    {
        currentLevel = Mathf.Max(1, level);
        SpawnNextPiece(); // Start with one piece
    }

    [Header("Difficulty")]
    [Range(0, 100)]
    [SerializeField] private float fakeChance = 20f; // Percent

    // Called externally (e.g. by SlotManager) when ready for next
    public void SpawnNextPiece()
    {
        StartCoroutine(SpawnSinglePieceRoutine());
    }

    private IEnumerator SpawnSinglePieceRoutine()
    {
        // Optional delay before new piece appears
        yield return new WaitForSeconds(0.2f);

        if (spawnPoint == null) yield break;
        if (levels == null || levels.Count == 0) yield break;

        // Simple random selection for now (or sequential from level config)
        // For prototype: Just pick random prefab from level 1
        var levelConfig = levels[0]; 
        var randomSpawn = levelConfig.spawns[UnityEngine.Random.Range(0, levelConfig.spawns.Count)];
        GameObject prefab = randomSpawn.prefab;

        GameObject go = Instantiate(prefab, spawnPoint.position, Quaternion.identity);

        // Rigidbody Setup: Suspend in air
        Rigidbody rb = go.GetComponent<Rigidbody>();
        if (!rb) rb = go.AddComponent<Rigidbody>();
        rb.useGravity = false; // Don't fall
        rb.isKinematic = true; // Don't move by physics

        // FallingPiece Setup
        FallingPiece fp = go.GetComponent<FallingPiece>();
        if (!fp) fp = go.AddComponent<FallingPiece>();
        fp.pieceType = (prefab == spherePrefab) ? FallingPiece.Type.Sphere : FallingPiece.Type.Cube;

        // FAKE LOGIC
        bool isFake = UnityEngine.Random.value * 100f < fakeChance;
        fp.SetFake(isFake);

        // Notify System
        OnPieceSpawned?.Invoke(fp);
    }

    private bool TryGetSpawnPosition(out Vector3 pos)
    {
        for (int t = 0; t < maxSpawnTries; t++)
        {
            Vector3 p = spawnPoint.position + new Vector3(
                UnityEngine.Random.Range(-randomX, randomX),
                UnityEngine.Random.Range(-randomY, randomY),
                UnityEngine.Random.Range(-randomZ, randomZ)
            );

            bool blocked = Physics.CheckSphere(p, noOverlapRadius, overlapMask, QueryTriggerInteraction.Ignore);
            if (!blocked)
            {
                pos = p;
                return true;
            }
        }

        pos = default;
        return false;
    }

    private void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private class DestroyOnZone : MonoBehaviour
    {
        private Collider zone;
        public void Init(Collider z) => zone = z;

        private void OnTriggerEnter(Collider other)
        {
            if (zone != null && other == zone)
                Destroy(gameObject);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!spawnPoint) return;
        Gizmos.DrawWireSphere(spawnPoint.position, noOverlapRadius);
    }
}
