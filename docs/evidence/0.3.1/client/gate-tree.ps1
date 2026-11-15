$src = 'V:\ValheimTest2'
$dst = 'V:\Gate031'

foreach ($pkg in 'BootstrapBoys-TestHarness', 'shudnal-ConditionalConfigSync', 'ValheimModding-YamlDotNet') {
  $from = Join-Path $src ('BepInEx\plugins\' + $pkg)
  $to = Join-Path $dst ('BepInEx\plugins\' + $pkg)
  if (Test-Path $from) {
    if (Test-Path $to) { Remove-Item -LiteralPath $to -Recurse -Force }
    Copy-Item -LiteralPath $from -Destination $to -Recurse -Force
    Write-Output ('copied ' + $pkg)
  } else { Write-Output ('missing source ' + $from) }
}

robocopy (Join-Path $src 'BepInEx\config') (Join-Path $dst 'BepInEx\config') /E /XF softstop.flag harness.cmd /NFL /NDL /NJH /NJS /R:1 /W:1 | Out-Null
Write-Output ('configs: ' + (@(Get-ChildItem (Join-Path $dst 'BepInEx\config') -Recurse -File).Count))

$hus = Join-Path $dst 'BepInEx\plugins\BootstrapBoys-Husnisse'
New-Item -ItemType Directory -Force -Path $hus | Out-Null
Copy-Item -LiteralPath (Join-Path $src 'BepInEx\plugins\BootstrapBoys-Husnisse\Husnisse.dll') -Destination (Join-Path $hus 'Husnisse.dll') -Force
Write-Output ('Husnisse.dll ' + (Get-FileHash (Join-Path $hus 'Husnisse.dll') -Algorithm SHA256).Hash)
Write-Output ('TestHarness.dll ' + (Get-FileHash (Join-Path $dst 'BepInEx\plugins\BootstrapBoys-TestHarness\TestHarness.dll') -Algorithm SHA256).Hash)
Get-ChildItem (Join-Path $dst 'BepInEx\plugins') -Directory | ForEach-Object { Write-Output ('plugin ' + $_.Name) }

$name = 'Valheim gate031 UDP 2956'
if (-not (Get-NetFirewallRule -DisplayName $name -ErrorAction SilentlyContinue)) {
  New-NetFirewallRule -DisplayName $name -Direction Inbound -Action Allow -Protocol UDP -LocalPort 2956-2957 -Profile Any `
    -Description '0.3.1 client gate tree V:\Gate031' | Out-Null
  Write-Output 'firewall rule added 2956-2957'
} else { Write-Output 'firewall rule present' }
