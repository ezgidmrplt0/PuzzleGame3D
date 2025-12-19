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

    // LevelManager Events
    public static event Action OnMatch3;

    [Header("Zone Configuration")]


    [Header("Zone Origins")]
    [SerializeField] private Transform upZoneOrigin;
    [SerializeField] private Transform downZoneOrigin;
    [SerializeField] private Transform leftZoneOrigin;
    [SerializeField] private Transform rightZoneOrigin;

    [Header("Animation Settings")]
    [SerializeField] private float moveDuration = 0.25f;
    [SerializeField] private Ease moveEase = Ease.OutBack;

    private bool inputEnabled = true;

    // Queue for the center piece spawns
    private Queue<FallingPiece> centerQueue = new Queue<FallingPiece>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void SetInputEnabled(bool enabled) => inputEnabled = enabled;

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

    // ================= RESET =================

    public void ResetBoard()
    {
        // Clear Center Queue
        while (centerQueue.Count > 0)
        {
            var p = centerQueue.Dequeue();
            if (p != null) Destroy(p.gameObject);
        }

        // Clear Slot Children (Hierarchy Based)
        ClearOriginChildren(upZoneOrigin);
        ClearOriginChildren(downZoneOrigin);
        ClearOriginChildren(leftZoneOrigin);
        ClearOriginChildren(rightZoneOrigin);
    }

    private void ClearOriginChildren(Transform origin)
    {
        if (!origin) return;
        // Slots are children of origin. Pieces are children of slots.
        // But wait, the previous code iterated slots?
        // Let's assume Origin -> Slot0, Slot1, Slot2 -> Piece
        
        for (int i = 0; i < origin.childCount; i++)
        {
            Transform slot = origin.GetChild(i);
            if (slot.childCount > 0)
            {
                // Destroy the resident piece
                for (int c = slot.childCount - 1; c >= 0; c--)
                {
                    Destroy(slot.GetChild(c).gameObject);
                }
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
                
                Debug.Log("GOOD! Fake destroyed.");
                
                if (sp) sp.SpawnNextPiece();
            }
            else
            {
                // WRONG: Destroyed a valid piece -> PENALTY
                Debug.Log("BAD! You destroyed a good piece.");
                if (LevelManager.Instance) LevelManager.Instance.ReduceLife();
                
                // Proceed to clear it
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
                // GAP LOGIC: Simply destroy the piece. Gaps are allowed.
                Destroy(hitPiece.gameObject);
            }
        }
    }

    private void OnSwipe(Direction dir)
    {
        if (!inputEnabled) return;
        if (centerQueue.Count == 0) return;

        Transform targetSlot = GetFirstEmptySlot(dir);
        
        // MISTAKE 1: Zone Full
        if (targetSlot == null) 
        {
             Debug.Log("ZONE FULL! Penalty.");
             if (LevelManager.Instance) LevelManager.Instance.ReduceLife();
             return;
        }

        FallingPiece piece = centerQueue.Dequeue();
        var sp = FindObjectOfType<Spawner>();
        if (sp) sp.NotifyCenterCleared(piece);

        piece.TweenToSlot(targetSlot, moveDuration, moveEase, () =>
        {
            // MISTAKE 2: Placing a Fake
            if (piece.isFake)
            {
                piece.SetFrozen(true);
                Debug.Log("OOPS! Placed a fake -> Penalty.");
                if (LevelManager.Instance) LevelManager.Instance.ReduceLife();
            }
            
            CheckMatch3(dir);
        });

        if (sp) sp.SpawnNextPiece();
    }

    // ================= HIERARCHY GRID LOGIC =================

    private Transform GetFirstEmptySlot(Direction dir)
    {
        Transform origin = GetOrigin(dir);
        if (origin == null) return null;

        // Scan slots 0, 1, 2... return first one with NO children
        for (int i = 0; i < origin.childCount; i++)
        {
            Transform slot = origin.GetChild(i);
            if (slot.childCount == 0) return slot;
        }
        
        return null;
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
        Transform origin = GetOrigin(dir);
        if (origin == null || origin.childCount < 3) return;

        // Logic: Check indices 0, 1, 2. If all have pieces and match, destroy 'em.
        FallingPiece p0 = GetPieceInSlot(origin, 0);
        FallingPiece p1 = GetPieceInSlot(origin, 1);
        FallingPiece p2 = GetPieceInSlot(origin, 2);

        if (p0 != null && p1 != null && p2 != null)
        {
            if (!p0.isFrozen && !p1.isFrozen && !p2.isFrozen &&
                p0.pieceType == p1.pieceType && p1.pieceType == p2.pieceType)
            {
                OnMatch3?.Invoke();

                Destroy(p0.gameObject);
                Destroy(p1.gameObject);
                Destroy(p2.gameObject);
            }
        }
    }

    private FallingPiece GetPieceInSlot(Transform origin, int index)
    {
        if (index >= origin.childCount) return null;
        Transform slot = origin.GetChild(index);
        
        if (slot.childCount > 0)
            return slot.GetChild(0).GetComponent<FallingPiece>();
            
        return null;
    }
    
    public bool IsZoneFull(Direction dir)
    {
        return GetFirstEmptySlot(dir) == null;
    }
}
