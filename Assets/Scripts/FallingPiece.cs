using UnityEngine;
using DG.Tweening;

public class FallingPiece : MonoBehaviour
{
    public bool isFake { get; private set; }
    public bool isFrozen { get; private set; }
    public int freezeHealth { get; private set; } = 3;

    [Header("Normal Visuals")]
    [SerializeField] private Texture faceTexture;

    [Header("Frozen Visuals")]
    [SerializeField] private Texture iceTexture;
    [SerializeField] private Color iceColor = Color.cyan;

    [Header("Fake Visuals")]
    [SerializeField] private Texture fakeTexture;
    [SerializeField] private Color fakeColor = Color.white;

    private MeshRenderer meshRenderer;

    private Color normalColor = Color.white;
    private Texture normalTexture = null;

    private Rigidbody rb;
    private Collider col;
    private Tween activeTween;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (!rb) rb = gameObject.AddComponent<Rigidbody>();

        col = GetComponent<Collider>();
        meshRenderer = GetComponent<MeshRenderer>();

        if (meshRenderer)
        {
            if (faceTexture != null)
                meshRenderer.material.mainTexture = faceTexture;

            normalTexture = meshRenderer.material.mainTexture;
        }

        RefreshVisualState();
    }

    public void SetNormalColor(Color c)
    {
        normalColor = c;
        RefreshVisualState();
    }

    public Color GetNormalColor() => normalColor;

    public void SetFake(bool fake)
    {
        isFake = fake;
        RefreshVisualState();
    }

    public void SetFrozen(bool frozen)
    {
        isFrozen = frozen;
        if (frozen) freezeHealth = 3;
        RefreshVisualState();
    }

    private void RefreshVisualState()
    {
        if (!meshRenderer) return;

        if (isFrozen)
        {
            meshRenderer.material.color = iceColor;
            meshRenderer.material.mainTexture = (iceTexture != null) ? iceTexture : normalTexture;
        }
        else if (isFake)
        {
            meshRenderer.material.color = fakeColor;
            meshRenderer.material.mainTexture = (fakeTexture != null) ? fakeTexture : normalTexture;
        }
        else
        {
            meshRenderer.material.color = normalColor;
            meshRenderer.material.mainTexture = normalTexture;
        }
    }

    public void TakeDamage()
    {
        freezeHealth--;

        // ✅ shake sonrası base scale’e dön (bitişte)
        Vector3 baseScale = transform.localScale;
        transform.DOShakeScale(0.15f, 0.2f).OnComplete(() =>
        {
            if (this != null && transform != null)
                transform.localScale = baseScale;
        });
    }

    public void TweenToSlot(Transform targetSlot, float duration, Ease ease, System.Action onComplete = null)
    {
        if (activeTween != null && activeTween.IsActive()) activeTween.Kill();
        if (rb == null) return;

        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.useGravity = false;
        rb.isKinematic = true;

        if (col) col.enabled = false;

        activeTween = transform.DOMove(targetSlot.position, duration)
            .SetEase(ease)
            .OnComplete(() =>
            {
                transform.SetParent(targetSlot);
                transform.localPosition = Vector3.zero;

                if (col) col.enabled = true;
                onComplete?.Invoke();
            });
    }

    // ✅ MATCH: balon gibi küçülüp yok olma (büyüme yok)
    public void PlayMatchShrinkAndDestroy(float duration, float shrinkScaleMultiplier)
    {
        if (activeTween != null && activeTween.IsActive()) activeTween.Kill();
        if (col) col.enabled = false;

        Vector3 original = transform.localScale;

        Sequence seq = DOTween.Sequence();
        seq.Append(transform.DOScale(original * shrinkScaleMultiplier, duration * 0.55f).SetEase(Ease.OutQuad));
        seq.Append(transform.DOScale(Vector3.zero, duration * 0.45f).SetEase(Ease.InBack));
        seq.OnComplete(() =>
        {
            if (this != null && gameObject != null)
                Destroy(gameObject);
        });

        activeTween = seq;
    }

    private void OnDestroy()
    {
        if (activeTween != null && activeTween.IsActive())
            activeTween.Kill();
    }
}
