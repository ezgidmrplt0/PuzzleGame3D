using UnityEngine;
using DG.Tweening;

public class FallingPiece : MonoBehaviour
{
    public bool isFake { get; private set; }
    public bool isFrozen { get; private set; }
    public int freezeHealth { get; private set; } = 3;

    [Header("Normal Visuals")]
    [Tooltip("Normal durumdayken uygulanacak texture (opsiyonel).")]
    [SerializeField] private Texture faceTexture;   // normal texture (optional)

    [Header("Frozen Visuals")]
    [SerializeField] private Texture iceTexture;
    [SerializeField] private Color iceColor = Color.cyan;

    [Header("Fake Visuals")]
    [SerializeField] private Texture fakeTexture;
    [SerializeField] private Color fakeColor = Color.white;

    private MeshRenderer meshRenderer;

    private Color normalColor = Color.white;        // <<< MATCH3 bununla yapılır
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

        // Priority: Frozen > Fake > Normal
        if (isFrozen)
        {
            meshRenderer.material.color = iceColor;

            if (iceTexture != null)
                meshRenderer.material.mainTexture = iceTexture;
            else
                meshRenderer.material.mainTexture = normalTexture;
        }
        else if (isFake)
        {
            meshRenderer.material.color = fakeColor;

            if (fakeTexture != null)
                meshRenderer.material.mainTexture = fakeTexture;
            else
                meshRenderer.material.mainTexture = normalTexture;
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
        transform.DOShakeScale(0.15f, 0.2f);
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

    private void OnDestroy()
    {
        if (activeTween != null && activeTween.IsActive())
            activeTween.Kill();
    }
}
