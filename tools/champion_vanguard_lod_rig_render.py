"""
Render turntable captures for the Champion Vanguard LOD+rig candidate.

Produces (in unity/ArtSource/Champions/preview/):
  - champion_vanguard_turntable_000.png .. _315.png   (8 high-LOD ortho angles)
  - champion_vanguard_lod1_front.png / _threequarter.png
  - champion_vanguard_lod2_front.png / _threequarter.png

Camera orbits a fixed rig in world space; lights stay fixed so the turntable is
consistent. The character faces -Y (front), so angle 0 == front view.
"""
import os
import math
import bpy
from mathutils import Vector

BLEND = r"C:\Users\MY\Documents\AnotherLife\.worktrees\t_eaaabf32\unity\ArtSource\Champions\champion_vanguard_working_v001.blend"
OUT_DIR = os.path.join(os.path.dirname(BLEND), "preview")
os.makedirs(OUT_DIR, exist_ok=True)

bpy.ops.wm.open_mainfile(filepath=BLEND)

scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 900
scene.render.resolution_y = 1200
scene.render.film_transparent = False
scene.render.image_settings.file_format = "PNG"

# neutral studio background
world = bpy.data.worlds.get("World")
if world is None:
    world = bpy.data.worlds.new("World")
scene.world = world
world.use_nodes = True
bg = world.node_tree.nodes.get("Background")
if bg is not None:
    bg.inputs["Color"].default_value = (0.14, 0.15, 0.17, 1.0)

# ---- lighting (fixed world-space) ----
def make_light(name, type, energy, loc, rot=(0, 0, 0)):
    data = bpy.data.lights.new(name, type)
    data.energy = energy
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    obj.location = loc
    obj.rotation_euler = rot
    return obj

# remove the scaffold point light at origin and add a proper 3-point rig
if "Light" in bpy.data.objects:
    bpy.data.objects.remove(bpy.data.objects["Light"], do_unlink=True)

key = make_light("Key_Sun", "SUN", 3.2, (4.0, -4.0, 6.0))
key.rotation_euler = (math.radians(50), 0, math.radians(40))
fill = make_light("Fill_Area", "AREA", 150.0, (-3.5, 2.0, 2.0))
fill.data.size = 3.0
rim = make_light("Rim_Area", "AREA", 220.0, (0.0, 5.0, 3.0))
rim.data.size = 2.0

# ---- visibility helpers ----
HELPERS = ["FORWARD_Unity+Z", "PetAnchor", "MountAnchor", "VFX_ChestAnchor",
           "VFX_Hand_L", "VFX_Hand_R", "Champion_Vanguard_Rig"]
LOD0_NAMES = [
    "SM_Head", "SM_Hair", "SM_Face", "SM_Eye_L", "SM_Eye_R", "SM_Torso",
    "SM_Arm_L", "SM_Arm_R", "SM_Leg_L", "SM_Leg_R",
    "Shoulder_L", "Shoulder_R", "Cape", "Weapon_Main", "Shield_Off", "Realm_Ornament",
]


def show_only(visible_set):
    for o in bpy.data.objects:
        o.hide_render = True
    for n in visible_set:
        if n in bpy.data.objects:
            bpy.data.objects[n].hide_render = False


# ---- camera ----
cam_data = bpy.data.cameras.new("TurntableCam")
cam_data.type = "ORTHO"
cam_data.ortho_scale = 2.9
cam = bpy.data.objects.new("TurntableCam", cam_data)
bpy.context.collection.objects.link(cam)
scene.camera = cam

TARGET = Vector((0.0, 0.0, 0.92))


def aim(radius, angle_deg, height):
    a = math.radians(angle_deg)
    pos = Vector((radius * math.sin(a), -radius * math.cos(a), height))
    cam.location = pos
    # camera forward is -Z; aim it from the camera toward the target
    cam.rotation_euler = (TARGET - pos).to_track_quat("-Z", "Y").to_euler()


def render_to(name):
    scene.render.filepath = os.path.join(OUT_DIR, name)
    bpy.ops.render.render(write_still=True)


# ---- high-LOD turntable (8 angles) ----
show_only(LOD0_NAMES)
for i in range(8):
    angle = i * 45
    aim(4.0, angle, 1.0)
    render_to(f"champion_vanguard_turntable_{angle:03d}.png")

# ---- medium & low LOD front + threequarter ----
lod1 = [n + "_LOD1" for n in LOD0_NAMES]
lod2 = [n + "_LOD2" for n in LOD0_NAMES]

show_only(lod1)
aim(4.0, 0, 1.0)
render_to("champion_vanguard_lod1_front.png")
aim(4.0, 45, 1.1)
render_to("champion_vanguard_lod1_threequarter.png")

show_only(lod2)
aim(4.0, 0, 1.0)
render_to("champion_vanguard_lod2_front.png")
aim(4.0, 45, 1.1)
render_to("champion_vanguard_lod2_threequarter.png")

print("RENDERED:", sorted(os.listdir(OUT_DIR)))
