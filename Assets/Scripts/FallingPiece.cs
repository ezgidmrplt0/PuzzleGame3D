using UnityEngine;
using DG.Tweening;

public class FallingPiece : MonoBehaviour
{
    public enum Type 
    { 
        Object1, 
        Object2, 
        Object3, 
        Object4, 
        Object5, 
        Object6,
        Object7,
        Object8,
        Object9,
        Object10,
        Object11,
        Object12,
        Object13,
        Object14,
        Object15,
        Object16,
        Object17,
        Object18,
        Object19,
        Object20,
        ObjectHeart
    }
    
    [Header("Settings")]
    public Type pieceType; // Set this in Prefab!

    public bool isFake { get; private set; }
    public bool isFrozen { get; private set; }
    public int freezeHealth { get; private set; } = 3;

    [Header("Visuals")]
    [SerializeField] private Texture faceTexture; // Drag your image here

    private MeshRenderer meshRenderer;
    private Color originalColor;

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
            originalColor = meshRenderer.material.color;
            
            // Apply Texture if assigned
            if (faceTexture != null)
            {
                meshRenderer.material.mainTexture = faceTexture;
            }
        }
    }

    public void SetFake(bool fake)
    {
        isFake = fake;
        if (meshRenderer)
            meshRenderer.material.color = fake ? Color.black : originalColor;
    }

    public void SetFrozen(bool frozen)
    {
        isFrozen = frozen;
        if (frozen)
        {
            freezeHealth = 3;
            if (meshRenderer) meshRenderer.material.color = Color.cyan;
        }
        else
        {
            if (meshRenderer)
                meshRenderer.material.color = isFake ? Color.black : originalColor;
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
