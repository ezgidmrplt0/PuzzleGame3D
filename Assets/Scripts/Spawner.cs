using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Spawner : MonoBehaviour
{
    public static event Action<FallingPiece> OnPieceSpawned;

    [Header("Spawn Point")]
    [SerializeField] private Transform spawnPoint;

    [Header("Piece Pool (Tüm Objeler)")]
    [SerializeField] private List<GameObject> availablePieces; 

    [Header("Level Configs (Difficulty Only)")]
    [Tooltip("Buradan Level 1, Level 2 gibi özel zorluk ayarları yapabilirsiniz. Obje seçimi otomatik olur.")]
    [SerializeField] private List<LevelConfig> levels = new List<LevelConfig>();

    [Header("Runtime")]
    [Min(1)]
    [SerializeField] private int currentLevel = 1;

    [Header("Spawn Timing")]
 
    
    private FallingPiece currentCenterPiece;
    private LevelConfig currentLevelConfig; // CACHED CONFIG

    [Serializable]
    public class LevelConfig
    {
        [Header("Zorluk Ayarları")]
        public int targetMatches = 3;
        public int timeLimitSeconds = 60;
        [Range(0, 100)] public float fakeChance = 20f;
        
        [Header("Çeşitlilik")]
        [Tooltip("Bu levelde kaç farklı obje türü olacak? (Örn: 2 seçerseniz Küp ve Küre gelir)")]
        [Range(2, 6)] public int pieceTypeCount = 2;

        [Header("Grid Yapısı")]
        [Tooltip("Her yönde (Yukarı, Aşağı, Sağ, Sol) kaç slot olacak?")]
        [Range(1, 4)] public int slotsPerZone = 1;

        // Auto-filled at runtime from pool. User doesn't need to touch this.
        [HideInInspector] 
        public List<SpawnEntry> spawns = new List<SpawnEntry>();
    }

    [Serializable]
    public class SpawnEntry
    {
        public GameObject prefab;
        public int count = 1;
    }

    // ...

    // Public API: Get existing config or create new one (Ensures consistency)
    public LevelConfig GetLevelConfig(int level)
    {
        // If we already generated this level's config, return it! 
        // (Prevents re-shuffling pieces mid-game)
        if (currentLevelConfig != null && currentLevel == level)
        {
            return currentLevelConfig;
        }

        // otherwise generate new
        return CreateLevelConfig(level);
    }

    // Interval Method: Generates new config from scratch
    private LevelConfig CreateLevelConfig(int level)
    {
        LevelConfig cfg = null;

        // 1. Try to get Hand-Crafted Difficulty
        if (levels != null && levels.Count > 0)
        {
            int idx = level - 1;
            if (idx < levels.Count)
            {
                // Clone settings from inspector
                var preset = levels[idx];
                cfg = new LevelConfig();
                cfg.targetMatches = preset.targetMatches;
                cfg.timeLimitSeconds = preset.timeLimitSeconds;
                cfg.fakeChance = preset.fakeChance;
                cfg.pieceTypeCount = preset.pieceTypeCount; 
                cfg.slotsPerZone = preset.slotsPerZone; // Copy slots count
            }
        }

        // 2. If no config found (Infinite Mode), generate purely procedural difficulty
        if (cfg == null)
        {
            cfg = GenerateProceduralDifficulty(level);

            // FIX: Override Slots Per Zone to match the LAST hand-crafted level
            // This prevents the game from suddenly switching to 1 slot if the user designed a 3-slot game.
            if (levels != null && levels.Count > 0)
            {
                var lastPreset = levels[levels.Count - 1];
                cfg.slotsPerZone = lastPreset.slotsPerZone;
                // We could also copy other things here if desired, but user specifically asked about the "type" (structure).
            }
        }

        // 3. AUTO-POPULATE PIECES
        PopulateSpawnsFromPool(cfg, level);

        return cfg;
    }

    private LevelConfig GenerateProceduralDifficulty(int level)
    {
        LevelConfig cfg = new LevelConfig();
        
        float roll = UnityEngine.Random.value; 

        // Note: slotsPerZone is now overridden above, so these assignments below 
        // form a "default fallback" if NO levels are defined in inspector.

        if (roll < 0.50f) 
        {
            // EASY
            cfg.fakeChance = UnityEngine.Random.Range(5f, 15f);
            cfg.targetMatches = 3 + (level / 5); 
            cfg.timeLimitSeconds = 45 + (level / 2);
            cfg.pieceTypeCount = 2;
            cfg.slotsPerZone = 1; 
        }
        else if (roll < 0.80f) 
        {
            // MEDIUM
            cfg.fakeChance = UnityEngine.Random.Range(20f, 35f);
            cfg.targetMatches = 5 + (level / 4);
            cfg.timeLimitSeconds = 40 + (level / 3);
            cfg.pieceTypeCount = 3;
            cfg.slotsPerZone = 2; 
        }
        else if (roll < 0.95f) 
        {
            // HARD
            cfg.fakeChance = UnityEngine.Random.Range(40f, 60f);
            cfg.targetMatches = 8 + (level / 3);
            cfg.timeLimitSeconds = 35 + (level / 3);
            cfg.pieceTypeCount = 4;
            cfg.slotsPerZone = 2; 
        }
        else 
        {
            // CHAOS
            cfg.fakeChance = 80f; 
            cfg.targetMatches = 10 + (level / 2);
            cfg.timeLimitSeconds = 30 + (level / 4);
            cfg.pieceTypeCount = 5;
            cfg.slotsPerZone = 3; 
        }

        return cfg;
    }

    private void PopulateSpawnsFromPool(LevelConfig cfg, int level)
    {
        cfg.spawns = new List<SpawnEntry>();
        
        if (availablePieces != null && availablePieces.Count > 0)
        {
            int totalAvailable = availablePieces.Count;
            // Use the explicit count set in Inspector (or generated procedurally)
            int typeCountToUse = cfg.pieceTypeCount;

            typeCountToUse = Mathf.Clamp(typeCountToUse, 2, totalAvailable);

            // Shuffle pool
            List<GameObject> pool = new List<GameObject>(availablePieces);
            Shuffle(pool);

            // Pick N items
            for (int i = 0; i < typeCountToUse; i++)
            {
                if (pool[i] != null)
                {
                    cfg.spawns.Add(new SpawnEntry { prefab = pool[i], count = 100 });
                }
            }
        }
        else
        {
            Debug.LogError("[Spawner] ERROR: No pieces in 'Available Pieces' pool!");
        }
    }

    public void StartLevel(int level)
    {
        currentLevel = Mathf.Max(1, level);
        currentCenterPiece = null;
        
        // Init config (if not already done by LevelManager)
        currentLevelConfig = GetLevelConfig(currentLevel);
        
        SpawnNextPiece();
    }

    // public LevelConfig GetCurrentConfig() => currentLevelConfig; // Removed, use GetLevelConfig

    public void SpawnNextPiece()
    {
        StartCoroutine(SpawnSinglePieceRoutine());
    }

    private IEnumerator SpawnSinglePieceRoutine()
    {
        yield return new WaitForSeconds(0.2f);

        if (spawnPoint == null) yield break;
        if (currentCenterPiece != null) yield break;

        // Use standard accessor which is now cached
        var cfg = GetLevelConfig(currentLevel);
        
        if (cfg == null || cfg.spawns.Count == 0) yield break;

        var randomEntry = cfg.spawns[UnityEngine.Random.Range(0, cfg.spawns.Count)];
        GameObject prefab = randomEntry.prefab;

        if (prefab == null) yield break;

        GameObject go = Instantiate(prefab, spawnPoint.position, Quaternion.Euler(90, 0, 90));

        Rigidbody rb = go.GetComponent<Rigidbody>();
        if (!rb) rb = go.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true;

        FallingPiece fp = go.GetComponent<FallingPiece>();
        if (!fp) fp = go.AddComponent<FallingPiece>();
        
        bool isFake = UnityEngine.Random.value * 100f < cfg.fakeChance;
        fp.SetFake(isFake);

        currentCenterPiece = fp;
        OnPieceSpawned?.Invoke(fp);
    }

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
