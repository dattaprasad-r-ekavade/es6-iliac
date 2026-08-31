<#
.SYNOPSIS
    One command that proves a Ratna Bay change is safe to commit.

.DESCRIPTION
    Runs the Release build, tool doctor, domain tests, content validation, the
    deterministic simulation, and a scripted playthrough of the built client. This is the
    check AGENTS.md asks for.

    The playthrough is the only step that draws a frame, and so the only one that can catch a
    rendering fault. It needs a display.

    Packaging (publish.ps1 and the packaged self-test) is opt-in with -Pack, because it
    needs a Windows machine and writes into .\build.

.PARAMETER Pack
    Also run publish.ps1 -SkipTests, then drive the packaged build through the smoke script.
    That last step is the only gate that asserts on a running client, so it needs the exe.

.PARAMETER Configuration
    Build configuration. Release is the default, matching the publish gate.
#>
param(
    [switch] $Pack,

    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path $PSScriptRoot).Path

function Assert-LastExitCode([string] $what) {
    if ($LASTEXITCODE -ne 0) { throw "$what failed with exit code $LASTEXITCODE" }
}

<#
.SYNOPSIS
    Run smoke.rbs through a client and fail if it does not pass.

.DESCRIPTION
    Start-Process -Wait, not `& $client`. The game is a WinExe, so PowerShell does not block
    on it — calling it directly returns instantly and the exit-code check reads a stale value
    from the previous command. That is not hypothetical: this step was first written with `&`
    and reported a green playthrough while the client was crashing on its first drawn frame.

    The build output is launched as `dotnet RatnaBay.dll` rather than through its apphost.
    The apphost in bin\ is framework-dependent, and under Start-Process it fails to locate the
    runtime — "Could not resolve CoreCLR path" — before any of our code runs. Going through
    the muxer sidesteps host resolution. The packaged build is self-contained and needs none
    of this, so it is launched directly.
#>
function Invoke-Script([string] $filePath, [string[]] $leadingArguments,
    [string] $workingDirectory, [string] $what, [string] $script = 'smoke.rbs') {
    $logPath = Join-Path $env:TEMP 'ratnabay-smoke.log'
    $arguments = $leadingArguments + @(
        '--yard', '--script', (Join-Path $root "Docs\scripts\$script"))

    $run = Start-Process -FilePath $filePath -ArgumentList $arguments `
        -WorkingDirectory $workingDirectory -NoNewWindow -Wait -PassThru `
        -RedirectStandardOutput $logPath

    if (Test-Path $logPath) {
        Get-Content $logPath | Select-Object -Last 12 | ForEach-Object { Write-Host "    $_" }
        Remove-Item $logPath -Force
    }

    if ($run.ExitCode -ne 0) {
        throw "smoke.rbs failed against $what (exit code $($run.ExitCode))."
    }
}

Push-Location $root
try {
    & (Join-Path $root 'build.ps1') -Configuration $Configuration
    Assert-LastExitCode 'build.ps1'

    Write-Host ''
    Write-Host '==> Validating content manifests' -ForegroundColor Cyan
    dotnet run --project 'tools\RatnaBay.Tools\RatnaBay.Tools.csproj' --configuration $Configuration --no-build -- validate
    Assert-LastExitCode 'RatnaBay.Tools validate'

    Write-Host ''
    Write-Host '==> Running the deterministic simulation' -ForegroundColor Cyan
    dotnet run --project 'tools\RatnaBay.Tools\RatnaBay.Tools.csproj' --configuration $Configuration --no-build -- sim
    Assert-LastExitCode 'RatnaBay.Tools sim'

    # The only step above this line that draws anything is none of them.
    #
    # Everything else here checks rules, content and generated pixels -- and all of it stayed
    # green through a null GraphicsDevice that crashed the game on the first frame it drew a
    # weapon. The ten fort-portrait checks in the packaged self-test passed too, because they
    # call PortraitForge.Render, which takes no device. A renderer is only exercised by running
    # the client, so the gate has to run the client.
    #
    # Needs a display. There is no CI; this is run on a dev machine.
    Write-Host ''
    Write-Host '==> Driving a scripted playthrough' -ForegroundColor Cyan
    $clientDir = Join-Path $root "src\RatnaBay.Game\bin\$Configuration\net9.0-windows\win-x64"
    $clientDll = Join-Path $clientDir 'RatnaBay.dll'
    if (-not (Test-Path $clientDll)) { throw "No client at $clientDll" }

    Invoke-Script 'dotnet' @($clientDll) $clientDir 'the build output'

    # And the same client again, walking instead of teleporting.
    #
    # smoke.rbs turns on noclip and god and `goto`es into the room, so it proves the room
    # exists and is fightable. walk.rbs holds W through the real controller and presses E on
    # the door, so it proves somebody can get there. The alpha's one outside player spent 110
    # minutes failing to, and every check in this file was green throughout.
    Write-Host ''
    Write-Host '==> Walking it, rather than teleporting' -ForegroundColor Cyan
    Invoke-Script 'dotnet' @($clientDll) $clientDir 'the build output, walking' 'walk.rbs'

    if ($Pack) {
        Write-Host ''
        Write-Host '==> Packaging' -ForegroundColor Cyan
        & (Join-Path $root 'publish.ps1') -Configuration $Configuration -SkipTests
        Assert-LastExitCode 'publish.ps1'

        # The same script again, against the packaged build rather than the build output.
        # Not redundant: the run above proves the code draws, this proves the artifact people
        # actually download does. Single-file publish has swallowed the bundled fonts once
        # before, and only a packaged run can see that.
        Write-Host ''
        Write-Host '==> Driving the build through the smoke script' -ForegroundColor Cyan
        $exe = Join-Path $root 'build\RatnaBay.exe'
        if (-not (Test-Path $exe)) { throw "No packaged build at $exe" }

        Invoke-Script $exe @() (Join-Path $root 'build') 'the packaged build'
    }

    Write-Host ''
    Write-Host 'verify.ps1 passed.' -ForegroundColor Green
}
finally {
    Pop-Location
}
