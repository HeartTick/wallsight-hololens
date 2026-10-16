using System;
using System.Collections.Generic;
using UnityEngine;

namespace WallSight
{
    /// <summary>
    /// Plans a walking route on the robot scan's occupancy grid (A*, 8-connected) and draws it.
    /// Everything lives in the scan's local metres under <see cref="scanRoot"/>, so the route
    /// scales and moves with the model (tabletop or 1:1).
    /// </summary>
    public class RoutePlanner : MonoBehaviour
    {
        [Header("Map")]
        [Tooltip("Grid JSON from scripts/build_occupancy_grid.py")]
        public TextAsset gridJson;
        [Tooltip("The scan mesh transform (floor at local y = 0, metres)")]
        public Transform scanRoot;
        [Tooltip("Flip the grid in X if the route does not line up with the mesh")]
        public bool mirrorX;

        [Header("Planning")]
        [Tooltip("Cells next to walls or unscanned space that are never walked (0.1 m cells)")]
        public int clearanceCells = 1;
        [Tooltip("Extra cost near walls, so routes keep to the middle of corridors")]
        public float wallPenalty = 3f;
        public int wallPenaltyRangeCells = 4;
        [Tooltip("Extra cost in the ring around a hazard")]
        public float hazardPenalty = 25f;
        [Tooltip("The scanned corridors are ~2 m wide: a larger hazard blocks them completely")]
        public float hazardMarginMetres = 0.4f;
        public float hazardRadiusMetres = 0.4f;
        [Tooltip("Route start in scan-local metres; snapped to the nearest walkable cell")]
        public Vector3 defaultStartLocal = new Vector3(-2.7f, 0f, -4.7f); // south end of the hallway-001 scan

        [Header("Display")]
        public Material lineMaterial;
        public Color routeColor = new Color(0.1f, 1f, 0.6f);
        public float lineWidthMetres = 0.12f;
        public float lineHeightMetres = 0.05f;
        public float markerSizeMetres = 0.35f;
        public TMPro.TMP_Text statusLabel;
        public bool drawGridGizmos;

        public event Action<IReadOnlyList<Vector3>> RouteChanged;
        public IReadOnlyList<Vector3> RouteLocal => route;
        public float RouteLengthMetres { get; private set; }
        /// The start marker's floor position in scan-local metres, or null while no valid start is set.
        public Vector3? StartLocal => start.HasValue ? grid.ToLocal(start.Value, 0f) : (Vector3?)null;

        OccupancyGrid grid;
        bool[] blocked;
        float[] baseCost;
        float[] cost;
        readonly List<Vector3> route = new List<Vector3>();
        readonly List<(Vector3 centre, GameObject marker)> hazards = new List<(Vector3, GameObject)>();
        Vector2Int? start, target;
        LineRenderer line;
        GameObject startMarker, targetMarker;

        void Awake()
        {
            if (gridJson == null || scanRoot == null)
            {
                Status("Route planner: assign the grid JSON and the scan root.");
                enabled = false;
                return;
            }
            grid = new OccupancyGrid(gridJson.text, mirrorX);
            BuildCosts();
            line = CreateLine();
            startMarker = CreateMarker("RouteStart", PrimitiveType.Sphere, new Color(0.2f, 0.9f, 0.3f));
            targetMarker = CreateMarker("RouteTarget", PrimitiveType.Sphere, new Color(1f, 0.85f, 0.1f));
            startMarker.SetActive(false);
            targetMarker.SetActive(false);
            // Wide search: a default start slightly off the scanned floor still snaps onto it.
            SetStartLocal(defaultStartLocal, 60);
        }

        void LateUpdate()
        {
            // LineRenderer width is in world units; keep it a fixed size in scan metres.
            line.widthMultiplier = lineWidthMetres * scanRoot.lossyScale.x;
        }

        // ---- Public API (mouse, hand ray and buttons all call these) ----

        public bool SetStartWorld(Vector3 world) => SetStartLocal(scanRoot.InverseTransformPoint(world));

