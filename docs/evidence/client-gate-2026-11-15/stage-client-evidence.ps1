# Stage the gate evidence where scp can fetch it without spaces in the path.
$out = "C:\Users\Hermes\gate-evidence"
New-Item -ItemType Directory -Force -Path $out | Out-Null
$g = "C:\Program Files (x86)\Steam\steamapps\common\Valheim"
Copy-Item -LiteralPath (Join-Path $g "BepInEx\LogOutput.log") -Destination (Join-Path $out "client-bepinex.log") -Force
if (Test-Path (Join-Path $g "BepInEx\BSBLogs\Husnisse.log")) {
    Copy-Item -LiteralPath (Join-Path $g "BepInEx\BSBLogs\Husnisse.log") -Destination (Join-Path $out "client-husnisse.log") -Force
}
$shots = Join-Path $env:USERPROFILE "Desktop\husnisse-captures"
foreach ($name in @("gate-05-scene","gate-06-alt-on","gate-07-before-alt","gate-08-raid-before","gate-09-raid-trolls","gate-10-bowl-ui","gate-11-bowl-ui","gate-13-after-relog")) {
    $src = Join-Path $shots ($name + ".png")
    if (Test-Path $src) { Copy-Item -LiteralPath $src -Destination (Join-Path $out ($name + ".png")) -Force }
}
Write-Host "== staged =="
Get-ChildItem $out | Select-Object Name,Length | Format-Table -AutoSize
