$log = Join-Path $env:USERPROFILE 'AppData\LocalLow\IronGate\Valheim\Player.log'
Write-Output '--- client failures ---'
Select-String -LiteralPath $log -Pattern 'fail|Fail|Disconnect|disconnect|timeout|Timeout|error|Error|Handshake|handshake' |
  Select-Object -Last 25 | ForEach-Object { Write-Output $_.Line }