        public bool SetTargetWorld(Vector3 world) => SetTargetLocal(scanRoot.InverseTransformPoint(world));

        public void ToggleHazardWorld(Vector3 world)
        {
            var local = scanRoot.InverseTransformPoint(world);
            local.y = 0f;
            int hit = hazards.FindIndex(h => Vector3.Distance(h.centre, local) < hazardRadiusMetres);
            if (hit >= 0)
            {
                Destroy(hazards[hit].marker);
                hazards.RemoveAt(hit);
            }
            else
            {
                var m = CreateMarker("SimulatedHazard", PrimitiveType.Cylinder, new Color(1f, 0.15f, 0.1f));
                m.transform.localPosition = local + Vector3.up * 0.02f;
                m.transform.localScale = new Vector3(hazardRadiusMetres * 2f, 0.02f, hazardRadiusMetres * 2f);
                hazards.Add((local, m));
            }
            ApplyHazards();
            Replan();
        }

        public void ClearHazards()
        {
            foreach (var h in hazards) Destroy(h.marker);
            hazards.Clear();
            ApplyHazards();
            Replan();
        }

        public bool SetStartLocal(Vector3 local, int searchRadiusCells = 6)
        {
            var c = NearestWalkable(grid.ToCell(local), searchRadiusCells);
            if (c == null) { Status("Start must be on scanned floor."); return false; }
            start = c;
            startMarker.SetActive(true);
            startMarker.transform.localPosition = grid.ToLocal(c.Value, markerSizeMetres * 0.5f);
            Replan();
            return true;
        }

        public bool SetTargetLocal(Vector3 local)
        {
            var c = NearestWalkable(grid.ToCell(local), 6);
            if (c == null) { Status("Target is not on scanned floor: the robot has not seen it."); return false; }
            target = c;
            targetMarker.SetActive(true);
            targetMarker.transform.localPosition = grid.ToLocal(c.Value, markerSizeMetres * 0.5f);
            Replan();
            return true;
        }

        public void Replan()
        {
            route.Clear();
            RouteLengthMetres = 0f;
            if (start == null || target == null)
            {
                line.positionCount = 0;
                Status(start == null ? "Set a start point." : "Tap a target on the map.");
                RouteChanged?.Invoke(route);
                return;
            }
            var cells = AStar(start.Value, target.Value);
            if (cells == null)
            {
                line.positionCount = 0;
                Status(hazards.Count > 0 ? "No safe route: blocked by hazard." : "No route on the scanned floor.");
                RouteChanged?.Invoke(route);
                return;
            }
            foreach (var c in Simplify(cells)) route.Add(grid.ToLocal(c, lineHeightMetres));
            for (int i = 1; i < route.Count; i++) RouteLengthMetres += Vector3.Distance(route[i - 1], route[i]);
            line.positionCount = route.Count;
            line.SetPositions(route.ToArray());
            Status($"Route {RouteLengthMetres:0.0} m, planned on the robot's scan" +
                   (hazards.Count > 0 ? $", avoiding {hazards.Count} simulated hazard(s)" : ""));
            RouteChanged?.Invoke(route);
        }

        // ---- Costs ----

        void BuildCosts()
        {
            int n = grid.Width * grid.Height;
            // Multi-source BFS: distance in cells from every free cell to the nearest non-free cell.
            var dist = new int[n];
            var queue = new Queue<int>();
            for (int i = 0; i < n; i++)
            {
                if (grid[grid.Cell(i)] != OccupancyGrid.Free) { dist[i] = 0; queue.Enqueue(i); }
                else dist[i] = int.MaxValue;
            }
            while (queue.Count > 0)
            {
                int i = queue.Dequeue();
                var c = grid.Cell(i);
                foreach (var d in Neighbours8)
                {
                    var nc = c + d;
                    if (!grid.InBounds(nc)) continue;
                    int j = grid.Index(nc);
                    if (dist[j] > dist[i] + 1) { dist[j] = dist[i] + 1; queue.Enqueue(j); }
                }
            }
            blocked = new bool[n];
            baseCost = new float[n];
            for (int i = 0; i < n; i++)
            {
                blocked[i] = dist[i] <= clearanceCells;
                float near = Mathf.Clamp01((wallPenaltyRangeCells - dist[i]) / (float)wallPenaltyRangeCells);
                baseCost[i] = 1f + wallPenalty * near;
            }
            KeepLargestArea();
            cost = (float[])baseCost.Clone();
        }

