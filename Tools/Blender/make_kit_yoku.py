"""Generate a modular desert ("Yoku") architecture kit for the southern region.

Run headless:
    blender -b -P Tools/Blender/make_kit_yoku.py -- Assets/Art/Generated/YokuKit

Why this exists
---------------
Free asset packs are the fastest way to fill a world and the fastest way to make it
look like five different games stapled together: different scales, different pivots,
different bevel language, different texel density. A generated kit fixes the style in
one place — every piece here shares a module size, a bevel width and a pivot rule, so
a street built from it reads as one civilisation.

Conventions every piece follows (this is the point):
  * 4 m module grid, metres, real-world scale
  * origin at base centre, so pieces drop onto terrain and rotate about their footprint
  * +Z up in Blender, exported Y-up for Unity
  * uniform bevel so silhouettes catch light consistently
  * one material slot named `M_Yoku_Sandstone` for a single shared Unity material
"""
from __future__ import annotations

import math
import sys
from pathlib import Path

import bpy

MODULE = 4.0          # grid size in metres
WALL_THICKNESS = 0.45
BEVEL_WIDTH = 0.025
MATERIAL_NAME = "M_Yoku_Sandstone"


# ---------------------------------------------------------------- scene helpers

def reset_scene() -> None:
    bpy.ops.wm.read_factory_settings(use_empty=True)


def active(obj) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def cube(name: str, size: tuple[float, float, float], loc=(0.0, 0.0, 0.0)):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=loc)
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = size
    active(obj)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return obj


def cylinder(name: str, radius: float, depth: float, loc=(0.0, 0.0, 0.0),
             rot=(0.0, 0.0, 0.0), verts: int = 48):
    bpy.ops.mesh.primitive_cylinder_add(radius=radius, depth=depth, location=loc,
                                        rotation=rot, vertices=verts)
    obj = bpy.context.active_object
    obj.name = name
    return obj


def boolean_cut(target, cutter) -> None:
    active(target)
    mod = target.modifiers.new(name="cut", type="BOOLEAN")
    mod.object = cutter
    mod.operation = "DIFFERENCE"
    mod.solver = "EXACT"
    bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.data.objects.remove(cutter, do_unlink=True)


def join(objs, name: str):
    keep = objs[0]
    active(keep)
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = keep
    bpy.ops.object.join()
    keep.name = name
    return keep


def set_base_origin(obj) -> None:
    """Origin at the centre of the footprint, on the ground plane."""
    active(obj)
    bpy.ops.object.origin_set(type="ORIGIN_GEOMETRY", center="BOUNDS")
    lowest = min((obj.matrix_world @ v.co).z for v in obj.data.vertices)
    offset = obj.location.z - lowest
    obj.data.transform(__import__("mathutils").Matrix.Translation((0.0, 0.0, -offset)))
    obj.location.z = 0.0
    obj.location.x = 0.0
    obj.location.y = 0.0


def finish(obj, bevel: bool = True) -> None:
    """Uniform bevel + smooth-by-angle + shared material + base origin."""
    active(obj)
    if bevel:
        mod = obj.modifiers.new(name="bevel", type="BEVEL")
        mod.width = BEVEL_WIDTH
        mod.segments = 2
        mod.limit_method = "ANGLE"
        mod.angle_limit = math.radians(40)
        mod.harden_normals = False
        bpy.ops.object.modifier_apply(modifier=mod.name)

    # Blender 4.1+ replaced mesh.use_auto_smooth with a shade-by-angle operator.
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(35))

    obj.data.materials.clear()
    obj.data.materials.append(shared_material())
    set_base_origin(obj)


def shared_material():
    mat = bpy.data.materials.get(MATERIAL_NAME)
    if mat is None:
        mat = bpy.data.materials.new(MATERIAL_NAME)
        mat.use_nodes = True
        bsdf = mat.node_tree.nodes.get("Principled BSDF")
        if bsdf:
            bsdf.inputs["Base Color"].default_value = (0.78, 0.62, 0.40, 1.0)
            bsdf.inputs["Roughness"].default_value = 0.85
    return mat


# ---------------------------------------------------------------- kit pieces

def piece_wall():
    return cube("Yoku_Wall", (MODULE, WALL_THICKNESS, MODULE),
                loc=(0, 0, MODULE / 2))


def piece_wall_window():
    wall = cube("Yoku_Wall_Window", (MODULE, WALL_THICKNESS, MODULE),
                loc=(0, 0, MODULE / 2))
    # Narrow slit windows — desert architecture keeps the sun out.
    for x in (-0.7, 0.7):
        cutter = cube("cut", (0.45, WALL_THICKNESS * 3, 1.5), loc=(x, 0, MODULE * 0.62))
        boolean_cut(wall, cutter)
    return wall


