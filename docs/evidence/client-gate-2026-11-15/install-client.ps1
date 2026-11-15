# Install the gate build + probe on the client, verify hashes, clear the command file.
$ErrorActionPreference = "Stop"
$g = "C:\Program Files (x86)\Steam\steamapps\common\Valheim"
$stage = "C:\Users\Hermes\stage"

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

$cmd = Join-Path $env:USERPROFILE "husnisse-probe.cmd"
Set-Content -Path $cmd -Value "# gate session" -Encoding ASCII
Write-Host ("cmd file reset: " + $cmd)

Write-Host "== plugin folders =="
Get-ChildItem (Join-Path $g "BepInEx\plugins") -Directory | Select-Object -ExpandProperty Name
