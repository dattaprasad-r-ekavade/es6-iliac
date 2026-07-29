# Kessil Bay map layout

Authored geography. All coordinates live in `WorldLayout.cs`; this file is the readable
summary of what that data builds.

## Orientation
- **+Z = North**, **+X = East**
- North: **Halbrand** (temperate / green)
- South: **Sarrakh** (arid / sand)
- Center: **Kessil Bay** (ocean)
- West opens toward open ocean

## Landmarks placed
| Location | Id | Role |
|---|---|---|
| Caldemar | `city_west` | Halbrand SW peninsula city (player spawn) |
| Estmere | `city_east` | Halbrand east shore, at the mouth of the Esk |
| Qadris | `city_south` | Sarrakh NW coast city |
| Tolm | `isle_west` | Western bay island |
| Corrath | `isle_center` | Central bay island + the Everspire |
| Sarn | `isle_south` | Island north of the Qadris coast |
| Karnoth Highlands | — | Northern Halbrand hills |
| Sarrakh Waste | — | Southern Sarrakh desert expanse |
| Kiln Hills | — | SE Sarrakh rocky hills |

Ids are the stable keys written into saves and quest targets; the display names above are
free to change without touching save data.

## Rebuild
Unity menu: **Kessil → World → Build Kessil Bay Map (Halbrand + Sarrakh)**
