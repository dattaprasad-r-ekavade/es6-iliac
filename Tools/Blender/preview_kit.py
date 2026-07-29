"""Render a contact sheet of a generated kit, so it can be eyeballed without opening Blender.

    blender -b -P Tools/Blender/preview_kit.py -- <kit_dir> <out.png>

The import axes deliberately match make_kit_sarrakh.py's export axes (Y-up, for Unity).
Importing with Blender's defaults instead makes every piece arrive lying on its face —
a useful smoke test that the export really is Unity-oriented.
"""
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
src, out = Path(argv[0]), Path(argv[1])

bpy.ops.wm.read_factory_settings(use_empty=True)

files = sorted(src.glob("*.fbx"))
placed, x = [], 0.0
for f in files:
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=str(f), axis_forward="-Z", axis_up="Y")
    for o in set(bpy.data.objects) - before:
        o.location.x = x
        placed.append(o)
    x += 6.5

# Bounds of everything, so the camera frames the row instead of guessing.
lo = Vector((1e9, 1e9, 1e9))
hi = Vector((-1e9, -1e9, -1e9))
for o in placed:
    if o.type != "MESH":
        continue
    for corner in o.bound_box:
        w = o.matrix_world @ Vector(corner)
        lo = Vector((min(lo[i], w[i]) for i in range(3)))
        hi = Vector((max(hi[i], w[i]) for i in range(3)))
center = (lo + hi) * 0.5
width, height = hi.x - lo.x, hi.z - lo.z

bpy.ops.mesh.primitive_plane_add(size=max(width, 40) * 6, location=(center.x, 0, -0.01))
gmat = bpy.data.materials.new("ground")
gmat.use_nodes = True
gmat.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.30, 0.24, 0.17, 1)
gmat.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 1.0
bpy.context.active_object.data.materials.append(gmat)

sun = bpy.data.lights.new("key", "SUN")
sun.energy, sun.angle = 5.0, math.radians(2)
so = bpy.data.objects.new("key", sun)
bpy.context.collection.objects.link(so)
so.rotation_euler = (math.radians(58), math.radians(4), math.radians(-28))

world = bpy.data.worlds.new("w")
bpy.context.scene.world = world
world.use_nodes = True
world.node_tree.nodes["Background"].inputs[0].default_value = (0.42, 0.55, 0.78, 1)
world.node_tree.nodes["Background"].inputs[1].default_value = 0.9

cam_data = bpy.data.cameras.new("cam")
cam_data.lens = 50
cam = bpy.data.objects.new("cam", cam_data)
bpy.context.collection.objects.link(cam)
bpy.context.scene.camera = cam

# Orthographic elevation. A perspective camera has to be positioned *and* aimed, and
# either being slightly wrong renders a confident picture of empty ground; ortho only
# needs a scale, so the framing is exact by construction.
RES_X, RES_Y = 1800, 760
cam_data.type = "ORTHO"
cam_data.ortho_scale = width * 1.06
cam.location = (center.x, -60.0, height * 0.62)
cam.rotation_euler = (math.pi / 2, 0.0, 0.0)

sc = bpy.context.scene
sc.render.engine = "BLENDER_EEVEE_NEXT"
sc.render.resolution_x, sc.render.resolution_y = RES_X, RES_Y
sc.render.filepath = str(out)
sc.view_settings.view_transform = "AgX"
bpy.ops.render.render(write_still=True)
print(f"PREVIEW_OK {out} pieces={len(files)} span={width:.1f}m tall={height:.1f}m")
