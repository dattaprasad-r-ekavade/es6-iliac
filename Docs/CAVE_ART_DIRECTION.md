# Cave and interface revision — September 6, 2026

The previous build used brick courses on mine walls, regular paving underfoot and
axis-aligned slabs everywhere. Recolouring those surfaces could not make them caves.
The interface had the same problem: too many identical framed rectangles.

## Implemented

- Generated mines now request rock and gravel materials. Rock combines broad mineral
  variation, folded strata and interrupted fractures; gravel has fine grain and scattered
  pits without paving joints. Texture noise is periodic and uses fixed seeds.
- Rock surfaces have subdivided, faceted geometry and world-position texture coordinates.
  Separate rounded, irregular outcrops interrupt wall edges and the roof silhouette.
- Seeded perimeter formations have solid bounds. Their placement preserves the central
  combat area, enemy spawn exclusion band and door approaches. Roof formations stay above
  standing height. Existing collision remains conservative boxes; the visible rounded
  surface sits within those bounds, so close contact is not exact mesh collision.
- Underground closed gates use timber rather than brick. Masonry remains available for
  the town and authored structures.
- World geometry outside the viewing frustum is skipped; generated rock meshes are cached
  with a bounded cache. This reduces repeated geometry work, but is not a benchmark of the
  maximum-length campaign on minimum-spec hardware.
- Shared UI panels use faint material lines and rubbed edges. List rows use a leading
  selection mark and a quiet baseline rather than another complete frame. Conversation
  portraits are larger, with softened edges and no outer window frame around the scene.

## What still needs a deeper change

These are still chained combat chambers with flat walkable floors and orthogonal links.
This revision improves materials and silhouettes; it does not implement an organic cavern
generator. Do not describe it as winding tunnels or fully natural cave topology.

The next geometry milestone should replace square chamber boundaries with irregular
outlines and bent passages, while retaining an obvious retreat/continue threshold. Build
collision and navigation from the same shape, then test clearance across seeds. Avoid
hiding rectangular collision under elaborate cave art.

## Recommended visual identities

Three sharply different environments will do more than many recoloured versions:

1. **Worked seams:** narrow pick-cut faces, wedge scars, timber shoring, baskets of spoil,
   numbered stakes and soot above clay lamps. Human work should explain every straight edge.
2. **Water-cut caves:** broad worn rock, runoff channels, dark mineral staining, shallow pools
   and bends that reveal a light before revealing its source. Keep footing readable.
3. **The sealed workings:** crushed supports, hand-stacked rescue rubble, tally marks and
   remnants of Uttara's operation. Jiva veins should be sparse points of significance,
   not glowing wallpaper covering every wall.

For UI, pursue objects belonging to this setting: a tally tablet for the at-risk count,
cloth-backed inventory, and a clerk's ledger for records. Avoid putting ornamental bronze
around every interaction. A distinctive final HUD needs its own composed artwork and
hierarchy, beyond this shared-panel treatment.

## Verification

Run `verify.ps1 -Pack`. The smoke script checks generated outcrops and captures the cave;
the walk script traverses a real generated mine. Domain tests cover seeded generation,
spawn clearance, door blocking, routes and endless extension. Inspect actual dialogue
with `--yard --show revati`, and caves with `--exec "descend 3 20789; wait 1"`.
