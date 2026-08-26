# Releasing to itch.io

One command puts a build in front of strangers:

```powershell
.\release.ps1
```

Everything below is what that command does, and the three things only you can do first.

---

## The three things only you can do

### 1. Make the project page

<https://itch.io/game/new>

| Field | Set it to |
| --- | --- |
| Kind of project | **Downloadable** |
| Platforms | tick **Windows** |
| Pricing | **No payments** for an alpha |
| Visibility | **Restricted** at first, then **Public** |

Keep it **Restricted** while you check the build downloads and runs. Restricted still gives
you a shareable link, so playtesters can get in before the page is listed anywhere.

The URL will look like `https://yourname.itch.io/ratna-bay`. The part `release.ps1` wants is
`yourname/ratna-bay` — the same two names, joined by a slash.

### 2. Log butler in

```powershell
tools\butler\butler.exe login
```

Opens a browser once and stores a key under `%APPDATA%\itch`. It never touches this
repository and never goes in a commit. `tools/butler/` is gitignored — the binary is fetched
on demand, not checked in.

### 3. Name the project, once

```powershell
.\release.ps1 -Target yourname/ratna-bay
```

Remembered in `itch.target` (also gitignored). Every release after this is just
`.\release.ps1`.

---

## What a release actually does

1. **Checks the login and the project** — before compiling anything. A missing login is the
   most likely reason a first release fails, and finding that out after a two-minute build is
   two minutes wasted.
2. **Runs the whole gate** — `publish.ps1`: 549 domain tests, the deterministic playthrough
   simulation, the publish, the folder verification, and the published build's own
   `--selftest`. If any of it fails, nothing is uploaded.
3. **Stamps the version** — `alpha-YYYY.MM.DD-<sha>`, written to `build\version.txt`.
4. **Pushes** — incrementally. butler diffs against what is already on the channel, so the
   second push of a 130 MB self-contained build moves a few megabytes, not 130.

`-DryRun` does all of it except the push.

### The version matters

The stamp is not decoration. It goes into `build\version.txt`, the game reads it at startup,
and every uploaded recording carries it in its `Build` field. A report saying *"it crashed in
the third room"* can then be traced to the exact commit that player ran:

```powershell
git checkout a1b2c3d
```

If the working tree is dirty at release time the stamp gains a `-wip` suffix and the script
says so, because that traceability is exactly what uncommitted changes destroy. **Commit
before a real release.**

---

## Checking it worked

itch takes a minute or so to process an upload before the download button appears.

```powershell
tools\butler\butler.exe status yourname/ratna-bay
```

Then download it from the page yourself, on a machine that is not this one if you can. A
self-contained build carries its own runtime, so it should not need .NET installed — but that
is a claim worth testing once rather than assuming, and a tester who hits a missing-runtime
dialog does not file a bug, they close the window.

---

## What testers see on the first run

- **A consent prompt**, before anything is sent. Recordings only leave the machine if they
  say yes. Details in [SERVER_SETUP.md](SERVER_SETUP.md).
- **The coach**, ten one-line explanations that arrive as each thing first becomes true —
  standing at the shaft, watching the first body rise, facing the first shut door with stones
  in the pot. Each is shown once, ever, remembered per installation.

Recordings land on the server as JSON. Read one with:

```powershell
dotnet run --project tools\RatnaBay.Tools -- review <recording.json>
```

---

## The page text

Two files, pasted into the two fields itch.io gives you:

| File | itch.io field |
| --- | --- |
| [`itch-description.html`](itch-description.html) | the project description |
| [`itch-install.html`](itch-install.html) | Install instructions |

They live here rather than only in the browser so the page can be reviewed in a diff like
anything else, and so a claim it makes about the game can be checked against the code that
has to keep it true.

**One thing must be edited before pasting.** The description carries a placeholder address,
`you@example.com`, on a reserved domain that reaches nobody. Replace it — in both the
`mailto:` and the visible link text — with a channel you own. The warning lives here rather
than in a comment inside the HTML, because a comment is one sanitiser's whitelist away from
being published on the page it was meant to keep clean.

This is not optional: a free download makes nobody an *owner* on itch.io and the
mass-email tool is gated on having made a sale, so **itch.io gives you no way whatsoever to
contact anybody who plays this**. See [PLAYTEST_DISTRIBUTION.md](PLAYTEST_DISTRIBUTION.md)
§2.3. Comments are the fallback, but they need the reader to hold an account, and the person
whose answer is worth most — the one who bounced off in room two — is exactly the person who
will not make one.

The description asks for one thing specifically, because a page that asks for feedback in
general gets "it was fun":

> **At the shut door — did you ever actually hesitate?**

That is the decision the whole game is built on, and it is the one thing the recordings can
measure but not explain. It sits directly under the pitch rather than below the controls,
which is where it used to be — most readers never reach the bottom of a store page, and this
question is the reason the page exists.
