using System;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using TMPro;

public enum Direction
{
    None,
    Up,
    Down,
    Left,
    Right
}

public class SlotManager : MonoBehaviour
{
    public static SlotManager Instance { get; private set; }

    public static event Action OnMatch3;

    [Header("Zone Configuration")]
    // NOTE: Controlled by Spawner now, but we keep track of limit if needed
    
    [Header("Zone Origins")]
    [SerializeField] private Transform upZoneOrigin;
    [SerializeField] private Transform downZoneOrigin;
    [SerializeField] private Transform leftZoneOrigin;
    [SerializeField] private Transform rightZoneOrigin;

    [Header("Generation Settings")]
    [SerializeField] private GameObject slotPrefab;
    [SerializeField] private float slotSpacing = 1.6f;
    [SerializeField] private float startDistance = 2.2f;

    [Header("Animation Settings")]
    [SerializeField] private float moveDuration = 0.25f;
    [SerializeField] private Ease moveEase = Ease.OutBack;

    [Header("Content Shuffle (Difficulty)")]
    [SerializeField] private bool enableContentShuffle = true;
    [SerializeField] private float shuffleInterval = 12f;
    [SerializeField] private float shuffleMoveDuration = 0.45f;
    [SerializeField] private Ease shuffleEase = Ease.InOutSine;

    private bool inputEnabled = true;
    private bool isShuffling = false;
    private Coroutine shuffleRoutine;

    // State Management: Dictionary instead of Hierarchy-walking
    private Dictionary<Direction, List<FallingPiece>> grid;
    private Queue<FallingPiece> centerQueue = new Queue<FallingPiece>();

