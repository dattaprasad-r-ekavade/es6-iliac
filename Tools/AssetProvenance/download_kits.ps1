$ErrorActionPreference = "Stop"
$dest = "D:\Projects\Elder Scrolls 6\TempDownloads"
$assets = "D:\Projects\Elder Scrolls 6\Assets\ThirdParty"
New-Item -ItemType Directory -Force -Path $dest | Out-Null

$downloads = @(
  @{
    Name = "kenney_pirate-kit.zip"
    Url  = "https://kenney.nl/media/pages/assets/pirate-kit/e6d4bb1525-1771333093/kenney_pirate-kit.zip"
    Out  = "$assets\Kenney\PirateKit"
  },
  @{
    # Modular medieval-ish extras from Kenney (if URL fails we skip)
    Name = "kenney_city-kit-commercial_2.1.zip"
    Url  = "https://kenney.nl/media/pages/assets/city-kit-commercial/a742d900eb-1753115042/kenney_city-kit-commercial_2.1.zip"
    Out  = "$assets\Kenney\CityKitCommercial"
  }
)

# Also try Quaternius Ultimate Fantasy RPG / Village from known mirrors if present later
foreach ($d in $downloads) {
  $zip = Join-Path $dest $d.Name
  if (-not (Test-Path $zip)) {
    Write-Host "Downloading $($d.Name) ..."
    Invoke-WebRequest -Uri $d.Url -OutFile $zip -UseBasicParsing
  } else {
    Write-Host "Exists $($d.Name)"
  }
  Write-Host "Extract $($d.Name) -> $($d.Out)"
  New-Item -ItemType Directory -Force -Path $d.Out | Out-Null
  Expand-Archive -Path $zip -DestinationPath "$dest\_extract_$($d.Name)" -Force
  # Flatten: copy Models / FBX folders into Out
  $extractRoot = Get-ChildItem "$dest\_extract_$($d.Name)" -Directory | Select-Object -First 1
  if (-not $extractRoot) { $extractRoot = Get-Item "$dest\_extract_$($d.Name)" }
  $fbx = Get-ChildItem $extractRoot.FullName -Recurse -Filter "*.fbx" -ErrorAction SilentlyContinue
  Write-Host "  FBX count: $($fbx.Count)"
  foreach ($f in $fbx) {
    $rel = $f.FullName.Substring($extractRoot.FullName.Length).TrimStart('\')
    # Prefer Models folder files at top of Out
    Copy-Item $f.FullName (Join-Path $d.Out $f.Name) -Force
  }
  # textures
  Get-ChildItem $extractRoot.FullName -Recurse -Include *.png,*.jpg,*.jpeg,*.tga -ErrorAction SilentlyContinue | ForEach-Object {
    Copy-Item $_.FullName (Join-Path $d.Out $_.Name) -Force -ErrorAction SilentlyContinue
  }
}

Write-Host "DONE downloads"
Get-ChildItem "$assets\Kenney" -Directory | Select-Object Name
