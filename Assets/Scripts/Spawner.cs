using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Spawner : MonoBehaviour
{
    public static event Action<FallingPiece> OnPieceSpawned;
    // public static event Action OnBagEmpty; // Removed

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
    [SerializeField] private bool spawnOnStart = false; 
    
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
        // 1. Try to get from Hand-Crafted List
        if (levels != null && levels.Count > 0)
        {
            int idx = level - 1;
            if (idx < levels.Count) return levels[idx];
        }

        // 2. Fallback: Procedural Generation (Infinite)
        return GenerateProceduralConfig(level);
    }

    private LevelConfig GenerateProceduralConfig(int level)
    {
        LevelConfig cfg = new LevelConfig();
        
        // --- DIFFICULTY ROULETTE ---
        float roll = UnityEngine.Random.value; // 0.0 to 1.0

        if (roll < 0.50f) // 50% EASY
        {
            cfg.fakeChance = UnityEngine.Random.Range(5f, 15f);
            cfg.targetMatches = 3 + (level / 5); // Slowly incresing
            Debug.Log($"[Spawner] Generated EASY Level {level}");
        }
        else if (roll < 0.80f) // 30% MEDIUM
        {
            cfg.fakeChance = UnityEngine.Random.Range(20f, 35f);
            cfg.targetMatches = 5 + (level / 4);
            Debug.Log($"[Spawner] Generated MEDIUM Level {level}");
        }
        else if (roll < 0.95f) // 15% HARD
        {
            cfg.fakeChance = UnityEngine.Random.Range(40f, 60f);
            cfg.targetMatches = 8 + (level / 3);
            Debug.Log($"[Spawner] Generated HARD Level {level}");
        }
        else // 5% EXPERT
        {
            cfg.fakeChance = 80f; // Chaos!
            cfg.targetMatches = 10 + (level / 2);
            Debug.Log($"[Spawner] Generated EXPERT Level {level}");
        }

        // Always allow all types for procedural (or unlock gradually)
        cfg.spawns = new List<SpawnEntry>
        {
            new SpawnEntry { prefab = cubePrefab, count = 100 }, // Dummy infinite counts
            new SpawnEntry { prefab = spherePrefab, count = 100 }
        };

        return cfg;
    }

    public void StartLevel(int level)
    {
        currentLevel = Mathf.Max(1, level);
        currentCenterPiece = null;
        
        // No bag building needed anymore. 
        // We just use the config to know WHICH pieces are allowed.
        
        SpawnNextPiece();
    }

    // Removed BuildBagForLevel

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

        var cfg = GetLevelConfig(currentLevel);
        if (cfg == null || cfg.spawns.Count == 0) yield break;

        // INFINITE SPAWN LOGIC:
        // Pick a random entry from the allowed list
        var randomEntry = cfg.spawns[UnityEngine.Random.Range(0, cfg.spawns.Count)];
        GameObject prefab = randomEntry.prefab;

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
