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

    // ✅ Match clear anim (shrink then destroy)
    [Header("Match Explode (Cosmetic)")]
    [SerializeField] private bool enableMatchExplode = true;
    [SerializeField] private float matchExplodeDuration = 0.22f;
    [SerializeField] private float matchShrinkScale = 0.85f;

    [Header("Content Shuffle (Difficulty)")]
    [SerializeField] private bool enableContentShuffle = true;
    [SerializeField] private float shuffleInterval = 12f;
    [SerializeField] private float shuffleMoveDuration = 0.45f;
    [SerializeField] private Ease shuffleEase = Ease.InOutSine;

    [Header("Frozen Slots (Difficulty)")]
    [SerializeField] private bool enableFrozenSlots = true;
    [SerializeField] private float freezeInterval = 5f;
    [SerializeField] private float freezeChance = 0.4f;

    [Header("Ice Slot Look")]
    [SerializeField] private Texture iceSlotTexture;
    [SerializeField] private Color iceSlotColor = Color.cyan;

    [Header("Idle Animations (Cosmetic)")]
    [SerializeField] private bool enableIdleWiggle = true;
    [SerializeField] private float idleDelay = 6f;
    [SerializeField] private float idleCheckInterval = 0.25f;
    [SerializeField] private float slotWiggleStrength = 0.08f;
    [SerializeField] private float slotWiggleDuration = 1.0f;
    [SerializeField] private float pieceWiggleStrength = 0.05f;
    [SerializeField] private float pieceWiggleDuration = 1.1f;
    [SerializeField] private bool wigglePiecesToo = true;

    // ✅ ALL pieces pulse same time
    [Header("Piece Pulse (Cosmetic)")]
    [SerializeField] private bool enablePiecePulse = true;
    [SerializeField] private float pulseCheckInterval = 2.0f;
    [Range(0f, 1f)]
    [SerializeField] private float pulseChanceAll = 0.6f; // her tetikte %60 ihtimal

    [SerializeField] private float pulseScale = 0.94f;      // küçülme oranı
    [SerializeField] private float pulseHalfDuration = 0.12f;
    [SerializeField] private Ease pulseEaseDown = Ease.OutQuad;
    [SerializeField] private Ease pulseEaseUp = Ease.OutBack;

    private bool inputEnabled = true;
    private bool isShuffling = false;
    private Coroutine shuffleRoutine;
    private Coroutine freezeRoutine;

    private Dictionary<Direction, List<FallingPiece>> grid;
    private Queue<FallingPiece> centerQueue = new Queue<FallingPiece>();

    private class SlotVisualState
    {
        public Color color;
        public Texture mainTexture;
        public SlotVisualState(Color c, Texture t) { color = c; mainTexture = t; }
    }

    private Dictionary<Transform, SlotVisualState> frozenSlots = new Dictionary<Transform, SlotVisualState>();

    private readonly Direction[] dirs = new Direction[]
    {
        Direction.Up, Direction.Down, Direction.Left, Direction.Right
    };

    // Idle state
    private float lastActivityTime;
    private Coroutine idleRoutine;
    private bool idleActive = false;
    private readonly Dictionary<Transform, Tween> slotIdleTweens = new Dictionary<Transform, Tween>();
    private readonly Dictionary<FallingPiece, Tween> pieceIdleTweens = new Dictionary<FallingPiece, Tween>();

    // ✅ base scale snapshot (idle wiggle sonrası geri dönmek için)
    private readonly Dictionary<Transform, Vector3> slotIdleBaseScales = new Dictionary<Transform, Vector3>();
    private readonly Dictionary<FallingPiece, Vector3> pieceIdleBaseScales = new Dictionary<FallingPiece, Vector3>();

    // Pulse (global)
    private Coroutine pulseRoutine;
    private Sequence globalPulseSeq;
    private readonly Dictionary<FallingPiece, Vector3> piecePulseOriginalScales = new Dictionary<FallingPiece, Vector3>();

    // Inversion
    private bool invertHorizontal;
    private bool invertVertical;

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

    public void SetSwipeInversion(bool invertHorizontalSwipe, bool invertVerticalSwipe)
    {
        invertHorizontal = invertHorizontalSwipe;
        invertVertical = invertVerticalSwipe;
    }

    private Direction ApplyInversion(Direction dir)
    {
        if (invertHorizontal)
        {
            if (dir == Direction.Left) dir = Direction.Right;
            else if (dir == Direction.Right) dir = Direction.Left;
        }

        if (invertVertical)
        {
            if (dir == Direction.Up) dir = Direction.Down;
            else if (dir == Direction.Down) dir = Direction.Up;
        }

        return dir;
    }

    private void OnEnable()
    {
        Spawner.OnPieceSpawned += OnPieceSpawned;
        SwipeInput.OnSwipe += OnSwipe;
        SwipeInput.OnTap += OnTap;

        StartShuffleRoutineIfNeeded();
        StartFreezeRoutineIfNeeded();

        MarkActivity();
        StartIdleRoutine();
        StartPulseRoutine();
    }

    private void OnDisable()
    {
        Spawner.OnPieceSpawned -= OnPieceSpawned;
        SwipeInput.OnSwipe -= OnSwipe;
        SwipeInput.OnTap -= OnTap;

        StopShuffleRoutine();
        StopFreezeRoutine();

        StopIdleRoutine();
        StopIdleWiggle();

        StopPulseRoutine();
        StopGlobalPulseTween();
    }

    // ================= Activity / Idle =================

    private void MarkActivity()
    {
        lastActivityTime = Time.time;
        if (idleActive) StopIdleWiggle();
    }

    private void StartIdleRoutine()
    {
        if (!enableIdleWiggle) return;

        if (idleRoutine != null)
            StopCoroutine(idleRoutine);

        idleRoutine = StartCoroutine(IdleLoop());
    }

    private void StopIdleRoutine()
    {
        if (idleRoutine != null)
        {
            StopCoroutine(idleRoutine);
            idleRoutine = null;
        }
    }

    private System.Collections.IEnumerator IdleLoop()
    {
        while (enableIdleWiggle)
        {
            yield return new WaitForSeconds(idleCheckInterval);

            if (!inputEnabled) continue;
            if (isShuffling) continue;

            float idleTime = Time.time - lastActivityTime;
            if (!idleActive && idleTime >= idleDelay)
                StartIdleWiggle();
        }
    }

    private void StartIdleWiggle()
    {
        if (idleActive) return;
        idleActive = true;

        WiggleSlots(upZoneOrigin);
        WiggleSlots(downZoneOrigin);
        WiggleSlots(leftZoneOrigin);
        WiggleSlots(rightZoneOrigin);

        if (wigglePiecesToo)
            WiggleAllPieces();
    }

    private void StopIdleWiggle()
    {
        idleActive = false;

        foreach (var kv in slotIdleTweens)
        {
            if (kv.Value != null && kv.Value.IsActive())
                kv.Value.Kill();
        }
        slotIdleTweens.Clear();

        foreach (var kv in pieceIdleTweens)
        {
            if (kv.Value != null && kv.Value.IsActive())
                kv.Value.Kill();
        }
        pieceIdleTweens.Clear();

        // ✅ base scale’e dön
        foreach (var kv in slotIdleBaseScales)
        {
            if (kv.Key) kv.Key.localScale = kv.Value;
        }
        slotIdleBaseScales.Clear();

        foreach (var kv in pieceIdleBaseScales)
        {
            if (kv.Key) kv.Key.transform.localScale = kv.Value;
        }
        pieceIdleBaseScales.Clear();
    }

    private void WiggleSlots(Transform origin)
    {
        if (!origin) return;

        for (int i = 0; i < origin.childCount; i++)
        {
            Transform slot = origin.GetChild(i);
            if (!slot) continue;
            if (slotIdleTweens.ContainsKey(slot)) continue;

            // ✅ gerçek başlangıç scale snapshot
            if (!slotIdleBaseScales.ContainsKey(slot))
                slotIdleBaseScales[slot] = slot.localScale;

            Vector3 baseScale = slotIdleBaseScales[slot];

            Tween t = slot.DOScale(baseScale * (1f + slotWiggleStrength), slotWiggleDuration * 0.5f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo);

            slotIdleTweens[slot] = t;
        }
    }

    private void WiggleAllPieces()
    {
        foreach (var kv in grid)
        {
            var list = kv.Value;
            for (int i = 0; i < list.Count; i++)
            {
                FallingPiece p = list[i];
                if (!p) continue;
                if (pieceIdleTweens.ContainsKey(p)) continue;

                // ✅ gerçek başlangıç scale snapshot
                if (!pieceIdleBaseScales.ContainsKey(p))
                    pieceIdleBaseScales[p] = p.transform.localScale;

                Vector3 baseScale = pieceIdleBaseScales[p];

                Tween t = p.transform.DOScale(baseScale * (1f + pieceWiggleStrength), pieceWiggleDuration * 0.5f)
                    .SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo);

                pieceIdleTweens[p] = t;
            }
        }
    }

    // ================= Pulse (ALL at once) =================

    private void StartPulseRoutine()
    {
        if (!enablePiecePulse) return;

        if (pulseRoutine != null) StopCoroutine(pulseRoutine);
        pulseRoutine = StartCoroutine(PulseLoop());
    }

    private void StopPulseRoutine()
    {
        if (pulseRoutine != null)
        {
            StopCoroutine(pulseRoutine);
            pulseRoutine = null;
        }
    }

    private void StopGlobalPulseTween()
    {
        if (globalPulseSeq != null && globalPulseSeq.IsActive())
            globalPulseSeq.Kill();

        globalPulseSeq = null;

        foreach (var kv in piecePulseOriginalScales)
        {
            if (kv.Key) kv.Key.transform.localScale = kv.Value;
        }
        piecePulseOriginalScales.Clear();
    }

    private System.Collections.IEnumerator PulseLoop()
    {
        yield return new WaitForSeconds(1f);

        while (enablePiecePulse)
        {
            yield return new WaitForSeconds(pulseCheckInterval);

            if (!inputEnabled) continue;
            if (isShuffling) continue;
            if (idleActive) continue; // idle ile çakışmasın

            if (UnityEngine.Random.value > pulseChanceAll) continue;

            PulseAllPiecesAtOnce();
        }
    }

    private void PulseAllPiecesAtOnce()
    {
        if (globalPulseSeq != null && globalPulseSeq.IsActive()) return;

        // Grid’deki parçaları topla
        List<FallingPiece> pieces = new List<FallingPiece>();
        foreach (var kv in grid)
        {
            var list = kv.Value;
            for (int i = 0; i < list.Count; i++)
                if (list[i]) pieces.Add(list[i]);
        }
        if (pieces.Count == 0) return;

        // ✅ Animasyon başlamadan önceki "GERÇEK" scale snapshot
        piecePulseOriginalScales.Clear();
        for (int i = 0; i < pieces.Count; i++)
        {
            var p = pieces[i];
            piecePulseOriginalScales[p] = p.transform.localScale;
        }

        globalPulseSeq = DOTween.Sequence();

        // ✅ 1) Küçül (hepsi aynı anda)
        for (int i = 0; i < pieces.Count; i++)
        {
            var p = pieces[i];
            Vector3 original = piecePulseOriginalScales[p];
            globalPulseSeq.Join(
                p.transform.DOScale(original * pulseScale, pulseHalfDuration).SetEase(pulseEaseDown)
            );
        }

        // ✅ 2) Eski haline dön (hepsi aynı anda)
        for (int i = 0; i < pieces.Count; i++)
        {
            var p = pieces[i];
            Vector3 original = piecePulseOriginalScales[p];
            globalPulseSeq.Join(
                p.transform.DOScale(original, pulseHalfDuration).SetEase(pulseEaseUp)
            );
        }

        // ✅ 3) GARANTİ: tween bitince scale'ı manuel olarak da eski haline sabitle
        globalPulseSeq.OnComplete(() =>
        {
            for (int i = 0; i < pieces.Count; i++)
            {
                var p = pieces[i];
                if (!p) continue;

                if (piecePulseOriginalScales.TryGetValue(p, out var original))
                    p.transform.localScale = original;
            }

            piecePulseOriginalScales.Clear();
            globalPulseSeq = null;
        });

        // ✅ EXTRA GARANTİ: tween kill olursa bile scale geri dönsün
        globalPulseSeq.OnKill(() =>
        {
            for (int i = 0; i < pieces.Count; i++)
            {
                var p = pieces[i];
                if (!p) continue;

                if (piecePulseOriginalScales.TryGetValue(p, out var original))
                    p.transform.localScale = original;
            }

            piecePulseOriginalScales.Clear();
            globalPulseSeq = null;
        });
    }

    // ================= Freeze Loop =================

    private void StartFreezeRoutineIfNeeded()
    {
        if (!enableFrozenSlots) return;
        if (freezeRoutine != null) StopCoroutine(freezeRoutine);
        freezeRoutine = StartCoroutine(FrozenSlotLoop());
    }

    private void StopFreezeRoutine()
    {
        if (freezeRoutine != null)
        {
            StopCoroutine(freezeRoutine);
            freezeRoutine = null;
        }
    }

    private System.Collections.IEnumerator FrozenSlotLoop()
    {
        while (enableFrozenSlots)
        {
            yield return new WaitForSeconds(freezeInterval);

            if (UnityEngine.Random.value < freezeChance && !isShuffling && inputEnabled)
                TryFreezeRandomSlot();
        }
    }

    private void TryFreezeRandomSlot()
    {
        Direction d = dirs[UnityEngine.Random.Range(0, dirs.Length)];
        Transform origin = GetOrigin(d);
        if (!origin || origin.childCount == 0) return;

        int randIdx = UnityEngine.Random.Range(0, origin.childCount);
        Transform slotTr = origin.GetChild(randIdx);

        bool occupied = false;
        foreach (var p in grid[d])
        {
            if (p && p.transform.parent == slotTr)
            {
                occupied = true;
                break;
            }
        }

        if (!occupied)
            FreezeSlot(slotTr);
    }

    private void FreezeSlot(Transform slot)
    {
        if (frozenSlots.ContainsKey(slot)) return;

        // ✅ shake sonrası base scale’e dön
        Vector3 baseScale = slot.localScale;

        var rend = slot.GetComponent<Renderer>();
        if (rend)
        {
            frozenSlots[slot] = new SlotVisualState(rend.material.color, rend.material.mainTexture);

            rend.material.DOColor(iceSlotColor, 0.3f);
            if (iceSlotTexture != null)
                rend.material.mainTexture = iceSlotTexture;

            slot.DOShakeScale(0.3f, 0.2f).OnComplete(() =>
            {
                if (slot) slot.localScale = baseScale;
            });
        }
        else
        {
            frozenSlots[slot] = new SlotVisualState(Color.white, null);
            slot.DOShakeScale(0.3f, 0.2f).OnComplete(() =>
            {
                if (slot) slot.localScale = baseScale;
            });
        }

        MarkActivity();
    }

    private void UnfreezeSlot(Transform slot)
    {
        if (!frozenSlots.ContainsKey(slot)) return;

        var rend = slot.GetComponent<Renderer>();
        if (rend)
        {
            var original = frozenSlots[slot];
            rend.material.DOColor(original.color, 0.3f);
            rend.material.mainTexture = original.mainTexture;
        }

        frozenSlots.Remove(slot);
        slot.DOShakeRotation(0.2f, 15f);

        MarkActivity();
    }

    // ================= Shuffle Loop =================

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

    // ✅ helper: bir zone'da frozen olmayan slot sayısı
    private int GetNonFrozenSlotCount(Direction d)
    {
        Transform origin = GetOrigin(d);
        if (!origin) return 0;

        int count = 0;
        for (int i = 0; i < origin.childCount; i++)
        {
            Transform s = origin.GetChild(i);
            if (!s) continue;
            if (frozenSlots.ContainsKey(s)) continue;
            count++;
        }
        return count;
    }

    private System.Collections.IEnumerator ShuffleContentsOnce()
    {
        if (isShuffling) yield break;
        if (!upZoneOrigin || !downZoneOrigin || !leftZoneOrigin || !rightZoneOrigin) yield break;

        int total = 0;
        foreach (var list in grid.Values) total += list.Count;
        if (total == 0) yield break;

        isShuffling = true;
        bool prevInput = inputEnabled;
        inputEnabled = false;

        MarkActivity();

        Direction[] fromDirs = (Direction[])dirs.Clone();
        Direction[] toDirs = (Direction[])dirs.Clone();

        // ✅ ÜST ÜSTE BİNME FIX:
        // Frozen slotlar yüzünden hedef zone'da "available slot" < "piece count" olursa parçalar parentsiz kalıp üst üste binebiliyordu.
        // Bu yüzden permutation'ı, her zone için newCount <= nonFrozenSlotCount şartı sağlanana kadar deniyoruz.
        const int maxPermutationTries = 25;
        bool foundFeasible = false;

        for (int attempt = 0; attempt < maxPermutationTries; attempt++)
        {
            // shuffle toDirs
            for (int i = toDirs.Length - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (toDirs[i], toDirs[j]) = (toDirs[j], toDirs[i]);
            }

            bool anyDiff = false;
            for (int i = 0; i < fromDirs.Length; i++)
            {
                if (fromDirs[i] != toDirs[i]) { anyDiff = true; break; }
            }
            if (!anyDiff)
            {
                (toDirs[0], toDirs[1], toDirs[2], toDirs[3]) = (toDirs[1], toDirs[2], toDirs[3], toDirs[0]);
            }

            // tentative counts
            Dictionary<Direction, int> tentativeCounts = new Dictionary<Direction, int>
            {
                { Direction.Up, 0 },
                { Direction.Down, 0 },
                { Direction.Left, 0 },
                { Direction.Right, 0 }
            };

            for (int i = 0; i < fromDirs.Length; i++)
            {
                Direction from = fromDirs[i];
                Direction to = toDirs[i];
                tentativeCounts[to] += grid[from].Count;
            }

            bool ok = true;
            foreach (var d in dirs)
            {
                int capacityNonFrozen = GetNonFrozenSlotCount(d);
                if (tentativeCounts[d] > capacityNonFrozen)
                {
                    ok = false;
                    break;
                }
            }

            if (ok)
            {
                foundFeasible = true;
                break;
            }
        }

        // uygun permutation bulunamazsa bu tur shuffle’ı pas geç (parça koparma yok, üst üste binme yok)
        if (!foundFeasible)
        {
            inputEnabled = prevInput;
            isShuffling = false;
            yield break;
        }

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

        foreach (var d in dirs)
        {
            var list = newGrid[d];
            for (int i = 0; i < list.Count; i++)
            {
                if (!list[i]) continue;
                list[i].transform.SetParent(null, true);
            }
        }

        Sequence seq = DOTween.Sequence();

        foreach (var d in dirs)
        {
            Transform origin = GetOrigin(d);
            if (!origin) continue;

            var list = newGrid[d];

            List<Transform> availableSlots = new List<Transform>();
            for (int i = 0; i < origin.childCount; i++)
            {
                Transform s = origin.GetChild(i);
                if (!s) continue;
                if (frozenSlots.ContainsKey(s)) continue;
                // shuffle sırasında zaten parçaları kopardığımız için burada kontrol ekstra ama kalsın
                if (s.GetComponentInChildren<FallingPiece>() != null) continue;
                availableSlots.Add(s);
            }

            if (availableSlots.Count == 0) continue;

            int moveCount = Mathf.Min(list.Count, availableSlots.Count);

            for (int i = 0; i < moveCount; i++)
            {
                FallingPiece p = list[i];
                if (!p) continue;

                Transform targetSlot = availableSlots[i];

                // parent set etmeyi tween sonunda yapmıyoruz çünkü senin mevcut mantığında
                // slot index/zone ilişkisi parent ile kullanılıyor. Aynı davranışı koruyoruz.
                p.transform.SetParent(targetSlot, true);
                seq.Join(p.transform.DOMove(targetSlot.position, shuffleMoveDuration).SetEase(shuffleEase));
            }
        }

        yield return seq.WaitForCompletion();

        grid = newGrid;

        inputEnabled = prevInput;
        isShuffling = false;

        MarkActivity();
    }

    // ================= Grid Generation =================

    public void SetupGrid(int slotsPerZone)
    {
        Debug.Log($"[SlotManager] Setting up Grid: {slotsPerZone} slots per zone.");

        foreach (var kv in grid) kv.Value.Clear();
        frozenSlots.Clear();

        GenerateSlotsForZone(Direction.Up, slotsPerZone);
        GenerateSlotsForZone(Direction.Down, slotsPerZone);
        GenerateSlotsForZone(Direction.Left, slotsPerZone);
        GenerateSlotsForZone(Direction.Right, slotsPerZone);

        StartShuffleRoutineIfNeeded();
        MarkActivity();
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
                spreadAxis = Vector3.forward;
                break;
            case Direction.Left:
            case Direction.Right:
                spreadAxis = Vector3.right;
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
            foreach (var p in kv.Value) if (p) Destroy(p.gameObject);
            kv.Value.Clear();
        }
        frozenSlots.Clear();

        MarkActivity();
    }

    // ================= Events =================

    private void OnPieceSpawned(FallingPiece piece)
    {
        centerQueue.Enqueue(piece);
        MarkActivity();
    }

    private void OnTap(Vector2 screenPos)
    {
        MarkActivity();
        if (!inputEnabled || isShuffling) return;

        Ray ray = Camera.main.ScreenPointToRay(screenPos);
        if (!Physics.Raycast(ray, out RaycastHit hit)) return;

        FallingPiece hitPiece = hit.collider.GetComponent<FallingPiece>();
        if (hitPiece == null) return;

        // Center
        if (centerQueue.Count > 0 && centerQueue.Peek() == hitPiece)
        {
            if (!hitPiece.isFake) return;

            var sp = FindObjectOfType<Spawner>();
            if (sp) sp.NotifyCenterCleared(hitPiece);

            centerQueue.Dequeue();
            Destroy(hitPiece.gameObject);

            if (sp) sp.SpawnNextPiece();
            return;
        }

        // Grid piece
        if (!TryFindPieceInGrid(hitPiece, out Direction dir, out int index)) return;

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

        RemoveFromGridAt(dir, index);
        Destroy(hitPiece.gameObject);
    }

    private bool TryGetFirstFrozenEmptySlot(Direction dir, out Transform frozenSlot)
    {
        frozenSlot = null;
        Transform origin = GetOrigin(dir);
        if (!origin) return false;

        for (int i = 0; i < origin.childCount; i++)
        {
            Transform slot = origin.GetChild(i);
            if (!slot) continue;

            if (!frozenSlots.ContainsKey(slot)) continue;
            if (slot.GetComponentInChildren<FallingPiece>() != null) continue;

            frozenSlot = slot;
            return true;
        }
        return false;
    }

    private void OnSwipe(Direction dir)
    {
        MarkActivity();
        if (!inputEnabled || isShuffling)
        {
            Debug.Log($"[OnSwipe] Ignored: InputEnabled={inputEnabled}, IsShuffling={isShuffling}");
            return;
        }
        if (centerQueue.Count == 0)
        {
            Debug.Log("[OnSwipe] Ignored: Center Queue Empty");
            return;
        }

        // ✅ inversion
        dir = ApplyInversion(dir);

        Transform origin = GetOrigin(dir);
        if (!origin) return;

        if (grid[dir].Count >= origin.childCount)
        {
            Debug.Log("ZONE FULL! Penalty.");
            if (LevelManager.Instance) LevelManager.Instance.ReduceLife();
            return;
        }

        FallingPiece peekPiece = centerQueue.Peek();
        bool isFakeThrow = (peekPiece != null && peekPiece.isFake);

        if (isFakeThrow && TryGetFirstFrozenEmptySlot(dir, out Transform frozenSlot))
        {
            FallingPiece piece = centerQueue.Dequeue();

            var sp = FindObjectOfType<Spawner>();
            if (sp) sp.NotifyCenterCleared(piece);

            piece.transform.DOMove(frozenSlot.position, moveDuration)
                .SetEase(Ease.OutQuad)
                .OnComplete(() =>
                {
                    UnfreezeSlot(frozenSlot);
                    Destroy(piece.gameObject);
                });

            if (sp) sp.SpawnNextPiece();
            return;
        }

        int targetIndex = grid[dir].Count;
        Transform targetSlot = origin.GetChild(targetIndex);

        if (!isFakeThrow && frozenSlots.ContainsKey(targetSlot))
        {
            Debug.Log("[OnSwipe] Blocked: Frozen slot. Use a FAKE to break it.");
            return;
        }

        FallingPiece piece2 = centerQueue.Dequeue();
        var sp2 = FindObjectOfType<Spawner>();
        if (sp2) sp2.NotifyCenterCleared(piece2);

        piece2.TweenToSlot(targetSlot, moveDuration, moveEase, () =>
        {
            RegisterPiece(dir, piece2);

            if (piece2.isFake)
            {
                piece2.SetFrozen(true);
                Debug.Log("OOPS! Placed a fake -> Penalty.");
                if (LevelManager.Instance) LevelManager.Instance.ReduceLife();
            }
        });

        if (sp2) sp2.SpawnNextPiece();
    }

    // ================= Helpers =================

    private void RegisterPiece(Direction dir, FallingPiece piece)
    {
        grid[dir].Add(piece);
        CheckMatch3(dir);
        MarkActivity();
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
        MarkActivity();
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

    // ================= Match3 =================

    private void CheckMatch3(Direction dir)
    {
        var list = grid[dir];
        Transform origin = GetOrigin(dir);

        int capacity = origin.childCount;
        if (list.Count < capacity) return;

        string targetKey = list[0] != null ? list[0].GetPieceKey() : null;
        Color targetColorFallback = list[0] != null ? list[0].GetNormalColor() : Color.white;

        foreach (var p in list)
        {
            if (p.isFrozen) return;

            // Prefer prefab/type key matching; fallback to color if key is not set.
            if (!string.IsNullOrEmpty(targetKey))
            {
                if (p.GetPieceKey() != targetKey) return;
            }
            else
            {
                if (p.GetNormalColor() != targetColorFallback) return;
            }
        }

        Debug.Log(!string.IsNullOrEmpty(targetKey)
            ? $"[CheckMatch3] MATCHED! Type: {targetKey}."
            : $"[CheckMatch3] MATCHED! Color: {targetColorFallback}.");
        MarkActivity();
        OnMatch3?.Invoke();

        if (enableMatchExplode)
        {
            foreach (var p in list)
                if (p) p.PlayMatchShrinkAndDestroy(matchExplodeDuration, matchShrinkScale);
        }
        else
        {
            foreach (var p in list)
                if (p) Destroy(p.gameObject);
        }

        list.Clear();
        Debug.Log($"ZONE CLEAR! {capacity} items matched.");
    }
}
