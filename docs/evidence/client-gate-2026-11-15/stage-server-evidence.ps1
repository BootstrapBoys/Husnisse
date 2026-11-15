# Stage the server side of the gate evidence.
$out = "V:\gate-evidence"
New-Item -ItemType Directory -Force -Path $out | Out-Null
Copy-Item -LiteralPath "V:\ValheimTest\BepInEx\LogOutput.log" -Destination (Join-Path $out "server-bepinex.log") -Force
Copy-Item -LiteralPath "V:\ValheimTest\BepInEx\BSBLogs\Husnisse.log" -Destination (Join-Path $out "server-husnisse.log") -Force
Copy-Item -LiteralPath "V:\ValheimTest\BepInEx\BSBLogs\Harness.log" -Destination (Join-Path $out "harness.log") -Force
Copy-Item -LiteralPath "V:\ValheimTest\logs\server.log" -Destination (Join-Path $out "server-game.log") -Force
Write-Host "== staged =="
Get-ChildItem $out | Select-Object Name,Length | Format-Table -AutoSize
