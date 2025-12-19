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

    // LevelManager için eventler
    public static event Action OnMatch3;

    [Header("Zone Configuration")]
    [SerializeField] private int maxSlotsPerZone = 3;

    [Header("Zone Origins")]
    [SerializeField] private Transform upZoneOrigin;
    [SerializeField] private Transform downZoneOrigin;
    [SerializeField] private Transform leftZoneOrigin;
    [SerializeField] private Transform rightZoneOrigin;

    [Header("Animation Settings")]
    [SerializeField] private float moveDuration = 0.25f;
    [SerializeField] private Ease moveEase = Ease.OutBack;

    [Header("UI")]
    // UI is now handled by LevelManager directly or events
    // [SerializeField] private TMP_Text normalDestroyText;
    // [SerializeField] private TMP_Text scoreText;

    private bool inputEnabled = true;

    private Dictionary<Direction, List<FallingPiece>> grid;
    private Queue<FallingPiece> centerQueue = new Queue<FallingPiece>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        InitializeGrid();
    }

    public void SetInputEnabled(bool enabled) => inputEnabled = enabled;

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

    private void OnEnable()
    {
        Spawner.OnPieceSpawned += OnPieceSpawned;
        SwipeInput.OnSwipe += OnSwipe;
        SwipeInput.OnTap += OnTap;
    }

    private void OnDisable()
    {
        Spawner.OnPieceSpawned -= OnPieceSpawned;
        SwipeInput.OnSwipe -= OnSwipe;
        SwipeInput.OnTap -= OnTap;
    }

    // ================= UI =================

    // UI Logic moved to LevelManager / Events


    // ================= RESET (Fail olunca temizle) =================

    public void ResetBoard()
    {
        // Center queue temizle
        while (centerQueue.Count > 0)
        {
            var p = centerQueue.Dequeue();
            if (p != null) Destroy(p.gameObject);
        }

        // Grid listeleri temizle
        foreach (var kv in grid)
            kv.Value.Clear();

        // Slotların altındaki parçaları destroy et
        ClearOriginChildren(upZoneOrigin);
        ClearOriginChildren(downZoneOrigin);
        ClearOriginChildren(leftZoneOrigin);
        ClearOriginChildren(rightZoneOrigin);
    }

    private void ClearOriginChildren(Transform origin)
    {
        if (!origin) return;

        // origin children = slotlar, slot children = parçalar
        for (int i = 0; i < origin.childCount; i++)
        {
            var slot = origin.GetChild(i);
            for (int c = slot.childCount - 1; c >= 0; c--)
            {
                Destroy(slot.GetChild(c).gameObject);
            }
        }
    }

    // ================= EVENTS =================

    private void OnPieceSpawned(FallingPiece piece)
    {
        centerQueue.Enqueue(piece);
    }

    private void OnTap(Vector2 screenPos)
    {
        if (!inputEnabled) return;

        Ray ray = Camera.main.ScreenPointToRay(screenPos);
        if (!Physics.Raycast(ray, out RaycastHit hit)) return;

        FallingPiece hitPiece = hit.collider.GetComponent<FallingPiece>();
        if (hitPiece == null) return;

        // CASE 1: Center piece
        if (centerQueue.Count > 0 && centerQueue.Peek() == hitPiece)
        {
            var sp = FindObjectOfType<Spawner>();

            if (hitPiece.isFake)
            {
                // CORRECT: Destroy Fake - No penalty
                if (sp) sp.NotifyCenterCleared(hitPiece);
                centerQueue.Dequeue();
                Destroy(hitPiece.gameObject);
                
                // Bonus/Feedback?
                Debug.Log("GOOD! Fake destroyed.");
                
                if (sp) sp.SpawnNextPiece();
            }
            else
            {
                // WRONG: Destroyed a valid piece -> PENALTY
                Debug.Log("BAD! You destroyed a good piece.");
                if (LevelManager.Instance) LevelManager.Instance.ReduceLife();
                
                // Proceed anyway (or block?) - Let's allow but penalize
                if (sp) sp.NotifyCenterCleared(hitPiece);
                centerQueue.Dequeue();
                Destroy(hitPiece.gameObject);
                if (sp) sp.SpawnNextPiece();
            }
        }
        // CASE 2: Frozen slot piece
        else if (hitPiece.isFrozen)
        {
            hitPiece.TakeDamage();
            if (hitPiece.freezeHealth <= 0)
            {
                RemoveFromGrid(hitPiece);
                Destroy(hitPiece.gameObject);
            }
        }
    }

    private void OnSwipe(Direction dir)
    {
        if (!inputEnabled) return;

        if (centerQueue.Count == 0) return;
        
        // MISTAKE 1: Zone Full
        if (IsZoneFull(dir)) 
        {
             Debug.Log("ZONE FULL! Penalty.");
             if (LevelManager.Instance) LevelManager.Instance.ReduceLife();
             return;
        }

        FallingPiece piece = centerQueue.Dequeue();
        Transform targetSlot = GetNextSlot(dir);
        if (targetSlot == null) return;

        var sp = FindObjectOfType<Spawner>();
        if (sp) sp.NotifyCenterCleared(piece);

        piece.TweenToSlot(targetSlot, moveDuration, moveEase, () =>
        {
            RegisterPiece(dir, piece);

            // MISTAKE 2: Placing a Fake
            if (piece.isFake)
            {
                piece.SetFrozen(true);
                Debug.Log("OOPS! Placed a fake -> Penalty.");
                if (LevelManager.Instance) LevelManager.Instance.ReduceLife();
            }
        });

        if (sp) sp.SpawnNextPiece();
    }

    // ================= GRID =================

    private bool IsZoneFull(Direction dir)
    {
        return grid[dir].Count >= maxSlotsPerZone;
    }

    private Transform GetNextSlot(Direction dir)
    {
        Transform origin = GetOrigin(dir);
        int index = grid[dir].Count;

        if (origin == null || index >= origin.childCount) return null;
        return origin.GetChild(index);
    }

    private void RegisterPiece(Direction dir, FallingPiece piece)
    {
        grid[dir].Add(piece);
        CheckMatch3(dir);
    }

    private void RemoveFromGrid(FallingPiece piece)
    {
        foreach (var list in grid.Values)
            if (list.Remove(piece)) return;
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
        if (list.Count < 3) return;

        var a = list[^1];
        var b = list[^2];
        var c = list[^3];

        if (!a.isFrozen && !b.isFrozen && !c.isFrozen &&
            a.pieceType == b.pieceType && b.pieceType == c.pieceType)
        {
            // AddScore(10); // Removed as Score is handled differently or ignored for now
            OnMatch3?.Invoke();

            list.RemoveRange(list.Count - 3, 3);
            Destroy(a.gameObject);
            Destroy(b.gameObject);
            Destroy(c.gameObject);
        }
    }
}
