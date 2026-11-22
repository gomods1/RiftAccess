# Builds the mod and restages dist\payload, the files the release workflow ships:
#   BepInEx\plugins\RiftAccess\ (RiftAccess.dll, prism.dll + LICENSES + NOTICE, Sounds\*.wav) and RiftAccess-README.md.
# CI cannot build the plugin (it references the game's own DLLs), so dist\payload is committed.
# CI adds the official BepInEx 5.4.23.5 x64 zip (checked by SHA-256) and publishes RiftAccess-vX.Y.Z.zip.
# Also writes release\RiftAccess-vX.Y.Z.zip locally, laid out exactly like the CI zip.
# Release: set <Version> in RiftAccess.csproj and Plugin.cs, add a "## X.Y.Z" section to CHANGELOG.md, then
#   pwsh tools/package.ps1; git add -A; git commit -m "Release X.Y.Z"; git tag vX.Y.Z; git push --follow-tags
$ErrorActionPreference = "Stop"
$root = Resolve-Path "$PSScriptRoot\.."
$version = ([xml](Get-Content "$root\RiftAccess\RiftAccess.csproj")).Project.PropertyGroup.Version | Select-Object -First 1
$pluginVersion = (Select-String -Path "$root\RiftAccess\Plugin.cs" -Pattern 'Version = "([^"]+)"').Matches[0].Groups[1].Value
if ($version -ne $pluginVersion) { throw "RiftAccess.csproj says $version but Plugin.cs says $pluginVersion" }

python "$root\tools\check-lang.py"
if ($LASTEXITCODE -ne 0) { throw "check-lang.py failed" }
$out = dotnet build "$root\RiftAccess\RiftAccess.csproj" -c Release 2>&1
if ($LASTEXITCODE -ne 0 -or ($out | Select-String -Pattern "error (CS|MSB)")) { $out | Select-String -Pattern "error"; throw "build failed" }

$payload = Join-Path $root "dist\payload"
if (Test-Path $payload) { Remove-Item $payload -Recurse -Force }
$plugin = Join-Path $payload "BepInEx\plugins\RiftAccess"
New-Item -ItemType Directory -Force "$plugin\Sounds" | Out-Null
Copy-Item "$root\RiftAccess\bin\Release\RiftAccess.dll" $plugin
Copy-Item "$root\vendor\prism\prism.dll" $plugin
Copy-Item "$root\vendor\prism\LICENSES" $plugin -Recurse
Copy-Item "$root\vendor\prism\NOTICE" $plugin
Copy-Item "$root\sounds\cues\*.wav" "$plugin\Sounds"
Copy-Item "$root\README.md" (Join-Path $payload "RiftAccess-README.md")

# Local zip, same layout as the one CI publishes: BepInEx (without its changelog) + payload.
$stage = Join-Path $root "release\stage"
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Force $stage | Out-Null
Expand-Archive "$root\vendor\BepInEx_win_x64_5.4.23.5.zip" -DestinationPath $stage
Remove-Item (Join-Path $stage "changelog.txt") -Force -ErrorAction SilentlyContinue
Copy-Item "$payload\*" $stage -Recurse -Force
$zip = Join-Path $root "release\RiftAccess-v$version.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path "$stage\*" -DestinationPath $zip
Remove-Item $stage -Recurse -Force
"staged $payload"
"wrote $zip"