        /// Small floor fragments are scan noise; block them so taps snap onto the reachable area.
        void KeepLargestArea()
        {
            int n = blocked.Length;
            var label = new int[n];
            int best = 0, bestSize = 0, next = 0;
            var queue = new Queue<int>();
            for (int s = 0; s < n; s++)
            {
                if (blocked[s] || label[s] != 0) continue;
                int size = 0;
                label[s] = ++next;
                queue.Enqueue(s);
                while (queue.Count > 0)
                {
                    int i = queue.Dequeue();
                    size++;
                    var c = grid.Cell(i);
                    for (int k = 0; k < 4; k++)
                    {
                        var nc = c + Neighbours8[k];
                        if (!grid.InBounds(nc)) continue;
                        int j = grid.Index(nc);
                        if (!blocked[j] && label[j] == 0) { label[j] = next; queue.Enqueue(j); }
                    }
                }
                if (size > bestSize) { bestSize = size; best = next; }
            }
            for (int i = 0; i < n; i++) if (label[i] != best) blocked[i] = true;
        }

        bool[] hazardBlocked;

        void ApplyHazards()
        {
            int n = baseCost.Length;
            cost = (float[])baseCost.Clone();
            hazardBlocked = new bool[n];
            for (int i = 0; i < n; i++)
            {
                var p = grid.ToLocal(grid.Cell(i), 0f);
                foreach (var h in hazards)
                {
                    float d = Vector3.Distance(p, h.centre);
                    if (d <= hazardRadiusMetres) hazardBlocked[i] = true;
                    else if (d <= hazardRadiusMetres + hazardMarginMetres) cost[i] += hazardPenalty;
                }
            }
        }

        bool Walkable(Vector2Int c)
        {
            if (!grid.InBounds(c)) return false;
            int i = grid.Index(c);
            return !blocked[i] && (hazardBlocked == null || !hazardBlocked[i]);
        }

        Vector2Int? NearestWalkable(Vector2Int c, int maxRadius)
        {
            for (int r = 0; r <= maxRadius; r++)
                for (int dz = -r; dz <= r; dz++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != r) continue;
                        var nc = new Vector2Int(c.x + dx, c.y + dz);
                        if (Walkable(nc)) return nc;
                    }
            return null;
        }

        // ---- A* ----

