import bpy
import time

def _start():
    # Prefer operator if present
    try:
        if hasattr(bpy.ops, "blendermcp") and hasattr(bpy.ops.blendermcp, "start_server"):
            bpy.ops.blendermcp.start_server()
            print("BlenderMCP: start_server operator called")
            return
    except Exception as e:
        print("BlenderMCP start_server op failed:", e)
    # Fallback: call module start
    try:
        import blender_mcp
        if hasattr(blender_mcp, "start_server"):
            blender_mcp.start_server()
            print("BlenderMCP: module start_server called")
            return
    except Exception as e:
        print("BlenderMCP module start failed:", e)
    print("BlenderMCP: open N-panel > BlenderMCP > Connect if not auto-started")

# Delay until UI is ready
bpy.app.timers.register(_start, first_interval=1.5)
print("BlenderMCP: queued auto-connect")
