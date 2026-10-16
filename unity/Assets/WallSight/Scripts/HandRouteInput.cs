using UnityEngine;
using UnityEngine.InputSystem;

namespace WallSight
{
    /// <summary>
    /// HoloLens 2 hand control for the route planner (OpenXR Microsoft Hand Interaction profile, no MRTK).
    /// Each hand casts a ray from its aim pose onto the map floor and shows a cursor dot.
    /// Right hand: pinch = set target; pinch and hold, then move = drag the target (route re-plans live).
    /// Left hand: pinch = add / remove a simulated hazard.
    /// </summary>
    public class HandRouteInput : MonoBehaviour
    {
        public RoutePlanner planner;
        [Tooltip("Longest ray drawn when it does not hit the map floor")]
        public float maxRayMetres = 2f;
        [Tooltip("While dragging, re-plan at most this often")]
        public float dragReplanSeconds = 0.12f;
        public Color rayColor = new Color(1f, 1f, 1f, 0.8f);
        public Color cursorColor = new Color(1f, 0.85f, 0.1f);

        class Hand
        {
            public InputAction position, rotation, select, tracked;
            public LineRenderer ray;
            public Transform cursor;
        }

        Hand right, left;
        float lastDragReplan;
        Vector3 lastDragPoint;

        void Awake()
        {
            right = CreateHand("RightHand");
            left = CreateHand("LeftHand");
        }

        void OnEnable()
        {
            foreach (var h in new[] { right, left })
            {
                h.position.Enable(); h.rotation.Enable(); h.select.Enable(); h.tracked.Enable();
            }
        }

        void OnDisable()
        {
            foreach (var h in new[] { right, left })
            {
                h.position.Disable(); h.rotation.Disable(); h.select.Disable(); h.tracked.Disable();
                h.ray.enabled = false;
                h.cursor.gameObject.SetActive(false);
            }
        }

        void OnDestroy()
        {
            foreach (var h in new[] { right, left })
            {
                h.position.Dispose(); h.rotation.Dispose(); h.select.Dispose(); h.tracked.Dispose();
            }
        }

        void Update()
        {
            if (planner == null || planner.scanRoot == null) return;

            if (UpdateHand(right, out var rightHit))
            {
                if (right.select.WasPressedThisFrame())
                {
                    planner.SetTargetWorld(rightHit);
                    lastDragPoint = rightHit;
                    lastDragReplan = Time.time;
                }
                else if (right.select.IsPressed() && Time.time - lastDragReplan >= dragReplanSeconds
                         && Vector3.Distance(rightHit, lastDragPoint) > 0.005f)
                {
                    planner.SetTargetWorld(rightHit);
                    lastDragPoint = rightHit;
                    lastDragReplan = Time.time;
                }
            }

            if (UpdateHand(left, out var leftHit) && left.select.WasPressedThisFrame())
                planner.ToggleHazardWorld(leftHit);
        }

        /// Draws the hand ray; true when the hand is tracked and the ray hits the map floor.
        bool UpdateHand(Hand h, out Vector3 hit)
        {
            hit = default;
            bool isTracked = h.tracked.ReadValue<float>() > 0.5f;
            h.ray.enabled = isTracked;
            if (!isTracked)
            {
                h.cursor.gameObject.SetActive(false);
                return false;
            }

            var origin = h.position.ReadValue<Vector3>();
            var direction = h.rotation.ReadValue<Quaternion>() * Vector3.forward;
            var floor = new Plane(planner.scanRoot.up, planner.scanRoot.position); // scan floor = local y 0
            bool onFloor = floor.Raycast(new Ray(origin, direction), out float distance) && distance <= maxRayMetres;
            var end = origin + direction * (onFloor ? distance : maxRayMetres);

            h.ray.SetPosition(0, origin);
            h.ray.SetPosition(1, end);
            h.cursor.gameObject.SetActive(onFloor);
            if (onFloor)
            {
                h.cursor.position = end;
                hit = end;
            }
            return onFloor;
        }

        Hand CreateHand(string side)
        {
            string device = "<HoloLensHand>{" + side + "}";
            var h = new Hand
            {
                position = new InputAction(side + "AimPosition", InputActionType.Value, device + "/pointerPosition"),
                rotation = new InputAction(side + "AimRotation", InputActionType.Value, device + "/pointerRotation"),
                select = new InputAction(side + "Pinch", InputActionType.Button, device + "/selectPressed"),
                tracked = new InputAction(side + "Tracked", InputActionType.Button, device + "/isTracked"),
            };

            var rayObject = new GameObject(side + "Ray");
            rayObject.transform.SetParent(transform, false);
            h.ray = rayObject.AddComponent<LineRenderer>();
            h.ray.useWorldSpace = true;
            h.ray.positionCount = 2;
            h.ray.widthMultiplier = 0.003f;
            h.ray.material = RoutePlanner.UnlitMaterial(rayColor);
            h.ray.startColor = h.ray.endColor = rayColor;
            h.ray.enabled = false;

            var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dot.name = side + "Cursor";
            Destroy(dot.GetComponent<Collider>());
            dot.transform.SetParent(transform, false);
            dot.transform.localScale = Vector3.one * 0.012f;
            dot.GetComponent<Renderer>().material = RoutePlanner.UnlitMaterial(side == "LeftHand" ? new Color(1f, 0.3f, 0.2f) : cursorColor);
            dot.SetActive(false);
            h.cursor = dot.transform;
            return h;
        }
    }
}
