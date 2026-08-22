# Ratna Bay UI font

The UI uses a two-tier type system:

- **Cinzel**, upright and without the decorative variant, for the game title, major
  headings, and feature labels. Its inscription-inspired capitals give the interface
  an old-world fantasy character without cursive swashes.
- **Noto Sans**, regular upright, for quest text, item names, controls, and compact UI
  labels. Its neutral shapes keep dense information readable at 720p and normal
  desktop/Steam Deck-like viewing distances.

The game loads both bundled variable TTFs through FontStashSharp at runtime. This keeps
the build independent of fonts installed on the developer's machine. FontStashSharp's
2× glyph resolution is used before the existing 1280×720 logical canvas is scaled to
the display, reducing the jagged edges seen in the first UI feasibility pass.

## License and provenance

- Source: <https://github.com/google/fonts/tree/main/ofl/cinzel>
- Font license: SIL Open Font License 1.1, included as `Cinzel/OFL.txt`
- Family description: `Cinzel/DESCRIPTION.en_us.html`
- Source: <https://github.com/google/fonts/tree/main/ofl/notosans>
- Font license: SIL Open Font License 1.1, included as `NotoSans/OFL.txt`
- Family description: `NotoSans/DESCRIPTION.en_us.html`
- Commercial bundling is allowed by the OFL; retain both licenses and attribution with
  the distributed game build.

## Usage rules

- Use Noto Sans for body copy and compact labels.
- Use Cinzel at larger sizes, with uppercase and the accent color, for headings instead
  of decorative swashes or all-caps body paragraphs.
- Keep body text at or above the logical 14 px token on the 1280×720 canvas.
- Review the font at 720p, 1080p, ultrawide, and Steam Deck-like viewing distances before
  locking the final UI typography.
