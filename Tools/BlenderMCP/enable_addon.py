import bpy
import addon_utils
addon_utils.enable('blender_mcp', default_set=True, persistent=True)
bpy.ops.wm.save_userpref()
print('ADDON_ENABLED', 'blender_mcp' in bpy.context.preferences.addons)
