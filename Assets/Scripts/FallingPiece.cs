using UnityEngine;
using DG.Tweening;

public class FallingPiece : MonoBehaviour
{
    public string pieceKey { get; private set; }

    [Header("Visuals")]
    [SerializeField] private Texture faceTexture;

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

    public void SetPieceKey(string key) => pieceKey = key;
    public string GetPieceKey() => pieceKey;

    public void SetNormalColor(Color c)
    {
        normalColor = c;
        RefreshVisualState();
    }

    public Color GetNormalColor() => normalColor;

    private void RefreshVisualState()
    {
        if (!meshRenderer) return;

        meshRenderer.material.color = normalColor;
        meshRenderer.material.mainTexture = normalTexture;
    }

    // TakeDamage removed

    public void TweenToSlot(Transform targetSlot, float duration, Ease ease, System.Action onComplete)
    {
        if (activeTween != null && activeTween.IsActive()) activeTween.Kill();
        if (rb == null) return;

        if (!rb.isKinematic)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        rb.useGravity = false;
        rb.isKinematic = true;

        if (col) col.enabled = false;

        Vector3 startPos = transform.position;
        Vector3 endPos = targetSlot.position;
        Vector3 dir = (endPos - startPos).normalized;

        // Hedef rotasyon: Park ettiğinde merkeze baksın
        // Sol taraftaki slot (X < 0) -> Sağa baksın (+X)
        // Sağ taraftaki slot (X > 0) -> Sola baksın (-X)
        // Yukarıdaki slot (Z > 0) -> Aşağı baksın (-Z)
        // Aşağıdaki slot (Z < 0) -> Yukarı baksın (+Z)
        
        Quaternion targetRot = Quaternion.identity;

        // Slotun merkezden konumuna göre yön belirle
        // (EndPos slot konumu olduğu için doğrudan kullanılabilir)
        if (Mathf.Abs(endPos.x) > Mathf.Abs(endPos.z))
        {
            // Yatay Zone
            if (endPos.x < 0) targetRot = Quaternion.Euler(0, 90, 0); // Solda, sağa bak
            else targetRot = Quaternion.Euler(0, -90, 0); // Sağda, sola bak
        }
        else
        {
            // Dikey Zone
            if (endPos.z > 0) targetRot = Quaternion.Euler(0, 180, 0); // Yukarda, aşağı bak
            else targetRot = Quaternion.Euler(0, 0, 0); // Aşağıda, yukarı bak (0 derece)
        }

        // Sequence: Move + Rotate
        Sequence seq = DOTween.Sequence();
        
        seq.Join(transform.DOMove(endPos, duration).SetEase(ease));
        // Yol boyunca hedefe dön
        seq.Join(transform.DORotateQuaternion(targetRot, duration * 0.8f).SetEase(Ease.OutSine));

        seq.OnComplete(() =>
        {
            if (this == null) return;
            transform.SetParent(targetSlot);
            transform.localPosition = Vector3.zero;
            transform.rotation = targetRot;

            if (col) col.enabled = true;
            onComplete?.Invoke();
        });

        activeTween = seq;
    }

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
