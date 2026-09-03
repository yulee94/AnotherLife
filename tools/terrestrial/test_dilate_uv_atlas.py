import tempfile
import unittest
from pathlib import Path

from PIL import Image

from tools.terrestrial.dilate_uv_atlas import dilate_transparent_pixels, stitch_tiles


class DilateUvAtlasTests(unittest.TestCase):
    def test_stitches_bottom_first_tiles_into_uv_orientation(self):
        colors = [(255, 0, 0, 255), (0, 255, 0, 255), (0, 0, 255, 255), (255, 255, 0, 255)]
        tiles = [Image.new("RGBA", (1, 1), color) for color in colors]

        result = stitch_tiles(tiles, grid=2)

        self.assertEqual(colors[2], result.getpixel((0, 0)))
        self.assertEqual(colors[3], result.getpixel((1, 0)))
        self.assertEqual(colors[0], result.getpixel((0, 1)))
        self.assertEqual(colors[1], result.getpixel((1, 1)))

    def test_fills_transparent_pixels_from_nearest_opaque_texel(self):
        image = Image.new("RGBA", (5, 5), (0, 0, 0, 0))
        image.putpixel((2, 2), (230, 40, 20, 255))

        result = dilate_transparent_pixels(image)

        self.assertEqual("RGB", result.mode)
        self.assertEqual((230, 40, 20), result.getpixel((0, 0)))
        self.assertEqual((230, 40, 20), result.getpixel((4, 4)))

    def test_preserves_existing_opaque_texels(self):
        image = Image.new("RGBA", (3, 1), (0, 0, 0, 0))
        image.putpixel((0, 0), (255, 0, 0, 255))
        image.putpixel((2, 0), (0, 0, 255, 255))

        result = dilate_transparent_pixels(image)

        self.assertEqual((255, 0, 0), result.getpixel((0, 0)))
        self.assertEqual((0, 0, 255), result.getpixel((2, 0)))


if __name__ == "__main__":
    unittest.main()
