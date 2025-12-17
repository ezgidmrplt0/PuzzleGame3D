using UnityEngine;

public class SwipeInput : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private SlotManager slotManager;

    [Header("Swipe Settings")]
    [SerializeField] private float minSwipeDistance = 60f;     // piksel
    [SerializeField] private float horizontalBias = 1.2f;      // yatay > dikey*1.2 ise kabul et

    private Vector2 startPos;
    private bool isDown;

    void Update()
    {
        // ---- TOUCH (Mobil / Emulator) ----
        if (Input.touchCount > 0)
        {
            Touch t = Input.GetTouch(0);

            if (t.phase == TouchPhase.Began)
            {
                startPos = t.position;
                isDown = true;
            }
            else if ((t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) && isDown)
            {
                HandleSwipe(t.position);
                isDown = false;
            }

            return; // touch varken mouse okuma
        }

        // ---- MOUSE (PC / Editor) ----
        if (Input.GetMouseButtonDown(0))
        {
            startPos = (Vector2)Input.mousePosition;
            isDown = true;
        }
        else if (Input.GetMouseButtonUp(0) && isDown)
        {
            HandleSwipe((Vector2)Input.mousePosition);
            isDown = false;
        }
    }

    private void HandleSwipe(Vector2 endPos)
    {
        if (slotManager == null) return;

        Vector2 delta = endPos - startPos;

        // yatay mý?
        if (Mathf.Abs(delta.x) < minSwipeDistance) return;
        if (Mathf.Abs(delta.x) < Mathf.Abs(delta.y) * horizontalBias) return;

        if (delta.x > 0)
        {
            // saða swipe -> sað slot: Sphere
            slotManager.PlaceRightSphere();
        }
        else
        {
            // sola swipe -> sol slot: Cube
            slotManager.PlaceLeftCube();
        }
    }
}
