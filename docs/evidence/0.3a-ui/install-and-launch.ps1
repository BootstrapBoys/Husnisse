# Stop the client, install the 0.3a build + probe from unique stage names, reset the probe file,
# then relaunch. Unique names: the shared stage folder also carries other sessions' builds.
$ErrorActionPreference = "Continue"
$g = "C:\Program Files (x86)\Steam\steamapps\common\Valheim"
$stage = "C:\Users\Hermes\stage"

$proc = Get-Process -Name valheim -ErrorAction SilentlyContinue
if ($proc) {
    Write-Output ("stopping valheim pid " + ($proc.Id -join ","))
    $proc | ForEach-Object { $_.CloseMainWindow() | Out-Null }
    Start-Sleep -Seconds 8
    $proc = Get-Process -Name valheim -ErrorAction SilentlyContinue
    if ($proc) {
        $proc | Stop-Process -Force
        Start-Sleep -Seconds 5
        Write-Output "force killed"
    }
}

$hus = Join-Path $g "BepInEx\plugins\BootstrapBoys-Husnisse"
New-Item -ItemType Directory -Force -Path $hus | Out-Null
Copy-Item -LiteralPath (Join-Path $stage "Husnisse-03a.dll") -Destination (Join-Path $hus "Husnisse.dll") -Force
$probe = Join-Path $g "BepInEx\plugins\BootstrapBoys-GateClientProbe"
New-Item -ItemType Directory -Force -Path $probe | Out-Null
Copy-Item -LiteralPath (Join-Path $stage "HusnisseGateClientProbe.dll") -Destination (Join-Path $probe "HusnisseGateClientProbe.dll") -Force

Write-Output ("husnisse " + (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $hus "Husnisse.dll")).Hash)
Write-Output ("probe " + (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $probe "HusnisseGateClientProbe.dll")).Hash)

Set-Content -Path (Join-Path $env:USERPROFILE "husnisse-probe.cmd") -Value "# idle" -Encoding ASCII
Start-ScheduledTask -TaskName "HusnisseLegsLaunch"
Start-Sleep -Seconds 3
Get-CimInstance Win32_Process -Filter "name like '%valheim%'" | Select-Object ProcessId, Name, SessionId | Format-Table -AutoSize
