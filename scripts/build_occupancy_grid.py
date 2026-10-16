"""Build a 2D occupancy grid from a cleaned scan PLY for route planning in Unity.

Input: binary little-endian PLY with float x, y, z (+ uchar r, g, b), Unity axes
(x right, y up with 0 = floor, z forward), metres.
Output: grid JSON for WallSight's Unity route planner, plus a preview PNG.

Cell codes: 0 = free (floor seen, no obstacle), 1 = obstacle, 2 = unknown (no data).
Unknown cells are not traversable: the robot never saw them.

Usage: py -3 scripts/build_occupancy_grid.py <scan.ply> <out.json> [--cell 0.10]
"""

import argparse
import json
from pathlib import Path

import numpy as np
from PIL import Image

FREE, OBSTACLE, UNKNOWN = 0, 1, 2


def read_ply(path):
    data = Path(path).read_bytes()
    end = data.index(b"end_header\n") + len(b"end_header\n")
    header = data[:end].decode("ascii").splitlines()
    if "format binary_little_endian 1.0" not in header:
        raise ValueError("expected binary_little_endian PLY")
    count = next(int(h.split()[2]) for h in header if h.startswith("element vertex"))
    props = [h.split() for h in header if h.startswith("property")]
    types = {"float": "<f4", "uchar": "u1", "double": "<f8", "int": "<i4"}
    dtype = np.dtype([(p[2], types[p[1]]) for p in props])
    v = np.frombuffer(data, dtype=dtype, count=count, offset=end)
    return np.stack([v["x"], v["y"], v["z"]], axis=1).astype(np.float64)


def dilate(mask, r):
    out = mask.copy()
    for dz in range(-r, r + 1):
        for dx in range(-r, r + 1):
            out |= np.roll(np.roll(mask, dz, axis=0), dx, axis=1)
    return out


def erode(mask, r):
    return ~dilate(~mask, r)


def build(points, cell, floor_max, obs_min, obs_max, min_hits, close_r):
    x, y, z = points[:, 0], points[:, 1], points[:, 2]
    origin_x, origin_z = x.min() - cell, z.min() - cell
    width = int(np.ceil((x.max() - origin_x) / cell)) + 2
    height = int(np.ceil((z.max() - origin_z) / cell)) + 2
    ix = ((x - origin_x) / cell).astype(int)
    iz = ((z - origin_z) / cell).astype(int)

    floor = np.zeros((height, width), bool)
    sel = y <= floor_max
    floor[iz[sel], ix[sel]] = True

    hits = np.zeros((height, width), int)
    sel = (y >= obs_min) & (y <= obs_max)
    np.add.at(hits, (iz[sel], ix[sel]), 1)
    obstacle = hits >= min_hits

    # Close small gaps in the sparse floor so the corridor is one connected area.
    floor = erode(dilate(floor, close_r), close_r)

    grid = np.full((height, width), UNKNOWN, np.uint8)
    grid[floor] = FREE
    grid[obstacle] = OBSTACLE
    return grid, origin_x, origin_z


def save_preview(grid, path, scale=6):
    colours = np.array([[230, 230, 230], [30, 30, 30], [120, 140, 170]], np.uint8)
    img = colours[grid][::-1]  # flip so +z points up in the image
    Image.fromarray(img).resize((grid.shape[1] * scale, grid.shape[0] * scale), Image.NEAREST).save(path)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("ply")
    ap.add_argument("out_json")
    ap.add_argument("--cell", type=float, default=0.10, help="cell size in metres")
    ap.add_argument("--floor-max", type=float, default=0.10, help="points below this height count as floor")
    ap.add_argument("--obs-min", type=float, default=0.15, help="obstacle band lower height")
    ap.add_argument("--obs-max", type=float, default=1.50, help="obstacle band upper height")
    ap.add_argument("--min-hits", type=int, default=3, help="points per cell to call it an obstacle")
    ap.add_argument("--close", type=int, default=2, help="floor gap-closing radius in cells")
    a = ap.parse_args()

    pts = read_ply(a.ply)
    grid, ox, oz = build(pts, a.cell, a.floor_max, a.obs_min, a.obs_max, a.min_hits, a.close)
    out = Path(a.out_json)
    out.write_text(json.dumps({
        "source": Path(a.ply).name,
        "cellSize": a.cell,
        "originX": round(ox, 4),
        "originZ": round(oz, 4),
        "width": int(grid.shape[1]),
        "height": int(grid.shape[0]),
        "cells": "".join(map(str, grid.ravel())),
    }))
    save_preview(grid, out.with_suffix(".png"))
    n = grid.size
    print(f"{len(pts)} points -> {grid.shape[1]}x{grid.shape[0]} cells of {a.cell} m "
          f"(free {np.sum(grid == FREE) / n:.0%}, obstacle {np.sum(grid == OBSTACLE) / n:.0%}, "
          f"unknown {np.sum(grid == UNKNOWN) / n:.0%}); origin x={ox:.3f} z={oz:.3f}")


if __name__ == "__main__":
    main()
