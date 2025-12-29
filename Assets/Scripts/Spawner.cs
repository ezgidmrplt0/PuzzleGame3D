using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class Spawner : MonoBehaviour
{
    public static event Action<FallingPiece> OnPieceSpawned;

    [Header("Spawn Point")]
    [SerializeField] private Transform spawnPoint;

    [Header("Piece Pool (Cars)")]
    [SerializeField] private List<GameObject> availablePieces;

    // [Header("Joker")] -> Moved to JokerSpawner

    [Header("Level Configs")]
    [SerializeField] private List<LevelConfig> levels = new List<LevelConfig>();

    [Header("Runtime")]
    [Min(1)]
    [SerializeField] private int currentLevel = 1;

    private FallingPiece currentCenterPiece;
    private LevelConfig currentLevelConfig;

    public bool HasActivePiece() => currentCenterPiece != null;

    public void DestroyCurrentPiece()
    {
        if (currentCenterPiece != null)
        {
            Destroy(currentCenterPiece.gameObject);
            currentCenterPiece = null;
        }
    }

    [Serializable]
    public class LevelConfig
    {
        [Header("Zorluk Ayarları")]
        public int targetMatches = 3;
        public int timeLimitSeconds = 60; // Global Timer (Optional)
        public float carSpawnDuration = 3.0f; // New: Per-car timer

        [Header("Grid Yapısı")]
        [Range(1, 4)] public int slotsPerZone = 1;



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
        // Always generate procedural config to ensure difficulty logic applies
        // If you want to use manual overrides from the list, uncomment the check below.
        /*
        if (currentLevelConfig != null && currentLevel == level)
            return currentLevelConfig;
        */

        return CreateLevelConfig(level);
    }

    private LevelConfig CreateLevelConfig(int level)
    {
        LevelConfig cfg = new LevelConfig();

        // ---------------------------------------------------------
        // DIFFICULTY LOGIC / LEVEL MANTIĞI
        // ---------------------------------------------------------

        // 1. RELIEF LEVEL CHECK (Every 5th level is easy)
        bool isReliefLevel = (level > 1 && level % 5 == 0);

        // 2. TARGET MATCHES
        // Base growth: Linear increase
        int baseTarget = 3 + Mathf.FloorToInt(level * 0.8f);
        if (isReliefLevel) baseTarget = Mathf.Max(3, Mathf.FloorToInt(baseTarget * 0.6f)); // %40 reduction
        cfg.targetMatches = baseTarget;

        // 3. OBJECT VARIETY (activeObjectCount)
        // Level 1-2: 2 types
        // Level 3-9: 3 types
        // Level 10+: 4 types (Max 4 as requested)
        int variety = 2;
        if (level >= 3) variety = 3;
        if (level >= 10) variety = 4;
        
        // Relief level reduces variety slightly for comfort
        if (isReliefLevel && variety > 2) variety--;

        // 4. SPAWN DURATION (Timer per car)
        // Starts at 4.0s, drops to 2.0s minimum.
        // Drops by 0.05s per level.
        float baseTime = 4.0f;
        float timeDrop = (level - 1) * 0.05f;
        float duration = Mathf.Max(2.0f, baseTime - timeDrop);
        
        if (isReliefLevel) duration += 1.5f; // Extra time for relief

        cfg.carSpawnDuration = duration;

        // 5. GRID / SLOTS
        // Increase slots occasionally? For now keep simple or random.
        cfg.slotsPerZone = 1; // Standard 1 slot
        // Maybe at very high levels introduce 2 slots?
        // if (level > 15) cfg.slotsPerZone = 2; 

        // 6. SWIPE INVERSION (Fun/Trick mechanics)
        // Randomly invert after level 7, but not on relief levels
        if (level > 7 && !isReliefLevel && UnityEngine.Random.value < 0.2f)
        {
            if (UnityEngine.Random.value < 0.5f) cfg.invertHorizontalSwipe = true;
            else cfg.invertVerticalSwipe = true;
        }

        cfg.timeLimitSeconds = 60; // Not mainly used
        cfg.timeLimitSeconds = 60; // Not mainly used

        // ---------------------------------------------------------

        // Populate spawns with limited variety
        PopulateSpawnsFromPool(cfg, variety);
        
        return cfg;
    }

    private void PopulateSpawnsFromPool(LevelConfig cfg, int varietyCount)
    {
        cfg.spawns = new List<SpawnEntry>();

        if (availablePieces != null && availablePieces.Count > 0)
        {
            // Create a shuffled temporary list
            List<GameObject> shuffled = new List<GameObject>(availablePieces);
            
            // Fisher-Yates shuffle
            for (int i = shuffled.Count - 1; i > 0; i--)
            {
                int rnd = UnityEngine.Random.Range(0, i + 1);
                var temp = shuffled[i];
                shuffled[i] = shuffled[rnd];
                shuffled[rnd] = temp;
            }

            // Pick first N items based on varietyCount
            int countToPick = Mathf.Clamp(varietyCount, 1, shuffled.Count);

            for (int i = 0; i < countToPick; i++)
            {
                if (shuffled[i] != null)
                    cfg.spawns.Add(new SpawnEntry { prefab = shuffled[i], count = 1 });
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

    [Header("Visual Queue")]
    [SerializeField] private Transform[] queuePoints; // Assign 3 arrows in inspector
    [SerializeField] private float queueMoveDuration = 0.4f;

    // We store the actual instantiated pieces currently waiting in line
    private List<FallingPiece> visualQueue = new List<FallingPiece>();

    public void StartLevel(int level)
    {
        currentLevel = Mathf.Max(1, level);
        currentCenterPiece = null;

        currentLevelConfig = GetLevelConfig(currentLevel);

        // Clear existing visual queue
        foreach (var p in visualQueue)
            if (p) Destroy(p.gameObject);
        visualQueue.Clear();

        // Fill Visual Queue initially
        if (queuePoints != null && queuePoints.Length > 0)
        {
            for (int i = 0; i < queuePoints.Length; i++)
            {
                SpawnAndAddToVisualQueue(i);
            }
        }

        SpawnNextPiece(); // Moves first one to center
    }

    private void SpawnAndAddToVisualQueue(int targetIndex)
    {
        // Data prep
        var cfg = GetLevelConfig(currentLevel);
        if (cfg == null) return;
        
        GameObject prefab = null;
        if (cfg.spawns != null && cfg.spawns.Count > 0)
        {
            var entry = PickWeighted(cfg.spawns);
            if (entry != null) prefab = entry.prefab;
        }

        if (prefab == null) return;

        // Instantiate at spawn point (or far away/offscreen)
        // For simplicity, let's spawn directly at the target Queue Point
        if (targetIndex >= queuePoints.Length) return;
        Transform targetPt = queuePoints[targetIndex];

        GameObject go = Instantiate(prefab, targetPt.position, targetPt.rotation); // Use point rotation

        // Setup Piece
        Rigidbody rb = go.GetComponent<Rigidbody>();
        if (!rb) rb = go.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true;

        FallingPiece fp = go.GetComponent<FallingPiece>();
        if (!fp) fp = go.AddComponent<FallingPiece>();
        
        fp.SetPieceKey(prefab.name);

        visualQueue.Add(fp);
    }

    public void SpawnNextPiece()
    {
        StartCoroutine(SpawnSinglePieceRoutine());
    }

    private IEnumerator SpawnSinglePieceRoutine()
    {
        // wait frame
        yield return null;

        if (spawnPoint == null) yield break;
        if (currentCenterPiece != null) yield break;

        // 1. Take from front of Visual Queue
        if (visualQueue.Count == 0)
        {
             // Fallback if queue is empty (shouldn't happen if points are set)
             // Force spawn one? Or just break.
             // Let's force fill if empty
             if (queuePoints != null && queuePoints.Length > 0) SpawnAndAddToVisualQueue(0);
             if (visualQueue.Count == 0) yield break;
        }

        FallingPiece nextPiece = visualQueue[0];
        visualQueue.RemoveAt(0);

        // 2. Move it to Center
        // We use DOTween (FallingPiece has TweenToSlot, but here we just need Move)
        // Or simpler: manually move. Let's use DOTween if available implicitly via FallingPiece logic or Spawner logic.
        // Assuming FallingPiece has a Move method or we direct transform.
        // We can just Tween it.
        
        // We want it to ANIMATE to the center spawn point.
        // During animation, it is the "currentCenterPiece" technically?
        // Or we set it after arrival. Let's set immediately to block logic.
        currentCenterPiece = nextPiece;
        
        // Animasyon: Queue noktasından Center SpawnPoint'a
        DG.Tweening.Sequence seq = DG.Tweening.DOTween.Sequence();
        seq.Join(nextPiece.transform.DOMove(spawnPoint.position, queueMoveDuration).SetEase(DG.Tweening.Ease.OutQuad));
        seq.Join(nextPiece.transform.DORotateQuaternion(spawnPoint.rotation, queueMoveDuration).SetEase(DG.Tweening.Ease.OutQuad));
        
        seq.OnComplete(() =>
        {
             // Animasyon bitince tam konumunu garantile (opsiyonel)
        });

        OnPieceSpawned?.Invoke(nextPiece);

        // 3. Shift others forward & Refill back
        AdvanceVisualQueue();
    }

    private void AdvanceVisualQueue()
    {
        if (queuePoints == null || visualQueue.Count == 0) 
        {
            // If empty, just spawn new at index 0
            // But we should fill up to length
            int currentCount = visualQueue.Count; // 0
            if (queuePoints != null)
            {
                for(int i=currentCount; i<queuePoints.Length; i++) SpawnAndAddToVisualQueue(i);
            }
            return;
        }

        // Shift existing items: 
        // Index 0 moved to center (already removed from list).
        // New Index 0 was previously at Point 1. Move it to Point 0.
        // New Index k was previously at Point k+1. Move it to Point k.
        
        for (int i = 0; i < visualQueue.Count; i++)
        {
            FallingPiece fp = visualQueue[i];
            if (i < queuePoints.Length)
            {
                Transform targetPt = queuePoints[i];
                fp.transform.DOMove(targetPt.position, queueMoveDuration).SetEase(DG.Tweening.Ease.OutQuad);
                fp.transform.DORotateQuaternion(targetPt.rotation, queueMoveDuration).SetEase(DG.Tweening.Ease.OutQuad);
            }
        }

        // Spawn new at the end
        // visualQueue.Count is currently (Max - 1) (if full before)
        // So we add at index = visualQueue.Count
        if (visualQueue.Count < queuePoints.Length)
        {
            SpawnAndAddToVisualQueue(visualQueue.Count);
            // The newly added one is at index LAST provided by Add method?
            // Wait, SpawnAndAddToVisualQueue(i) instantiates at that point.
            // visualQueue.Add puts it at end.
            // Correct.
        }
    }

    public void NotifyCenterCleared(FallingPiece piece)
    {
        if (currentCenterPiece == piece)
            currentCenterPiece = null;
    }
}
