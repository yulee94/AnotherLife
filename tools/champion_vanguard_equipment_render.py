import bpy
import os

out_dir = os.path.join(os.path.dirname(bpy.data.filepath), "preview")
os.makedirs(out_dir, exist_ok=True)

scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 900
scene.render.resolution_y = 1200
scene.render.film_transparent = False
scene.render.image_settings.file_format = "PNG"

# hide the FORWARD marker + anchors for clean renders
for o in bpy.data.objects:
    o.hide_render = False
for n in ("FORWARD_Unity+Z", "PetAnchor", "MountAnchor", "VFX_ChestAnchor",
          "VFX_Hand_L", "VFX_Hand_R"):
    if n in bpy.data.objects:
        bpy.data.objects[n].hide_render = True

def render_ortho(name, loc, rot, ortho_scale=2.6):
    cam_data = bpy.data.cameras.new(name + "_cam")
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = ortho_scale
    cam = bpy.data.objects.new(name + "_cam", cam_data)
    bpy.context.collection.objects.link(cam)
    cam.location = loc
    cam.rotation_euler = rot
    scene.camera = cam
    scene.render.filepath = os.path.join(out_dir, "champion_vanguard_equipment_" + name + ".png")
    bpy.ops.render.render(write_still=True)

# front view: character faces -Y, so camera sits at -Y looking toward +Y
render_ortho("front", (0.0, -4.2, 1.0), (1.570796, 0.0, 0.0))
# side view: camera at +X looking toward -X (right side of character)
render_ortho("side", (4.2, 0.0, 1.0), (1.570796, 0.0, 1.570796))
# three-quarter (perspective-ish using existing camera position)
cam_data = bpy.data.cameras.new("threequarter_cam")
cam_data.type = "ORTHO"
cam_data.ortho_scale = 2.8
cam = bpy.data.objects.new("threequarter_cam", cam_data)
bpy.context.collection.objects.link(cam)
cam.location = (3.2, -4.5, 1.9)
# look at origin
from mathutils import Vector
dir = Vector((0, 0, 1.0)) - cam.location
cam.rotation_euler = dir.to_track_quat('-Z', 'Y').to_euler()
scene.camera = cam
scene.render.filepath = os.path.join(out_dir, "champion_vanguard_equipment_threequarter.png")
bpy.ops.render.render(write_still=True)

print("RENDERED:", os.listdir(out_dir))
