# Quit the test client (it has no window message loop reachable from session 0, so the fallback is
# a force stop - this is a disposable tester on a VM), install the gate build + probe from stage,
# and reset the probe command file. Launch is a separate step so the server can be READY first.
$ErrorActionPreference = "Stop"
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
        Start-Sleep -Seconds 6
        Write-Output "force killed"
    }
}
if (Get-Process -Name valheim -ErrorAction SilentlyContinue) { throw "valheim.exe still running" }

$hus = Join-Path $g "BepInEx\plugins\BootstrapBoys-Husnisse"
New-Item -ItemType Directory -Force -Path $hus | Out-Null
Copy-Item -LiteralPath (Join-Path $stage "Husnisse.dll") -Destination (Join-Path $hus "Husnisse.dll") -Force
$probe = Join-Path $g "BepInEx\plugins\BootstrapBoys-GateClientProbe"
New-Item -ItemType Directory -Force -Path $probe | Out-Null
Copy-Item -LiteralPath (Join-Path $stage "HusnisseGateClientProbe.dll") -Destination (Join-Path $probe "HusnisseGateClientProbe.dll") -Force

Write-Output "== installed =="
foreach ($p in @((Join-Path $hus "Husnisse.dll"), (Join-Path $probe "HusnisseGateClientProbe.dll"))) {
    Write-Output ("  " + $p + " " + (Get-FileHash -Algorithm SHA256 -LiteralPath $p).Hash)
}
Set-Content -Path (Join-Path $env:USERPROFILE "husnisse-probe.cmd") -Value "# gate session" -Encoding ASCII
Write-Output "cmd file reset"
