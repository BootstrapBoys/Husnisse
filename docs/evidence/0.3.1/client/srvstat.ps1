$p = Get-Process -Name valheim_server -ErrorAction SilentlyContinue
if (-not $p) { $p = Get-Process -Name valheim -ErrorAction SilentlyContinue }
Write-Output ('server pid: ' + ($p | ForEach-Object { $_.Id }) )
Get-NetUDPEndpoint -ErrorAction SilentlyContinue | Where-Object { $_.LocalPort -ge 2650 -and $_.LocalPort -le 2660 } |
  Select-Object LocalAddress, LocalPort, OwningProcess | Format-Table -AutoSize | Out-String
Write-Output '--- game log tail ---'
$log = 'V:\ValheimTest2\BepInEx\BSBLogs\Husnisse.log'
$g = 'V:\ValheimTest2\LogOutput.log'
if (Test-Path $g) { Get-Content $g -Tail 15 }
