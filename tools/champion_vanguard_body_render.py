"""Render preview turntable views of the body modules (evidence only; does not save)."""
import bpy
import math
import os
from mathutils import Vector

scn = bpy.context.scene
scn.render.engine = 'BLENDER_EEVEE'
scn.render.resolution_x = 1024
scn.render.resolution_y = 1024
scn.render.film_transparent = True

cam = bpy.data.objects.get("Camera")
if cam is None:
    cam = bpy.data.objects.new("Camera", bpy.data.cameras.new("Camera"))
    bpy.context.collection.objects.link(cam)
    scn.camera = cam
scn.camera = cam

def look_at(cam_obj, target):
    direction = (Vector(target) - cam_obj.location)
    cam_obj.rotation_euler = direction.to_track_quat('-Z', 'Y').to_euler()

outdir = os.path.join(os.path.dirname(bpy.path.abspath("//")), "preview")
os.makedirs(outdir, exist_ok=True)

target = (0.0, 0.0, 0.9)
views = [
    ("front", (0.0, -3.6, 0.95)),
    ("threequarter", (2.6, -3.0, 1.15)),
    ("back", (0.0, 3.6, 0.95)),
]
for name, pos in views:
    cam.location = pos
    look_at(cam, target)
    scn.render.filepath = os.path.join(outdir, "champion_vanguard_body_%s.png" % name)
    bpy.ops.render.render(write_still=True)
    print("rendered:", scn.render.filepath)

print("PREVIEW DIR:", outdir)
