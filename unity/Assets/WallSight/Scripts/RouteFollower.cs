using System.Collections.Generic;
using UnityEngine;

namespace WallSight
{
    /// <summary>
    /// A simulated robot marker that drives the current route on the map:
    /// "this is where the robot would go next". It does not command a real robot.
    /// </summary>
    public class RouteFollower : MonoBehaviour
    {
        public RoutePlanner planner;
        public float speedMetresPerSecond = 0.6f;
        public float pauseAtEndSeconds = 1.5f;
        public bool loop = true;
        [Tooltip("Optional robot model; a simple puck with a nose is built if empty")]
        public GameObject robotPrefab;

        /// The simulated robot (child of the scan root, so its local pose is in scan metres).
        public Transform Robot => robot;

        Transform robot;
        readonly List<Vector3> path = new List<Vector3>();
        float travelled, pathLength, pause;

        void Start()
        {
            robot = robotPrefab != null ? Instantiate(robotPrefab, planner.scanRoot, false).transform : BuildRobot();
            planner.RouteChanged += OnRouteChanged;
            OnRouteChanged(planner.RouteLocal);
        }

        void OnDestroy()
        {
            if (planner != null) planner.RouteChanged -= OnRouteChanged;
        }

        void OnRouteChanged(IReadOnlyList<Vector3> route)
        {
            path.Clear();
            path.AddRange(route);
            pathLength = 0f;
            for (int i = 1; i < path.Count; i++) pathLength += Vector3.Distance(path[i - 1], path[i]);
            travelled = 0f;
            pause = 0f;
            if (path.Count > 1) { robot.gameObject.SetActive(true); return; }
            // No route yet (or blocked): park the robot at the start marker so it is always visible.
            var parked = planner.StartLocal;
            robot.gameObject.SetActive(parked.HasValue);
            if (parked.HasValue) robot.localPosition = parked.Value;
        }

        void Update()
        {
            if (path.Count < 2) return;
            if (travelled >= pathLength)
            {
                pause += Time.deltaTime;
                if (loop && pause >= pauseAtEndSeconds) { travelled = 0f; pause = 0f; }
            }
            else travelled = Mathf.Min(pathLength, travelled + speedMetresPerSecond * Time.deltaTime);
            Place(travelled);
        }

        void Place(float distance)
        {
            for (int i = 1; i < path.Count; i++)
            {
                float segment = Vector3.Distance(path[i - 1], path[i]);
                if (distance <= segment || i == path.Count - 1)
                {
                    var p = Vector3.Lerp(path[i - 1], path[i], segment > 0f ? Mathf.Clamp01(distance / segment) : 1f);
                    p.y = 0f;
                    robot.localPosition = p;
                    var dir = path[i] - path[i - 1];
                    dir.y = 0f;
                    if (dir.sqrMagnitude > 1e-6f) robot.localRotation = Quaternion.LookRotation(dir);
                    return;
                }
                distance -= segment;
            }
        }

        Transform BuildRobot()
        {
            var root = new GameObject("SimulatedRobot").transform;
            root.SetParent(planner.scanRoot, false);
            Part(root, PrimitiveType.Cylinder, new Vector3(0f, 0.12f, 0f), new Vector3(0.45f, 0.1f, 0.45f), new Color(0.2f, 0.55f, 1f));
            Part(root, PrimitiveType.Cube, new Vector3(0f, 0.2f, 0.2f), new Vector3(0.12f, 0.08f, 0.12f), Color.white);
            return root;
        }

        static void Part(Transform parent, PrimitiveType type, Vector3 position, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().material = RoutePlanner.UnlitMaterial(color);
        }
    }
}
