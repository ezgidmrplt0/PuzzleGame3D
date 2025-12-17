using System.Collections;
using UnityEngine;
using DG.Tweening;

public class FallingPiece : MonoBehaviour
{
    public enum Type { Cube, Sphere }
    public Type pieceType;

    Rigidbody rb;
    Collider col;
    Tween activeTween;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (!rb) rb = gameObject.AddComponent<Rigidbody>();

        col = GetComponent<Collider>();
    }

    // Swipe ile slota "uçur"
    public void TweenToSlot(Transform slotPoint, float duration, Ease ease, System.Action onComplete = null)
    {
        if (activeTween != null && activeTween.IsActive()) activeTween.Kill();

        // Fizikten çýkar
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.useGravity = false;
        rb.isKinematic = true;

        // Çarpýþmayý kapatmak iyi olur (yolda bir þeylere takýlmasýn)
        if (col) col.enabled = false;

        // Ýstersen küçük bir "kaybolup belirme" hissi için scale de ekleyebilirsin (opsiyonel)
        // transform.DOScale(0.9f, duration * 0.3f).SetLoops(2, LoopType.Yoyo);

        activeTween = transform.DOMove(slotPoint.position, duration)
            .SetEase(ease)
            .OnComplete(() =>
            {
                // Slotun çocuðu yap: sahnede düzgün dursun
                transform.SetParent(slotPoint, true);
                transform.position = slotPoint.position;

                onComplete?.Invoke();
            });
    }

    private void OnDestroy()
    {
        if (activeTween != null && activeTween.IsActive())
            activeTween.Kill();
    }
}