        static readonly Vector2Int[] Neighbours8 =
        {
            new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1),
            new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1),
        };

        List<Vector2Int> AStar(Vector2Int from, Vector2Int to)
        {
            int n = cost.Length;
            var g = new float[n];
            var parent = new int[n];
            var closed = new bool[n];
            for (int i = 0; i < n; i++) { g[i] = float.PositiveInfinity; parent[i] = -1; }
            var open = new MinHeap();
            int s = grid.Index(from), t = grid.Index(to);
            g[s] = 0f;
            open.Push(s, Heuristic(from, to));
            while (open.Count > 0)
            {
                int i = open.Pop();
                if (closed[i]) continue;
                if (i == t) break;
                closed[i] = true;
                var c = grid.Cell(i);
                foreach (var d in Neighbours8)
                {
                    var nc = c + d;
                    if (!Walkable(nc)) continue;
                    bool diagonal = d.x != 0 && d.y != 0;
                    // No corner cutting past walls.
                    if (diagonal && (!Walkable(new Vector2Int(c.x + d.x, c.y)) || !Walkable(new Vector2Int(c.x, c.y + d.y)))) continue;
                    int j = grid.Index(nc);
                    float step = (diagonal ? 1.41421f : 1f) * 0.5f * (cost[i] + cost[j]);
                    if (g[i] + step < g[j])
                    {
                        g[j] = g[i] + step;
                        parent[j] = i;
                        open.Push(j, g[j] + Heuristic(nc, to));
                    }
                }
            }
            if (parent[t] < 0 && s != t) return null;
            var path = new List<Vector2Int>();
            for (int i = t; i >= 0; i = parent[i]) path.Add(grid.Cell(i));
            path.Reverse();
            return path;
        }

        static float Heuristic(Vector2Int a, Vector2Int b)
        {
            int dx = Mathf.Abs(a.x - b.x), dz = Mathf.Abs(a.y - b.y);
            return Mathf.Max(dx, dz) + 0.41421f * Mathf.Min(dx, dz);
        }

        static List<Vector2Int> Simplify(List<Vector2Int> cells)
        {
            // Keep only the cells where the direction changes.
            var result = new List<Vector2Int> { cells[0] };
            for (int i = 1; i < cells.Count - 1; i++)
                if (cells[i] - cells[i - 1] != cells[i + 1] - cells[i]) result.Add(cells[i]);
            if (cells.Count > 1) result.Add(cells[cells.Count - 1]);
            return result;
        }

        class MinHeap
        {
            readonly List<(int item, float key)> heap = new List<(int, float)>();
            public int Count => heap.Count;

            public void Push(int item, float key)
            {
                heap.Add((item, key));
                for (int i = heap.Count - 1; i > 0;)
                {
                    int p = (i - 1) / 2;
                    if (heap[p].key <= heap[i].key) break;
                    (heap[p], heap[i]) = (heap[i], heap[p]);
                    i = p;
                }
            }

            public int Pop()
            {
                int top = heap[0].item;
                heap[0] = heap[heap.Count - 1];
                heap.RemoveAt(heap.Count - 1);
                for (int i = 0; ;)
                {
                    int l = 2 * i + 1, r = l + 1, m = i;
                    if (l < heap.Count && heap[l].key < heap[m].key) m = l;
                    if (r < heap.Count && heap[r].key < heap[m].key) m = r;
                    if (m == i) break;
                    (heap[m], heap[i]) = (heap[i], heap[m]);
                    i = m;
                }
                return top;
            }
        }

        // ---- Display helpers ----

        LineRenderer CreateLine()
        {
            var go = new GameObject("Route");
            go.transform.SetParent(scanRoot, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.material = lineMaterial != null ? lineMaterial : UnlitMaterial(routeColor);
            lr.startColor = lr.endColor = routeColor;
            lr.numCornerVertices = 4;
            lr.numCapVertices = 4;
            lr.positionCount = 0;
            return lr;
        }

        GameObject CreateMarker(string name, PrimitiveType type, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Destroy(go.GetComponent<Collider>()); // markers must not block taps on the map
            go.transform.SetParent(scanRoot, false);
            go.transform.localScale = Vector3.one * markerSizeMetres;
            go.GetComponent<Renderer>().material = UnlitMaterial(color);
            return go;
        }

        public static Material UnlitMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var m = new Material(shader);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            m.color = color;
            return m;
        }

        void Status(string text)
        {
            if (statusLabel != null) statusLabel.text = text;
            else Debug.Log($"[WallSight] {text}");
        }

        void OnDrawGizmosSelected()
        {
            if (!drawGridGizmos || gridJson == null || scanRoot == null) return;
            var g = grid ?? new OccupancyGrid(gridJson.text, mirrorX);
            Gizmos.matrix = scanRoot.localToWorldMatrix;
            var size = new Vector3(g.CellSize * 0.9f, 0.01f, g.CellSize * 0.9f);
            for (int z = 0; z < g.Height; z++)
                for (int x = 0; x < g.Width; x++)
                {
                    var c = new Vector2Int(x, z);
                    byte v = g[c];
                    if (v == OccupancyGrid.Unknown) continue;
                    Gizmos.color = v == OccupancyGrid.Free ? new Color(0f, 1f, 0f, 0.35f) : new Color(1f, 0f, 0f, 0.6f);
                    Gizmos.DrawCube(g.ToLocal(c, 0.01f), size);
                }
        }
    }
}
