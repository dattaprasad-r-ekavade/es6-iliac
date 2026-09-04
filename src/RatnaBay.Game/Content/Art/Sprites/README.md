# Painted sprites

Drop a PNG in here named after a sprite key and the game stops forging that one.

    build\RatnaBay.exe --sprites art\        # write every generated sprite as a first draft
    (paint art\sword.png in Pixelorama)
    copy art\sword.png Content\Art\Sprites\  # the game uses it from the next launch

Keys are file names: `sword`, `jiva`, `bandit`, `risen.vetala`, `boss.khanda`, `fort.governor`.
The dump writes them all under the right names, at the right size, so start from those rather
than from a blank canvas — a sprite drawn at the wrong dimensions lands in the world the wrong
size, and one lit from the wrong side sits beside twenty that are not.

Nothing has to be painted. An empty folder means every sprite is generated, which is what the
game shipped with; painting one does not commit anyone to painting the rest.

A file whose name matches no key is loaded and never asked for — harmless, and easy to mistake
for the paint not having saved. Check the name against the dump.
