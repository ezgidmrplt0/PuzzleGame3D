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
            SpawnForLevel(currentLevel);
    }

    public void SpawnForLevel(int level)
    {
        currentLevel = Mathf.Max(1, level);

        if (spawnRoutine != null)
            StopCoroutine(spawnRoutine);

        spawnRoutine = StartCoroutine(SpawnRoutine());
    }

    private IEnumerator SpawnRoutine()
    {
        if (spawnPoint == null)
        {
            Debug.LogError("[Spawner] SpawnPoint atanmadı!");
            yield break;
        }
        if (cubePrefab == null || spherePrefab == null)
        {
            Debug.LogError("[Spawner] cubePrefab ve spherePrefab Inspector'da atanmalı (tip tespiti için).");
            yield break;
        }

        int index = currentLevel - 1;
        if (levels == null || index < 0 || index >= levels.Count)
        {
            Debug.LogError($"[Spawner] Level config yok: Level {currentLevel}");
            yield break;
        }

        List<GameObject> list = new List<GameObject>();
        foreach (var e in levels[index].spawns)
        {
            if (e.prefab == null || e.count <= 0) continue;
            for (int i = 0; i < e.count; i++)
                list.Add(e.prefab);
        }

        if (shuffleOrder)
            Shuffle(list);

        for (int i = 0; i < list.Count; i++)
        {
            if (!TryGetSpawnPosition(out Vector3 pos))
            {
                Debug.LogWarning("[Spawner] Boş spawn noktası bulunamadı (max deneme doldu). Bu spawn atlandı.");
                yield return new WaitForSeconds(spawnInterval);
                continue;
            }

            GameObject prefab = list[i];
            GameObject go = Instantiate(prefab, pos, Quaternion.identity);

            // Rigidbody
            Rigidbody rb = go.GetComponent<Rigidbody>();
            if (!rb) rb = go.AddComponent<Rigidbody>();
            rb.useGravity = useGravity;
            rb.isKinematic = false;
            rb.drag = drag;
            rb.angularDrag = angularDrag;

            // FallingPiece ekle + tip ver + SlotManager'a bildir
            FallingPiece fp = go.GetComponent<FallingPiece>();
            if (!fp) fp = go.AddComponent<FallingPiece>();

            fp.pieceType = (prefab == spherePrefab) ? FallingPiece.Type.Sphere : FallingPiece.Type.Cube;
            OnPieceSpawned?.Invoke(fp);

            // +X yönünde random kuvvet
            float f = UnityEngine.Random.Range(forwardForceMin, forwardForceMax);
            float zSide = UnityEngine.Random.Range(-sideForceZ, sideForceZ);
            float yUp = UnityEngine.Random.Range(0f, upForce);
            rb.AddForce(new Vector3(f, yUp, zSide), ForceMode.Impulse);

            // DestroyZone'a girince sil
            if (destroyZone != null && !go.TryGetComponent<DestroyOnZone>(out _))
            {
                var d = go.AddComponent<DestroyOnZone>();
                d.Init(destroyZone);
            }

            yield return new WaitForSeconds(spawnInterval);
        }
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
