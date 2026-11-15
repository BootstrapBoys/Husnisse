# Install THIS card's build and probe on the client, from the uniquely named stage files.
# The canonical stage names (Husnisse.dll / HusnisseGateClientProbe.dll) are shared with other
# cards and get overwritten: an install that read them put build 91B9AF22 on the client on
# 2026-11-15 08:44. Only the -orders-<hash> names are this card's.
$ErrorActionPreference = "Stop"
$g = "C:\Program Files (x86)\Steam\steamapps\common\Valheim"
$stage = "C:\Users\Hermes\stage"

$proc = Get-Process -Name valheim -ErrorAction SilentlyContinue
if ($proc) { throw ("valheim.exe is running (pid " + ($proc.Id -join ",") + "); the DLL is mapped") }

$hus = Join-Path $g "BepInEx\plugins\BootstrapBoys-Husnisse"
New-Item -ItemType Directory -Force -Path $hus | Out-Null
Copy-Item -LiteralPath (Join-Path $stage "Husnisse-orders-165f728b.dll") -Destination (Join-Path $hus "Husnisse.dll") -Force

$probe = Join-Path $g "BepInEx\plugins\BootstrapBoys-GateClientProbe"
New-Item -ItemType Directory -Force -Path $probe | Out-Null
Copy-Item -LiteralPath (Join-Path $stage "HusnisseGateClientProbe-orders-c00c6bd4.dll") -Destination (Join-Path $probe "HusnisseGateClientProbe.dll") -Force

foreach ($p in @((Join-Path $hus "Husnisse.dll"), (Join-Path $probe "HusnisseGateClientProbe.dll"))) {
    Write-Host ((Split-Path $p -Leaf) + " " + (Get-FileHash -Algorithm SHA256 -LiteralPath $p).Hash)
}
Set-Content -Path (Join-Path $env:USERPROFILE "husnisse-probe.cmd") -Value "# orders gate session" -Encoding ASCII
