$ErrorActionPreference = "Continue"
$dest = "D:\Projects\Elder Scrolls 6\TempDownloads"
New-Item -ItemType Directory -Force -Path $dest | Out-Null

$pages = @(
  "https://kenney.nl/assets/city-kit-suburban",
  "https://kenney.nl/assets/city-kit-commercial",
  "https://kenney.nl/assets/city-kit-industrial",
  "https://kenney.nl/assets/pirate-kit",
  "https://quaternius.com/packs/medievalvillagemegakit.html",
  "https://quaternius.com/packs/ultimatemodularbuildings.html"
)

foreach ($page in $pages) {
  Write-Host "==== $page ===="
  try {
    $html = (Invoke-WebRequest -Uri $page -UseBasicParsing -TimeoutSec 45).Content
    $rx = [regex]'https://[^\s"<>]+\.zip'
    $zips = $rx.Matches($html) | ForEach-Object { $_.Value } | Select-Object -Unique
    if ($zips) { $zips | ForEach-Object { Write-Host "ZIP $_" } } else { Write-Host "(no zip urls)" }
    $rx2 = [regex]'href="([^"]*(?:download|itch|drive|dropbox|media/pages)[^"]*)"'
    $rx2.Matches($html) | ForEach-Object { $_.Groups[1].Value } | Select-Object -Unique -First 20 | ForEach-Object { Write-Host "HREF $_" }
  } catch {
    Write-Host "ERR $($_.Exception.Message)"
  }
}