def piece_arch():
    """Horseshoe arch — the most recognisable tell of the style."""
    wall = cube("Yoku_Arch", (MODULE, WALL_THICKNESS, MODULE), loc=(0, 0, MODULE / 2))

    radius = 1.15
    springline = 2.0

    # Two sequential cuts rather than one joined cutter: joining the cylinder and the
    # box produces a self-intersecting mesh, and the EXACT solver answers that with an
    # empty result rather than an error.
    head = cylinder("cut_head", radius, WALL_THICKNESS * 3,
                    loc=(0, 0, springline), rot=(math.radians(90), 0, 0))
    boolean_cut(wall, head)

    # Body below the springline, slightly narrower so the arch "returns" inward.
    body = cube("cut_body", (radius * 1.88, WALL_THICKNESS * 3, springline),
                loc=(0, 0, springline / 2))
    boolean_cut(wall, body)
    return wall


def piece_pillar():
    shaft = cylinder("Yoku_Pillar", 0.32, 3.4, loc=(0, 0, 1.7), verts=12)
    base = cube("pillar_base", (0.85, 0.85, 0.3), loc=(0, 0, 0.15))
    cap = cube("pillar_cap", (0.95, 0.95, 0.35), loc=(0, 0, 3.55))
    return join([shaft, base, cap], "Yoku_Pillar")


def piece_dome():
    """Drum + hemisphere + finial — the skyline silhouette."""
    drum = cylinder("Yoku_Dome", 2.0, 1.2, loc=(0, 0, 0.6), verts=32)

    bpy.ops.mesh.primitive_uv_sphere_add(radius=2.0, location=(0, 0, 1.2),
                                         segments=32, ring_count=16)
    sphere = bpy.context.active_object
    sphere.scale = (1.0, 1.0, 1.15)
    active(sphere)
    bpy.ops.object.transform_apply(scale=True)
    # Trim the lower half of the sphere.
    trim = cube("cut_lower", (6, 6, 4), loc=(0, 0, 1.2 - 2.0))
    boolean_cut(sphere, trim)

    bpy.ops.mesh.primitive_cone_add(radius1=0.28, depth=0.8, location=(0, 0, 3.9), vertices=12)
    finial = bpy.context.active_object

    return join([drum, sphere, finial], "Yoku_Dome")


def piece_parapet():
    """Crenellated parapet strip that tiles along a roof edge."""
    rail = cube("Yoku_Parapet", (MODULE, WALL_THICKNESS, 0.5), loc=(0, 0, 0.25))
    merlons = [rail]
    count = 5
    for i in range(count):
        x = -MODULE / 2 + MODULE * (i + 0.5) / count
        merlons.append(cube(f"merlon_{i}", (0.45, WALL_THICKNESS, 0.55), loc=(x, 0, 0.75)))
    return join(merlons, "Yoku_Parapet")


def piece_stairs():
    steps = []
    count = 8
    rise, run = 0.22, 0.34
    for i in range(count):
        steps.append(cube(f"step_{i}",
                          (MODULE * 0.75, run, rise * (i + 1)),
                          loc=(0, -run * i, rise * (i + 1) / 2)))
    return join(steps, "Yoku_Stairs")


PIECES = {
    "Yoku_Wall": piece_wall,
    "Yoku_Wall_Window": piece_wall_window,
    "Yoku_Arch": piece_arch,
    "Yoku_Pillar": piece_pillar,
    "Yoku_Dome": piece_dome,
    "Yoku_Parapet": piece_parapet,
    "Yoku_Stairs": piece_stairs,
}


# ---------------------------------------------------------------- export

def export(obj, out_dir: Path) -> Path:
    out_dir.mkdir(parents=True, exist_ok=True)
    path = out_dir / f"{obj.name}.fbx"
    active(obj)
    bpy.ops.export_scene.fbx(
        filepath=str(path),
        use_selection=True,
        apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z",
        axis_up="Y",            # Unity convention
        mesh_smooth_type="FACE",
        # Must stay False. With bake_space_transform=True the axis conversion is baked
        # into the mesh *and* left on the node transform, so the piece arrives rotated
        # 90 degrees — a 4 x 0.45 x 4 wall imports as 4 x 4 x 0.45, lying flat.
        bake_space_transform=False,
        path_mode="COPY",
    )
    return path


def main() -> None:
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    out_dir = Path(argv[0]) if argv else Path("Assets/Art/Generated/YokuKit")
    if not out_dir.is_absolute():
        out_dir = Path(__file__).resolve().parents[2] / out_dir

    total_tris = 0
    for name, build in PIECES.items():
        reset_scene()
        obj = build()
        obj.name = name
        finish(obj)
        obj.data.calc_loop_triangles()
        tris = len(obj.data.loop_triangles)
        total_tris += tris
        path = export(obj, out_dir)
        print(f"KIT {name:20} {tris:6d} tris  -> {path.name}")

    print(f"KIT_DONE {len(PIECES)} pieces, {total_tris} tris total, out={out_dir}")


if __name__ == "__main__":
    main()
