"""Clean selected Meshy Slagfall sources into Unity-ready LOD packages.

Run with Blender 5.2 in background mode. The script preserves the approved
Meshy surfaces, normalizes each family to a four-metre profiling footprint,
remaps the existing UVs into one padded shared 2K atlas, builds four render
LODs, exports one FBX per family, saves a master .blend, and writes provenance
and topology metrics. The four-metre scale is explicitly for profiling and is
not gameplay-dimension authority.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import sys
from dataclasses import dataclass
from pathlib import Path

import bmesh
import bpy
import numpy as np


ATLAS_SIZE = 2048
ATLAS_GRID = 4
TILE_SIZE = ATLAS_SIZE // ATLAS_GRID
TILE_PADDING = 16
TILE_INNER = TILE_SIZE - (2 * TILE_PADDING)
PROFILING_FOOTPRINT_METRES = 4.0
LOD_RATIOS = (1.0, 0.55, 0.25, 0.08)


@dataclass(frozen=True)
class Family:
    key: str
    family_id: str
    source_relative: str
    texture_relative: str
    tile_index: int


FAMILIES = (
    Family(
        "01",
        "irregular_fracture_raft",
        "Raw/01_irregular_fracture_raft_meshy_t2_v001.fbx",
        "Raw/01_irregular_fracture_raft_meshy_t2_v001_textures",
        0,
    ),
    Family(
        "02",
        "broken_fracture_raft",
        "Replacement/02_broken_fracture_raft_meshy7_v002.fbx",
        "Replacement/02_broken_fracture_raft_meshy7_v002_textures",
        1,
    ),
    Family(
        "03",
        "undercut_extraction_ledge",
        "Replacement/03_undercut_extraction_ledge_meshy7_v002.fbx",
        "Replacement/03_undercut_extraction_ledge_meshy7_v002_textures",
        2,
    ),
    Family(
        "04",
        "talus_apron",
        "Replacement/04_talus_apron_meshy7_v002.fbx",
        "Replacement/04_talus_apron_meshy7_v002_textures",
        3,
    ),
    Family(
        "05",
        "collapsed_gallery_mouth",
        "Raw/05_collapsed_gallery_mouth_meshy_t2_v001.fbx",
        "Raw/05_collapsed_gallery_mouth_meshy_t2_v001_textures",
        4,
    ),
    Family(
        "06",
        "diagonal_fault_slab",
        "Replacement/06_diagonal_fault_slab_meshy7_v002.fbx",
        "Replacement/06_diagonal_fault_slab_meshy7_v002_textures",
        5,
    ),
    Family(
        "07",
        "braided_runoff_pool",
        "Replacement/07_braided_runoff_pool_meshy7_v002.fbx",
        "Replacement/07_braided_runoff_pool_meshy7_v002_textures",
        6,
    ),
    Family(
        "08",
        "iron_soil_wedge",
        "Replacement/08_iron_soil_wedge_meshy7_v002.fbx",
        "Replacement/08_iron_soil_wedge_meshy7_v002_textures",
        7,
    ),
)


def parse_args() -> argparse.Namespace:
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    repo_root = Path(__file__).resolve().parents[1]
    source_root = (
        repo_root
        / "unity/ArtSource/Terrestrials/Stonehold/SlagfallQuarry/Environment/Meshy"
    )
    unity_root = (
        repo_root
        / "unity/Assets/AL/Art/Terrestrials/Stonehold/SlagfallQuarry/Environment"
    )
    art_source_root = (
        repo_root / "unity/ArtSource/Terrestrials/Stonehold/SlagfallQuarry/Environment"
    )

    parser = argparse.ArgumentParser()
    parser.add_argument("--source-root", type=Path, default=source_root)
    parser.add_argument("--unity-root", type=Path, default=unity_root)
    parser.add_argument(
        "--master-blend",
        type=Path,
        default=art_source_root / "tdf_envkit_stonehold_slagfall_meshy_cleanup_v001.blend",
    )
    parser.add_argument(
        "--report",
        type=Path,
        default=source_root / "slagfall_environment_meshy_cleanup_v001.json",
    )
    return parser.parse_args(argv)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def repository_locator(path: Path) -> str:
    repo_root = Path(__file__).resolve().parents[1]
    return path.resolve().relative_to(repo_root).as_posix()


def write_json_lf(path: Path, payload: object) -> None:
    serialized = json.dumps(payload, indent=2) + "\n"
    path.write_bytes(serialized.encode("utf-8"))


def require_inputs(source_root: Path) -> None:
    missing: list[str] = []
    for family in FAMILIES:
        source_path = source_root / family.source_relative
        texture_dir = source_root / family.texture_relative
        if not source_path.is_file():
            missing.append(str(source_path))
        for texture_name in ("base_color.png", "normal.png", "metallic.png", "roughness.png"):
            texture_path = texture_dir / texture_name
            if not texture_path.is_file():
                missing.append(str(texture_path))
    if missing:
        raise FileNotFoundError("Missing Slagfall inputs:\n" + "\n".join(missing))


def reset_scene() -> None:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene.render.engine = "BLENDER_WORKBENCH"


def image_pixels(path: Path, size: int) -> np.ndarray:
    image = bpy.data.images.load(str(path), check_existing=False)
    image.colorspace_settings.name = "Non-Color"
    image.scale(size, size)
    flat = np.empty(size * size * 4, dtype=np.float32)
    try:
        image.pixels.foreach_get(flat)
    except AttributeError:
        flat[:] = image.pixels[:]
    pixels = flat.reshape((size, size, 4))
    bpy.data.images.remove(image)
    return pixels


def save_pixels(path: Path, pixels: np.ndarray) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    image = bpy.data.images.new(
        path.stem,
        width=pixels.shape[1],
        height=pixels.shape[0],
        alpha=True,
        float_buffer=False,
    )
    image.colorspace_settings.name = "Non-Color"
    try:
        image.pixels.foreach_set(pixels.astype(np.float32, copy=False).ravel())
    except AttributeError:
        image.pixels[:] = pixels.astype(np.float32, copy=False).ravel()
    image.filepath_raw = str(path)
    image.file_format = "PNG"
    image.save()
    bpy.data.images.remove(image)


def padded_tile(pixels: np.ndarray) -> np.ndarray:
    return np.pad(
        pixels,
        ((TILE_PADDING, TILE_PADDING), (TILE_PADDING, TILE_PADDING), (0, 0)),
        mode="edge",
    )


def tile_bounds(tile_index: int) -> tuple[int, int, int, int]:
    column = tile_index % ATLAS_GRID
    row = tile_index // ATLAS_GRID
    x0 = column * TILE_SIZE
    y0 = row * TILE_SIZE
    return x0, y0, x0 + TILE_SIZE, y0 + TILE_SIZE


def build_atlases(source_root: Path, texture_root: Path) -> dict[str, Path]:
    atlas_paths = {
        "base_color": texture_root / "tdf_atlas_stonehold_slagfall_environment_basecolor_v001.png",
        "normal": texture_root / "tdf_atlas_stonehold_slagfall_environment_normal_v001.png",
        "metallic_smoothness": texture_root
        / "tdf_atlas_stonehold_slagfall_environment_metallic_smoothness_v001.png",
    }

    base = np.zeros((ATLAS_SIZE, ATLAS_SIZE, 4), dtype=np.float32)
    base[..., 0] = 0.18
    base[..., 1] = 0.16
    base[..., 2] = 0.12
    base[..., 3] = 1.0
    normal = np.zeros_like(base)
    normal[..., 0] = 0.5
    normal[..., 1] = 0.5
    normal[..., 2] = 1.0
    normal[..., 3] = 1.0
    packed = np.zeros_like(base)
    packed[..., 3] = 0.25

    for family in FAMILIES:
        texture_dir = source_root / family.texture_relative
        x0, y0, x1, y1 = tile_bounds(family.tile_index)

        base[y0:y1, x0:x1] = padded_tile(
            image_pixels(texture_dir / "base_color.png", TILE_INNER)
        )
        normal[y0:y1, x0:x1] = padded_tile(
            image_pixels(texture_dir / "normal.png", TILE_INNER)
        )

        metallic = image_pixels(texture_dir / "metallic.png", TILE_INNER)
        roughness = image_pixels(texture_dir / "roughness.png", TILE_INNER)
        packed_inner = np.empty_like(metallic)
        packed_inner[..., 0:3] = metallic[..., 0:1]
        packed_inner[..., 3] = np.clip(1.0 - roughness[..., 0], 0.0, 1.0)
        packed[y0:y1, x0:x1] = padded_tile(packed_inner)

    save_pixels(atlas_paths["base_color"], base)
    save_pixels(atlas_paths["normal"], normal)
    save_pixels(atlas_paths["metallic_smoothness"], packed)
    return atlas_paths


def shared_export_material() -> bpy.types.Material:
    material = bpy.data.materials.new(
        "tdf_mat_stonehold_slagfall_environment_atlas_v001"
    )
    material.diffuse_color = (0.22, 0.19, 0.15, 1.0)
    material.roughness = 0.78
    material.metallic = 0.05
    return material


def import_source(path: Path) -> bpy.types.Object:
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=str(path), use_anim=False)
    imported = [obj for obj in bpy.data.objects if obj not in before]
    meshes = [obj for obj in imported if obj.type == "MESH"]
    if not meshes:
        raise RuntimeError(f"No meshes imported from {path}")

    for mesh in meshes:
        world_matrix = mesh.matrix_world.copy()
        mesh.parent = None
        mesh.matrix_world = world_matrix

    for obj in imported:
        if obj.type != "MESH":
            bpy.data.objects.remove(obj, do_unlink=True)

    bpy.ops.object.select_all(action="DESELECT")
    for mesh in meshes:
        mesh.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    if len(meshes) > 1:
        bpy.ops.object.join()
    obj = bpy.context.view_layer.objects.active
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    return obj


def clean_mesh(obj: bpy.types.Object) -> None:
    mesh = obj.data
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bm.verts.ensure_lookup_table()
    bm.edges.ensure_lookup_table()
    bm.faces.ensure_lookup_table()

    if bm.verts:
        bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=0.00001)
    loose_verts = [vertex for vertex in bm.verts if not vertex.link_faces]
    if loose_verts:
        bmesh.ops.delete(bm, geom=loose_verts, context="VERTS")
    if bm.edges:
        bmesh.ops.dissolve_degenerate(bm, dist=0.000001, edges=list(bm.edges))
    if bm.faces:
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        bmesh.ops.triangulate(
            bm,
            faces=list(bm.faces),
            quad_method="BEAUTY",
            ngon_method="BEAUTY",
        )

    bm.to_mesh(mesh)
    bm.free()
    mesh.update(calc_edges=True)
    for polygon in mesh.polygons:
        polygon.use_smooth = True


def normalize_profiling_scale(obj: bpy.types.Object) -> None:
    mesh = obj.data
    xs = [vertex.co.x for vertex in mesh.vertices]
    ys = [vertex.co.y for vertex in mesh.vertices]
    zs = [vertex.co.z for vertex in mesh.vertices]
    width = max(xs) - min(xs)
    depth = max(ys) - min(ys)
    footprint = max(width, depth)
    if footprint <= 0.0001:
        raise RuntimeError(f"Degenerate footprint for {obj.name}")

    scale = PROFILING_FOOTPRINT_METRES / footprint
    center_x = (min(xs) + max(xs)) * 0.5
    center_y = (min(ys) + max(ys)) * 0.5
    min_z = min(zs)
    for vertex in mesh.vertices:
        vertex.co.x = (vertex.co.x - center_x) * scale
        vertex.co.y = (vertex.co.y - center_y) * scale
        vertex.co.z = (vertex.co.z - min_z) * scale
    mesh.update()


def remap_uv_to_tile(obj: bpy.types.Object, tile_index: int) -> None:
    uv_layer = obj.data.uv_layers.active
    if uv_layer is None:
        raise RuntimeError(f"{obj.name} has no UV layer")

    column = tile_index % ATLAS_GRID
    row = tile_index // ATLAS_GRID
    inner_scale = TILE_INNER / ATLAS_SIZE
    u0 = ((column * TILE_SIZE) + TILE_PADDING) / ATLAS_SIZE
    v0 = ((row * TILE_SIZE) + TILE_PADDING) / ATLAS_SIZE
    for loop in uv_layer.data:
        loop.uv.x = u0 + (min(max(loop.uv.x, 0.0), 1.0) * inner_scale)
        loop.uv.y = v0 + (min(max(loop.uv.y, 0.0), 1.0) * inner_scale)


def move_to_collection(obj: bpy.types.Object, collection: bpy.types.Collection) -> None:
    for current in tuple(obj.users_collection):
        current.objects.unlink(obj)
    collection.objects.link(obj)


def apply_decimate(obj: bpy.types.Object, ratio: float) -> None:
    if ratio >= 0.999:
        return
    modifier = obj.modifiers.new(name="ProfilingLOD", type="DECIMATE")
    modifier.decimate_type = "COLLAPSE"
    modifier.ratio = ratio
    modifier.use_collapse_triangulate = True
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    obj.select_set(False)
    for polygon in obj.data.polygons:
        polygon.use_smooth = True


def mesh_metrics(obj: bpy.types.Object) -> dict[str, object]:
    mesh = obj.data
    bm = bmesh.new()
    bm.from_mesh(mesh)
    boundary_edges = sum(1 for edge in bm.edges if edge.is_boundary)
    non_manifold_edges = sum(1 for edge in bm.edges if not edge.is_manifold)
    bm.free()

    xs = [vertex.co.x for vertex in mesh.vertices]
    ys = [vertex.co.y for vertex in mesh.vertices]
    zs = [vertex.co.z for vertex in mesh.vertices]
    return {
        "vertices": len(mesh.vertices),
        "triangles": len(mesh.polygons),
        "uv_layers": len(mesh.uv_layers),
        "material_slots": len(mesh.materials),
        "boundary_edges": boundary_edges,
        "non_manifold_edges": non_manifold_edges,
        "dimensions_metres": [
            round(max(xs) - min(xs), 4),
            round(max(ys) - min(ys), 4),
            round(max(zs) - min(zs), 4),
        ],
    }


def build_lods(
    family: Family,
    source: bpy.types.Object,
    material: bpy.types.Material,
) -> list[bpy.types.Object]:
    collection = bpy.data.collections.new(f"Slagfall_{family.key}_{family.family_id}")
    bpy.context.scene.collection.children.link(collection)

    source.data.materials.clear()
    source.data.materials.append(material)
    base_name = f"tdf_prop_stonehold_slagfall_{family.family_id}_v001"
    source.name = f"{base_name}_LOD0"
    source.data.name = f"{base_name}_LOD0_Mesh"
    move_to_collection(source, collection)

    lods = [source]
    for lod_index, ratio in enumerate(LOD_RATIOS[1:], start=1):
        lod = source.copy()
        lod.data = source.data.copy()
        collection.objects.link(lod)
        lod.name = f"{base_name}_LOD{lod_index}"
        lod.data.name = f"{base_name}_LOD{lod_index}_Mesh"
        apply_decimate(lod, ratio)
        lods.append(lod)
    return lods


def export_family(path: Path, lods: list[bpy.types.Object]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    for lod in lods:
        lod.select_set(True)
    bpy.context.view_layer.objects.active = lods[0]
    bpy.ops.export_scene.fbx(
        filepath=str(path),
        use_selection=True,
        object_types={"MESH"},
        global_scale=1.0,
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS",
        use_space_transform=True,
        bake_space_transform=True,
        axis_forward="-Z",
        axis_up="Y",
        use_mesh_modifiers=True,
        mesh_smooth_type="FACE",
        use_subsurf=False,
        use_mesh_edges=False,
        use_tspace=False,
        add_leaf_bones=False,
        bake_anim=False,
        path_mode="AUTO",
        embed_textures=False,
    )
    bpy.ops.object.select_all(action="DESELECT")


def build_family(
    family: Family,
    source_root: Path,
    model_root: Path,
    material: bpy.types.Material,
) -> dict[str, object]:
    source_path = source_root / family.source_relative
    obj = import_source(source_path)
    clean_mesh(obj)
    normalize_profiling_scale(obj)
    remap_uv_to_tile(obj, family.tile_index)
    lods = build_lods(family, obj, material)

    output_path = model_root / f"tdf_prop_stonehold_slagfall_{family.family_id}_v001.fbx"
    export_family(output_path, lods)
    return {
        "key": family.key,
        "family_id": family.family_id,
        "profiling_scale_only": True,
        "source": repository_locator(source_path),
        "source_sha256": sha256(source_path),
        "atlas_tile": family.tile_index,
        "output": repository_locator(output_path),
        "output_sha256": sha256(output_path),
        "lods": [mesh_metrics(lod) for lod in lods],
    }


def main() -> None:
    args = parse_args()
    source_root = args.source_root.resolve()
    unity_root = args.unity_root.resolve()
    model_root = unity_root / "Models"
    texture_root = unity_root / "Textures"
    args.master_blend = args.master_blend.resolve()
    args.report = args.report.resolve()

    require_inputs(source_root)
    model_root.mkdir(parents=True, exist_ok=True)
    texture_root.mkdir(parents=True, exist_ok=True)
    args.master_blend.parent.mkdir(parents=True, exist_ok=True)
    args.report.parent.mkdir(parents=True, exist_ok=True)

    reset_scene()
    atlas_paths = build_atlases(source_root, texture_root)
    material = shared_export_material()
    family_reports = [
        build_family(family, source_root, model_root, material) for family in FAMILIES
    ]

    bpy.ops.wm.save_as_mainfile(filepath=str(args.master_blend))
    atlas_report = {
        name: {"path": repository_locator(path), "sha256": sha256(path)}
        for name, path in atlas_paths.items()
    }
    report = {
        "schema": "anotherlife.slagfall-meshy-cleanup.v1",
        "blender_version": bpy.app.version_string,
        "family_count": len(family_reports),
        "profiling_footprint_metres": PROFILING_FOOTPRINT_METRES,
        "gameplay_dimensions_authorized": False,
        "atlas_size": ATLAS_SIZE,
        "atlas_tile_inner_size": TILE_INNER,
        "lod_ratios": list(LOD_RATIOS),
        "master_blend": repository_locator(args.master_blend),
        "master_blend_sha256": sha256(args.master_blend),
        "atlases": atlas_report,
        "families": family_reports,
    }
    write_json_lf(args.report, report)
    print(
        json.dumps(
            {
                "family_count": len(family_reports),
                "model_root": str(model_root),
                "texture_root": str(texture_root),
                "master_blend": str(args.master_blend),
                "report": str(args.report),
            }
        )
    )


if __name__ == "__main__":
    main()
