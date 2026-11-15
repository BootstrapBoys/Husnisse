$dst = 'V:\Gate031'
Write-Output ('harness ' + (Get-FileHash "$dst\BepInEx\plugins\BootstrapBoys-TestHarness\TestHarness.dll" -Algorithm SHA256).Hash)
$p = Get-CimInstance Win32_Process | Where-Object { $_.CommandLine -and $_.CommandLine.Contains($dst) }
Write-Output ('server pid ' + ($p | ForEach-Object { $_.ProcessId }))
if (Test-Path "$dst\logs\server.log") { Write-Output ('game log mtime ' + (Get-Item "$dst\logs\server.log").LastWriteTime) }
if (Test-Path "$dst\BepInEx\LogOutput.log") {
  Select-String -Path "$dst\BepInEx\LogOutput.log" -Pattern 'content registered|harness ready|harness patch' | Select-Object -Last 5 | ForEach-Object { Write-Output $_.Line }
}
Write-Output '--- harness tail ---'
if (Test-Path "$dst\BepInEx\BSBLogs\Harness.log") { Get-Content "$dst\BepInEx\BSBLogs\Harness.log" -Tail 6 }
