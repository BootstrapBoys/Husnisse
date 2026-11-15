$p = Get-Process -Name valheim -ErrorAction SilentlyContinue
if ($p) {
  $p | ForEach-Object { $_.CloseMainWindow() | Out-Null }
  Start-Sleep -Seconds 8
  $p = Get-Process -Name valheim -ErrorAction SilentlyContinue
  if ($p) { $p | Stop-Process -Force; Start-Sleep -Seconds 5; Write-Output 'force killed' }
  else { Write-Output 'closed' }
} else { Write-Output 'not running' }
