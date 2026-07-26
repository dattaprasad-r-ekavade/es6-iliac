"""
First-pass Kenney → denser High-Rock / Hammerfell look (homage, not Bethesda rips).

Usage (Blender GUI with a file open, or):
  blender --python Tools/BlenderMCP/upgrade_kenney_pass.py -- <input.fbx> <output.fbx>

What it does:
  - Imports FBX
  - Applies scale/rotation
  - Bevels hard edges slightly (stone/wood read)
  - Adds Weighted Normal-friendly shading (auto smooth)
  - Exports FBX for Unity reimport
"""
from __future__ import annotations

import sys
from pathlib import Path

import bpy


def clear_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for block in bpy.data.meshes:
        if block.users == 0:
            bpy.data.meshes.remove(block)


def import_fbx(path: Path) -> list:
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=str(path), automatic_bone_orientation=True)
    return [o for o in bpy.data.objects if o not in before]


def upgrade_object(obj) -> None:
    if obj.type != "MESH":
        return
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)

    # Mild bevel — reads less "toy brick", more carved stone/wood.
    bevel = obj.modifiers.new(name="HomageBevel", type="BEVEL")
    bevel.width = 0.02
    bevel.segments = 2
    bevel.limit_method = "ANGLE"
    bevel.angle_limit = 0.523599  # 30 deg

    # Weighted normals / auto smooth for harder material breaks.
    if hasattr(obj.data, "use_auto_smooth"):
        obj.data.use_auto_smooth = True
        obj.data.auto_smooth_angle = 0.785398  # 45 deg

    bpy.ops.object.shade_smooth()
    obj.select_set(False)


def export_fbx(path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=str(path),
        use_selection=False,
        apply_scale_options="FBX_SCALE_ALL",
        mesh_smooth_type="FACE",
        path_mode="COPY",
        embed_textures=True,
    )


def main(argv: list[str]) -> None:
    # Args after `--`
    if "--" in argv:
        argv = argv[argv.index("--") + 1 :]
    else:
        argv = []

    project = Path(r"D:\Projects\Elder Scrolls 6")
    default_in = project / "Assets/ThirdParty/Kenney/CastleKit"
    # Pick first fbx if no args
    if len(argv) >= 2:
        src = Path(argv[0])
        dst = Path(argv[1])
    else:
        fbxs = sorted(default_in.rglob("*.fbx"))
        if not fbxs:
            print("No FBX found under CastleKit")
            return
        src = fbxs[0]
        dst = project / "Assets/Art/Upgraded" / (src.stem + "_upgraded.fbx")

    print("UPGRADE", src, "->", dst)
    clear_scene()
    objs = import_fbx(src)
    for o in objs:
        upgrade_object(o)
    export_fbx(dst)
    print("DONE", dst)


if __name__ == "__main__":
    main(sys.argv)
