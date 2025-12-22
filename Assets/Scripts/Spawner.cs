using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Spawner : MonoBehaviour
{
    public static event Action<FallingPiece> OnPieceSpawned;

    [Header("Spawn Point")]
    [SerializeField] private Transform spawnPoint;

    [Header("Piece Pool (Tüm Objeler) - ARTIK 1 PREFAB YETERLİ")]
    [Tooltip("Bu listede artık tek prefab kullanabilirsiniz. (Tek obje)")]
    [SerializeField] private List<GameObject> availablePieces;

    [Header("Level Configs (Difficulty Only)")]
    [Tooltip("Buradan Level 1, Level 2 gibi özel zorluk ayarları yapabilirsiniz. Obje seçimi otomatik olur.")]
    [SerializeField] private List<LevelConfig> levels = new List<LevelConfig>();

    [Header("Color Palette (Normal Piece Colors)")]
    [Tooltip("Normal parçanın alabileceği renk paleti. pieceTypeCount kadarını kullanır (örn 3 => ilk 3 renk).")]
    [SerializeField]
    private List<Color> colorPalette = new List<Color>()
    {
        Color.red,
        Color.green,
        Color.blue,
        Color.yellow,
        new Color(1f, 0.2f, 1f),
        Color.cyan
    };

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
        [Range(0, 100)] public float fakeChance = 20f;

        [Header("Çeşitlilik (RENK SAYISI)")]
        [Range(1, 6)] public int pieceTypeCount = 3;

        [Header("Grid Yapısı")]
        [Range(1, 4)] public int slotsPerZone = 1;

        [Header("Ters Input (Opsiyonel)")]
        [Tooltip("Sağa swipe atınca sola yerleştirir, sola swipe atınca sağa yerleştirir.")]
        public bool invertHorizontalSwipe = false;

        [Tooltip("Yukarı swipe atınca aşağı yerleştirir, aşağı swipe atınca yukarı yerleştirir.")]
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

        // 1) Preset
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
                    fakeChance = preset.fakeChance,
                    pieceTypeCount = preset.pieceTypeCount,
                    slotsPerZone = preset.slotsPerZone,

                    invertHorizontalSwipe = preset.invertHorizontalSwipe,
                    invertVerticalSwipe = preset.invertVerticalSwipe
                };
            }
        }

        // 2) Procedural
        if (cfg == null)
        {
            cfg = GenerateProceduralDifficulty(level);

            if (levels != null && levels.Count > 0)
            {
                var lastPreset = levels[levels.Count - 1];
                cfg.slotsPerZone = lastPreset.slotsPerZone;
            }
        }

        // 3) Spawn pool
        PopulateSpawnsFromPool(cfg, level);

        return cfg;
    }

    private LevelConfig GenerateProceduralDifficulty(int level)
    {
        LevelConfig cfg = new LevelConfig();
        float roll = UnityEngine.Random.value;

        if (roll < 0.50f)
        {
            cfg.fakeChance = UnityEngine.Random.Range(5f, 15f);
            cfg.targetMatches = 3 + (level / 5);
            cfg.timeLimitSeconds = 45 + (level / 2);
            cfg.pieceTypeCount = 3;
            cfg.slotsPerZone = 1;
        }
        else if (roll < 0.80f)
        {
            cfg.fakeChance = UnityEngine.Random.Range(20f, 35f);
            cfg.targetMatches = 5 + (level / 4);
            cfg.timeLimitSeconds = 40 + (level / 3);
            cfg.pieceTypeCount = 4;
            cfg.slotsPerZone = 2;
        }
        else if (roll < 0.95f)
        {
            cfg.fakeChance = UnityEngine.Random.Range(40f, 60f);
            cfg.targetMatches = 8 + (level / 3);
            cfg.timeLimitSeconds = 35 + (level / 3);
            cfg.pieceTypeCount = 5;
            cfg.slotsPerZone = 2;
        }
        else
        {
            cfg.fakeChance = 80f;
            cfg.targetMatches = 10 + (level / 2);
            cfg.timeLimitSeconds = 30 + (level / 4);
            cfg.pieceTypeCount = 6;
            cfg.slotsPerZone = 3;
        }

        // İstersen procedural ters inputu kapatabilirsin:
        if (level >= 6 && UnityEngine.Random.value < 0.15f)
            cfg.invertHorizontalSwipe = true;

        if (level >= 10 && UnityEngine.Random.value < 0.10f)
            cfg.invertVerticalSwipe = true;

        return cfg;
    }

    private void PopulateSpawnsFromPool(LevelConfig cfg, int level)
    {
        cfg.spawns = new List<SpawnEntry>();

        if (availablePieces != null && availablePieces.Count > 0)
        {
            int totalAvailable = availablePieces.Count;
            int typeCountToUse = 1; // tek prefab
            typeCountToUse = Mathf.Clamp(typeCountToUse, 1, totalAvailable);

            List<GameObject> pool = new List<GameObject>(availablePieces);
            Shuffle(pool);

            for (int i = 0; i < typeCountToUse; i++)
            {
                if (pool[i] != null)
                    cfg.spawns.Add(new SpawnEntry { prefab = pool[i], count = 100 });
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

        currentLevelConfig = GetLevelConfig(currentLevel);
        SpawnNextPiece();
    }

    public void SpawnNextPiece()
    {
        StartCoroutine(SpawnSinglePieceRoutine());
    }

    private IEnumerator SpawnSinglePieceRoutine()
    {
        yield return new WaitForSeconds(0.2f);

        if (spawnPoint == null) yield break;
        if (currentCenterPiece != null) yield break;

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

        // Renk ata (normal)
        int paletteCount = (colorPalette != null) ? colorPalette.Count : 0;
        int colorCountToUse = Mathf.Clamp(cfg.pieceTypeCount, 1, Mathf.Max(1, paletteCount));

        Color chosen = Color.white;
        if (paletteCount > 0)
        {
            int idx = UnityEngine.Random.Range(0, colorCountToUse);
            chosen = colorPalette[idx];
        }
        fp.SetNormalColor(chosen);

        // Fake
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
