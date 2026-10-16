using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace WallSight
{
    /// <summary>
    /// Draws the edges of this object's mesh as glowing lines (a "hologram" wireframe of the scan).
    /// Put it on the object that has the MeshFilter (the imported OBJ's child). On HoloLens dark is
    /// see-through, so only the lines remain visible.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class WireframeOverlay : MonoBehaviour
    {
        public Color lineColor = new Color(0.2f, 1f, 1f);
        [Tooltip("Keep the faces as a dark fill (hides lines behind walls); off = lines only")]
        public bool darkFill;
        public Color fillColor = new Color(0.04f, 0.06f, 0.09f);

        void Start()
        {
            var source = GetComponent<MeshFilter>().sharedMesh;
            var triangles = source.triangles;
            var seen = new HashSet<long>();
            var indices = new List<int>();
            for (int i = 0; i < triangles.Length; i += 3)
                for (int e = 0; e < 3; e++)
                {
                    int a = triangles[i + e], b = triangles[i + (e + 1) % 3];
                    int lo = Mathf.Min(a, b), hi = Mathf.Max(a, b);
                    if (seen.Add(((long)lo << 32) | (uint)hi)) { indices.Add(lo); indices.Add(hi); }
                }

            var lines = new Mesh
            {
                name = source.name + "_edges",
                indexFormat = source.vertexCount > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16,
                vertices = source.vertices,
            };
            lines.SetIndices(indices, MeshTopology.Lines, 0);

            var go = new GameObject("Wireframe");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = lines;
            go.AddComponent<MeshRenderer>().sharedMaterial = RoutePlanner.UnlitMaterial(lineColor);

            var fill = GetComponent<MeshRenderer>();
            if (darkFill) fill.sharedMaterial = RoutePlanner.UnlitMaterial(fillColor);
            else fill.enabled = false;
        }
    }
}
