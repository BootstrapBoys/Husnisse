$ErrorActionPreference = "Continue"
$g = "C:\Program Files (x86)\Steam\steamapps\common\Valheim"
foreach ($p in @(
    "$g\BepInEx\plugins\BootstrapBoys-Husnisse\Husnisse.dll",
    "$g\BepInEx\plugins\BootstrapBoys-GateClientProbe\HusnisseGateClientProbe.dll")) {
    if (Test-Path -LiteralPath $p) {
        $f = Get-Item -LiteralPath $p
        Write-Output ("dll " + $p + " " + $f.LastWriteTime.ToString("MM-dd HH:mm") + " " + (Get-FileHash -Algorithm SHA256 -LiteralPath $p).Hash)
    } else { Write-Output ("MISSING " + $p) }
}
Write-Output "== client plugin folders =="
(Get-ChildItem "$g\BepInEx\plugins" -Directory | Select-Object -ExpandProperty Name) -join ','
Write-Output "== stage =="
if (Test-Path 'C:\Users\Hermes\stage') { Get-ChildItem 'C:\Users\Hermes\stage' | Select-Object Name,Length,LastWriteTime | Format-Table -AutoSize | Out-String -Width 200 } else { 'no stage' }
