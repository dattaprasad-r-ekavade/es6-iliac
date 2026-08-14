# Arena Miniature environment slice

**Status (2026-08-12):** deterministic builder, Unity regeneration, both proof captures and
automated geometry/collision checks are complete. The real-controller walkthrough and 45+ FPS
minimum-machine acceptance checks remain open; this document does not claim they passed.

The first exterior target is the dock-to-city street in `Capital_Region`. The first interior
target is the four-room Ratnapur prison. Both use only the generated 64 px, point-filtered
`Plaster`, `Stone`, `Roof`, `Timber`, `Foliage` and `Water` surfaces. No downloaded PBR material
or hand-edited generated scene is part of the slice.

## Rebuild and capture

In Unity use **Kessil → Art Direction → Build + Capture Arena Miniature Slice**, or run the
headless execute method:

```text
ArenaMiniatureSliceBuilder.BuildAndCapture
```

It rebakes procedural surfaces, rebuilds `Capital_Region` and all Chapter 01 interiors, and
writes these approval images:

- `Docs/Screenshots/arena-miniature-ratnapur-street.png`
- `Docs/Screenshots/arena-miniature-prison.png`

Approved visual captures:

![Ratnapur dock street](Screenshots/arena-miniature-ratnapur-street.png)

![Ratnapur prison](Screenshots/arena-miniature-prison.png)

The generated scenes remain rebuild artifacts. Do not hand-author into them.

## Reusable contract

`ArenaMiniatureSliceLayout` contains stable facade, street-prop and dungeon-module records.
`ArenaMiniatureSliceBuilder` is their Unity materializer. This split is intentional: the W-11
external editor can later emit the same records from data rather than gaining a separate set of
one-off art rules.

The street contract guarantees a 16 m clear central lane, reserves its full footprint from the
generic city-block pass, and gives every non-enterable facade one simple body collider. Thin
painted registers and decoration are collider-free. The dungeon contract places all dressing and
route mechanics inside real chamber bounds; the previous mechanic position was beyond the sealed
far wall.

## Acceptance ledger

1. [x] Both proof images show readable pigment fields and dark drawn contours without PBR glare.
2. [ ] Walk from the docks through the full street with a real controller, without collision
   snags or overlapping buildings. Automated clear-lane/collider checks pass.
3. [ ] Enter the prison with a real controller, reach the deepest room and use its route
   mechanic. Automated chamber-bound placement checks pass.
4. [x] Rebuild twice and confirm module positions and captures do not change.
5. [ ] Keep 45+ FPS on the minimum target machine in the street view.

The standalone [`Ratna World Builder`](../Tools/WorldBuilder/README.md) MVP was completed after
the automated/visual proof. Do not propagate this treatment across the full W-12 region or call
the slice performance-approved until the three unchecked human/performance gates are closed.
