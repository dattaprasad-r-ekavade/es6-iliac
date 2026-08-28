#requires -Version 5.1
<#
.SYNOPSIS
    Builds the game and pushes it to itch.io.

.DESCRIPTION
    publish.ps1 makes a build you can trust. This makes one strangers can download.

    It runs the whole gate first -- domain tests, simulation, publish, verify, and the
    published build's own self-test -- and refuses to upload anything that does not pass.
    A broken alpha does not produce bad feedback, it produces no feedback, because nobody
    files a report about a game that would not start.

    Uploads are incremental. butler diffs against what is already on the channel and sends
    only the blocks that changed, so the second push of a 130 MB self-contained build moves
    a few megabytes. That is worth the tool existing; do not replace it with a zip upload.

.PARAMETER Target
    The itch.io project, as user/game -- for example 'datathecodie/ratna-bay'. Remembered in
    itch.target after the first run, so later runs need no arguments.

.PARAMETER Channel
    The itch channel. 'windows' tells itch this is a Windows download and makes the launcher
    offer it correctly. Leave it alone unless you are publishing a second platform.

.PARAMETER DryRun
    Build and gate, then say what would be pushed without pushing it.

.EXAMPLE
    .\release.ps1 -Target datathecodie/ratna-bay
    The first release. Afterwards: .\release.ps1
#>
[CmdletBinding()]
param(
    [string]$Target = "",
    [string]$Channel = "windows",
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

$butlerDir = Join-Path $root "tools\butler"
$butler = Join-Path $butlerDir "butler.exe"
$targetFile = Join-Path $root "itch.target"
$buildDir = Join-Path $root "build"

function Say([string]$text, [string]$colour = "Gray") { Write-Host $text -ForegroundColor $colour }
function Step([string]$text) { Write-Host ""; Write-Host "==> $text" -ForegroundColor Cyan }
function Die([string]$text) { Write-Host ""; Write-Host "  $text" -ForegroundColor Red; exit 1 }

# Windows PowerShell turns any line a native tool writes to stderr into a terminating error
# while ErrorActionPreference is Stop -- and butler writes its version banner there. Native
# calls go through here so that only a non-zero exit code counts as failure, which is what
# an exit code is for. Every caller checks $LASTEXITCODE itself.
function Native {
    param([scriptblock]$Command)
    $previous = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try { & $Command 2>&1 } finally { $ErrorActionPreference = $previous }
}

# ---------------------------------------------------------------- who we are pushing to
if (-not $Target) {
    if (Test-Path $targetFile) { $Target = (Get-Content $targetFile -Raw).Trim() }
}

if (-not $Target) {
    Die @"
No itch.io project set.

Create the project first at https://itch.io/game/new -- as a Downloadable game, with
'Windows' ticked under platforms. Then run this once with the target:

    .\release.ps1 -Target yourname/ratna-bay

The target is the part of the page URL after itch.io/, with the slash kept. It is
remembered afterwards.
"@
}

if ($Target -notmatch '^[A-Za-z0-9_\-]+/[A-Za-z0-9_\-]+$') {
    Die "'$Target' does not look like user/game. It should be two names with a slash, no https and no colon."
}

# ---------------------------------------------------------------- butler itself
if (-not (Test-Path $butler)) {
    Step "Fetching butler"
    New-Item -ItemType Directory -Force $butlerDir | Out-Null
    $zip = Join-Path $env:TEMP "butler-$(Get-Random).zip"
    Invoke-WebRequest -Uri "https://broth.itch.zone/butler/windows-amd64/LATEST/archive/default" `
        -OutFile $zip -UseBasicParsing
    Expand-Archive -Path $zip -DestinationPath $butlerDir -Force
    Remove-Item $zip -Force
    Say "    [ok] installed to tools\butler"
}

Native { & $butler -V } | Out-Null
if ($LASTEXITCODE -ne 0) { Die "butler will not run. Delete tools\butler and try again." }

# butler keeps its own credentials; it does not read anything of ours. A missing login is
# the single most likely reason a first release fails, so it is checked before the build
# rather than after fifteen seconds of compiling.
Step "Checking the itch.io login"
$who = Native { & $butler status $Target } | Out-String
if ($LASTEXITCODE -ne 0) {
    if ($who -match "(?i)not logged in|no credentials|authenticate") {
        Die @"
butler is not logged in.

    tools\butler\butler.exe login

That opens a browser once and stores a key under %APPDATA%\itch. It never touches this
repository, and the key is not something to paste anywhere.
"@
    }

    Die @"
butler cannot see $Target.

$($who.Trim())

Check the project exists, that it is yours, and that the name is spelled as it is in the URL.
"@
}
Say "    [ok] $Target is reachable"

# Remembered as soon as it is known good, rather than after a successful push. The target was
# previously only written at the very end, so every dry run before the first real release had
# to retype it -- which is exactly the run you repeat most while getting a first build ready.
Set-Content -Path $targetFile -Value $Target -Encoding utf8 -NoNewline

# ---------------------------------------------------------------- the gate
Step "Running the full build gate"
Native { & (Join-Path $root "publish.ps1") } | ForEach-Object { Write-Host $_ }
if ($LASTEXITCODE -ne 0) { Die "The gate failed. Nothing was uploaded." }

if (-not (Test-Path (Join-Path $buildDir "RatnaBay.exe"))) {
    Die "publish.ps1 reported success but build\RatnaBay.exe is missing."
}

# ---------------------------------------------------------------- the version people will quote
# Date first so channel history sorts readably, commit last so a bug report naming a version
# is enough to check out exactly what they played. Telemetry carries the same string.
$stamp = Get-Date -Format "yyyy.MM.dd"
$sha = (Native { & git rev-parse --short HEAD } | Select-Object -First 1)
if (-not $sha) { $sha = "nogit" }

$dirty = Native { & git status --porcelain }
if ($dirty) {
    Say ""
    Say "  Uncommitted changes are in this build. Whatever a player reports, you will not be" Yellow
    Say "  able to check out exactly what they ran. Commit first if this is a real release." Yellow
    $sha = "$sha-wip"
}

$version = "alpha-$stamp-$sha"

# The build has to know its own name before it is uploaded, or a bug report naming a version
# cannot be matched to a recording. Telemetry reads this file at startup.
Set-Content -Path (Join-Path $buildDir "version.txt") -Value $version -Encoding utf8 -NoNewline

$size = "{0:N1} MB" -f ((Get-ChildItem $buildDir -Recurse -File | Measure-Object Length -Sum).Sum / 1MB)

Step "Pushing to itch.io"
Say "    project   $Target"
Say "    channel   $Channel"
Say "    version   $version"
Say "    size      $size"

if ($DryRun) {
    Write-Host ""
    Say "  Dry run. Nothing was uploaded." Yellow
    exit 0
}

Native { & $butler push $buildDir "${Target}:${Channel}" --userversion $version } |
    ForEach-Object { Write-Host $_ }
if ($LASTEXITCODE -ne 0) { Die "The upload failed. Nothing on itch.io changed." }

Write-Host ""
Say "  Pushed $version" Green
Say "  Page      https://$($Target.Split('/')[0]).itch.io/$($Target.Split('/')[1])"
Say "  Builds    https://itch.io/dashboard"
Write-Host ""
Say "  itch processes the upload for a minute or so before the download button appears."
Say "  Recordings from this build arrive tagged $version."
