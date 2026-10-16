using System;
using UnityEngine;

namespace WallSight
{
    /// <summary>
    /// 2D occupancy grid over the scan's floor plane (x, z in scan-local metres),
    /// loaded from the JSON written by scripts/build_occupancy_grid.py.
    /// </summary>
    public class OccupancyGrid
    {
        public const byte Free = 0, Obstacle = 1, Unknown = 2;

        [Serializable]
        class GridFile
        {
            public string source;
            public float cellSize, originX, originZ;
            public int width, height;
            public string cells;
        }

        public readonly string Source;
        public readonly float CellSize, OriginX, OriginZ;
        public readonly int Width, Height;
        readonly byte[] cells;

        public OccupancyGrid(string json, bool mirrorX)
        {
            var f = JsonUtility.FromJson<GridFile>(json);
            if (f == null || f.cells == null || f.cells.Length != f.width * f.height)
                throw new ArgumentException("Grid JSON is missing cells or has the wrong size.");
            Source = f.source;
            CellSize = f.cellSize;
            Width = f.width;
            Height = f.height;
            OriginZ = f.originZ;
            cells = new byte[f.cells.Length];
            for (int z = 0; z < Height; z++)
                for (int x = 0; x < Width; x++)
                    // Mirroring X flips the column order so scan x maps to -x.
                    cells[z * Width + (mirrorX ? Width - 1 - x : x)] = (byte)(f.cells[z * Width + x] - '0');
            OriginX = mirrorX ? -(f.originX + Width * CellSize) : f.originX;
        }

        public bool InBounds(Vector2Int c) => c.x >= 0 && c.y >= 0 && c.x < Width && c.y < Height;

        public byte this[Vector2Int c] => cells[c.y * Width + c.x];

        public int Index(Vector2Int c) => c.y * Width + c.x;

        public Vector2Int Cell(int index) => new Vector2Int(index % Width, index / Width);

        public Vector2Int ToCell(Vector3 local) =>
            new Vector2Int(Mathf.FloorToInt((local.x - OriginX) / CellSize), Mathf.FloorToInt((local.z - OriginZ) / CellSize));

        public Vector3 ToLocal(Vector2Int c, float y) =>
            new Vector3(OriginX + (c.x + 0.5f) * CellSize, y, OriginZ + (c.y + 0.5f) * CellSize);
    }
}
