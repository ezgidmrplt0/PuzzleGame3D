using UnityEngine;
using System;

public class SwipeInput : MonoBehaviour
{
    public static event Action<Direction> OnSwipe; // Up, Down, Left, Right
    public static event Action<Vector2> OnTap;

    [Header("Settings")]
    [SerializeField] private float minSwipeDistance = 50f;
    [SerializeField] private float tapThreshold = 0.2f; // Seconds
    [SerializeField] private float maxTapMovement = 20f; // Pixels

    private Vector2 startPos;
    private float startTime;
    private bool isTouching;

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            startPos = Input.mousePosition;
            startTime = Time.time;
            isTouching = true;
        }
        else if (Input.GetMouseButtonUp(0) && isTouching)
        {
            HandleInput(Input.mousePosition);
            isTouching = false;
        }
    }

    private void HandleInput(Vector2 endPos)
    {
        Vector2 delta = endPos - startPos;
        float timeDelta = Time.time - startTime;
        float distance = delta.magnitude;

        // TAP
        if (timeDelta < tapThreshold && distance < maxTapMovement)
        {
            Debug.Log("[Input] Tap Detected");
            OnTap?.Invoke(endPos);
            return;
        }

        // SWIPE
        if (distance >= minSwipeDistance)
        {
            float x = delta.x;
            float y = delta.y;

            if (Mathf.Abs(x) > Mathf.Abs(y))
            {
                Direction dir = (x > 0) ? Direction.Right : Direction.Left;
                OnSwipe?.Invoke(dir);
            }
            else
            {
                Direction dir = (y > 0) ? Direction.Up : Direction.Down;
                OnSwipe?.Invoke(dir);
            }
        }
    }
}
