using System;
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

    [Header("Content Shuffle (Difficulty)")]
    [SerializeField] private bool enableContentShuffle = true;
    [SerializeField] private float shuffleInterval = 12f;     // kaç saniyede bir içerikler taşınsın
    [SerializeField] private float shuffleMoveDuration = 0.45f;
    [SerializeField] private Ease shuffleEase = Ease.InOutSine;

    private bool inputEnabled = true;
    private bool isShuffling = false;
    private Coroutine shuffleRoutine;

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

        StartShuffleRoutineIfNeeded();
    }

    private void OnDisable()
    {
        Spawner.OnPieceSpawned -= OnPieceSpawned;
        SwipeInput.OnSwipe -= OnSwipe;
        SwipeInput.OnTap -= OnTap;

        StopShuffleRoutine();
    }

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

    // ================= RESET =================

    public void ResetBoard()
    {
        while (centerQueue.Count > 0)
        {
            var p = centerQueue.Dequeue();
            if (p != null) Destroy(p.gameObject);
        }

        foreach (var kv in grid)
            kv.Value.Clear();

        ClearOriginChildren(upZoneOrigin);
        ClearOriginChildren(downZoneOrigin);
        ClearOriginChildren(leftZoneOrigin);
        ClearOriginChildren(rightZoneOrigin);
    }

    private void ClearOriginChildren(Transform origin)
    {
        if (!origin) return;

        for (int i = 0; i < origin.childCount; i++)
        {
            var slot = origin.GetChild(i);
            for (int c = slot.childCount - 1; c >= 0; c--)
                Destroy(slot.GetChild(c).gameObject);
        }
    }

    // ================= EVENTS =================

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

        // CASE 1: Center'daki parça
        if (centerQueue.Count > 0 && centerQueue.Peek() == hitPiece)
        {
            // Center'da sadece fake yok edilsin ve CAN GİTMEZ
            if (!hitPiece.isFake) return;

            var sp = FindObjectOfType<Spawner>();
            if (sp) sp.NotifyCenterCleared(hitPiece);

            centerQueue.Dequeue();
            Destroy(hitPiece.gameObject);

            if (sp) sp.SpawnNextPiece();
            return;
        }

        // CASE 2: Slot'taki parça
        if (!TryFindPieceInGrid(hitPiece, out Direction dir, out int index))
            return;

        // Slot'ta silme => her zaman can götürsün
        if (LevelManager.Instance) LevelManager.Instance.ReduceLife();

        // Slot'taki FAKE: frozen olsa bile 1 tıkta silinsin
        if (hitPiece.isFake)
        {
            RemoveFromGridAt(dir, index);
            Destroy(hitPiece.gameObject);
            return;
        }

        // Frozen normal: hasar ile kır
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

        // Normal: direkt sil
        RemoveFromGridAt(dir, index);
        Destroy(hitPiece.gameObject);
    }

    private void OnSwipe(Direction dir)
    {
        if (!inputEnabled || isShuffling) return;
        if (centerQueue.Count == 0) return;

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

            // Fake yerleştirince donsun + can gitsin
            if (piece.isFake)
            {
                piece.SetFrozen(true);
                Debug.Log("OOPS! Placed a fake -> Penalty.");
                if (LevelManager.Instance) LevelManager.Instance.ReduceLife();
            }
        });

        if (sp) sp.SpawnNextPiece();
    }

    // ================= CONTENT SHUFFLE =================

    private System.Collections.IEnumerator ShuffleContentsOnce()
    {
        if (isShuffling) yield break;

        // origins null kontrol
        if (!upZoneOrigin || !downZoneOrigin || !leftZoneOrigin || !rightZoneOrigin)
            yield break;

        // Taş yoksa uğraşma
        int total = grid[Direction.Up].Count + grid[Direction.Down].Count + grid[Direction.Left].Count + grid[Direction.Right].Count;
        if (total == 0) yield break;

        isShuffling = true;
        bool prevInput = inputEnabled;
        inputEnabled = false;

        // 1) Direction’lar için random permütasyon hazırla
        // mapping: fromDirs[i] -> toDirs[i]
        Direction[] fromDirs = (Direction[])dirs.Clone();
        Direction[] toDirs = (Direction[])dirs.Clone();

        // Fisher-Yates shuffle
        for (int i = toDirs.Length - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (toDirs[i], toDirs[j]) = (toDirs[j], toDirs[i]);
        }

        // "hiç değişmedi" olmasın diye (çok nadiren olur)
        bool anyDiff = false;
        for (int i = 0; i < fromDirs.Length; i++)
        {
            if (fromDirs[i] != toDirs[i]) { anyDiff = true; break; }
        }
        if (!anyDiff)
        {
            // basitçe 1 rotasyon yap
            (toDirs[0], toDirs[1], toDirs[2], toDirs[3]) = (toDirs[1], toDirs[2], toDirs[3], toDirs[0]);
        }

        // 2) Yeni grid oluştur (listeyi komple taşıyoruz: Left’tekiler Down’a gibi)
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

            // from listesini to’ya taşı
            newGrid[to].AddRange(grid[from]);
        }

        // 3) Animasyon: her to-zone’daki parçaları slotlara yerleştir
        Sequence seq = DOTween.Sequence();

        foreach (var d in dirs)
        {
            Transform origin = GetOrigin(d);
            var list = newGrid[d];

            // güvenlik: zone kapasitesi
            int cap = Mathf.Min(maxSlotsPerZone, origin.childCount);
            if (list.Count > cap)
            {
                // normalde zaten list.Count <= maxSlotsPerZone idi, yine de güvenlik:
                // fazla olanları sona atıyoruz (istersen destroy da edebiliriz)
                list.RemoveRange(cap, list.Count - cap);
            }

            for (int i = 0; i < list.Count; i++)
            {
                FallingPiece p = list[i];
                if (!p) continue;

                Transform targetSlot = origin.GetChild(i);

                // parent düzgün kalsın
                p.transform.SetParent(targetSlot, true);

                // DOTween seq içine join edelim
                // TweenToSlot callback beklemiyor ama içinde DOTween yapıyorsan sorun yok.
                // Daha garantili olsun diye direkt DOLocalMove ile de yapılabilirdi.
                seq.Join(p.transform.DOMove(targetSlot.position, shuffleMoveDuration).SetEase(shuffleEase));
            }
        }

        yield return seq.WaitForCompletion();

        // 4) grid’i yeni haline geçir
        grid = newGrid;

        inputEnabled = prevInput;
        isShuffling = false;
    }

    // ================= GRID =================

    private bool IsZoneFull(Direction dir) => grid[dir].Count >= maxSlotsPerZone;

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

    // ---- Grid helpers ----

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

        for (int i = 0; i < list.Count; i++)
        {
            Transform slot = origin.GetChild(i);
            FallingPiece p = list[i];
            if (!p) continue;

            p.transform.SetParent(slot, true);
            p.TweenToSlot(slot, moveDuration, moveEase, null);
        }
    }

    // ---- Match3 ----

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
            OnMatch3?.Invoke();

            list.RemoveRange(list.Count - 3, 3);
            Destroy(a.gameObject);
            Destroy(b.gameObject);
            Destroy(c.gameObject);

            CompactZone(dir);
        }
    }
}
