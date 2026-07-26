# Blender + Blender MCP setup

## Installed
- **Blender 4.5 LTS** via winget (`BlenderFoundation.Blender.LTS.4.5`)
- **Blender MCP addon** copied to `%APPDATA%\Blender Foundation\Blender\4.5\scripts\addons\blender_mcp.py`
- **Cursor MCP** server `blender` → `uvx blender-mcp` (project + user `.cursor/mcp.json`)
- Addon source mirror: `Tools/BlenderMCP/addon.py`

## Connect (each Blender session)
1. Fully **restart Cursor** so it picks up the new MCP server
2. Open Blender 4.5
3. Confirm addon: **Edit → Preferences → Add-ons** → search `Blender MCP` / `blender_mcp` → enabled
4. In the 3D Viewport press **N** → **BlenderMCP** tab → **Connect to Claude** (works with Cursor too)
5. In Cursor **Settings → MCP**, `blender` should show as connected once the addon is listening

## Asset upgrade path (Kenney → Skyrim-like)
Kenney kits are CC0 blockouts. For High Rock / Hammerfell look:
1. Import FBX from `Assets/ThirdParty/Kenney/` into Blender
2. Retopo / bevel / trim sheets, add wear, moss, wood grain
3. Bake normals / AO; export FBX or glTF back into Unity
4. Prefer Quaternius / Poly Haven / Kenney upgrades as bases; do **not** copy Bethesda meshes

Target feel: denser architecture, weathered stone, thatch/wood roofs, arid sandstone for Hammerfell — homage, not a Skyrim asset rip.
