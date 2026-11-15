$ErrorActionPreference = "Continue"
$log = "C:\Program Files (x86)\Steam\steamapps\common\Valheim\BepInEx\LogOutput.log"
Write-Output ("now     " + (Get-Date -Format "HH:mm:ss"))
Write-Output ("logmtime " + (Get-Item $log).LastWriteTime.ToString("HH:mm:ss") + " size=" + (Get-Item $log).Length)
$p = Get-Process -Name valheim -ErrorAction SilentlyContinue
if ($p) { Write-Output ("valheim pid=" + ($p.Id -join ",")) } else { Write-Output "valheim NOT running" }
$cmd = Join-Path $env:USERPROFILE "husnisse-probe.cmd"
if (Test-Path $cmd) {
    Write-Output ("probe cmd mtime " + (Get-Item $cmd).LastWriteTime.ToString("HH:mm:ss"))
    Write-Output "--- husnisse-probe.cmd ---"
    Get-Content $cmd
} else { Write-Output "probe cmd file MISSING" }
$st = "C:\Users\Hermes\stage\cmd.txt"
if (Test-Path $st) {
    Write-Output ("stage cmd.txt mtime " + (Get-Item $st).LastWriteTime.ToString("HH:mm:ss"))
    Write-Output "--- stage cmd.txt ---"
    Get-Content $st
} else { Write-Output "stage cmd.txt MISSING" }
