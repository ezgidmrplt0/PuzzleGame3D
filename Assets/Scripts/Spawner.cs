using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Spawner : MonoBehaviour
{
    public static event Action<FallingPiece> OnPieceSpawned;
    public static event Action OnBagEmpty;

    [Header("Piece Prefabs (tip tespiti için şart)")]
    [SerializeField] private GameObject cubePrefab;
    [SerializeField] private GameObject spherePrefab;

    [Header("Spawn Point")]
    [SerializeField] private Transform spawnPoint;

    [Header("Level Configs (0 = Level 1)")]
    [SerializeField] private List<LevelConfig> levels = new List<LevelConfig>();

    [Header("Runtime")]
    [Min(1)]
    [SerializeField] private int currentLevel = 1;

    [Header("Spawn Timing")]
    [SerializeField] private bool spawnOnStart = false;   // ✅ LevelManager varken FALSE
    [SerializeField] private bool shuffleOrder = true;

    // ===== Level Bag Runtime =====
    private readonly List<GameObject> bag = new List<GameObject>();
    private int bagIndex = 0;

    // ===== Center Lock (üst üste spawn fix) =====
    private FallingPiece currentCenterPiece;

    [Serializable]
    public class LevelConfig
    {
        [Header("Goal")]
        public int targetMatches = 3;

        [Header("Limits")]
        public int timeLimitSeconds = 45;

        [Header("Difficulty")]
        [Range(0, 100)] public float fakeChance = 20f;

        [Header("Bag Content")]
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
            StartLevel(1);
    }

    public LevelConfig GetLevelConfig(int level)
    {
        if (levels == null || levels.Count == 0) return null;
        int idx = Mathf.Clamp(level - 1, 0, levels.Count - 1);
        return levels[idx];
    }

    public void StartLevel(int level)
    {
        currentLevel = Mathf.Max(1, level);

        // level başında merkez boş
        currentCenterPiece = null;

        BuildBagForLevel(currentLevel);
        SpawnNextPiece();
    }

    private void BuildBagForLevel(int level)
    {
        bag.Clear();
        bagIndex = 0;

        var cfg = GetLevelConfig(level);
        if (cfg == null || cfg.spawns == null) return;

        foreach (var entry in cfg.spawns)
        {
            if (entry == null || entry.prefab == null) continue;
            int c = Mathf.Max(0, entry.count);
            for (int i = 0; i < c; i++)
                bag.Add(entry.prefab);
        }

        if (shuffleOrder)
            Shuffle(bag);
    }

    public void SpawnNextPiece()
    {
        StartCoroutine(SpawnSinglePieceRoutine());
    }

    private IEnumerator SpawnSinglePieceRoutine()
    {
        yield return new WaitForSeconds(0.2f);

        if (spawnPoint == null) yield break;

        // ✅ center doluysa spawn etme
        if (currentCenterPiece != null)
            yield break;

        // bag bitti
        if (bagIndex >= bag.Count)
        {
            OnBagEmpty?.Invoke();
            yield break;
        }

        var cfg = GetLevelConfig(currentLevel);
        if (cfg == null) yield break;

        GameObject prefab = bag[bagIndex];
        bagIndex++;

        GameObject go = Instantiate(prefab, spawnPoint.position, Quaternion.identity);

        Rigidbody rb = go.GetComponent<Rigidbody>();
        if (!rb) rb = go.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true;

        FallingPiece fp = go.GetComponent<FallingPiece>();
        if (!fp) fp = go.AddComponent<FallingPiece>();
        fp.pieceType = (prefab == spherePrefab) ? FallingPiece.Type.Sphere : FallingPiece.Type.Cube;

        bool isFake = UnityEngine.Random.value * 100f < cfg.fakeChance;
        fp.SetFake(isFake);

        // ✅ center lock set
        currentCenterPiece = fp;

        OnPieceSpawned?.Invoke(fp);
    }

    // ✅ SlotManager, merkez boşaldığında çağırır
    public void NotifyCenterCleared(FallingPiece piece)
    {
        if (currentCenterPiece == piece)
            currentCenterPiece = null;
    }

    private void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
