# Remove the gate probe and the staging folders; leave the client on its base plugin set.
$g = "C:\Program Files (x86)\Steam\steamapps\common\Valheim"
$probe = Join-Path $g "BepInEx\plugins\BootstrapBoys-GateClientProbe"
if (Test-Path $probe) { Remove-Item -Recurse -Force $probe }
foreach ($d in @("C:\Users\Hermes\stage", "C:\Users\Hermes\gate-evidence")) {
    if (Test-Path $d) { Remove-Item -Recurse -Force $d }
}
$cmd = Join-Path $env:USERPROFILE "husnisse-probe.cmd"
if (Test-Path $cmd) { Set-Content -Path $cmd -Value "# idle" -Encoding ASCII }

Write-Host "== probe removed =="
Write-Host ("probe present: " + (Test-Path $probe))
Write-Host "== plugin folders =="
Get-ChildItem (Join-Path $g "BepInEx\plugins") -Directory | Select-Object -ExpandProperty Name
Write-Host ("valheim running: " + [bool](Get-Process valheim -ErrorAction SilentlyContinue))
