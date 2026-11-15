$p = Get-Process valheim -ErrorAction SilentlyContinue
if ($p) {
  $cpu1 = $p.TotalProcessorTime.TotalSeconds
  Start-Sleep -Seconds 5
  $p.Refresh()
  $cpu2 = $p.TotalProcessorTime.TotalSeconds
  Write-Output ('valheim pid ' + $p.Id + ' cpu_delta_5s=' + [math]::Round($cpu2 - $cpu1, 2) + ' ws_mb=' + [math]::Round($p.WorkingSet64 / 1MB) + ' responding=' + $p.Responding)
}
$log = Join-Path $env:USERPROFILE 'AppData\LocalLow\IronGate\Valheim\Player.log'
Write-Output ('player.log mtime ' + (Get-Item $log).LastWriteTime + ' size ' + (Get-Item $log).Length)
Get-Content -LiteralPath $log -Tail 8
Get-NetUDPEndpoint -ErrorAction SilentlyContinue | Where-Object { $_.OwningProcess -eq ($p.Id) } |
  Select-Object LocalAddress, LocalPort | Format-Table -AutoSize | Out-String
