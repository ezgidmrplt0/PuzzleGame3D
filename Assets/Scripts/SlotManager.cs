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

    [Header("Zone Origins")]
    [SerializeField] private Transform upZoneOrigin;
    [SerializeField] private Transform downZoneOrigin;
    [SerializeField] private Transform leftZoneOrigin;
    [SerializeField] private Transform rightZoneOrigin;

    [Header("Generation Settings (unused - keep)")]
    [SerializeField] private GameObject slotPrefab;        // kullanılmıyor
    [SerializeField] private float slotSpacing = 1.6f;     // kullanılmıyor
    [SerializeField] private float startDistance = 2.2f;   // kullanılmıyor

    [Header("Animation Settings")]
    [SerializeField] private float moveDuration = 0.25f;
    [SerializeField] private Ease moveEase = Ease.OutBack;

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

    [Header("Piece Pulse (Cosmetic)")]
    [SerializeField] private bool enablePiecePulse = true;
    [SerializeField] private float pulseCheckInterval = 2.0f;
    [Range(0f, 1f)]
    [SerializeField] private float pulseChanceAll = 0.6f;
    [SerializeField] private float pulseScale = 0.94f;
    [SerializeField] private float pulseHalfDuration = 0.12f;
    [SerializeField] private Ease pulseEaseDown = Ease.OutQuad;
    [SerializeField] private Ease pulseEaseUp = Ease.OutBack;

    private bool inputEnabled = true;
    private bool isShuffling = false;
    private bool randomFreezeEnabled = true;

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

    // ✅ sahnede elinle dizdiğin slotlar
    private readonly Dictionary<Direction, List<Transform>> zoneSlots = new Dictionary<Direction, List<Transform>>();

    // Idle
    private float lastActivityTime;
    private Coroutine idleRoutine;
    private bool idleActive = false;
    private readonly Dictionary<Transform, Tween> slotIdleTweens = new Dictionary<Transform, Tween>();
    private readonly Dictionary<FallingPiece, Tween> pieceIdleTweens = new Dictionary<FallingPiece, Tween>();
    private readonly Dictionary<Transform, Vector3> slotIdleBaseScales = new Dictionary<Transform, Vector3>();
    private readonly Dictionary<FallingPiece, Vector3> pieceIdleBaseScales = new Dictionary<FallingPiece, Vector3>();

    // Pulse
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

        zoneSlots[Direction.Up] = new List<Transform>();
        zoneSlots[Direction.Down] = new List<Transform>();
        zoneSlots[Direction.Left] = new List<Transform>();
        zoneSlots[Direction.Right] = new List<Transform>();
    }

    // ✅ world scale sabitle
    private static void KeepWorldScale(Transform t, Vector3 targetWorldScale)
    {
        if (!t) return;

        Transform parent = t.parent;
        if (parent == null)
        {
            t.localScale = targetWorldScale;
            return;
        }

        Vector3 p = parent.lossyScale;

        float lx = (Mathf.Abs(p.x) > 0.00001f) ? targetWorldScale.x / p.x : t.localScale.x;
        float ly = (Mathf.Abs(p.y) > 0.00001f) ? targetWorldScale.y / p.y : t.localScale.y;
        float lz = (Mathf.Abs(p.z) > 0.00001f) ? targetWorldScale.z / p.z : t.localScale.z;

        t.localScale = new Vector3(lx, ly, lz);
    }

    // ================= PUBLIC API =================

    public void SetInputEnabled(bool enabled) => inputEnabled = enabled;

    public void SetSwipeInversion(bool invertHorizontalSwipe, bool invertVerticalSwipe)
    {
        invertHorizontal = invertHorizontalSwipe;
        invertVertical = invertVerticalSwipe;
    }

    // ✅ Random freeze aç/kapat (Level design kontrolü)
    public void SetRandomFreezeEnabled(bool enabled) => randomFreezeEnabled = enabled;

    // ✅ Level başı kaç frozen olacak
    public void ApplyStartFrozenCount(int count)
    {
        if (count <= 0) return;

        CacheManualSlots();

        // tüm slotları havuza koy (boş olanlardan seçelim)
        List<Transform> candidates = new List<Transform>();
        foreach (var d in dirs)
        {
            if (!zoneSlots.ContainsKey(d)) continue;
            var slots = zoneSlots[d];
            for (int i = 0; i < slots.Count; i++)
            {
                var s = slots[i];
                if (!s) continue;
                if (frozenSlots.ContainsKey(s)) continue;
                if (s.GetComponentInChildren<FallingPiece>() != null) continue; // doluysa donma
                candidates.Add(s);
            }
        }

        // rastgele seç
        for (int i = 0; i < count && candidates.Count > 0; i++)
        {
            int idx = UnityEngine.Random.Range(0, candidates.Count);
            Transform pick = candidates[idx];
            candidates.RemoveAt(idx);
            FreezeSlot(pick);
        }
    }

    // ================= Manual Slot Cache =================

    private void CacheManualSlots()
    {
        CacheZone(Direction.Up, upZoneOrigin);
        CacheZone(Direction.Down, downZoneOrigin);
        CacheZone(Direction.Left, leftZoneOrigin);
        CacheZone(Direction.Right, rightZoneOrigin);
    }

    private void CacheZone(Direction dir, Transform origin)
    {
        zoneSlots[dir].Clear();
        if (!origin) return;

        for (int i = 0; i < origin.childCount; i++)
        {
            Transform slot = origin.GetChild(i);
            if (slot) zoneSlots[dir].Add(slot);
        }
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

        CacheManualSlots();

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
            if (kv.Value != null && kv.Value.IsActive()) kv.Value.Kill();
        slotIdleTweens.Clear();

        foreach (var kv in pieceIdleTweens)
            if (kv.Value != null && kv.Value.IsActive()) kv.Value.Kill();
        pieceIdleTweens.Clear();

        foreach (var kv in slotIdleBaseScales)
            if (kv.Key) kv.Key.localScale = kv.Value;
        slotIdleBaseScales.Clear();

        foreach (var kv in pieceIdleBaseScales)
            if (kv.Key) kv.Key.transform.localScale = kv.Value;
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

    // ================= Pulse =================

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
            if (kv.Key) kv.Key.transform.localScale = kv.Value;
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
            if (idleActive) continue;

            if (UnityEngine.Random.value > pulseChanceAll) continue;

            PulseAllPiecesAtOnce();
        }
    }

    private void PulseAllPiecesAtOnce()
    {
        if (globalPulseSeq != null && globalPulseSeq.IsActive()) return;

        List<FallingPiece> pieces = new List<FallingPiece>();
        foreach (var kv in grid)
        {
            var list = kv.Value;
            for (int i = 0; i < list.Count; i++)
                if (list[i]) pieces.Add(list[i]);
        }
        if (pieces.Count == 0) return;

        piecePulseOriginalScales.Clear();
        for (int i = 0; i < pieces.Count; i++)
            piecePulseOriginalScales[pieces[i]] = pieces[i].transform.localScale;

        globalPulseSeq = DOTween.Sequence();

        for (int i = 0; i < pieces.Count; i++)
        {
            var p = pieces[i];
            Vector3 original = piecePulseOriginalScales[p];
            globalPulseSeq.Join(p.transform.DOScale(original * pulseScale, pulseHalfDuration).SetEase(pulseEaseDown));
        }

        for (int i = 0; i < pieces.Count; i++)
        {
            var p = pieces[i];
            Vector3 original = piecePulseOriginalScales[p];
            globalPulseSeq.Join(p.transform.DOScale(original, pulseHalfDuration).SetEase(pulseEaseUp));
        }

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

    // ================= Freeze =================

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

            if (!randomFreezeEnabled) continue;
            if (UnityEngine.Random.value < freezeChance && !isShuffling && inputEnabled)
                TryFreezeRandomSlot();
        }
    }

    private void TryFreezeRandomSlot()
    {
        Direction d = dirs[UnityEngine.Random.Range(0, dirs.Length)];
        if (!zoneSlots.ContainsKey(d) || zoneSlots[d].Count == 0) return;

        int randIdx = UnityEngine.Random.Range(0, zoneSlots[d].Count);
        Transform slotTr = zoneSlots[d][randIdx];
        if (!slotTr) return;

        if (slotTr.GetComponentInChildren<FallingPiece>() == null)
            FreezeSlot(slotTr);
    }

    private void FreezeSlot(Transform slot)
    {
        if (frozenSlots.ContainsKey(slot)) return;

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

    // ================= Shuffle =================

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

    private int GetNonFrozenSlotCount(Direction d)
    {
        if (!zoneSlots.ContainsKey(d)) return 0;

        int count = 0;
        for (int i = 0; i < zoneSlots[d].Count; i++)
        {
            Transform s = zoneSlots[d][i];
            if (!s) continue;
            if (frozenSlots.ContainsKey(s)) continue;
            count++;
        }
        return count;
    }

    private Quaternion GetZonePieceWorldRotation(Direction d)
    {
        // “Araba yönü” slotların yönüyle sabit kalsın
        // Up/Down dikey, Left/Right yatay gibi düşün.
        return d switch
        {
            Direction.Up => Quaternion.Euler(0f, 90f, 0f),
            Direction.Down => Quaternion.Euler(0f, -90f, 0f),
            Direction.Left => Quaternion.Euler(0f, 0f, 0f),
            Direction.Right => Quaternion.Euler(0f, 180f, 0f),
            _ => Quaternion.identity
        };
    }

    private System.Collections.IEnumerator ShuffleContentsOnce()
    {
        if (isShuffling) yield break;

        int total = 0;
        foreach (var list in grid.Values) total += list.Count;
        if (total == 0) yield break;

        isShuffling = true;
        bool prevInput = inputEnabled;
        inputEnabled = false;

        MarkActivity();

        Direction[] fromDirs = (Direction[])dirs.Clone();
        Direction[] toDirs = (Direction[])dirs.Clone();

        const int maxPermutationTries = 25;
        bool foundFeasible = false;

        for (int attempt = 0; attempt < maxPermutationTries; attempt++)
        {
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

            if (ok) { foundFeasible = true; break; }
        }

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
                if (list[i]) list[i].transform.SetParent(null, true);
        }

        Sequence master = DOTween.Sequence();

        foreach (var d in dirs)
        {
            var list = newGrid[d];
            var slots = zoneSlots[d];
            if (slots == null || slots.Count == 0) continue;

            List<Transform> availableSlots = new List<Transform>();
            for (int i = 0; i < slots.Count; i++)
            {
                Transform s = slots[i];
                if (!s) continue;
                if (frozenSlots.ContainsKey(s)) continue;
                if (s.GetComponentInChildren<FallingPiece>() != null) continue;
                availableSlots.Add(s);
            }

            int moveCount = Mathf.Min(list.Count, availableSlots.Count);

            for (int i = 0; i < moveCount; i++)
            {
                FallingPiece p = list[i];
                if (!p) continue;

                Transform targetSlot = availableSlots[i];

                // ✅ scale asla oynamasın
                Vector3 worldScaleBefore = p.transform.lossyScale;

                p.transform.SetParent(targetSlot, true);

                Quaternion targetRot = GetZonePieceWorldRotation(d);

                Sequence pieceSeq = DOTween.Sequence();
                pieceSeq.Join(p.transform.DOMove(targetSlot.position, shuffleMoveDuration).SetEase(shuffleEase));
                pieceSeq.Join(p.transform.DORotateQuaternion(targetRot, shuffleMoveDuration).SetEase(shuffleEase));

                pieceSeq.OnComplete(() =>
                {
                    if (!p || !targetSlot) return;

                    p.transform.SetParent(targetSlot, true);
                    p.transform.localPosition = Vector3.zero;
                    p.transform.rotation = targetRot;

                    KeepWorldScale(p.transform, worldScaleBefore);
                });

                master.Join(pieceSeq);
            }
        }

        yield return master.WaitForCompletion();

        grid = newGrid;

        inputEnabled = prevInput;
        isShuffling = false;

        MarkActivity();
    }

    // ================= Manual SetupGrid =================

    public void SetupGrid(int slotsPerZone)
    {
        Debug.Log($"[SlotManager] SetupGrid called (manual slots). slotsPerZone ignored: {slotsPerZone}");

        foreach (var kv in grid) kv.Value.Clear();
        frozenSlots.Clear();

        CacheManualSlots();

        StartShuffleRoutineIfNeeded();
        MarkActivity();
    }

    // ✅ BURASI: iç içe spawn fix
    public void ResetBoard()
    {
        // center queue temizle
        while (centerQueue.Count > 0)
        {
            var p = centerQueue.Dequeue();
            if (p != null) Destroy(p.gameObject);
        }

        // grid temizle
        foreach (var kv in grid)
        {
            foreach (var p in kv.Value)
                if (p) Destroy(p.gameObject);

            kv.Value.Clear();
        }

        // ✅ SAHNEDE KALMIŞ HER ŞEYİ TEMİZLE (slot altında olsun/olmasın)
        var allPieces = FindObjectsOfType<FallingPiece>(true);
        for (int i = 0; i < allPieces.Length; i++)
            if (allPieces[i]) Destroy(allPieces[i].gameObject);

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

        // Center tap
        if (centerQueue.Count > 0 && centerQueue.Peek() == hitPiece)
        {
            var sp = FindObjectOfType<Spawner>();
            if (sp) sp.NotifyCenterCleared(hitPiece);

            centerQueue.Dequeue();
            Destroy(hitPiece.gameObject);

            if (sp) sp.SpawnNextPiece();
            return;
        }

        // Grid tap: sadece arabaları (joker değil) tıklayıp silebilirsin istersen
        if (hitPiece.isJoker) return;

        if (!TryFindPieceInGrid(hitPiece, out Direction dir, out int index)) return;

        if (LevelManager.Instance) LevelManager.Instance.ReduceLife();

        RemoveFromGridAt(dir, index);
        Destroy(hitPiece.gameObject);
    }

    private bool TryGetFirstFrozenEmptySlot(Direction dir, out Transform frozenSlot)
    {
        frozenSlot = null;
        if (!zoneSlots.ContainsKey(dir)) return false;

        var slots = zoneSlots[dir];
        for (int i = 0; i < slots.Count; i++)
        {
            Transform slot = slots[i];
            if (!slot) continue;

            if (!frozenSlots.ContainsKey(slot)) continue;
            if (slot.GetComponentInChildren<FallingPiece>() != null) continue;

            frozenSlot = slot;
            return true;
        }
        return false;
    }

    private bool TryGetFirstEmptySlot(Direction dir, out Transform emptySlot)
    {
        emptySlot = null;
        if (!zoneSlots.ContainsKey(dir)) return false;

        var slots = zoneSlots[dir];
        for (int i = 0; i < slots.Count; i++)
        {
            Transform s = slots[i];
            if (!s) continue;

            if (frozenSlots.ContainsKey(s)) continue;
            if (s.GetComponentInChildren<FallingPiece>() != null) continue;

            emptySlot = s;
            return true;
        }
        return false;
    }

    private void OnSwipe(Direction dir)
    {
        MarkActivity();
        if (!inputEnabled || isShuffling) return;
        if (centerQueue.Count == 0) return;

        dir = ApplyInversion(dir);

        if (!zoneSlots.ContainsKey(dir) || zoneSlots[dir].Count == 0) return;

        FallingPiece peekPiece = centerQueue.Peek();
        if (!peekPiece) return;

        // ✅ JOKER: bu yönde frozen varsa gidip aç (1 joker = 1 frozen)
        if (peekPiece.isJoker)
        {
            if (TryGetFirstFrozenEmptySlot(dir, out Transform frozenSlot))
            {
                FallingPiece joker = centerQueue.Dequeue();

                var sp = FindObjectOfType<Spawner>();
                if (sp) sp.NotifyCenterCleared(joker);

                Vector3 worldScaleBefore = joker.transform.lossyScale;
                Quaternion targetRot = GetZonePieceWorldRotation(dir);

                Sequence seq = DOTween.Sequence();
                seq.Join(joker.transform.DOMove(frozenSlot.position, moveDuration).SetEase(Ease.OutQuad));
                seq.Join(joker.transform.DORotateQuaternion(targetRot, moveDuration).SetEase(Ease.OutQuad));
                seq.OnComplete(() =>
                {
                    UnfreezeSlot(frozenSlot);
                    if (joker) Destroy(joker.gameObject);
                });

                if (sp) sp.SpawnNextPiece();
            }
            else
            {
                // buz yoksa: jokeri çöpe (tap ile de zaten gider)
                FallingPiece joker = centerQueue.Dequeue();
                var sp = FindObjectOfType<Spawner>();
                if (sp) sp.NotifyCenterCleared(joker);
                if (joker) Destroy(joker.gameObject);
                if (sp) sp.SpawnNextPiece();
            }
            return;
        }

        // normal araç yerleştir
        int capacity = zoneSlots[dir].Count;
        if (grid[dir].Count >= capacity)
        {
            if (LevelManager.Instance) LevelManager.Instance.ReduceLife();
            return;
        }

        if (!TryGetFirstEmptySlot(dir, out Transform targetSlot))
        {
            if (LevelManager.Instance) LevelManager.Instance.ReduceLife();
            return;
        }

        if (frozenSlots.ContainsKey(targetSlot))
        {
            // frozen'a araba atamaz (joker açar)
            return;
        }

        FallingPiece piece2 = centerQueue.Dequeue();
        var sp2 = FindObjectOfType<Spawner>();
        if (sp2) sp2.NotifyCenterCleared(piece2);

        piece2.TweenToSlot(targetSlot, moveDuration, moveEase, () =>
        {
            RegisterPiece(dir, piece2);
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
        if (!zoneSlots.ContainsKey(dir)) return;

        var slots = zoneSlots[dir];
        if (slots == null || slots.Count == 0) return;

        int write = 0;
        for (int i = 0; i < slots.Count; i++)
        {
            Transform slot = slots[i];
            if (!slot) continue;

            if (frozenSlots.ContainsKey(slot)) continue;
            if (write >= list.Count) break;

            FallingPiece p = list[write];
            if (!p) { write++; continue; }

            p.transform.SetParent(slot, true);
            p.TweenToSlot(slot, moveDuration, moveEase, null);

            write++;
        }
    }

    // ================= Match3 =================

    private void CheckMatch3(Direction dir)
    {
        var list = grid[dir];

        int capacity = zoneSlots.ContainsKey(dir) ? zoneSlots[dir].Count : 0;
        if (capacity <= 0) return;
        if (list.Count < capacity) return;

        // joker match'e girmez
        for (int i = 0; i < list.Count; i++)
            if (list[i] && list[i].isJoker) return;

        string targetKey = list[0] != null ? list[0].GetPieceKey() : null;
        Color targetColorFallback = list[0] != null ? list[0].GetNormalColor() : Color.white;

        foreach (var p in list)
        {
            if (!p) return;
            if (p.isFrozen) return;

            if (!string.IsNullOrEmpty(targetKey))
            {
                if (p.GetPieceKey() != targetKey) return;
            }
            else
            {
                if (p.GetNormalColor() != targetColorFallback) return;
            }
        }

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
    }
}