    private readonly Direction[] dirs = new Direction[]
    {
        Direction.Up, Direction.Down, Direction.Left, Direction.Right
    };

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        InitializeGrid();
    }

    private void InitializeGrid()
    {
        grid = new Dictionary<Direction, List<FallingPiece>>
        {
            { Direction.Up, new List<FallingPiece>() },
            { Direction.Down, new List<FallingPiece>() },
            { Direction.Left, new List<FallingPiece>() },
            { Direction.Right, new List<FallingPiece>() }
        };
    }

    public void SetInputEnabled(bool enabled) => inputEnabled = enabled;

    private void OnEnable()
    {
        Spawner.OnPieceSpawned += OnPieceSpawned;
        SwipeInput.OnSwipe += OnSwipe;
        SwipeInput.OnTap += OnTap;

        StartShuffleRoutineIfNeeded();
    }

    private void OnDisable()
    {
        Spawner.OnPieceSpawned -= OnPieceSpawned;
        SwipeInput.OnSwipe -= OnSwipe;
        SwipeInput.OnTap -= OnTap;

        StopShuffleRoutine();
    }

    // ================= SHUFFLE LOOP =================

    private void StartShuffleRoutineIfNeeded()
    {
        if (!enableContentShuffle) return;

        if (shuffleRoutine != null)
            StopCoroutine(shuffleRoutine);

        shuffleRoutine = StartCoroutine(ContentShuffleLoop());
    }

    private void StopShuffleRoutine()
    {
        if (shuffleRoutine != null)
        {
            StopCoroutine(shuffleRoutine);
            shuffleRoutine = null;
        }
        isShuffling = false;
    }

    private System.Collections.IEnumerator ContentShuffleLoop()
    {
        yield return new WaitForSeconds(shuffleInterval);

        while (enableContentShuffle)
        {
            yield return ShuffleContentsOnce();
            yield return new WaitForSeconds(shuffleInterval);
        }
    }

    private System.Collections.IEnumerator ShuffleContentsOnce()
    {
        if (isShuffling) yield break;
        if (!upZoneOrigin || !downZoneOrigin || !leftZoneOrigin || !rightZoneOrigin) yield break;

        // Check if board is empty
        int total = 0;
        foreach(var list in grid.Values) total += list.Count;
        if (total == 0) yield break;

        isShuffling = true;
        bool prevInput = inputEnabled;
        inputEnabled = false;

        // 1. Random Permutation of Zones
        Direction[] fromDirs = (Direction[])dirs.Clone();
        Direction[] toDirs = (Direction[])dirs.Clone();

        // Fisher-Yates
        for (int i = toDirs.Length - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (toDirs[i], toDirs[j]) = (toDirs[j], toDirs[i]);
        }

        // Ensure movement happens (avoid identity)
        bool anyDiff = false;
        for (int i = 0; i < fromDirs.Length; i++)
        {
            if (fromDirs[i] != toDirs[i]) { anyDiff = true; break; }
        }
        if (!anyDiff)
        {
            // Simple rotation
            (toDirs[0], toDirs[1], toDirs[2], toDirs[3]) = (toDirs[1], toDirs[2], toDirs[3], toDirs[0]);
        }

        // 2. Create New Grid State
        var newGrid = new Dictionary<Direction, List<FallingPiece>>
        {
            { Direction.Up, new List<FallingPiece>() },
            { Direction.Down, new List<FallingPiece>() },
            { Direction.Left, new List<FallingPiece>() },
            { Direction.Right, new List<FallingPiece>() }
        };

        for (int i = 0; i < fromDirs.Length; i++)
        {
            Direction from = fromDirs[i];
            Direction to = toDirs[i];
            newGrid[to].AddRange(grid[from]);
        }

        // 3. Animate To New Slots
        Sequence seq = DOTween.Sequence();

        foreach (var d in dirs)
        {
            Transform origin = GetOrigin(d);
            var list = newGrid[d];
            int capacity = origin.childCount; // How many slots physically exist

            // Handle overflow if shuffle moves too many items to a small zone? 
            // Ideally zones are same size, but if not, we must handle it.
            // For now, assume equal size or simple overflow handling:
            
            for (int i = 0; i < list.Count; i++)
            {
                 FallingPiece p = list[i];
                 if (!p) continue;
                 
                 // If more items than slots, stack them or just move to last slot?
                 // Let's cap at capacity to prevent errors, destroy excess? 
                 // Or just pile them on the last slot.
                 int slotIdx = Mathf.Min(i, capacity - 1);
                 Transform targetSlot = origin.GetChild(slotIdx);
                 
                 p.transform.SetParent(targetSlot, true);
                 seq.Join(p.transform.DOMove(targetSlot.position, shuffleMoveDuration).SetEase(shuffleEase));
            }
        }

        yield return seq.WaitForCompletion();

        grid = newGrid;
        
        // After shuffle, check for matches?
        // Maybe.
        
        inputEnabled = prevInput;
        isShuffling = false;
    }

    // ================= GRID GENERATION (Preserved Logic) =================

    public void SetupGrid(int slotsPerZone)
    {
        Debug.Log($"[SlotManager] Setting up Grid: {slotsPerZone} slots per zone.");
        
        // Clear Logical Grid
        foreach (var kv in grid) kv.Value.Clear();

        GenerateSlotsForZone(Direction.Up, slotsPerZone);
        GenerateSlotsForZone(Direction.Down, slotsPerZone);
        GenerateSlotsForZone(Direction.Left, slotsPerZone);
        GenerateSlotsForZone(Direction.Right, slotsPerZone);
        
        // Reset Timer if needed
         StartShuffleRoutineIfNeeded();
    }

    private void GenerateSlotsForZone(Direction dir, int count)
    {
        Transform origin = GetOrigin(dir);
        if (!origin) return;

        for (int i = origin.childCount - 1; i >= 0; i--)
            DestroyImmediate(origin.GetChild(i).gameObject);

        Vector3 spreadAxis = Vector3.zero;
        switch (dir)
        {
            case Direction.Up: 
            case Direction.Down: 
                spreadAxis = Vector3.forward; // Vertical
                break;
            case Direction.Left: 
            case Direction.Right: 
                spreadAxis = Vector3.right; // Horizontal
                break;
        }

        for (int i = 0; i < count; i++)
        {
            if (slotPrefab == null) continue;
            GameObject slot = Instantiate(slotPrefab, origin);
            
            float centerOffset = (i - (count - 1) * 0.5f) * slotSpacing;
            Vector3 finalPos = origin.position + (spreadAxis * centerOffset);
            
            slot.transform.position = finalPos;
            slot.transform.rotation = Quaternion.identity; 
            slot.name = $"Slot_{dir}_{i}";
        }
    }

    public void ResetBoard()
    {
        while (centerQueue.Count > 0)
        {
            var p = centerQueue.Dequeue();
            if (p != null) Destroy(p.gameObject);
        }

        foreach (var kv in grid)
        {
            foreach(var p in kv.Value) if(p) Destroy(p.gameObject);
            kv.Value.Clear();
        }
    }

    // ================= GAMEPLAY EVENTS =================

    private void OnPieceSpawned(FallingPiece piece)
    {
        centerQueue.Enqueue(piece);
    }

    private void OnTap(Vector2 screenPos)
    {
        if (!inputEnabled || isShuffling) return;

        Ray ray = Camera.main.ScreenPointToRay(screenPos);
        if (!Physics.Raycast(ray, out RaycastHit hit)) return;

        FallingPiece hitPiece = hit.collider.GetComponent<FallingPiece>();
        if (hitPiece == null) return;

        // CASE 1: Center
        if (centerQueue.Count > 0 && centerQueue.Peek() == hitPiece)
        {
            if (!hitPiece.isFake) return; // Can't destroy valid center piece by tap
            
            // Destroy Fake
            var sp = FindObjectOfType<Spawner>();
            if (sp) sp.NotifyCenterCleared(hitPiece);

            centerQueue.Dequeue();
            Destroy(hitPiece.gameObject);

            if (sp) sp.SpawnNextPiece();
            return;
        }

        // CASE 2: Grid Piece
        if (!TryFindPieceInGrid(hitPiece, out Direction dir, out int index)) return;

        // Penalty for tapping grid
        if (LevelManager.Instance) LevelManager.Instance.ReduceLife();

        if (hitPiece.isFake)
        {
            RemoveFromGridAt(dir, index);
            Destroy(hitPiece.gameObject);
            return;
        }

        if (hitPiece.isFrozen)
        {
            hitPiece.TakeDamage();
            if (hitPiece.freezeHealth <= 0)
            {
                RemoveFromGridAt(dir, index);
                Destroy(hitPiece.gameObject);
            }
            return;
        }

        // Normal piece
        RemoveFromGridAt(dir, index);
        Destroy(hitPiece.gameObject);
    }

    private void OnSwipe(Direction dir)
    {
        if (!inputEnabled || isShuffling) return;
        if (centerQueue.Count == 0) return;

        // Check if Zone Full
        Transform origin = GetOrigin(dir);
        if (grid[dir].Count >= origin.childCount)
        {
             Debug.Log("ZONE FULL! Penalty.");
             if (LevelManager.Instance) LevelManager.Instance.ReduceLife();
             return;
        }

        FallingPiece piece = centerQueue.Dequeue();
        var sp = FindObjectOfType<Spawner>();
        if (sp) sp.NotifyCenterCleared(piece);

        // Target is next available slot
        int targetIndex = grid[dir].Count;
        Transform targetSlot = origin.GetChild(targetIndex);

        piece.TweenToSlot(targetSlot, moveDuration, moveEase, () =>
        {
            RegisterPiece(dir, piece);

            if (piece.isFake)
            {
                piece.SetFrozen(true);
                Debug.Log("OOPS! Placed a fake -> Penalty.");
                if (LevelManager.Instance) LevelManager.Instance.ReduceLife();
            }
        });

        if (sp) sp.SpawnNextPiece();
    }

    // ================= HELPERS & LOGIC =================

    private void RegisterPiece(Direction dir, FallingPiece piece)
    {
        grid[dir].Add(piece);
        CheckMatch3(dir);
    }

    private bool TryFindPieceInGrid(FallingPiece piece, out Direction dir, out int index)
    {
        foreach (var kv in grid)
        {
            int idx = kv.Value.IndexOf(piece);
            if (idx >= 0)
            {
                dir = kv.Key;
                index = idx;
                return true;
            }
        }
        dir = Direction.None;
        index = -1;
        return false;
    }

    private void RemoveFromGridAt(Direction dir, int index)
    {
        if (dir == Direction.None) return;
        var list = grid[dir];
        if (index < 0 || index >= list.Count) return;

        list.RemoveAt(index);
        CompactZone(dir);
    }

    private void CompactZone(Direction dir)
    {
        var list = grid[dir];
        Transform origin = GetOrigin(dir);
        if (!origin) return;

        // Shift everyone towards index 0
        for (int i = 0; i < list.Count; i++)
        {
            Transform slot = origin.GetChild(i);
            FallingPiece p = list[i];
            if (!p) continue;

            p.transform.SetParent(slot, true);
            p.TweenToSlot(slot, moveDuration, moveEase, null);
        }
    }

    private Transform GetOrigin(Direction dir)
    {
        return dir switch
        {
            Direction.Up => upZoneOrigin,
            Direction.Down => downZoneOrigin,
            Direction.Left => leftZoneOrigin,
            Direction.Right => rightZoneOrigin,
            _ => null
        };
    }

    private void CheckMatch3(Direction dir)
    {
        var list = grid[dir];
        Transform origin = GetOrigin(dir);
        
        // Dynamic Match: Requires Full Zone
        int capacity = origin.childCount;
        if (list.Count < capacity) return; // Wait until full

        // Check compatibility
        var targetType = list[0].pieceType;
        foreach (var p in list)
        {
            if (p.isFrozen) return; 
            if (p.pieceType != targetType) return;
        }

        // MATCH!
        OnMatch3?.Invoke();

        // Destroy all
        foreach (var p in list) if(p) Destroy(p.gameObject);
        list.Clear();
        
        Debug.Log($"ZONE CLEAR! {capacity} items matched.");
    }
}
