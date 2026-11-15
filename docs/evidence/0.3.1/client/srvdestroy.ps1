Write-Output '--- game server.log tail ---'
Get-Content 'V:\Gate031\logs\server.log' -Tail 20
Write-Output '--- BepInEx LogOutput tail (harness/scene/ZNet) ---'
Select-String -Path 'V:\Gate031\BepInEx\LogOutput.log' -Pattern 'scenario loaded|scenario done|cleanup|Destroy|destroy|zone|Zone|cull|Cull|error|Error|Warn' |
  Select-Object -Last 30 | ForEach-Object { Write-Output $_.Line }
Write-Output '--- harness log size/mtime ---'
Get-Item 'V:\Gate031\BepInEx\BSBLogs\Harness.log' | Select-Object Length, LastWriteTime | Format-Table -AutoSize | Out-String
Write-Output '--- config harness.cmd ---'
if (Test-Path 'V:\Gate031\BepInEx\config\harness.cmd') { Get-Content 'V:\Gate031\BepInEx\config\harness.cmd' } else { 'no command file' }
