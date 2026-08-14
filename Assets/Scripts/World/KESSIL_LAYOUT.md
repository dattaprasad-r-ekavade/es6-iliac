# Ratna Bay map layout

Authored geography. All coordinates live in `WorldLayout.cs`; this file is the readable
summary of what that data builds.

## Orientation
- **+Z = North**, **+X = East**
- North: **Uttara** (temperate / green)
- South: **Maru** (arid / sand)
- Center: **Ratna Bay** (ocean)
- West opens toward open ocean

## Landmarks placed
| Location | Id | Role |
|---|---|---|
| Sabhapur | `city_west` | Uttara SW peninsula city (legacy free-roam spawn) |
| Ratnapur | `city_east` | Uttara east shore, at the mouth of the Nira |
| Marukot | `city_south` | Maru NW coast city |
| Kusha | `isle_west` | Western bay island |
| Meru | `isle_center` | Central bay island + the Stambha |
| Shaka | `isle_south` | Island north of the Marukot coast |
| Giri Highlands | — | Northern Uttara hills |
| Maru Waste | — | Southern desert expanse |
| Agni Hills | — | SE Maru rocky hills |

Ids are the stable keys written into saves and quest targets; the display names above are
free to change without touching save data.

## Rebuild
Unity menu: **Kessil → World → Build Kessil Bay Map (Halbrand + Sarrakh)**. This editor path
and the world JSON's biome/name fields are internal codenames retained for tooling and saves;
they are not player-facing names.
