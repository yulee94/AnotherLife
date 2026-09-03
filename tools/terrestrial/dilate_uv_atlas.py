#!/usr/bin/env python3
"""Fill unused UV-atlas pixels from the nearest rendered texel."""
from __future__ import annotations

import argparse
from pathlib import Path
from typing import Sequence

import numpy as np
from PIL import Image
from scipy.ndimage import distance_transform_edt


def stitch_tiles(tiles: Sequence[Image.Image], *, grid: int) -> Image.Image:
    if len(tiles) != grid * grid:
        raise ValueError(f"expected {grid * grid} tiles, found {len(tiles)}")
    tile_width, tile_height = tiles[0].size
    output = Image.new("RGBA", (tile_width * grid, tile_height * grid))
    for index, tile in enumerate(tiles):
        tile_x = index % grid
        tile_y_from_bottom = index // grid
        output.paste(tile.convert("RGBA"), (tile_x * tile_width, (grid - 1 - tile_y_from_bottom) * tile_height))
    return output


def dilate_transparent_pixels(image: Image.Image) -> Image.Image:
    rgba = np.asarray(image.convert("RGBA"), dtype=np.uint8)
    opaque = rgba[:, :, 3] > 0
    if not opaque.any():
        raise ValueError("atlas has no opaque texels")
    if opaque.all():
        return Image.fromarray(rgba[:, :, :3], mode="RGB")
    nearest = distance_transform_edt(~opaque, return_distances=False, return_indices=True)
    output = rgba[:, :, :3].copy()
    transparent = ~opaque
    output[transparent] = rgba[nearest[0][transparent], nearest[1][transparent], :3]
    return Image.fromarray(output, mode="RGB")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("paths", type=Path, nargs="+")
    parser.add_argument("--stitch-grid", type=int)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    if args.stitch_grid is not None:
        if args.output is None:
            parser.error("--output is required with --stitch-grid")
        images = [Image.open(path) for path in args.paths]
        stitch_tiles(images, grid=args.stitch_grid).save(args.output, format="PNG", optimize=False)
        print(args.output)
        return 0
    for path in args.paths:
        image = Image.open(path)
        dilate_transparent_pixels(image).save(path, format="PNG", optimize=False)
        print(path)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
