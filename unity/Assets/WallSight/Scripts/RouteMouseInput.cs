using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace WallSight
{
    /// <summary>
    /// Desktop test input for the route planner. Right-click on the map floor:
    /// plain = target, Shift = start, H held = add/remove simulated hazard. C = clear hazards.
    /// Right-click so it does not clash with left-drag rotation of the diorama.
    /// </summary>
    public class RouteMouseInput : MonoBehaviour
    {
        public RoutePlanner planner;
        public Camera viewCamera;
        [Tooltip("A right-click that moves more than this many pixels is a drag, not a click")]
        public float maxClickDragPixels = 6f;

        Vector2 downPosition;

        void Update()
        {
            if (planner == null || planner.scanRoot == null) return;
            var cam = viewCamera != null ? viewCamera : Camera.main;
            if (RightPressed()) downPosition = MousePosition();
            if (RightReleased() && (MousePosition() - downPosition).magnitude <= maxClickDragPixels
                && FloorPoint(cam, MousePosition(), out var point))
            {
                if (ShiftHeld()) planner.SetStartWorld(point);
                else if (HazardKeyHeld()) planner.ToggleHazardWorld(point);
                else planner.SetTargetWorld(point);
            }
            if (ClearPressed()) planner.ClearHazards();
        }

        bool FloorPoint(Camera cam, Vector2 screen, out Vector3 world)
        {
            var ray = cam.ScreenPointToRay(screen);
            var floor = new Plane(planner.scanRoot.up, planner.scanRoot.position); // scan floor is local y = 0
            world = default;
            if (!floor.Raycast(ray, out float distance)) return false;
            world = ray.GetPoint(distance);
            return true;
        }

#if ENABLE_INPUT_SYSTEM
        static Vector2 MousePosition() => Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
        static bool RightPressed() => Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
        static bool RightReleased() => Mouse.current != null && Mouse.current.rightButton.wasReleasedThisFrame;
        static bool ShiftHeld() => Keyboard.current != null && Keyboard.current.shiftKey.isPressed;
        static bool HazardKeyHeld() => Keyboard.current != null && Keyboard.current.hKey.isPressed;
        static bool ClearPressed() => Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame;
#else
        static Vector2 MousePosition() => Input.mousePosition;
        static bool RightPressed() => Input.GetMouseButtonDown(1);
        static bool RightReleased() => Input.GetMouseButtonUp(1);
        static bool ShiftHeld() => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        static bool HazardKeyHeld() => Input.GetKey(KeyCode.H);
        static bool ClearPressed() => Input.GetKeyDown(KeyCode.C);
#endif
    }
}
