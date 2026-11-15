# Install the order-gate build + probe on the client, verify hashes, reset the command file.
# The DLLs come from C:\Users\Hermes\stage, uploaded by the card (scp from artifacts/debug).
$ErrorActionPreference = "Stop"
$g = "C:\Program Files (x86)\Steam\steamapps\common\Valheim"
$stage = "C:\Users\Hermes\stage"

$proc = Get-Process -Name valheim -ErrorAction SilentlyContinue
if ($proc) { throw ("valheim.exe is running (pid " + ($proc.Id -join ",") + "); the DLL is mapped - take the client when it is free") }

$hus = Join-Path $g "BepInEx\plugins\BootstrapBoys-Husnisse"
New-Item -ItemType Directory -Force -Path $hus | Out-Null
Copy-Item -LiteralPath (Join-Path $stage "Husnisse.dll") -Destination (Join-Path $hus "Husnisse.dll") -Force

$probe = Join-Path $g "BepInEx\plugins\BootstrapBoys-GateClientProbe"
New-Item -ItemType Directory -Force -Path $probe | Out-Null
Copy-Item -LiteralPath (Join-Path $stage "HusnisseGateClientProbe.dll") -Destination (Join-Path $probe "HusnisseGateClientProbe.dll") -Force

Write-Host "== installed =="
foreach ($p in @((Join-Path $hus "Husnisse.dll"), (Join-Path $probe "HusnisseGateClientProbe.dll"))) {
    $h = (Get-FileHash -Algorithm SHA256 -LiteralPath $p).Hash
    Write-Host ("  " + $p + " " + $h)
}

Set-Content -Path (Join-Path $env:USERPROFILE "husnisse-probe.cmd") -Value "# gate session" -Encoding ASCII
Write-Host "== plugin folders =="
Get-ChildItem (Join-Path $g "BepInEx\plugins") -Directory | Select-Object -ExpandProperty Name
