# Closes the game (by the PID recorded at launch, or by name), rebuilds and deploys the mod, relaunches the game.
param([switch]$NoBuild, [int]$WaitSec = 30)
$g = "S:\steam\steamapps\common\RiftOfTheNecroDancerOSTVolume1"
$pidFile = "$env:TEMP\rodn_pid.txt"
if (Test-Path $pidFile) { $old = Get-Content $pidFile; Stop-Process -Id $old -Force -ErrorAction SilentlyContinue }
Get-Process RiftOfTheNecroDancer -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep 2
if (-not $NoBuild) {
    $out = dotnet build "$PSScriptRoot\..\RiftAccess\RiftAccess.csproj" -c Release 2>&1
    $errs = $out | Select-String -Pattern "error (CS|MSB)" | Select-Object -Unique
    if ($errs) { $errs; exit 1 }
}
$p = Start-Process -FilePath "$g\RiftOfTheNecroDancer.exe" -WorkingDirectory $g -PassThru
Set-Content $pidFile $p.Id
Start-Sleep $WaitSec
"pid $($p.Id)"
Get-Content "$g\BepInEx\LogOutput.log" | Select-String -Pattern "Rift Access\]|Exception" | Select-Object -Last 12 | ForEach-Object { $_.Line.Substring(0, [Math]::Min(300, $_.Line.Length)) }
