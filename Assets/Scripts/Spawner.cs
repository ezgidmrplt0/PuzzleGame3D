using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Spawner : MonoBehaviour
{
    public static event Action<FallingPiece> OnPieceSpawned;

    [Header("Spawn Point")]
    [SerializeField] private Transform spawnPoint;

    [Header("Piece Pool (Cars)")]
    [SerializeField] private List<GameObject> availablePieces;

    [Header("Joker")]
    [SerializeField] private GameObject jokerPrefab;
    [Range(0, 100)]
    [SerializeField] private float jokerChance = 15f; // genel şans (ister level config'e de taşırız)

    [Header("Level Configs")]
    [SerializeField] private List<LevelConfig> levels = new List<LevelConfig>();

    [Header("Runtime")]
    [Min(1)]
    [SerializeField] private int currentLevel = 1;

    private FallingPiece currentCenterPiece;
    private LevelConfig currentLevelConfig;

    [Serializable]
    public class LevelConfig
    {
        [Header("Zorluk Ayarları")]
        public int targetMatches = 3;
        public int timeLimitSeconds = 60;

        [Header("Grid Yapısı")]
        [Range(1, 4)] public int slotsPerZone = 1;

        [Header("Level Start Frozen (Design)")]
        [Min(0)] public int startFrozenCount = 0;

        [Header("Ters Input (Opsiyonel)")]
        public bool invertHorizontalSwipe = false;
        public bool invertVerticalSwipe = false;

        [HideInInspector]
        public List<SpawnEntry> spawns = new List<SpawnEntry>();
    }

    [Serializable]
    public class SpawnEntry
    {
        public GameObject prefab;
        public int count = 1;
    }

    public LevelConfig GetLevelConfig(int level)
    {
        if (currentLevelConfig != null && currentLevel == level)
            return currentLevelConfig;

        return CreateLevelConfig(level);
    }

    private LevelConfig CreateLevelConfig(int level)
    {
        LevelConfig cfg = null;

        if (levels != null && levels.Count > 0)
        {
            int idx = level - 1;
            if (idx < levels.Count)
            {
                var preset = levels[idx];
                cfg = new LevelConfig
                {
                    targetMatches = preset.targetMatches,
                    timeLimitSeconds = preset.timeLimitSeconds,
                    slotsPerZone = preset.slotsPerZone,
                    startFrozenCount = preset.startFrozenCount,
                    invertHorizontalSwipe = preset.invertHorizontalSwipe,
                    invertVerticalSwipe = preset.invertVerticalSwipe
                };
            }
        }

        if (cfg == null)
        {
            cfg = new LevelConfig();
            cfg.targetMatches = 3 + (level / 4);
            cfg.timeLimitSeconds = 30;
            cfg.slotsPerZone = 1;
            cfg.startFrozenCount = 0;
        }

        PopulateSpawnsFromPool(cfg);
        return cfg;
    }

    private void PopulateSpawnsFromPool(LevelConfig cfg)
    {
        cfg.spawns = new List<SpawnEntry>();

        if (availablePieces != null && availablePieces.Count > 0)
        {
            // hepsini kullan (istersen typeCount geri ekleriz)
            for (int i = 0; i < availablePieces.Count; i++)
            {
                if (availablePieces[i] != null)
                    cfg.spawns.Add(new SpawnEntry { prefab = availablePieces[i], count = 1 });
            }
        }
        else
        {
            Debug.LogError("[Spawner] ERROR: No pieces in 'availablePieces'!");
        }
    }

    private SpawnEntry PickWeighted(List<SpawnEntry> entries)
    {
        if (entries == null || entries.Count == 0) return null;

        int total = 0;
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i] == null || entries[i].prefab == null) continue;
            total += Mathf.Max(0, entries[i].count);
        }

        if (total <= 0)
        {
            var valid = new List<SpawnEntry>();
            for (int i = 0; i < entries.Count; i++)
                if (entries[i] != null && entries[i].prefab != null) valid.Add(entries[i]);

            if (valid.Count == 0) return null;
            return valid[UnityEngine.Random.Range(0, valid.Count)];
        }

        int roll = UnityEngine.Random.Range(0, total);
        int acc = 0;
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e == null || e.prefab == null) continue;
            acc += Mathf.Max(0, e.count);
            if (roll < acc) return e;
        }

        return null;
    }

    public void StartLevel(int level)
    {
        currentLevel = Mathf.Max(1, level);
        currentCenterPiece = null;

        currentLevelConfig = GetLevelConfig(currentLevel);
        SpawnNextPiece();
    }

    public void SpawnNextPiece()
    {
        StartCoroutine(SpawnSinglePieceRoutine());
    }

    private IEnumerator SpawnSinglePieceRoutine()
    {
        yield return new WaitForSeconds(0.05f);

        if (spawnPoint == null) yield break;
        if (currentCenterPiece != null) yield break;

        var cfg = GetLevelConfig(currentLevel);
        if (cfg == null) yield break;

        // ✅ Joker spawn?
        bool spawnJoker = (jokerPrefab != null) && (UnityEngine.Random.value * 100f < jokerChance);

        GameObject prefab;
        if (spawnJoker)
        {
            prefab = jokerPrefab;
        }
        else
        {
            if (cfg.spawns == null || cfg.spawns.Count == 0) yield break;
            var entry = PickWeighted(cfg.spawns);
            if (entry == null) yield break;
            prefab = entry.prefab;
        }

        if (prefab == null) yield break;

        GameObject go = Instantiate(prefab, spawnPoint.position, prefab.transform.rotation);

        Rigidbody rb = go.GetComponent<Rigidbody>();
        if (!rb) rb = go.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true;

        FallingPiece fp = go.GetComponent<FallingPiece>();
        if (!fp) fp = go.AddComponent<FallingPiece>();

        fp.SetPieceKey(prefab.name);

        // ✅ Joker flag
        fp.SetJoker(spawnJoker);

        currentCenterPiece = fp;
        OnPieceSpawned?.Invoke(fp);
    }

    public void NotifyCenterCleared(FallingPiece piece)
    {
        if (currentCenterPiece == piece)
            currentCenterPiece = null;
    }
}
