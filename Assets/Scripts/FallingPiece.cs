using System.Collections;
using UnityEngine;
using DG.Tweening;

public class FallingPiece : MonoBehaviour
{
    public enum Type { Cube, Sphere }
    public Type pieceType;

    // State
    public bool isFake { get; private set; }
    public bool isFrozen { get; private set; }
    public int freezeHealth { get; private set; } = 3;

    // Visuals
    private MeshRenderer meshRenderer;
    private Color originalColor;
    
    // Components / Internal
    private Rigidbody rb;
    private Collider col;
    private Tween activeTween;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (!rb) rb = gameObject.AddComponent<Rigidbody>();

        col = GetComponent<Collider>();
        meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer) originalColor = meshRenderer.material.color;
    }

    public void SetFake(bool fake)
    {
        isFake = fake;
        if (meshRenderer)
        {
            // PROTOTYPE VISUAL: Red/Black for Fake
            meshRenderer.material.color = fake ? Color.black : originalColor;
        }
    }

    public void SetFrozen(bool frozen)
    {
        isFrozen = frozen;
        if (frozen)
        {
            freezeHealth = 3; // Reset health
            if (meshRenderer)
                meshRenderer.material.color = Color.cyan; // Ice color
        }
        else
        {
            // Restore visual if un-frozen (rare case)
             if (meshRenderer)
                meshRenderer.material.color = isFake ? Color.black : originalColor;
        }
    }

    public void TakeDamage()
    {
        freezeHealth--;
        // Visual feedback (shake or color flash) could go here
        transform.DOShakeScale(0.15f, 0.2f);
    }

    // UPDATED: Move to Slot Transform and Parent it
    public void TweenToSlot(Transform targetSlot, float duration, Ease ease, System.Action onComplete = null)
    {
        if (activeTween != null && activeTween.IsActive()) activeTween.Kill();

        // Disable physics
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.useGravity = false;
        rb.isKinematic = true;

        if (col) col.enabled = false;

        activeTween = transform.DOMove(targetSlot.position, duration)
            .SetEase(ease)
            .OnComplete(() =>
            {
                // Parent and Zero
                transform.SetParent(targetSlot);
                transform.localPosition = Vector3.zero;

                // Re-enable collider for Raycast interaction (Breaking ice)
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
