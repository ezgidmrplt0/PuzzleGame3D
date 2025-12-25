using System;
using UnityEngine;

public class JokerPickup : MonoBehaviour
{
    public static event Action<JokerPickup> OnPicked;

    [Header("Optional")]
    [SerializeField] private bool destroyOnPick = true;

    public void Pick()
    {
        OnPicked?.Invoke(this);
        if (destroyOnPick)
            Destroy(gameObject);
    }
}
