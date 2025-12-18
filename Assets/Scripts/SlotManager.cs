using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

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

    [Header("Zone Configuration")]
    [SerializeField] private int maxSlotsPerZone = 3;
    [SerializeField] private float slotSpacing = 1.2f;
    [SerializeField] private Vector3 placementOffset = Vector3.zero; // Manual XYZ adjustment

    [Header("Zone Origins")]
    [SerializeField] private Transform upZoneOrigin;
    [SerializeField] private Transform downZoneOrigin;
    [SerializeField] private Transform leftZoneOrigin;
    [SerializeField] private Transform rightZoneOrigin;

    [Header("Animation Settings")]
    [SerializeField] private float moveDuration = 0.25f;
    [SerializeField] private Ease moveEase = Ease.OutBack;

    [Header("Scoring/Game Settings")]
    [SerializeField] private bool autoClearMatch3 = true;

    // Internal Logical Grid
    private Dictionary<Direction, List<FallingPiece>> grid;
    
    // Queue of objects waiting in the center
    private Queue<FallingPiece> centerQueue = new Queue<FallingPiece>();

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

    // --- Event Handlers ---

    private void OnPieceSpawned(FallingPiece piece)
    {
        centerQueue.Enqueue(piece);
    }

    private void OnTap(Vector2 screenPos)
    {
        Ray ray = Camera.main.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            FallingPiece hitPiece = hit.collider.GetComponent<FallingPiece>();
            if (hitPiece == null) return;

            // CASE 1: Tapping the Central Object (Waiting in queue)
            if (centerQueue.Count > 0 && centerQueue.Peek() == hitPiece)
            {
                if (hitPiece.isFake)
                {
                    // Correct Move! Destroy Fake.
                    Debug.Log("Success! Fake destroyed.");
                    centerQueue.Dequeue();
                    Destroy(hitPiece.gameObject);
                    FindObjectOfType<Spawner>().SpawnNextPiece();
                }
                else
                {
                    // Penalize? Or just allow destroy? For now, allow but log warning.
                    Debug.Log("Warning: You destroyed a valid piece!");
                    centerQueue.Dequeue();
                    Destroy(hitPiece.gameObject);
                    FindObjectOfType<Spawner>().SpawnNextPiece();
                }
            }
            // CASE 2: Tapping a Frozen Object in Slot
            else if (hitPiece.isFrozen)
            {
                hitPiece.TakeDamage();
                Debug.Log($"Frozen Hit! Health: {hitPiece.freezeHealth}");

                if (hitPiece.freezeHealth <= 0)
                {
                    // Broken!
                    // Remove from grid list? We need to find which list it belongs to.
                    RemoveFromGrid(hitPiece);
                    Destroy(hitPiece.gameObject);
                }
            }
        }
    }

    private void RemoveFromGrid(FallingPiece piece)
    {
        foreach (var list in grid.Values)
        {
            if (list.Contains(piece))
            {
                list.Remove(piece);
                return;
            }
        }
    }

    private void OnSwipe(Direction dir)
    {
        if (centerQueue.Count == 0) return;
        FallingPiece piece = centerQueue.Peek();

        // 1. Check if Zone is Full
        if (IsZoneFull(dir))
        {
            Debug.Log($"Zone {dir} is Full!");
            return; 
        }

        // 2. Get Target Slot Transform
        Transform targetSlot = GetNextSlot(dir);
        if (targetSlot == null) return; 

        // 3. Dequeue and Move
        centerQueue.Dequeue();
        
        piece.TweenToSlot(targetSlot, moveDuration, moveEase, () =>
        {
            RegisterPiece(dir, piece);
            
            // FAKE LOGIC: If it was fake, FREEZE IT!
            if (piece.isFake)
            {
                piece.SetFrozen(true);
                Debug.Log("Oops! You placed a fake. SLOT FROZEN!");
                // Do NOT trigger match check for this specific placement if it's frozen
            }
        });
        
        FindObjectOfType<Spawner>().SpawnNextPiece();
    }

    // --- Grid Logic (Formerly GridManager) ---

    private bool IsZoneFull(Direction dir)
    {
        if (!grid.ContainsKey(dir)) return true;
        return grid[dir].Count >= maxSlotsPerZone;
    }

    private Transform GetNextSlot(Direction dir)
    {
        if (!grid.ContainsKey(dir)) return null;

        int slotIndex = grid[dir].Count;
        Transform origin = GetOrigin(dir);

        if (origin == null)
        {
             Debug.LogWarning($"[SlotManager] Origin for {dir} is not assigned!");
             return null;
        }

        // Assumption: The physical slot objects (pink squares) are children of the Zone Origin
        if (slotIndex < origin.childCount)
        {
            return origin.GetChild(slotIndex);
        }
        else
        {
            Debug.LogWarning($"[SlotManager] Not enough slots under {dir} origin! Expected index {slotIndex}");
            return null;
        }
    }

    private void RegisterPiece(Direction dir, FallingPiece piece)
    {
        if (!grid.ContainsKey(dir)) return;
        grid[dir].Add(piece);

        if (autoClearMatch3)
            CheckMatch3(dir);
    }

    private Transform GetOrigin(Direction dir)
    {
        switch (dir)
        {
            case Direction.Up: return upZoneOrigin;
            case Direction.Down: return downZoneOrigin;
            case Direction.Left: return leftZoneOrigin;
            case Direction.Right: return rightZoneOrigin;
            default: return null;
        }
    }

    private void CheckMatch3(Direction dir)
    {
        var list = grid[dir];
        if (list.Count < 3) return;

        // Check last 3 items
        var p1 = list[list.Count - 1];
        var p2 = list[list.Count - 2];
        var p3 = list[list.Count - 3];

        if (p1 != null && p2 != null && p3 != null &&
            !p1.isFrozen && !p2.isFrozen && !p3.isFrozen && // Ensure none are frozen
            p1.pieceType == p2.pieceType && p2.pieceType == p3.pieceType)
        {
            Debug.Log($"[SlotManager] MATCH-3 on {dir}!");
            
            // Remove from list
            list.Remove(p1);
            list.Remove(p2);
            list.Remove(p3);

            // Destroy visual
            Destroy(p1.gameObject);
            Destroy(p2.gameObject);
            Destroy(p3.gameObject);
        }
    }
}
