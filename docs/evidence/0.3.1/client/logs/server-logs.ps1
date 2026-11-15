$dst = 'C:\Users\WinGSM\stage\031'
New-Item -ItemType Directory -Force -Path $dst | Out-Null
foreach ($pair in @(@('V:\Gate031\BepInEx\BSBLogs\Husnisse.log', 'server-husnisse.log'),
                    @('V:\Gate031\BepInEx\LogOutput.log', 'server-bepinex.log'),
                    @('V:\Gate031\BepInEx\BSBLogs\Harness.log', 'server-harness.log'))) {
  if (Test-Path -LiteralPath $pair[0]) {
    Copy-Item -LiteralPath $pair[0] -Destination (Join-Path $dst $pair[1]) -Force
    Write-Output ('copied ' + $pair[1])
  } else { Write-Output ('missing ' + $pair[0]) }
}
