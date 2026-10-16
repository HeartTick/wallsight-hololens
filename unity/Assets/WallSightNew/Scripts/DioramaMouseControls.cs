
using UnityEngine;
using UnityEngine.EventSystems;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class DioramaMouseControls : MonoBehaviour
{
    [SerializeField] private float rotationSpeed = 0.3f;
    [SerializeField] private float zoomStep = 0.04f;

    private Camera viewCamera;
    private Vector2 lastMousePosition;
    private bool dragging;

    private void Awake()
    {
        viewCamera = Camera.main;
    }

    private void Update()
    {
        Vector2 mousePosition;
        bool mouseDown;
        bool mouseHeld;
        float scroll;

#if ENABLE_INPUT_SYSTEM

        Mouse mouse = Mouse.current;

        if (mouse == null) return;

        mousePosition = mouse.position.ReadValue();
        mouseDown = mouse.leftButton.wasPressedThisFrame;
        mouseHeld = mouse.leftButton.isPressed;
        scroll = mouse.scroll.ReadValue().y / 120f;

#elif ENABLE_LEGACY_INPUT_MANAGER

        mousePosition = Input.mousePosition;
        mouseDown = Input.GetMouseButtonDown(0);
        mouseHeld = Input.GetMouseButton(0);
        scroll = Input.mouseScrollDelta.y;

#else
        return;
#endif

        // Start dragging only when not clicking the UI.
        if (mouseDown)
        {
            dragging = EventSystem.current == null ||
                !EventSystem.current.IsPointerOverGameObject();

            lastMousePosition = mousePosition;
        }

        if (!mouseHeld)
        {
            dragging = false;
        }

        // Rotate the entire diorama.
        if (dragging && mouseHeld)
        {
            float deltaX =
                mousePosition.x - lastMousePosition.x;

            transform.Rotate(
                Vector3.up,
                -deltaX * rotationSpeed,
                Space.World
            );
        }

        lastMousePosition = mousePosition;

        // Zoom using the orthographic presentation camera.
        if (viewCamera != null && viewCamera.orthographic)
        {
            if (Mathf.Abs(scroll) > 0.001f)
            {
                viewCamera.orthographicSize = Mathf.Clamp(
                    viewCamera.orthographicSize - scroll * zoomStep,
                    0.25f,
                    0.65f
                );
            }
        }
    }
}
