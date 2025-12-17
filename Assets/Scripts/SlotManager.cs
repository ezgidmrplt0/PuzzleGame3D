using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class SlotManager : MonoBehaviour
{
    [Header("Slot Points (Top -> Bottom)")]
    public List<Transform> leftSlotPoints;   // L0,L1,L2
    public List<Transform> rightSlotPoints;  // R0,R1,R2

    [Header("Tween Settings")]
    public float moveToSlotDuration = 0.25f;
    public Ease moveEase = Ease.OutBack;

    [Header("Scoring")]
    public int scorePerTriple = 10;
    public float clearDelay = 0.35f; // 3. taşı gördükten sonra temizle
    public int Score { get; private set; }

    // Düşen objeler kuyruğu (gerçek objeler)
    private readonly Queue<FallingPiece> cubes = new Queue<FallingPiece>();
    private readonly Queue<FallingPiece> spheres = new Queue<FallingPiece>();

    // Kolon içindeki gerçek objeler
    private readonly List<FallingPiece> leftPieces = new List<FallingPiece>();
    private readonly List<FallingPiece> rightPieces = new List<FallingPiece>();

    private bool leftClearing, rightClearing;

    private void OnEnable() => Spawner.OnPieceSpawned += OnSpawned;
    private void OnDisable() => Spawner.OnPieceSpawned -= OnSpawned;

    private void OnSpawned(FallingPiece fp)
    {
        if (fp == null) return;

        if (fp.pieceType == FallingPiece.Type.Cube) cubes.Enqueue(fp);
        else spheres.Enqueue(fp);
    }

    // SwipeInput çağırır:
    public void PlaceLeftCube()
    {
        if (leftClearing) return;
        if (leftPieces.Count >= 3) return;
        if (cubes.Count == 0) return;

        var fp = cubes.Dequeue();
        if (fp == null) return;

        int idx = leftPieces.Count; // 0-1-2
        Transform target = leftSlotPoints[idx];

        // Düşen objeyi slota uçur
        fp.TweenToSlot(target, moveToSlotDuration, moveEase, () =>
        {
            leftPieces.Add(fp);

            // 3 olduysa önce göster, sonra temizle
            if (leftPieces.Count == 3)
                StartCoroutine(ClearIfTripleAfterDelay(leftPieces, true));
        });
    }

    public void PlaceRightSphere()
    {
        if (rightClearing) return;
        if (rightPieces.Count >= 3) return;
        if (spheres.Count == 0) return;

        var fp = spheres.Dequeue();
        if (fp == null) return;

        int idx = rightPieces.Count; // 0-1-2
        Transform target = rightSlotPoints[idx];

        fp.TweenToSlot(target, moveToSlotDuration, moveEase, () =>
        {
            rightPieces.Add(fp);

            if (rightPieces.Count == 3)
                StartCoroutine(ClearIfTripleAfterDelay(rightPieces, false));
        });
    }

    private IEnumerator ClearIfTripleAfterDelay(List<FallingPiece> col, bool isLeft)
    {
        if (isLeft) leftClearing = true; else rightClearing = true;

        // 3. taşı bir an gör
        yield return new WaitForSeconds(clearDelay);

        // Hepsi aynı mı? (sol zaten sadece cube, sağ sadece sphere olduğu için bu hep true)
        // Ama ileride karışık yaparsan diye güvenli kontrol:
        bool allSame = col.Count == 3 &&
                       col[0] != null && col[1] != null && col[2] != null &&
                       col[0].pieceType == col[1].pieceType && col[1].pieceType == col[2].pieceType;

        if (allSame)
        {
            Score += scorePerTriple;
            Debug.Log("SCORE: " + Score);

            // Slotları boşalt (objeleri yok et)
            for (int i = col.Count - 1; i >= 0; i--)
            {
                if (col[i] != null) Destroy(col[i].gameObject);
            }
            col.Clear();
        }

        if (isLeft) leftClearing = false; else rightClearing = false;
    }
}
